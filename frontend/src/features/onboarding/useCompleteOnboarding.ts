import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import { useAuth } from "../auth/auth";
import type { GoalSummary } from "../auth/useMe";

export type CompleteOnboardingInput = {
  goalType: string;
  targetWeightKg?: number;
  estimate?: {
    weightKg: number;
    activityLevel: string;
    pace?: string | null;
  };
  manualTarget?: {
    calorieTarget: number;
    proteinGrams?: number;
    carbGrams?: number;
    fatGrams?: number;
  };
};

/**
 * POST /api/v1/me/onboarding/complete. On success it invalidates the ["me"] query so the
 * onboarding route guards (RequireOnboarding / RedirectIfOnboarded) re-evaluate with the
 * now-stamped profile — invalidateQueries resolves only after that refetch, so the caller
 * can navigate to /dashboard straight after awaiting the mutation without a flash.
 */
export function useCompleteOnboarding() {
  const { session } = useAuth();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: CompleteOnboardingInput) =>
      apiFetch<GoalSummary>("/api/v1/me/onboarding/complete", {
        method: "POST",
        body: input,
        accessToken: session?.accessToken,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["me"] }),
  });
}
