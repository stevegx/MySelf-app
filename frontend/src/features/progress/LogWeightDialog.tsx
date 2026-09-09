import { useState } from "react";
import { Button, Input, Modal } from "../../components/ui";
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
    <Modal title="Log your weight" onClose={onClose}>
      <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
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

      <label className="flex flex-col gap-1 text-xs font-semibold text-foreground-muted">
        Date
        <Input type="date" max={today()} value={date} onChange={(e) => setDate(e.target.value)} />
      </label>

      {error && (
        <p className="m-0 text-sm text-danger" role="alert">
          {error}
        </p>
      )}

      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={onClose}>
          Cancel
        </Button>
        <Button variant="primary" size="sm" onClick={submit} disabled={log.isPending}>
          {log.isPending ? "Saving…" : "Save"}
        </Button>
      </div>
    </Modal>
  );
}
