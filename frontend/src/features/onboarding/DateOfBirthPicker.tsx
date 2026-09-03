import { useState } from "react";
import { cn } from "../../lib/cn";

const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

const CURRENT_YEAR = new Date().getFullYear();
// Backend accepts up to age 120; there's no lower bound here (the wizard handles under-18s).
const YEARS = Array.from({ length: 121 }, (_, i) => CURRENT_YEAR - i);

function daysInMonth(year: number, month1: number): number {
  if (!year || !month1) return 31;
  return new Date(year, month1, 0).getDate();
}

type Parts = { y: number; m: number; d: number };

function splitIso(value: string): Parts {
  const [y, m, d] = (value || "").split("-").map(Number);
  return { y: y || 0, m: m || 0, d: d || 0 };
}

/**
 * Day / Month / Year dropdowns for a birth date — far quicker than paging a native
 * `<input type="date">` calendar back decades. Emits the same `"YYYY-MM-DD"` string the rest
 * of the wizard already expects (`""` until all three parts are chosen). Keeps its own
 * partial state so choosing the year, then month, then day works — an ISO string can't
 * represent "year picked, month not yet".
 */
export function DateOfBirthPicker({
  value,
  onChange,
  invalid,
}: {
  value: string;
  onChange: (isoDate: string) => void;
  invalid?: boolean;
}) {
  const [parts, setParts] = useState<Parts>(() => splitIso(value));

  const update = (patch: Partial<Parts>) => {
    const next = { ...parts, ...patch };
    setParts(next);
    if (next.y && next.m && next.d) {
      const d = Math.min(next.d, daysInMonth(next.y, next.m));
      onChange(`${next.y}-${String(next.m).padStart(2, "0")}-${String(d).padStart(2, "0")}`);
    } else {
      onChange("");
    }
  };

  const selectClass = cn(
    "min-h-11 rounded-control border bg-surface-subtle px-2.5 py-2 text-sm text-foreground",
    "hover:border-border-strong focus-visible:border-primary focus-visible:outline-none",
    invalid ? "border-danger" : "border-border",
  );

  return (
    <div className="flex gap-2" role="group" aria-label="Date of birth" aria-invalid={invalid || undefined}>
      <select
        className={cn(selectClass, "flex-1")}
        aria-label="Day"
        value={parts.d || ""}
        onChange={(e) => update({ d: Number(e.target.value) })}
      >
        <option value="">Day</option>
        {Array.from({ length: daysInMonth(parts.y, parts.m) }, (_, i) => i + 1).map((d) => (
          <option key={d} value={d}>{d}</option>
        ))}
      </select>
      <select
        className={cn(selectClass, "flex-[1.4]")}
        aria-label="Month"
        value={parts.m || ""}
        onChange={(e) => update({ m: Number(e.target.value) })}
      >
        <option value="">Month</option>
        {MONTHS.map((name, i) => (
          <option key={name} value={i + 1}>{name}</option>
        ))}
      </select>
      <select
        className={cn(selectClass, "flex-1")}
        aria-label="Year"
        value={parts.y || ""}
        onChange={(e) => update({ y: Number(e.target.value) })}
      >
        <option value="">Year</option>
        {YEARS.map((y) => (
          <option key={y} value={y}>{y}</option>
        ))}
      </select>
    </div>
  );
}
