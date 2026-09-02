import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { AuthContext } from "../features/auth/auth";
import type { AuthContextValue } from "../features/auth/auth";
import { RedirectIfAuthenticated } from "./RedirectIfAuthenticated";

function renderWithStatus(value: AuthContextValue) {
  const router = createMemoryRouter(
    [
      { path: "/dashboard", element: <div>Dashboard content</div> },
      {
        path: "/login",
        element: <RedirectIfAuthenticated />,
        children: [{ index: true, element: <div>Login screen</div> }],
      },
    ],
    { initialEntries: ["/login"] },
  );

  return render(
    <AuthContext.Provider value={value}>
      <RouterProvider router={router} />
    </AuthContext.Provider>,
  );
}

describe("RedirectIfAuthenticated", () => {
  it("renders nothing while the initial session check is loading", () => {
    renderWithStatus({ session: null, status: "loading", setSession: () => {} });
    expect(screen.queryByText("Login screen")).not.toBeInTheDocument();
    expect(screen.queryByText("Dashboard content")).not.toBeInTheDocument();
  });

  it("renders the login screen when unauthenticated", () => {
    renderWithStatus({ session: null, status: "unauthenticated", setSession: () => {} });
    expect(screen.getByText("Login screen")).toBeInTheDocument();
  });

  it("redirects to /dashboard when already authenticated", () => {
    renderWithStatus({
      session: { accessToken: "token", user: { id: "1", username: "demo", email: "demo@example.com" } },
      status: "authenticated",
      setSession: () => {},
    });
    expect(screen.getByText("Dashboard content")).toBeInTheDocument();
  });
});
