import { render, screen, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { WorkoutHistoryScreen } from "./WorkoutHistoryScreen";

const AUTH = { accessToken: "test-token", user: { id: "u1", username: "demo", email: "demo@example.com" } };

function summary(over: Partial<Record<string, number | null>> = {}) {
  return {
    durationSeconds: 3300,
    completedSetCount: 6,
    skippedSetCount: 1,
    totalReps: 54,
    totalVolumeKg: 4200,
    ...over,
  };
}

function installFetch(items: unknown[]) {
  const spy = vi.fn<typeof fetch>((input) => {
    const url = String(input);
    const json = (b: unknown) =>
      Promise.resolve(new Response(JSON.stringify(b), { status: 200, headers: { "content-type": "application/json" } }));
    if (url.includes("/auth/refresh")) return json(AUTH);
    if (url.includes("/workout-sessions?status=")) {
      return json({ items, page: 1, pageSize: 50, total: items.length });
    }
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return spy;
}

function renderScreen() {
  const router = createMemoryRouter(
    [
      { path: "/", element: <WorkoutHistoryScreen /> },
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

describe("WorkoutHistoryScreen", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("lists completed workouts with their summary stats", async () => {
    installFetch([
      {
        id: "s1",
        dayName: "Legs A",
        programName: "PPL",
        status: "Completed",
        startedAt: "2026-09-04T09:00:00Z",
        completedAt: "2026-09-04T10:00:00Z",
        performedOnLocalDate: "2026-09-04",
        summary: summary(),
      },
    ]);
    renderScreen();

    expect(await screen.findByText("Legs A")).toBeInTheDocument();
    expect(screen.getByText("6 sets")).toBeInTheDocument();
    expect(screen.getByText("1 skipped")).toBeInTheDocument();
    expect(screen.getByText(/4[.,]?200 kg volume/)).toBeInTheDocument();
    expect(screen.getByText("55 min")).toBeInTheDocument();
  });

  it("shows an empty state when there are no completed workouts", async () => {
    installFetch([]);
    renderScreen();
    await waitFor(() => expect(screen.getByText(/No completed workouts yet/i)).toBeInTheDocument());
  });
});
