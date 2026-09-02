import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";

export type ResetPasswordResponse = { message: string };

export function useResetPassword() {
  return useMutation({
    mutationFn: (input: { email: string; token: string; newPassword: string }) =>
      apiFetch<ResetPasswordResponse>("/api/v1/auth/reset-password", { method: "POST", body: input }),
  });
}
