import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import { useAuth } from "./auth";
import type { AuthUser } from "./auth";

/** Calls the protected GET /api/v1/me — proves the JWT-bearer middleware actually works. */
export function useMe() {
  const { session } = useAuth();

  return useQuery({
    queryKey: ["me", session?.accessToken],
    queryFn: () => apiFetch<AuthUser>("/api/v1/me", { accessToken: session?.accessToken }),
    enabled: session !== null,
  });
}
