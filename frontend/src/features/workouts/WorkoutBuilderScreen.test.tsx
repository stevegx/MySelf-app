import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { WorkoutBuilderScreen } from "./WorkoutBuilderScreen";

type User = ReturnType<typeof userEvent.setup>;

/** The screen now opens on the "Train" home; the program list is behind "Manage programs". */
async function gotoManage(user: User) {
  await user.click(await screen.findByRole("button", { name: /Manage programs/i }));
}

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
  const router = createMemoryRouter(
    [
      { path: "/", element: <WorkoutBuilderScreen /> },
      { path: "/workouts/active", element: <div>Active workout screen</div> },
    ],
    { initialEntries: ["/"] },
  );
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("WorkoutBuilderScreen — Train home", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("leads with the active program's days, each with a Start button", async () => {
    const programs = [
      { id: "p1", name: "PPL", splitLabel: "Push/Pull/Legs", isActive: true, dayCount: 2, exerciseCount: 8, createdAt: "2026-08-01T00:00:00Z" },
    ];
    const detail = {
      id: "p1", name: "PPL", splitLabel: "Push/Pull/Legs", isActive: true, createdAt: "2026-08-01T00:00:00Z", rowVersion: 3,
      days: [
        { id: "d1", name: "Push", sortOrder: 0, exerciseCount: 5 },
        { id: "d2", name: "Pull", sortOrder: 1, exerciseCount: 3 },
      ],
    };
    const stats = {
      totalSessions: 6, firstPerformedOn: "2026-08-01", lastPerformedOn: "2026-09-04",
      sessionsThisWeek: 1, sessionsThisMonth: 2, weeklyAverage: 1.5, totalVolumeKg: 5000,
      avgDurationSeconds: 3000, completedSets: 40, skippedSets: 2, skippedSetRate: 0.05,
      perDay: [
        { dayId: "d1", dayName: "Push", sessions: 4, lastPerformedOn: "2026-09-04" },
        { dayId: "d2", dayName: "Pull", sessions: 2, lastPerformedOn: null },
      ],
      personalRecords: [],
      muscleWeeklySets: [{ muscle: "Quads", setsPerWeek: 3.5 }, { muscle: "Chest", setsPerWeek: 12 }],
    };
    const spy = vi.fn<typeof fetch>((input, init) => {
      const url = typeof input === "string" ? input : input.toString();
      const method = init?.method ?? "GET";
      const json = (d: unknown, s = 200) =>
        Promise.resolve(new Response(JSON.stringify(d), { status: s, headers: { "content-type": "application/json" } }));
      if (url.includes("/auth/refresh")) return json({ accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } });
      if (url.includes("/api/v1/programs/p1/stats")) return json(stats);
      if (url.includes("/api/v1/programs/p1")) return json(detail);
      if (url.endsWith("/api/v1/programs")) return json(programs);
      if (url.endsWith("/api/v1/workout-sessions") && method === "POST") return json({ id: "s1" }, 201);
      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal("fetch", spy);
    const user = userEvent.setup();

    renderScreen();
    expect(await screen.findByText("Push")).toBeInTheDocument();
    expect(screen.getByText("Pull")).toBeInTheDocument();
    expect(screen.getByText(/PPL · active program/i)).toBeInTheDocument();
    // Pull was never performed -> stale hint.
    expect(screen.getByText("Not done yet")).toBeInTheDocument();

    const starts = screen.getAllByRole("button", { name: "Start" });
    expect(starts).toHaveLength(2);
    await user.click(starts[0]);

    await waitFor(() => {
      const call = spy.mock.calls.find(([u, i]) => String(u).endsWith("/api/v1/workout-sessions") && i?.method === "POST");
      expect(call).toBeTruthy();
      expect(JSON.parse((call![1] as RequestInit).body as string)).toEqual({ dayId: "d1" });
    });
  });

  it("marks the next day in the rotation after the one trained most recently", async () => {
    const programs = [
      { id: "p1", name: "PPL", splitLabel: null, isActive: true, dayCount: 3, exerciseCount: 12, createdAt: "2026-08-01T00:00:00Z" },
    ];
    const detail = {
      id: "p1", name: "PPL", splitLabel: null, isActive: true, createdAt: "2026-08-01T00:00:00Z", rowVersion: 1,
      days: [
        { id: "d1", name: "Push", sortOrder: 0, exerciseCount: 4 },
        { id: "d2", name: "Pull", sortOrder: 1, exerciseCount: 4 },
        { id: "d3", name: "Legs", sortOrder: 2, exerciseCount: 4 },
      ],
    };
    const stats = {
      totalSessions: 3, firstPerformedOn: "2026-08-20", lastPerformedOn: "2026-09-04",
      sessionsThisWeek: 1, sessionsThisMonth: 3, weeklyAverage: 1, totalVolumeKg: 3000,
      avgDurationSeconds: 3000, completedSets: 20, skippedSets: 0, skippedSetRate: 0,
      perDay: [
        { dayId: "d1", dayName: "Push", sessions: 1, lastPerformedOn: "2026-08-28" },
        { dayId: "d2", dayName: "Pull", sessions: 1, lastPerformedOn: "2026-09-04" },
        { dayId: "d3", dayName: "Legs", sessions: 1, lastPerformedOn: "2026-08-21" },
      ],
      personalRecords: [], muscleWeeklySets: [],
    };
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>((input) => {
        const url = typeof input === "string" ? input : input.toString();
        const json = (d: unknown, s = 200) =>
          Promise.resolve(new Response(JSON.stringify(d), { status: s, headers: { "content-type": "application/json" } }));
        if (url.includes("/auth/refresh")) return json({ accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } });
        if (url.includes("/api/v1/programs/p1/stats")) return json(stats);
        if (url.includes("/api/v1/programs/p1")) return json(detail);
        if (url.endsWith("/api/v1/programs")) return json(programs);
        return Promise.resolve(new Response(null, { status: 404 }));
      }),
    );

    renderScreen();

    // Pull was trained most recently -> Legs is up next.
    const legsCard = (await screen.findByText("Legs")).closest("div")!.parentElement!;
    expect(within(legsCard).getByText("Up next")).toBeInTheDocument();
    expect(screen.getByText(/up next: Legs/i)).toBeInTheDocument();
    // Only one day is flagged.
    expect(screen.getAllByText("Up next")).toHaveLength(1);
  });

  it("prompts to pick an active program when none is active", async () => {
    installFetch(); // empty program list
    renderScreen();
    await waitFor(() =>
      expect(screen.getByText(/Create a program to give your workouts some structure/i)).toBeInTheDocument(),
    );
  });
});

describe("WorkoutBuilderScreen — manage programs", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("shows the empty state and the new-program form", async () => {
    installFetch();
    const user = userEvent.setup();
    renderScreen();
    await gotoManage(user);
    await waitFor(() =>
      expect(screen.getByText("No programs yet. Create one above.")).toBeInTheDocument(),
    );
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
    await gotoManage(user);
    await user.click(await screen.findByText("PPL"));
    await user.click(await screen.findByRole("tab", { name: "Days" }));

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
    await gotoManage(user);
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
    await gotoManage(user);
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
    await gotoManage(user);
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
    await gotoManage(user);
    await user.click(await screen.findByText("PPL"));
    await user.click(await screen.findByRole("tab", { name: "Days" }));
    await user.click(await screen.findByRole("button", { name: "Delete day Push" }));

    // The app's own dialog, and window.confirm was never used.
    expect(await screen.findByRole("dialog", { name: /Delete day/i })).toBeInTheDocument();
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(spy.mock.calls.some(([, i]) => i?.method === "DELETE")).toBe(false);

    await user.click(screen.getByRole("button", { name: "Delete day" }));
    await vi.waitFor(() =>
      expect(spy.mock.calls.some(([u, i]) => String(u).includes("/workout-days/d1") && i?.method === "DELETE")).toBe(true),
    );
  });

  it("opens a program on the Overview tab and shows its stats", async () => {
    const populated = {
      id: "p1",
      name: "PPL",
      splitLabel: null,
      isActive: false,
      createdAt: "2026-09-02T00:00:00Z",
      rowVersion: 1,
      days: [{ id: "d1", name: "Push", sortOrder: 0, exerciseCount: 4 }],
    };
    const stats = {
      totalSessions: 7,
      firstPerformedOn: "2026-08-01",
      lastPerformedOn: "2026-09-04",
      sessionsThisWeek: 2,
      sessionsThisMonth: 3,
      weeklyAverage: 2.5,
      totalVolumeKg: 12450,
      avgDurationSeconds: 3300,
      completedSets: 84,
      skippedSets: 6,
      skippedSetRate: 0.067,
      perDay: [{ dayId: "d1", dayName: "Push", sessions: 4, lastPerformedOn: "2026-09-04" }],
      personalRecords: [
        { exerciseName: "Back Squat", type: "HeaviestWeight", value: 140, achievedOn: "2026-09-04" },
      ],
      muscleWeeklySets: [{ muscle: "Chest", setsPerWeek: 10 }],
    };
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>((input) => {
        const url = typeof input === "string" ? input : input.toString();
        const json = (d: unknown, s = 200) =>
          Promise.resolve(new Response(JSON.stringify(d), { status: s, headers: { "content-type": "application/json" } }));
        if (url.includes("/auth/refresh"))
          return json({ accessToken: "t", user: { id: "u1", username: "demo", email: "d@e.com" } });
        if (url.includes("/api/v1/programs/p1/stats")) return json(stats);
        if (url.includes("/api/v1/workout-calendar")) return json({ from: "x", to: "y", days: [] });
        if (url.endsWith("/api/v1/programs")) return json([populated]);
        if (url.includes("/api/v1/programs/p1")) return json(populated);
        return Promise.resolve(new Response(null, { status: 404 }));
      }),
    );

    const user = userEvent.setup();
    renderScreen();
    await gotoManage(user);
    await user.click(await screen.findByText("PPL"));

    // The detail view opens on Days now; Overview is a tab away.
    await user.click(await screen.findByRole("tab", { name: "Overview" }));
    const timesCard = (await screen.findByText("Times performed")).closest("div")!.parentElement!;
    expect(timesCard).toHaveTextContent("7");
    expect(screen.getByText("2.5/wk average")).toBeInTheDocument();
    expect(screen.getByText(/Back Squat/)).toBeInTheDocument();
    expect(screen.getByText(/Muscle coverage/)).toBeInTheDocument();
    expect(screen.getByText(/When you trained this program/)).toBeInTheDocument();

    // The editable day list lives behind the Days tab.
    await user.click(screen.getByRole("tab", { name: "Days" }));
    expect(await screen.findByPlaceholderText("New day…")).toBeInTheDocument();
  });

  it("creates a program and opens its detail view", async () => {
    const fetchSpy = installFetch();
    const user = userEvent.setup();
    renderScreen();
    await gotoManage(user);

    await user.type(await screen.findByPlaceholderText(/Program name/i), "PPL");
    await user.click(screen.getByRole("button", { name: "Create" }));

    // Detail view for the new program — opens on Days, which is empty for a fresh program.
    expect(await screen.findByText(/Add your first day on the left/i)).toBeInTheDocument();
    expect(fetchSpy.mock.calls.some(([u, i]) => String(u).endsWith("/api/v1/programs") && i?.method === "POST")).toBe(true);
  });
});
