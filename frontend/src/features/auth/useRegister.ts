import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import type { AuthUser } from "./auth";

export type RegisterResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
};

export function useRegister() {
  return useMutation({
    mutationFn: (input: { username: string; email: string; password: string; trustThisDevice: boolean }) =>
      apiFetch<RegisterResponse>("/api/v1/auth/register", { method: "POST", body: input }),
  });
}
