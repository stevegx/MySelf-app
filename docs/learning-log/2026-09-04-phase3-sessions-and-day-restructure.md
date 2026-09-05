# Phase 3 kickoff: the Day restructure + starting a workout session

Written 2026-09-04. Two things landed together:

1. **A model change** — the two-level `Workout Group → Workout Variant` structure was
   collapsed to a single flat `Workout Day`. This amends **locked decision #4** (docs/08),
   and docs 01/02/08 were updated to match.
2. **The first Phase 3 slice** — `WorkoutSession` / `ExerciseLog` / `SetLog` entities, and
   `POST /workout-sessions` + `GET /workout-sessions/active`.

Audience: comfortable with React/TypeScript, newer to .NET/EF Core.

---

## Part 1 — Why the Day restructure

### What changed

Before:

```
WorkoutProgram → WorkoutGroup ("Legs") → WorkoutVariant ("Legs #1", "Legs #2")
                                          → VariantExercise → SetPrescription
```

After:

```
WorkoutProgram → WorkoutDay ("Legs A", "Legs B")
                 → DayExercise → SetPrescription
```

`WorkoutGroup`, `WorkoutVariant`, `VariantExercise` and their endpoints are gone.
`workout-groups` / `workout-variants` became just `workout-days`. `VariantEditor.tsx` →
`DayEditor.tsx`.

### Why

The Group level never earned its keep in the MVP. The docs themselves said the `#1 / #2`
naming was "just a naming convention" — so a Group like "Legs" containing "Legs #1" and
"Legs #2" is two clicks and a wrapper object to express what "Legs A" + "Legs B" as two
plain days says directly. Nothing in the MVP reads the Group as a unit: the manual picker
lists individual workouts, analytics key off the *exercise*, and there are no weekdays or
rotation. Two similar days are now just two days the user names.

Fewer entities → fewer endpoints, a simpler tree to snapshot at session start (Part 2), and
less UI. If a real grouping need shows up later (e.g. "show me all my leg days together"), a
`tag` column on `WorkoutDay` covers it without a table.

### The migration

`RestructureProgramToWorkoutDays`:

- `DropTable` on `variant_exercises`, `workout_variants`, `workout_groups`.
- `CreateTable` `workout_days`, `day_exercises`.
- **Renames** on the just-added session tables: `superset_groups.VariantId → DayId`,
  `set_prescriptions.VariantExerciseId → DayExerciseId`, `workout_sessions.VariantName →
  DayName` / `SourceVariantId → SourceDayId`, drop `workout_sessions.GroupName`.

**EF note — `RenameColumn` vs drop+add.** A rename keeps the column's data and just changes
its name; `DropColumn` + `AddColumn` would lose everything in it. EF's migration diff picks
rename automatically when a property is renamed (not retyped). Because this restructure
followed right after `AddWorkoutSessions` in the same work session, the session tables were
built with `Variant*` names and then renamed here — two sequential migrations rather than
one, which is fine: migrations always run in order.

**This is a destructive migration.** `Down()` recreates the old tables but **cannot restore
their rows** — the data was dropped. That's acceptable here because the builder had no real
user data yet, but it's the kind of migration you never run against a populated production
database without a backup and a data-copy step.

---

## Part 2 — Starting a workout session

### The three new entities

| Entity | Role |
|---|---|
| `WorkoutSession` | One actual workout, `InProgress` / `Completed` / `Discarded`. Only one `InProgress` per user (filtered unique index, same trick as one-active-program). |
| `ExerciseLog` | One exercise *inside* a session — a **snapshot** of the catalogue exercise's name + tracking mode, taken at start. |
| `SetLog` | One set inside an `ExerciseLog`. Carries both the **target** (copied from the `SetPrescription` at start) and the **performed** values (null until the user logs them). |

### The core idea: snapshot at start

```csharp
var session = new WorkoutSession
{
    SourceDayId = day?.Id,          // a soft pointer — nothing re-reads the live day
    DayName     = day?.Name,        // captured
    ProgramName = day?.Program.Name,// captured
    Status      = SessionStatus.InProgress,
    StartedAt   = clock.GetUtcNow(),
    ExerciseLogs = day.Exercises.OrderBy(e => e.SortOrder).Select(SnapshotExercise).ToList(),
};
```

Every name, target, superset grouping and set is **copied into the session** at start.
After that, editing the program — renaming the day, changing a rep range, deleting the day
entirely — never touches this session's history. `SourceDayId` is kept only so the UI can
say "you started this from Legs A"; the code never loads the day again.

This is the same principle as the nutrition `NutritionEstimateSnapshot` and the meal-log
snapshots: **history records what happened, not a live reference to a plan that can change.**

### The single-in-progress guard, done twice

```csharp
// 1. check first
var existingId = await db.OwnedSessions(userId)
    .Where(s => s.Status == SessionStatus.InProgress)
    .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
if (existingId is { } activeId) return AlreadyActive(activeId);

// ... build the session ...

// 2. and catch the race at the database
try { await db.SaveChangesAsync(ct); }
catch (DbUpdateException)
{
    var raceId = await db.OwnedSessions(userId)
        .Where(s => s.Status == SessionStatus.InProgress)
        .Select(s => s.Id).FirstAsync(ct);
    return AlreadyActive(raceId);
}
```

The check-first is for the normal case (a friendly 409 with the existing session's id). The
`try/catch` is because two quick taps on "Start" could both pass the check before either
inserts. The DB's filtered unique index (`WHERE Status = 'InProgress'`) rejects the second
insert with a `DbUpdateException`, and we turn that into the same 409 rather than a 500.
**The database is the real guarantee; the app-level check is just a nicer error most of the
time.**

### Endpoints so far

| Method | Path | Notes |
|---|---|---|
| `POST` | `/api/v1/workout-sessions` | `{ dayId }` snapshots that day; `{ dayId: null }` starts an ad-hoc session with no exercises. `RequireRateLimiting("write")`. |
| `GET` | `/api/v1/workout-sessions/active` | The caller's `InProgress` session in full, or 404. |

Ownership uses the centralised `db.OwnedSessions(userId)` / `db.OwnedDays(userId)` helpers
from the Phase 2 gap closure (`OwnedWorkouts.cs`) — a query that filters by user id up the
tree, so a handler can't forget it.

---

## Part 3 — Logging sets, skipping, and finishing (added same day)

The read-only "start a session" from Part 2 became a workout you can actually run.

### New endpoints

| Method | Path | Notes |
|---|---|---|
| `GET` | `/workout-sessions/{id}` | one session, any status (the completed one you just finished) |
| `POST` | `/workout-sessions/{id}/set-logs` | log/re-log a performed set — marks it complete |
| `POST` | `/workout-sessions/{id}/skip-set` | skip a set with an optional reason |
| `POST` | `/workout-sessions/{id}/complete` | finish: status → `Completed`, stamp `PerformedOnLocalDate` |
| `POST` | `/workout-sessions/{id}/discard` | throw the in-progress session away |

### Tracking-mode-aware validation (`Domain/Workouts/SetLogValidation.cs`)

A pure function — same style as `CalorieEstimator` — that says which performed fields a set
needs before it can complete, keyed on the exercise's `TrackingMode`:

```csharp
public static string? MissingRequiredField(TrackingMode mode, SetPerformance p) => mode switch
{
    TrackingMode.WeightAndReps        => p is { WeightKg: not null, Reps: not null } ? null : "Enter both weight and reps.",
    TrackingMode.BodyweightReps       => p.Reps is not null ? null : "Enter reps.",
    TrackingMode.BodyweightPlusWeight => p is { AddedWeightKg: not null, Reps: not null } ? null : "Enter both added weight and reps.",
    TrackingMode.AssistanceReps       => p is { AssistanceKg: not null, Reps: not null } ? null : "Enter both assistance weight and reps.",
    TrackingMode.RepsOnly             => p.Reps is not null ? null : "Enter reps.",
    TrackingMode.Duration             => p.DurationSeconds is not null and > 0 ? null : "Enter a duration in seconds.",
    _ => null,
};
```

This is locked decision #10 ("both values required for Weight × Reps, no inheritance"). The
front end shows only the relevant inputs per mode, but the *server* is the gate — the
endpoint returns `400 { errors: { set: ["Enter both weight and reps."] } }` if a field is
missing, so a hand-crafted request can't slip an incomplete set through.

**C# note — property patterns:** `p is { WeightKg: not null, Reps: not null }` checks two
properties on one object in a single expression. `p.DurationSeconds is not null and > 0`
combines a null check and a relational check with `and`. Both are cleaner than the `&&`
chains they replace and read close to English.

### "Log" vs "skip" are mirror operations

Logging a set writes the performed values, sets `CompletedAt = now`, and **clears**
`SkippedAt` / `SkippedReason`. Skipping does the opposite: sets `SkippedAt`, clears
`CompletedAt` and *every* performed value. So a set is always in exactly one of three
states — pending, completed, skipped — and toggling between logged and skipped never leaves
stale data behind. An uncompleted set (pending) simply doesn't enter analytics later
(docs/06).

### Finishing

`complete` takes `{ localDate, notes }`. The **client** sends `localDate` — its own
calendar date — because "which day did this workout happen on" is a local-time question and
the server only knows UTC (locked decision #8). The server falls back to the UTC date if
the client doesn't send one. After completion, `GET /workout-sessions/active` returns 404
(nothing in progress) and the session is readable at `GET /workout-sessions/{id}`.
Completing an already-finished session is a `409`.

### Front end

`ActiveWorkoutScreen` went from a static preview to a working logger: per-set inputs that
switch by tracking mode (`fieldsFor(mode)`), a "Log set" / "Skip" pair per set, a running
"N of M sets logged or skipped" count, and "Finish workout" / "Discard" (the latter behind
the styled `useConfirm` dialog). Each action is its own request; the query invalidates and
re-renders from the server's response. `useCompleteSession` builds the local `YYYY-MM-DD`
from `new Date()` before posting.

### Still not built (rest of Phase 3)

`Copy previous set`, PR / volume / e1RM calculation and the finish summary, add / replace
exercise mid-session, the history list, the workout calendar, the in-app rest timer, and
the offline draft + sync.

---

## Files

**Backend**

| File | |
|---|---|
| `Domain/Workouts/WorkoutDay.cs`, `DayExercise.cs` | replace `WorkoutGroup` + `WorkoutVariant` + `VariantExercise` |
| `Domain/Workouts/WorkoutSession.cs`, `SessionStatus.cs`, `ExerciseLog.cs`, `SetLog.cs` | **new** — session history |
| `Api/Workouts/WorkoutDayEndpoints.cs` | `GET`/`PUT`/`DELETE /workout-days/{id}` + `bulk-copy` / `bulk-move` (carried over from the variant version) |
| `Api/Workouts/WorkoutSessionEndpoints.cs` | **new** — `POST` + `GET /active` |
| `Api/Workouts/ProgramEndpoints.cs`, `WorkoutContracts.cs`, `OwnedWorkouts.cs`, `WorkoutLimits.cs` | day-shaped instead of group/variant-shaped |
| `Infrastructure/.../WorkoutConfigurations.cs`, `WorkoutSessionConfigurations.cs` | mappings, filtered unique index on `(UserId) WHERE Status = 'InProgress'` |
| Migrations `AddWorkoutSessions`, `RestructureProgramToWorkoutDays` | |
| `IntegrationTests/Workouts/WorkoutSessionEndpointTests.cs` | **new**, 6 tests; `WorkoutBuilderEndpointTests.cs` reworked for days |

**Frontend**

| File | |
|---|---|
| `features/workouts/DayEditor.tsx` (+ test) | replaces `VariantEditor.tsx` |
| `features/workouts/WorkoutBuilderScreen.tsx`, `ActiveWorkoutScreen.tsx`, `api.ts` | day-shaped |

**Docs**

`docs/08` decision #4 amended (dated note) + Story 3 / product-constraints; `docs/02`
section 6 rewritten flat; `docs/01` builder + journey lines.

Test totals: **30 unit + 80 integration + 45 frontend**, all green.

---

## Self-test

1. Why does `WorkoutSession` copy `DayName` / `ProgramName` / every exercise + set instead of
   just keeping `SourceDayId` and reading the day when needed?
2. `POST /workout-sessions` checks for an existing in-progress session *and* catches a
   `DbUpdateException`. What does each one handle, and which is the actual guarantee?
3. `RestructureProgramToWorkoutDays` uses `RenameColumn` for the session-table columns but
   `DropTable` for `workout_variants`. What's the practical difference, and why can't its
   `Down()` fully undo it?
4. What did the Workout Group level actually provide in the MVP, and what's the cheap way to
   get grouping back later if it's needed?
