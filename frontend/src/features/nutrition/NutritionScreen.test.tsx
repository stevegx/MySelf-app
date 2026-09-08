import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Providers } from "../../app/providers";
import { NutritionScreen } from "./NutritionScreen";
import type { MealCategory, NutritionDay } from "./api";

const AUTH = { accessToken: "t", user: { id: "u1", username: "d", email: "d@e.com" } };

function emptyDay(date: string): NutritionDay {
  return {
    date,
    targets: { kcal: 2000, proteinG: 150, carbG: 200, fatG: 60 },
    totals: { kcal: 0, proteinG: 0, carbG: 0, fatG: 0 },
    meals: (["Breakfast", "Lunch", "Dinner", "Snacks"] as MealCategory[]).map((category) => ({
      mealLogId: null,
      category,
      subtotals: { kcal: 0, proteinG: 0, carbG: 0, fatG: 0 },
      items: [],
    })),
  };
}

function dayWithLunchItem(date: string) {
  const d = emptyDay(date);
  const lunch = d.meals.find((m) => m.category === "Lunch")!;
  lunch.mealLogId = "ml1";
  lunch.items = [
    {
      id: "i1", sortOrder: 0, name: "Plain yogurt", servingBasis: "Per100g", servingSizeGrams: null,
      amount: 150, unit: "Grams", kcal: 78, proteinG: 5.1, carbG: 7.5, fatG: 2.6,
      basisKcal: 52, basisProteinG: 3.4, basisCarbG: 5, basisFatG: 1.7,
    },
  ];
  lunch.subtotals = { kcal: 78, proteinG: 5.1, carbG: 7.5, fatG: 2.6 };
  d.totals = { ...lunch.subtotals };
  return d;
}

function dayWithTwoLunchItems(date: string) {
  const d = dayWithLunchItem(date);
  const lunch = d.meals.find((m) => m.category === "Lunch")!;
  lunch.items.push({
    id: "i2", sortOrder: 1, name: "Chicken breast", servingBasis: "Per100g", servingSizeGrams: null,
    amount: 200, unit: "Grams", kcal: 330, proteinG: 62, carbG: 0, fatG: 7.2,
    basisKcal: 165, basisProteinG: 31, basisCarbG: 0, basisFatG: 3.6,
  });
  lunch.subtotals = { kcal: 408, proteinG: 67.1, carbG: 7.5, fatG: 9.8 };
  d.totals = { ...lunch.subtotals };
  return d;
}

const DEFAULT_CATEGORIES = [
  { id: "c1", name: "Breakfast", sortOrder: 0 },
  { id: "c2", name: "Lunch", sortOrder: 1 },
  { id: "c3", name: "Dinner", sortOrder: 2 },
  { id: "c4", name: "Snacks", sortOrder: 3 },
];

const SAVED_FOOD = {
  id: "f1", name: "Chicken breast", brand: "Farm", barcode: null,
  servingBasis: "Per100g", servingSizeGrams: null, kcal: 165, proteinG: 31, carbG: 0, fatG: 3.6,
};

function installFetch(
  day: unknown,
  opts: { myFoods?: unknown[]; savedMeals?: unknown[]; categories?: unknown[] } = {},
) {
  const calls: { url: string; method: string; body: unknown }[] = [];
  let current = day;
  const spy = vi.fn<typeof fetch>((input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    const parsedBody = init?.body ? JSON.parse(init.body as string) : undefined;
    calls.push({ url, method, body: parsedBody });
    const json = (b: unknown, status = 200) =>
      Promise.resolve(new Response(JSON.stringify(b), { status, headers: { "content-type": "application/json" } }));

    if (url.includes("/auth/refresh")) return json(AUTH);

    if (url.endsWith("/api/v1/meal-categories") && method === "GET") return json(opts.categories ?? DEFAULT_CATEGORIES);
    if (url.endsWith("/api/v1/meal-categories") && method === "POST")
      return json({ id: "c-new", name: parsedBody.name, sortOrder: 4 }, 201);
    if (url.endsWith("/api/v1/meal-categories/reorder") && method === "PUT") return json(DEFAULT_CATEGORIES);
    if (url.match(/\/api\/v1\/meal-categories\/[\w-]+$/) && method === "PUT")
      return json({ id: "c1", name: parsedBody.name, sortOrder: 0 });
    if (url.match(/\/api\/v1\/meal-categories\/[\w-]+$/) && method === "DELETE")
      return Promise.resolve(new Response(null, { status: 204 }));

    if (url.match(/\/api\/v1\/nutrition-days\/[\d-]+\/items\/bulk-(delete|move|copy|add)$/) && method === "POST") {
      current = url.endsWith("bulk-add") ? dayWithLunchItem((current as NutritionDay).date) : emptyDay((current as NutritionDay).date);
      return json(current);
    }

    if (url.includes("/api/v1/foods/search")) return json(opts.myFoods ?? []);
    if (url.includes("/api/v1/foods/barcode/")) {
      return json({
        barcode: "5000112637922", name: "Diet Cola", brand: "Cola Co", source: "Open Food Facts",
        license: "ODbL", per100g: { energyKcal: 0.4, protein: 0, carbs: 0, fat: 0 }, servingQuantityGrams: 330,
      });
    }
    if (url.includes("/api/v1/foods/custom") && method === "POST") return json({ ...SAVED_FOOD, id: "new" }, 201);
    if (url.match(/\/api\/v1\/saved-meals\/[\w-]+\/add-to-day$/) && method === "POST") {
      current = dayWithLunchItem((current as NutritionDay).date);
      return json(current);
    }
    if (url.endsWith("/api/v1/saved-meals") && method === "GET") return json(opts.savedMeals ?? []);
    if (url.endsWith("/api/v1/saved-meals") && method === "POST") return json({ id: "sm-new" }, 201);
    if (url.match(/\/api\/v1\/nutrition-days\/[\d-]+\/items$/) && method === "POST") {
      current = dayWithLunchItem((init!.body ? JSON.parse(init!.body as string).category : ""));
      return json(current);
    }
    if (url.match(/\/api\/v1\/meal-log-items\/\w+$/) && method === "DELETE") {
      current = emptyDay((current as NutritionDay).date);
      return Promise.resolve(new Response(null, { status: 204 }));
    }
    if (url.includes("/api/v1/nutrition-days/")) return json(current);
    return Promise.resolve(new Response(null, { status: 404 }));
  });
  vi.stubGlobal("fetch", spy);
  return calls;
}

function renderScreen() {
  const router = createMemoryRouter([{ path: "/", element: <NutritionScreen /> }], { initialEntries: ["/"] });
  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}

describe("NutritionScreen", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("shows the day's totals against targets and the four meal slots", async () => {
    installFetch(dayWithLunchItem("2026-09-08"));
    renderScreen();

    // Calorie headline: 78 logged / 2000 target.
    expect(await screen.findByText(/\/ 2000 kcal/)).toBeInTheDocument();
    expect(screen.getByText(/1922 kcal left/)).toBeInTheDocument();
    for (const c of ["Breakfast", "Lunch", "Dinner", "Snacks"]) {
      expect(screen.getByText(c)).toBeInTheDocument();
    }
    expect(screen.getByText("Plain yogurt")).toBeInTheDocument();
  });

  it("adds a food to a meal via the dialog", async () => {
    const calls = installFetch(emptyDay("2026-09-08"));
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Add food to Lunch" }));

    const dialog = await screen.findByRole("dialog", { name: /Add food to Lunch/i });
    await user.type(within(dialog).getByLabelText("Name"), "Plain yogurt");
    await user.type(within(dialog).getByLabelText(/Calories/i), "52");
    await user.type(within(dialog).getByLabelText("Protein g"), "3.4");
    await user.type(within(dialog).getByLabelText("Carbs g"), "5");
    await user.type(within(dialog).getByLabelText("Fat g"), "1.7");
    await user.type(within(dialog).getByLabelText("Amount eaten"), "150");
    await user.click(within(dialog).getByRole("button", { name: "Add" }));

    await waitFor(() => {
      const post = calls.find((c) => c.method === "POST" && c.url.endsWith("/items"));
      expect(post).toBeTruthy();
      expect(post!.body).toMatchObject({ category: "Lunch", name: "Plain yogurt", perBasisKcal: 52, amount: 150 });
    });
  });

  it("prefills the form from a saved My Food, then can re-save on add", async () => {
    const calls = installFetch(emptyDay("2026-09-08"), { myFoods: [SAVED_FOOD] });
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Add food to Dinner" }));
    const dialog = await screen.findByRole("dialog", { name: /Add food to Dinner/i });

    await user.type(within(dialog).getByLabelText(/Search My Foods/i), "chick");
    await user.click(await within(dialog).findByRole("button", { name: /Chicken breast/i }));

    // The manual fields are now filled from the saved food.
    expect(within(dialog).getByLabelText("Name")).toHaveValue("Chicken breast");
    expect(within(dialog).getByLabelText(/Calories/i)).toHaveValue(165);

    await user.type(within(dialog).getByLabelText("Amount eaten"), "200");
    await user.click(within(dialog).getByLabelText("Save to My Foods"));
    await user.click(within(dialog).getByRole("button", { name: "Add" }));

    await waitFor(() => {
      expect(calls.some((c) => c.method === "POST" && c.url.endsWith("/items"))).toBe(true);
      expect(calls.some((c) => c.method === "POST" && c.url.endsWith("/foods/custom"))).toBe(true);
    });
  });

  it("prefills the form from a barcode lookup", async () => {
    installFetch(emptyDay("2026-09-08"));
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Add food to Snacks" }));
    const dialog = await screen.findByRole("dialog", { name: /Add food to Snacks/i });

    await user.type(within(dialog).getByLabelText("Barcode"), "5000112637922");
    await user.click(within(dialog).getByRole("button", { name: "Look up" }));

    await waitFor(() => expect(within(dialog).getByLabelText("Name")).toHaveValue("Diet Cola"));
    expect(within(dialog).getByLabelText(/Calories/i)).toHaveValue(0.4);
    expect(within(dialog).getByText(/Open Food Facts · ODbL/)).toBeInTheDocument();
    expect(within(dialog).getByLabelText("Save to My Foods")).toBeChecked();
  });

  it("adds a saved meal to the day via its chip", async () => {
    const savedMeal = {
      id: "sm1", name: "Chicken & Rice", category: "Lunch", notes: null,
      totals: { kcal: 525, proteinG: 66, carbG: 42, fatG: 7.6 },
      items: [{ id: "x", sortOrder: 0, name: "Chicken", servingBasis: "Per100g", servingSizeGrams: null, perBasisKcal: 165, perBasisProteinG: 31, perBasisCarbG: 0, perBasisFatG: 3.6, defaultAmount: 200, unit: "Grams", kcal: 330, proteinG: 62, carbG: 0, fatG: 7.2 }],
    };
    const calls = installFetch(emptyDay("2026-09-08"), { savedMeals: [savedMeal] });
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Chicken & Rice" }));
    const dialog = await screen.findByRole("dialog", { name: /Add Chicken & Rice/i });
    await user.click(within(dialog).getByRole("button", { name: "0.5×" }));
    await user.click(within(dialog).getByRole("button", { name: "Add to day" }));

    await waitFor(() => {
      const post = calls.find((c) => c.method === "POST" && c.url.endsWith("/saved-meals/sm1/add-to-day"));
      expect(post).toBeTruthy();
      expect(post!.body).toMatchObject({ multiplier: 0.5, category: "Lunch" });
    });
  });

  it("saves a populated meal as a template", async () => {
    const calls = installFetch(dayWithLunchItem("2026-09-08"));
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Save as meal" }));
    const dialog = await screen.findByRole("dialog", { name: /Save Lunch as a meal/i });
    await user.type(within(dialog).getByLabelText("Name"), "Yogurt bowl");
    await user.click(within(dialog).getByRole("button", { name: "Save meal" }));

    await waitFor(() => {
      const post = calls.find((c) => c.method === "POST" && c.url.endsWith("/saved-meals"));
      expect(post).toBeTruthy();
      const body = post!.body as { name: string; category: string; items: { name: string; perBasisKcal: number; defaultAmount: number }[] };
      expect(body).toMatchObject({ name: "Yogurt bowl", category: "Lunch" });
      expect(body.items[0]).toMatchObject({ name: "Plain yogurt", perBasisKcal: 52, defaultAmount: 150 });
    });
  });

  it("removes a logged item", async () => {
    const calls = installFetch(dayWithLunchItem("2026-09-08"));
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Remove Plain yogurt" }));
    await waitFor(() =>
      expect(calls.some((c) => c.method === "DELETE" && c.url.includes("/meal-log-items/i1"))).toBe(true),
    );
  });

  it("adds a custom meal category from the manage dialog", async () => {
    const calls = installFetch(emptyDay("2026-09-08"));
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Manage" }));
    const dialog = await screen.findByRole("dialog", { name: /Manage meal categories/i });
    await user.type(within(dialog).getByLabelText("New category name"), "Pre-workout");
    await user.click(within(dialog).getByRole("button", { name: "Add" }));

    await waitFor(() => {
      const post = calls.find((c) => c.method === "POST" && c.url.endsWith("/api/v1/meal-categories"));
      expect(post).toBeTruthy();
      expect(post!.body).toMatchObject({ name: "Pre-workout" });
    });
  });

  it("bulk-deletes selected items and offers undo", async () => {
    const calls = installFetch(dayWithTwoLunchItems("2026-09-08"));
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Select" }));
    await user.click(await screen.findByRole("checkbox", { name: "Plain yogurt" }));
    await user.click(screen.getByRole("checkbox", { name: "Chicken breast" }));
    await user.click(screen.getByRole("button", { name: "Delete" }));

    await waitFor(() => {
      const post = calls.find((c) => c.method === "POST" && c.url.endsWith("/items/bulk-delete"));
      expect(post).toBeTruthy();
      expect(post!.body).toMatchObject({ ids: ["i1", "i2"] });
    });

    // The undo bar appears; clicking it replays the snapshots through bulk-add.
    await user.click(await screen.findByRole("button", { name: "Undo" }));
    await waitFor(() => {
      const add = calls.find((c) => c.method === "POST" && c.url.endsWith("/items/bulk-add"));
      expect(add).toBeTruthy();
      const body = add!.body as { items: { name: string; category: string }[] };
      expect(body.items.map((i) => i.name)).toEqual(["Plain yogurt", "Chicken breast"]);
      expect(body.items[0].category).toBe("Lunch");
    });
  });

  it("bulk-moves selected items to another slot", async () => {
    const calls = installFetch(dayWithTwoLunchItems("2026-09-08"));
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Select" }));
    await user.click(await screen.findByRole("checkbox", { name: "Plain yogurt" }));
    await user.click(screen.getByRole("button", { name: "Move" }));

    const dialog = await screen.findByRole("dialog", { name: "Move to…" });
    await user.click(within(dialog).getByRole("button", { name: "Move to Dinner" }));

    await waitFor(() => {
      const post = calls.find((c) => c.method === "POST" && c.url.endsWith("/items/bulk-move"));
      expect(post).toBeTruthy();
      expect(post!.body).toMatchObject({ ids: ["i1"], toCategory: "Dinner" });
    });
  });
});
