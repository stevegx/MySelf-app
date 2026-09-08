import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "./providers";
import { AppShell } from "./AppShell";

function renderShell(initialPath = "/dashboard") {
  const router = createMemoryRouter(
    [
      {
        path: "/",
        element: <AppShell />,
        children: [{ path: "dashboard", element: <div>Dashboard content</div> }],
      },
    ],
    { initialEntries: [initialPath] },
  );

  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("AppShell", () => {
  it("renders the primary navigation", () => {
    renderShell();
    // Two navs exist (desktop sidebar + mobile bottom bar); CSS decides which shows.
    for (const label of ["Dashboard", "Workouts", "Nutrition", "Progress", "Settings"]) {
      expect(screen.getAllByRole("link", { name: label }).length).toBeGreaterThanOrEqual(1);
    }
  });

  it("renders the routed child content", () => {
    renderShell();
    expect(screen.getByText("Dashboard content")).toBeInTheDocument();
  });
});
