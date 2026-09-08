import { useState } from "react";
import { ArrowDown, ArrowUp, Trash2 } from "lucide-react";
import { Button, Input, Modal } from "../../components/ui";
import { ApiError } from "../../lib/api";
import {
  useCreateMealCategory,
  useDeleteMealCategory,
  useMealCategories,
  useRenameMealCategory,
  useReorderMealCategories,
} from "./api";
import type { MealCategoryRow } from "./api";

function errMessage(e: unknown) {
  return e instanceof ApiError ? (e.detail ?? e.title) : "Something went wrong. Try again.";
}

/** Rename / reorder / add / remove the meal slots shown on the nutrition day (docs/08 #19). */
export function ManageCategoriesDialog({ onClose }: { onClose: () => void }) {
  const { data: rows = [] } = useMealCategories();
  const create = useCreateMealCategory();
  const rename = useRenameMealCategory();
  const reorder = useReorderMealCategories();
  const remove = useDeleteMealCategory();

  const [newName, setNewName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const busy = create.isPending || rename.isPending || reorder.isPending || remove.isPending;

  const move = (index: number, delta: number) => {
    const next = [...rows];
    const target = index + delta;
    if (target < 0 || target >= next.length) return;
    [next[index], next[target]] = [next[target], next[index]];
    setError(null);
    reorder.mutate(next.map((r) => r.id));
  };

  const add = async () => {
    const n = newName.trim();
    if (!n) return;
    setError(null);
    try {
      await create.mutateAsync(n);
      setNewName("");
    } catch (e) {
      setError(errMessage(e));
    }
  };

  return (
    <Modal title="Meal categories" label="Manage meal categories" onClose={onClose}>
      <p className="m-0 text-sm text-foreground-muted">
        These are the slots on your nutrition day. Renaming or removing one never changes days you
        already logged.
      </p>

      <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
        {rows.map((row, i) => (
          <CategoryRow
            key={row.id}
            row={row}
            first={i === 0}
            last={i === rows.length - 1}
            onlyOne={rows.length <= 1}
            busy={busy}
            onRename={(name) => {
              setError(null);
              if (name && name !== row.name) rename.mutate({ id: row.id, name });
            }}
            onUp={() => move(i, -1)}
            onDown={() => move(i, 1)}
            onDelete={async () => {
              setError(null);
              try {
                await remove.mutateAsync(row.id);
              } catch (e) {
                setError(errMessage(e));
              }
            }}
          />
        ))}
      </ul>

      <div className="flex gap-2">
        <Input
          aria-label="New category name"
          placeholder="Add a category…"
          value={newName}
          onChange={(e) => setNewName(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && add()}
        />
        <Button size="sm" onClick={add} disabled={busy || !newName.trim()}>
          Add
        </Button>
      </div>

      {error && (
        <p className="m-0 text-[13px] text-danger" role="alert">
          {error}
        </p>
      )}

      <div className="flex justify-end">
        <Button variant="primary" size="sm" onClick={onClose}>
          Done
        </Button>
      </div>
    </Modal>
  );
}

function CategoryRow({
  row,
  first,
  last,
  onlyOne,
  busy,
  onRename,
  onUp,
  onDown,
  onDelete,
}: {
  row: MealCategoryRow;
  first: boolean;
  last: boolean;
  onlyOne: boolean;
  busy: boolean;
  onRename: (name: string) => void;
  onUp: () => void;
  onDown: () => void;
  onDelete: () => void;
}) {
  const [name, setName] = useState(row.name);

  return (
    <li className="flex items-center gap-1.5">
      <Input
        aria-label={`Name of ${row.name}`}
        className="min-h-9"
        value={name}
        onChange={(e) => setName(e.target.value)}
        onBlur={() => onRename(name.trim())}
        onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()}
      />
      <Button variant="ghost" size="sm" iconOnly aria-label={`Move ${row.name} up`} disabled={first || busy} onClick={onUp}>
        <ArrowUp size={14} aria-hidden />
      </Button>
      <Button
        variant="ghost"
        size="sm"
        iconOnly
        aria-label={`Move ${row.name} down`}
        disabled={last || busy}
        onClick={onDown}
      >
        <ArrowDown size={14} aria-hidden />
      </Button>
      <Button
        variant="ghost"
        size="sm"
        iconOnly
        aria-label={`Remove ${row.name}`}
        disabled={onlyOne || busy}
        onClick={onDelete}
      >
        <Trash2 size={13} aria-hidden />
      </Button>
    </li>
  );
}
