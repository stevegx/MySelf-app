import { useMemo, useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { useNavigate } from "react-router";
import { Button, Card, PageHeader, Tag } from "../../components/ui";
import { cn } from "../../lib/cn";
import { useRescheduleSession, useWorkoutCalendar } from "./api";
import type { WorkoutSessionListItem } from "./api";

function iso(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

/** Monday-based weekday index (0 = Mon … 6 = Sun). */
function mondayIndex(d: Date) {
  return (d.getDay() + 6) % 7;
}

export function WorkoutCalendarScreen() {
  const navigate = useNavigate();
  const [month, setMonth] = useState(() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  });
  const [openDay, setOpenDay] = useState<string | null>(null);
  const reschedule = useRescheduleSession();

  const gridStart = useMemo(() => {
    const d = new Date(month);
    d.setDate(d.getDate() - mondayIndex(d));
    return d;
  }, [month]);

  const cells = useMemo(
    () => Array.from({ length: 42 }, (_, i) => new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + i)),
    [gridStart],
  );

  const { data } = useWorkoutCalendar(iso(cells[0]), iso(cells[41]));
  const byDate = useMemo(() => {
    const m = new Map<string, WorkoutSessionListItem[]>();
    for (const d of data?.days ?? []) m.set(d.date, d.sessions);
    return m;
  }, [data]);

  const monthLabel = month.toLocaleDateString(undefined, { month: "long", year: "numeric" });
  const openSessions = openDay ? (byDate.get(openDay) ?? []) : [];

  return (
    <>
      <PageHeader
        title="Workout calendar"
        subtitle="Completed sessions, by the day they happened."
        actions={
          <Button variant="ghost" onClick={() => navigate("/workouts/history")}>
            List view
          </Button>
        }
      />

      <Card className="mb-4 gap-3">
        <div className="flex items-center justify-between">
          <Button variant="ghost" size="sm" iconOnly aria-label="Previous month"
            onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() - 1, 1))}>
            <ChevronLeft size={16} aria-hidden />
          </Button>
          <span className="text-sm font-bold">{monthLabel}</span>
          <Button variant="ghost" size="sm" iconOnly aria-label="Next month"
            onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() + 1, 1))}>
            <ChevronRight size={16} aria-hidden />
          </Button>
        </div>

        <div className="grid grid-cols-7 gap-1 text-center text-[11px] text-foreground-muted">
          {["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => (
            <div key={d}>{d}</div>
          ))}
        </div>

        <div className="grid grid-cols-7 gap-1">
          {cells.map((d) => {
            const key = iso(d);
            const sessions = byDate.get(key) ?? [];
            const inMonth = d.getMonth() === month.getMonth();
            return (
              <button
                key={key}
                type="button"
                onClick={() => setOpenDay(sessions.length ? key : null)}
                className={cn(
                  "flex min-h-[52px] flex-col items-center rounded-control border p-1 text-[12px]",
                  inMonth ? "border-border" : "border-transparent text-foreground-muted",
                  sessions.length ? "bg-primary-soft" : "",
                  openDay === key ? "ring-2 ring-primary" : "",
                )}
              >
                <span>{d.getDate()}</span>
                {sessions.length > 0 && (
                  <span className="mt-0.5 inline-block size-1.5 rounded-full bg-primary" aria-label={`${sessions.length} session(s)`} />
                )}
              </button>
            );
          })}
        </div>
      </Card>

      {openDay && (
        <Card className="gap-2">
          <span className="text-sm font-bold">
            {new Date(`${openDay}T00:00:00`).toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "long" })}
          </span>
          {openSessions.map((s) => (
            <div key={s.id} className="flex flex-wrap items-center gap-2 border-t border-border pt-2 first:border-t-0 first:pt-0">
              <span className="text-[13px] font-semibold">{s.dayName ?? "Ad-hoc workout"}</span>
              <Tag tone="neutral">{s.summary.completedSetCount} sets</Tag>
              {s.summary.totalVolumeKg > 0 && (
                <Tag tone="neutral">{Math.round(s.summary.totalVolumeKg).toLocaleString()} kg</Tag>
              )}
              <label className="ml-auto text-[12px] text-foreground-muted">
                Move to{" "}
                <input
                  type="date"
                  defaultValue={s.performedOnLocalDate ?? openDay}
                  onChange={(e) => {
                    if (e.target.value) reschedule.mutate({ sessionId: s.id, localDate: e.target.value });
                  }}
                  className="rounded-control border border-border bg-surface-subtle px-2 py-1 text-[12px]"
                />
              </label>
            </div>
          ))}
        </Card>
      )}
    </>
  );
}
