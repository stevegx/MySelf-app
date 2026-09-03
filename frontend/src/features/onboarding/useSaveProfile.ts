import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import { useAuth } from "../auth/auth";

export type SaveProfileInput = {
  unitSystem: "Metric" | "Imperial";
  dateOfBirth: string; // "YYYY-MM-DD"
  heightCm: number;
  calculationSex: "Male" | "Female" | null;
  timezone?: string | null;
};

/**
 * PUT /api/v1/me/profile (onboarding step 1). Create-or-update and idempotent, so the wizard
 * calls it right before POST /me/onboarding/complete, which refuses to run without a saved
 * profile.
 */
export function useSaveProfile() {
  const { session } = useAuth();
  return useMutation({
    mutationFn: (input: SaveProfileInput) =>
      apiFetch<unknown>("/api/v1/me/profile", {
        method: "PUT",
        body: input,
        accessToken: session?.accessToken,
      }),
  });
}
