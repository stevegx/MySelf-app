import { Navigate, Outlet } from "react-router";
import { useAuth } from "../features/auth/auth";

/**
 * Gates the app shell routes behind a session. Waits out the initial silent-refresh check
 * ("loading") before deciding — otherwise a returning "trust this device" visitor would
 * flash the login page for a moment before landing back on the dashboard.
 */
export function RequireAuth() {
  const { status } = useAuth();

  if (status === "loading") {
    return null;
  }

  if (status === "unauthenticated") {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
