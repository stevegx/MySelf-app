# 2026-09-06 — Edit a completed workout (the last Phase 3 slice)

Written for a web dev learning .NET. Branch: `phase-3-execution`.

This finishes the one thing left from Phase 3: **you can now fix a finished
workout** — the weight you fat-fingered, a set you didn't really do.

## The rule (docs/02 §7)

> Completed workout logs stay editable. Each edit recomputes volume, e1RM, PRs
> and the dashboard/progress charts, and marks the session as `Edited`.

## Backend

### 1. `WorkoutSession.WasEdited` (bool)

One new column. `nullable: false, defaultValue: false` — every existing row
becomes `false` automatically, no data migration needed.

```csharp
// migration 20260906080510_AddSessionWasEdited
migrationBuilder.AddColumn<bool>("WasEdited", "workout_sessions",
    type: "boolean", nullable: false, defaultValue: false);
```

### 2. Let the set endpoints touch a *completed* session

`POST /workout-sessions/{id}/set-logs` and `/skip-set` used to load the session
with `Status == InProgress` only. There's now a second loader:

```csharp
private static Task<WorkoutSession?> LoadForSetEditAsync(
    MySelfDbContext db, Guid userId, Guid id, CancellationToken ct) =>
    db.OwnedSessions(userId)
        .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
        .Where(s => s.Id == id
            && (s.Status == SessionStatus.InProgress || s.Status == SessionStatus.Completed))
        .FirstOrDefaultAsync(ct);
```

A **discarded** session still isn't editable — it falls through to
`NotFoundOrConflict`, which returns `409`. (Structural edits — add / replace /
remove an exercise mid-session — stay `InProgress`-only; reopening the shape of a
finished workout is a bigger can of worms and nobody asked for it.)

### 3. Rebuild the PRs on an edit

An edit can *create* a PR (you actually lifted 140, not 100) or *invalidate* one
(you didn't — correct it down). So after a change to a completed session:

```csharp
private static async Task RecomputeAfterEditAsync(
    MySelfDbContext db, Guid userId, WorkoutSession session, DateTimeOffset now, CancellationToken ct)
{
    session.WasEdited = true;

    var stale = await db.PersonalRecords.Where(p => p.SessionId == session.Id).ToListAsync(ct);
    db.PersonalRecords.RemoveRange(stale);
    await db.SaveChangesAsync(ct);          // clear first…

    var fresh = await DetectPersonalRecordsAsync(db, userId, session, now, ct);  // …then re-detect
    db.PersonalRecords.AddRange(fresh);
}
```

Why the mid-method `SaveChangesAsync`: `DetectPersonalRecordsAsync` compares the
session's lifts against `db.PersonalRecords` for that user+exercise. If this
session's own (now-stale) rows are still in the table, an edit *downward* would
be compared against its own old higher number and wrongly find "no PR". Deleting
them first gives a clean baseline. The summary (`totalVolumeKg`, e1RM, set
counts) isn't stored — it's computed by `SessionSummaryCalculator` every time the
session is read — so it updates for free.

C# notes for a JS dev:

- `RemoveRange` / `AddRange` — batch versions of `Remove` / `Add`; still just
  change-tracker bookkeeping until `SaveChangesAsync`.
- `is not null` — pattern-matching null check, same as `!= null` here.
- `DateTimeOffset` is passed in (`clock.GetUtcNow()`) rather than read inside, so
  the method stays testable with a fake clock — the project does this everywhere.

### Contracts

`WorkoutSessionDetail` and `WorkoutSessionListItem` gained `bool WasEdited`, so
the history list and calendar can show an "Edited" badge without a second fetch.

## Frontend

### `SessionEditScreen` — `/workouts/session/:id`

A focused editor, separate from the running-workout screen (which is full of
rest-timer / superset logic that doesn't apply here). It:

- loads the session with a new `useSession(id)` hook (`["workout-session", id]`,
  so the existing `invalidateQueries(["workout-session"])` calls refresh it);
- guards: skeleton while loading → "couldn't be found" → "this one isn't
  finished, go to the active workout" → the editor;
- renders each set with its performed values in editable inputs (`fieldsFor(mode)`
  picks which fields per tracking mode). **Save** re-logs the set, **Skip** skips
  it. Each change is its own request.
- shows a summary strip (sets / reps / volume) that updates as you save, and an
  **Edited** tag once `wasEdited` is true.

### Getting there

- History rows are now `<Link to={`/workouts/session/${id}`}>` — the whole card
  is clickable — with an "Edited" tag in the tag row.
- The calendar day popover gets an **Edit** link per session (next to "Move to").

### `useLogSet` / `useSkipSet` invalidation

They used to invalidate only `["workout-session"]`. Editing a *completed* session
also moves the calendar, the program Overview stats and the per-exercise strength
views, so both mutations now invalidate `["workout-calendar"]`,
`["program-stats"]`, `["exercise-history"]` and `["strength-analytics"]` too.

## Verified

- **Backend:** 49 unit + 102 integration green. New integration tests: set edit
  flags `wasEdited` + recomputes the summary (800 → 880 kg volume); edit rebuilds
  the exercise's PR (100 → 130 kg); a discarded session returns 409.
- **Frontend:** 65 tests, lint, `npm run build` all clean. New
  `SessionEditScreen.test.tsx`: re-logs with a corrected value, shows the Edited
  badge, refuses a non-completed session.
- **Live smoke test** against the running API: log 100 kg → complete
  (`wasEdited: false`, volume 500, HeaviestWeight PR 100) → edit to 140 kg
  (`wasEdited: true`, status still Completed, volume 700, PRs rebuilt to
  HeaviestWeight 140 / e1RM 163.3 / volume 700); discarded session edit → 409.

## Phase 3 is now complete.

Next: merge `phase-3-execution` → `master`, then Phase 4 (aggregated dashboard
endpoint, body-weight logging).
