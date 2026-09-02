import { useState } from "react";
import { ArrowDown, ArrowUp, Trash2 } from "lucide-react";
import { Button, Input } from "../../components/ui";
import { ApiError } from "../../lib/api";
import { ExercisePicker } from "./ExercisePicker";
import { useUpdateVariant, useVariant } from "./api";
import type { ExerciseListItem, UpdateVariantBody } from "./api";

/**
 * Edits one variant's exercises. The UI model is "N sets of X–Y reps at Z kg" per exercise;
 * on save it expands to that many Standard set prescriptions. Per-set differences (drop
 * sets, AMRAP on the last set) and superset grouping are backend-supported but not yet in
 * this screen.
 */
type Row = {
  exerciseId: string;
  exerciseName: string;
  setCount: number;
  repsMin: string;
  repsMax: string;
  weightKg: string;
};

export function VariantEditor({
  variantId,
  programId,
  onClose,
}: {
  variantId: string;
  programId: string;
  onClose: () => void;
}) {
  const { data: variant, isLoading } = useVariant(variantId);
  const update = useUpdateVariant(programId);

  const [rows, setRows] = useState<Row[]>([]);
  const [loadedFrom, setLoadedFrom] = useState<string | null>(null);
  const [picking, setPicking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Seed the editable rows the first time this variant's data arrives (and again if the
  // component is reused for a different variant). Setting state during render, guarded by a
  // value that changes, is the pattern React recommends over a syncing effect.
  if (variant && loadedFrom !== variant.id) {
    setLoadedFrom(variant.id);
    setRows(
      variant.exercises.map((e) => ({
        exerciseId: e.exerciseId,
        exerciseName: e.exerciseName,
        setCount: Math.max(1, e.sets.length),
        repsMin: e.sets[0]?.targetRepsMin?.toString() ?? "",
        repsMax: e.sets[0]?.targetRepsMax?.toString() ?? "",
        weightKg: e.sets[0]?.targetWeightKg?.toString() ?? "",
      })),
    );
  }

  if (isLoading || !variant) {
    return <p className="text-sm text-foreground-muted">Loading variant…</p>;
  }

  const update1 = (i: number, patch: Partial<Row>) =>
    setRows((r) => r.map((row, idx) => (idx === i ? { ...row, ...patch } : row)));

  const move = (i: number, dir: -1 | 1) =>
    setRows((r) => {
      const j = i + dir;
      if (j < 0 || j >= r.length) return r;
      const copy = [...r];
      [copy[i], copy[j]] = [copy[j], copy[i]];
      return copy;
    });

  const addExercise = (ex: ExerciseListItem) => {
    setRows((r) => [
      ...r,
      { exerciseId: ex.id, exerciseName: ex.name, setCount: 3, repsMin: "8", repsMax: "12", weightKg: "" },
    ]);
    setPicking(false);
  };

  async function save() {
    setError(null);
    const body: UpdateVariantBody = {
      name: variant!.name,
      exercises: rows.map((row, index) => ({
        exerciseId: row.exerciseId,
        sortOrder: index,
        supersetRef: null,
        supersetMemberOrder: 0,
        restSeconds: null,
        notes: null,
        sets: Array.from({ length: Math.max(1, Math.min(20, row.setCount)) }, (_, s) => ({
          sortOrder: s,
          kind: "Standard" as const,
          isAmrap: false,
          targetToFailure: false,
          targetRepsMin: row.repsMin === "" ? null : Number(row.repsMin),
          targetRepsMax: row.repsMax === "" ? null : Number(row.repsMax),
          targetWeightKg: row.weightKg === "" ? null : Number(row.weightKg),
          targetRir: null,
        })),
      })),
      supersets: [],
    };

    try {
      await update.mutateAsync({ variantId, body });
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? (Object.values(e.errors ?? {})[0]?.[0] ?? e.detail ?? e.title) : "Save failed.");
    }
  }

  const existingIds = new Set(rows.map((r) => r.exerciseId));

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h3 className="m-0 text-base font-bold">{variant.name}</h3>
        <div className="flex gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" onClick={save} disabled={update.isPending}>
            {update.isPending ? "Saving…" : "Save variant"}
          </Button>
        </div>
      </div>

      {error && (
        <div role="alert" className="rounded-control border border-danger/40 bg-danger-soft px-3 py-2 text-sm text-danger">
          {error}
        </div>
      )}

      {rows.map((row, i) => (
        <div key={`${row.exerciseId}-${i}`} className="rounded-control border border-border p-3">
          <div className="mb-2 flex items-center justify-between gap-2">
            <span className="text-sm font-bold">{row.exerciseName}</span>
            <div className="flex gap-1">
              <Button variant="secondary" size="sm" iconOnly aria-label="Move up" onClick={() => move(i, -1)}>
                <ArrowUp size={14} aria-hidden />
              </Button>
              <Button variant="secondary" size="sm" iconOnly aria-label="Move down" onClick={() => move(i, 1)}>
                <ArrowDown size={14} aria-hidden />
              </Button>
              <Button
                variant="danger"
                size="sm"
                iconOnly
                aria-label="Remove exercise"
                onClick={() => setRows((r) => r.filter((_, idx) => idx !== i))}
              >
                <Trash2 size={14} aria-hidden />
              </Button>
            </div>
          </div>
          <div className="grid grid-cols-4 gap-2">
            <label className="text-xs text-foreground-muted">
              Sets
              <Input
                type="number"
                inputMode="numeric"
                value={row.setCount}
                onChange={(e) => update1(i, { setCount: Math.max(1, Number(e.target.value) || 1) })}
              />
            </label>
            <label className="text-xs text-foreground-muted">
              Reps min
              <Input type="number" value={row.repsMin} onChange={(e) => update1(i, { repsMin: e.target.value })} />
            </label>
            <label className="text-xs text-foreground-muted">
              Reps max
              <Input type="number" value={row.repsMax} onChange={(e) => update1(i, { repsMax: e.target.value })} />
            </label>
            <label className="text-xs text-foreground-muted">
              Weight (kg)
              <Input type="number" value={row.weightKg} onChange={(e) => update1(i, { weightKg: e.target.value })} />
            </label>
          </div>
        </div>
      ))}

      {picking ? (
        <ExercisePicker onPick={addExercise} onClose={() => setPicking(false)} existingIds={existingIds} />
      ) : (
        <Button variant="secondary" onClick={() => setPicking(true)}>
          + Add exercise
        </Button>
      )}
    </div>
  );
}
