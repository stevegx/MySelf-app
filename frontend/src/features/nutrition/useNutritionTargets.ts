import { useMe } from "../auth/useMe";
import type { GoalSummary } from "../auth/useMe";

export type NutritionTargets = {
  /** True once the user has a calorie target (i.e. not skipped / not Track-only). */
  hasTarget: boolean;
  calorieTarget: number | null;
  proteinGrams: number | null;
  carbGrams: number | null;
  fatGrams: number | null;
  goalType: GoalSummary["goalType"] | null;
};

/**
 * The signed-in user's current nutrition targets, straight from GET /api/v1/me's
 * currentGoal (set during onboarding). Screens read this instead of hardcoding numbers.
 */
export function useNutritionTargets(): NutritionTargets {
  const goal = useMe().data?.currentGoal ?? null;
  return {
    hasTarget: goal?.calorieTarget != null,
    calorieTarget: goal?.calorieTarget ?? null,
    proteinGrams: goal?.proteinGrams ?? null,
    carbGrams: goal?.carbGrams ?? null,
    fatGrams: goal?.fatGrams ?? null,
    goalType: goal?.goalType ?? null,
  };
}

/** Whole number with locale grouping; "—" when the target isn't set. */
export function formatTarget(value: number | null | undefined): string {
  return value == null ? "—" : Math.round(value).toLocaleString();
}
