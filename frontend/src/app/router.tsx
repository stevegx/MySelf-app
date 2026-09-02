import { createBrowserRouter, Navigate } from "react-router";
import { AppShell } from "./AppShell";
import { RedirectIfAuthenticated } from "./RedirectIfAuthenticated";
import { RequireAuth } from "./RequireAuth";
import { ForgotPasswordScreen } from "../features/auth/ForgotPasswordScreen";
import { LoginScreen } from "../features/auth/LoginScreen";
import { RegisterScreen } from "../features/auth/RegisterScreen";
import { ResetPasswordScreen } from "../features/auth/ResetPasswordScreen";
import { OnboardingReviewScreen } from "../features/onboarding/OnboardingReviewScreen";
import { DashboardScreen } from "../features/dashboard/DashboardScreen";
import { WorkoutBuilderScreen } from "../features/workouts/WorkoutBuilderScreen";
import { ActiveWorkoutScreen } from "../features/workouts/ActiveWorkoutScreen";
import { NutritionScreen } from "../features/nutrition/NutritionScreen";
import { AddFoodScreen } from "../features/nutrition/AddFoodScreen";
import { ProgressScreen } from "../features/progress/ProgressScreen";
import { SettingsScreen } from "../features/settings/SettingsScreen";

export const router = createBrowserRouter([
  // Login/register render full-bleed, outside the app shell, and redirect away if a
  // session already exists (RedirectIfAuthenticated) — no point re-showing the form.
  {
    element: <RedirectIfAuthenticated />,
    children: [
      { path: "/login", element: <LoginScreen /> },
      { path: "/register", element: <RegisterScreen /> },
    ],
  },
  { path: "/onboarding", element: <OnboardingReviewScreen /> },
  // Reachable whether or not a session exists — e.g. resetting a password from another
  // device while still signed in here (not wrapped in RedirectIfAuthenticated).
  { path: "/forgot-password", element: <ForgotPasswordScreen /> },
  { path: "/reset-password", element: <ResetPasswordScreen /> },
  {
    // Everything under the app shell requires a session — RequireAuth redirects to
    // /login otherwise (docs/05: the app's business screens are all owner-scoped).
    path: "/",
    element: <RequireAuth />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <Navigate to="/dashboard" replace /> },
          { path: "dashboard", element: <DashboardScreen /> },
          { path: "workouts", element: <Navigate to="/workouts/builder" replace /> },
          { path: "workouts/builder", element: <WorkoutBuilderScreen /> },
          { path: "workouts/active", element: <ActiveWorkoutScreen /> },
          { path: "nutrition", element: <NutritionScreen /> },
          { path: "nutrition/add", element: <AddFoodScreen /> },
          { path: "progress", element: <ProgressScreen /> },
          { path: "settings", element: <SettingsScreen /> },
          { path: "*", element: <Navigate to="/dashboard" replace /> },
        ],
      },
    ],
  },
]);
