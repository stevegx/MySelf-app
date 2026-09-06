import { ChevronRight, Circle, Dumbbell, Scale, Utensils } from "lucide-react";
import { useNavigate } from "react-router";
import { Button, Card, CardKicker, PageHeader, Ring, Skeleton } from "../../components/ui";
import { cn } from "../../lib/cn";
import { useAuth } from "../auth/auth";
import { useMe } from "../auth/useMe";
import { formatTarget, useNutritionTargets } from "../nutrition/useNutritionTargets";
import { useActiveSession, useSessionHistory } from "../workouts/api";

const WEEK_DAYS = ["M", "T", "W", "T", "F", "S", "S"];

/** Monday-based index (0 = Mon … 6 = Sun). */
function mondayIndex(d: Date) {
  return (d.getDay() + 6) % 7;
}

function startOfWeek(d: Date) {
  const s = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  s.setDate(s.getDate() - mondayIndex(s));
  return s;
}

const SETUP_ITEMS: { label: string; to: string }[] = [
  { label: "Create your first workout program", to: "/workouts/builder" },
  { label: "Set up your workout days", to: "/workouts/builder" },
  { label: "Log your first meal", to: "/nutrition" },
  { label: "Log your weight", to: "/progress" },
];

function useWorkoutFrequency() {
  const { data, isLoading } = useSessionHistory();
  const items = data?.items ?? [];
  const today = new Date();
  const weekStart = startOfWeek(today);
  const monthKey = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`;

  // How many sessions fell on each day of the current week.
  const perDay = [0, 0, 0, 0, 0, 0, 0];
  let thisWeek = 0;
  let thisMonth = 0;
  for (const s of items) {
    if (!s.performedOnLocalDate) continue;
    if (s.performedOnLocalDate.startsWith(monthKey)) thisMonth += 1;
    const d = new Date(`${s.performedOnLocalDate}T00:00:00`);
    const diffDays = Math.floor((d.getTime() - weekStart.getTime()) / 86_400_000);
    if (diffDays >= 0 && diffDays < 7) {
      perDay[diffDays] += 1;
      thisWeek += 1;
    }
  }
  const max = Math.max(1, ...perDay);
  return { perDay, max, thisWeek, thisMonth, todayIndex: mondayIndex(today), isLoading };
}

export function DashboardScreen() {
  const navigate = useNavigate();
  const { session } = useAuth();
  const { data: me } = useMe();
  const username = (me?.user ?? session?.user)?.username;

  const targets = useNutritionTargets();
  const { data: active } = useActiveSession();
  const freq = useWorkoutFrequency();

  const startWorkout = () => navigate(active ? "/workouts/active" : "/workouts/builder");

  const macros = [
    { label: "Protein", target: targets.proteinGrams },
    { label: "Carbs", target: targets.carbGrams },
    { label: "Fat", target: targets.fatGrams },
  ];
  const calorieLabel = targets.hasTarget
    ? `Calories: 0 of ${formatTarget(targets.calorieTarget)} kcal`
    : "Calories logged today: 0 kcal";

  const todayLabel = new Date().toLocaleDateString(undefined, {
    weekday: "long",
    day: "numeric",
    month: "long",
  });

  return (
    <>
      <PageHeader
        title={username ? `Hello, ${username}` : "Hello"}
        subtitle={todayLabel}
        actions={
          <>
            <Button variant="secondary" onClick={() => navigate("/nutrition")}>
              <Utensils size={15} aria-hidden />
              Log meal
            </Button>
            <Button variant="secondary" onClick={startWorkout}>
              <Dumbbell size={15} aria-hidden />
              {active ? "Resume workout" : "Start workout"}
            </Button>
            <Button variant="primary" onClick={() => navigate("/progress")}>
              <Scale size={15} aria-hidden />
              Log weight
            </Button>
          </>
        }
      />

      <div className="grid grid-cols-[repeat(auto-fit,minmax(260px,1fr))] gap-4">
        <Card className="col-span-2">
          <CardKicker>Nutrition today</CardKicker>
          <div className="flex flex-wrap items-center gap-7 py-1">
            <Ring size={112} stroke={10} ariaLabel={calorieLabel} />
            <div className="flex flex-col gap-0.5">
              <div className="text-[26px] font-bold">
                0{" "}
                <span className="text-sm font-normal text-foreground-muted">
                  {targets.hasTarget ? `/ ${formatTarget(targets.calorieTarget)} kcal` : "kcal"}
                </span>
              </div>
              <div className="text-[13px] text-foreground-muted">
                {targets.hasTarget
                  ? "Nothing logged yet today"
                  : "No calorie target — set one in Settings"}
              </div>
            </div>
            <div className="ml-auto flex gap-[18px]">
              {macros.map((macro) => (
                <div key={macro.label} className="text-center">
                  <Ring
                    size={52}
                    stroke={6}
                    ariaLabel={`${macro.label}: 0 of ${macro.target == null ? "no" : formatTarget(macro.target)} g`}
                  />
                  <div className="mt-1 text-xs">{macro.label}</div>
                  <div className="text-[11px] text-foreground-muted">
                    {macro.target == null ? "—" : `0 / ${formatTarget(macro.target)}g`}
                  </div>
                </div>
              ))}
            </div>
          </div>
          <Button variant="primary" className="self-start" onClick={() => navigate("/nutrition")}>
            Log your first meal
          </Button>
        </Card>

        <Card>
          <CardKicker>Workout frequency</CardKicker>
          {freq.isLoading ? (
            <>
              <Skeleton className="h-16" />
              <Skeleton className="mb-2 h-3 w-40" />
            </>
          ) : (
          <>
          <div className="flex h-16 items-end gap-1.5 py-1.5">
            {WEEK_DAYS.map((day, i) => (
              <div key={i} className="flex flex-1 flex-col items-center gap-1">
                <div className="flex h-10 w-full items-end">
                  <div
                    className={cn(
                      "w-full rounded-full",
                      freq.perDay[i] > 0 ? "bg-primary" : "bg-viz-track",
                    )}
                    style={{ height: freq.perDay[i] > 0 ? `${(freq.perDay[i] / freq.max) * 100}%` : "4px" }}
                  />
                </div>
                <div className={cn("text-[10px]", i === freq.todayIndex ? "font-bold text-primary" : "text-foreground-muted")}>
                  {day}
                </div>
              </div>
            ))}
          </div>
          <p className="m-0 mb-2 flex-1 text-[13px] text-foreground-muted">
            {freq.thisWeek === 0
              ? "No completed workouts this week yet."
              : `${freq.thisWeek} this week · ${freq.thisMonth} this month`}
          </p>
          </>
          )}
          <Button variant="secondary" block onClick={startWorkout}>
            {active ? "Resume workout" : "Start a workout"}
          </Button>
        </Card>

        <Card>
          <CardKicker>Body weight</CardKicker>
          <p className="m-0 mb-2 flex-1 text-[13px] text-foreground-muted">
            No weight logs yet — log your weight to start a trend line.
          </p>
          <Button variant="secondary" block onClick={() => navigate("/progress")}>
            Log your weight
          </Button>
        </Card>

        <Card className="col-span-2">
          <CardKicker>Get set up</CardKicker>
          <div className="flex flex-col gap-0.5">
            {SETUP_ITEMS.map((item) => (
              <button
                key={item.label}
                onClick={() => navigate(item.to)}
                className="flex items-center gap-2.5 border-b border-border px-1 py-2.5 text-left hover:bg-surface-subtle"
              >
                <Circle size={16} className="text-primary" aria-hidden />
                <span className="flex-1 text-sm">{item.label}</span>
                <ChevronRight size={16} className="text-foreground-muted" aria-hidden />
              </button>
            ))}
          </div>
        </Card>
      </div>
    </>
  );
}
