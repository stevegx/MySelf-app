import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight, Plus, Trash2 } from "lucide-react";
import { Button, Card, CardTitle, Checkbox, Input, PageHeader, Ring, Skeleton } from "../../components/ui";
import { AddFoodDialog } from "./AddFoodDialog";
import { ManageCategoriesDialog } from "./MealCategories";
import { SaveMealDialog, SavedMealsBar } from "./SavedMeals";
import {
  toRestoreInput,
  useBulkCopyItems,
  useBulkDeleteItems,
  useBulkMoveItems,
  useBulkRestoreItems,
  useDeleteMealItem,
  useMealCategories,
  useNutritionDay,
  useUpdateMealItem,
} from "./api";
import type { MealCategory, MealItem, RestoreMealItemInput } from "./api";

const iso = (d: Date) => d.toISOString().slice(0, 10);
const todayIso = () => iso(new Date());

function shiftDay(date: string, days: number) {
  const d = new Date(`${date}T00:00:00`);
  d.setDate(d.getDate() + days);
  return iso(d);
}

function labelFor(date: string) {
  const t = todayIso();
  if (date === t) return "Today";
  if (date === shiftDay(t, -1)) return "Yesterday";
  return new Date(`${date}T00:00:00`).toLocaleDateString(undefined, {
    weekday: "short",
    day: "numeric",
    month: "short",
  });
}

const UNIT_LABEL = { Grams: "g", Millilitres: "ml", Serving: "serving" } as const;

const round = (n: number) => Math.round(n * 10) / 10;

function MacroRing({ label, value, target }: { label: string; value: number; target: number | null }) {
  return (
    <div className="text-center">
      <Ring size={48} stroke={5} value={target ? value / target : 0} ariaLabel={`${label}: ${round(value)} of ${target ?? "no"} g`} />
      <div className="mt-1 text-xs font-semibold">{label}</div>
      <div className="text-[12px] text-foreground-muted">
        {round(value)}
        {target != null ? ` / ${target}` : ""} g
      </div>
    </div>
  );
}

function ItemRow({
  date,
  item,
  selecting,
  checked,
  onToggle,
}: {
  date: string;
  item: MealItem;
  selecting: boolean;
  checked: boolean;
  onToggle: () => void;
}) {
  const update = useUpdateMealItem(date);
  const del = useDeleteMealItem(date);
  const [amount, setAmount] = useState(String(item.amount));

  const commit = () => {
    const n = Number(amount);
    if (Number.isFinite(n) && n > 0 && n !== item.amount) {
      update.mutate({ id: item.id, amount: n, unit: item.unit, servingSizeGrams: item.servingSizeGrams });
    } else {
      setAmount(String(item.amount));
    }
  };

  if (selecting) {
    return (
      <div className="flex items-center gap-2 border-t border-border py-2 text-sm first:border-t-0">
        <Checkbox label={item.name} checked={checked} onChange={onToggle} />
        <span className="ml-auto shrink-0 text-xs text-foreground-muted">
          {round(item.amount)} {UNIT_LABEL[item.unit]}
        </span>
        <span className="w-20 shrink-0 text-right tabular-nums">{round(item.kcal)} kcal</span>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2 border-t border-border py-2 text-sm first:border-t-0">
      <span className="min-w-0 flex-1 truncate">{item.name}</span>
      <span className="w-[4.5rem] shrink-0">
        <Input
          type="number"
          inputMode="decimal"
          aria-label={`Amount of ${item.name}`}
          className="min-h-8 px-2 text-right"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()}
        />
      </span>
      <span className="w-12 shrink-0 text-xs text-foreground-muted">{UNIT_LABEL[item.unit]}</span>
      <span className="w-20 shrink-0 text-right tabular-nums">{round(item.kcal)} kcal</span>
      <Button
        variant="ghost"
        size="sm"
        iconOnly
        aria-label={`Remove ${item.name}`}
        disabled={del.isPending}
        onClick={() => del.mutate(item.id)}
      >
        <Trash2 size={13} aria-hidden />
      </Button>
    </div>
  );
}

/** A small modal that just picks one meal slot — reused for bulk Move and bulk Copy. */
function PickSlotDialog({
  title,
  confirmLabel,
  onPick,
  onClose,
}: {
  title: string;
  confirmLabel: string;
  onPick: (category: MealCategory) => void;
  onClose: () => void;
}) {
  const { data: rows = [] } = useMealCategories();

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onKeyDown={(e) => e.key === "Escape" && onClose()}
    >
      <div className="flex w-full max-w-xs flex-col gap-2 rounded-card border border-border bg-surface p-4 shadow-xl">
        <h3 className="m-0 text-base font-bold">{title}</h3>
        <div className="flex flex-col gap-1">
          {rows.map((r) => (
            <Button key={r.id} variant="secondary" block onClick={() => onPick(r.name)}>
              {confirmLabel} {r.name}
            </Button>
          ))}
        </div>
        <div className="flex justify-end">
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
        </div>
      </div>
    </div>
  );
}

type UndoState = { message: string; onUndo: () => void };

/** Fixed bar offering to undo the last destructive bulk action. Auto-dismisses after 8s. */
function UndoBar({ state, onDismiss }: { state: UndoState; onDismiss: () => void }) {
  useEffect(() => {
    const t = setTimeout(onDismiss, 8000);
    return () => clearTimeout(t);
  }, [state, onDismiss]);

  return (
    <div className="fixed inset-x-0 bottom-4 z-50 mx-auto flex w-fit items-center gap-3 rounded-pill border border-border bg-surface px-4 py-2 text-sm shadow-xl">
      <span>{state.message}</span>
      <button
        type="button"
        className="font-semibold text-primary underline"
        onClick={() => {
          state.onUndo();
          onDismiss();
        }}
      >
        Undo
      </button>
      <button type="button" aria-label="Dismiss" className="text-foreground-muted" onClick={onDismiss}>
        ✕
      </button>
    </div>
  );
}

export function NutritionScreen() {
  const [date, setDate] = useState(todayIso());
  const [adding, setAdding] = useState<MealCategory | null>(null);
  const [savingMeal, setSavingMeal] = useState<{ category: MealCategory; items: MealItem[] } | null>(null);
  const [managing, setManaging] = useState(false);
  const { data: day, isLoading } = useNutritionDay(date);

  const [selecting, setSelecting] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(() => new Set());
  const [picker, setPicker] = useState<"move" | "copy" | null>(null);
  const [undo, setUndo] = useState<UndoState | null>(null);

  const bulkDelete = useBulkDeleteItems(date);
  const bulkMove = useBulkMoveItems(date);
  const bulkCopy = useBulkCopyItems(date);
  const bulkRestore = useBulkRestoreItems(date);

  const targets = day?.targets;
  const totals = day?.totals ?? { kcal: 0, proteinG: 0, carbG: 0, fatG: 0 };
  const kcalTarget = targets?.kcal ?? null;
  const remaining = kcalTarget != null ? Math.round(kcalTarget - totals.kcal) : null;

  // id -> slot, for restoring/undoing a move to the original slots.
  const slotById = new Map<string, MealCategory>();
  for (const meal of day?.meals ?? []) {
    for (const it of meal.items) slotById.set(it.id, meal.category);
  }
  const restoreInputsFor = (ids: string[]): RestoreMealItemInput[] => {
    const byId = new Map<string, MealItem>();
    for (const meal of day?.meals ?? []) for (const it of meal.items) byId.set(it.id, it);
    return ids
      .map((id) => {
        const it = byId.get(id);
        return it ? toRestoreInput(slotById.get(id)!, it) : null;
      })
      .filter((x): x is RestoreMealItemInput => x != null);
  };

  const leaveSelect = () => {
    setSelecting(false);
    setSelected(new Set());
    setPicker(null);
  };

  const toggle = (id: string) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const doDelete = async () => {
    const ids = [...selected];
    const snapshots = restoreInputsFor(ids);
    leaveSelect();
    await bulkDelete.mutateAsync(ids);
    setUndo({
      message: `Removed ${ids.length} ${ids.length === 1 ? "item" : "items"}`,
      onUndo: () => bulkRestore.mutate(snapshots),
    });
  };

  const doMove = async (toCategory: MealCategory) => {
    const ids = [...selected];
    const origin = new Map<string, MealCategory>();
    for (const id of ids) origin.set(id, slotById.get(id)!);
    leaveSelect();
    await bulkMove.mutateAsync({ ids, toCategory });
    setUndo({
      message: `Moved ${ids.length} ${ids.length === 1 ? "item" : "items"} to ${toCategory}`,
      onUndo: () => {
        const byOrigin = new Map<MealCategory, string[]>();
        for (const [id, cat] of origin) byOrigin.set(cat, [...(byOrigin.get(cat) ?? []), id]);
        for (const [cat, catIds] of byOrigin) bulkMove.mutate({ ids: catIds, toCategory: cat });
      },
    });
  };

  const doCopy = async (toCategory: MealCategory) => {
    const ids = [...selected];
    leaveSelect();
    await bulkCopy.mutateAsync({ ids, toCategory });
  };

  const doDuplicate = async () => {
    const ids = [...selected];
    const byOrigin = new Map<MealCategory, string[]>();
    for (const id of ids) {
      const cat = slotById.get(id)!;
      byOrigin.set(cat, [...(byOrigin.get(cat) ?? []), id]);
    }
    leaveSelect();
    for (const [cat, catIds] of byOrigin) await bulkCopy.mutateAsync({ ids: catIds, toCategory: cat });
  };

  return (
    <>
      {adding && <AddFoodDialog date={date} category={adding} onClose={() => setAdding(null)} />}
      {savingMeal && (
        <SaveMealDialog
          category={savingMeal.category}
          items={savingMeal.items}
          onClose={() => setSavingMeal(null)}
        />
      )}
      {managing && <ManageCategoriesDialog onClose={() => setManaging(false)} />}
      {picker === "move" && (
        <PickSlotDialog title="Move to…" confirmLabel="Move to" onPick={doMove} onClose={() => setPicker(null)} />
      )}
      {picker === "copy" && (
        <PickSlotDialog title="Copy to…" confirmLabel="Copy to" onPick={doCopy} onClose={() => setPicker(null)} />
      )}
      {undo && <UndoBar state={undo} onDismiss={() => setUndo(null)} />}

      <PageHeader
        title="Nutrition"
        actions={
          selecting ? (
            <Button variant="ghost" size="sm" onClick={leaveSelect}>
              Done
            </Button>
          ) : (
            <>
              <Button variant="ghost" size="sm" onClick={() => setManaging(true)}>
                Manage
              </Button>
              <Button variant="secondary" size="sm" onClick={() => setSelecting(true)}>
                Select
              </Button>
            </>
          )
        }
        subtitle={
          <span className="inline-flex items-center gap-1.5">
            <button type="button" aria-label="Previous day" onClick={() => setDate((d) => shiftDay(d, -1))}>
              <ChevronLeft size={16} aria-hidden />
            </button>
            <span className="min-w-[7rem] text-center font-semibold text-foreground">{labelFor(date)}</span>
            <button
              type="button"
              aria-label="Next day"
              disabled={date >= todayIso()}
              onClick={() => setDate((d) => shiftDay(d, 1))}
              className="disabled:opacity-30"
            >
              <ChevronRight size={16} aria-hidden />
            </button>
            {date !== todayIso() && (
              <button type="button" className="ml-1 text-primary underline" onClick={() => setDate(todayIso())}>
                Today
              </button>
            )}
          </span>
        }
      />

      {isLoading ? (
        <Card className="gap-3">
          <Skeleton className="h-24" />
          <Skeleton className="h-16" />
        </Card>
      ) : (
        <>
          <Card className="mb-4 flex-row flex-wrap items-center gap-7">
            <Ring
              size={108}
              stroke={9}
              value={kcalTarget ? totals.kcal / kcalTarget : 0}
              ariaLabel={
                kcalTarget != null
                  ? `Calories: ${round(totals.kcal)} of ${kcalTarget} kcal`
                  : `Calories logged: ${round(totals.kcal)} kcal`
              }
            />
            <div>
              <div className="text-2xl font-bold">
                {round(totals.kcal)}{" "}
                <span className="text-sm font-normal text-foreground-muted">
                  {kcalTarget != null ? `/ ${kcalTarget} kcal` : "kcal"}
                </span>
              </div>
              <div className="text-sm text-foreground-muted">
                {kcalTarget != null
                  ? `${remaining} kcal ${remaining! >= 0 ? "left" : "over"}`
                  : "No calorie target — set one in Settings"}
              </div>
            </div>
            <div className="ml-auto flex gap-4">
              <MacroRing label="Protein" value={totals.proteinG} target={targets?.proteinG ?? null} />
              <MacroRing label="Carbs" value={totals.carbG} target={targets?.carbG ?? null} />
              <MacroRing label="Fat" value={totals.fatG} target={targets?.fatG ?? null} />
            </div>
          </Card>

          {!selecting && <SavedMealsBar date={date} />}

          <div className={`flex flex-col gap-3 ${selecting && selected.size > 0 ? "pb-20" : ""}`}>
            {(day?.meals ?? []).map((meal) => (
              <Card key={meal.category}>
                <div className="flex items-center justify-between">
                  <CardTitle>{meal.category}</CardTitle>
                  <span className="text-sm text-foreground-muted">{round(meal.subtotals.kcal)} kcal</span>
                </div>

                {meal.items.map((item) => (
                  <ItemRow
                    key={item.id}
                    date={date}
                    item={item}
                    selecting={selecting}
                    checked={selected.has(item.id)}
                    onToggle={() => toggle(item.id)}
                  />
                ))}

                {!selecting && (
                  <div className="mt-1 flex gap-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      aria-label={`Add food to ${meal.category}`}
                      onClick={() => setAdding(meal.category)}
                    >
                      <Plus size={14} aria-hidden />
                      Add food
                    </Button>
                    {meal.items.length > 0 && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setSavingMeal({ category: meal.category, items: meal.items })}
                      >
                        Save as meal
                      </Button>
                    )}
                  </div>
                )}
              </Card>
            ))}
          </div>

          {selecting && selected.size > 0 && (
            <div className="fixed inset-x-0 bottom-0 z-40 border-t border-border bg-surface px-4 py-3 shadow-[0_-4px_16px_rgba(0,0,0,0.08)]">
              <div className="mx-auto flex max-w-3xl flex-wrap items-center gap-2">
                <span className="text-sm font-semibold">{selected.size} selected</span>
                <div className="ml-auto flex flex-wrap gap-2">
                  <Button size="sm" onClick={() => setPicker("move")} disabled={bulkMove.isPending}>
                    Move
                  </Button>
                  <Button size="sm" onClick={() => setPicker("copy")} disabled={bulkCopy.isPending}>
                    Copy to
                  </Button>
                  <Button size="sm" onClick={doDuplicate} disabled={bulkCopy.isPending}>
                    Duplicate
                  </Button>
                  <Button variant="danger" size="sm" onClick={doDelete} disabled={bulkDelete.isPending}>
                    Delete
                  </Button>
                </div>
              </div>
            </div>
          )}
        </>
      )}
    </>
  );
}
