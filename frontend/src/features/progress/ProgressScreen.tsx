import { useState } from "react";
import { Card, CardKicker, Input, PageHeader, Segmented, Tag } from "../../components/ui";
import { useExerciseHistory, useExerciseSearch } from "../workouts/api";
import type { ExerciseHistoryEntry, PersonalRecordDetail } from "../workouts/api";

type ProgressTab = "strength" | "weight" | "measurements";

const PR_LABEL: Record<PersonalRecordDetail["type"], string> = {
  HeaviestWeight: "Heaviest weight",
  BestEstimatedOneRepMax: "Best estimated 1RM",
  MostRepsAtWeight: "Most reps at a weight",
  BestExerciseVolume: "Best session volume",
};

function prValue(pr: PersonalRecordDetail) {
  switch (pr.type) {
    case "HeaviestWeight":
    case "BestEstimatedOneRepMax":
      return `${Math.round(pr.value * 10) / 10} kg`;
    case "MostRepsAtWeight":
      return `${pr.value} reps @ ${pr.weightKg} kg`;
    case "BestExerciseVolume":
      return `${Math.round(pr.value).toLocaleString()} kg`;
  }
}

/** A tiny inline sparkline — no chart library. */
function Sparkline({ values }: { values: number[] }) {
  if (values.length < 2) return null;
  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  const pts = values
    .map((v, i) => `${(i / (values.length - 1)) * 100},${30 - ((v - min) / span) * 28 - 1}`)
    .join(" ");
  return (
    <svg viewBox="0 0 100 30" preserveAspectRatio="none" className="h-10 w-full text-primary" aria-hidden>
      <polyline points={pts} fill="none" stroke="currentColor" strokeWidth="1.5" vectorEffect="non-scaling-stroke" />
    </svg>
  );
}

function SessionRow({ e }: { e: ExerciseHistoryEntry }) {
  return (
    <div className="flex items-center justify-between border-t border-border py-2 text-[13px] first:border-t-0">
      <span className="text-foreground-muted">{new Date(`${e.performedOn}T00:00:00`).toLocaleDateString()}</span>
      <span>
        {e.topSetWeightKg != null ? `${e.topSetWeightKg} kg × ${e.topSetReps}` : `${e.completedSets} sets`}
      </span>
      <span className="text-foreground-muted">
        {e.estimatedOneRepMax != null ? `e1RM ${Math.round(e.estimatedOneRepMax)}` : "—"}
      </span>
      <span className="text-foreground-muted">{Math.round(e.volume).toLocaleString()} kg</span>
    </div>
  );
}

function StrengthTab() {
  const [q, setQ] = useState("");
  const [exerciseId, setExerciseId] = useState<string | null>(null);
  const search = useExerciseSearch(q);
  const { data: history, isLoading } = useExerciseHistory(exerciseId);

  if (!exerciseId) {
    return (
      <Card className="gap-2">
        <CardKicker>Pick an exercise</CardKicker>
        <Input placeholder="Search the catalogue…" value={q} onChange={(e) => setQ(e.target.value)} autoFocus />
        <ul className="m-0 flex max-h-72 list-none flex-col gap-1 overflow-y-auto p-0">
          {search.data?.items.map((ex) => (
            <li key={ex.id}>
              <button
                type="button"
                onClick={() => setExerciseId(ex.id)}
                className="w-full rounded-control border border-border bg-surface px-3 py-2 text-left text-[13px] hover:border-border-strong"
              >
                {ex.name}
              </button>
            </li>
          ))}
        </ul>
      </Card>
    );
  }

  if (isLoading || !history) {
    return <p className="text-sm text-foreground-muted">Loading…</p>;
  }

  const e1rmSeries = history.sessions
    .map((s) => s.estimatedOneRepMax)
    .filter((v): v is number => v != null)
    .reverse();

  return (
    <div className="flex flex-col gap-4">
      <button className="self-start text-[13px] text-primary underline" onClick={() => setExerciseId(null)}>
        ← Pick a different exercise
      </button>

      <Card className="gap-2">
        <CardKicker>{history.exerciseName} — personal records</CardKicker>
        {history.personalRecords.length === 0 ? (
          <p className="m-0 text-[13px] text-foreground-muted">
            No records yet — complete a weight-and-reps workout with this exercise.
          </p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {history.personalRecords.map((pr, i) => (
              <Tag key={i} tone="primary">
                {PR_LABEL[pr.type]}: {prValue(pr)}
              </Tag>
            ))}
          </div>
        )}
      </Card>

      {e1rmSeries.length >= 2 && (
        <Card className="gap-1">
          <CardKicker>Estimated 1RM trend</CardKicker>
          <Sparkline values={e1rmSeries} />
        </Card>
      )}

      <Card className="gap-1">
        <CardKicker>Recent sessions</CardKicker>
        {history.sessions.length === 0 ? (
          <p className="m-0 text-[13px] text-foreground-muted">No completed sessions with this exercise yet.</p>
        ) : (
          history.sessions.map((s) => <SessionRow key={s.sessionId} e={s} />)
        )}
      </Card>
    </div>
  );
}

export function ProgressScreen() {
  const [tab, setTab] = useState<ProgressTab>("strength");

  return (
    <>
      <PageHeader
        title="Progress"
        actions={
          <Segmented<ProgressTab>
            aria-label="Progress view"
            value={tab}
            onChange={setTab}
            options={[
              { value: "strength", label: "Strength" },
              { value: "weight", label: "Body weight" },
              { value: "measurements", label: "Measurements" },
            ]}
          />
        }
      />

      {tab === "strength" ? (
        <StrengthTab />
      ) : (
        <Card>
          <CardKicker>{tab === "weight" ? "Body weight trend" : "Measurements"}</CardKicker>
          <p className="m-0 text-[13px] text-foreground-muted">Coming in a later phase.</p>
        </Card>
      )}
    </>
  );
}
