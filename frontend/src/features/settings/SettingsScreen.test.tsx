import { useState } from "react";
import type { ReactNode } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider } from "react-router";
import { ThemeProvider } from "../../app/ThemeProvider";
import { AuthContext } from "../auth/auth";
import { SettingsScreen } from "./SettingsScreen";

// A stand-in for AuthProvider that starts already "authenticated" — AuthProvider itself
// always begins by attempting a silent refresh, which is exactly what this test wants to
// bypass to check what happens *after* a session already exists.
function FakeAuthenticatedProvider({
  onSetSession,
  children,
}: {
  onSetSession: (session: unknown) => void;
  children: ReactNode;
}) {
  const [session] = useState({
    accessToken: "token",
    user: { id: "1", username: "demo", email: "demo@example.com" },
  });

  return (
    <AuthContext.Provider value={{ session, status: "authenticated", setSession: onSetSession }}>
      {children}
    </AuthContext.Provider>
  );
}

function renderSettingsScreen(onSetSession: (session: unknown) => void) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter([{ path: "/settings", element: <SettingsScreen /> }], {
    initialEntries: ["/settings"],
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <FakeAuthenticatedProvider onSetSession={onSetSession}>
          <RouterProvider router={router} />
        </FakeAuthenticatedProvider>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

describe("SettingsScreen", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("clears the session when Log out is clicked", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((_url: string, options?: RequestInit) => {
        if (options?.method === "POST") {
          return Promise.resolve(new Response(null, { status: 204 }));
        }
        return Promise.resolve(
          new Response(JSON.stringify({ id: "1", username: "demo", email: "demo@example.com" }), {
            status: 200,
            headers: { "content-type": "application/json" },
          }),
        );
      }),
    );
    const onSetSession = vi.fn();
    const user = userEvent.setup();

    renderSettingsScreen(onSetSession);
    await user.click(screen.getByRole("button", { name: "Log out" }));

    await vi.waitFor(() => expect(onSetSession).toHaveBeenCalledWith(null));
  });
});
