import { ApiError, apiFetch } from "../../lib/api";

/**
 * A tiny localStorage-backed retry queue for active-workout mutations (docs/02 "offline
 * local autosave"). Set logging already persists to the server on every change; this keeps
 * those changes safe when the network drops — they replay on reconnect instead of being
 * lost. Full offline browsing and the server/local conflict-recovery flow are out of MVP.
 */

const KEY = "myself.workoutQueue";

export type QueuedMutation = { id: string; url: string; body: unknown; queuedAt: number };
type SyncState = { pending: number; lastError: string | null; flushing: boolean };

let state: SyncState = { pending: readQueue().length, lastError: null, flushing: false };
const listeners = new Set<() => void>();

function readQueue(): QueuedMutation[] {
  try {
    return JSON.parse(localStorage.getItem(KEY) ?? "[]") as QueuedMutation[];
  } catch {
    return [];
  }
}

function writeQueue(items: QueuedMutation[]) {
  try {
    localStorage.setItem(KEY, JSON.stringify(items));
  } catch {
    /* private mode / quota — the in-memory count still drives the indicator */
  }
  emit({ pending: items.length });
}

function emit(patch: Partial<SyncState>) {
  state = { ...state, ...patch };
  listeners.forEach((l) => l());
}

export function subscribeSync(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function getSyncState(): SyncState {
  return state;
}

/** Add a failed mutation to the queue. */
export function enqueue(url: string, body: unknown) {
  const items = readQueue();
  items.push({ id: crypto.randomUUID(), url, body, queuedAt: Date.now() });
  writeQueue(items);
}

/**
 * Replay queued mutations oldest-first. Stops on the first network error (stays offline);
 * drops a mutation that fails with a 4xx (its target set is gone / the session moved on)
 * and records it as a sync error.
 */
export async function flushQueue(accessToken: string | undefined): Promise<void> {
  if (state.flushing) return;
  let items = readQueue();
  if (items.length === 0) return;

  emit({ flushing: true });
  try {
    for (const item of items) {
      try {
        await apiFetch(item.url, { method: "POST", body: item.body, accessToken });
        items = items.filter((q) => q.id !== item.id);
        writeQueue(items);
      } catch (e) {
        if (e instanceof ApiError) {
          // A real rejection (404/409/400) — drop it, it can't succeed on retry.
          items = items.filter((q) => q.id !== item.id);
          writeQueue(items);
          emit({ lastError: "Some offline changes could not be synced." });
        } else {
          return; // still offline — try again later
        }
      }
    }
    if (readQueue().length === 0 && !state.lastError) {
      emit({ lastError: null });
    }
  } finally {
    emit({ flushing: false });
  }
}

export function clearSyncError() {
  emit({ lastError: null });
}
