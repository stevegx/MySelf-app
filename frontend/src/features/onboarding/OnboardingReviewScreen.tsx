import { Button } from "../../components/ui";

const ESTIMATE_ROWS = [
  { label: "Estimated BMR", value: "1,680 kcal/day" },
  { label: "Activity estimate", value: "× 1.55" },
  { label: "Estimated maintenance", value: "2,604 kcal/day" },
  { label: "Goal adjustment", value: "−500 kcal/day" },
];

export function OnboardingReviewScreen() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-5 py-12">
      <div className="flex w-[min(520px,100%)] flex-col gap-5 rounded-card border border-border bg-surface p-9 pb-7 shadow-md">
        <div className="flex gap-1.5">
          {[0, 1, 2, 3].map((i) => (
            <div key={i} className="h-[5px] flex-1 rounded-full bg-primary" />
          ))}
        </div>

        <div>
          <div className="mb-1.5 text-[11px] uppercase tracking-[0.08em] text-foreground-muted">
            Step 4 of 4
          </div>
          <h2 className="mb-1">Review your estimate</h2>
          <p className="m-0 text-sm text-foreground-muted">
            Based on what you told us. You can always adjust this later.
          </p>
        </div>

        <div className="flex flex-col overflow-hidden rounded-control border border-border">
          {ESTIMATE_ROWS.map((row) => (
            <div
              key={row.label}
              className="flex justify-between border-b border-border px-4 py-3"
            >
              <span className="text-[13px] text-foreground-muted">{row.label}</span>
              <span className="text-sm">{row.value}</span>
            </div>
          ))}
          <div className="flex justify-between bg-primary-soft px-4 py-3.5">
            <span className="text-sm font-bold">Suggested daily target</span>
            <span className="text-lg font-bold text-primary-pressed">2,104 kcal</span>
          </div>
        </div>

        <p className="m-0 text-xs leading-relaxed text-foreground-muted">
          This calorie target is an estimate based on the information you provided. MySelf is a
          tracking tool, not medical advice, and does not replace a doctor, registered dietitian, or
          qualified trainer. Your actual needs may differ.
        </p>

        <div className="flex flex-col gap-2">
          <Button variant="primary" block>
            Use this estimate
          </Button>
          <div className="flex gap-2">
            <Button variant="secondary" className="flex-1">
              Adjust target
            </Button>
            <Button variant="secondary" className="flex-1">
              Set manually
            </Button>
          </div>
          <div className="mt-1 flex justify-between">
            <Button variant="ghost">← Back</Button>
            <Button variant="ghost">Skip nutrition setup</Button>
          </div>
        </div>
      </div>
    </div>
  );
}
