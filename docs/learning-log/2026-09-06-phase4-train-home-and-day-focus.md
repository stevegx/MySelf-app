# 2026-09-06 — Phase 4 starts: Train home, exercise muscles, day focus

Written for a web dev learning .NET. Branch: `phase-4-workouts-home`
(Phase 3 was merged to `master` first — `92efb38`).

Phase 4 is the big UX rework. After a long discovery chat the shape is: one
**active program** you live in for months; the app centres on it → its **days**
(with stats) → **exercises** (with stats); quick "twitching" (swap an exercise,
nudge a target, set a focus) never means opening a full builder. This log covers
the first three slices.

## Slice 1 — the "Train" home (`402676e`)

The `/workouts` landing used to be the program list. Now it's a **training
view**:

- The active program's **days as cards** — each with exercise count,
  *last performed* (a ⚠ once it's been over a week), how many times you've done
  it this block, and a big **Start** button.
- No active program → a prompt to pick or create one.
- **Manage programs** (header) opens the old list + builder. A day's **Edit**
  link opens the program detail (Overview / Days tabs) — unchanged.
- The workout sub-tab "Programs" is renamed **Train**.

No backend work — it's composed from data we already had (`GET /programs` +
`GET /programs/{id}` + the `perDay` block of `GET /programs/{id}/stats`).
`WorkoutBuilderScreen` gained a `view` state (`"home"` | `"programs"`);
`ProgramList` gained an optional `onBack`.

## Slice 2a — muscles + equipment on the exercise API (`3707aba`)

The catalogue was imported from wger with muscle and equipment data, but the API
never returned it. In the DB: **734 / 862** exercises have Primary/Secondary
muscles, **674** have equipment, across 15 muscle groups.

`ExerciseListItem` gained `primaryMuscles` / `secondaryMuscles` / `equipment`
(`string[]`). Both `GET /exercises` and `GET /exercises/{id}` use **one shared EF
projection** so they can't drift:

```csharp
private static readonly Expression<Func<Exercise, ExerciseListItem>> ToListItem = e => new ExerciseListItem(
    e.Id, e.Name, e.Category.Name, e.DefaultTrackingMode.ToString(),
    e.Muscles.Where(m => m.Role == MuscleRole.Primary).OrderBy(m => m.Muscle.Name).Select(m => m.Muscle.Name).ToList(),
    e.Muscles.Where(m => m.Role == MuscleRole.Secondary).OrderBy(m => m.Muscle.Name).Select(m => m.Muscle.Name).ToList(),
    e.Equipment.OrderBy(x => x.Equipment.Name).Select(x => x.Equipment.Name).ToList());
```

`.NET` note for a JS dev: a `.Select(...)` on an `IQueryable` isn't a normal
function — EF turns it into SQL. Storing it as an `Expression<Func<...>>` (a
*description* of the code, not the code) lets EF read that description and
generate the joins. A plain lambda `Func<...>` would run in .NET after loading
whole rows — much slower. The `Include(e => e.Category)` calls went away because
the projection already tells EF exactly which columns/joins it needs.

The exercise picker now shows a line like
`Bench Press · Chest · +Shoulders, Triceps · Barbell, Bench`.

## Slice 2b — day focus + the off-focus nudge (`86d7879` + `cc54cc5`)

**The idea (yours):** a day can declare which muscle groups it trains ("Upper" =
tick Chest, Shoulders, Triceps, …). The exercise picker then suggests matching
exercises first; adding one that's off-focus still works but shows a gentle note,
with a "don't warn me again" that lives in Settings.

### Backend

- **`GET /api/v1/muscles`** — the 15 catalogue muscle groups (`id, name, isFront`).
- **`WorkoutDay.FocusMuscleIds`** — a Postgres `integer[]` column
  (`default '{}'`). EF Core + Npgsql map a `List<int>` straight to `integer[]`,
  no join table. On `DayDetail`; accepted by `PUT /workout-days/{id}` where
  `null` = leave as-is, `[]` = clear, and any id that isn't a real muscle is
  dropped:
  ```csharp
  if (request.FocusMuscleIds is not null)
  {
      var wanted = request.FocusMuscleIds.Distinct().ToList();
      day.FocusMuscleIds = wanted.Count == 0
          ? []
          : await db.Muscles.Where(m => wanted.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
  }
  ```
- **`UserProfile.WarnOffFocusExercises`** — a `bool` (default `true`), surfaced in
  the profile on `GET /me`. `PUT /api/v1/me/profile` requires the calc fields
  (dob/height), so a tiny **`PUT /api/v1/me/preferences`** carries just this
  toggle instead (409 if the user has no profile yet).
- One migration, `20260906091458_AddDayFocusAndOffFocusPref` — an `AddColumn` for
  each. `integer[] NOT NULL DEFAULT '{}'` and `boolean NOT NULL DEFAULT true`, so
  every existing row is handled with no data step.

### Frontend

- `DayEditor` gets a **Focus** row of muscle chips (toggle buttons). The selected
  ids go into the edit state, into the dirty-check fingerprint, and into the PUT
  body.
- `ExercisePicker` takes `focusMuscleNames`: with a focus set it shows only
  matching-primary exercises plus a **"Show all (N more)"** toggle, and tags
  off-focus rows.
- On adding an exercise whose primary muscles are *all* outside the focus (and
  the user hasn't opted out), an **inline note** appears: "Added Overhead Press —
  Shoulders, outside this day's focus." with **Dismiss** / **Don't warn me
  again**. The second calls `PUT /me/preferences` and the note never comes back.
- `Settings › Preferences` has the same toggle.

### Verified

- Backend: **49 unit + 106 integration** green. New tests: exercise search
  returns muscles/equipment (Bench Press → primary Chest, secondary Triceps);
  `GET /muscles` + day focus round-trip dropping unknown ids; `/me/preferences`
  toggle round-trips via `GET /me` and 409s without a profile.
- Frontend: **68 tests**, lint, build clean. New DayEditor test: set a focus →
  picker filters → "Show all" → add off-focus → note appears → focus id `[2]` in
  the save body.
- Live smoke: `GET /muscles` → 15; day PUT `focusMuscleIds: [4, 999]` → stored
  `[4]`; `PUT /me/preferences {false}` → `GET /me` shows `false`.

## Slice 2c — wger illustration images (`272fb64`)

The catalogue had no pictures. wger publishes CC-BY-SA "main" images for about a
third of exercises (many recent ones are AI-generated — skipped those).

- **Domain:** `Exercise.ImageUrl` / `ImageThumbUrl` / `ImageAttribution` (all
  nullable) + migration (`AddExerciseImages`, three `character varying` columns).
- **The enrichment tool.** The seed is `backend/seed-data/wger-catalogue.json`,
  committed so `import` needs no network. Rather than re-`fetch` the whole
  catalogue (which would churn names/muscles too), there's a new command that
  touches *only* the image fields:
  ```
  dotnet run --project backend/MySelf.Tools.WgerImport -- enrich-images
  ```
  It reads the existing snapshot, pulls
  `wger.de/api/v2/exerciseimage/?is_main=true` (paginated), matches each image's
  `exercise_uuid` to our `ExternalId`, and rewrites the JSON with
  `record ... with { ImageUrl = ..., ImageThumbUrl = ..., ImageAttribution = ... }`.
  Result: **236 / 862** exercises (27%) get an illustration.

  C# note: `SnapshotExercise` is a positional `record`, so `e with { ImageUrl = x }`
  makes a copy with one field changed — records are immutable by default, `with`
  is the idiomatic "change one thing" operator (like `{...obj, imageUrl: x}` in JS
  but type-checked and shallow-cloned for you).

- **`CatalogueImporter`** copies the three fields onto the entity;
  `dotnet run ... -- import` reloads the enriched seed (idempotent).
- **API:** `ExerciseListItem` gained `imageThumbUrl` / `imageUrl` /
  `imageAttribution` via the same shared projection.
- **Frontend:** `ExercisePicker` rows show a 36px `object-cover` thumbnail when
  present; `onError` hides it if wger's CDN doesn't answer. We hotlink wger's
  media URLs (no download/host step) — the trade-off is a dependency on their CDN
  staying up.

Attribution string: `"{author} · wger.de (CC BY-SA)"`, or just
`"wger.de (CC BY-SA)"` when the image has no listed author.

### Verified (2c)

- Backend: 49 unit + 106 integration (the exercise-search test now also asserts a
  broad "squat" search returns at least one `http…` thumbnail + a non-empty
  attribution).
- Frontend: 68 tests (DayEditor test asserts the Back Squat row renders its
  `<img src>`).
- Live: `GET /exercises?q=squat` → 7 / 20 rows carry a real wger thumbnail URL.

## Next

- **Slice 3** — the gym/phone logging redesign (current exercise expanded, rest
  collapsed, prefilled sets + `+/-` steppers + big Done, "last time" inline,
  auto-advance).
- **Slice 4** — persistent Resume-workout bar everywhere; mobile bottom tab bar;
  per-exercise progression + "stalled" flag + under-trained-muscle hint in the
  drill-down.

## Slice 3 — the gym logging screen, rebuilt (`a18f13e`)

The active-workout screen was a flat scroll of every exercise with a number box
per field and three buttons per set. Rebuilt for a phone between sets:

- **One exercise open at a time.** The rest collapse to a tap-to-open row with a
  `done/total` badge. When you finish an exercise's sets it auto-expands the next.
- **Sets come pre-filled.** Priority: a value you already logged → the previous
  set *this* session → the day's target → last session's top set → blank. So a
  straight-sets workout is mostly just tapping **Log**.
- **Steppers, not typing.** New `<StepperInput>` — `−  100  +` with big buttons
  (weight ±2.5, reps ±1, seconds ±5); the middle stays a real input you can type
  into. One primary **Log** button ("Update" once logged). After logging, focus
  jumps to the next pending set's first field.
- "Last time: 100 kg × 8" once per exercise; a thin progress bar up top.
- The old "Copy previous" button is gone — that behaviour is the default now.

React note: the "which exercise is expanded" state is seeded from the data
(`firstUnfinished(exercises)`) but the user can override by tapping a row. The
component reconciles: `active = exercises.some(e => e.id === activeId) ? activeId
: firstUnfinished(...)` — so a stale id (exercise removed) falls back cleanly.

## Slice 4 — navigation + a weakness signal (`cfed1e6`, `6599b67`)

**Resume-workout bar.** A fixed pill rendered by `AppShell` on every screen while
a session is `InProgress` (`useActiveSession()` has data), showing the day name +
sets done/total, linking to `/workouts/active`. Hidden on that screen itself.

**Mobile bottom tab bar.** The primary nav used to be a scrolling icon row at the
top on phones. Now `AppShell` renders the sidebar nav `hidden nav:flex` (desktop
only) and a separate `position: fixed` bottom `<nav>` `nav:hidden` with the five
icon+label items. `<main>` gets `pb-[calc(4.5rem+env(safe-area-inset-bottom))]`
so content clears it.

**Muscle coverage** (the "don't skip leg day" signal). `GET
/programs/{id}/stats` gained `muscleWeeklySets`: for every completed session
started from the program, it joins each logged exercise to its **primary**
muscles and tallies completed sets, then divides by `WeeksInRange` (the same
1–8-week divisor the rolling averages use, now exposed on
`ProgramStatsResult`). The Overview shows a bar per muscle, lowest first,
warning-toned under ~6 sets/week.

C# note: the tally is plain dictionary work after the query —
`setsByMuscle[m] = setsByMuscle.GetValueOrDefault(m) + completedSets`. The only
DB round-trip is loading `exerciseId → primary muscle names` for the exercises
that actually appear in the sessions.

Deferred: a per-exercise "stalled lift" chip in the day editor. The strength
trend (e1RM sparkline, per exercise) already lives on the Progress screen; a
compact version in the editor is a nice-to-have, not blocking.

### Phase 4 verified

- Backend: **49 unit + 107 integration** green (new: `/muscles` + day focus
  round-trip, `/me/preferences` toggle, exercise search muscles/equipment/image,
  program-stats sets-per-muscle).
- Frontend: **71 tests**, lint, `npm run build` all clean (new suites:
  `ResumeWorkoutBar`, Train-home; rewritten: `ActiveWorkoutScreen`, `DayEditor`,
  `WorkoutBuilderScreen`).
- Branch `phase-4-workouts-home`, ~14 commits. Ready for review, then merge to
  `master`.
