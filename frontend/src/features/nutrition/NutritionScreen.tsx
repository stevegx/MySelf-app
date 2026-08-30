import { useNavigate } from "react-router";
import { Plus, ScanLine } from "lucide-react";
import { Button, Card, CardTitle, PageHeader, Ring } from "../../components/ui";

const MACROS = [
  { label: "Protein", detail: "0/112g" },
  { label: "Carbs", detail: "0/288g" },
  { label: "Fat", detail: "0/56g" },
];

const MEALS = ["Breakfast", "Lunch", "Dinner", "Snacks"];

export function NutritionScreen() {
  const navigate = useNavigate();

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
        <Ring size={104} stroke={9} ariaLabel="Calories: 0 of 2,104 kcal" />
        <div>
          <div className="text-2xl font-bold">
            0 <span className="text-[13px] font-normal text-foreground-muted">/ 2,104 kcal</span>
          </div>
          <div className="text-xs text-foreground-muted">2,104 remaining</div>
        </div>
        <div className="ml-auto flex gap-4">
          {MACROS.map((macro) => (
            <div key={macro.label} className="text-center">
              <Ring size={46} stroke={5} ariaLabel={`${macro.label}: ${macro.detail}`} />
              <div className="mt-1 text-[11px]">{macro.label}</div>
              <div className="text-[10px] text-foreground-muted">{macro.detail}</div>
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
