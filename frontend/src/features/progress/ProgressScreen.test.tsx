import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { ProgressScreen } from "./ProgressScreen";

const AUTH = { accessToken: "t", user: { id: "u1", username: "d", email: "d@e.com" } };

function installFetch() {
  const spy = vi.fn<typeof fetch>((input) => {
    const url = String(input);
    const json = (b: unknown) =>
      Promise.resolve(new Response(JSON.stringify(b), { status: 200, headers: { "content-type": "application/json" } }));
    if (url.includes("/auth/refresh")) return json(AUTH);
    if (url.includes("/api/v1/exercises?")) {
      return json({ items: [{ id: "x1", name: "Back Squat", category: "Legs", defaultTrackingMode: "WeightAndReps", primaryMuscles: ["Quads"], secondaryMuscles: [], equipment: ["Barbell"], imageThumbUrl: null, imageUrl: null, imageAttribution: null }], page: 1, pageSize: 25, total: 1 });
    }
    if (url.includes("/api/v1/exercises/x1/history")) {
      return json({
        exerciseId: "x1",
        exerciseName: "Back Squat",
        personalRecords: [
          { type: "HeaviestWeight", value: 140, weightKg: 140, reps: 3, achievedOn: "2026-09-01" },
        ],
        sessions: [
          { sessionId: "s1", performedOn: "2026-09-01", dayName: "Legs A", topSetWeightKg: 140, topSetReps: 3, estimatedOneRepMax: 154, volume: 3600, completedSets: 4 },
        ],
      });
    }
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return spy;
}

function renderScreen() {
  const router = createMemoryRouter([{ path: "/", element: <ProgressScreen /> }], { initialEntries: ["/"] });
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("ProgressScreen strength tab", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("picks an exercise and shows its PRs and recent sessions", async () => {
    installFetch();
    const user = userEvent.setup();
    renderScreen();

    await user.type(await screen.findByPlaceholderText(/Search the catalogue/i), "squat");
    await user.click(await screen.findByRole("button", { name: "Back Squat" }));

    expect(await screen.findByText(/Heaviest weight: 140 kg/)).toBeInTheDocument();
    expect(screen.getByText("140 kg × 3")).toBeInTheDocument();
  });
});

function installWeightFetch(opts: { points?: unknown[]; latest?: number | null; change?: number | null; readings?: unknown[] } = {}) {
  const calls: { url: string; method: string; body: unknown }[] = [];
  const spy = vi.fn<typeof fetch>((input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(init.body as string) : undefined });
    const json = (b: unknown, status = 200) =>
      Promise.resolve(new Response(JSON.stringify(b), { status, headers: { "content-type": "application/json" } }));

    if (url.includes("/auth/refresh")) return json(AUTH);
    if (url.includes("/api/v1/analytics/weight")) {
      return json({
        from: null, to: null,
        latest: opts.latest ?? null,
        latestOn: opts.latest != null ? "2026-09-08" : null,
        sevenDayChangeKg: opts.change ?? null,
        points: opts.points ?? [],
      });
    }
    if (url.includes("/api/v1/me/body-measurements") && method === "GET") return json(opts.readings ?? []);
    if (url.includes("/api/v1/me/body-measurements") && method === "POST")
      return json({ id: "m9", weightKg: 80, localDate: "2026-09-08", measuredAt: "2026-09-08T07:00:00Z" }, 201);
    if (url.match(/\/me\/body-measurements\/\w+$/) && method === "DELETE")
      return Promise.resolve(new Response(null, { status: 204 }));
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return calls;
}

describe("ProgressScreen body-weight tab", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("logs a reading from the empty state", async () => {
    const calls = installWeightFetch();
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("radio", { name: "Body weight" }));
    expect(await screen.findByText(/No weight logged yet/i)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Log weight" }));
    await user.type(await screen.findByLabelText("Weight (kg)"), "80");
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => {
      const post = calls.find((c) => c.method === "POST" && c.url.includes("/me/body-measurements"));
      expect(post).toBeTruthy();
      expect((post!.body as { weightKg: number }).weightKg).toBe(80);
    });
  });

  it("shows the trend headline and deletes a reading", async () => {
    const calls = installWeightFetch({
      latest: 79,
      change: -0.5,
      points: [
        { date: "2026-09-01", average: 80, rollingAverage: 80 },
        { date: "2026-09-08", average: 79, rollingAverage: 79.5 },
      ],
      readings: [
        { id: "m1", weightKg: 79, localDate: "2026-09-08", measuredAt: "2026-09-08T07:00:00Z" },
        { id: "m2", weightKg: 80, localDate: "2026-09-01", measuredAt: "2026-09-01T07:00:00Z" },
      ],
    });
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("radio", { name: "Body weight" }));
    // Headline latest + a recent-readings row both show 79.0 kg.
    await waitFor(() => expect(screen.getAllByText(/79\.0 kg/).length).toBeGreaterThanOrEqual(2));
    expect(screen.getByText(/▼\s*0\.5 kg \/ 7 days/)).toBeInTheDocument();
    expect(screen.getByText("7-day rolling")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Delete reading from 2026-09-08" }));
    await waitFor(() =>
      expect(calls.some((c) => c.method === "DELETE" && c.url.includes("/me/body-measurements/m1"))).toBe(true),
    );
  });
});

function installNutritionFetch(body: unknown) {
  const spy = vi.fn<typeof fetch>((input) => {
    const url = String(input);
    const json = (b: unknown) =>
      Promise.resolve(new Response(JSON.stringify(b), { status: 200, headers: { "content-type": "application/json" } }));
    if (url.includes("/auth/refresh")) return json(AUTH);
    if (url.includes("/api/v1/analytics/nutrition")) return json(body);
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
}

describe("ProgressScreen nutrition tab", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("shows the empty state when nothing is logged", async () => {
    installNutritionFetch({
      from: "2026-08-25", to: "2026-09-07",
      targets: { kcal: null, proteinG: null, carbG: null, fatG: null },
      daysLogged: 0,
      average: { kcal: 0, proteinG: 0, carbG: 0, fatG: 0 },
      days: [],
    });
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("radio", { name: "Nutrition" }));
    expect(await screen.findByText(/No meals logged in the last two weeks/i)).toBeInTheDocument();
  });

  it("shows the calorie average vs target and macro averages", async () => {
    installNutritionFetch({
      from: "2026-08-25", to: "2026-09-07",
      targets: { kcal: 2000, proteinG: 150, carbG: 200, fatG: 60 },
      daysLogged: 2,
      average: { kcal: 2200, proteinG: 140, carbG: 210, fatG: 70 },
      days: [
        { date: "2026-09-06", kcal: 2000, proteinG: 130, carbG: 200, fatG: 65 },
        { date: "2026-09-07", kcal: 2400, proteinG: 150, carbG: 220, fatG: 75 },
      ],
    });
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("radio", { name: "Nutrition" }));
    expect(await screen.findByText("2200")).toBeInTheDocument();
    expect(screen.getByText(/target 2000/)).toBeInTheDocument();
    expect(screen.getByText("+200")).toBeInTheDocument();
    expect(screen.getByText("140 g")).toBeInTheDocument();
  });
});
