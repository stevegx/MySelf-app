import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import { useAuth } from "./auth";
import type { AuthUser } from "./auth";

/** The onboarding profile (docs/04 UserProfile). Null until the user saves onboarding step 1. */
export type ProfileSummary = {
  dateOfBirth: string; // ISO date, e.g. "1994-03-21"
  heightCm: number;
  calculationSex: "Male" | "Female" | null;
  unitSystem: "Metric" | "Imperial";
  timezone: string | null;
  locale: string | null;
  onboardingCompletedAt: string | null;
  warnOffFocusExercises: boolean;
};

/** One nutrition goal (docs/04 UserGoal). Targets are null when nutrition was skipped or the goal is Track-only. */
export type GoalSummary = {
  id: string;
  goalType: "Lose" | "Maintain" | "Gain" | "TrackOnly";
  source: "Estimated" | "Manual";
  targetWeightKg: number | null;
  calorieTarget: number | null;
  proteinGrams: number | null;
  carbGrams: number | null;
  fatGrams: number | null;
  effectiveFrom: string;
};

export type MeResponse = {
  user: AuthUser;
  profile: ProfileSummary | null;
  currentGoal: GoalSummary | null;
};

/**
 * Calls the protected GET /api/v1/me — the account summary plus the onboarding profile.
 * Also the proof the JWT-bearer middleware works, rather than only trusting the cached
 * login/register response.
 */
export function useMe() {
  const { session } = useAuth();

  return useQuery({
    queryKey: ["me", session?.accessToken],
    queryFn: () => apiFetch<MeResponse>("/api/v1/me", { accessToken: session?.accessToken }),
    enabled: session !== null,
  });
}
