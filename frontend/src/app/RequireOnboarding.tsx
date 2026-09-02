import { Navigate, Outlet } from "react-router";
import { useMe } from "../features/auth/useMe";

/**
 * Sits between RequireAuth and the app shell: a signed-in user who hasn't finished
 * onboarding is sent to /onboarding. Renders nothing while GET /me is still in flight, so
 * the shell doesn't flash before the redirect.
 */
export function RequireOnboarding() {
  const { data: me, isLoading } = useMe();

  if (isLoading) {
    return null;
  }

  if (!me?.profile?.onboardingCompletedAt) {
    return <Navigate to="/onboarding" replace />;
  }

  return <Outlet />;
}
