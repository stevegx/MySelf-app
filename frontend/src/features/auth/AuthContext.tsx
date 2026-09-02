import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { apiFetch } from "../../lib/api";
import { AuthContext } from "./auth";
import type { AuthSession, AuthStatus } from "./auth";

type RefreshResponse = { accessToken: string; user: AuthSession["user"] };

/**
 * Holds the access token in memory only (docs/05: "no auth token in localStorage"). What
 * survives a page reload or browser restart is the HttpOnly refresh-token cookie the
 * backend sets on register/login — persistent ("trust this device" checked) or a
 * browser-session cookie (unchecked) per AuthEndpoints.SetRefreshCookie. On mount, this
 * silently tries to exchange that cookie for a fresh access token; failure (no cookie, or
 * it expired) is the normal "not logged in" outcome, not an error to surface.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSessionState] = useState<AuthSession | null>(null);
  const [status, setStatus] = useState<AuthStatus>("loading");

  useEffect(() => {
    let cancelled = false;

    apiFetch<RefreshResponse>("/api/v1/auth/refresh", { method: "POST" })
      .then((result) => {
        if (!cancelled) {
          setSessionState({ accessToken: result.accessToken, user: result.user });
          setStatus("authenticated");
        }
      })
      .catch(() => {
        if (!cancelled) {
          setStatus("unauthenticated");
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const setSession = (next: AuthSession | null) => {
    setSessionState(next);
    setStatus(next ? "authenticated" : "unauthenticated");
  };

  const value = useMemo(() => ({ session, status, setSession }), [session, status]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
