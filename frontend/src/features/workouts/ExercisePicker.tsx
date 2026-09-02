import { useState } from "react";
import { Button, Input } from "../../components/ui";
import { useExerciseSearch } from "./api";
import type { ExerciseListItem } from "./api";

/** A small inline search panel for picking one catalogue exercise. */
export function ExercisePicker({
  onPick,
  onClose,
  existingIds,
}: {
  onPick: (exercise: ExerciseListItem) => void;
  onClose: () => void;
  existingIds: Set<string>;
}) {
  const [q, setQ] = useState("");
  const { data, isLoading } = useExerciseSearch(q);

  return (
    <div className="rounded-control border border-border bg-surface-subtle p-3">
      <div className="mb-2 flex items-center gap-2">
        <Input
          autoFocus
          placeholder="Search the exercise catalogue…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <Button variant="ghost" onClick={onClose}>
          Close
        </Button>
      </div>

      {isLoading ? (
        <p className="m-0 px-1 py-2 text-xs text-foreground-muted">Searching…</p>
      ) : (
        <ul className="m-0 flex max-h-64 list-none flex-col gap-1 overflow-y-auto p-0">
          {data?.items.map((ex) => {
            const added = existingIds.has(ex.id);
            return (
              <li key={ex.id}>
                <button
                  type="button"
                  disabled={added}
                  onClick={() => onPick(ex)}
                  className="flex w-full items-center justify-between gap-3 rounded-control border border-border bg-surface px-3 py-2 text-left text-[13px] hover:border-border-strong disabled:opacity-50"
                >
                  <span>
                    <span className="font-semibold">{ex.name}</span>
                    <span className="ml-2 text-xs text-foreground-muted">{ex.category}</span>
                  </span>
                  <span className="text-xs text-foreground-muted">{added ? "Added" : "Add"}</span>
                </button>
              </li>
            );
          })}
          {data && data.items.length === 0 && (
            <li className="px-1 py-2 text-xs text-foreground-muted">No matches.</li>
          )}
        </ul>
      )}
    </div>
  );
}
