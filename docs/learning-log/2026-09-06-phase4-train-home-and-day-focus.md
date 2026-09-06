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

## Next

- **Slice 2c** — wger image enrichment: `Exercise.ImageUrl` + attribution +
  migration, a fetch pass in `MySelf.Tools.WgerImport`, thumbnails in the picker
  and exercise detail.
- **Slice 3** — the gym/phone logging redesign (current exercise expanded, rest
  collapsed, prefilled sets + `+/-` steppers + big Done, "last time" inline,
  auto-advance).
- **Slice 4** — persistent Resume-workout bar everywhere; mobile bottom tab bar;
  per-exercise progression + "stalled" flag + under-trained-muscle hint in the
  drill-down.
