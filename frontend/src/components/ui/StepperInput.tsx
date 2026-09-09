import { Minus, Plus } from "lucide-react";
import { cn } from "../../lib/cn";

/**
 * A number field with −/+ steppers on either side. The middle stays a real editable
 * `<input>` so a value can still be typed; the buttons nudge it by `step`. Built for
 * one-handed gym logging (big tap targets, no reliance on the OS number keyboard).
 */
export function StepperInput({
  value,
  onChange,
  step = 1,
  min = 0,
  label,
  ariaLabel,
  className,
  inputRef,
}: {
  value: string;
  onChange: (next: string) => void;
  step?: number;
  min?: number;
  label?: string;
  ariaLabel: string;
  className?: string;
  inputRef?: React.Ref<HTMLInputElement>;
}) {
  const nudge = (dir: 1 | -1) => {
    const n = value.trim() === "" ? 0 : Number(value);
    const base = Number.isFinite(n) ? n : 0;
    const next = Math.max(min, Math.round((base + dir * step) * 100) / 100);
    onChange(String(next));
  };

  return (
    <label className={cn("flex flex-col gap-1", className)}>
      {label && <span className="text-[12px] text-foreground-muted">{label}</span>}
      <span className="flex items-stretch">
        <button
          type="button"
          aria-label={`Decrease ${ariaLabel}`}
          onClick={() => nudge(-1)}
          className="flex w-9 items-center justify-center rounded-l-control border border-r-0 border-border-strong bg-surface-subtle text-foreground hover:bg-surface-strong"
        >
          <Minus size={15} aria-hidden />
        </button>
        <input
          ref={inputRef}
          type="number"
          inputMode="decimal"
          min={min}
          aria-label={ariaLabel}
          value={value}
          onKeyDown={(e) => {
            if (e.key === "-" || e.key === "+" || e.key === "e" || e.key === "E") e.preventDefault();
          }}
          onChange={(e) => {
            const v = e.target.value;
            const n = Number(v);
            onChange(v !== "" && Number.isFinite(n) && n < min ? String(min) : v);
          }}
          className="w-16 min-w-0 border-y border-border-strong bg-surface px-1 text-center text-[15px] font-semibold tabular-nums outline-none focus-visible:border-ring"
        />
        <button
          type="button"
          aria-label={`Increase ${ariaLabel}`}
          onClick={() => nudge(1)}
          className="flex w-9 items-center justify-center rounded-r-control border border-l-0 border-border-strong bg-surface-subtle text-foreground hover:bg-surface-strong"
        >
          <Plus size={15} aria-hidden />
        </button>
      </span>
    </label>
  );
}
