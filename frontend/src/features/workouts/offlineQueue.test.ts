import { clearSyncError, enqueue, flushQueue, getSyncState } from "./offlineQueue";

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
}

describe("offlineQueue", () => {
  afterEach(() => {
    localStorage.clear();
    clearSyncError();
  });

  it("holds a mutation and replays it when the network is back", async () => {
    enqueue("/api/v1/workout-sessions/s1/set-logs", { setLogId: "x", reps: 8 });
    enqueue("/api/v1/workout-sessions/s1/skip-set", { setLogId: "y" });
    expect(getSyncState().pending).toBe(2);

    // First attempt: offline (fetch rejects) -> nothing drains.
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    await flushQueue("token");
    expect(getSyncState().pending).toBe(2);

    // Reconnect: both succeed and the queue empties.
    vi.stubGlobal("fetch", vi.fn().mockImplementation(() => Promise.resolve(jsonResponse({ ok: true }))));
    await flushQueue("token");
    expect(getSyncState().pending).toBe(0);
    expect(getSyncState().lastError).toBeNull();

    vi.unstubAllGlobals();
  });

  it("drops a mutation the server rejects and records a sync error", async () => {
    enqueue("/api/v1/workout-sessions/s1/set-logs", { setLogId: "gone" });

    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(() =>
        Promise.resolve(new Response(JSON.stringify({ title: "Not found" }), { status: 404, headers: { "content-type": "application/json" } })),
      ),
    );
    await flushQueue("token");

    expect(getSyncState().pending).toBe(0);
    expect(getSyncState().lastError).toBeTruthy();
    vi.unstubAllGlobals();
  });
});
