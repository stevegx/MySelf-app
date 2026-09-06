import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { SessionEditScreen } from "./SessionEditScreen";

const AUTH = { accessToken: "test-token", user: { id: "u1", username: "demo", email: "demo@example.com" } };

function completedSession(over: Record<string, unknown> = {}) {
  return {
    id: "s1",
    sourceDayId: "d1",
    dayName: "Legs A",
    programName: "PPL",
    status: "Completed",
    startedAt: "2026-09-04T09:00:00Z",
    completedAt: "2026-09-04T10:00:00Z",
    performedOnLocalDate: "2026-09-04",
    notes: null,
    wasEdited: false,
    summary: { durationSeconds: 3600, completedSetCount: 1, skippedSetCount: 0, totalReps: 8, totalVolumeKg: 800 },
    newPersonalRecords: [],
    exercises: [
      {
        id: "e1",
        exerciseId: "x1",
        exerciseName: "Back Squat",
        trackingMode: "WeightAndReps",
        sortOrder: 0,
        restSeconds: null,
        supersetGroupSnapshotId: null,
        supersetMemberOrder: 0,
        supersetRestAfterRoundSeconds: null,
        sets: [
          {
            id: "set1", sortOrder: 0, kind: "Standard", isAmrap: false, targetToFailure: false,
            targetRepsMin: 8, targetRepsMax: 8, targetWeightKg: 100, targetRir: 2,
            weightKg: 100, addedWeightKg: null, assistanceKg: null, reps: 8,
            durationSeconds: null, distanceMeters: null, rir: null, reachedFailure: false,
            completedAt: "2026-09-04T09:20:00Z", skippedAt: null, skippedReason: null,
          },
        ],
      },
    ],
    ...over,
  };
}

function installFetch(session: unknown) {
  const spy = vi.fn<typeof fetch>((input, init) => {
    const url = typeof input === "string" ? input : input.toString();
    const method = init?.method ?? "GET";
    const json = (b: unknown, s = 200) =>
      Promise.resolve(new Response(JSON.stringify(b), { status: s, headers: { "content-type": "application/json" } }));
    if (url.includes("/auth/refresh")) return json(AUTH);
    if (url.match(/\/workout-sessions\/s1$/) && method === "GET") return json(session);
    if (url.endsWith("/set-logs") && method === "POST") return json({ id: "set1", completedAt: "2026-09-04T09:21:00Z", skippedAt: null });
    if (url.endsWith("/skip-set") && method === "POST") return json({ id: "set1", completedAt: null, skippedAt: "2026-09-04T09:21:00Z" });
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return spy;
}

function renderAt() {
  const router = createMemoryRouter(
    [
      { path: "/workouts/session/:id", element: <SessionEditScreen /> },
      { path: "/workouts/history", element: <div>History screen</div> },
      { path: "/workouts/active", element: <div>Active screen</div> },
    ],
    { initialEntries: ["/workouts/session/s1"] },
  );
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("SessionEditScreen", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("re-logs a set with a corrected value", async () => {
    const spy = installFetch(completedSession());
    const user = userEvent.setup();
    renderAt();

    await waitFor(() => expect(screen.getByText("Back Squat")).toBeInTheDocument());
    const weight = screen.getByLabelText("Set 1 Weight (kg)");
    await user.clear(weight);
    await user.type(weight, "110");
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => {
      const call = spy.mock.calls.find(([u, i]) => String(u).endsWith("/set-logs") && i?.method === "POST");
      expect(call).toBeTruthy();
      expect(JSON.parse((call![1] as RequestInit).body as string)).toMatchObject({ setLogId: "set1", weightKg: 110, reps: 8 });
    });
  });

  it("shows the Edited badge when the session was already edited", async () => {
    installFetch(completedSession({ wasEdited: true }));
    renderAt();
    await waitFor(() => expect(screen.getByText("Edited")).toBeInTheDocument());
  });

  it("refuses to edit a session that isn't completed", async () => {
    installFetch(completedSession({ status: "InProgress" }));
    renderAt();
    await waitFor(() =>
      expect(screen.getByText(/only edit a completed workout/i)).toBeInTheDocument(),
    );
  });
});
