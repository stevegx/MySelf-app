import { useState } from "react";
import { Trash2 } from "lucide-react";
import { Button, Card, CardKicker, Input, PageHeader, Segmented, Skeleton, Tag } from "../../components/ui";
import { useExerciseHistory, useExerciseSearch } from "../workouts/api";
import type { ExerciseHistoryEntry, PersonalRecordDetail } from "../workouts/api";
import { useBodyMeasurements, useDeleteWeight, useNutritionAnalytics, useWeightTrend } from "./api";
import type { NutritionAnalytics, WeightTrendPoint } from "./api";
import { LogWeightDialog } from "./LogWeightDialog";

type ProgressTab = "strength" | "weight" | "nutrition" | "measurements";

const PR_LABEL: Record<PersonalRecordDetail["type"], string> = {
  HeaviestWeight: "Heaviest weight",
  BestEstimatedOneRepMax: "Best estimated 1RM",
  MostRepsAtWeight: "Most reps at a weight",
  BestExerciseVolume: "Best session volume",
};

function prValue(pr: PersonalRecordDetail) {
  switch (pr.type) {
    case "HeaviestWeight":
    case "BestEstimatedOneRepMax":
      return `${Math.round(pr.value * 10) / 10} kg`;
    case "MostRepsAtWeight":
      return `${pr.value} reps @ ${pr.weightKg} kg`;
    case "BestExerciseVolume":
      return `${Math.round(pr.value).toLocaleString()} kg`;
  }
}

/** A tiny inline sparkline — no chart library. */
function Sparkline({ values }: { values: number[] }) {
  if (values.length < 2) return null;
  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  const pts = values
    .map((v, i) => `${(i / (values.length - 1)) * 100},${30 - ((v - min) / span) * 28 - 1}`)
    .join(" ");
  return (
    <svg viewBox="0 0 100 30" preserveAspectRatio="none" className="h-10 w-full text-primary" aria-hidden>
      <polyline points={pts} fill="none" stroke="currentColor" strokeWidth="1.5" vectorEffect="non-scaling-stroke" />
    </svg>
  );
}

function SessionRow({ e }: { e: ExerciseHistoryEntry }) {
  return (
    <div className="flex items-center justify-between border-t border-border py-2 text-sm first:border-t-0">
      <span className="text-foreground-muted">{new Date(`${e.performedOn}T00:00:00`).toLocaleDateString()}</span>
      <span>
        {e.topSetWeightKg != null ? `${e.topSetWeightKg} kg × ${e.topSetReps}` : `${e.completedSets} sets`}
      </span>
      <span className="text-foreground-muted">
        {e.estimatedOneRepMax != null ? `e1RM ${Math.round(e.estimatedOneRepMax)}` : "—"}
      </span>
      <span className="text-foreground-muted">{Math.round(e.volume).toLocaleString()} kg</span>
    </div>
  );
}

function StrengthTab() {
  const [q, setQ] = useState("");
  const [exerciseId, setExerciseId] = useState<string | null>(null);
  const search = useExerciseSearch(q);
  const { data: history, isLoading } = useExerciseHistory(exerciseId);

  if (!exerciseId) {
    return (
      <Card className="gap-2">
        <CardKicker>Pick an exercise</CardKicker>
        <Input placeholder="Search the catalogue…" value={q} onChange={(e) => setQ(e.target.value)} autoFocus />
        <ul className="m-0 flex max-h-72 list-none flex-col gap-1 overflow-y-auto p-0">
          {search.data?.items.map((ex) => (
            <li key={ex.id}>
              <button
                type="button"
                onClick={() => setExerciseId(ex.id)}
                className="w-full rounded-control border border-border bg-surface px-3 py-2 text-left text-sm hover:border-border-strong"
              >
                {ex.name}
              </button>
            </li>
          ))}
        </ul>
      </Card>
    );
  }

  if (isLoading || !history) {
    return (
      <Card className="gap-3">
        <Skeleton className="h-4 w-32" />
        <Skeleton className="h-24" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-2/3" />
      </Card>
    );
  }

  const e1rmSeries = history.sessions
    .map((s) => s.estimatedOneRepMax)
    .filter((v): v is number => v != null)
    .reverse();

  return (
    <div className="flex flex-col gap-4">
      <button className="self-start text-sm text-primary underline" onClick={() => setExerciseId(null)}>
        ← Pick a different exercise
      </button>

      <Card className="gap-2">
        <CardKicker>{history.exerciseName} — personal records</CardKicker>
        {history.personalRecords.length === 0 ? (
          <p className="m-0 text-sm text-foreground-muted">
            No records yet — complete a weight-and-reps workout with this exercise.
          </p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {history.personalRecords.map((pr, i) => (
              <Tag key={i} tone="primary">
                {PR_LABEL[pr.type]}: {prValue(pr)}
              </Tag>
            ))}
          </div>
        )}
      </Card>

      {e1rmSeries.length >= 2 && (
        <Card className="gap-1">
          <CardKicker>Estimated 1RM trend</CardKicker>
          <Sparkline values={e1rmSeries} />
        </Card>
      )}

      <Card className="gap-1">
        <CardKicker>Recent sessions</CardKicker>
        {history.sessions.length === 0 ? (
          <p className="m-0 text-sm text-foreground-muted">No completed sessions with this exercise yet.</p>
        ) : (
          history.sessions.map((s) => <SessionRow key={s.sessionId} e={s} />)
        )}
      </Card>
    </div>
  );
}

/** Two lines over one date axis: the faint daily average and the bold 7-day rolling average. */
function WeightTrendChart({ points }: { points: WeightTrendPoint[] }) {
  if (points.length < 2) return null;

  const values = points.flatMap((p) => [p.average, p.rollingAverage]);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  const x = (i: number) => (i / (points.length - 1)) * 100;
  const y = (v: number) => 32 - ((v - min) / span) * 30 - 1;
  const line = (get: (p: WeightTrendPoint) => number) =>
    points.map((p, i) => `${x(i)},${y(get(p))}`).join(" ");

  return (
    <svg viewBox="0 0 100 32" preserveAspectRatio="none" className="h-28 w-full" aria-hidden>
      <polyline
        points={line((p) => p.average)}
        fill="none"
        stroke="var(--color-foreground-subtle)"
        strokeWidth="1"
        vectorEffect="non-scaling-stroke"
      />
      <polyline
        points={line((p) => p.rollingAverage)}
        fill="none"
        stroke="var(--color-primary)"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}

function WeightTab() {
  const trend = useWeightTrend();
  const list = useBodyMeasurements();
  const del = useDeleteWeight();
  const [logging, setLogging] = useState(false);

  const points = trend.data?.points ?? [];
  const latest = trend.data?.latest ?? null;
  const change = trend.data?.sevenDayChangeKg ?? null;

  return (
    <div className="flex flex-col gap-4">
      {logging && <LogWeightDialog onClose={() => setLogging(false)} />}

      <div className="flex items-center justify-between">
        <h3 className="m-0 text-base font-bold">Body weight</h3>
        <Button variant="primary" size="sm" onClick={() => setLogging(true)}>
          Log weight
        </Button>
      </div>

      {trend.isLoading ? (
        <Card className="gap-3">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-28" />
        </Card>
      ) : points.length === 0 ? (
        <Card>
          <p className="m-0 text-sm text-foreground-muted">
            No weight logged yet. Log a reading to start a trend line — the chart uses each day's
            average and a 7-day rolling average.
          </p>
        </Card>
      ) : (
        <>
          <Card className="gap-2">
            <div className="flex items-baseline gap-3">
              <span className="text-[26px] font-bold">{latest?.toFixed(1)} kg</span>
              {change != null && (
                <span
                  className={
                    change < 0 ? "text-sm text-success" : change > 0 ? "text-sm text-warning" : "text-sm text-foreground-muted"
                  }
                >
                  {change > 0 ? "▲" : change < 0 ? "▼" : "→"} {Math.abs(change).toFixed(1)} kg / 7 days
                </span>
              )}
            </div>
            <WeightTrendChart points={points} />
            <div className="flex gap-3 text-[12px] text-foreground-muted">
              <span className="flex items-center gap-1">
                <span className="inline-block h-0.5 w-4 bg-foreground-subtle" /> daily average
              </span>
              <span className="flex items-center gap-1">
                <span className="inline-block h-[3px] w-4 rounded-full bg-primary" /> 7-day rolling
              </span>
            </div>
          </Card>

          <Card className="gap-1">
            <CardKicker>Recent readings</CardKicker>
            {(list.data ?? []).map((m) => (
              <div
                key={m.id}
                className="flex items-center justify-between border-t border-border py-2 text-sm first:border-t-0"
              >
                <span className="text-foreground-muted">
                  {new Date(`${m.localDate}T00:00:00`).toLocaleDateString()}
                </span>
                <span className="font-semibold">{m.weightKg.toFixed(1)} kg</span>
                <Button
                  variant="ghost"
                  size="sm"
                  iconOnly
                  aria-label={`Delete reading from ${m.localDate}`}
                  disabled={del.isPending}
                  onClick={() => del.mutate(m.id)}
                >
                  <Trash2 size={13} aria-hidden />
                </Button>
              </div>
            ))}
          </Card>
        </>
      )}
    </div>
  );
}

const rnd = (n: number) => Math.round(n);

/** One bar per logged day (calories), with a dashed line at the target. No chart library. */
function KcalBars({ days, target }: { days: NutritionAnalytics["days"]; target: number | null }) {
  const max = Math.max(target ?? 0, ...days.map((d) => d.kcal), 1);
  return (
    <div className="flex h-32 items-end gap-1">
      {days.map((d) => {
        const over = target != null && d.kcal > target * 1.05;
        return (
          <div
            key={d.date}
            className="min-w-0 flex-1"
            title={`${d.date}: ${rnd(d.kcal)} kcal`}
            style={{ height: "100%" }}
          >
            <div className="flex h-full items-end">
              <div
                className={over ? "w-full rounded-t bg-warning" : "w-full rounded-t bg-primary"}
                style={{ height: `${(d.kcal / max) * 100}%` }}
              />
            </div>
          </div>
        );
      })}
    </div>
  );
}

function NutritionTab() {
  const { data, isLoading } = useNutritionAnalytics();

  if (isLoading) {
    return (
      <Card className="gap-3">
        <Skeleton className="h-4 w-24" />
        <Skeleton className="h-32" />
      </Card>
    );
  }
  if (!data || data.daysLogged === 0) {
    return (
      <Card>
        <p className="m-0 text-sm text-foreground-muted">
          No meals logged in the last two weeks. Log food on the Nutrition screen and your
          calorie/macro adherence shows up here.
        </p>
      </Card>
    );
  }

  const t = data.targets;
  const macro = (label: string, avg: number, target: number | null) => (
    <div key={label} className="text-center">
      <div className="text-lg font-bold">{rnd(avg)} g</div>
      <div className="text-xs text-foreground-muted">
        {label}
        {target != null ? ` · target ${target}` : ""}
      </div>
    </div>
  );

  return (
    <div className="flex flex-col gap-4">
      <Card className="gap-2">
        <CardKicker>Calories · last {data.days.length} logged days</CardKicker>
        <KcalBars days={data.days} target={t.kcal} />
        <div className="flex items-baseline justify-between text-sm">
          <span className="text-foreground-muted">
            Avg <span className="font-bold text-foreground">{rnd(data.average.kcal)}</span> kcal / day
          </span>
          {t.kcal != null && (
            <span className="text-foreground-muted">
              target {t.kcal} ·{" "}
              <span className={data.average.kcal > t.kcal ? "text-warning" : "text-success"}>
                {data.average.kcal > t.kcal ? "+" : ""}
                {rnd(data.average.kcal - t.kcal)}
              </span>
            </span>
          )}
        </div>
      </Card>

      <Card className="gap-3">
        <CardKicker>Average macros / day</CardKicker>
        <div className="flex justify-around">
          {macro("Protein", data.average.proteinG, t.proteinG)}
          {macro("Carbs", data.average.carbG, t.carbG)}
          {macro("Fat", data.average.fatG, t.fatG)}
        </div>
      </Card>
    </div>
  );
}

export function ProgressScreen() {
  const [tab, setTab] = useState<ProgressTab>("strength");

  return (
    <>
      <PageHeader
        title="Progress"
        actions={
          <Segmented<ProgressTab>
            aria-label="Progress view"
            value={tab}
            onChange={setTab}
            options={[
              { value: "strength", label: "Strength" },
              { value: "weight", label: "Body weight" },
              { value: "nutrition", label: "Nutrition" },
              { value: "measurements", label: "Measurements" },
            ]}
          />
        }
      />

      {tab === "strength" ? (
        <StrengthTab />
      ) : tab === "weight" ? (
        <WeightTab />
      ) : tab === "nutrition" ? (
        <NutritionTab />
      ) : (
        <Card>
          <CardKicker>Measurements</CardKicker>
          <p className="m-0 text-sm text-foreground-muted">Coming in a later phase.</p>
        </Card>
      )}
    </>
  );
}
