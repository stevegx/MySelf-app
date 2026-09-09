import { useState } from "react";
import { Button, Checkbox, Input, Modal, Segmented, Tag } from "../../components/ui";
import { ApiError } from "../../lib/api";
import { useAddMealItem, useBarcodeLookup, useCreateCustomFood, useFoodSearch } from "./api";
import type { CustomFood, MealAmountUnit, MealCategory, ServingBasis } from "./api";

const num = (s: string) => (s.trim() === "" ? NaN : Number(s));

/**
 * Add a food to a meal slot. Two ways in, sharing one form: pick a saved food from "My
 * Foods" (prefills the nutrients), or type it manually and optionally tick "Save to My
 * Foods". Barcode prefill lands here too in a later slice. Mount only while open.
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
  const createFood = useCreateCustomFood();
  const barcode = useBarcodeLookup();

  const [search, setSearch] = useState("");
  const { data: results } = useFoodSearch(search.trim());
  const [code, setCode] = useState("");

  const [name, setName] = useState("");
  const [brand, setBrand] = useState<string | null>(null);
  const [savedBarcode, setSavedBarcode] = useState<string | null>(null);
  const [sourceTag, setSourceTag] = useState<string | null>(null);
  const [basis, setBasis] = useState<ServingBasis>("Per100g");
  const [servingSize, setServingSize] = useState("");
  const [kcal, setKcal] = useState("");
  const [protein, setProtein] = useState("");
  const [carb, setCarb] = useState("");
  const [fat, setFat] = useState("");
  const [amount, setAmount] = useState("");
  const [unit, setUnit] = useState<MealAmountUnit>("Grams");
  const [saveFood, setSaveFood] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const lookUp = async () => {
    const c = code.trim();
    if (!/^\d{8,14}$/.test(c)) return setError("A barcode is 8–14 digits.");
    setError(null);
    try {
      const f = await barcode.mutateAsync(c);
      setName(f.name ?? "");
      setBrand(f.brand);
      setSavedBarcode(f.barcode);
      setSourceTag(`${f.source} · ${f.license}`);
      setBasis("Per100g");
      setUnit("Grams");
      setServingSize(f.servingQuantityGrams != null ? String(f.servingQuantityGrams) : "");
      setKcal(f.per100g.energyKcal != null ? String(f.per100g.energyKcal) : "");
      setProtein(f.per100g.protein != null ? String(f.per100g.protein) : "");
      setCarb(f.per100g.carbs != null ? String(f.per100g.carbs) : "");
      setFat(f.per100g.fat != null ? String(f.per100g.fat) : "");
      setSaveFood(true);
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 404
          ? "Not in Open Food Facts — fill it in below and it'll be saved with the barcode."
          : e instanceof ApiError
            ? (e.detail ?? e.title)
            : "Couldn't reach the food database. Try again.",
      );
      setSavedBarcode(c);
      setSaveFood(true);
    }
  };

  const onBasisChange = (b: ServingBasis) => {
    setBasis(b);
    setUnit(b === "PerServing" ? "Serving" : "Grams");
  };

  const applyFood = (f: CustomFood) => {
    setName(f.name);
    setBrand(f.brand);
    setSavedBarcode(f.barcode);
    setSourceTag(null);
    setBasis(f.servingBasis);
    setUnit(f.servingBasis === "PerServing" ? "Serving" : "Grams");
    setServingSize(f.servingSizeGrams != null ? String(f.servingSizeGrams) : "");
    setKcal(String(f.kcal));
    setProtein(String(f.proteinG));
    setCarb(String(f.carbG));
    setFat(String(f.fatG));
    setSaveFood(false);
    setSearch("");
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

    const size = needsServingSize || servingSize.trim() !== "" ? num(servingSize) : null;
    try {
      await add.mutateAsync({
        category,
        name: n,
        servingBasis: basis,
        servingSizeGrams: size,
        perBasisKcal: values.kcal,
        perBasisProteinG: values.protein,
        perBasisCarbG: values.carb,
        perBasisFatG: values.fat,
        amount: values.amount,
        unit,
      });
      if (saveFood) {
        await createFood
          .mutateAsync({
            name: n,
            brand,
            barcode: savedBarcode,
            servingBasis: basis,
            servingSizeGrams: size,
            kcal: values.kcal,
            proteinG: values.protein,
            carbG: values.carb,
            fatG: values.fat,
          })
          .catch(() => {}); // the meal item is already logged; a save failure isn't fatal
      }
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
    <Modal
      title={`Add food · ${category}`}
      label={`Add food to ${category}`}
      onClose={onClose}
      size="md"
      className="max-h-[92vh] overflow-y-auto"
    >
      <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
        Search My Foods
        <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Type to find a saved food" />
      </label>
      {search.trim() !== "" && (results?.length ?? 0) > 0 && (
        <ul className="m-0 flex max-h-40 list-none flex-col gap-1 overflow-y-auto p-0">
          {results!.map((f) => (
            <li key={f.id}>
              <button
                type="button"
                onClick={() => applyFood(f)}
                className="flex w-full items-center justify-between rounded-control border border-border bg-surface-subtle px-3 py-2 text-left text-[13px] hover:border-border-strong"
              >
                <span className="min-w-0 truncate">
                  {f.name}
                  {f.brand ? <span className="ml-1 text-foreground-muted">· {f.brand}</span> : null}
                </span>
                <span className="shrink-0 text-xs text-foreground-muted">
                  {Math.round(f.kcal)} kcal / {f.servingBasis === "Per100g" ? "100g" : "serving"}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="flex items-end gap-2">
        <label className="flex flex-1 flex-col gap-1 text-xs font-semibold text-foreground-muted">
          Barcode
          <Input
            value={code}
            onChange={(e) => setCode(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && lookUp()}
            placeholder="8–14 digits"
            inputMode="numeric"
          />
        </label>
        <Button variant="secondary" size="sm" onClick={lookUp} disabled={barcode.isPending}>
          {barcode.isPending ? "Looking up…" : "Look up"}
        </Button>
      </div>

      <div className="flex flex-col gap-1">
        <span className="flex items-center gap-2 text-xs font-semibold text-foreground-muted">
          Name
          {sourceTag && <Tag tone="neutral">{sourceTag}</Tag>}
        </span>
        <Input
          autoFocus
          aria-label="Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Plain yogurt"
        />
      </div>

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

      <Checkbox
        label="Save to My Foods"
        checked={saveFood}
        onChange={(e) => setSaveFood(e.target.checked)}
      />

    {error && (
      <p className="m-0 text-[13px] text-danger" role="alert">
        {error}
      </p>
    )}

    <div className="flex justify-end gap-2">
      <Button variant="ghost" size="sm" onClick={onClose}>
        Cancel
      </Button>
      <Button variant="primary" size="sm" onClick={submit} disabled={add.isPending}>
        {add.isPending ? "Adding…" : "Add"}
      </Button>
    </div>
    </Modal>
  );
}
