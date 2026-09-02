import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { RegisterScreen } from "./RegisterScreen";

function renderRegisterScreen() {
  const router = createMemoryRouter(
    [
      { path: "/register", element: <RegisterScreen /> },
      { path: "/dashboard", element: <div>Dashboard content</div> },
    ],
    { initialEntries: ["/register"] },
  );

  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText("Username"), "demo-user");
  await user.type(screen.getByLabelText("Email"), "demo@example.com");
  await user.type(screen.getByLabelText("Password"), "Str0ng!Pass");
  await user.type(screen.getByLabelText("Confirm password"), "Str0ng!Pass");
}

describe("RegisterScreen", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("blocks submit with client-side validation and never calls the API", async () => {
    // Resolves so the mount-time silent refresh (AuthProvider) doesn't error out; that
    // call is the "1" this test's fetchSpy assertion accounts for below.
    const fetchSpy = vi.fn().mockResolvedValue(new Response(null, { status: 401 }));
    vi.stubGlobal("fetch", fetchSpy);
    const user = userEvent.setup();

    renderRegisterScreen();
    await user.type(screen.getByLabelText("Username"), "ok");
    await user.type(screen.getByLabelText("Email"), "not-an-email");
    await user.type(screen.getByLabelText("Password"), "weak");
    await user.type(screen.getByLabelText("Confirm password"), "weak");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByText("Must be at least 3 characters.")).toBeInTheDocument();
    expect(screen.getByText("Enter a valid email address.")).toBeInTheDocument();
    // Only the initial silent session-refresh call happened; the blocked submit added none.
    expect(fetchSpy).toHaveBeenCalledTimes(1);
  });

  it("toggles the password field between hidden and visible text", async () => {
    const user = userEvent.setup();
    renderRegisterScreen();

    // Two password fields on this screen (Password, Confirm password) each get their own
    // toggle — this exercises the first (Password); order matches DOM/visual order.
    const passwordInput = screen.getByLabelText("Password") as HTMLInputElement;
    const [showButton] = screen.getAllByRole("button", { name: "Show password" });
    expect(passwordInput.type).toBe("password");

    await user.click(showButton);
    expect(passwordInput.type).toBe("text");

    await user.click(screen.getAllByRole("button", { name: "Hide password" })[0]);
    expect(passwordInput.type).toBe("password");
  });

  it("shows the backend's field-level error for a taken username", async () => {
    // mockImplementation (not mockResolvedValue with a single instance) — AuthProvider's
    // mount-time refresh call and this test's submit both hit fetch, and a Response body
    // can only be read once, so each call needs its own fresh Response object.
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(
        () =>
          new Response(
            JSON.stringify({
              title: "Validation failed",
              status: 400,
              errors: { username: ["Username 'demo-user' is already taken."] },
            }),
            { status: 400, headers: { "content-type": "application/problem+json" } },
          ),
      ),
    );
    const user = userEvent.setup();

    renderRegisterScreen();
    await fillValidForm(user);
    await user.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByText("Username 'demo-user' is already taken.")).toBeInTheDocument();
  });

  it("shows a form-level banner for an error the backend doesn't attach to a field", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(
        () =>
          new Response(
            JSON.stringify({
              title: "Validation failed",
              status: 400,
              errors: { form: ["Something about this request could not be processed."] },
            }),
            { status: 400, headers: { "content-type": "application/problem+json" } },
          ),
      ),
    );
    const user = userEvent.setup();

    renderRegisterScreen();
    await fillValidForm(user);
    await user.click(screen.getByRole("button", { name: "Create account" }));

    expect(
      await screen.findByText("Something about this request could not be processed."),
    ).toBeInTheDocument();
  });
});
