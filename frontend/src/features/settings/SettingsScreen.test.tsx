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

  it("exports account data as an Excel download", async () => {
    const calls: { url: string; method: string }[] = [];
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url: string, options?: RequestInit) => {
        calls.push({ url: String(url), method: options?.method ?? "GET" });
        if (String(url).includes("/api/v1/me/export")) {
          return Promise.resolve(new Response(new Blob(["spreadsheet-bytes"]), { status: 200 }));
        }
        return Promise.resolve(
          new Response(JSON.stringify({ id: "1", username: "demo", email: "demo@example.com" }), {
            status: 200,
            headers: { "content-type": "application/json" },
          }),
        );
      }),
    );
    const createUrl = vi.fn(() => "blob:fake");
    const revokeUrl = vi.fn();
    vi.stubGlobal("URL", { ...URL, createObjectURL: createUrl, revokeObjectURL: revokeUrl });
    const clickSpy = vi
      .spyOn(HTMLAnchorElement.prototype, "click")
      .mockImplementation(() => undefined);

    const user = userEvent.setup();
    renderSettingsScreen(vi.fn());

    await user.click(screen.getByRole("button", { name: "Export to Excel" }));

    await vi.waitFor(() => {
      expect(
        calls.some((c) => c.url.includes("/api/v1/me/export?format=xlsx") && c.method === "GET"),
      ).toBe(true);
      expect(createUrl).toHaveBeenCalled();
      expect(clickSpy).toHaveBeenCalled();
    });
    clickSpy.mockRestore();
  });

  it("deletes the account only after DELETE is typed, then clears the session", async () => {
    const calls: { url: string; method: string }[] = [];
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url: string, options?: RequestInit) => {
        calls.push({ url: String(url), method: options?.method ?? "GET" });
        if (options?.method === "DELETE") return Promise.resolve(new Response(null, { status: 204 }));
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

    await user.click(screen.getByRole("button", { name: "Delete account" }));

    const confirm = screen.getByRole("button", { name: "Delete my account" });
    expect(confirm).toBeDisabled();

    await user.type(screen.getByLabelText("Type DELETE to confirm"), "delete");
    expect(confirm).toBeDisabled(); // case-sensitive

    await user.clear(screen.getByLabelText("Type DELETE to confirm"));
    await user.type(screen.getByLabelText("Type DELETE to confirm"), "DELETE");
    expect(confirm).toBeEnabled();

    await user.click(confirm);

    await vi.waitFor(() => {
      expect(calls.some((c) => c.url.endsWith("/api/v1/me") && c.method === "DELETE")).toBe(true);
      expect(onSetSession).toHaveBeenCalledWith(null);
    });
  });
});
