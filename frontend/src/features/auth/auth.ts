import { createContext, useContext } from "react";

export type AuthUser = { id: string; username: string; email: string };
export type AuthSession = { accessToken: string; user: AuthUser };

/**
 * "loading" only while the app's initial silent /auth/refresh attempt (AuthContext.tsx)
 * is still in flight — RequireAuth/RedirectIfAuthenticated wait for it to resolve rather
 * than guessing, so a returning "trust this device" session doesn't flash the login screen.
 */
export type AuthStatus = "loading" | "authenticated" | "unauthenticated";

export type AuthContextValue = {
  session: AuthSession | null;
  status: AuthStatus;
  setSession: (session: AuthSession | null) => void;
};

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within <AuthProvider>");
  }
  return ctx;
}
