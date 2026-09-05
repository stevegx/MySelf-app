import { useEffect, useSyncExternalStore } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Tag } from "../../components/ui";
import { useAuth } from "../auth/auth";
import { clearSyncError, flushQueue, getSyncState, subscribeSync } from "./offlineQueue";

function useOnline() {
  return useSyncExternalStore(
    (cb) => {
      window.addEventListener("online", cb);
      window.addEventListener("offline", cb);
      return () => {
        window.removeEventListener("online", cb);
        window.removeEventListener("offline", cb);
      };
    },
    () => navigator.onLine,
    () => true,
  );
}

/**
 * The `Saving locally / Syncing / Synced / Sync error` indicator (docs/02). Also owns the
 * retry loop: flush the queue when the connection returns and every 15s while items remain.
 */
export function SyncStatus() {
  const online = useOnline();
  const sync = useSyncExternalStore(subscribeSync, getSyncState, getSyncState);
  const accessToken = useAuth().session?.accessToken;
  const qc = useQueryClient();

  useEffect(() => {
    if (!online) return;
    let cancelled = false;
    const run = async () => {
      await flushQueue(accessToken);
      if (!cancelled) qc.invalidateQueries({ queryKey: ["workout-session"] });
    };
    run();
    const t = setInterval(run, 15_000);
    return () => {
      cancelled = true;
      clearInterval(t);
    };
  }, [online, accessToken, qc]);

  if (sync.lastError) {
    return (
      <Tag tone="warning">
        <button type="button" onClick={clearSyncError} className="underline">
          Sync error — dismiss
        </button>
      </Tag>
    );
  }
  if (!online && sync.pending > 0) {
    return <Tag tone="warning">Offline — {sync.pending} change{sync.pending > 1 ? "s" : ""} queued</Tag>;
  }
  if (!online) {
    return <Tag tone="neutral">Offline</Tag>;
  }
  if (sync.pending > 0 || sync.flushing) {
    return <Tag tone="neutral">Syncing…</Tag>;
  }
  return <Tag tone="success">Synced</Tag>;
}
