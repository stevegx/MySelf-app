import { useState } from "react";
import { Button, Card, CardKicker, PageHeader, Segmented } from "../../components/ui";

type ProgressTab = "strength" | "weight" | "measurements";

export function ProgressScreen() {
  const [tab, setTab] = useState<ProgressTab>("strength");

  return (
    <>
      <PageHeader
        title="Progress"
        actions={
          <Segmented<ProgressTab>
            aria-label="Progress view"
            value={tab}
            onChange={setTab}
            options={[
              { value: "strength", label: "Strength" },
              { value: "weight", label: "Body weight" },
              { value: "measurements", label: "Measurements" },
            ]}
          />
        }
      />

      <Card className="mb-4">
        <CardKicker>Body weight trend</CardKicker>
        <div className="my-1.5 flex h-[140px] items-center justify-center rounded-control border-[1.5px] border-dashed border-border-strong">
          <span className="text-[13px] text-foreground-muted">
            Log your weight to see your trend
          </span>
        </div>
        <Button variant="secondary" className="self-start">
          Log your weight
        </Button>
      </Card>

      <Card>
        <CardKicker>Personal records</CardKicker>
        <p className="m-0 flex-1 text-[13px] text-foreground-muted">
          No personal records yet — complete a workout to start tracking heaviest weight, best e1RM
          and top volume per exercise.
        </p>
      </Card>
    </>
  );
}
