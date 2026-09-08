import type { ReactNode } from "react";
import { cn } from "../../lib/cn";

type Tone = "primary" | "neutral" | "warning" | "success" | "outline";

const TONE: Record<Tone, string> = {
  primary: "bg-primary-soft text-primary-pressed",
  neutral: "bg-surface-subtle text-foreground-muted",
  warning: "bg-warning-soft text-warning",
  success: "bg-success-soft text-success",
  outline: "border border-primary text-primary",
};

export function Tag({ tone = "neutral", children }: { tone?: Tone; children: ReactNode }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2.5 py-[3px] text-[12px] tracking-[0.02em]",
        TONE[tone],
      )}
    >
      {children}
    </span>
  );
}
