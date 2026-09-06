# Phase 3 — workout execution (slices 4–8)

Written 2026-09-05. Continues from
[the sessions + Day restructure write-up](./2026-09-04-phase3-sessions-and-day-restructure.md)
(slices 1–3: flat `WorkoutDay`, start-a-session, log/skip/complete/discard, finish summary,
history list, `Copy previous set`).

Audience: comfortable with React/TypeScript, newer to .NET/EF Core.

---

## Slice 4 — PRs, exercise history, strength trend

### The formulas (docs/02 §7), as pure Domain code

- `StrengthMath.EstimatedOneRepMax(weight, reps)` — Epley: `weight × (1 + reps/30)`.
- `StrengthMath.SetVolume(weight, reps)` — `weight × reps`.
- `PersonalRecordDetector.Detect(...)` — given the completed weight-and-reps lifts for one
  exercise this session **and the user's stored PRs**, returns the new records. Four types:
  heaviest weight, best e1RM, most reps at a given weight (compared *per weight*), best
  single-session volume. A result only counts if it is **strictly greater** than the prior
  best — "matched previous best" is not a PR.

All three are pure static classes with unit tests — same style as `CalorieEstimator`.

### Where it runs

`POST /workout-sessions/{id}/complete` now, in the same transaction that flips the status:

```csharp
var newPrs = await DetectPersonalRecordsAsync(db, userId, session, now, ct);
db.PersonalRecords.AddRange(newPrs);
await db.SaveChangesAsync(ct);
return Results.Ok(ToDetail(session, newPrs.Select(ToPrDetail).ToList()));
```

PRs are computed **once**, at finish (docs/02: "finish calculates summary and PRs"). The
`complete` response carries `newPersonalRecords`; every other endpoint returns `[]` there.

### Reading it back

- `GET /exercises/{id}/history?dayId=` → current PRs + one row per completed session
  (top set, its e1RM, volume, completed-set count), newest first. The active screen uses the
  latest row to show **"Previous: 100 kg × 8"** under an exercise.
- `GET /analytics/strength?exerciseId=&range=` → an e1RM + volume time series for charting.
  `range` is `30d | 90d | 1y | all`.

Progress screen "Strength" tab: pick an exercise → PR tags, a recent-sessions table, and a
**hand-rolled inline-SVG sparkline** of e1RM (the project has no chart library yet).

### Naming gotcha

The Domain helper is `SessionSummaryCalculator`, not `SessionSummary` — the API contract
record is `SessionSummary` and they would collide in the endpoint file. Same reason the PR
math lives in `StrengthMath` / `PersonalRecordDetector`, not on `PersonalRecord`.

---

## Slice 5 — add / replace / remove exercises mid-session

Four endpoints under `/workout-sessions/{id}`:

| Endpoint | Notes |
|---|---|
| `POST /exercises` `{exerciseId, sets?}` | snapshot a catalogue exercise into the running session with N empty set slots |
| `POST /exercises/{exLogId}/replace` `{exerciseId, scope}` | swap the movement; clears performed values on not-yet-completed sets; `scope: "TodayAndFuture"` also updates the first matching exercise on the source day |
| `POST /exercises/{exLogId}/add-set` | one more empty set |
| `DELETE /exercises/{exLogId}` | remove; `409` if it has a completed set |

### EF gotcha — adding to a tracked graph with client-set keys

`StartAsync` builds the whole `WorkoutSession` graph and calls `db.WorkoutSessions.Add(session)`
— one `Add` on the root marks everything `Added`, keys and all. But **mid-session**, the
`session` is already tracked (loaded), and doing

```csharp
var log = new ExerciseLog { Id = Guid.NewGuid(), /* ... */ };
session.ExerciseLogs.Add(log);   // ← EF's change detector sees a set key and thinks "existing row"
await db.SaveChangesAsync();      // ← issues an UPDATE, 0 rows, DbUpdateConcurrencyException
```

The fix is to add through the **DbSet**, which forces `Added` state; EF's relationship fixup
then wires `log` into `session.ExerciseLogs` itself (adding to both duplicates it):

```csharp
db.ExerciseLogs.Add(log);        // log.SessionId is set; fixup does the rest
await db.SaveChangesAsync();
```

Front end: `useSessionExercises` (add / replace / addSet / remove); the active screen gets an
`+ Add exercise` picker and per-card `+ Add set` / `Replace` (This-workout-only vs
Also-update-the-day) / `Remove`.

---

## Slice 6 — workout calendar + reschedule

- `GET /workout-calendar?from=&to=` → completed sessions grouped by `PerformedOnLocalDate`
  (default: last 6 weeks), per-owner, each with its summary.
- `POST /workout-sessions/{id}/reschedule` `{localDate}` → move a **completed** session's
  calendar date (docs/01 Story 3A: "correct the completed session date"); `409` if not
  completed.

Front end: `WorkoutCalendarScreen` at `/workouts/calendar` — a Monday-based month grid
(42 cells, prev/next month), days with sessions marked, click to list that day and move a
session with a `<input type="date">`. No calendar library.

---

## Slice 7 — in-app rest timer

### Snapshot the rest values at start

`ExerciseLog` gained `RestSeconds` (from the day's exercise) and
`SupersetRestAfterRoundSeconds` (from the source superset group). Snapshotted at session
start like everything else, so editing the program later doesn't change a past session.
Migration `AddSessionRestSnapshot`.

### The timer (front end, `useRestTimer`)

One countdown at a time. `start(seconds)` (re)starts it; a fixed bottom bar shows `mm:ss` +
a progress bar + "Skip rest". On reaching zero: a ~0.35s WebAudio chime + `navigator.vibrate`
— **no browser notifications** (locked decision #31). Cleans up its interval on unmount.

### When it starts

`ActiveWorkoutScreen` calls it after a set is logged **or skipped**:

- standalone exercise → `restSeconds`, every set;
- superset member → `supersetRestAfterRoundSeconds`, but only once *every member's set for
  that round* (`set.sortOrder`) is completed or skipped. The check treats the set just acted
  on as done, so it works off the not-yet-refetched session.

---

## Slice 8 — offline autosave (the MVP subset)

Set logging already hits the server on every change; this keeps those changes safe when the
network drops (docs/02 "offline local autosave").

- **`offlineQueue.ts`** — a `localStorage`-backed retry queue with a tiny subscribable store.
  `useLogSet` / `useSkipSet` now call `postOrQueue`: on a **network** error (not an
  `ApiError`) the mutation is queued and resolves so the workout isn't blocked; a real
  rejection still throws. A queued item that later fails with a 4xx (its set is gone, the
  session moved on) is dropped and a "sync error" is flagged.
- **`SyncStatus`** — the `Synced / Syncing… / Offline (N queued) / Sync error` chip in the
  active-workout header. It also owns the retry loop: flush on the `online` event and every
  15s while items remain, then revalidate the session query.
- **`useActiveSession`** mirrors its result to `localStorage` and hydrates from it as
  `initialData`, so a mid-workout reload paints the last-known session instantly.

**Not done (deliberately):** full offline browsing/editing of the whole app, and the
server↔local conflict-recovery flow — both low value for a single-user MVP, both called out
in `docs/08`.

---

## Where Phase 3 stands

Done: start/resume a session, tracking-mode-aware set logging + skip, `Copy previous set`,
finish with a summary + PRs, history list + calendar + reschedule, per-exercise history &
strength trend, add/replace/remove exercises mid-session, in-app rest timer (standalone +
superset round), offline queue + sync indicator.

Test totals: **45 unit + 95 integration + 57 frontend**, all green.

Deferred to V1 / later: strength charts beyond the sparkline (a real chart lib), the
`Distance + Duration` tracking mode, full offline + conflict recovery, undo for destructive
edits, the "archive vs delete a day that has session history" guard.
