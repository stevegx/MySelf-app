import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { RedirectIfOnboarded } from "./RedirectIfOnboarded";

const useMeMock = vi.fn();
vi.mock("../features/auth/useMe", () => ({ useMe: () => useMeMock() }));

function renderGuard() {
  const router = createMemoryRouter(
    [
      { path: "/dashboard", element: <div>Dashboard content</div> },
      {
        path: "/",
        element: <RedirectIfOnboarded />,
        children: [{ path: "onboarding", element: <div>Onboarding wizard</div> }],
      },
    ],
    { initialEntries: ["/onboarding"] },
  );
  return render(<RouterProvider router={router} />);
}

describe("RedirectIfOnboarded", () => {
  it("renders nothing while GET /me is loading", () => {
    useMeMock.mockReturnValue({ data: undefined, isLoading: true });
    renderGuard();
    expect(screen.queryByText("Onboarding wizard")).not.toBeInTheDocument();
  });

  it("shows the wizard when onboarding is not complete", () => {
    useMeMock.mockReturnValue({ data: { profile: null }, isLoading: false });
    renderGuard();
    expect(screen.getByText("Onboarding wizard")).toBeInTheDocument();
  });

  it("redirects to /dashboard when onboarding is already complete", () => {
    useMeMock.mockReturnValue({
      data: { profile: { onboardingCompletedAt: "2026-09-02T10:00:00Z" } },
      isLoading: false,
    });
    renderGuard();
    expect(screen.getByText("Dashboard content")).toBeInTheDocument();
  });
});
