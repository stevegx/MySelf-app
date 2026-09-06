import { useState } from "react";
import { X } from "lucide-react";
import { Button, Input } from "../../components/ui";
import { useExerciseSearch } from "./api";
import type { ExerciseListItem } from "./api";

/**
 * Search + pick one catalogue exercise. Renders inline (`drawer` false) or as a right-side
 * slide-in drawer with a backdrop (`drawer` true — the program builder uses this).
 */
export function ExercisePicker({
  onPick,
  onClose,
  existingIds,
  focusMuscleNames,
  focusLabels,
  drawer = false,
}: {
  onPick: (exercise: ExerciseListItem) => void;
  onClose: () => void;
  existingIds: Set<string>;
  /** When set, exercises whose primary muscle is in this list are shown first / on their own. */
  focusMuscleNames?: string[];
  /** Human-friendly names for the focus note (e.g. the 7 muscle groups); falls back to the raw muscle names. */
  focusLabels?: string[];
  drawer?: boolean;
}) {
  const [q, setQ] = useState("");
  const [showAll, setShowAll] = useState(false);
  const { data, isLoading } = useExerciseSearch(q);

  const focus = focusMuscleNames ?? [];
  const inFocus = (ex: ExerciseListItem) =>
    focus.length === 0 || (ex.primaryMuscles ?? []).some((m) => focus.includes(m));

  const items = data?.items ?? [];
  const matches = focus.length === 0 ? items : items.filter(inFocus);
  const shown = focus.length === 0 || showAll ? items : matches;
  const hiddenCount = items.length - matches.length;

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

      {focus.length > 0 && (
        <p className="m-0 mb-1 px-1 text-[11px] text-foreground-muted">
          Showing exercises for {(focusLabels && focusLabels.length > 0 ? focusLabels : focus).join(", ")}.{" "}
          {hiddenCount > 0 && (
            <button type="button" className="text-primary underline" onClick={() => setShowAll(true)}>
              Show all ({hiddenCount} more)
            </button>
          )}
          {showAll && (
            <button type="button" className="text-primary underline" onClick={() => setShowAll(false)}>
              Show only focus
            </button>
          )}
        </p>
      )}

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
            const off = focus.length > 0 && !inFocus(ex);
            return (
              <li key={ex.id}>
                <button
                  type="button"
                  disabled={added}
                  onClick={() => onPick(ex)}
                  className="flex w-full items-center gap-3 rounded-control border border-border bg-surface px-3 py-2 text-left text-[13px] hover:border-border-strong disabled:opacity-50"
                >
                  <span className="min-w-0 flex-1">
                    <span className="font-semibold">{ex.name}</span>
                    <span className="ml-2 text-xs text-foreground-muted">{ex.category}</span>
                    {off && <span className="ml-2 text-[11px] text-info">off focus</span>}
                    {((ex.primaryMuscles?.length ?? 0) > 0 || (ex.equipment?.length ?? 0) > 0) && (
                      <span className="mt-0.5 block truncate text-[11px] text-foreground-subtle">
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
              {focus.length > 0 && !showAll ? (
                <>
                  No focus matches.{" "}
                  <button type="button" className="text-primary underline" onClick={() => setShowAll(true)}>
                    Show all
                  </button>
                </>
              ) : (
                "No matches."
              )}
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
