import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { WorkoutBuilderScreen } from "./WorkoutBuilderScreen";

/** Minimal fetch router: quiet the auth refresh, empty program list, echo a created program. */
function installFetch() {
  const created = { id: "p1", name: "PPL", splitLabel: null, isActive: false, dayCount: 0, exerciseCount: 0, createdAt: "2026-09-02T00:00:00Z" };
  const emptyProgram = { id: "p1", name: "PPL", splitLabel: null, isActive: false, createdAt: "2026-09-02T00:00:00Z", days: [] };

  const session = { accessToken: "test-token", user: { id: "u1", username: "demo", email: "demo@example.com" } };

  const spy = vi.fn<typeof fetch>((input, init) => {
    const url = typeof input === "string" ? input : input.toString();
    const method = init?.method ?? "GET";
    if (url.includes("/auth/refresh")) {
      return Promise.resolve(
        new Response(JSON.stringify(session), { status: 200, headers: { "content-type": "application/json" } }),
      );
    }
    if (url.endsWith("/api/v1/programs") && method === "GET") {
      return Promise.resolve(new Response("[]", { status: 200, headers: { "content-type": "application/json" } }));
    }
    if (url.endsWith("/api/v1/programs") && method === "POST") {
      return Promise.resolve(new Response(JSON.stringify(created), { status: 201, headers: { "content-type": "application/json" } }));
    }
    if (url.includes("/api/v1/programs/p1")) {
      return Promise.resolve(new Response(JSON.stringify(emptyProgram), { status: 200, headers: { "content-type": "application/json" } }));
    }
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return spy;
}

function renderScreen() {
  const router = createMemoryRouter([{ path: "/", element: <WorkoutBuilderScreen /> }], { initialEntries: ["/"] });
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("WorkoutBuilderScreen", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("shows the empty state and the new-program form", async () => {
    installFetch();
    renderScreen();
    expect(await screen.findByText("No programs yet. Create one above.")).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/Program name/i)).toBeInTheDocument();
  });

  it("renders drag handles for days in the detail view", async () => {
    const populated = {
      id: "p1",
      name: "PPL",
      splitLabel: null,
      isActive: false,
      createdAt: "2026-09-02T00:00:00Z",
      rowVersion: 7,
      days: [
        { id: "d1", name: "Push", sortOrder: 0, exerciseCount: 3 },
        { id: "d2", name: "Pull", sortOrder: 1, exerciseCount: 0 },
      ],
    };
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>((input) => {
        const url = typeof input === "string" ? input : input.toString();
        const json = (d: unknown, s = 200) =>
          Promise.resolve(new Response(JSON.stringify(d), { status: s, headers: { "content-type": "application/json" } }));
        if (url.includes("/auth/refresh"))
          return json({ accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } });
        if (url.endsWith("/api/v1/programs")) return json([populated]);
        if (url.includes("/api/v1/programs/p1")) return json(populated);
        return Promise.resolve(new Response(null, { status: 404 }));
      }),
    );

    const user = userEvent.setup();
    renderScreen();
    await user.click(await screen.findByText("PPL"));

    // One handle per day.
    expect(await screen.findAllByRole("button", { name: "Drag to reorder" })).toHaveLength(2);
  });

  it("multi-selects programs and bulk-deletes them", async () => {
    const list = [
      { id: "p1", name: "Alpha", splitLabel: null, isActive: false, dayCount: 0, exerciseCount: 0, createdAt: "2026-09-02T00:00:00Z" },
      { id: "p2", name: "Beta", splitLabel: null, isActive: false, dayCount: 0, exerciseCount: 0, createdAt: "2026-09-02T00:00:00Z" },
    ];
    const spy = vi.fn<typeof fetch>((input, init) => {
      const url = typeof input === "string" ? input : input.toString();
      const json = (d: unknown, s = 200) =>
        Promise.resolve(new Response(JSON.stringify(d), { status: s, headers: { "content-type": "application/json" } }));
      if (url.includes("/auth/refresh"))
        return json({ accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } });
      if (url.match(/\/api\/v1\/programs\/p[12]$/) && init?.method === "DELETE")
        return Promise.resolve(new Response(null, { status: 204 }));
      if (url.endsWith("/api/v1/programs")) return json(list);
      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal("fetch", spy);
    const user = userEvent.setup();

    renderScreen();
    await screen.findByText("Alpha");
    await user.click(screen.getByRole("button", { name: "Select" }));
    await user.click(screen.getByRole("checkbox", { name: "Select Alpha" }));
    await user.click(screen.getByRole("checkbox", { name: "Select Beta" }));
    await user.click(screen.getByRole("button", { name: "Delete" }));
    await user.click(await screen.findByRole("button", { name: "Delete 2" })); // dialog confirm

    await vi.waitFor(() => {
      const deletes = spy.mock.calls.filter(([u, i]) => /\/programs\/p[12]$/.test(String(u)) && i?.method === "DELETE");
      expect(deletes).toHaveLength(2);
    });
  });

  it("shows an inline error when deleting an archived program fails", async () => {
    const archived = [
      { id: "a1", name: "Old PPL", splitLabel: null, isActive: false, dayCount: 3, exerciseCount: 12, createdAt: "2026-07-01T00:00:00Z" },
    ];
    const spy = vi.fn<typeof fetch>((input, init) => {
      const url = typeof input === "string" ? input : input.toString();
      const json = (d: unknown, s = 200) =>
        Promise.resolve(new Response(JSON.stringify(d), { status: s, headers: { "content-type": "application/json" } }));
      if (url.includes("/auth/refresh"))
        return json({ accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } });
      if (url.endsWith("/api/v1/programs/archived")) return json(archived);
      if (url.match(/\/api\/v1\/programs\/a1$/) && init?.method === "DELETE")
        return json({ title: "Not Found", detail: "That program no longer exists." }, 404);
      if (url.endsWith("/api/v1/programs")) return json([]);
      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal("fetch", spy);
    const user = userEvent.setup();

    renderScreen();
    await user.click(await screen.findByRole("button", { name: /Archived programs/ }));
    await user.click(await screen.findByRole("button", { name: "Delete" }));
    const dlg = await screen.findByRole("dialog", { name: /Permanently delete/i });
    await user.click(within(dlg).getByRole("button", { name: "Delete" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/no longer exists/i);
  });

  it("hard-deletes a program via DELETE after confirming", async () => {
    const populated = {
      id: "p1",
      name: "Throwaway",
      splitLabel: null,
      isActive: false,
      createdAt: "2026-09-02T00:00:00Z",
      rowVersion: 1,
      days: [],
    };
    const spy = vi.fn<typeof fetch>((input, init) => {
      const url = typeof input === "string" ? input : input.toString();
      const json = (d: unknown, s = 200) =>
        Promise.resolve(new Response(JSON.stringify(d), { status: s, headers: { "content-type": "application/json" } }));
      if (url.includes("/auth/refresh"))
        return json({ accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } });
      if (url.match(/\/api\/v1\/programs\/p1$/) && init?.method === "DELETE")
        return Promise.resolve(new Response(null, { status: 204 }));
      if (url.endsWith("/api/v1/programs")) return json([populated]);
      if (url.includes("/api/v1/programs/p1")) return json(populated);
      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal("fetch", spy);
    const user = userEvent.setup();

    renderScreen();
    await user.click(await screen.findByText("Throwaway"));
    await user.click(await screen.findByRole("button", { name: "Delete program" }));
    await user.click(await screen.findByRole("button", { name: "Delete" })); // dialog confirm

    await vi.waitFor(() =>
      expect(spy.mock.calls.some(([u, i]) => /\/programs\/p1$/.test(String(u)) && i?.method === "DELETE")).toBe(true),
    );
  });

  it("confirms destructive deletes with a styled dialog, not window.confirm", async () => {
    const populated = {
      id: "p1",
      name: "PPL",
      splitLabel: null,
      isActive: false,
      createdAt: "2026-09-02T00:00:00Z",
      rowVersion: 7,
      days: [{ id: "d1", name: "Push", sortOrder: 0, exerciseCount: 3 }],
    };
    const spy = vi.fn<typeof fetch>((input, init) => {
      const url = typeof input === "string" ? input : input.toString();
      const json = (d: unknown, s = 200) =>
        Promise.resolve(new Response(JSON.stringify(d), { status: s, headers: { "content-type": "application/json" } }));
      if (url.includes("/auth/refresh"))
        return json({ accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } });
      if (url.includes("/api/v1/workout-days/d1") && (init?.method ?? "GET") === "DELETE")
        return Promise.resolve(new Response(null, { status: 204 }));
      if (url.endsWith("/api/v1/programs")) return json([populated]);
      if (url.includes("/api/v1/programs/p1")) return json(populated);
      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal("fetch", spy);
    const confirmSpy = vi.fn(() => true);
    vi.stubGlobal("confirm", confirmSpy);

    const user = userEvent.setup();
    renderScreen();
    await user.click(await screen.findByText("PPL"));
    await user.click(await screen.findByRole("button", { name: "Delete" }));

    // The app's own dialog, and window.confirm was never used.
    expect(await screen.findByRole("dialog", { name: /Delete day/i })).toBeInTheDocument();
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(spy.mock.calls.some(([, i]) => i?.method === "DELETE")).toBe(false);

    await user.click(screen.getByRole("button", { name: "Delete day" }));
    await vi.waitFor(() =>
      expect(spy.mock.calls.some(([u, i]) => String(u).includes("/workout-days/d1") && i?.method === "DELETE")).toBe(true),
    );
  });

  it("creates a program and opens its detail view", async () => {
    const fetchSpy = installFetch();
    const user = userEvent.setup();
    renderScreen();

    await user.type(await screen.findByPlaceholderText(/Program name/i), "PPL");
    await user.click(screen.getByRole("button", { name: "Create" }));

    // Detail view for the new program.
    expect(await screen.findByText("New day")).toBeInTheDocument();
    expect(fetchSpy.mock.calls.some(([u, i]) => String(u).endsWith("/api/v1/programs") && i?.method === "POST")).toBe(true);
  });
});
