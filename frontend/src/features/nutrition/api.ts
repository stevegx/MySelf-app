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
