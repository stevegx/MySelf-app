import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { RequireOnboarding } from "./RequireOnboarding";

const useMeMock = vi.fn();
vi.mock("../features/auth/useMe", () => ({ useMe: () => useMeMock() }));

function renderGuard() {
  const router = createMemoryRouter(
    [
      { path: "/onboarding", element: <div>Onboarding wizard</div> },
      {
        path: "/",
        element: <RequireOnboarding />,
        children: [{ path: "dashboard", element: <div>Dashboard content</div> }],
      },
    ],
    { initialEntries: ["/dashboard"] },
  );
  return render(<RouterProvider router={router} />);
}

describe("RequireOnboarding", () => {
  it("renders nothing while GET /me is loading", () => {
    useMeMock.mockReturnValue({ data: undefined, isLoading: true });
    renderGuard();
    expect(screen.queryByText("Dashboard content")).not.toBeInTheDocument();
    expect(screen.queryByText("Onboarding wizard")).not.toBeInTheDocument();
  });

  it("redirects to /onboarding when the profile has no completion stamp", () => {
    useMeMock.mockReturnValue({ data: { profile: { onboardingCompletedAt: null } }, isLoading: false });
    renderGuard();
    expect(screen.getByText("Onboarding wizard")).toBeInTheDocument();
  });

  it("renders the protected route once onboarding is complete", () => {
    useMeMock.mockReturnValue({
      data: { profile: { onboardingCompletedAt: "2026-09-02T10:00:00Z" } },
      isLoading: false,
    });
    renderGuard();
    expect(screen.getByText("Dashboard content")).toBeInTheDocument();
  });
});
