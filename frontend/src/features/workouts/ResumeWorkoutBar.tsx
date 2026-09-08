import { Link, useLocation } from "react-router";
import { Dumbbell } from "lucide-react";
import { useActiveSession } from "./api";

/**
 * A fixed pill shown on every screen while a workout is in progress — so you can always
 * get back to it. Hidden on the active-workout screen itself.
 */
export function ResumeWorkoutBar() {
  const { data: session } = useActiveSession();
  const { pathname } = useLocation();

  if (!session || pathname.startsWith("/workouts/active")) return null;

  const total = session.exercises.reduce((n, e) => n + e.sets.length, 0);
  const done = session.exercises.reduce(
    (n, e) => n + e.sets.filter((s) => s.completedAt || s.skippedAt).length,
    0,
  );

  return (
    <Link
      to="/workouts/active"
      className="fixed inset-x-0 bottom-[calc(3.5rem+env(safe-area-inset-bottom))] z-40 mx-auto flex w-[min(92%,420px)] items-center gap-2 rounded-full bg-primary px-4 py-2.5 text-on-primary shadow-lg no-underline nav:bottom-5 nav:left-[248px] nav:right-auto nav:mx-0"
    >
      <Dumbbell size={16} aria-hidden />
      <span className="text-sm font-semibold">Resume workout</span>
      <span className="ml-auto text-sm opacity-90">
        {session.dayName ?? "Quick workout"} · {done}/{total}
      </span>
    </Link>
  );
}
