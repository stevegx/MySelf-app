import { useState } from "react";
import { Button, Input } from "../../components/ui";
import { ApiError } from "../../lib/api";
import { useLogWeight } from "./api";

const today = () => new Date().toISOString().slice(0, 10);

/**
 * A small centred dialog for logging one body-weight reading. Mount it only while it should
 * be open (`{showing && <LogWeightDialog onClose={…} />}`) so each open starts fresh. Weight
 * is entered in kg; the date defaults to today and can be back-dated.
 */
export function LogWeightDialog({ onClose }: { onClose: () => void }) {
  const log = useLogWeight();
  const [weight, setWeight] = useState("");
  const [date, setDate] = useState(today);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    const kg = Number(weight);
    if (!Number.isFinite(kg) || kg <= 0) {
      setError("Enter your weight in kilograms.");
      return;
    }
    setError(null);
    try {
      await log.mutateAsync({ weightKg: kg, localDate: date });
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? (Object.values(e.errors ?? {})[0]?.[0] ?? e.detail ?? e.title)
          : "Couldn't save that. Try again.",
      );
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Log your weight"
      onKeyDown={(e) => e.key === "Escape" && onClose()}
    >
      <div className="w-full max-w-sm rounded-card border border-border bg-surface p-4 shadow-xl">
        <h3 className="m-0 mb-3 text-base font-bold">Log your weight</h3>

        <label className="mb-2 flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
          Weight (kg)
          <Input
            type="number"
            inputMode="decimal"
            step="0.1"
            autoFocus
            value={weight}
            onChange={(e) => setWeight(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && submit()}
          />
        </label>

        <label className="mb-4 flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
          Date
          <Input type="date" max={today()} value={date} onChange={(e) => setDate(e.target.value)} />
        </label>

        {error && <p className="m-0 mb-3 text-[13px] text-danger">{error}</p>}

        <div className="flex justify-end gap-2">
          <Button variant="ghost" size="sm" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" size="sm" onClick={submit} disabled={log.isPending}>
            {log.isPending ? "Saving…" : "Save"}
          </Button>
        </div>
      </div>
    </div>
  );
}
