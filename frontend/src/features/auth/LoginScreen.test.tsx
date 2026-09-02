import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { LoginScreen } from "./LoginScreen";

function renderLoginScreen() {
  const router = createMemoryRouter(
    [
      { path: "/login", element: <LoginScreen /> },
      { path: "/dashboard", element: <div>Dashboard content</div> },
    ],
    { initialEntries: ["/login"] },
  );

  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("LoginScreen", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("blocks submit with client-side validation and never calls the API", async () => {
    // Resolves so the mount-time silent refresh (AuthProvider) doesn't error out; that
    // call is the "1" this test's fetchSpy assertion accounts for below.
    const fetchSpy = vi.fn().mockResolvedValue(new Response(null, { status: 401 }));
    vi.stubGlobal("fetch", fetchSpy);
    const user = userEvent.setup();

    renderLoginScreen();
    await user.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findByText("Enter your username or email.")).toBeInTheDocument();
    expect(screen.getByText("Password is required.")).toBeInTheDocument();
    // Only the initial silent session-refresh call happened; the blocked submit added none.
    expect(fetchSpy).toHaveBeenCalledTimes(1);
  });

  it("shows the backend's generic invalid-credentials message, not tied to a field", async () => {
    // mockImplementation — AuthProvider's mount-time refresh call and this test's submit
    // both hit fetch, and a Response body can only be read once each.
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(
        () =>
          new Response(
            JSON.stringify({ title: "Invalid credentials", detail: "Invalid email or password." }),
            { status: 401, headers: { "content-type": "application/problem+json" } },
          ),
      ),
    );
    const user = userEvent.setup();

    renderLoginScreen();
    await user.type(screen.getByLabelText("Username or email"), "demo@example.com");
    await user.type(screen.getByLabelText("Password"), "wrong-password");
    await user.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findByText("Invalid email or password.")).toBeInTheDocument();
  });

  it("toggles the password field between hidden and visible text", async () => {
    const user = userEvent.setup();
    renderLoginScreen();

    const passwordInput = screen.getByLabelText("Password") as HTMLInputElement;
    expect(passwordInput.type).toBe("password");

    await user.click(screen.getByRole("button", { name: "Show password" }));
    expect(passwordInput.type).toBe("text");

    await user.click(screen.getByRole("button", { name: "Hide password" }));
    expect(passwordInput.type).toBe("password");
  });
});
