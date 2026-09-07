import { useNavigate } from "react-router";
import { Plus, ScanLine } from "lucide-react";
import { Button, Card, CardTitle, PageHeader, Ring } from "../../components/ui";
import { formatTarget, useNutritionTargets } from "./useNutritionTargets";

const MEALS = ["Breakfast", "Lunch", "Dinner", "Snacks"];

export function NutritionScreen() {
  const navigate = useNavigate();
  const targets = useNutritionTargets();
  const macros = [
    { label: "Protein", target: targets.proteinGrams },
    { label: "Carbs", target: targets.carbGrams },
    { label: "Fat", target: targets.fatGrams },
  ];

  return (
    <>
      <PageHeader
        title="Nutrition today"
        subtitle="Friday, August 30"
        actions={
          <>
            <Button variant="secondary" onClick={() => navigate("/nutrition/add")}>
              <ScanLine size={15} aria-hidden />
              Scan barcode
            </Button>
            <Button variant="primary" onClick={() => navigate("/nutrition/add")}>
              <Plus size={15} aria-hidden />
              Add food
            </Button>
          </>
        }
      />

      <Card className="mb-[18px] flex-row flex-wrap items-center gap-7">
        <Ring
          size={104}
          stroke={9}
          ariaLabel={
            targets.hasTarget
              ? `Calories: 0 of ${formatTarget(targets.calorieTarget)} kcal`
              : "Calories logged today: 0 kcal"
          }
        />
        <div>
          <div className="text-2xl font-bold">
            0{" "}
            <span className="text-sm font-normal text-foreground-muted">
              {targets.hasTarget ? `/ ${formatTarget(targets.calorieTarget)} kcal` : "kcal"}
            </span>
          </div>
          <div className="text-xs text-foreground-muted">
            {targets.hasTarget
              ? `${formatTarget(targets.calorieTarget)} remaining`
              : "No calorie target — set one in Settings"}
          </div>
        </div>
        <div className="ml-auto flex gap-4">
          {macros.map((macro) => (
            <div key={macro.label} className="text-center">
              <Ring
                size={46}
                stroke={5}
                ariaLabel={`${macro.label}: 0 of ${macro.target == null ? "no" : formatTarget(macro.target)} g`}
              />
              <div className="mt-1 text-[12px]">{macro.label}</div>
              <div className="text-[11px] text-foreground-muted">
                {macro.target == null ? "—" : `0/${formatTarget(macro.target)}g`}
              </div>
            </div>
          ))}
        </div>
      </Card>

      <div className="flex flex-col gap-3">
        {MEALS.map((meal) => (
          <Card key={meal}>
            <div className="flex items-center justify-between">
              <CardTitle>{meal}</CardTitle>
              <span className="text-xs text-foreground-muted">0 kcal</span>
            </div>
            <Button variant="ghost" size="sm" className="self-start">
              <Plus size={14} aria-hidden />
              Add food to {meal.toLowerCase()}
            </Button>
          </Card>
        ))}
      </div>
    </>
  );
}
