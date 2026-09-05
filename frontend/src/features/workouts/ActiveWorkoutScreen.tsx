import { Link } from "react-router";
import { Card, CardKicker, CardTitle, Field, Input, PageHeader, Tag } from "../../components/ui";
import { useActiveSession } from "./api";

function targetLabel(set: { targetRepsMin: number | null; targetRepsMax: number | null; targetWeightKg: number | null }) {
  const reps =
    set.targetRepsMin != null && set.targetRepsMax != null
      ? set.targetRepsMin === set.targetRepsMax
        ? `${set.targetRepsMin} reps`
        : `${set.targetRepsMin}–${set.targetRepsMax} reps`
      : null;
  const weight = set.targetWeightKg != null ? `${set.targetWeightKg} kg` : null;
  return [weight, reps].filter(Boolean).join(" × ") || "No target set";
}

export function ActiveWorkoutScreen() {
  const { data: session, isLoading } = useActiveSession();

  if (isLoading) {
    return (
      <>
        <PageHeader title="Active workout" />
        <p className="text-sm text-foreground-muted">Loading…</p>
      </>
    );
  }

  if (!session) {
    return (
      <>
        <PageHeader title="Active workout" subtitle="Nothing in progress right now." />
        <Card>
          <p className="text-sm text-foreground-muted">
            Start a workout from a variant in{" "}
            <Link to="/workouts/builder" className="text-primary underline">
              your programs
            </Link>
            .
          </p>
        </Card>
      </>
    );
  }

  return (
    <>
      <PageHeader
        title={session.dayName ?? "Ad-hoc workout"}
        subtitle={session.programName ?? "No source program"}
      />

      {session.exercises.length === 0 ? (
        <Card>
          <p className="text-sm text-foreground-muted">
            No exercises yet in this ad-hoc session. Adding exercises during a workout is coming soon.
          </p>
        </Card>
      ) : (
        session.exercises.map((exercise) => (
          <div key={exercise.id} className="mb-4">
            <CardKicker>{exercise.trackingMode.replace(/([A-Z])/g, " $1").trim()}</CardKicker>
            <Card>
              <div className="flex items-center justify-between">
                <CardTitle>{exercise.exerciseName}</CardTitle>
                <Tag tone="neutral">{exercise.sets.length} sets</Tag>
              </div>
              <div className="flex flex-col gap-2">
                {exercise.sets.map((set, i) => (
                  <div key={set.id} className="flex flex-wrap items-end gap-2.5 border-t border-border pt-2 first:border-t-0 first:pt-0">
                    <span className="w-14 text-xs text-foreground-muted">Set {i + 1}</span>
                    <span className="text-[13px] text-foreground-muted">Target: {targetLabel(set)}</span>
                    <Field label="Weight (kg)" className="ml-auto w-[110px]">
                      <Input disabled placeholder="—" />
                    </Field>
                    <Field label="Reps" className="w-[90px]">
                      <Input disabled placeholder="—" />
                    </Field>
                  </div>
                ))}
              </div>
              <p className="mt-1 text-xs text-foreground-muted">
                Logging sets, the rest timer and finishing the workout are coming in the next update.
              </p>
            </Card>
          </div>
        ))
      )}
    </>
  );
}
