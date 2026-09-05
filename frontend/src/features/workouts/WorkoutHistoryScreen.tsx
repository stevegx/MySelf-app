import { ChevronLeft } from "lucide-react";
import { Link, useNavigate } from "react-router";
import { Button, Card, PageHeader, Tag } from "../../components/ui";
import { useSessionHistory } from "./api";
import type { WorkoutSessionListItem } from "./api";

function formatDate(iso: string | null) {
  if (!iso) return "—";
  return new Date(`${iso}T00:00:00`).toLocaleDateString(undefined, {
    weekday: "short",
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

function formatDuration(seconds: number | null) {
  if (seconds == null) return null;
  const m = Math.round(seconds / 60);
  return m < 60 ? `${m} min` : `${Math.floor(m / 60)}h ${m % 60}m`;
}

function Row({ s }: { s: WorkoutSessionListItem }) {
  const duration = formatDuration(s.summary.durationSeconds);
  return (
    <Card className="gap-1">
      <div className="flex items-center justify-between">
        <span className="font-bold">{s.dayName ?? "Ad-hoc workout"}</span>
        <span className="text-xs text-foreground-muted">{formatDate(s.performedOnLocalDate)}</span>
      </div>
      {s.programName && <span className="text-xs text-foreground-muted">{s.programName}</span>}
      <div className="mt-1 flex flex-wrap gap-1.5">
        <Tag tone="neutral">{s.summary.completedSetCount} sets</Tag>
        {s.summary.skippedSetCount > 0 && <Tag tone="warning">{s.summary.skippedSetCount} skipped</Tag>}
        {s.summary.totalVolumeKg > 0 && (
          <Tag tone="neutral">{Math.round(s.summary.totalVolumeKg).toLocaleString()} kg volume</Tag>
        )}
        {duration && <Tag tone="neutral">{duration}</Tag>}
      </div>
    </Card>
  );
}

export function WorkoutHistoryScreen() {
  const { data, isLoading } = useSessionHistory();
  const navigate = useNavigate();

  return (
    <>
      <PageHeader
        title="Workout history"
        subtitle="Sessions you've completed, most recent first."
        actions={
          <>
            <Button variant="ghost" onClick={() => navigate("/workouts/calendar")}>
              Calendar
            </Button>
            <Button variant="ghost" onClick={() => navigate("/workouts/builder")}>
              <ChevronLeft size={15} aria-hidden />
              Programs
            </Button>
          </>
        }
      />

      {isLoading ? (
        <p className="text-sm text-foreground-muted">Loading…</p>
      ) : data && data.items.length > 0 ? (
        <div className="flex flex-col gap-2">
          {data.items.map((s) => (
            <Row key={s.id} s={s} />
          ))}
        </div>
      ) : (
        <Card>
          <p className="text-sm text-foreground-muted">
            No completed workouts yet. Start one from{" "}
            <Link to="/workouts/builder" className="text-primary underline">
              your programs
            </Link>
            .
          </p>
        </Card>
      )}
    </>
  );
}
