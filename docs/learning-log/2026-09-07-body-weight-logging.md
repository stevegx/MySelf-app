# 2026-09-07 — Body-weight logging (Phase 4)

Branch `phase-4-workouts-home`. Backend commit `57816ca`, frontend `928cdfe`.

The Progress screen's "Body weight" tab said *"Coming in a later phase"* and
the dashboard card said *"No weight logs yet"* with nothing behind either.
This is the concrete Phase 4 gap — filled here. (The other Phase 4 roadmap
bullets — the aggregated `GET /dashboard` endpoint, light gamification,
context-aware theming — were deliberately left: the first has no nutrition
data to aggregate until Phase 5, the last two are big and need their own
design pass with colours unfrozen.)

## Slice 1 — backend (`57816ca`)

**`BodyMeasurement`** — a flat entity: `WeightKg` (decimal, never float —
docs/04), `LocalDate` (the user's own calendar day, like
`WorkoutSession.PerformedOnLocalDate`), `MeasuredAt` (UTC). `UserId` is a
plain Guid + index, FK declared in the configuration, no navigation property
— the same shape as `UserGoal` / `RefreshToken`. Migration
`20260907160452_AddBodyMeasurements`, applied to the dev and test DBs.
Multiple readings per day are allowed (locked decision #30).

**Endpoints** (`BodyMeasurementEndpoints`, mapped in `Program.cs`):

| Route | Does |
|---|---|
| `POST /api/v1/me/body-measurements` | log one reading; weight validated 20–500 kg; `localDate` defaults to today |
| `GET /api/v1/me/body-measurements?from=&to=` | the raw list, newest first (cap 400) |
| `DELETE /api/v1/me/body-measurements/{id}` | remove a mistaken entry — only the caller's own |
| `GET /api/v1/analytics/weight?from=&to=` | the trend: daily average → 7-day rolling average, plus a headline latest + 7-day change |

**`WeightTrendCalculator`** — a *pure* domain calculator (no DB, no clock),
unit-tested like `ProgramStatsCalculator`:

1. collapse readings to one point per calendar day (the mean of that day's
   readings),
2. for each daily point, the rolling average = mean of the daily points in
   the trailing 7 calendar days (this day included), so early points just
   track the daily line until a week of history exists,
3. `SevenDayChange` = the last point's rolling average minus the rolling
   average of whichever earlier point sits closest to 7 days before it.

New .NET notes for a JS dev:
- `readonly record struct Reading(...)` — a value type with structural
  equality, ideal for a small immutable input row (no heap allocation per
  reading).
- `DateOnly.DayNumber` gives an integer day index, handy for "closest to N
  days ago" without `TimeSpan` arithmetic.
- `Math.Round(x, 2)` on `decimal` is exact (no binary-float surprises) —
  which is why weight is `decimal` end to end.

Tests: 6 unit (`WeightTrendCalculatorTests` — day averaging, the rolling
window, gaps wider than the window, the 7-day change, single-point → null),
4 integration (`BodyMeasurementEndpointTests` — log/list newest-first,
day-average + rolling, out-of-range rejection, per-owner delete).

## Slice 2 — frontend (`928cdfe`)

- **`progress/api.ts`** — `useBodyMeasurements`, `useWeightTrend`,
  `useLogWeight`, `useDeleteWeight`; the two mutations invalidate both
  queries.
- **`LogWeightDialog`** — a centred modal (weight number input + a
  back-datable date, default today). Mounted only while open
  (`{showing && <LogWeightDialog … />}`) so each open is a fresh component —
  no reset-in-`useEffect` (which the `react-hooks/set-state-in-effect` lint
  rule rejects). Used by both the Progress tab and the dashboard header.
- **Progress → Body weight tab** — headline latest + a coloured
  `▼/▲/→ N kg / 7 days`, a two-line SVG chart (faint `--color-foreground-
  subtle` daily average, bold `--color-primary` 7-day rolling) with a
  legend, and a recent-readings list with a delete icon per row. The two
  lines are a measure and its smoothed self, so a muted/emphasis pair — not
  two competing categorical hues.
- **Dashboard "Body weight" card** — latest + 7-day direction + a mini
  rolling-average sparkline, falling back to the log prompt when empty. The
  header and card "Log weight" buttons now open the dialog instead of
  routing to `/progress`.

Tests: 2 `ProgressScreen` (log from the empty state → POST fires with the
right weight; trend headline + legend render, and a row delete fires DELETE).

## Verification

- Backend `dotnet test MySelf.sln` — **55 unit + 112 integration**, green.
- Frontend `npm run build` + `npm run lint` clean; `npx vitest run` —
  **91 pass**.
- Browser: logged 82.5 kg (today) and 84 kg (back-dated a week) → the
  headline showed `82.5 kg ▼ 0.8 kg / 7 days`, the two-line chart drew, both
  readings listed; the dashboard card mirrored it with the sparkline;
  deleting both returned the empty state. Dev account left clean.
