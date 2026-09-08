import { useState } from "react";
import { Trash2 } from "lucide-react";
import { Button, Input, Segmented } from "../../components/ui";
import { ApiError } from "../../lib/api";
import {
  useAddSavedMealToDay,
  useCreateSavedMeal,
  useDeleteSavedMeal,
  useMealCategories,
  useSavedMeals,
} from "./api";
import type { MealCategory, MealItem, SavedMeal } from "./api";

const round = (n: number) => Math.round(n * 10) / 10;
const MULTIPLIERS = [0.5, 1, 1.5, 2] as const;

/** The strip of saved-meal chips above the day. Hidden until the user has saved one. */
export function SavedMealsBar({ date }: { date: string }) {
  const { data: meals } = useSavedMeals();
  const del = useDeleteSavedMeal();
  const [picking, setPicking] = useState<SavedMeal | null>(null);

  if (!meals || meals.length === 0) return null;

  return (
    <div className="mb-3">
      {picking && <AddSavedMealDialog meal={picking} date={date} onClose={() => setPicking(null)} />}
      <div className="mb-1 text-xs font-semibold text-foreground-muted">Saved meals</div>
      <div className="flex flex-wrap gap-2">
        {meals.map((m) => (
          <span
            key={m.id}
            className="inline-flex items-center gap-1 rounded-pill border border-border bg-surface-subtle py-1 pl-3 pr-1 text-[13px]"
          >
            <button type="button" className="font-semibold" onClick={() => setPicking(m)}>
              {m.name}
            </button>
            <span className="text-xs text-foreground-muted">{round(m.totals.kcal)} kcal</span>
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              aria-label={`Delete saved meal ${m.name}`}
              disabled={del.isPending}
              onClick={() => del.mutate(m.id)}
            >
              <Trash2 size={12} aria-hidden />
            </Button>
          </span>
        ))}
      </div>
    </div>
  );
}

function AddSavedMealDialog({
  meal,
  date,
  onClose,
}: {
  meal: SavedMeal;
  date: string;
  onClose: () => void;
}) {
  const add = useAddSavedMealToDay(date);
  const { data: categories = [] } = useMealCategories();
  const [multiplier, setMultiplier] = useState<number>(1);
  const [category, setCategory] = useState<MealCategory>(meal.category);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setError(null);
    try {
      await add.mutateAsync({ id: meal.id, category, multiplier });
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? (e.detail ?? e.title) : "Couldn't add that. Try again.");
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-label={`Add ${meal.name}`}
      onKeyDown={(e) => e.key === "Escape" && onClose()}
    >
      <div className="flex w-full max-w-sm flex-col gap-3 rounded-card border border-border bg-surface p-4 shadow-xl">
        <h3 className="m-0 text-base font-bold">{meal.name}</h3>

        <div className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
          Amount
          <div className="flex gap-1.5">
            {MULTIPLIERS.map((x) => (
              <button
                key={x}
                type="button"
                aria-pressed={multiplier === x}
                onClick={() => setMultiplier(x)}
                className={
                  multiplier === x
                    ? "rounded-pill bg-primary px-3 py-1 text-sm font-semibold text-on-primary"
                    : "rounded-pill border border-border px-3 py-1 text-sm text-foreground-muted hover:border-border-strong"
                }
              >
                {x}×
              </button>
            ))}
          </div>
        </div>

        <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
          Add to
          <Segmented<MealCategory>
            aria-label="Meal"
            value={category}
            onChange={setCategory}
            options={(categories.length ? categories.map((c) => c.name) : [meal.category]).map((c) => ({
              value: c,
              label: c,
            }))}
          />
        </label>

        <p className="m-0 text-sm text-foreground-muted">
          Adds {meal.items.length} {meal.items.length === 1 ? "food" : "foods"} · ≈{" "}
          {round(meal.totals.kcal * multiplier)} kcal. You can fine-tune each amount afterwards.
        </p>

        {error && <p className="m-0 text-[13px] text-danger">{error}</p>}

        <div className="flex justify-end gap-2">
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" size="sm" onClick={submit} disabled={add.isPending}>
            {add.isPending ? "Adding…" : "Add to day"}
          </Button>
        </div>
      </div>
    </div>
  );
}

/** "Save as meal" from a populated meal card — turns its items into a reusable template. */
export function SaveMealDialog({
  category,
  items,
  onClose,
}: {
  category: MealCategory;
  items: MealItem[];
  onClose: () => void;
}) {
  const create = useCreateSavedMeal();
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    const n = name.trim();
    if (!n) return setError("Give the meal a name.");
    setError(null);
    try {
      await create.mutateAsync({
        name: n,
        category,
        notes: null,
        items: items.map((i) => ({
          name: i.name,
          servingBasis: i.servingBasis,
          servingSizeGrams: i.servingSizeGrams,
          perBasisKcal: i.basisKcal,
          perBasisProteinG: i.basisProteinG,
          perBasisCarbG: i.basisCarbG,
          perBasisFatG: i.basisFatG,
          defaultAmount: i.amount,
          unit: i.unit,
        })),
      });
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? (e.detail ?? e.title) : "Couldn't save that. Try again.");
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-label={`Save ${category} as a meal`}
      onKeyDown={(e) => e.key === "Escape" && onClose()}
    >
      <div className="flex w-full max-w-sm flex-col gap-3 rounded-card border border-border bg-surface p-4 shadow-xl">
        <h3 className="m-0 text-base font-bold">Save as a meal</h3>
        <p className="m-0 text-sm text-foreground-muted">
          Saves {items.length} {items.length === 1 ? "food" : "foods"} from {category} as a reusable
          template.
        </p>
        <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
          Name
          <Input
            autoFocus
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && submit()}
            placeholder="e.g. Chicken & Rice Lunch"
          />
        </label>
        {error && <p className="m-0 text-[13px] text-danger">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" size="sm" onClick={submit} disabled={create.isPending}>
            {create.isPending ? "Saving…" : "Save meal"}
          </Button>
        </div>
      </div>
    </div>
  );
}
