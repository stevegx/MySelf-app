import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import type { AuthUser } from "./auth";

export type LoginResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
};

export function useLogin() {
  return useMutation({
    mutationFn: (input: { identifier: string; password: string; trustThisDevice: boolean }) =>
      apiFetch<LoginResponse>("/api/v1/auth/login", { method: "POST", body: input }),
  });
}
