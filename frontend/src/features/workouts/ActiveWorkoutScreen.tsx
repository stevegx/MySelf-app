import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { Button, Card, CardKicker, CardTitle, Field, Input, PageHeader, Tag } from "../../components/ui";
import { ApiError } from "../../lib/api";
import {
  useActiveSession,
  useCompleteSession,
  useDiscardSession,
  useExerciseHistory,
  useLogSet,
  useSessionExercises,
  useSkipSet,
} from "./api";
import type { ExerciseLogDetail, LogSetBody, SetLogDetail, WorkoutSessionDetail } from "./api";
import { ExercisePicker } from "./ExercisePicker";
import { useConfirm } from "./useConfirm";
import { useRestTimer } from "./useRestTimer";

type SessionOps = ReturnType<typeof useSessionExercises>;

function targetLabel(set: SetLogDetail) {
  const reps =
    set.targetRepsMin != null && set.targetRepsMax != null
      ? set.targetRepsMin === set.targetRepsMax
        ? `${set.targetRepsMin} reps`
        : `${set.targetRepsMin}–${set.targetRepsMax} reps`
      : null;
  const weight = set.targetWeightKg != null ? `${set.targetWeightKg} kg` : null;
  return [weight, reps].filter(Boolean).join(" × ") || "no target";
}

type FieldKey = "weightKg" | "addedWeightKg" | "assistanceKg" | "reps" | "durationSeconds";

/** Which performed fields to show for a tracking mode (docs/02 tracking-mode-aware inputs). */
function fieldsFor(mode: string): { key: FieldKey; label: string }[] {
  switch (mode) {
    case "WeightAndReps":
      return [{ key: "weightKg", label: "Weight (kg)" }, { key: "reps", label: "Reps" }];
    case "BodyweightPlusWeight":
      return [{ key: "addedWeightKg", label: "Added (kg)" }, { key: "reps", label: "Reps" }];
    case "AssistanceReps":
      return [{ key: "assistanceKg", label: "Assist (kg)" }, { key: "reps", label: "Reps" }];
    case "Duration":
      return [{ key: "durationSeconds", label: "Seconds" }];
    default: // BodyweightReps, RepsOnly
      return [{ key: "reps", label: "Reps" }];
  }
}

function SetRow({
  sessionId,
  set,
  index,
  mode,
  prevCompleted,
  onActed,
}: {
  sessionId: string;
  set: SetLogDetail;
  index: number;
  mode: string;
  prevCompleted: SetLogDetail | undefined;
  onActed: () => void;
}) {
  const fields = fieldsFor(mode);
  const [values, setValues] = useState<Record<string, string>>(() => ({
    weightKg: set.weightKg?.toString() ?? "",
    addedWeightKg: set.addedWeightKg?.toString() ?? "",
    assistanceKg: set.assistanceKg?.toString() ?? "",
    reps: set.reps?.toString() ?? "",
    durationSeconds: set.durationSeconds?.toString() ?? "",
  }));
  const [error, setError] = useState<string | null>(null);

  const logSet = useLogSet(sessionId);
  const skipSet = useSkipSet(sessionId);
  const busy = logSet.isPending || skipSet.isPending;

  const done = set.completedAt != null;
  const skipped = set.skippedAt != null;

  async function log() {
    setError(null);
    const body: LogSetBody = { setLogId: set.id, reachedFailure: false };
    for (const f of fields) {
      const raw = values[f.key];
      (body as Record<string, unknown>)[f.key] = raw === "" ? null : Number(raw);
    }
    try {
      await logSet.mutateAsync(body);
      onActed();
    } catch (e) {
      setError(e instanceof ApiError ? (Object.values(e.errors ?? {})[0]?.[0] ?? e.detail ?? e.title) : "Could not save.");
    }
  }

  return (
    <div className="flex flex-wrap items-end gap-2.5 border-t border-border pt-2 first:border-t-0 first:pt-0">
      <span className="w-12 shrink-0 text-xs text-foreground-muted">Set {index + 1}</span>
      <span className="text-[13px] text-foreground-muted">Target: {targetLabel(set)}</span>

      {skipped ? (
        <Tag tone="warning">Skipped{set.skippedReason ? ` · ${set.skippedReason}` : ""}</Tag>
      ) : (
        <>
          {fields.map((f) => (
            <Field key={f.key} label={f.label} className="w-[104px]">
              <Input
                type="number"
                inputMode="decimal"
                aria-label={`Set ${index + 1} ${f.label}`}
                value={values[f.key]}
                onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))}
              />
            </Field>
          ))}
          <div className="flex gap-1.5">
            <Button variant={done ? "secondary" : "primary"} size="sm" onClick={log} disabled={busy}>
              {done ? "Update" : "Log set"}
            </Button>
            {!done && prevCompleted && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() =>
                  // Prefill from the previous completed set of this exercise; never auto-completes
                  // (locked decision: "Copy previous set fills both fields but never completes").
                  setValues((v) => ({
                    ...v,
                    weightKg: prevCompleted.weightKg?.toString() ?? v.weightKg,
                    addedWeightKg: prevCompleted.addedWeightKg?.toString() ?? v.addedWeightKg,
                    assistanceKg: prevCompleted.assistanceKg?.toString() ?? v.assistanceKg,
                    reps: prevCompleted.reps?.toString() ?? v.reps,
                    durationSeconds: prevCompleted.durationSeconds?.toString() ?? v.durationSeconds,
                  }))
                }
                disabled={busy}
              >
                Copy previous
              </Button>
            )}
            <Button
              variant="ghost"
              size="sm"
              onClick={() => skipSet.mutate({ setLogId: set.id }, { onSuccess: onActed })}
              disabled={busy}
            >
              Skip
            </Button>
          </div>
          {done && <Tag tone="success">Logged</Tag>}
        </>
      )}
      {error && <p className="w-full text-xs text-danger">{error}</p>}
    </div>
  );
}

function ExerciseCard({
  sessionId,
  exercise,
  ops,
  onSetActed,
}: {
  sessionId: string;
  exercise: ExerciseLogDetail;
  ops: SessionOps;
  onSetActed: (exercise: ExerciseLogDetail, set: SetLogDetail) => void;
}) {
  const doneCount = exercise.sets.filter((s) => s.completedAt || s.skippedAt).length;
  const { data: history } = useExerciseHistory(exercise.exerciseId);
  const prev = history?.sessions.find((s) => s.sessionId !== sessionId && s.topSetWeightKg != null);
  const [replacing, setReplacing] = useState<{ exerciseId: string; name: string } | null>(null);
  const [picking, setPicking] = useState(false);
  const canRemove = exercise.sets.every((s) => s.completedAt == null);
  const busy = ops.replace.isPending || ops.addSet.isPending || ops.remove.isPending;

  return (
    <div className="mb-4">
      <CardKicker>{exercise.trackingMode.replace(/([A-Z])/g, " $1").trim()}</CardKicker>
      <Card>
        <div className="flex items-center justify-between">
          <CardTitle>{exercise.exerciseName}</CardTitle>
          <Tag tone={doneCount === exercise.sets.length ? "success" : "neutral"}>
            {doneCount}/{exercise.sets.length} sets
          </Tag>
        </div>
        {prev && (
          <p className="m-0 text-xs text-foreground-muted">
            Previous: {prev.topSetWeightKg} kg × {prev.topSetReps} ·{" "}
            {new Date(`${prev.performedOn}T00:00:00`).toLocaleDateString()}
          </p>
        )}
        <div className="flex flex-col gap-2">
          {exercise.sets.map((set, i) => (
            <SetRow
              key={set.id}
              sessionId={sessionId}
              set={set}
              index={i}
              mode={exercise.trackingMode}
              prevCompleted={[...exercise.sets.slice(0, i)].reverse().find((s) => s.completedAt != null)}
              onActed={() => onSetActed(exercise, set)}
            />
          ))}
        </div>

        <div className="mt-1 flex flex-wrap gap-1.5">
          <Button variant="ghost" size="sm" onClick={() => ops.addSet.mutate(exercise.id)} disabled={busy}>
            + Add set
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setPicking((p) => !p)} disabled={busy}>
            Replace
          </Button>
          {canRemove && (
            <Button variant="ghost" size="sm" onClick={() => ops.remove.mutate(exercise.id)} disabled={busy}>
              Remove
            </Button>
          )}
        </div>

        {picking && !replacing && (
          <ExercisePicker
            existingIds={new Set()}
            onClose={() => setPicking(false)}
            onPick={(ex) => setReplacing({ exerciseId: ex.id, name: ex.name })}
          />
        )}

        {replacing && (
          <div className="rounded-control border border-border p-3 text-[13px]">
            <p className="m-0 mb-2">
              Replace <strong>{exercise.exerciseName}</strong> with <strong>{replacing.name}</strong>?
            </p>
            <div className="flex flex-wrap gap-2">
              <Button
                variant="primary"
                size="sm"
                onClick={() => {
                  ops.replace.mutate(
                    { exerciseLogId: exercise.id, exerciseId: replacing.exerciseId, scope: "TodayOnly" },
                    { onSettled: () => { setReplacing(null); setPicking(false); } },
                  );
                }}
              >
                This workout only
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => {
                  ops.replace.mutate(
                    { exerciseLogId: exercise.id, exerciseId: replacing.exerciseId, scope: "TodayAndFuture" },
                    { onSettled: () => { setReplacing(null); setPicking(false); } },
                  );
                }}
              >
                Also update the day
              </Button>
              <Button variant="ghost" size="sm" onClick={() => setReplacing(null)}>
                Cancel
              </Button>
            </div>
          </div>
        )}
      </Card>
    </div>
  );
}

export function ActiveWorkoutScreen() {
  const { data: session, isLoading } = useActiveSession();
  const { confirm, dialog } = useConfirm();

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
            Start a workout from a day in{" "}
            <Link to="/workouts/builder" className="text-primary underline">
              your programs
            </Link>
            .
          </p>
        </Card>
      </>
    );
  }

  return <RunningSession session={session} confirm={confirm} dialog={dialog} />;
}

function RunningSession({
  session,
  confirm,
  dialog,
}: {
  session: NonNullable<ReturnType<typeof useActiveSession>["data"]>;
  confirm: ReturnType<typeof useConfirm>["confirm"];
  dialog: ReturnType<typeof useConfirm>["dialog"];
}) {
  const navigate = useNavigate();
  const complete = useCompleteSession(session.id);
  const discard = useDiscardSession(session.id);
  const ops = useSessionExercises(session.id);
  const rest = useRestTimer();
  const [finished, setFinished] = useState<WorkoutSessionDetail | null>(null);
  const [addingExercise, setAddingExercise] = useState(false);

  // Start the rest countdown when a set is logged/skipped: after each set for a standalone
  // exercise, or after the whole round for a superset (docs/02 §7).
  const onSetActed = (exercise: ExerciseLogDetail, set: SetLogDetail) => {
    if (!exercise.supersetGroupSnapshotId) {
      if (exercise.restSeconds) rest.start(exercise.restSeconds);
      return;
    }
    const members = session.exercises.filter(
      (e) => e.supersetGroupSnapshotId === exercise.supersetGroupSnapshotId,
    );
    const roundComplete = members.every((m) => {
      const s = m.sets.find((x) => x.sortOrder === set.sortOrder);
      if (!s || s.id === set.id) return true; // no set this round, or the one just acted on
      return s.completedAt != null || s.skippedAt != null;
    });
    if (roundComplete && exercise.supersetRestAfterRoundSeconds) {
      rest.start(exercise.supersetRestAfterRoundSeconds);
    }
  };

  const finish = () => complete.mutate(undefined, { onSuccess: (data) => setFinished(data) });

  if (finished) {
    return <SessionComplete session={finished} onDone={() => navigate("/workouts/history")} />;
  }

  const totalSets = session.exercises.reduce((n, e) => n + e.sets.length, 0);
  const actedSets = session.exercises.reduce(
    (n, e) => n + e.sets.filter((s) => s.completedAt || s.skippedAt).length,
    0,
  );

  return (
    <>
      {dialog}
      <PageHeader
        title={session.dayName ?? "Ad-hoc workout"}
        subtitle={session.programName ?? "No source program"}
        actions={
          <>
            <Button
              variant="ghost"
              onClick={async () => {
                if (
                  await confirm({
                    title: "Discard this workout?",
                    message: "Nothing from this session will be recorded.",
                    confirmLabel: "Discard",
                  })
                ) {
                  discard.mutate();
                }
              }}
              disabled={complete.isPending || discard.isPending}
            >
              Discard
            </Button>
            <Button
              variant="primary"
              onClick={finish}
              disabled={complete.isPending || discard.isPending}
            >
              {complete.isPending ? "Finishing…" : "Finish workout"}
            </Button>
          </>
        }
      />

      {totalSets > 0 && (
        <p className="mb-3 text-[13px] text-foreground-muted">
          {actedSets} of {totalSets} sets logged or skipped
        </p>
      )}

      {session.exercises.length === 0 && (
        <Card className="mb-4">
          <p className="m-0 text-sm text-foreground-muted">
            No exercises yet — add one below, or finish to put the session on record.
          </p>
        </Card>
      )}

      {session.exercises.map((exercise) => (
        <ExerciseCard
          key={exercise.id}
          sessionId={session.id}
          exercise={exercise}
          ops={ops}
          onSetActed={onSetActed}
        />
      ))}

      {rest.bar}

      {addingExercise ? (
        <Card>
          <ExercisePicker
            existingIds={new Set(session.exercises.map((e) => e.exerciseId))}
            onClose={() => setAddingExercise(false)}
            onPick={(ex) => ops.add.mutate(ex.id, { onSettled: () => setAddingExercise(false) })}
          />
        </Card>
      ) : (
        <Button variant="secondary" onClick={() => setAddingExercise(true)}>
          + Add exercise
        </Button>
      )}
    </>
  );
}

const PR_LABEL: Record<string, string> = {
  HeaviestWeight: "Heaviest weight",
  BestEstimatedOneRepMax: "New estimated 1RM",
  MostRepsAtWeight: "Most reps at a weight",
  BestExerciseVolume: "Highest exercise volume",
};

function SessionComplete({ session, onDone }: { session: WorkoutSessionDetail; onDone: () => void }) {
  const s = session.summary;
  const minutes = s.durationSeconds != null ? Math.round(s.durationSeconds / 60) : null;

  return (
    <>
      <PageHeader title="Workout complete" subtitle={session.dayName ?? "Ad-hoc workout"} />
      <Card className="mb-4 gap-2">
        <CardKicker>Summary</CardKicker>
        <div className="flex flex-wrap gap-2">
          {minutes != null && <Tag tone="neutral">{minutes} min</Tag>}
          <Tag tone="neutral">{s.completedSetCount} sets</Tag>
          {s.skippedSetCount > 0 && <Tag tone="warning">{s.skippedSetCount} skipped</Tag>}
          <Tag tone="neutral">{s.totalReps} reps</Tag>
          {s.totalVolumeKg > 0 && <Tag tone="neutral">{Math.round(s.totalVolumeKg).toLocaleString()} kg volume</Tag>}
        </div>
      </Card>

      {session.newPersonalRecords.length > 0 && (
        <Card className="mb-4 gap-2">
          <CardKicker>Personal records</CardKicker>
          <div className="flex flex-col gap-1.5">
            {session.newPersonalRecords.map((pr, i) => (
              <div key={i} className="flex items-center gap-2 text-[13px]">
                <Tag tone="success">PR</Tag>
                <span>
                  {PR_LABEL[pr.type] ?? pr.type}:{" "}
                  <strong>
                    {pr.type === "MostRepsAtWeight"
                      ? `${pr.value} reps @ ${pr.weightKg} kg`
                      : `${Math.round(pr.value * 10) / 10} kg`}
                  </strong>
                </span>
              </div>
            ))}
          </div>
        </Card>
      )}

      <Button variant="primary" onClick={onDone}>
        View history
      </Button>
    </>
  );
}
