import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import { useAuth } from "../auth/auth";

export type EstimateRequest = {
  dateOfBirth: string;
  calculationSex: "Male" | "Female";
  heightCm: number;
  weightKg: number;
  activityLevel: string;
  goalType: string;
  pace?: string | null;
};

export type MacroTargets = {
  proteinGrams: number;
  fatGrams: number;
  carbGrams: number;
  proteinFactor: number;
  fatFactor: number;
};

export type NutritionEstimate = {
  formulaName: string;
  formulaVersion: string;
  nutritionEstimateAvailable: boolean;
  unavailableReason: string | null;
  age: number | null;
  bmr: number | null;
  activityFactor: number | null;
  maintenanceCalories: number | null;
  goalAdjustment: number | null;
  suggestedCalories: number | null;
  macros: MacroTargets | null;
  warnings: { code: string; message: string }[];
  disclaimer: string;
};

/** POST /api/v1/me/nutrition-estimate — the non-persisted breakdown for the review step. */
export function useNutritionEstimate() {
  const { session } = useAuth();

  return useMutation({
    mutationFn: (input: EstimateRequest) =>
      apiFetch<NutritionEstimate>("/api/v1/me/nutrition-estimate", {
        method: "POST",
        body: input,
        accessToken: session?.accessToken,
      }),
  });
}
