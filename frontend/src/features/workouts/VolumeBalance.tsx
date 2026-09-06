import { useState } from "react";
import { TriangleAlert } from "lucide-react";
import { Card, Segmented } from "../../components/ui";
import { cn } from "../../lib/cn";
import { foldMusclesToGroups } from "./muscleGroups";
import type { RecentVolume } from "./api";

/**
 * A "is my training balanced" read for the Train home: completed sets over the last ~14
 * days, one bar per source day or per coarse muscle group (a toggle). Bars are normalised
 * to the biggest slice, so it's a relative-volume comparison, not a part-of-whole stack —
 * identity comes from the row label, so no categorical colour is needed.
 *
 * Verdict is a "coverage" call: Balanced unless a slice sat at zero while others were busy.
 */
export function VolumeBalance({ recent }: { recent: RecentVolume }) {
  const [view, setView] = useState<"day" | "muscle">("day");

  const slices =
    view === "day"
      ? recent.byDay.map((s) => ({ label: s.label, sets: s.sets }))
      : foldMusclesToGroups(recent.byMuscle).map((g) => ({ label: g.label, sets: g.sets }));

  const total = slices.reduce((n, s) => n + s.sets, 0);
  const max = Math.max(1, ...slices.map((s) => s.sets));
  const neglected = slices.filter((s) => s.sets === 0);

  const verdict: { text: string; tone: "success" | "warning" | "muted" } =
    recent.sessions === 0
      ? { text: "Nothing logged in the last 14 days", tone: "muted" }
      : max < 6
        ? { text: "Building a baseline", tone: "muted" }
        : neglected.length === 0
          ? { text: "Balanced", tone: "success" }
          : neglected.length === 1
            ? { text: `${neglected[0].label} light`, tone: "warning" }
            : { text: `${neglected[0].label} & ${neglected[1].label} light`, tone: "warning" };

  const toneClass = {
    success: "bg-success-soft text-success",
    warning: "bg-warning-soft text-warning",
    muted: "bg-surface-subtle text-foreground-muted",
  }[verdict.tone];

  return (
    <Card className="gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="text-[13px] text-foreground-muted">
          <span className="font-semibold text-foreground">Volume balance</span>{" "}
          <span>
            · {recent.sessions} {recent.sessions === 1 ? "session" : "sessions"} · {total}{" "}
            {view === "day" ? (total === 1 ? "set" : "sets") : "muscle-sets"} · last 14 days
          </span>
        </div>
        <span className={cn("rounded-pill px-2 py-0.5 text-xs font-semibold", toneClass)}>{verdict.text}</span>
      </div>

      <Segmented
        aria-label="Split volume by"
        options={[
          { value: "day", label: "By day" },
          { value: "muscle", label: "By muscle" },
        ]}
        value={view}
        onChange={setView}
      />

      {recent.sessions > 0 && (
        <div className="flex flex-col gap-1.5">
          {slices.map((s) => {
            const dim = s.sets === 0;
            return (
              <div key={s.label} className="flex items-center gap-2 text-[12px]">
                <span
                  className={cn(
                    "flex w-20 shrink-0 items-center gap-1 truncate",
                    dim ? "text-warning" : "text-foreground-muted",
                  )}
                >
                  {dim && <TriangleAlert size={11} aria-hidden />}
                  {s.label}
                </span>
                <span className="h-2 flex-1 overflow-hidden rounded-full bg-viz-track">
                  {s.sets > 0 && (
                    <span
                      className="block h-full rounded-full bg-primary"
                      style={{ width: `${Math.max(3, (s.sets / max) * 100)}%` }}
                    />
                  )}
                </span>
                <span className="w-6 shrink-0 text-right tabular-nums text-foreground-muted">{s.sets}</span>
              </div>
            );
          })}
        </div>
      )}
    </Card>
  );
}
