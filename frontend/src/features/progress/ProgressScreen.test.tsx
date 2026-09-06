import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { ProgressScreen } from "./ProgressScreen";

const AUTH = { accessToken: "t", user: { id: "u1", username: "d", email: "d@e.com" } };

function installFetch() {
  const spy = vi.fn<typeof fetch>((input) => {
    const url = String(input);
    const json = (b: unknown) =>
      Promise.resolve(new Response(JSON.stringify(b), { status: 200, headers: { "content-type": "application/json" } }));
    if (url.includes("/auth/refresh")) return json(AUTH);
    if (url.includes("/api/v1/exercises?")) {
      return json({ items: [{ id: "x1", name: "Back Squat", category: "Legs", defaultTrackingMode: "WeightAndReps", primaryMuscles: ["Quads"], secondaryMuscles: [], equipment: ["Barbell"], imageThumbUrl: null, imageUrl: null, imageAttribution: null }], page: 1, pageSize: 25, total: 1 });
    }
    if (url.includes("/api/v1/exercises/x1/history")) {
      return json({
        exerciseId: "x1",
        exerciseName: "Back Squat",
        personalRecords: [
          { type: "HeaviestWeight", value: 140, weightKg: 140, reps: 3, achievedOn: "2026-09-01" },
        ],
        sessions: [
          { sessionId: "s1", performedOn: "2026-09-01", dayName: "Legs A", topSetWeightKg: 140, topSetReps: 3, estimatedOneRepMax: 154, volume: 3600, completedSets: 4 },
        ],
      });
    }
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return spy;
}

function renderScreen() {
  const router = createMemoryRouter([{ path: "/", element: <ProgressScreen /> }], { initialEntries: ["/"] });
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("ProgressScreen strength tab", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("picks an exercise and shows its PRs and recent sessions", async () => {
    installFetch();
    const user = userEvent.setup();
    renderScreen();

    await user.type(await screen.findByPlaceholderText(/Search the catalogue/i), "squat");
    await user.click(await screen.findByRole("button", { name: "Back Squat" }));

    expect(await screen.findByText(/Heaviest weight: 140 kg/)).toBeInTheDocument();
    expect(screen.getByText("140 kg × 3")).toBeInTheDocument();
  });
});
