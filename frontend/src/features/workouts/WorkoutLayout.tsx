import { NavLink, Outlet } from "react-router";
import { Dumbbell } from "lucide-react";
import { useActiveSession } from "./api";

const TABS = [
  { to: "/workouts/builder", label: "Programs" },
  { to: "/workouts/history", label: "History" },
  { to: "/workouts/calendar", label: "Calendar" },
];

/** Shared header + tab strip for every /workouts/* screen so they navigate as one section. */
export function WorkoutLayout() {
  const { data: active } = useActiveSession();

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-1 border-b border-border pb-2">
        {active && (
          <NavLink
            to="/workouts/active"
            className="mr-2 inline-flex items-center gap-1.5 rounded-full bg-primary px-3 py-1.5 text-[13px] font-semibold text-on-primary"
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
              `rounded-control px-3 py-1.5 text-[13px] font-medium ${
                isActive ? "bg-primary-soft text-primary-pressed" : "text-foreground-muted hover:bg-surface-subtle"
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
