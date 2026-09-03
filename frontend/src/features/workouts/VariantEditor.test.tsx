import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Providers } from "../../app/providers";
import { VariantEditor } from "./VariantEditor";

const session = { accessToken: "test-token", user: { id: "u1", username: "demo", email: "demo@example.com" } };

const variant = {
  id: "v1",
  name: "Legs #1",
  sortOrder: 0,
  estimatedDurationMinutes: null,
  programRowVersion: 42,
  exercises: [
    {
      id: "ve1",
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
};

const program = {
  id: "p1",
  name: "PPL",
  splitLabel: null,
  isActive: false,
  createdAt: "2026-09-03T00:00:00Z",
  rowVersion: 42,
  groups: [{ id: "g1", name: "Legs", sortOrder: 0, variants: [{ id: "v1", name: "Legs #1", sortOrder: 0, exerciseCount: 1 }] }],
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
    if (url.includes("/api/v1/workout-variants/v1") && method === "GET") return json(variant);
    if (url.includes("/api/v1/workout-variants/v1") && method === "PUT") return json(variant);
    if (url.includes("/api/v1/programs/p1")) return json(program);
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return calls;
}

describe("VariantEditor", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("shows every prescribed set instead of flattening them", async () => {
    installFetch();
    render(
      <Providers>
        <VariantEditor variantId="v1" programId="p1" onClose={() => {}} />
      </Providers>,
    );

    await screen.findByText("Back Squat");
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
        <VariantEditor variantId="v1" programId="p1" onClose={() => {}} />
      </Providers>,
    );

    await screen.findByText("Back Squat");
    await user.click(screen.getByRole("button", { name: /Save variant/i }));

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
        <VariantEditor variantId="v1" programId="p1" onClose={() => {}} />
      </Providers>,
    );

    await screen.findByText("Back Squat");
    expect(screen.queryByText("Unsaved changes")).not.toBeInTheDocument();

    const firstWeight = screen.getByDisplayValue("100");
    await user.clear(firstWeight);
    await user.type(firstWeight, "105");

    expect(await screen.findByText("Unsaved changes")).toBeInTheDocument();
  });

  it("asks before discarding unsaved changes", async () => {
    installFetch();
    const onClose = vi.fn();
    const user = userEvent.setup();
    render(
      <Providers>
        <VariantEditor variantId="v1" programId="p1" onClose={onClose} />
      </Providers>,
    );

    await screen.findByText("Back Squat");
    const firstWeight = screen.getByDisplayValue("100");
    await user.clear(firstWeight);
    await user.type(firstWeight, "105");

    await user.click(screen.getByRole("button", { name: "Close" }));
    expect(onClose).not.toHaveBeenCalled();
    const bar = screen.getByText(/Discard your unsaved changes/i).closest("div")!;
    await user.click(within(bar).getByRole("button", { name: "Discard" }));
    expect(onClose).toHaveBeenCalled();
  });
});
