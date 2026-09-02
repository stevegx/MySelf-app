import { ChevronRight, Circle, Dumbbell, Scale, Utensils } from "lucide-react";
import { Button, Card, CardKicker, PageHeader, Ring } from "../../components/ui";
import { useAuth } from "../auth/auth";
import { useMe } from "../auth/useMe";

const MACROS = [
  { label: "Protein", detail: "0 / 112g" },
  { label: "Carbs", detail: "0 / 288g" },
  { label: "Fat", detail: "0 / 56g" },
];

const WEEK_DAYS = ["M", "T", "W", "T", "F", "S", "S"];

const SETUP_ITEMS = [
  "Create your first workout program",
  "Create your workout variants",
  "Log your first meal",
  "Log your weight",
];

export function DashboardScreen() {
  const { session } = useAuth();
  const { data: me } = useMe();
  const username = (me ?? session?.user)?.username;

  return (
    <>
      <PageHeader
        title={username ? `Good morning, ${username}` : "Good morning"}
        subtitle="Friday, August 30"
        actions={
          <>
            <Button variant="secondary">
              <Utensils size={15} aria-hidden />
              Log meal
            </Button>
            <Button variant="secondary">
              <Dumbbell size={15} aria-hidden />
              Start workout
            </Button>
            <Button variant="primary">
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
            <Ring size={112} stroke={10} ariaLabel="Calories: 0 of 2,104 kcal" />
            <div className="flex flex-col gap-0.5">
              <div className="text-[26px] font-bold">
                0 <span className="text-sm font-normal text-foreground-muted">/ 2,104 kcal</span>
              </div>
              <div className="text-[13px] text-foreground-muted">Nothing logged yet today</div>
            </div>
            <div className="ml-auto flex gap-[18px]">
              {MACROS.map((macro) => (
                <div key={macro.label} className="text-center">
                  <Ring size={52} stroke={6} ariaLabel={`${macro.label}: ${macro.detail}`} />
                  <div className="mt-1 text-xs">{macro.label}</div>
                  <div className="text-[11px] text-foreground-muted">{macro.detail}</div>
                </div>
              ))}
            </div>
          </div>
          <Button variant="primary" className="self-start">
            Log your first meal
          </Button>
        </Card>

        <Card>
          <CardKicker>Workout frequency</CardKicker>
          <div className="flex h-16 items-end gap-1.5 py-1.5">
            {WEEK_DAYS.map((day, i) => (
              <div key={i} className="flex flex-1 flex-col items-center gap-1">
                <div className="h-1 w-full rounded-full bg-viz-track" />
                <div className="text-[10px] text-foreground-muted">{day}</div>
              </div>
            ))}
          </div>
          <p className="m-0 mb-2 flex-1 text-[13px] text-foreground-muted">
            No completed workouts this week yet.
          </p>
          <Button variant="secondary" block>
            Start a workout
          </Button>
        </Card>

        <Card>
          <CardKicker>Body weight</CardKicker>
          <p className="m-0 mb-2 flex-1 text-[13px] text-foreground-muted">
            No weight logs yet — log your weight to start a trend line.
          </p>
          <Button variant="secondary" block>
            Log your weight
          </Button>
        </Card>

        <Card className="col-span-2">
          <CardKicker>Get set up</CardKicker>
          <div className="flex flex-col gap-0.5">
            {SETUP_ITEMS.map((item) => (
              <div
                key={item}
                className="flex items-center gap-2.5 border-b border-border px-1 py-2.5"
              >
                <Circle size={16} className="text-primary" aria-hidden />
                <span className="flex-1 text-sm">{item}</span>
                <ChevronRight size={16} className="text-foreground-muted" aria-hidden />
              </div>
            ))}
          </div>
        </Card>
      </div>
    </>
  );
}
