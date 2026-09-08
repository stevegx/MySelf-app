import { useState, type ReactNode } from "react";
import { X } from "lucide-react";
import { Button, Input } from "../../components/ui";
import { cn } from "../../lib/cn";
import { MUSCLE_GROUPS, expandGroupsToMuscleNames, type MuscleGroupKey } from "./muscleGroups";
import { useExerciseSearch } from "./api";
import type { ExerciseListItem } from "./api";

/**
 * Search + pick one catalogue exercise. Renders inline (`drawer` false) or as a right-side
 * slide-in drawer with a backdrop (`drawer` true — the program builder uses this).
 *
 * A pill row filters the list by muscle group: "All" plus the seven groups. When the day
 * has a focus, those groups start selected; the user can tap "All" or other groups freely.
 */
export function ExercisePicker({
  onPick,
  onClose,
  existingIds,
  focusGroupKeys,
  drawer = false,
}: {
  onPick: (exercise: ExerciseListItem) => void;
  onClose: () => void;
  existingIds: Set<string>;
  /** Muscle groups the day focuses on — used to pre-select the filter pills. */
  focusGroupKeys?: MuscleGroupKey[];
  drawer?: boolean;
}) {
  const [q, setQ] = useState("");
  const [selected, setSelected] = useState<Set<MuscleGroupKey>>(() => new Set(focusGroupKeys ?? []));
  const { data, isLoading } = useExerciseSearch(q);

  const toggle = (key: MuscleGroupKey) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });

  const activeNames =
    selected.size === 0 ? null : new Set(expandGroupsToMuscleNames([...selected]));

  const items = data?.items ?? [];
  const shown =
    activeNames === null
      ? items
      : items.filter((ex) => (ex.primaryMuscles ?? []).some((m) => activeNames.has(m)));

  const body = (
    <>
      <div className={drawer ? "flex items-center justify-between" : "mb-2 flex items-center gap-2"}>
        {drawer && <h3 className="m-0">Add exercise</h3>}
        {drawer ? (
          <Button variant="secondary" iconOnly aria-label="Close" onClick={onClose}>
            <X size={16} aria-hidden />
          </Button>
        ) : null}
      </div>

      <div className={drawer ? "flex items-center gap-2" : "mb-2 flex items-center gap-2"}>
        <Input
          autoFocus
          placeholder="Search the exercise catalogue…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        {!drawer && (
          <Button variant="ghost" onClick={onClose}>
            Close
          </Button>
        )}
      </div>

      <div className="flex flex-wrap gap-1.5">
        <FilterPill active={selected.size === 0} onClick={() => setSelected(new Set())}>
          All
        </FilterPill>
        {MUSCLE_GROUPS.map((g) => (
          <FilterPill key={g.key} active={selected.has(g.key)} onClick={() => toggle(g.key)}>
            {g.label}
          </FilterPill>
        ))}
      </div>

      {isLoading ? (
        <p className="m-0 px-1 py-2 text-xs text-foreground-muted">Searching…</p>
      ) : (
        <ul
          className={
            drawer
              ? "m-0 flex flex-1 list-none flex-col gap-1 overflow-y-auto p-0"
              : "m-0 flex max-h-64 list-none flex-col gap-1 overflow-y-auto p-0"
          }
        >
          {shown.map((ex) => {
            const added = existingIds.has(ex.id);
            return (
              <li key={ex.id}>
                <button
                  type="button"
                  disabled={added}
                  onClick={() => onPick(ex)}
                  className="flex w-full items-center gap-3 rounded-control border border-border bg-surface px-3 py-2 text-left text-sm hover:border-border-strong disabled:opacity-50"
                >
                  <span className="min-w-0 flex-1">
                    <span className="font-semibold">{ex.name}</span>
                    <span className="ml-2 text-xs text-foreground-muted">{ex.category}</span>
                    {((ex.primaryMuscles?.length ?? 0) > 0 || (ex.equipment?.length ?? 0) > 0) && (
                      <span className="mt-0.5 block truncate text-[12px] text-foreground-subtle">
                        {(ex.primaryMuscles ?? []).join(", ")}
                        {(ex.secondaryMuscles?.length ?? 0) > 0 && (
                          <span className="text-foreground-subtle"> · +{ex.secondaryMuscles.join(", ")}</span>
                        )}
                        {(ex.equipment?.length ?? 0) > 0 && <span> · {ex.equipment.join(", ")}</span>}
                      </span>
                    )}
                  </span>
                  <span className="shrink-0 text-xs text-foreground-muted">{added ? "Added" : "Add"}</span>
                </button>
              </li>
            );
          })}
          {data && shown.length === 0 && (
            <li className="px-1 py-2 text-xs text-foreground-muted">
              {activeNames === null ? "No matches." : "No matches in these groups."}
            </li>
          )}
        </ul>
      )}
    </>
  );

  if (!drawer) {
    return <div className="rounded-control border border-border bg-surface-subtle p-3">{body}</div>;
  }

  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-black/40"
      role="dialog"
      aria-modal="true"
      aria-label="Add exercise"
      onClick={onClose}
    >
      <div
        className="flex h-full w-[380px] max-w-[92vw] flex-col gap-3 overflow-y-auto bg-background p-5 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        {body}
      </div>
    </div>
  );
}

function FilterPill({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        "rounded-pill border px-2.5 py-1 text-[13px] font-medium",
        active
          ? "border-primary bg-primary-soft text-primary-pressed"
          : "border-border text-foreground-muted hover:border-border-strong",
      )}
    >
      {children}
    </button>
  );
}
