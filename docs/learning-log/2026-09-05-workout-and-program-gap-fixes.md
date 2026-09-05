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

---

## Phase 4a — Program insights (same session, later)

You asked for two more things: **archived programs wouldn't delete**, and you
want **stats + a calendar when you open a program**, not just the day list.

### The archived-delete bug

On the latest code the delete path is fine end to end — `DELETE
/api/v1/programs/{id}` removes archived programs too. The reason it failed for
you: **your running API predates commit `4ac03a4`**, which is the commit that
first added that route. The request 404s and, until now, the UI threw the error
away with no message.

Fix (`68e08d1`, shipped): an `InlineError` component on the program list, the
archived list and the program header that renders whatever `ApiError` a failed
`remove` / `restore` / `archive` / `clone` produced. And: **restart the API** and
the delete itself works.

### Attributing a session to a program

A `WorkoutSession` stored `SourceDayId` (a soft pointer, no foreign key) and a
`ProgramName` *string* — but no program id. So "sessions from this program"
couldn't be queried.

Fix: add `WorkoutSession.SourceProgramId` (`Guid?`, soft pointer, indexed), set
at start from the source day's program. The migration also **backfills** existing
rows:

```sql
UPDATE "workout_sessions" s
   SET "SourceProgramId" = d."ProgramId"
  FROM "workout_days" d
 WHERE s."SourceDayId" = d."Id";
```

Why a snapshot column instead of a live join every time? Same reason
`SourceDayId` is soft: if you later move a day to another program or delete it,
history shouldn't move or vanish. A join would do both.

### `ProgramStatsCalculator` (pure, unit-tested)

Same pattern as `SessionSummaryCalculator` — a static class, no EF, no DI, so it
runs in the fast `MySelf.UnitTests` project (which builds even while the API
holds the main build lock). Given a program's completed sessions + its current
days + "today", it returns:

- total sessions, first / last performed date
- sessions this week, this month
- **weekly average** = sessions in range ÷ weeks in range, where the range is how
  many weeks the program has existed, **capped at 8**. Deliberately *not* an
  adherence % or missed-workout rate — docs/02 forbids those without a fixed
  plan.
- total volume (Σ of each session's volume), average duration
- completed vs skipped set counts + a skipped rate
- per-day: how many sessions used each day, and the last date

PRs ("records set here") are gathered in the endpoint, not the calculator,
because they need a join to the exercise catalogue for the name.

### `GET /api/v1/programs/{id}/stats`

Loads the program's days + its completed sessions (`SourceProgramId == id`),
calls the calculator, then joins `PersonalRecords` whose `SessionId` is one of
those sessions. `?today=YYYY-MM-DD` lets the client pass its local date (locked
decision #8); falls back to the server's UTC date.

The workout-calendar endpoint got one new optional query param, `?programId=`,
which just adds `&& s.SourceProgramId == programId` to its existing filter.

### The Overview tab (frontend)

`ProgramDetail` now has two tabs:

- **Overview** (default) — `ProgramOverview.tsx`: a grid of stat cards, a "by
  day" table, a compact month calendar (reuses the calendar endpoint with
  `?programId=`, has its own prev/next month), and a PR list.
- **Days** — exactly what the page was before (the sortable editable day list +
  "New day"). Kept mounted but `hidden` when Overview is active so the
  drag-and-drop state doesn't reset on every tab switch.

New in `api.ts`: `useProgramStats(id)`, an optional `programId` arg on
`useWorkoutCalendar`, and `complete` / `reschedule` now also invalidate
`["program-stats"]` so the Overview refreshes after you finish or move a workout.

### State of play

| Piece | Status |
| --- | --- |
| `68e08d1` archived-delete error surfacing | shipped, 61→62 frontend tests |
| `ada9fb7` `SourceProgramId` + config | compiles (via the unit-test build) |
| `ada9fb7` `ProgramStatsCalculator` + 4 unit tests | green |
| `ada9fb7` `/programs/{id}/stats` endpoint + `?programId=` | **written, not compiled** (API build lock) |
| `ada9fb7` migration | **not created** — needs the API stopped |
| `373289a` Overview tab UI | shipped, 62 frontend tests, build clean |

**To finish Phase 4a**, once the dev API is stopped:

```bash
taskkill /PID 8512 /F        # or Ctrl-C in its terminal
cd backend
dotnet ef migrations add AddSessionSourceProgram --project MySelf.Infrastructure --startup-project MySelf.Api
#  -> then paste the backfill UPDATE (above) into the new migration's Up()
dotnet test MySelf.sln
dotnet run --project MySelf.Api
```

### Update — Phase 4a finished the same day

The dev API was stopped and everything above is now live:

| Piece | Status |
| --- | --- |
| `4dec9b2` migration `AddSessionSourceProgram` (+ backfill `Sql()`) | applied to dev DB; **49 unit + 99 integration** green |
| `4dec9b2` fix broken `Archive_hides_the_program_from_the_default_list` assertion | done (it archived a program then expected the archived list empty) |
| `4dec9b2` new tests `Program_stats_count_…`, `An_archived_program_can_still_be_hard_deleted` | green |
| `af92e6e` `<Skeleton>` / `<SkeletonText>` + Overview skeleton + Overview error card | shipped, 62 frontend tests |
| `af92e6e` one test stabilised: `findByText` → `waitFor(getByText)` | the repo's known fast-reject flake, tipped by the extra render work |
| `a77a4f5` `docs/api/workout-responses.md` | example JSON for every workout endpoint + how to inspect live |
| API restarted by me | new PID; serves `DELETE /programs/{id}`, `/programs/{id}/stats`, `?programId=` — verified via `/openapi/v1.json` |

**Archived-delete bug**: root cause was the running binary predating the `DELETE` route
(`4ac03a4`). Rebuilding + restarting fixed it; `68e08d1` also stops the UI swallowing the
error. Both covered by `An_archived_program_can_still_be_hard_deleted`.

**Still open:** Slice B (edit a finished workout) — unblocked, not started.
