import { createBrowserRouter, Navigate } from "react-router";
import { AppShell } from "./AppShell";
import { OnboardingReviewScreen } from "../features/onboarding/OnboardingReviewScreen";
import { DashboardScreen } from "../features/dashboard/DashboardScreen";
import { WorkoutBuilderScreen } from "../features/workouts/WorkoutBuilderScreen";
import { ActiveWorkoutScreen } from "../features/workouts/ActiveWorkoutScreen";
import { NutritionScreen } from "../features/nutrition/NutritionScreen";
import { AddFoodScreen } from "../features/nutrition/AddFoodScreen";
import { ProgressScreen } from "../features/progress/ProgressScreen";
import { SettingsScreen } from "../features/settings/SettingsScreen";

export const router = createBrowserRouter([
  // Onboarding renders full-bleed, outside the app shell.
  { path: "/onboarding", element: <OnboardingReviewScreen /> },
  {
    path: "/",
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
]);
