import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import { useAuth } from "./auth";

export function useLogout() {
  const { setSession } = useAuth();

  return useMutation({
    mutationFn: () => apiFetch<void>("/api/v1/auth/logout", { method: "POST" }),
    // Clear the local session regardless of whether the network call itself succeeded —
    // the access token is gone from memory either way, and RequireAuth reacts to that by
    // redirecting to /login. No explicit navigate() needed.
    onSettled: () => setSession(null),
  });
}
