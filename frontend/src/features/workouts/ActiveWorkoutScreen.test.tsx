import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { ActiveWorkoutScreen } from "./ActiveWorkoutScreen";

const AUTH = { accessToken: "test-token", user: { id: "u1", username: "demo", email: "demo@example.com" } };

const SUMMARY = {
  durationSeconds: null,
  completedSetCount: 0,
  skippedSetCount: 0,
  totalReps: 0,
  totalVolumeKg: 0,
};

function makeSet(id: string, over: Partial<Record<string, unknown>> = {}) {
  return {
    id,
    sortOrder: 0,
    kind: "Standard",
    isAmrap: false,
    targetToFailure: false,
    targetRepsMin: 8,
    targetRepsMax: 8,
    targetWeightKg: 100,
    targetRir: 2,
    weightKg: null,
    addedWeightKg: null,
    assistanceKg: null,
    reps: null,
    durationSeconds: null,
    distanceMeters: null,
    rir: null,
    reachedFailure: false,
    completedAt: null,
    skippedAt: null,
    skippedReason: null,
    ...over,
  };
}

function sessionWithOneSet(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: "s1",
    sourceDayId: "d1",
    dayName: "Legs A",
    programName: "PPL",
    status: "InProgress",
    startedAt: "2026-09-04T09:00:00Z",
    completedAt: null,
    performedOnLocalDate: null,
    notes: null,
    summary: SUMMARY,
    newPersonalRecords: [],
    exercises: [
      {
        id: "e1",
        exerciseId: "x1",
        exerciseName: "Back Squat",
        trackingMode: "WeightAndReps",
        sortOrder: 0,
        supersetGroupSnapshotId: null,
        supersetMemberOrder: 0,
        sets: [
          {
            id: "set1",
            sortOrder: 0,
            kind: "Standard",
            isAmrap: false,
            targetToFailure: false,
            targetRepsMin: 8,
            targetRepsMax: 8,
            targetWeightKg: 100,
            targetRir: 2,
            weightKg: null,
            addedWeightKg: null,
            assistanceKg: null,
            reps: null,
            durationSeconds: null,
            distanceMeters: null,
            rir: null,
            reachedFailure: false,
            completedAt: null,
            skippedAt: null,
            skippedReason: null,
          },
        ],
      },
    ],
    ...overrides,
  };
}

function installFetch(opts: { active?: unknown } = {}) {
  const spy = vi.fn<typeof fetch>((input, init) => {
    const url = typeof input === "string" ? input : input.toString();
    const method = init?.method ?? "GET";
    const json = (body: unknown, status = 200) =>
      Promise.resolve(new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } }));

    if (url.includes("/auth/refresh")) return json(AUTH);
    if (url.endsWith("/workout-sessions/active") && method === "GET") {
      return opts.active === undefined
        ? Promise.resolve(new Response(null, { status: 404 }))
        : json(opts.active);
    }
    if (url.includes("/history")) {
      return json({ exerciseId: "x1", exerciseName: "Back Squat", personalRecords: [], sessions: [] });
    }
    if (url.includes("/api/v1/exercises?")) {
      return json({
        items: [{ id: "x2", name: "Leg Press", category: "Legs", defaultTrackingMode: "WeightAndReps", primaryMuscles: ["Quads"], secondaryMuscles: ["Glutes"], equipment: ["Cable machine"] }],
        page: 1,
        pageSize: 25,
        total: 1,
      });
    }
    if (url.endsWith("/exercises") && method === "POST") {
      return json({ ...sessionWithOneSet(), exercises: [] });
    }
    if (url.endsWith("/set-logs") && method === "POST") {
      return json({ id: "set1", completedAt: "2026-09-04T09:05:00Z", skippedAt: null });
    }
    if (url.endsWith("/complete") && method === "POST") {
      return json({
        ...sessionWithOneSet(),
        status: "Completed",
        completedAt: "2026-09-04T10:00:00Z",
        summary: { durationSeconds: 3600, completedSetCount: 1, skippedSetCount: 0, totalReps: 8, totalVolumeKg: 800 },
        newPersonalRecords: [{ type: "HeaviestWeight", value: 100, weightKg: 100, reps: 8, achievedOn: "2026-09-04" }],
      });
    }
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return spy;
}

function renderScreen() {
  const router = createMemoryRouter(
    [
      { path: "/", element: <ActiveWorkoutScreen /> },
      { path: "/workouts/builder", element: <div>Builder</div> },
      { path: "/workouts/history", element: <div>History screen</div> },
    ],
    { initialEntries: ["/"] },
  );
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("ActiveWorkoutScreen", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("shows the empty state when nothing is in progress", async () => {
    installFetch();
    renderScreen();
    await waitFor(() => expect(screen.getByText("Nothing in progress right now.")).toBeInTheDocument());
  });

  it("logs a set with the entered values", async () => {
    const fetchSpy = installFetch({ active: sessionWithOneSet() });
    const user = userEvent.setup();
    renderScreen();

    await screen.findByText("Back Squat");
    await user.type(screen.getByLabelText("Set 1 Weight (kg)"), "102.5");
    await user.type(screen.getByLabelText("Set 1 Reps"), "8");
    await user.click(screen.getByRole("button", { name: "Log set" }));

    const logCall = fetchSpy.mock.calls.find(([u, i]) => String(u).endsWith("/set-logs") && i?.method === "POST");
    expect(logCall).toBeTruthy();
    expect(JSON.parse((logCall![1] as RequestInit).body as string)).toMatchObject({
      setLogId: "set1",
      weightKg: 102.5,
      reps: 8,
      reachedFailure: false,
    });
  });

  it("keeps Finish disabled until at least one set is logged or skipped", async () => {
    installFetch({ active: sessionWithOneSet() });
    renderScreen();

    // Nothing acted on yet — the empty-workout guard blocks finishing.
    expect(await screen.findByRole("button", { name: "Finish workout" })).toBeDisabled();
    expect(screen.getByText(/log or skip at least one to finish/i)).toBeInTheDocument();
  });

  it("finishes the workout", async () => {
    const base = sessionWithOneSet();
    const done = {
      ...base,
      exercises: [
        {
          ...base.exercises[0],
          sets: [makeSet("set1", { sortOrder: 0, weightKg: 100, reps: 8, completedAt: "2026-09-04T09:05:00Z" })],
        },
      ],
    };
    const fetchSpy = installFetch({ active: done });
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Finish workout" }));

    const completeCall = fetchSpy.mock.calls.find(([u, i]) => String(u).endsWith("/complete") && i?.method === "POST");
    expect(completeCall).toBeTruthy();
    expect(JSON.parse((completeCall![1] as RequestInit).body as string)).toHaveProperty("localDate");

    // Completion panel: summary + the new PR, then "View history".
    expect(await screen.findByText("Workout complete")).toBeInTheDocument();
    expect(screen.getByText(/Heaviest weight/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "View history" }));
    expect(await screen.findByText("History screen")).toBeInTheDocument();
  });

  it("starts a rest countdown after logging a set", async () => {
    const base = sessionWithOneSet();
    const withRest = {
      ...base,
      exercises: [{ ...base.exercises[0], restSeconds: 45 }],
    };
    installFetch({ active: withRest });
    const user = userEvent.setup();
    renderScreen();

    await user.type(await screen.findByLabelText("Set 1 Weight (kg)"), "100");
    await user.type(screen.getByLabelText("Set 1 Reps"), "8");
    await user.click(screen.getByRole("button", { name: "Log set" }));

    expect(await screen.findByText(/^Rest 0:4[0-9]$/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Skip rest" }));
    expect(screen.queryByText(/^Rest /)).not.toBeInTheDocument();
  });

  it("adds an exercise to the running session", async () => {
    const fetchSpy = installFetch({ active: sessionWithOneSet() });
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "+ Add exercise" }));
    await user.type(await screen.findByPlaceholderText(/Search the exercise catalogue/i), "leg");
    await user.click(await screen.findByRole("button", { name: /Leg Press/ }));

    const addCall = fetchSpy.mock.calls.find(([u, i]) => String(u).endsWith("/exercises") && i?.method === "POST");
    expect(addCall).toBeTruthy();
    expect(JSON.parse((addCall![1] as RequestInit).body as string)).toMatchObject({ exerciseId: "x2" });
  });

  it("copies the previous completed set's values into a pending set", async () => {
    const twoSetSession = {
      ...sessionWithOneSet(),
      exercises: [
        {
          id: "e1",
          exerciseId: "x1",
          exerciseName: "Back Squat",
          trackingMode: "WeightAndReps",
          sortOrder: 0,
          supersetGroupSnapshotId: null,
          supersetMemberOrder: 0,
          sets: [
            makeSet("setA", { sortOrder: 0, weightKg: 100, reps: 8, completedAt: "2026-09-04T09:05:00Z" }),
            makeSet("setB", { sortOrder: 1 }),
          ],
        },
      ],
    };
    installFetch({ active: twoSetSession });
    const user = userEvent.setup();
    renderScreen();

    await screen.findByText("Back Squat");
    await user.click(screen.getByRole("button", { name: "Copy previous" }));

    expect((screen.getByLabelText("Set 2 Weight (kg)") as HTMLInputElement).value).toBe("100");
    expect((screen.getByLabelText("Set 2 Reps") as HTMLInputElement).value).toBe("8");
  });
});
