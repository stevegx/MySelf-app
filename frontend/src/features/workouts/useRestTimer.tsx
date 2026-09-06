import { useCallback, useEffect, useRef, useState } from "react";
import { Button } from "../../components/ui";

/**
 * A single in-app rest countdown (docs/02 §7, locked decision #31: in-app sound/vibration
 * only while open, never browser notifications). `start(seconds)` (re)starts it; on reaching
 * zero it beeps + vibrates once and stops. Render `bar` inside the screen.
 */
export function useRestTimer() {
  const [remaining, setRemaining] = useState(0);
  const [total, setTotal] = useState(0);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const stop = useCallback(() => {
    if (intervalRef.current) clearInterval(intervalRef.current);
    intervalRef.current = null;
    setRemaining(0);
    setTotal(0);
  }, []);

  const chime = useCallback(() => {
    try {
      const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      const ctx = new Ctx();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.frequency.value = 880;
      gain.gain.setValueAtTime(0.001, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.2, ctx.currentTime + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.35);
      osc.connect(gain).connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.36);
      osc.onended = () => ctx.close();
    } catch {
      /* audio not available — the visible timer is still the primary signal */
    }
    navigator.vibrate?.(200);
  }, []);

  const start = useCallback(
    (seconds: number) => {
      if (!seconds || seconds <= 0) return;
      if (intervalRef.current) clearInterval(intervalRef.current);
      setTotal(seconds);
      setRemaining(seconds);
      intervalRef.current = setInterval(() => {
        setRemaining((r) => {
          if (r <= 1) {
            if (intervalRef.current) clearInterval(intervalRef.current);
            intervalRef.current = null;
            chime();
            setTotal(0);
            return 0;
          }
          return r - 1;
        });
      }, 1000);
    },
    [chime],
  );

  useEffect(() => () => stop(), [stop]);

  const mmss = `${Math.floor(remaining / 60)}:${String(remaining % 60).padStart(2, "0")}`;
  const pct = total > 0 ? (remaining / total) * 100 : 0;

  const bar =
    remaining > 0 ? (
      <div className="fixed inset-x-0 bottom-0 z-40 border-t border-border bg-surface px-4 py-3 shadow-[0_-4px_16px_rgba(0,0,0,0.08)]">
        <div className="mx-auto flex max-w-[1120px] items-center gap-3">
          <span className="text-sm font-bold tabular-nums">Rest {mmss}</span>
          <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-surface-subtle">
            <div className="h-full rounded-full bg-primary transition-[width] duration-1000 ease-linear" style={{ width: `${pct}%` }} />
          </div>
          <Button variant="ghost" size="sm" onClick={stop}>
            Skip rest
          </Button>
        </div>
      </div>
    ) : null;

  return { start, stop, bar, remaining };
}
