import { useRef, useState } from "react";
import { Link, useNavigate } from "react-router";
import { ChevronDown } from "lucide-react";
import { Button, Card, CardKicker, PageHeader, Skeleton, StepperInput, Tag } from "../../components/ui";
import { cn } from "../../lib/cn";
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
import type { ExerciseHistoryEntry, ExerciseLogDetail, LogSetBody, SetLogDetail, WorkoutSessionDetail } from "./api";
import { ExercisePicker } from "./ExercisePicker";
import { SyncStatus } from "./SyncStatus";
import { useConfirm } from "./useConfirm";
import { useRestTimer } from "./useRestTimer";

type SessionOps = ReturnType<typeof useSessionExercises>;
type FieldKey = "weightKg" | "addedWeightKg" | "assistanceKg" | "reps" | "durationSeconds";

/** Which performed fields (and their stepper size) a tracking mode uses. */
function fieldsFor(mode: string): { key: FieldKey; label: string; step: number }[] {
  switch (mode) {
    case "WeightAndReps":
      return [{ key: "weightKg", label: "kg", step: 2.5 }, { key: "reps", label: "reps", step: 1 }];
    case "BodyweightPlusWeight":
      return [{ key: "addedWeightKg", label: "+kg", step: 2.5 }, { key: "reps", label: "reps", step: 1 }];
    case "AssistanceReps":
      return [{ key: "assistanceKg", label: "assist kg", step: 2.5 }, { key: "reps", label: "reps", step: 1 }];
    case "Duration":
      return [{ key: "durationSeconds", label: "seconds", step: 5 }];
    default: // BodyweightReps, RepsOnly
      return [{ key: "reps", label: "reps", step: 1 }];
  }
}

function targetLabel(set: SetLogDetail) {
  const reps =
    set.targetRepsMin != null && set.targetRepsMax != null
      ? set.targetRepsMin === set.targetRepsMax
        ? `${set.targetRepsMin}`
        : `${set.targetRepsMin}–${set.targetRepsMax}`
      : null;
  const weight = set.targetWeightKg != null ? `${set.targetWeightKg} kg` : null;
  return [weight, reps && `${reps} reps`].filter(Boolean).join(" × ") || "no target";
}

/** Best starting value for a field: what's already performed → the previous set this
 *  session → the day's target → last session's top set → blank. */
function seedValue(
  key: FieldKey,
  set: SetLogDetail,
  prev: SetLogDetail | undefined,
  last: ExerciseHistoryEntry | undefined,
): string {
  const own = set[key];
  if (own != null) return String(own);
  const p = prev?.[key];
  if (p != null) return String(p);
  if (key === "weightKg") {
    if (set.targetWeightKg != null) return String(set.targetWeightKg);
    if (last?.topSetWeightKg != null) return String(last.topSetWeightKg);
  }
  if (key === "reps") {
    if (set.targetRepsMax != null) return String(set.targetRepsMax);
    if (set.targetRepsMin != null) return String(set.targetRepsMin);
    if (last?.topSetReps != null) return String(last.topSetReps);
  }
  return "";
}

function SetRow({
  sessionId,
  set,
  index,
  fields,
  prev,
  last,
  firstInputRef,
  onLogged,
  onActed,
}: {
  sessionId: string;
  set: SetLogDetail;
  index: number;
  fields: ReturnType<typeof fieldsFor>;
  prev: SetLogDetail | undefined;
  last: ExerciseHistoryEntry | undefined;
  firstInputRef?: React.Ref<HTMLInputElement>;
  onLogged: () => void;
  onActed: () => void;
}) {
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(fields.map((f) => [f.key, seedValue(f.key, set, prev, last)])),
  );
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
      onLogged();
    } catch (e) {
      setError(e instanceof ApiError ? (Object.values(e.errors ?? {})[0]?.[0] ?? e.detail ?? e.title) : "Could not save.");
    }
  }

  return (
    <div
      className={cn(
        "flex flex-wrap items-end gap-2 rounded-control border p-2",
        done ? "border-success/40 bg-success-soft/40" : skipped ? "border-warning/40 bg-warning-soft/40" : "border-border",
      )}
    >
      <span className="w-10 shrink-0 self-center text-xs font-semibold text-foreground-muted">#{index + 1}</span>

      {skipped ? (
        <span className="self-center text-[13px] text-warning">Skipped{set.skippedReason ? ` · ${set.skippedReason}` : ""}</span>
      ) : (
        <>
          {fields.map((f, i) => (
            <StepperInput
              key={f.key}
              label={f.label}
              ariaLabel={`Set ${index + 1} ${f.label}`}
              step={f.step}
              value={values[f.key]}
              onChange={(next) => setValues((v) => ({ ...v, [f.key]: next }))}
              inputRef={i === 0 ? firstInputRef : undefined}
            />
          ))}
          <Button variant={done ? "secondary" : "primary"} onClick={log} disabled={busy} className="self-end">
            {done ? "Update" : "Log"}
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => skipSet.mutate({ setLogId: set.id }, { onSuccess: () => { onActed(); onLogged(); } })}
            disabled={busy}
            className="self-end"
          >
            Skip
          </Button>
        </>
      )}
      {error && <p className="w-full text-xs text-danger">{error}</p>}
    </div>
  );
}

function CollapsedExercise({
  exercise,
  onOpen,
}: {
  exercise: ExerciseLogDetail;
  onOpen: () => void;
}) {
  const done = exercise.sets.filter((s) => s.completedAt || s.skippedAt).length;
  const total = exercise.sets.length;
  const allDone = total > 0 && done === total;
  return (
    <button
      type="button"
      onClick={onOpen}
      className="mb-2 flex w-full items-center gap-3 rounded-card border border-border bg-surface px-4 py-3 text-left hover:border-border-strong"
    >
      <span className={cn("flex-1 text-sm font-semibold", allDone && "text-foreground-muted line-through")}>
        {exercise.exerciseName}
      </span>
      <Tag tone={allDone ? "success" : "neutral"}>
        {done}/{total}
      </Tag>
      <ChevronDown size={16} className="text-foreground-muted" aria-hidden />
    </button>
  );
}

function ExercisePanel({
  sessionId,
  exercise,
  ops,
  onSetActed,
  onAllDone,
}: {
  sessionId: string;
  exercise: ExerciseLogDetail;
  ops: SessionOps;
  onSetActed: (exercise: ExerciseLogDetail, set: SetLogDetail) => void;
  onAllDone: () => void;
}) {
  const { data: history } = useExerciseHistory(exercise.exerciseId);
  const last = history?.sessions.find((s) => s.sessionId !== sessionId && s.completedSets > 0);
  const fields = fieldsFor(exercise.trackingMode);

  const [replacing, setReplacing] = useState<{ exerciseId: string; name: string } | null>(null);
  const [picking, setPicking] = useState(false);
  const canRemove = exercise.sets.every((s) => s.completedAt == null);
  const busy = ops.replace.isPending || ops.addSet.isPending || ops.remove.isPending;

  const inputRefs = useRef<Record<string, HTMLInputElement | null>>({});
  const doneCount = exercise.sets.filter((s) => s.completedAt || s.skippedAt).length;

  const advanceAfter = (loggedIndex: number) => {
    const next = exercise.sets.find(
      (s, i) => i > loggedIndex && s.completedAt == null && s.skippedAt == null,
    );
    if (next) {
      const el = inputRefs.current[next.id];
      el?.focus();
      el?.scrollIntoView({ block: "center", behavior: "smooth" });
    } else if (exercise.sets.every((s) => s.completedAt != null || s.skippedAt != null)) {
      onAllDone();
    }
  };

  return (
    <div className="mb-4">
      <CardKicker>{exercise.trackingMode.replace(/([A-Z])/g, " $1").trim()}</CardKicker>
      <Card>
        <div className="flex items-center justify-between">
          <span className="text-[17px] font-bold leading-tight">{exercise.exerciseName}</span>
          <Tag tone={doneCount === exercise.sets.length ? "success" : "neutral"}>
            {doneCount}/{exercise.sets.length}
          </Tag>
        </div>
        <p className="m-0 text-xs text-foreground-muted">
          {last
            ? `Last time: ${last.topSetWeightKg != null ? `${last.topSetWeightKg} kg × ` : ""}${last.topSetReps ?? "?"} · ${new Date(`${last.performedOn}T00:00:00`).toLocaleDateString()}`
            : "First time doing this — no history yet."}
        </p>

        <div className="flex flex-col gap-2">
          {exercise.sets.map((set, i) => (
            <div key={set.id} className="flex flex-col gap-0.5">
              <span className="pl-11 text-[11px] text-foreground-muted">Target {targetLabel(set)}</span>
              <SetRow
                sessionId={sessionId}
                set={set}
                index={i}
                fields={fields}
                prev={[...exercise.sets.slice(0, i)].reverse().find((s) => s.completedAt != null)}
                last={last}
                firstInputRef={(el) => {
                  inputRefs.current[set.id] = el;
                }}
                onLogged={() => advanceAfter(i)}
                onActed={() => onSetActed(exercise, set)}
              />
            </div>
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
                onClick={() =>
                  ops.replace.mutate(
                    { exerciseLogId: exercise.id, exerciseId: replacing.exerciseId, scope: "TodayOnly" },
                    { onSettled: () => { setReplacing(null); setPicking(false); } },
                  )
                }
              >
                This workout only
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={() =>
                  ops.replace.mutate(
                    { exerciseLogId: exercise.id, exerciseId: replacing.exerciseId, scope: "TodayAndFuture" },
                    { onSettled: () => { setReplacing(null); setPicking(false); } },
                  )
                }
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
        <div className="flex flex-col gap-4">
          {[0, 1].map((i) => (
            <Card key={i} className="gap-3">
              <Skeleton className="h-5 w-40" />
              <Skeleton className="h-9" />
              <Skeleton className="h-9" />
            </Card>
          ))}
        </div>
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

function firstUnfinished(exercises: ExerciseLogDetail[]): string | null {
  const e = exercises.find((x) => x.sets.some((s) => s.completedAt == null && s.skippedAt == null));
  return (e ?? exercises[0])?.id ?? null;
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

  // Which exercise is expanded. Seeded from the data; the user can override by tapping a
  // collapsed row. Re-seeds only while the user hasn't picked one this render-life.
  const [activeId, setActiveId] = useState<string | null>(() => firstUnfinished(session.exercises));
  const active = session.exercises.some((e) => e.id === activeId) ? activeId : firstUnfinished(session.exercises);

  const onSetActed = (exercise: ExerciseLogDetail, set: SetLogDetail) => {
    if (!exercise.supersetGroupSnapshotId) {
      if (exercise.restSeconds) rest.start(exercise.restSeconds);
      return;
    }
    const members = session.exercises.filter((e) => e.supersetGroupSnapshotId === exercise.supersetGroupSnapshotId);
    const roundComplete = members.every((m) => {
      const s = m.sets.find((x) => x.sortOrder === set.sortOrder);
      if (!s || s.id === set.id) return true;
      return s.completedAt != null || s.skippedAt != null;
    });
    if (roundComplete && exercise.supersetRestAfterRoundSeconds) rest.start(exercise.supersetRestAfterRoundSeconds);
  };

  const goToNextExercise = (fromId: string) => {
    const idx = session.exercises.findIndex((e) => e.id === fromId);
    const next = session.exercises
      .slice(idx + 1)
      .find((e) => e.sets.some((s) => s.completedAt == null && s.skippedAt == null));
    if (next) setActiveId(next.id);
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
            <SyncStatus />
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
              disabled={complete.isPending || discard.isPending || actedSets === 0}
              title={actedSets === 0 ? "Log or skip at least one set first" : undefined}
            >
              {complete.isPending ? "Finishing…" : "Finish workout"}
            </Button>
          </>
        }
      />

      {totalSets > 0 && (
        <div className="mb-3">
          <div className="h-1.5 overflow-hidden rounded-full bg-viz-track">
            <div className="h-full rounded-full bg-primary transition-[width]" style={{ width: `${(actedSets / totalSets) * 100}%` }} />
          </div>
          <p className="mt-1 text-[12px] text-foreground-muted">
            {actedSets} of {totalSets} sets{actedSets === 0 && " — log or skip one to finish"}
          </p>
        </div>
      )}

      {session.exercises.length === 0 && (
        <Card className="mb-4">
          <p className="m-0 text-sm text-foreground-muted">
            No exercises yet — add one below. An empty workout can't be finished; discard it if
            you're not training now.
          </p>
        </Card>
      )}

      {session.exercises.map((exercise) =>
        exercise.id === active ? (
          <ExercisePanel
            key={exercise.id}
            sessionId={session.id}
            exercise={exercise}
            ops={ops}
            onSetActed={onSetActed}
            onAllDone={() => goToNextExercise(exercise.id)}
          />
        ) : (
          <CollapsedExercise key={exercise.id} exercise={exercise} onOpen={() => setActiveId(exercise.id)} />
        ),
      )}

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
