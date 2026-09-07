import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import { useAuth } from "../auth/auth";

function useToken() {
  return useAuth().session?.accessToken;
}

export type ServingBasis = "Per100g" | "PerServing";
export type MealAmountUnit = "Grams" | "Millilitres" | "Serving";
export const MEAL_CATEGORIES = ["Breakfast", "Lunch", "Dinner", "Snacks"] as const;
export type MealCategory = (typeof MEAL_CATEGORIES)[number];

export type Nutrients = { kcal: number; proteinG: number; carbG: number; fatG: number };
export type NutrientTargets = {
  kcal: number | null;
  proteinG: number | null;
  carbG: number | null;
  fatG: number | null;
};

export type MealItem = {
  id: string;
  sortOrder: number;
  name: string;
  servingBasis: ServingBasis;
  servingSizeGrams: number | null;
  amount: number;
  unit: MealAmountUnit;
  basisKcal: number;
  basisProteinG: number;
  basisCarbG: number;
  basisFatG: number;
} & Nutrients;

export type Meal = {
  mealLogId: string | null;
  category: MealCategory;
  subtotals: Nutrients;
  items: MealItem[];
};

export type NutritionDay = {
  date: string;
  targets: NutrientTargets;
  totals: Nutrients;
  meals: Meal[];
};

export type AddMealItemBody = {
  category: MealCategory;
  name: string;
  servingBasis: ServingBasis;
  servingSizeGrams?: number | null;
  perBasisKcal: number;
  perBasisProteinG: number;
  perBasisCarbG: number;
  perBasisFatG: number;
  amount: number;
  unit: MealAmountUnit;
};

export function useNutritionDay(date: string) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["nutrition-day", date],
    queryFn: () => apiFetch<NutritionDay>(`/api/v1/nutrition-days/${date}`, { accessToken }),
    enabled: accessToken != null,
  });
}

function useInvalidateDay(date: string) {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: ["nutrition-day", date] });
}

export function useAddMealItem(date: string) {
  const accessToken = useToken();
  const invalidate = useInvalidateDay(date);
  return useMutation({
    mutationFn: (body: AddMealItemBody) =>
      apiFetch<NutritionDay>(`/api/v1/nutrition-days/${date}/items`, { method: "POST", body, accessToken }),
    onSuccess: invalidate,
  });
}

export function useUpdateMealItem(date: string) {
  const accessToken = useToken();
  const invalidate = useInvalidateDay(date);
  return useMutation({
    mutationFn: ({
      id,
      amount,
      unit,
      servingSizeGrams,
    }: {
      id: string;
      amount: number;
      unit: MealAmountUnit;
      servingSizeGrams?: number | null;
    }) =>
      apiFetch<NutritionDay>(`/api/v1/meal-log-items/${id}`, {
        method: "PUT",
        body: { amount, unit, servingSizeGrams: servingSizeGrams ?? null },
        accessToken,
      }),
    onSuccess: invalidate,
  });
}

export function useDeleteMealItem(date: string) {
  const accessToken = useToken();
  const invalidate = useInvalidateDay(date);
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/v1/meal-log-items/${id}`, { method: "DELETE", accessToken }),
    onSuccess: invalidate,
  });
}

// --- My Foods ---

export type CustomFood = {
  id: string;
  name: string;
  brand: string | null;
  barcode: string | null;
  servingBasis: ServingBasis;
  servingSizeGrams: number | null;
  kcal: number;
  proteinG: number;
  carbG: number;
  fatG: number;
};

export type CreateCustomFoodBody = Omit<CustomFood, "id">;

/** Search the caller's saved My Foods by name/brand (empty query lists all). */
export function useFoodSearch(query: string) {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["my-foods", query],
    queryFn: () =>
      apiFetch<CustomFood[]>(`/api/v1/foods/search?q=${encodeURIComponent(query)}`, { accessToken }),
    enabled: accessToken != null,
  });
}

export function useCreateCustomFood() {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateCustomFoodBody) =>
      apiFetch<CustomFood>("/api/v1/foods/custom", { method: "POST", body, accessToken }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["my-foods"] }),
  });
}

// --- Barcode (Open Food Facts) ---

export type BarcodeFood = {
  barcode: string;
  name: string | null;
  brand: string | null;
  source: string;
  license: string;
  per100g: {
    energyKcal: number | null;
    protein: number | null;
    carbs: number | null;
    fat: number | null;
  };
  servingQuantityGrams: number | null;
};

/** Look up a packaged food by barcode via the backend (Open Food Facts, cached). */
export function useBarcodeLookup() {
  const accessToken = useToken();
  return useMutation({
    mutationFn: (code: string) =>
      apiFetch<BarcodeFood>(`/api/v1/foods/barcode/${encodeURIComponent(code)}`, { accessToken }),
  });
}

// --- Saved Meals ---

export type SavedMealItem = {
  id: string;
  sortOrder: number;
  name: string;
  servingBasis: ServingBasis;
  servingSizeGrams: number | null;
  perBasisKcal: number;
  perBasisProteinG: number;
  perBasisCarbG: number;
  perBasisFatG: number;
  defaultAmount: number;
  unit: MealAmountUnit;
} & Nutrients;

export type SavedMeal = {
  id: string;
  name: string;
  category: MealCategory;
  notes: string | null;
  totals: Nutrients;
  items: SavedMealItem[];
};

export type SaveMealItemInput = {
  name: string;
  servingBasis: ServingBasis;
  servingSizeGrams: number | null;
  perBasisKcal: number;
  perBasisProteinG: number;
  perBasisCarbG: number;
  perBasisFatG: number;
  defaultAmount: number;
  unit: MealAmountUnit;
};

export function useSavedMeals() {
  const accessToken = useToken();
  return useQuery({
    queryKey: ["saved-meals"],
    queryFn: () => apiFetch<SavedMeal[]>("/api/v1/saved-meals", { accessToken }),
    enabled: accessToken != null,
  });
}

export function useCreateSavedMeal() {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; category: MealCategory; notes?: string | null; items: SaveMealItemInput[] }) =>
      apiFetch<SavedMeal>("/api/v1/saved-meals", { method: "POST", body, accessToken }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["saved-meals"] }),
  });
}

export function useDeleteSavedMeal() {
  const accessToken = useToken();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiFetch<void>(`/api/v1/saved-meals/${id}`, { method: "DELETE", accessToken }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["saved-meals"] }),
  });
}

export function useAddSavedMealToDay(date: string) {
  const accessToken = useToken();
  const invalidate = useInvalidateDay(date);
  return useMutation({
    mutationFn: ({ id, category, multiplier }: { id: string; category?: MealCategory; multiplier: number }) =>
      apiFetch<NutritionDay>(`/api/v1/saved-meals/${id}/add-to-day`, {
        method: "POST",
        body: { date, category: category ?? null, multiplier },
        accessToken,
      }),
    onSuccess: invalidate,
  });
}
