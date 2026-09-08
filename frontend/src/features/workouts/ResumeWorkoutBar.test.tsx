import { render, screen, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { ResumeWorkoutBar } from "./ResumeWorkoutBar";

const AUTH = { accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } };

const ACTIVE = {
  id: "s1",
  sourceDayId: "d1",
  dayName: "Push",
  programName: "PPL",
  status: "InProgress",
  startedAt: "2026-09-04T09:00:00Z",
  completedAt: null,
  performedOnLocalDate: null,
  notes: null,
  wasEdited: false,
  summary: { durationSeconds: null, completedSetCount: 0, skippedSetCount: 0, totalReps: 0, totalVolumeKg: 0 },
  newPersonalRecords: [],
  exercises: [
    { id: "e1", exerciseId: "x1", exerciseName: "Bench", trackingMode: "WeightAndReps", sortOrder: 0, restSeconds: null, supersetGroupSnapshotId: null, supersetMemberOrder: 0, supersetRestAfterRoundSeconds: null, sets: [
      { id: "a", sortOrder: 0, kind: "Standard", isAmrap: false, targetToFailure: false, targetRepsMin: 8, targetRepsMax: 8, targetWeightKg: 60, targetRir: 2, weightKg: 60, addedWeightKg: null, assistanceKg: null, reps: 8, durationSeconds: null, distanceMeters: null, rir: null, reachedFailure: false, completedAt: "2026-09-04T09:05:00Z", skippedAt: null, skippedReason: null },
      { id: "b", sortOrder: 1, kind: "Standard", isAmrap: false, targetToFailure: false, targetRepsMin: 8, targetRepsMax: 8, targetWeightKg: 60, targetRir: 2, weightKg: null, addedWeightKg: null, assistanceKg: null, reps: null, durationSeconds: null, distanceMeters: null, rir: null, reachedFailure: false, completedAt: null, skippedAt: null, skippedReason: null },
    ] },
  ],
};

function installFetch(active: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn<typeof fetch>((input) => {
      const url = String(input);
      const json = (b: unknown, s = 200) =>
        Promise.resolve(new Response(JSON.stringify(b), { status: s, headers: { "content-type": "application/json" } }));
      if (url.includes("/auth/refresh")) return json(AUTH);
      if (url.endsWith("/workout-sessions/active")) return active === null ? Promise.resolve(new Response(null, { status: 404 })) : json(active);
      return Promise.resolve(new Response(null, { status: 404 }));
    }),
  );
}

function renderAt(path: string) {
  const router = createMemoryRouter(
    [
      { path: "/dashboard", element: <ResumeWorkoutBar /> },
      { path: "/workouts/active", element: <ResumeWorkoutBar /> },
    ],
    { initialEntries: [path] },
  );
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("ResumeWorkoutBar", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("links back to the active workout with progress", async () => {
    installFetch(ACTIVE);
    renderAt("/dashboard");
    const link = await screen.findByRole("link", { name: /Resume workout/ });
    expect(link).toHaveAttribute("href", "/workouts/active");
    expect(link).toHaveTextContent("Push · 1/2");
  });

  it("stays hidden on the active-workout screen", async () => {
    installFetch(ACTIVE);
    renderAt("/workouts/active");
    await waitFor(() => {});
    expect(screen.queryByRole("link", { name: /Resume workout/ })).not.toBeInTheDocument();
  });

  it("stays hidden when nothing is in progress", async () => {
    installFetch(null);
    renderAt("/dashboard");
    await waitFor(() => {});
    expect(screen.queryByRole("link", { name: /Resume workout/ })).not.toBeInTheDocument();
  });
});
