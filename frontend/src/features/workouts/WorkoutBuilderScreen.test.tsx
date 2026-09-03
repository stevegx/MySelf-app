import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { WorkoutBuilderScreen } from "./WorkoutBuilderScreen";

/** Minimal fetch router: quiet the auth refresh, empty program list, echo a created program. */
function installFetch() {
  const created = { id: "p1", name: "PPL", splitLabel: null, isActive: false, groupCount: 0, variantCount: 0, createdAt: "2026-09-02T00:00:00Z" };
  const emptyProgram = { id: "p1", name: "PPL", splitLabel: null, isActive: false, createdAt: "2026-09-02T00:00:00Z", groups: [] };

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

  it("renders drag handles for groups and variants in the detail view", async () => {
    const populated = {
      id: "p1",
      name: "PPL",
      splitLabel: null,
      isActive: false,
      createdAt: "2026-09-02T00:00:00Z",
      rowVersion: 7,
      groups: [
        { id: "g1", name: "Push", sortOrder: 0, variants: [{ id: "v1", name: "Push #1", sortOrder: 0, exerciseCount: 3 }] },
        { id: "g2", name: "Pull", sortOrder: 1, variants: [] },
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

    // One handle per group (2) + one per variant (1).
    expect(await screen.findAllByRole("button", { name: "Drag to reorder" })).toHaveLength(3);
  });

  it("confirms destructive deletes with a styled dialog, not window.confirm", async () => {
    const populated = {
      id: "p1",
      name: "PPL",
      splitLabel: null,
      isActive: false,
      createdAt: "2026-09-02T00:00:00Z",
      rowVersion: 7,
      groups: [{ id: "g1", name: "Push", sortOrder: 0, variants: [{ id: "v1", name: "Push #1", sortOrder: 0, exerciseCount: 3 }] }],
    };
    const spy = vi.fn<typeof fetch>((input, init) => {
      const url = typeof input === "string" ? input : input.toString();
      const json = (d: unknown, s = 200) =>
        Promise.resolve(new Response(JSON.stringify(d), { status: s, headers: { "content-type": "application/json" } }));
      if (url.includes("/auth/refresh"))
        return json({ accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } });
      if (url.includes("/api/v1/workout-variants/v1") && (init?.method ?? "GET") === "DELETE")
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
    expect(await screen.findByRole("dialog", { name: /Delete variant/i })).toBeInTheDocument();
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(spy.mock.calls.some(([, i]) => i?.method === "DELETE")).toBe(false);

    await user.click(screen.getByRole("button", { name: "Delete variant" }));
    await vi.waitFor(() =>
      expect(spy.mock.calls.some(([u, i]) => String(u).includes("/workout-variants/v1") && i?.method === "DELETE")).toBe(true),
    );
  });

  it("creates a program and opens its detail view", async () => {
    const fetchSpy = installFetch();
    const user = userEvent.setup();
    renderScreen();

    await user.type(await screen.findByPlaceholderText(/Program name/i), "PPL");
    await user.click(screen.getByRole("button", { name: "Create" }));

    // Detail view for the new program.
    expect(await screen.findByText("Add workout group")).toBeInTheDocument();
    expect(fetchSpy.mock.calls.some(([u, i]) => String(u).endsWith("/api/v1/programs") && i?.method === "POST")).toBe(true);
  });
});
