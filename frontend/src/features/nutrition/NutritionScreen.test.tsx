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
    },
  ];
  lunch.subtotals = { kcal: 78, proteinG: 5.1, carbG: 7.5, fatG: 2.6 };
  d.totals = { ...lunch.subtotals };
  return d;
}

function installFetch(day: unknown) {
  const calls: { url: string; method: string; body: unknown }[] = [];
  let current = day;
  const spy = vi.fn<typeof fetch>((input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(init.body as string) : undefined });
    const json = (b: unknown, status = 200) =>
      Promise.resolve(new Response(JSON.stringify(b), { status, headers: { "content-type": "application/json" } }));

    if (url.includes("/auth/refresh")) return json(AUTH);
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

  it("removes a logged item", async () => {
    const calls = installFetch(dayWithLunchItem("2026-09-08"));
    const user = userEvent.setup();
    renderScreen();

    await user.click(await screen.findByRole("button", { name: "Remove Plain yogurt" }));
    await waitFor(() =>
      expect(calls.some((c) => c.method === "DELETE" && c.url.includes("/meal-log-items/i1"))).toBe(true),
    );
  });
});
