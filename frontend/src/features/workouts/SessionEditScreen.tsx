import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { Button, Card, CardKicker, CardTitle, Field, Input, PageHeader, Skeleton, Tag } from "../../components/ui";
import { ApiError } from "../../lib/api";
import { useLogSet, useSession, useSkipSet } from "./api";
import type { LogSetBody, SetLogDetail } from "./api";

type FieldKey = "weightKg" | "addedWeightKg" | "assistanceKg" | "reps" | "durationSeconds";

/** Which performed fields a tracking mode uses (mirrors the active-workout screen). */
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
    default:
      return [{ key: "reps", label: "Reps" }];
  }
}

function fmtDate(value: string | null) {
  if (!value) return "";
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
  });
}

function errText(e: unknown) {
  if (e instanceof ApiError) return Object.values(e.errors ?? {})[0]?.[0] ?? e.detail ?? e.title;
  return "Couldn't save that change.";
}

function EditableSet({
  sessionId,
  set,
  index,
  mode,
}: {
  sessionId: string;
  set: SetLogDetail;
  index: number;
  mode: string;
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

  const skipped = set.skippedAt != null;
  const done = set.completedAt != null;

  async function save() {
    setError(null);
    const body: LogSetBody = { setLogId: set.id, reachedFailure: set.reachedFailure };
    for (const f of fields) {
      const raw = values[f.key];
      (body as Record<string, unknown>)[f.key] = raw === "" ? null : Number(raw);
    }
    try {
      await logSet.mutateAsync(body);
    } catch (e) {
      setError(errText(e));
    }
  }

  return (
    <div className="flex flex-wrap items-end gap-2.5 border-t border-border pt-2 first:border-t-0 first:pt-0">
      <span className="w-12 shrink-0 text-xs text-foreground-muted">Set {index + 1}</span>

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
        <Button variant="primary" size="sm" onClick={save} disabled={busy}>
          Save
        </Button>
        {!skipped && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setError(null);
              skipSet.mutate({ setLogId: set.id }, { onError: (e) => setError(errText(e)) });
            }}
            disabled={busy}
          >
            Skip
          </Button>
        )}
      </div>

      {done && <Tag tone="success">Logged</Tag>}
      {skipped && <Tag tone="warning">Skipped{set.skippedReason ? ` · ${set.skippedReason}` : ""}</Tag>}
      {error && <p className="w-full text-xs text-danger">{error}</p>}
    </div>
  );
}

export function SessionEditScreen() {
  const { id = null } = useParams();
  const navigate = useNavigate();
  const { data: session, isLoading } = useSession(id);

  if (isLoading) {
    return (
      <>
        <PageHeader title="Edit workout" />
        <div className="flex flex-col gap-4">
          <Skeleton className="h-5 w-48" />
          <Card className="gap-3">
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-9" />
            <Skeleton className="h-9" />
          </Card>
        </div>
      </>
    );
  }

  if (!session) {
    return (
      <>
        <PageHeader title="Edit workout" subtitle="That workout couldn't be found." />
        <Card>
          <Link to="/workouts/history" className="text-primary underline">
            Back to history
          </Link>
        </Card>
      </>
    );
  }

  if (session.status !== "Completed") {
    return (
      <>
        <PageHeader title="Edit workout" subtitle="This one isn't finished yet." />
        <Card>
          <p className="m-0 text-sm text-foreground-muted">
            You can only edit a completed workout here.{" "}
            <Link to="/workouts/active" className="text-primary underline">
              Go to the active workout
            </Link>
            .
          </p>
        </Card>
      </>
    );
  }

  const s = session.summary;

  return (
    <>
      <PageHeader
        title={session.dayName ?? "Quick workout"}
        subtitle={fmtDate(session.performedOnLocalDate)}
        actions={
          <>
            {session.wasEdited && <Tag tone="neutral">Edited</Tag>}
            <Button variant="ghost" onClick={() => navigate("/workouts/history")}>
              Done
            </Button>
          </>
        }
      />

      <p className="mb-3 text-sm text-foreground-muted">
        Fix a number you mistyped, or skip a set you didn't really do. Changes save one at a time and
        recompute this workout's totals and PRs.
      </p>

      <div className="mb-4 flex flex-wrap gap-1.5">
        <Tag tone="neutral">{s.completedSetCount} sets</Tag>
        {s.skippedSetCount > 0 && <Tag tone="warning">{s.skippedSetCount} skipped</Tag>}
        <Tag tone="neutral">{s.totalReps} reps</Tag>
        {s.totalVolumeKg > 0 && <Tag tone="neutral">{Math.round(s.totalVolumeKg).toLocaleString()} kg volume</Tag>}
      </div>

      {session.exercises.map((exercise) => (
        <div key={exercise.id} className="mb-4">
          <CardKicker>{exercise.trackingMode.replace(/([A-Z])/g, " $1").trim()}</CardKicker>
          <Card>
            <CardTitle>{exercise.exerciseName}</CardTitle>
            <div className="flex flex-col gap-2">
              {exercise.sets.map((set, i) => (
                <EditableSet
                  key={set.id}
                  sessionId={session.id}
                  set={set}
                  index={i}
                  mode={exercise.trackingMode}
                />
              ))}
            </div>
          </Card>
        </div>
      ))}
    </>
  );
}
