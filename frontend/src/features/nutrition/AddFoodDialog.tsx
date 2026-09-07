import { useState } from "react";
import { Button, Input, Segmented } from "../../components/ui";
import { ApiError } from "../../lib/api";
import { useAddMealItem } from "./api";
import type { MealAmountUnit, MealCategory, ServingBasis } from "./api";

const num = (s: string) => (s.trim() === "" ? NaN : Number(s));

/**
 * Manual food entry (docs/03 §8.7 "Create food manually"): a name, the nutrients per 100 g
 * or per serving, and how much was eaten. Barcode / My Foods prefill this same form in
 * later slices. Mount only while open so each open starts clean.
 */
export function AddFoodDialog({
  date,
  category,
  onClose,
}: {
  date: string;
  category: MealCategory;
  onClose: () => void;
}) {
  const add = useAddMealItem(date);
  const [name, setName] = useState("");
  const [basis, setBasis] = useState<ServingBasis>("Per100g");
  const [servingSize, setServingSize] = useState("");
  const [kcal, setKcal] = useState("");
  const [protein, setProtein] = useState("");
  const [carb, setCarb] = useState("");
  const [fat, setFat] = useState("");
  const [amount, setAmount] = useState("");
  const [unit, setUnit] = useState<MealAmountUnit>("Grams");
  const [error, setError] = useState<string | null>(null);

  // Per-serving foods are logged in servings by default; per-100g in grams.
  const onBasisChange = (b: ServingBasis) => {
    setBasis(b);
    setUnit(b === "PerServing" ? "Serving" : "Grams");
  };

  const needsServingSize =
    (basis === "PerServing" && unit !== "Serving") || (basis === "Per100g" && unit === "Serving");

  const submit = async () => {
    const n = name.trim();
    const values = { kcal: num(kcal), protein: num(protein), carb: num(carb), fat: num(fat), amount: num(amount) };
    if (!n) return setError("Enter a food name.");
    if (Object.values(values).some((v) => !Number.isFinite(v) || v < 0)) {
      return setError("Fill in the nutrients and amount with non-negative numbers.");
    }
    if (values.amount <= 0) return setError("Amount must be greater than zero.");
    if (needsServingSize && !(num(servingSize) > 0)) {
      return setError("Enter the serving size in grams for this unit.");
    }
    setError(null);
    try {
      await add.mutateAsync({
        category,
        name: n,
        servingBasis: basis,
        servingSizeGrams: needsServingSize || servingSize.trim() !== "" ? num(servingSize) : null,
        perBasisKcal: values.kcal,
        perBasisProteinG: values.protein,
        perBasisCarbG: values.carb,
        perBasisFatG: values.fat,
        amount: values.amount,
        unit,
      });
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? (Object.values(e.errors ?? {})[0]?.[0] ?? e.detail ?? e.title)
          : "Couldn't save that. Try again.",
      );
    }
  };

  const basisLabel = basis === "Per100g" ? "per 100 g / ml" : "per serving";

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-label={`Add food to ${category}`}
      onKeyDown={(e) => e.key === "Escape" && onClose()}
    >
      <div className="flex max-h-[92vh] w-full max-w-md flex-col gap-3 overflow-y-auto rounded-card border border-border bg-surface p-4 shadow-xl">
        <h3 className="m-0 text-base font-bold">Add food · {category}</h3>

        <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
          Name
          <Input autoFocus value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Plain yogurt" />
        </label>

        <Segmented<ServingBasis>
          aria-label="Nutrients are given"
          value={basis}
          onChange={onBasisChange}
          options={[
            { value: "Per100g", label: "Per 100 g" },
            { value: "PerServing", label: "Per serving" },
          ]}
        />

        <div className="grid grid-cols-2 gap-2">
          <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
            Calories ({basisLabel})
            <Input type="number" inputMode="decimal" value={kcal} onChange={(e) => setKcal(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
            Protein g
            <Input type="number" inputMode="decimal" value={protein} onChange={(e) => setProtein(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
            Carbs g
            <Input type="number" inputMode="decimal" value={carb} onChange={(e) => setCarb(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
            Fat g
            <Input type="number" inputMode="decimal" value={fat} onChange={(e) => setFat(e.target.value)} />
          </label>
        </div>

        <div className="flex items-end gap-2">
          <label className="flex flex-1 flex-col gap-1 text-xs font-semibold text-foreground-muted">
            Amount eaten
            <Input
              type="number"
              inputMode="decimal"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && submit()}
            />
          </label>
          <Segmented<MealAmountUnit>
            aria-label="Unit"
            value={unit}
            onChange={setUnit}
            options={[
              { value: "Grams", label: "g" },
              { value: "Millilitres", label: "ml" },
              { value: "Serving", label: "serving" },
            ]}
          />
        </div>

        {needsServingSize && (
          <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
            Serving size (g)
            <Input
              type="number"
              inputMode="decimal"
              value={servingSize}
              onChange={(e) => setServingSize(e.target.value)}
              placeholder="grams one serving weighs"
            />
          </label>
        )}

        {error && <p className="m-0 text-[13px] text-danger">{error}</p>}

        <div className="flex justify-end gap-2">
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" size="sm" onClick={submit} disabled={add.isPending}>
            {add.isPending ? "Adding…" : "Add"}
          </Button>
        </div>
      </div>
    </div>
  );
}
