import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { ArrowDown, ArrowUp, Copy, Plus, Trash2 } from "lucide-react";
import { Button, Checkbox, Input, Segmented } from "../../components/ui";
import { cn } from "../../lib/cn";
import { ApiError } from "../../lib/api";
import { useMe } from "../auth/useMe";
import { ExercisePicker } from "./ExercisePicker";
import { SortableList } from "./SortableList";
import { useBulkExercises, useMuscles, useProgram, useUpdateDay, useUpdatePreferences, useDay } from "./api";
import type { DayDetail, ExerciseListItem, UpdateDayBody } from "./api";

/**
 * Edits one day in full: every exercise, every prescribed set (kind, rep range, weight,
 * AMRAP, to-failure, RIR), optional superset grouping with a rest-after-round, plus bulk
 * copy/move of exercises to another day. Round-trips per-set detail losslessly — loading a
 * day with drop sets / an AMRAP last set / per-set weights and saving no longer flattens it.
 */

type EditSet = {
  key: string;
  kind: "Standard" | "Drop";
  isAmrap: boolean;
  toFailure: boolean;
  repsMin: string;
  repsMax: string;
  weightKg: string;
  rir: string;
};

type EditExercise = {
  key: string;
  serverId: string | null; // the persisted DayExercise id; null for a freshly added row
  exerciseId: string;
  exerciseName: string;
  restSeconds: string;
  notes: string;
  supersetKey: string | null;
  moreOpen: boolean;
  sets: EditSet[];
};

type EditSuperset = { key: string; restAfterRoundSeconds: string };

type EditState = { exercises: EditExercise[]; supersets: EditSuperset[]; focusMuscleIds: number[] };

let seq = 0;
const uid = (prefix: string) => `${prefix}-${(seq += 1)}`;

const numOrNull = (s: string) => (s.trim() === "" ? null : Number(s));

function seed(day: DayDetail): EditState {
  const supersetKeyByServerId = new Map<string, string>();
  const supersets: EditSuperset[] = day.supersets
    .slice()
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((s) => {
      const key = uid("ss");
      supersetKeyByServerId.set(s.id, key);
      return { key, restAfterRoundSeconds: String(s.restAfterRoundSeconds) };
    });

  const exercises: EditExercise[] = day.exercises
    .slice()
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((e) => ({
      key: uid("ex"),
      serverId: e.id,
      exerciseId: e.exerciseId,
      exerciseName: e.exerciseName,
      restSeconds: e.restSeconds == null ? "" : String(e.restSeconds),
      notes: e.notes ?? "",
      supersetKey: e.supersetGroupId ? (supersetKeyByServerId.get(e.supersetGroupId) ?? null) : null,
      moreOpen: false,
      sets: e.sets
        .slice()
        .sort((a, b) => a.sortOrder - b.sortOrder)
        .map((s) => ({
          key: uid("set"),
          kind: s.kind,
          isAmrap: s.isAmrap,
          toFailure: s.targetToFailure,
          repsMin: s.targetRepsMin == null ? "" : String(s.targetRepsMin),
          repsMax: s.targetRepsMax == null ? "" : String(s.targetRepsMax),
          weightKg: s.targetWeightKg == null ? "" : String(s.targetWeightKg),
          rir: s.targetRir == null ? "" : String(s.targetRir),
        })),
    }));

  return { exercises, supersets, focusMuscleIds: [...day.focusMuscleIds] };
}

const blankSet = (): EditSet => ({
  key: uid("set"),
  kind: "Standard",
  isAmrap: false,
  toFailure: false,
  repsMin: "8",
  repsMax: "12",
  weightKg: "",
  rir: "",
});

// A comparable snapshot for dirty-tracking (drops the volatile React keys).
const fingerprint = (s: EditState) =>
  JSON.stringify({
    focus: [...s.focusMuscleIds].sort((a, b) => a - b),
    exercises: s.exercises.map((e) => ({
      exerciseId: e.exerciseId,
      restSeconds: e.restSeconds,
      notes: e.notes,
      supersetKey: e.supersetKey,
      sets: e.sets.map((st) => [st.kind, st.isAmrap, st.toFailure, st.repsMin, st.repsMax, st.weightKg, st.rir]),
    })),
    supersets: s.supersets.map((g) => ({
      used: s.exercises.filter((e) => e.supersetKey === g.key).length,
      rest: g.restAfterRoundSeconds,
    })),
  });

export function DayEditor({
  dayId,
  programId,
  onClose,
}: {
  dayId: string;
  programId: string;
  onClose: () => void;
}) {
  const { data: day, isLoading } = useDay(dayId);
  const { data: program } = useProgram(programId);
  const { data: muscles } = useMuscles();
  const { data: me } = useMe();
  const update = useUpdateDay(programId);
  const bulk = useBulkExercises(programId);
  const prefs = useUpdatePreferences();

  const [state, setState] = useState<EditState>({ exercises: [], supersets: [], focusMuscleIds: [] });
  const [offFocusNote, setOffFocusNote] = useState<{ name: string; muscles: string[] } | null>(null);
  const [baseline, setBaseline] = useState<string>("");
  const [loadedFrom, setLoadedFrom] = useState<string | null>(null);
  const [picking, setPicking] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [confirmingDiscard, setConfirmingDiscard] = useState(false);

  // Seed editable state the first time this day's data arrives (React's recommended
  // "set state during render, guarded by a changing value" pattern).
  if (day && loadedFrom !== day.id) {
    const seeded = seed(day);
    setLoadedFrom(day.id);
    setState(seeded);
    setBaseline(fingerprint(seeded));
    setSelected(new Set());
  }

  const dirty = baseline !== "" && fingerprint(state) !== baseline;

  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = "";
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  const otherDays = useMemo(() => {
    if (!program) return [];
    return program.days.filter((d) => d.id !== dayId).map((d) => ({ id: d.id, label: d.name }));
  }, [program, dayId]);

  const muscleName = useMemo(() => {
    const m = new Map((muscles ?? []).map((x) => [x.id, x.name]));
    return (id: number) => m.get(id) ?? "";
  }, [muscles]);

  const focusNames = state.focusMuscleIds.map(muscleName).filter(Boolean);
  const warnOffFocus = me?.profile?.warnOffFocusExercises ?? true;

  if (isLoading || !day) {
    return <p className="text-sm text-foreground-muted">Loading day…</p>;
  }

  const patchExercise = (key: string, patch: Partial<EditExercise>) =>
    setState((s) => ({ ...s, exercises: s.exercises.map((e) => (e.key === key ? { ...e, ...patch } : e)) }));

  const patchSet = (exKey: string, setKey: string, patch: Partial<EditSet>) =>
    setState((s) => ({
      ...s,
      exercises: s.exercises.map((e) =>
        e.key === exKey ? { ...e, sets: e.sets.map((st) => (st.key === setKey ? { ...st, ...patch } : st)) } : e,
      ),
    }));

  const moveExercise = (key: string, dir: -1 | 1) =>
    setState((s) => {
      const i = s.exercises.findIndex((e) => e.key === key);
      const j = i + dir;
      if (i < 0 || j < 0 || j >= s.exercises.length) return s;
      const copy = [...s.exercises];
      [copy[i], copy[j]] = [copy[j], copy[i]];
      return { ...s, exercises: copy };
    });

  const addExercise = (ex: ExerciseListItem) => {
    setState((s) => ({
      ...s,
      exercises: [
        ...s.exercises,
        {
          key: uid("ex"),
          serverId: null,
          exerciseId: ex.id,
          exerciseName: ex.name,
          restSeconds: "",
          notes: "",
          supersetKey: null,
          moreOpen: false,
          sets: [blankSet()],
        },
      ],
    }));
    setPicking(false);

    // Off-focus nudge (docs: never blocks — just informs). Only when this day has a focus,
    // the exercise's primary muscles fall entirely outside it, and the user hasn't opted out.
    if (warnOffFocus && focusNames.length > 0 && ex.primaryMuscles.length > 0) {
      const outside = ex.primaryMuscles.filter((m) => !focusNames.includes(m));
      if (outside.length === ex.primaryMuscles.length) {
        setOffFocusNote({ name: ex.name, muscles: outside });
      }
    }
  };

  const toggleFocus = (id: number) =>
    setState((s) => ({
      ...s,
      focusMuscleIds: s.focusMuscleIds.includes(id)
        ? s.focusMuscleIds.filter((x) => x !== id)
        : [...s.focusMuscleIds, id],
    }));

  const removeExercise = (key: string) =>
    setState((s) => ({ ...s, exercises: s.exercises.filter((e) => e.key !== key) }));

  const newSuperset = (exKey: string) =>
    setState((s) => {
      const key = uid("ss");
      return {
        ...s,
        supersets: [...s.supersets, { key, restAfterRoundSeconds: "60" }],
        exercises: s.exercises.map((e) => (e.key === exKey ? { ...e, supersetKey: key } : e)),
      };
    });

  const setGroupRest = (groupKey: string, value: string) =>
    setState((s) => ({
      ...s,
      supersets: s.supersets.map((g) => (g.key === groupKey ? { ...g, restAfterRoundSeconds: value } : g)),
    }));

  const toggleSelected = (serverId: string) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(serverId)) next.delete(serverId);
      else next.add(serverId);
      return next;
    });

  function buildBody(): UpdateDayBody {
    const memberOrder = new Map<string, number>();
    return {
      name: day!.name,
      rowVersion: day!.programRowVersion,
      focusMuscleIds: state.focusMuscleIds,
      exercises: state.exercises.map((e, index) => {
        let supersetMemberOrder = 0;
        if (e.supersetKey) {
          const n = memberOrder.get(e.supersetKey) ?? 0;
          supersetMemberOrder = n;
          memberOrder.set(e.supersetKey, n + 1);
        }
        return {
          exerciseId: e.exerciseId,
          sortOrder: index,
          supersetRef: e.supersetKey,
          supersetMemberOrder,
          restSeconds: numOrNull(e.restSeconds),
          notes: e.notes.trim() === "" ? null : e.notes.trim(),
          sets: e.sets.map((st, s) => ({
            sortOrder: s,
            kind: st.kind,
            isAmrap: st.isAmrap,
            targetToFailure: st.toFailure,
            targetRepsMin: numOrNull(st.repsMin),
            targetRepsMax: numOrNull(st.repsMax),
            targetWeightKg: numOrNull(st.weightKg),
            targetRir: numOrNull(st.rir),
          })),
        };
      }),
      // Only groups with 2+ members are valid supersets (backend rejects the rest).
      supersets: state.supersets
        .filter((g) => state.exercises.filter((e) => e.supersetKey === g.key).length >= 2)
        .map((g, index) => ({
          ref: g.key,
          sortOrder: index,
          restAfterRoundSeconds: Number(g.restAfterRoundSeconds) || 0,
        })),
    };
  }

  async function save() {
    setError(null);
    try {
      await update.mutateAsync({ dayId, body: buildBody() });
      onClose();
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        setError(
          "This program changed in another tab since you opened this day. Reload the page to get the latest, then reapply your changes.",
        );
        return;
      }
      setError(e instanceof ApiError ? (Object.values(e.errors ?? {})[0]?.[0] ?? e.detail ?? e.title) : "Save failed.");
    }
  }

  async function runBulk(kind: "copy" | "move", destDayId: string) {
    setError(null);
    const ids = [...selected];
    try {
      await (kind === "copy" ? bulk.copy : bulk.move).mutateAsync({
        destDayId,
        sourceDayId: dayId,
        dayExerciseIds: ids,
        rowVersion: day!.programRowVersion,
      });
      setSelected(new Set());
      if (kind === "move") {
        // Rows left this day — reseed from the server on the next render.
        setLoadedFrom(null);
      }
    } catch (e) {
      setError(e instanceof ApiError ? (e.detail ?? e.title) : `Bulk ${kind} failed.`);
    }
  }

  const requestClose = () => {
    if (dirty) setConfirmingDiscard(true);
    else onClose();
  };

  const groupLabel = (key: string) => `Superset ${state.supersets.findIndex((g) => g.key === key) + 1}`;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <h3 className="m-0 text-base font-bold">
          {day.name}
          {dirty ? <span className="ml-2 text-xs font-normal text-warning">Unsaved changes</span> : null}
        </h3>
        <div className="flex gap-2">
          <Button variant="ghost" onClick={requestClose}>
            {dirty ? "Close" : "Cancel"}
          </Button>
          <Button variant="primary" onClick={save} disabled={update.isPending}>
            {update.isPending ? "Saving…" : "Save day"}
          </Button>
        </div>
      </div>

      {confirmingDiscard && (
        <div className="flex items-center justify-between gap-3 rounded-control border border-warning/40 bg-warning-soft px-3 py-2 text-sm">
          <span>Discard your unsaved changes to this day?</span>
          <span className="flex gap-2">
            <Button variant="ghost" size="sm" onClick={() => setConfirmingDiscard(false)}>
              Keep editing
            </Button>
            <Button variant="danger" size="sm" onClick={onClose}>
              Discard
            </Button>
          </span>
        </div>
      )}

      {error && (
        <div role="alert" className="rounded-control border border-danger/40 bg-danger-soft px-3 py-2 text-sm text-danger">
          {error}
        </div>
      )}

      <div className="rounded-control border border-border p-3">
        <div className="mb-2 text-xs font-semibold text-foreground-muted">
          Focus{" "}
          <span className="font-normal">
            — the muscles this day trains. Filters the exercise picker; you can still add anything.
          </span>
        </div>
        <div className="flex flex-wrap gap-1.5">
          {(muscles ?? []).map((m) => {
            const on = state.focusMuscleIds.includes(m.id);
            return (
              <button
                key={m.id}
                type="button"
                aria-pressed={on}
                onClick={() => toggleFocus(m.id)}
                className={cn(
                  "rounded-full border px-2.5 py-1 text-[12px]",
                  on
                    ? "border-primary bg-primary-soft font-semibold text-primary-pressed"
                    : "border-border text-foreground-muted hover:border-border-strong",
                )}
              >
                {m.name}
              </button>
            );
          })}
        </div>
      </div>

      {offFocusNote && (
        <div className="flex flex-wrap items-center gap-2 rounded-control border border-info/40 bg-info-soft px-3 py-2 text-[13px]">
          <span>
            Added <strong>{offFocusNote.name}</strong> — {offFocusNote.muscles.join(", ")}, outside this day's focus.
          </span>
          <span className="ml-auto flex gap-2">
            <Button variant="ghost" size="sm" onClick={() => setOffFocusNote(null)}>
              Dismiss
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                prefs.mutate({ warnOffFocusExercises: false });
                setOffFocusNote(null);
              }}
            >
              Don&apos;t warn me again
            </Button>
          </span>
        </div>
      )}

      {selected.size > 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-control border border-border bg-surface-subtle px-3 py-2 text-sm">
          <span className="font-semibold">{selected.size} selected</span>
          {dirty ? (
            <span className="text-xs text-foreground-muted">Save your changes before copying or moving.</span>
          ) : otherDays.length === 0 ? (
            <span className="text-xs text-foreground-muted">No other day to copy or move to.</span>
          ) : (
            <>
              <BulkTargetMenu label="Copy to…" icon={<Copy size={13} aria-hidden />} targets={otherDays} onPick={(id) => runBulk("copy", id)} />
              <BulkTargetMenu label="Move to…" icon={<ArrowDown size={13} aria-hidden />} targets={otherDays} onPick={(id) => runBulk("move", id)} />
            </>
          )}
          <Button variant="ghost" size="sm" onClick={() => setSelected(new Set())}>
            Clear
          </Button>
        </div>
      )}

      <SortableList
        items={state.exercises}
        getId={(e) => e.key}
        onReorder={(order) =>
          setState((s) => ({ ...s, exercises: order.map((k) => s.exercises.find((e) => e.key === k)!) }))
        }
      >
        {(e, dragHandle) => {
        const grouped = e.supersetKey != null;
        return (
          <div
            className={`rounded-control border p-3 ${grouped ? "border-primary/50 bg-primary-soft/30" : "border-border"}`}
          >
            <div className="mb-2 flex items-center justify-between gap-2">
              <span className="flex items-center gap-2 text-sm font-bold">
                {dragHandle}
                {e.serverId && (
                  <Checkbox
                    label=""
                    aria-label={`Select ${e.exerciseName}`}
                    checked={selected.has(e.serverId)}
                    onChange={() => toggleSelected(e.serverId!)}
                  />
                )}
                {e.exerciseName}
                {grouped ? <span className="text-xs font-normal text-primary-pressed">{groupLabel(e.supersetKey!)}</span> : null}
              </span>
              <div className="flex gap-1">
                <Button variant="secondary" size="sm" iconOnly aria-label="Move up" onClick={() => moveExercise(e.key, -1)}>
                  <ArrowUp size={14} aria-hidden />
                </Button>
                <Button variant="secondary" size="sm" iconOnly aria-label="Move down" onClick={() => moveExercise(e.key, 1)}>
                  <ArrowDown size={14} aria-hidden />
                </Button>
                <Button variant="danger" size="sm" iconOnly aria-label="Remove exercise" onClick={() => removeExercise(e.key)}>
                  <Trash2 size={14} aria-hidden />
                </Button>
              </div>
            </div>

            <div className="flex flex-col gap-2">
              <div className="grid grid-cols-[auto_1fr_1fr_1fr_1fr_auto] items-center gap-2 text-[11px] text-foreground-muted">
                <span>#</span>
                <span>Kind</span>
                <span>Reps min–max</span>
                <span>Weight (kg)</span>
                <span>RIR</span>
                <span />
              </div>
              {e.sets.map((st, s) => (
                <div key={st.key} className="flex flex-col gap-1 rounded-[6px] border border-border/70 p-2">
                  <div className="grid grid-cols-[auto_1fr_1fr_1fr_1fr_auto] items-center gap-2">
                    <span className="text-xs text-foreground-muted">{s + 1}</span>
                    <Segmented
                      aria-label="Set kind"
                      options={[
                        { value: "Standard", label: "Std" },
                        { value: "Drop", label: "Drop" },
                      ]}
                      value={st.kind}
                      onChange={(v) => patchSet(e.key, st.key, { kind: v })}
                    />
                    <span className="flex items-center gap-1">
                      <Input type="number" aria-label="Reps min" value={st.repsMin} onChange={(ev) => patchSet(e.key, st.key, { repsMin: ev.target.value })} />
                      <span className="text-xs text-foreground-muted">–</span>
                      <Input type="number" aria-label="Reps max" value={st.repsMax} onChange={(ev) => patchSet(e.key, st.key, { repsMax: ev.target.value })} />
                    </span>
                    <Input type="number" aria-label="Weight kg" value={st.weightKg} onChange={(ev) => patchSet(e.key, st.key, { weightKg: ev.target.value })} />
                    <Input type="number" aria-label="RIR" value={st.rir} onChange={(ev) => patchSet(e.key, st.key, { rir: ev.target.value })} />
                    <Button
                      variant="ghost"
                      size="sm"
                      iconOnly
                      aria-label="Remove set"
                      onClick={() => setState((prev) => ({ ...prev, exercises: prev.exercises.map((x) => (x.key === e.key ? { ...x, sets: x.sets.filter((y) => y.key !== st.key) } : x)) }))}
                    >
                      <Trash2 size={13} aria-hidden />
                    </Button>
                  </div>
                  <div className="flex gap-4 pl-6">
                    <Checkbox label="AMRAP" checked={st.isAmrap} onChange={(ev) => patchSet(e.key, st.key, { isAmrap: ev.target.checked })} />
                    <Checkbox label="To failure" checked={st.toFailure} onChange={(ev) => patchSet(e.key, st.key, { toFailure: ev.target.checked })} />
                  </div>
                </div>
              ))}

              <div className="flex flex-wrap gap-2">
                <Button variant="ghost" size="sm" onClick={() => patchExercise(e.key, { sets: [...e.sets, blankSet()] })}>
                  <Plus size={13} aria-hidden />
                  Add set
                </Button>
                {e.sets.length > 0 && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      const first = e.sets[0];
                      patchExercise(e.key, { sets: e.sets.map((st) => ({ ...st, repsMin: first.repsMin, repsMax: first.repsMax, weightKg: first.weightKg, rir: first.rir })) });
                    }}
                  >
                    Fill down from set 1
                  </Button>
                )}
                <Button variant="ghost" size="sm" onClick={() => patchExercise(e.key, { moreOpen: !e.moreOpen })}>
                  {e.moreOpen ? "Hide options" : "More options"}
                </Button>
              </div>

              {e.moreOpen && (
                <div className="flex flex-col gap-2 rounded-[6px] border border-border/70 p-2">
                  <label className="flex items-center gap-2 text-xs text-foreground-muted">
                    Rest after exercise (s)
                    <Input type="number" className="max-w-[120px]" value={e.restSeconds} onChange={(ev) => patchExercise(e.key, { restSeconds: ev.target.value })} />
                  </label>
                  <label className="flex flex-col gap-1 text-xs text-foreground-muted">
                    Notes
                    <Input value={e.notes} onChange={(ev) => patchExercise(e.key, { notes: ev.target.value })} />
                  </label>
                  <label className="flex items-center gap-2 text-xs text-foreground-muted">
                    Superset
                    <select
                      className="min-h-[34px] rounded-control border border-border bg-surface px-2 text-[13px] text-foreground"
                      value={e.supersetKey ?? ""}
                      onChange={(ev) => patchExercise(e.key, { supersetKey: ev.target.value === "" ? null : ev.target.value })}
                    >
                      <option value="">No superset</option>
                      {state.supersets.map((g) => (
                        <option key={g.key} value={g.key}>
                          {groupLabel(g.key)}
                        </option>
                      ))}
                    </select>
                    <Button variant="ghost" size="sm" onClick={() => newSuperset(e.key)}>
                      New superset
                    </Button>
                  </label>
                  {grouped && (
                    <label className="flex items-center gap-2 text-xs text-foreground-muted">
                      Rest after superset round (s)
                      <Input
                        type="number"
                        className="max-w-[120px]"
                        value={state.supersets.find((g) => g.key === e.supersetKey)?.restAfterRoundSeconds ?? ""}
                        onChange={(ev) => setGroupRest(e.supersetKey!, ev.target.value)}
                      />
                    </label>
                  )}
                  {grouped && state.exercises.filter((x) => x.supersetKey === e.supersetKey).length < 2 && (
                    <p className="m-0 text-xs text-warning">Add a second exercise to this superset, or it won't be saved.</p>
                  )}
                </div>
              )}
            </div>
          </div>
        );
        }}
      </SortableList>

      {picking ? (
        <ExercisePicker
          onPick={addExercise}
          onClose={() => setPicking(false)}
          existingIds={new Set(state.exercises.map((e) => e.exerciseId))}
          focusMuscleNames={focusNames}
        />
      ) : (
        <Button variant="secondary" onClick={() => setPicking(true)}>
          + Add exercise
        </Button>
      )}
    </div>
  );
}

function BulkTargetMenu({
  label,
  icon,
  targets,
  onPick,
}: {
  label: string;
  icon: ReactNode;
  targets: { id: string; label: string }[];
  onPick: (id: string) => void;
}) {
  const [open, setOpen] = useState(false);
  return (
    <span className="relative">
      <Button variant="secondary" size="sm" onClick={() => setOpen((o) => !o)}>
        {icon}
        {label}
      </Button>
      {open && (
        <span className="absolute left-0 top-full z-10 mt-1 flex min-w-[200px] flex-col rounded-control border border-border bg-surface p-1 shadow-lg">
          {targets.map((t) => (
            <button
              key={t.id}
              type="button"
              className="rounded-[6px] px-2 py-1.5 text-left text-[13px] hover:bg-surface-subtle"
              onClick={() => {
                setOpen(false);
                onPick(t.id);
              }}
            >
              {t.label}
            </button>
          ))}
        </span>
      )}
    </span>
  );
}
