import { useExerciseHistory } from "./api";

/**
 * A tiny inline e1RM sparkline for one exercise, oldest → newest. Renders nothing until
 * there are at least two completed sessions to draw a line between.
 */
export function ExerciseTrend({ exerciseId, className }: { exerciseId: string; className?: string }) {
  const { data } = useExerciseHistory(exerciseId);

  const values = (data?.sessions ?? [])
    .slice()
    .reverse()
    .map((s) => s.estimatedOneRepMax)
    .filter((v): v is number => v != null);

  if (values.length < 2) return null;

  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  const pts = values
    .map((v, i) => `${(i / (values.length - 1)) * 72},${24 - ((v - min) / span) * 22 - 1}`)
    .join(" ");
  const rising = values[values.length - 1] >= values[0];

  return (
    <svg
      viewBox="0 0 72 24"
      preserveAspectRatio="none"
      className={className ?? "h-6 w-14 shrink-0"}
      aria-hidden
      style={{ color: rising ? "var(--color-viz-positive)" : "var(--color-foreground-subtle)" }}
    >
      <polyline
        points={pts}
        fill="none"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}
