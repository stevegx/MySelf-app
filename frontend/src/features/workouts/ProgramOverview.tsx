import { useMemo, useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button, Card, CardKicker, Skeleton, Tag } from "../../components/ui";
import { cn } from "../../lib/cn";
import { useProgramStats, useWorkoutCalendar } from "./api";

function iso(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

/** Monday-based weekday index (0 = Mon … 6 = Sun). */
function mondayIndex(d: Date) {
  return (d.getDay() + 6) % 7;
}

function fmtDate(value: string | null) {
  if (!value) return "—";
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });
}

const PR_LABEL: Record<string, string> = {
  HeaviestWeight: "Heaviest weight",
  BestEstimatedOneRepMax: "Best est. 1RM",
  MostRepsAtWeight: "Most reps at weight",
  BestExerciseVolume: "Best exercise volume",
};

function StatCard({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <Card className="gap-1">
      <CardKicker>{label}</CardKicker>
      <div className="text-[22px] font-bold leading-tight">{value}</div>
      {hint && <div className="text-[12px] text-foreground-muted">{hint}</div>}
    </Card>
  );
}

function ProgramMiniCalendar({ programId }: { programId: string }) {
  const [month, setMonth] = useState(() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  });

  const gridStart = useMemo(() => {
    const d = new Date(month);
    d.setDate(d.getDate() - mondayIndex(d));
    return d;
  }, [month]);

  const cells = useMemo(
    () => Array.from({ length: 42 }, (_, i) => new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + i)),
    [gridStart],
  );

  const { data } = useWorkoutCalendar(iso(cells[0]), iso(cells[41]), programId);
  const counts = useMemo(() => {
    const m = new Map<string, number>();
    for (const d of data?.days ?? []) m.set(d.date, d.sessions.length);
    return m;
  }, [data]);

  const monthLabel = month.toLocaleDateString(undefined, { month: "long", year: "numeric" });

  return (
    <Card className="gap-3">
      <div className="flex items-center justify-between">
        <CardKicker>When you trained this program</CardKicker>
        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            aria-label="Previous month"
            onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() - 1, 1))}
          >
            <ChevronLeft size={15} aria-hidden />
          </Button>
          <span className="min-w-[120px] text-center text-[13px] font-semibold">{monthLabel}</span>
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            aria-label="Next month"
            onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() + 1, 1))}
          >
            <ChevronRight size={15} aria-hidden />
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-7 gap-1 text-center text-[10px] text-foreground-muted">
        {["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => (
          <div key={d}>{d}</div>
        ))}
      </div>

      <div className="grid grid-cols-7 gap-1">
        {cells.map((d) => {
          const key = iso(d);
          const n = counts.get(key) ?? 0;
          const inMonth = d.getMonth() === month.getMonth();
          return (
            <div
              key={key}
              className={cn(
                "flex min-h-[40px] flex-col items-center rounded-control border p-1 text-[11px]",
                inMonth ? "border-border" : "border-transparent text-foreground-muted",
                n > 0 ? "bg-primary-soft font-semibold" : "",
              )}
              aria-label={n > 0 ? `${key}: ${n} session(s)` : undefined}
            >
              <span>{d.getDate()}</span>
              {n > 0 && <span className="mt-0.5 inline-block size-1.5 rounded-full bg-primary" aria-hidden />}
            </div>
          );
        })}
      </div>
    </Card>
  );
}

function OverviewSkeleton() {
  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-3">
        {Array.from({ length: 6 }, (_, i) => (
          <Card key={i} className="gap-2">
            <Skeleton className="h-2 w-16" />
            <Skeleton className="h-6 w-20" />
            <Skeleton className="h-2 w-24" />
          </Card>
        ))}
      </div>
      <Card className="gap-2">
        <Skeleton className="h-2 w-12" />
        <Skeleton className="h-4" />
        <Skeleton className="h-4" />
      </Card>
      <Card>
        <Skeleton className="h-40" />
      </Card>
    </div>
  );
}

export function ProgramOverview({ programId }: { programId: string }) {
  const { data: stats, isLoading, isError, refetch, isFetching } = useProgramStats(programId);

  if (isLoading) {
    return <OverviewSkeleton />;
  }

  if (isError || !stats || typeof stats.totalSessions !== "number") {
    return (
      <Card className="gap-2">
        <p className="m-0 text-sm text-foreground-muted">Couldn't load this program's stats.</p>
        <Button variant="secondary" size="sm" className="self-start" onClick={() => refetch()} disabled={isFetching}>
          {isFetching ? "Retrying…" : "Try again"}
        </Button>
      </Card>
    );
  }

  if (stats.totalSessions === 0) {
    return (
      <Card>
        <p className="m-0 text-sm text-foreground-muted">
          No sessions from this program yet. Start one from the <strong>Days</strong> tab and its stats and calendar
          will build up here.
        </p>
      </Card>
    );
  }

  const avgMin = stats.avgDurationSeconds != null ? Math.round(stats.avgDurationSeconds / 60) : null;

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-3">
        <StatCard
          label="Times performed"
          value={String(stats.totalSessions)}
          hint={`Last ${fmtDate(stats.lastPerformedOn)}`}
        />
        <StatCard
          label="This week"
          value={String(stats.sessionsThisWeek)}
          hint={`${stats.weeklyAverage}/wk average`}
        />
        <StatCard label="This month" value={String(stats.sessionsThisMonth)} hint={`Since ${fmtDate(stats.firstPerformedOn)}`} />
        <StatCard
          label="Total volume"
          value={`${Math.round(stats.totalVolumeKg).toLocaleString()} kg`}
          hint="Completed working sets"
        />
        {avgMin != null && (
          <StatCard label="Avg session" value={`${avgMin} min`} hint={`Over ${stats.totalSessions} sessions`} />
        )}
        <StatCard
          label="Sets logged"
          value={String(stats.completedSets)}
          hint={stats.skippedSets > 0 ? `${stats.skippedSets} skipped (${Math.round(stats.skippedSetRate * 100)}%)` : "None skipped"}
        />
      </div>

      <Card className="gap-2">
        <CardKicker>By day</CardKicker>
        {stats.perDay.length === 0 ? (
          <p className="m-0 text-[13px] text-foreground-muted">This program has no days.</p>
        ) : (
          <div className="flex flex-col gap-1">
            {stats.perDay.map((d) => (
              <div key={d.dayId} className="flex items-center justify-between border-b border-border py-1.5 text-[13px] last:border-b-0">
                <span className="font-semibold">{d.dayName}</span>
                <span className="flex items-center gap-3 text-foreground-muted">
                  <span>
                    {d.sessions} {d.sessions === 1 ? "session" : "sessions"}
                  </span>
                  <span>last {fmtDate(d.lastPerformedOn)}</span>
                </span>
              </div>
            ))}
          </div>
        )}
      </Card>

      <ProgramMiniCalendar programId={programId} />

      {stats.personalRecords.length > 0 && (
        <Card className="gap-2">
          <CardKicker>Personal records set here</CardKicker>
          <div className="flex flex-col gap-1.5">
            {stats.personalRecords.map((pr, i) => (
              <div key={i} className="flex flex-wrap items-center gap-2 text-[13px]">
                <Tag tone="success">PR</Tag>
                <span className="font-semibold">{pr.exerciseName}</span>
                <span className="text-foreground-muted">
                  {PR_LABEL[pr.type] ?? pr.type}:{" "}
                  <strong>
                    {pr.type === "MostRepsAtWeight" ? `${pr.value} reps` : `${Math.round(pr.value * 10) / 10} kg`}
                  </strong>
                </span>
                <span className="ml-auto text-xs text-foreground-muted">{fmtDate(pr.achievedOn)}</span>
              </div>
            ))}
          </div>
        </Card>
      )}
    </div>
  );
}
