import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { Button, Card, CardKicker, CardTitle, Field, Input, PageHeader, Tag } from "../../components/ui";
import { ApiError } from "../../lib/api";
import {
  useActiveSession,
  useCompleteSession,
  useDiscardSession,
  useLogSet,
  useSkipSet,
} from "./api";
import type { ExerciseLogDetail, LogSetBody, SetLogDetail } from "./api";
import { useConfirm } from "./useConfirm";

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
}: {
  sessionId: string;
  set: SetLogDetail;
  index: number;
  mode: string;
  prevCompleted: SetLogDetail | undefined;
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
              onClick={() => skipSet.mutate({ setLogId: set.id })}
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

function ExerciseCard({ sessionId, exercise }: { sessionId: string; exercise: ExerciseLogDetail }) {
  const doneCount = exercise.sets.filter((s) => s.completedAt || s.skippedAt).length;
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
        <div className="flex flex-col gap-2">
          {exercise.sets.map((set, i) => (
            <SetRow
              key={set.id}
              sessionId={sessionId}
              set={set}
              index={i}
              mode={exercise.trackingMode}
              prevCompleted={[...exercise.sets.slice(0, i)].reverse().find((s) => s.completedAt != null)}
            />
          ))}
        </div>
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

  const finish = () =>
    complete.mutate(undefined, { onSuccess: () => navigate("/workouts/history") });

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

      {session.exercises.length === 0 ? (
        <Card>
          <p className="text-sm text-foreground-muted">
            No exercises in this ad-hoc session. Adding exercises mid-workout is a later slice — you can
            still finish it to put it on record.
          </p>
        </Card>
      ) : (
        session.exercises.map((exercise) => (
          <ExerciseCard key={exercise.id} sessionId={session.id} exercise={exercise} />
        ))
      )}
    </>
  );
}
