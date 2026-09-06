import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Providers } from "../../app/providers";
import { DayEditor } from "./DayEditor";

const session = { accessToken: "test-token", user: { id: "u1", username: "demo", email: "demo@example.com" } };

const day = {
  id: "d1",
  name: "Legs #1",
  sortOrder: 0,
  estimatedDurationMinutes: null,
  programRowVersion: 42,
  exercises: [
    {
      id: "de1",
      exerciseId: "ex1",
      exerciseName: "Back Squat",
      sortOrder: 0,
      supersetGroupId: null,
      supersetMemberOrder: 0,
      restSeconds: 120,
      notes: null,
      sets: [
        { id: "s1", sortOrder: 0, kind: "Standard", isAmrap: false, targetToFailure: false, targetRepsMin: 5, targetRepsMax: 5, targetWeightKg: 100, targetRir: 2 },
        { id: "s2", sortOrder: 1, kind: "Drop", isAmrap: true, targetToFailure: false, targetRepsMin: null, targetRepsMax: null, targetWeightKg: 80, targetRir: null },
      ],
    },
  ],
  supersets: [],
  focusMuscleIds: [] as number[],
};

const MUSCLES = [
  { id: 1, name: "Chest", isFront: true },
  { id: 2, name: "Quads", isFront: true },
  { id: 3, name: "Shoulders", isFront: true },
];

const ME = {
  user: session.user,
  profile: {
    dateOfBirth: "1994-03-21", heightCm: 178, calculationSex: null, unitSystem: "Metric",
    timezone: null, locale: null, onboardingCompletedAt: "2026-09-01T00:00:00Z", warnOffFocusExercises: true,
  },
  currentGoal: null,
};

const program = {
  id: "p1",
  name: "PPL",
  splitLabel: null,
  isActive: false,
  createdAt: "2026-09-03T00:00:00Z",
  rowVersion: 42,
  days: [{ id: "d1", name: "Legs #1", sortOrder: 0, exerciseCount: 1 }],
};

function installFetch() {
  const calls: { url: string; method: string; body: unknown }[] = [];
  const spy = vi.fn<typeof fetch>((input, init) => {
    const url = typeof input === "string" ? input : input.toString();
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(init.body as string) : undefined });
    const json = (data: unknown, status = 200) =>
      Promise.resolve(new Response(JSON.stringify(data), { status, headers: { "content-type": "application/json" } }));

    if (url.includes("/auth/refresh")) return json(session);
    if (url.endsWith("/api/v1/me")) return json(ME);
    if (url.endsWith("/api/v1/muscles")) return json(MUSCLES);
    if (url.endsWith("/api/v1/me/preferences") && method === "PUT") return json({ ...ME.profile, warnOffFocusExercises: false });
    if (url.includes("/api/v1/exercises?")) {
      return json({
        items: [
          { id: "sq", name: "Back Squat", category: "Legs", defaultTrackingMode: "WeightAndReps", primaryMuscles: ["Quads"], secondaryMuscles: ["Glutes"], equipment: ["Barbell"], imageThumbUrl: "https://wger.de/media/x.png", imageUrl: "https://wger.de/media/x-full.png", imageAttribution: "wger.de (CC BY-SA)" },
          { id: "ohp", name: "Overhead Press", category: "Shoulders", defaultTrackingMode: "WeightAndReps", primaryMuscles: ["Shoulders"], secondaryMuscles: ["Triceps"], equipment: ["Barbell"], imageThumbUrl: null, imageUrl: null, imageAttribution: null },
        ],
        page: 1, pageSize: 25, total: 2,
      });
    }
    if (url.includes("/api/v1/workout-days/d1") && method === "GET") return json(day);
    if (url.includes("/api/v1/workout-days/d1") && method === "PUT") return json(day);
    if (url.includes("/api/v1/programs/p1")) return json(program);
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return calls;
}

type U = ReturnType<typeof userEvent.setup>;

/** Exercise rows start collapsed (summary tags); click the name to reveal the set editor. */
async function expandExercise(user: U, name: string) {
  await user.click((await screen.findByText(name)).closest("button")!);
}

describe("DayEditor", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("shows every prescribed set instead of flattening them", async () => {
    installFetch();
    const user = userEvent.setup();
    render(
      <Providers>
        <DayEditor dayId="d1" programId="p1" onClose={() => {}} />
      </Providers>,
    );

    await expandExercise(user, "Back Squat");
    // Two distinct set rows: one Standard, one Drop.
    expect(screen.getByDisplayValue("100")).toBeInTheDocument(); // set 1 weight
    expect(screen.getByDisplayValue("80")).toBeInTheDocument(); // set 2 weight
    expect(screen.getAllByRole("checkbox", { name: "AMRAP" })).toHaveLength(2);
  });

  it("round-trips per-set detail on save", async () => {
    const calls = installFetch();
    const user = userEvent.setup();
    render(
      <Providers>
        <DayEditor dayId="d1" programId="p1" onClose={() => {}} />
      </Providers>,
    );

    await screen.findByText("Back Squat");
    await user.click(screen.getByRole("button", { name: /Save day/i }));

    const put = calls.find((c) => c.method === "PUT");
    expect(put).toBeTruthy();
    const body = put!.body as { rowVersion: number; exercises: { sets: { kind: string; isAmrap: boolean; targetWeightKg: number | null }[] }[] };
    expect(body.rowVersion).toBe(42);
    expect(body.exercises[0].sets).toHaveLength(2);
    expect(body.exercises[0].sets[0]).toMatchObject({ kind: "Standard", targetWeightKg: 100 });
    expect(body.exercises[0].sets[1]).toMatchObject({ kind: "Drop", isAmrap: true, targetWeightKg: 80 });
  });

  it("flags unsaved changes after an edit", async () => {
    installFetch();
    const user = userEvent.setup();
    render(
      <Providers>
        <DayEditor dayId="d1" programId="p1" onClose={() => {}} />
      </Providers>,
    );

    await expandExercise(user, "Back Squat");
    expect(screen.queryByText("Unsaved changes")).not.toBeInTheDocument();

    const firstWeight = screen.getByDisplayValue("100");
    await user.clear(firstWeight);
    await user.type(firstWeight, "105");

    expect(await screen.findByText("Unsaved changes")).toBeInTheDocument();
  });

  it("sets a day focus, filters the picker to it, and warns on an off-focus add", async () => {
    const calls = installFetch();
    const user = userEvent.setup();
    render(
      <Providers>
        <DayEditor dayId="d1" programId="p1" onClose={() => {}} />
      </Providers>,
    );

    await screen.findByText("Back Squat");

    // Pick the "Legs" group as this day's focus (it expands to Quads + co.).
    await user.click(await screen.findByRole("button", { name: "Legs", pressed: false }));

    // Open the picker — it opens with the day's focus group pre-selected.
    await user.click(screen.getByRole("button", { name: /add exercise/i }));
    const picker = await screen.findByRole("dialog", { name: "Add exercise" });
    expect(within(picker).getByRole("button", { name: "Legs", pressed: true })).toBeInTheDocument();
    // The off-focus exercise is filtered out until we widen the filter.
    expect(within(picker).queryByRole("button", { name: /Overhead Press/ })).not.toBeInTheDocument();

    // Switch to "All", then add the off-focus one -> inline note in the editor.
    await user.click(within(picker).getByRole("button", { name: "All" }));
    await user.click(await within(picker).findByRole("button", { name: /Overhead Press/ }));
    expect(await screen.findByText(/outside this day's focus/i)).toBeInTheDocument();

    // Focus id rides along on save.
    await user.click(screen.getByRole("button", { name: /Save day/i }));
    const put = calls.find((c) => c.method === "PUT" && c.url.includes("/workout-days/d1"));
    expect((put!.body as { focusMuscleIds: number[] }).focusMuscleIds).toEqual([2]);
  });

  it("asks before discarding unsaved changes", async () => {
    installFetch();
    const onClose = vi.fn();
    const user = userEvent.setup();
    render(
      <Providers>
        <DayEditor dayId="d1" programId="p1" onClose={onClose} />
      </Providers>,
    );

    await expandExercise(user, "Back Squat");
    const firstWeight = screen.getByDisplayValue("100");
    await user.clear(firstWeight);
    await user.type(firstWeight, "105");

    await user.click(screen.getByRole("button", { name: "Close" }));
    expect(onClose).not.toHaveBeenCalled();
    const bar = screen.getByText(/Discard your unsaved changes/i).closest("div")!;
    await user.click(within(bar).getByRole("button", { name: "Discard" }));
    expect(onClose).toHaveBeenCalled();
  });

  it("renames the day from the ⋯ menu and saves the new name", async () => {
    const calls = installFetch();
    const user = userEvent.setup();
    render(
      <Providers>
        <DayEditor dayId="d1" programId="p1" onClose={() => {}} />
      </Providers>,
    );

    await screen.findByText("Back Squat");
    await user.click(screen.getByRole("button", { name: "Day options" }));
    await user.click(screen.getByRole("button", { name: "Rename day" }));

    const nameInput = screen.getByRole("textbox", { name: "Day name" });
    await user.clear(nameInput);
    await user.type(nameInput, "Leg Day A");
    expect(await screen.findByText("Unsaved changes")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /Save day/i }));
    const put = calls.find((c) => c.method === "PUT" && c.url.includes("/workout-days/d1"));
    expect((put!.body as { name: string }).name).toBe("Leg Day A");
  });

  it("offers Delete day in the ⋯ menu, wired to onDeleteDay", async () => {
    installFetch();
    const onDeleteDay = vi.fn();
    const user = userEvent.setup();
    render(
      <Providers>
        <DayEditor dayId="d1" programId="p1" onClose={() => {}} onDeleteDay={onDeleteDay} />
      </Providers>,
    );

    await screen.findByText("Back Squat");
    await user.click(screen.getByRole("button", { name: "Day options" }));
    await user.click(screen.getByRole("button", { name: "Delete day" }));
    expect(onDeleteDay).toHaveBeenCalled();
  });
});
