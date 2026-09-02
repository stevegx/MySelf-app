import { Navigate, Outlet } from "react-router";
import { useAuth } from "../features/auth/auth";

/** Wraps /login and /register: an already-signed-in visitor skips straight to the dashboard. */
export function RedirectIfAuthenticated() {
  const { status } = useAuth();

  if (status === "loading") {
    return null;
  }

  if (status === "authenticated") {
    return <Navigate to="/dashboard" replace />;
  }

  return <Outlet />;
}
