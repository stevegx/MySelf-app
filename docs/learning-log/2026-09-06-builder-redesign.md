# 2026-09-06 — Program builder redesign (single screen)

Written for a web dev learning .NET. Branch: `phase-4-workouts-home`.
Frontend-only work — no C#, no migration. Commits: `69ad226`, `1d7c878`.

## Why

The old "Days" tab was a list you drilled into: click a day → a separate
full-screen `DayEditor` → back out → click the next day. The mockup
(`MySelf App UI Mockups.dc.html`) shows the whole thing on one screen: day
list on the left, the selected day's editor on the right, an exercise
picker that slides in from the edge rather than shoving the page down.

## Slice A — two-column builder (`69ad226`)

`ProgramDetail`'s **Days** tab is now a two-column layout:

- **Left**: a `SortableList` of the program's days (drag to reorder →
  `PATCH /programs/{id}` with the new `dayOrder`), each row a button that
  sets `selectedDayId`, plus an icon-only delete (styled confirm dialog)
  and a "New day…" input.
- **Right**: `<DayEditor>` for the selected day, or a placeholder card.

`ProgramDetail` went from `useState<"overview" | "days">("overview")` to
defaulting to `"days"` — opening a program lands you where you edit.
`selectedDay` falls back to `days[0]` so the right pane is never empty when
days exist. The old `WorkoutBuilderScreen` `dayId` state and the
`if (programId && dayId) return <DayEditor .../>` branch are gone.

New concept vs. the old flow: `DayEditor` now takes an **`onSaved`** callback
separate from `onClose`. Full-screen use passed only `onClose`, so saving
always closed the editor. Inline, we want to save and *stay* on the day, so
`save()` now resets the dirty baseline (`setBaseline(fingerprint(state))`)
and calls `(onSaved ?? onClose)()`. The inline builder passes `onSaved={() => {}}`.

## Slice B — collapsible exercise rows

Each exercise in `DayEditor` starts **collapsed**: a summary row showing the
name and tags like `3 sets · 8–12 reps · rest 90s` (computed by a new
`summaryTags()` pure helper). Click the row (it's a `<button aria-expanded>`
with a chevron that rotates) to expand the full per-set editor. Freshly
added exercises start expanded (`collapsed: false` in `addExercise`), seeded
ones start collapsed. `collapsed` is UI-only state — it never goes to the
server and isn't part of the dirty `fingerprint`.

This is the same "summary until you need the detail" idea as a `<details>`
element, done by hand so we control the summary content.

## Slice B — slide-in exercise picker

`ExercisePicker` gained a `drawer?: boolean` prop. The body is unchanged;
only the wrapper differs:

- `drawer` false (default) → the old inline `<div>` panel. Still used
  nowhere-special now but kept so the component stays flexible.
- `drawer` true → a fixed full-height panel pinned to the right
  (`fixed inset-0 flex justify-end`, inner `w-[380px]`) over a
  semi-transparent backdrop. `role="dialog" aria-modal="true"`. Click the
  backdrop or the ✕ to close; clicks inside `stopPropagation()` so they
  don't bubble to the backdrop's `onClose`.

`DayEditor` renders it as `<ExercisePicker drawer … />` and the old
"+ Add exercise" `<Button>` became an always-visible dashed drop-zone-style
`<button>`.

## Slice C — per-exercise trend sparkline

New `ExerciseTrend.tsx`: given an `exerciseId`, it reads `useExerciseHistory`
and draws a tiny inline `<svg>` polyline of estimated 1RM over time
(oldest → newest). Renders **nothing** until there are ≥ 2 data points.
Line is green when the last point ≥ the first, muted otherwise. Shown on
collapsed rows only, so the day at a glance tells you "this lift is moving".

`viewBox="0 0 72 24"` with `preserveAspectRatio="none"` +
`vectorEffect="non-scaling-stroke"` means the SVG stretches to whatever box
CSS gives it while the stroke stays a constant 2.5px — handy trick for
sparklines.

## Slice D — rotation "up next" (`1d7c878`)

The active program has no fixed weekday schedule, so "what do I train today"
is answered by rotation: **the day after whichever was performed most
recently, wrapping around**; before any session is logged, the first day.

Pure frontend, in `WorkoutsHome`:

```ts
const orderedDays = [...(detail?.days ?? [])].sort((a, b) => a.sortOrder - b.sortOrder);
// find the day with the latest lastPerformedOn (from stats.perDay),
// upNext = orderedDays[(itsIndex + 1) % orderedDays.length]
```

The matching day's `DayCard` gets an "Up next" pill + a primary ring, and
the header subtitle reads `PPL · active program · up next: Legs`. Reuses the
`SortOrder` the builder already maintains — no new field, no backend.

## Follow-up — 7 muscle groups for day focus (`f2b97b9`)

The focus chip row in `DayEditor` listed all **15 wger muscles** — Brachialis,
Serratus anterior, Obliquus externus abdominis, Soleus and friends. Fine for
*stats* granularity, far too much for *picking* what a day trains. Collapsed
to seven: Chest / Back / Shoulders / Biceps / Triceps / Legs / Core.

Key design call: this is a **UI-only** concern, so it does **not** touch the
domain. The catalogue genuinely has 15 muscles; `WorkoutDay.FocusMuscleIds`
still stores *leaf* muscle ids; the stats endpoints still report per-muscle.
The mapping is a frontend constant, `muscleGroups.ts`:

- `MUSCLE_GROUPS` — 7 `{ key, label, memberNames[] }`. A unit test asserts the
  member names partition the 15 exactly (no muscle dropped or double-counted).
- `toggleGroup(key, focusIds, muscles)` — picking "Legs" writes all five of
  its member ids at once; toggling off removes the whole set. Symmetric, so a
  round-trip leaves `FocusMuscleIds` (and the dirty `fingerprint`) unchanged.
- `activeGroupKeys(focusIds, muscles)` — a chip reads as *on* when **any** one
  of its members is in the focus set (forgiving of a partially-populated day).
- The `ExercisePicker` filter is unchanged — it still matches on leaf muscle
  *names* (`focusMuscleNames`, expanded from the ids). Only the "Showing
  exercises for …" note changed: it now shows the group labels
  (`focusLabels`) instead of a long list of leaf names.

New concept worth noting from a JS background: `as const` on the
`MUSCLE_GROUPS` array makes `memberNames` a `readonly ["Chest", ...]` tuple of
string *literals*, so `new Set(memberNames)` infers `Set<"Chest" | ...>` and
`set.has(someString)` won't compile. Fix: `new Set<string>(memberNames)`.

## Verification

- `npm run build` + `npm run lint` clean.
- `npx vitest run` — **80 pass** (72 after slices A–D, +8 `muscleGroups`).
  Reworked 4 `DayEditor` tests (expand the row before asserting on set inputs;
  picker button renamed) and 3 `WorkoutBuilderScreen` tests (default tab is
  now Days; day delete is an icon button; empty-days placeholder text). New
  tests for the rotation pick and the group mapping; the "sets a day focus"
  DayEditor test now picks the **Legs** group and checks it expands to Quads.
- Browser (slices A–C, before the muscle-groups follow-up): opened `PPL x2` →
  Days tab shows the two columns; the `Bench Press` row expands/collapses the
  set editor; **Add exercise** slides the picker in from the right with the
  focus filter working; Overview tab still renders. The 7-group chip change
  was not browser-checked — the dev servers were down at commit time — but
  it's exercised end-to-end through the real `DayEditor` in the focus test.

## New/changed files

| File | What |
|---|---|
| `WorkoutBuilderScreen.tsx` | two-column Days tab; removed `dayId` plumbing; rotation `upNext` |
| `DayEditor.tsx` | collapsible rows, `summaryTags()`, `onSaved`, drawer picker, dashed add button; 7-group focus chips |
| `ExercisePicker.tsx` | `drawer` variant (right slide-in + backdrop); `focusLabels` for the note |
| `ExerciseTrend.tsx` | **new** — inline e1RM sparkline |
| `muscleGroups.ts` | **new** — 15-muscle → 7-group map + toggle/expand helpers |
| `*.test.tsx` / `muscleGroups.test.ts` | updated for the new structure; rotation + group-mapping tests |

---

# Mockup influence, pass 2 (`af49d8d`, `4e7d5b7`, `07ab06d`, `7dab9c0`)

Second batch off the `MySelf App UI Mockups` screens. User's brief: take the
**design and appearance**, not the colours — palette stays "Balanced Indigo".

## Slice G — organic look (`af49d8d`)

Pure token/component pass:

- Added `@fontsource/caprasimo`; new `--font-display` token; h1/h2 and
  `CardTitle` render in the Caprasimo serif (weight 400 — the face is already
  chunky). Smaller UI headings (h3–h6) stay Inter for density.
- `--radius-card` 12 → 20, `--radius-control` 10 → 12, **new `--radius-pill`
  (999px)**. `rounded-control` still covers inputs/panels; buttons, `Segmented`,
  and the workout tab strips switched to `rounded-pill`. The Overview/Days
  tabs became a filled-pill toggle inside a `bg-surface-subtle` track.
- Zero colour-token edits.

## Slice F — program list as a card grid (`4e7d5b7`)

`ProgramList` was a flat row per program; now a responsive `grid` of `Card`s
(serif name, split label, `N days · N exercises`, Active/Draft badge,
full-width **Open**, then Duplicate / Delete). A pill filter row —
`All / Active / Drafts` — narrows the grid (`shown` derived from `filter`).
**No template gallery**: the user was clear ("dont suggest templates"), so the
"New program" form stays the only way in.

## Slice I — muscle-group filter pills in the drawer (`07ab06d`)

Replaced the picker's "focus filter + Show all" toggle with a pill row: `All`
plus the seven groups, in a small `FilterPill` sub-component. The picker owns
a `Set<MuscleGroupKey>` seeded from the day's focus (`focusGroupKeys` prop,
replacing `focusMuscleNames`/`focusLabels`). Filtering intersects an
exercise's `primaryMuscles` with `expandGroupsToMuscleNames([...selected])`.
`All` = clear the set = everything.

## Slice H — ⋯ menu on the day editor (`7dab9c0`)

The open day's header gets a `MoreHorizontal` button → a little dropdown:

- **Rename day** — flips the `<h3>` to an `<Input>`. The day name is now part
  of `DayEditor`'s `EditState` (seeded by `seed()`, in the dirty `fingerprint`,
  sent by `buildBody()` as `name: state.name.trim() || day.name`). No new
  endpoint — the day `PUT` already carries `name`; the rename just rides the
  normal Save.
- **Delete day** — calls an `onDeleteDay` prop. `WorkoutBuilderScreen` now has
  one `removeDay()` shared by the day-list trash icon and this menu item.

**Still pending:** *Duplicate day* (needs a `POST /workout-days/{id}/duplicate`
backend endpoint — the `CloneDay` graph-copy helper already exists in
`ProgramEndpoints`, just needs lifting to a shared spot) and the **weekly
volume bar** (design agreed in principle, waiting on: split by day vs muscle
group, planned vs logged sets).

## Verification (pass 2)

- `npm run build` + `npm run lint` clean; `npx vitest run` — **82 pass**
  (+2 DayEditor tests for the rename-saves-name path and the `onDeleteDay`
  wiring).
- Browser: Train home + programs list + builder all render with the serif
  titles / pill controls / softer cards, colours unchanged; program cards with
  the `All/Active/Drafts` filter; the drawer's `All + 7 group` pill row filters
  the list; the day `⋯` menu opens and Rename swaps in the name input.
