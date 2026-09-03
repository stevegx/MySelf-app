import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { DashboardScreen } from "./DashboardScreen";

const session = { accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } };

function installFetch(currentGoal: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn<typeof fetch>((input) => {
      const url = typeof input === "string" ? input : input.toString();
      const json = (d: unknown) =>
        Promise.resolve(new Response(JSON.stringify(d), { status: 200, headers: { "content-type": "application/json" } }));
      if (url.includes("/auth/refresh")) return json(session);
      if (url.includes("/api/v1/me")) return json({ user: session.user, profile: null, currentGoal });
      return Promise.resolve(new Response(null, { status: 404 }));
    }),
  );
}

function renderDashboard() {
  const router = createMemoryRouter([{ path: "/", element: <DashboardScreen /> }], { initialEntries: ["/"] });
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("DashboardScreen", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("shows the user's real calorie and macro targets from GET /me", async () => {
    installFetch({
      id: "g1",
      goalType: "Lose",
      source: "Estimated",
      targetWeightKg: 68,
      calorieTarget: 1795,
      proteinGrams: 140,
      carbGrams: 180,
      fatGrams: 55,
      effectiveFrom: "2026-09-03T00:00:00Z",
    });
    renderDashboard();

    expect(await screen.findByText(/\/ 1[.,]?795 kcal/)).toBeInTheDocument();
    expect(screen.getByText("0 / 140g")).toBeInTheDocument();
    expect(screen.getByText("0 / 55g")).toBeInTheDocument();
    expect(screen.queryByText(/No calorie target/i)).not.toBeInTheDocument();
  });

  it("falls back to a 'no target' state when nutrition was skipped", async () => {
    installFetch(null);
    renderDashboard();

    expect(await screen.findByText(/No calorie target/i)).toBeInTheDocument();
    // No hardcoded target sneaks through.
    expect(screen.queryByText(/2[.,]?104/)).not.toBeInTheDocument();
  });
});
