import { useState } from "react";
import { ChevronLeft, ChevronRight, Plus, Trash2 } from "lucide-react";
import { Button, Card, CardTitle, Input, PageHeader, Ring, Skeleton } from "../../components/ui";
import { AddFoodDialog } from "./AddFoodDialog";
import { useDeleteMealItem, useNutritionDay, useUpdateMealItem } from "./api";
import type { MealCategory, MealItem } from "./api";

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

function ItemRow({ date, item }: { date: string; item: MealItem }) {
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

export function NutritionScreen() {
  const [date, setDate] = useState(todayIso());
  const [adding, setAdding] = useState<MealCategory | null>(null);
  const { data: day, isLoading } = useNutritionDay(date);

  const targets = day?.targets;
  const totals = day?.totals ?? { kcal: 0, proteinG: 0, carbG: 0, fatG: 0 };
  const kcalTarget = targets?.kcal ?? null;
  const remaining = kcalTarget != null ? Math.round(kcalTarget - totals.kcal) : null;

  return (
    <>
      {adding && <AddFoodDialog date={date} category={adding} onClose={() => setAdding(null)} />}

      <PageHeader
        title="Nutrition"
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

          <div className="flex flex-col gap-3">
            {(day?.meals ?? []).map((meal) => (
              <Card key={meal.category}>
                <div className="flex items-center justify-between">
                  <CardTitle>{meal.category}</CardTitle>
                  <span className="text-sm text-foreground-muted">{round(meal.subtotals.kcal)} kcal</span>
                </div>

                {meal.items.map((item) => (
                  <ItemRow key={item.id} date={date} item={item} />
                ))}

                <Button
                  variant="ghost"
                  size="sm"
                  className="mt-1 self-start"
                  aria-label={`Add food to ${meal.category}`}
                  onClick={() => setAdding(meal.category)}
                >
                  <Plus size={14} aria-hidden />
                  Add food
                </Button>
              </Card>
            ))}
          </div>
        </>
      )}
    </>
  );
}
