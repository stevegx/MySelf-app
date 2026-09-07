import { useState } from "react";
import { useNavigate } from "react-router";
import { ScanLine } from "lucide-react";
import { Button, Card, CardTitle, Field, Input, Segmented, Tag } from "../../components/ui";

type Unit = "g" | "ml" | "serving";

const MACRO_TILES = [
  { value: "96", label: "kcal" },
  { value: "15g", label: "protein" },
  { value: "6g", label: "carbs" },
  { value: "0.6g", label: "fat" },
];

export function AddFoodScreen() {
  const navigate = useNavigate();
  const [unit, setUnit] = useState<Unit>("g");

  return (
    <>
      <h2 className="mb-[18px]">Add food</h2>

      <div className="grid grid-cols-[1fr_1.2fr] items-start gap-5 max-nav:grid-cols-1">
        <Card className="items-center px-5 py-8 text-center">
          <div className="relative flex aspect-[4/3] w-full items-center justify-center rounded-control bg-indigo-950">
            <div className="h-[52%] w-[70%] rounded-xl border-2 border-primary" />
            <ScanLine size={34} className="absolute text-primary" aria-hidden />
          </div>
          <p className="mt-3 text-sm text-foreground-muted">
            Point your camera at a barcode
          </p>
          <Button variant="secondary" block onClick={() => navigate("/nutrition")}>
            Cancel
          </Button>
        </Card>

        <Card>
          <div className="flex items-start justify-between">
            <div>
              <CardTitle>Greek Yogurt, Plain</CardTitle>
              <div className="text-xs text-foreground-muted">150 g serving · per 100 g</div>
            </div>
            <Tag tone="neutral">Open Food Facts</Tag>
          </div>
          <div className="text-[12px] text-foreground-muted">Fetched just now</div>
          <hr className="my-2 border-0 border-t border-border" />

          <div className="flex flex-wrap items-end gap-2.5">
            <Field label="Quantity" className="w-[110px]">
              <Input defaultValue="150" inputMode="decimal" />
            </Field>
            <Segmented<Unit>
              aria-label="Unit"
              value={unit}
              onChange={setUnit}
              options={[
                { value: "g", label: "g" },
                { value: "ml", label: "ml" },
                { value: "serving", label: "serving" },
              ]}
            />
          </div>

          <div className="mt-3.5 grid grid-cols-4 gap-2">
            {MACRO_TILES.map((tile) => (
              <div
                key={tile.label}
                className="rounded-control bg-surface-subtle p-2.5"
              >
                <div className="text-base font-bold">{tile.value}</div>
                <div className="text-[12px] text-foreground-muted">{tile.label}</div>
              </div>
            ))}
          </div>

          <div className="mt-3.5 flex gap-2">
            <Button variant="secondary" className="flex-1">
              Save to My Foods
            </Button>
            <Button variant="primary" className="flex-1">
              Add to log
            </Button>
          </div>
        </Card>
      </div>
    </>
  );
}
