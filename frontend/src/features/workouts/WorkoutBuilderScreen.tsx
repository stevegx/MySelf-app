import { GripVertical, MoreHorizontal, Plus } from "lucide-react";
import { Button, Card, CardKicker, CardTitle, PageHeader } from "../../components/ui";
import { cn } from "../../lib/cn";

const variantRow =
  "flex w-full items-center gap-2 rounded-control px-2.5 py-2 text-left text-[13px] cursor-pointer";

export function WorkoutBuilderScreen() {
  return (
    <>
      <PageHeader
        title="New Program"
        subtitle="Draft — not yet active"
        actions={
          <>
            <Button variant="secondary">Preview</Button>
            <Button variant="primary" disabled>
              Activate program
            </Button>
          </>
        }
      />

      <div className="grid grid-cols-[280px_1fr] items-start gap-5 max-nav:grid-cols-1">
        <Card className="gap-1">
          <CardKicker>Workout groups</CardKicker>
          <div className="mt-1 flex flex-col gap-0.5">
            <div className="px-1 pb-0.5 pt-1.5 text-xs uppercase tracking-[0.06em] text-primary-pressed">
              Legs
            </div>
            <button
              className={cn(variantRow, "bg-primary-soft font-bold text-primary-pressed")}
            >
              <GripVertical size={14} className="text-foreground-muted" aria-hidden />
              Legs #1
            </button>
            <button className={cn(variantRow, "hover:bg-surface-subtle")}>
              <GripVertical size={14} className="text-foreground-muted" aria-hidden />
              Legs #2
            </button>
            <Button variant="ghost" className="mt-0.5 justify-start">
              <Plus size={14} aria-hidden />
              Add variant
            </Button>
          </div>
          <hr className="my-4 border-0 border-t border-border" />
          <Button variant="ghost" className="justify-start">
            <Plus size={14} aria-hidden />
            Add workout group
          </Button>
        </Card>

        <Card>
          <div className="flex items-center justify-between">
            <CardTitle>Legs #1</CardTitle>
            <Button variant="secondary" iconOnly aria-label="Variant options">
              <MoreHorizontal size={16} aria-hidden />
            </Button>
          </div>
          <div className="mt-1.5 flex flex-col gap-2">
            <div className="flex items-center gap-3 rounded-control border border-border px-3.5 py-3">
              <GripVertical size={16} className="text-foreground-muted" aria-hidden />
              <div className="flex-1">
                <div className="text-sm font-bold">Back Squat</div>
                <div className="text-xs text-foreground-muted">
                  3 sets · 8–10 reps · rest 90s
                </div>
              </div>
              <Button variant="ghost" size="sm">
                More options
              </Button>
            </div>
            <div className="flex items-center justify-center gap-2 rounded-control border-[1.5px] border-dashed border-border-strong p-[22px] text-primary-pressed">
              <Plus size={16} aria-hidden />
              <span className="text-sm">Add exercise</span>
            </div>
          </div>
        </Card>
      </div>
    </>
  );
}
