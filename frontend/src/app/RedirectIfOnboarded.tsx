import { Navigate, Outlet } from "react-router";
import { useMe } from "../features/auth/useMe";

/** Wraps /onboarding: a user who has already completed it skips straight to the dashboard. */
export function RedirectIfOnboarded() {
  const { data: me, isLoading } = useMe();

  if (isLoading) {
    return null;
  }

  if (me?.profile?.onboardingCompletedAt) {
    return <Navigate to="/dashboard" replace />;
  }

  return <Outlet />;
}
