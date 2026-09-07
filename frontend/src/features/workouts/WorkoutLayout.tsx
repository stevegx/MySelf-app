import { NavLink, Outlet } from "react-router";
import { Dumbbell } from "lucide-react";
import { useActiveSession } from "./api";

const TABS = [
  { to: "/workouts/builder", label: "Train" },
  { to: "/workouts/history", label: "History" },
  { to: "/workouts/calendar", label: "Calendar" },
];

/** Shared header + tab strip for every /workouts/* screen so they navigate as one section. */
export function WorkoutLayout() {
  const { data: active } = useActiveSession();

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-1 pb-2">
        {active && (
          <NavLink
            to="/workouts/active"
            className="mr-2 inline-flex items-center gap-1.5 rounded-pill bg-primary px-3 py-1.5 text-sm font-semibold text-on-primary"
          >
            <Dumbbell size={14} aria-hidden />
            Resume workout
          </NavLink>
        )}
        {TABS.map((t) => (
          <NavLink
            key={t.to}
            to={t.to}
            end={t.to === "/workouts/builder"}
            className={({ isActive }) =>
              `rounded-pill px-3.5 py-1.5 text-sm font-medium ${
                isActive ? "bg-primary text-on-primary" : "text-foreground-muted hover:bg-surface-subtle"
              }`
            }
          >
            {t.label}
          </NavLink>
        ))}
      </div>
      <Outlet />
    </>
  );
}
