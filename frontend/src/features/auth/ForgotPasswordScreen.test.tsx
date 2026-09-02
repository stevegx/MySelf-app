import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { ForgotPasswordScreen } from "./ForgotPasswordScreen";

function renderScreen() {
  const router = createMemoryRouter(
    [{ path: "/forgot-password", element: <ForgotPasswordScreen /> }],
    { initialEntries: ["/forgot-password"] },
  );

  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("ForgotPasswordScreen", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("shows the generic success message and the dev-mode reset link", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(
        () =>
          new Response(
            JSON.stringify({
              message: "If an account exists for that email, a reset link has been sent.",
              resetLink: "http://localhost:5173/reset-password?email=demo%40example.com&token=abc123",
            }),
            { status: 200, headers: { "content-type": "application/json" } },
          ),
      ),
    );
    const user = userEvent.setup();

    renderScreen();
    await user.type(screen.getByLabelText("Email"), "demo@example.com");
    await user.click(screen.getByRole("button", { name: "Send reset link" }));

    expect(
      await screen.findByText("If an account exists for that email, a reset link has been sent."),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /reset-password\?email=/ })).toHaveAttribute(
      "href",
      "http://localhost:5173/reset-password?email=demo%40example.com&token=abc123",
    );
  });

  it("blocks submit with client-side validation and never calls the API", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(new Response(null, { status: 401 }));
    vi.stubGlobal("fetch", fetchSpy);
    const user = userEvent.setup();

    renderScreen();
    await user.click(screen.getByRole("button", { name: "Send reset link" }));

    expect(await screen.findByText("Email is required.")).toBeInTheDocument();
    expect(fetchSpy).toHaveBeenCalledTimes(1); // only the mount-time silent refresh
  });
});
