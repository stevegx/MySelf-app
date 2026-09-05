# 2026-09-05 — Workout & program gap fixes

Written for a web dev who knows JS/React and is learning .NET. This session was a
round of "I tested the running app and here's what's wrong / missing" fixes on
top of Phase 3. Branch: `phase-3-execution`.

## What shipped

| Commit | What |
| --- | --- |
| `4ac03a4` | Real program delete (hard `DELETE`) split from Archive; one shared Programs/History/Calendar tab strip |
| `ba645f8` | Dashboard buttons all wired; "Workout frequency" card shows real data from session history |
| `a81923d` | Delete / duplicate a program straight from the list + multi-select bulk copy/delete |
| `ce109bf` | You can't "Finish" a totally empty workout |

Slice B ("edit a workout after you finish it") is **designed but not built** — see
the last section. It needs a database migration and the backend currently won't
build (the running dev API is holding the compiled DLLs open).

---

## 1. Why couldn't I delete a program? (`4ac03a4`)

The list only had "Archive". Archive sets `ArchivedAt` and `IsActive = false` but
keeps the row — that's a soft delete, on purpose (you might want the history).
There was no way to actually remove one.

**Fix:** a second endpoint.

```csharp
// ProgramEndpoints.cs
programs.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteProgram");
programs.MapPost("/{id:guid}/archive", ArchiveAsync).WithName("ArchiveProgram");

private static async Task<IResult> DeleteAsync(Guid id, Guid userId, MySelfDbContext db, CancellationToken ct)
{
    var program = await db.OwnedPrograms(userId).FirstOrDefaultAsync(p => p.Id == id, ct);
    if (program is null) return Results.NotFound();
    db.WorkoutPrograms.Remove(program);   // hard delete
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
}
```

`.NET` concepts here vs. JS:

- `MapDelete` / `MapPost` — these register a route + handler, like
  `app.delete("/...", handler)` in Express. The `{id:guid}` bit is a route
  constraint: the request only matches if that segment parses as a GUID,
  otherwise you get a 404 before your code runs.
- `OwnedPrograms(userId)` is our own extension method returning an
  `IQueryable<WorkoutProgram>` already filtered to the caller. `IQueryable` is a
  **query you haven't run yet** — EF turns it into one `SELECT ... WHERE
  "UserId" = @userId AND "Id" = @id` when you `await` a terminal call like
  `FirstOrDefaultAsync`. Chaining `.Where(...)` doesn't hit the DB; the `await`
  does. Think of it as a lazily-built SQL string.
- `db.WorkoutPrograms.Remove(program)` just **marks** the entity `Deleted` in
  EF's change tracker. Nothing happens until `SaveChangesAsync`, which issues the
  `DELETE`. Child rows (days, exercises, prescribed sets) go too because the
  foreign keys are configured `ON DELETE CASCADE` in
  `WorkoutConfigurations.cs`.
- `Results.NoContent()` = HTTP 204. `Results.NotFound()` = 404. These are the
  minimal-API way of returning a status with no body.

**Threat to know:** hard delete is irreversible and cascades. The ownership
filter (`OwnedPrograms`) is the only thing stopping user A from deleting user B's
program by guessing an id — if you ever add an endpoint that skips it, that's an
IDOR (insecure direct object reference) hole.

---

## 2. Easier navigation (`4ac03a4`)

Before: each workout screen drew its own back-button and ad-hoc links to the
others. Now there's one `WorkoutLayout.tsx` with a tab strip
(Programs / History / Calendar) and a "Resume workout" pill that only appears
when `useActiveSession()` has data.

It's wired with a **nested route**:

```tsx
{ element: <WorkoutLayout />, children: [
    { path: "workouts/builder", element: <WorkoutBuilderScreen /> },
    { path: "workouts/history", element: <WorkoutHistoryScreen /> },
    { path: "workouts/calendar", element: <WorkoutCalendarScreen /> },
] }
```

`WorkoutLayout` renders the tabs once and an `<Outlet />` where the active child
screen goes — same idea as a layout route in Next.js. `workouts/active` is
deliberately left *outside* the layout so the running-workout screen is
full-bleed with no tab chrome.

---

## 3. Dashboard (`ba645f8`)

- Every button had a real `onClick` added (`navigate("/nutrition")` etc.).
- "Start workout" now checks for an active session: `navigate(active ?
  "/workouts/active" : "/workouts/builder")` and relabels to "Resume workout".
- "Workout frequency" used to be hard-coded bars. Now `useWorkoutFrequency()`
  reads `useSessionHistory()` and buckets `performedOnLocalDate` into the current
  Mon–Sun week (`perDay[7]`) plus this-week / this-month counts. Bar height is
  `count / max * 100%`.

Nothing backend here — it's all client math over data we already fetched.

---

## 4. Delete / duplicate programs from the list + multi-select (`a81923d`)

Frontend only. `ProgramList` in `WorkoutBuilderScreen.tsx` gained:

- Per row: ghost **Duplicate** and **Delete** buttons (Delete goes through the
  styled `useConfirm()` dialog, not `window.confirm`).
- A **Select** toggle. In select mode every row gets a `<Checkbox>` and a bulk
  toolbar appears: "{n} selected · Duplicate · Delete".
- Bulk actions fan out client-side:
  ```ts
  await Promise.all(ids.map((id) => m.remove.mutateAsync(id).catch(() => {})));
  ```
  Each id is its own request / its own transaction. `.catch(() => {})` so one
  failure doesn't abort the rest; TanStack Query then refetches the list once.

**Drawback to be aware of:** N deletes = N round trips and N rate-limiter hits
(`RequireRateLimiting(WritePolicy)`). Fine for a handful of programs, but if this
list ever got long we'd want a real `POST /programs/bulk-delete` taking an id
array and doing one `DELETE ... WHERE "Id" = ANY(@ids)`.

Tests added to `WorkoutBuilderScreen.test.tsx`:
- "hard-deletes a program via DELETE after confirming"
- "multi-selects programs and bulk-deletes them" (expects 2 `DELETE` calls)

---

## 5. You can't finish an empty workout (`ce109bf`)

**The rule (docs/06 edge cases):** a session with nothing logged *and* nothing
skipped isn't a workout. It must never land in history, the calendar or
analytics. Skipping a set *does* count as "I showed up" — a bailed session can
still be recorded.

### Backend — one guard in `CompleteAsync`

```csharp
var actedAnySet = session.ExerciseLogs
    .Any(e => e.Sets.Any(s => s.CompletedAt is not null || s.SkippedAt is not null));
if (!actedAnySet)
    return Validation("session", "Log or skip at least one set before finishing this workout.");
```

- `session.ExerciseLogs` is already loaded here — the query above it does
  `.Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)`, EF's version of a
  join + hydrate into an object graph. So `.Any(...)` runs **in memory**, not as
  SQL.
- `is not null` is C#'s null check in a pattern-matching style; `!= null` would
  do the same here.
- `Validation(...)` is a shared helper returning `Results.ValidationProblem` —
  HTTP 400 with a `{ "errors": { "session": ["..."] } }` body, the same shape
  every other validation failure in the API uses, so the frontend's existing
  error parsing picks it up for free.

### Frontend — don't even let them click it

`ActiveWorkoutScreen.tsx` already computed `actedSets`. Now:

```tsx
<Button
  onClick={finish}
  disabled={complete.isPending || discard.isPending || actedSets === 0}
  title={actedSets === 0 ? "Log or skip at least one set first" : undefined}
>
```

Plus the "no exercises yet" copy now says an empty workout can't be finished and
points at **Discard**.

Two layers on purpose: the button disable is UX, the API check is the actual
rule (a stale tab, a replayed request, or the offline queue could still POST
`/complete`).

### Tests (integration, `WorkoutSessionEndpointTests.cs`)

- `Completing_an_empty_session_is_rejected_until_a_set_is_logged_or_skipped` —
  start → `POST /complete` → expect 400 `errors.session`; session still active;
  log a set → `POST /complete` → 200.
- `Completing_a_session_where_a_set_was_only_skipped_is_allowed` — skip the set,
  then complete → 200.

> ⚠️ These two tests are written but **not yet run** — see below.

---

## The build-lock problem (why backend isn't verified)

`dotnet build` fails with `MSB3021 / MSB3027`:

```
Could not copy "MySelf.Infrastructure.dll" ... The file is locked by: "MySelf.Api (8512)"
```

Your `dotnet run` dev server has the compiled `MySelf.Domain.dll` /
`MySelf.Infrastructure.dll` open, so MSBuild can't overwrite them. The C#
compiles fine — only the copy-into-`MySelf.Api/bin` step fails — but that means
**I can't run the backend tests or create an EF migration** until the API is
stopped.

The frontend is fully verified: **60 tests, lint, and `npm run build` all green.**

### To verify the backend yourself

```bash
# 1. Stop the running API (Ctrl-C in its terminal, or:)
taskkill /PID 8512 /F

# 2. From backend/
dotnet test MySelf.sln

# 3. Restart it
dotnet run --project MySelf.Api
```

---

## Still to do — Slice B: edit a workout after finishing it

You asked: *"When I finish a workout I want to be able to edit it."* This needs a
schema change, so it's blocked on the build lock. The plan:

1. **`WorkoutSession.WasEdited` (bool, default false)** + an EF migration
   (`dotnet ef migrations add AddSessionWasEdited`). Can't generate a migration
   while the API holds the build.
2. **Relax `LogSetAsync` / `SkipSetAsync`** — today they load the session with
   `Status == SessionStatus.InProgress`. Allow `Completed` too; when a completed
   session is touched, set `WasEdited = true`.
3. **Recompute personal records on edit** — an edit can invalidate a PR that was
   awarded at finish. On the first edit of a completed session, delete that
   session's `PersonalRecord` rows and re-run `PersonalRecordDetector` against
   the rest of the user's history.
4. **Frontend `SessionEditScreen`** at `/workouts/session/:id`, reachable from a
   row in History and Calendar. Reuses the `SetRow` component. An "Edited" badge
   wherever the session is shown.

Open question for you before I build it: should editing a finished session be a
dedicated edit screen (plan above), **or** a "Reopen" button that flips it back
to `InProgress` so it becomes the active workout again and you use the normal
running-workout screen? The second is less code and no new screen, but the
session temporarily vanishes from History/Calendar while you edit, and an
abandoned edit leaves it stuck "in progress". I lean toward the dedicated edit
screen.
