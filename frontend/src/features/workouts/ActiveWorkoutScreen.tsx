import { Check, Clock } from "lucide-react";
import {
  Button,
  Card,
  CardKicker,
  CardTitle,
  Field,
  Input,
  PageHeader,
  Tag,
} from "../../components/ui";

export function ActiveWorkoutScreen() {
  return (
    <>
      <PageHeader
        title="Legs #1"
        subtitle="Set 2 of 3 · Back Squat"
        actions={
          <div className="flex items-center gap-2.5">
            <Tag tone="primary">
              <Clock size={12} aria-hidden />
              18:42
            </Tag>
            <Button variant="secondary">Finish workout</Button>
          </div>
        }
      />

      <Card className="mb-4">
        <div className="flex items-center justify-between">
          <CardTitle>Back Squat</CardTitle>
          <Tag tone="neutral">Working set</Tag>
        </div>
        <div className="flex gap-6 text-[13px] text-foreground-muted">
          <span>Target: 8–10 reps</span>
          <span>Previous: 90 kg × 8</span>
        </div>
        <div className="mt-1.5 flex flex-wrap items-end gap-2.5">
          <Field label="Weight (kg)" className="w-[120px]">
            <Input defaultValue="92.5" inputMode="decimal" />
          </Field>
          <Field label="Reps" className="w-[100px]">
            <Input defaultValue="8" inputMode="numeric" />
          </Field>
          <Field label="RIR" className="w-[100px]">
            <Input placeholder="2" inputMode="numeric" />
          </Field>
          <Button variant="secondary">Copy previous</Button>
          <Button variant="primary">
            <Check size={15} aria-hidden />
            Complete set
          </Button>
        </div>
      </Card>

      <CardKicker>Up next — superset B</CardKicker>
      <Card className="mt-2">
        <div className="flex items-center justify-between">
          <CardTitle>Round 1 of 3</CardTitle>
          <Tag tone="warning">Rest starts after round</Tag>
        </div>
        <div className="mt-1 flex flex-col gap-2">
          <div className="flex items-center gap-3 rounded-control border border-border px-3 py-2.5">
            <Tag tone="outline">B1</Tag>
            <div className="flex-1 text-sm">Walking Lunge — 12 reps/side</div>
            <Button variant="secondary" size="sm">
              Complete
            </Button>
          </div>
          <div className="flex items-center gap-3 rounded-control border border-border px-3 py-2.5 opacity-55">
            <Tag tone="outline">B2</Tag>
            <div className="flex-1 text-sm">Leg Extension — 15 reps</div>
            <Button variant="secondary" size="sm">
              Complete
            </Button>
          </div>
        </div>
      </Card>
    </>
  );
}
