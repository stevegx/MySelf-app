import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { ResetPasswordScreen } from "./ResetPasswordScreen";

function renderScreen(search = "?email=demo%40example.com&token=abc123") {
  const router = createMemoryRouter(
    [{ path: "/reset-password", element: <ResetPasswordScreen /> }],
    { initialEntries: [`/reset-password${search}`] },
  );

  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("ResetPasswordScreen", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("shows an invalid-link message when the URL is missing email/token", () => {
    renderScreen("");
    expect(screen.getByText("Invalid reset link")).toBeInTheDocument();
  });

  it("blocks submit with client-side password validation and never calls the API", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(new Response(null, { status: 401 }));
    vi.stubGlobal("fetch", fetchSpy);
    const user = userEvent.setup();

    renderScreen();
    // 6+ chars so only the "needs a number" rule is the one that fails.
    await user.type(screen.getByLabelText("New password"), "weakpw");
    await user.type(screen.getByLabelText("Confirm new password"), "weakpw");
    await user.click(screen.getByRole("button", { name: "Reset password" }));

    expect(await screen.findByText("Must include a number.")).toBeInTheDocument();
    expect(fetchSpy).toHaveBeenCalledTimes(1); // only the mount-time silent refresh
  });

  it("shows the success screen once the backend confirms the reset", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(
        () =>
          new Response(JSON.stringify({ message: "Your password has been reset. Please log in." }), {
            status: 200,
            headers: { "content-type": "application/json" },
          }),
      ),
    );
    const user = userEvent.setup();

    renderScreen();
    await user.type(screen.getByLabelText("New password"), "Str0ng!Pass");
    await user.type(screen.getByLabelText("Confirm new password"), "Str0ng!Pass");
    await user.click(screen.getByRole("button", { name: "Reset password" }));

    expect(await screen.findByText("Password reset")).toBeInTheDocument();
  });

  it("shows the backend's field-level error for an invalid/expired token", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(
        () =>
          new Response(
            JSON.stringify({
              title: "Invalid reset link",
              detail: "This reset link is invalid or has expired. Request a new one.",
            }),
            { status: 400, headers: { "content-type": "application/problem+json" } },
          ),
      ),
    );
    const user = userEvent.setup();

    renderScreen();
    await user.type(screen.getByLabelText("New password"), "Str0ng!Pass");
    await user.type(screen.getByLabelText("Confirm new password"), "Str0ng!Pass");
    await user.click(screen.getByRole("button", { name: "Reset password" }));

    expect(
      await screen.findByText("This reset link is invalid or has expired. Request a new one."),
    ).toBeInTheDocument();
  });
});
