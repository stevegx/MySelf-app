import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { AuthContext } from "../features/auth/auth";
import type { AuthContextValue } from "../features/auth/auth";
import { RequireAuth } from "./RequireAuth";

function renderWithStatus(value: AuthContextValue) {
  const router = createMemoryRouter(
    [
      { path: "/login", element: <div>Login screen</div> },
      {
        path: "/",
        element: <RequireAuth />,
        children: [{ path: "dashboard", element: <div>Dashboard content</div> }],
      },
    ],
    { initialEntries: ["/dashboard"] },
  );

  return render(
    <AuthContext.Provider value={value}>
      <RouterProvider router={router} />
    </AuthContext.Provider>,
  );
}

describe("RequireAuth", () => {
  it("renders nothing while the initial session check is loading", () => {
    renderWithStatus({ session: null, status: "loading", setSession: () => {} });
    expect(screen.queryByText("Login screen")).not.toBeInTheDocument();
    expect(screen.queryByText("Dashboard content")).not.toBeInTheDocument();
  });

  it("redirects to /login once unauthenticated", () => {
    renderWithStatus({ session: null, status: "unauthenticated", setSession: () => {} });
    expect(screen.getByText("Login screen")).toBeInTheDocument();
  });

  it("renders the protected route once authenticated", () => {
    renderWithStatus({
      session: { accessToken: "token", user: { id: "1", username: "demo", email: "demo@example.com" } },
      status: "authenticated",
      setSession: () => {},
    });
    expect(screen.getByText("Dashboard content")).toBeInTheDocument();
  });
});
