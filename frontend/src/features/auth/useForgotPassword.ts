import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";

export type ForgotPasswordResponse = { message: string; resetLink: string | null };

export function useForgotPassword() {
  return useMutation({
    mutationFn: (input: { email: string }) =>
      apiFetch<ForgotPasswordResponse>("/api/v1/auth/forgot-password", { method: "POST", body: input }),
  });
}
