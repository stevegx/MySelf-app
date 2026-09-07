import { Link } from "react-router";
import { Card, PageHeader, Skeleton, Tag } from "../../components/ui";
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
    <Link
      to={`/workouts/session/${s.id}`}
      className="block rounded-card no-underline transition-colors hover:bg-surface-subtle"
    >
      <Card className="gap-1">
        <div className="flex items-center justify-between">
          <span className="font-bold">{s.dayName ?? "Quick workout"}</span>
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
          {s.wasEdited && <Tag tone="neutral">Edited</Tag>}
        </div>
      </Card>
    </Link>
  );
}

export function WorkoutHistoryScreen() {
  const { data, isLoading } = useSessionHistory();

  return (
    <>
      <PageHeader
        title="Workout history"
        subtitle="Sessions you've completed, most recent first."
      />

      {isLoading ? (
        <div className="flex flex-col gap-2">
          {Array.from({ length: 4 }, (_, i) => (
            <div key={i} className="rounded-card border border-border bg-surface px-4 py-3">
              <Skeleton className="mb-2 h-4 w-44" />
              <Skeleton className="h-3 w-64" />
            </div>
          ))}
        </div>
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
