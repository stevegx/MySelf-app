import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { ActiveWorkoutScreen } from "./ActiveWorkoutScreen";

const AUTH = { accessToken: "test-token", user: { id: "u1", username: "demo", email: "demo@example.com" } };

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
    if (url.endsWith("/set-logs") && method === "POST") {
      return json({ id: "set1", completedAt: "2026-09-04T09:05:00Z", skippedAt: null });
    }
    if (url.endsWith("/complete") && method === "POST") {
      return json({ ...sessionWithOneSet(), status: "Completed" });
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

  it("finishes the workout", async () => {
    const fetchSpy = installFetch({ active: sessionWithOneSet() });
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Finish workout" }));

    const completeCall = fetchSpy.mock.calls.find(([u, i]) => String(u).endsWith("/complete") && i?.method === "POST");
    expect(completeCall).toBeTruthy();
    expect(JSON.parse((completeCall![1] as RequestInit).body as string)).toHaveProperty("localDate");
  });
});
