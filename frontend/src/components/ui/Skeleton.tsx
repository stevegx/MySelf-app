import { cn } from "../../lib/cn";

/**
 * A shimmering placeholder block. Use while data is loading instead of a "Loading…"
 * line or an empty screen. Give it a width/height via className (defaults to a text line).
 */
export function Skeleton({ className }: { className?: string }) {
  return (
    <span
      aria-hidden
      className={cn("block h-4 w-full animate-pulse rounded bg-surface-strong", className)}
    />
  );
}

/** N stacked line skeletons — a quick body placeholder. */
export function SkeletonText({ lines = 3, className }: { lines?: number; className?: string }) {
  return (
    <span className={cn("flex flex-col gap-2", className)} aria-hidden>
      {Array.from({ length: lines }, (_, i) => (
        <Skeleton key={i} className={i === lines - 1 ? "w-2/3" : "w-full"} />
      ))}
    </span>
  );
}
