# 2026-09-07 — Phase 5 nutrition (slices 1–5)

Branch `phase-4-workouts-home`. Commits `d07a516`, `8277716`, `2e1041c`,
`ebd03e2`, `248a2d4`, `52cdabd`.

Phase 5 is "nutrition": log food into a day, track it against the targets
onboarding already produced, and reuse foods/meals. Built in thin slices,
each committed on its own and left runnable.

## The domain model

Everything a logged item stores is a **snapshot**. If you log "Basmati rice"
today and next week edit your saved "Basmati rice" food, today's log does not
change. This is the same idea as `MealLogItem` copying nutrient numbers off
the source rather than holding a foreign key to it — history is immutable.

- **`MealLog`** — one row per `(UserId, LocalDate, Category)`. Enforced by a
  unique index. Created lazily the first time a food lands in that slot,
  deleted when its last item is removed. `LocalDate` is a `DateOnly` (the
  user's own calendar day), like `WorkoutSession.PerformedOnLocalDate`.
- **`MealLogItem`** — the snapshot. Holds both the *per-basis* numbers
  (`BasisKcal` … per 100 g or per serving) **and** the *scaled* numbers
  (`Kcal` … for the amount actually eaten). Keeping the per-basis figures
  means an inline amount edit can rescale without re-fetching the source.
- **`CustomFood`** ("My Foods") — a reusable food the user typed once:
  name, optional brand/barcode, `ServingBasis`, per-basis nutrients, soft
  `ArchivedAt`.
- **`SavedMeal` / `SavedMealItem`** — a named template (e.g. "Rice bowl").
  Items store per-basis numbers + a `DefaultAmount`. Adding a saved meal to
  a day copies each item into a fresh `MealLogItem` snapshot, scaled by
  `DefaultAmount × multiplier`.

Two enums drive scaling: `ServingBasis` (`Per100g` | `PerServing`) and
`MealAmountUnit` (`Grams` | `Millilitres` | `Serving`).

### `MealNutrientCalculator` — a pure calculator

No DI, no clock, no DB — unit-tested in isolation like
`ProgramStatsCalculator` and `WeightTrendCalculator`.

- `readonly record struct Nutrients(Kcal, ProteinG, CarbG, FatG)` with
  `.Zero` / `.Plus` / `.Scale` / `.Rounded(int = 1)`.
- `AmountFactor(basis, unit, amount, servingSizeGrams?)` — how many
  "basis units" the eaten amount represents. Crossing mass ↔ serving
  (e.g. basis is per-serving but you ate 150 g) needs a positive
  `servingSizeGrams`, otherwise it throws `InvalidOperationException`
  which the endpoint turns into a 400 on the `servingSizeGrams` field.
- `ForAmount(perBasis, …)` = `perBasis.Scale(AmountFactor(…)).Rounded()`.

New .NET notes for a JS dev:
- `readonly record struct` — a value type with structural equality and no
  heap allocation. Good for a small immutable tuple you pass around a lot.
- switch **expressions** (`basis switch { Per100g => …, _ => … }`) return a
  value; they must be exhaustive or the compiler warns.
- `[Theory]` + `[InlineData]` with a `double` literal cast to `decimal` is
  **imprecise** — `(decimal)2.4` is `2.3999…`. For decimal cases use a
  single `[Fact]` with `2.4m` literals.

## Slice 1 — manual food logging (`d07a516`, `8277716`)

**Endpoints** (`NutritionDayEndpoints`):

| Route | Does |
|---|---|
| `GET /api/v1/nutrition-days/{date}` | the whole day: targets, totals, 4 meal slots each with items |
| `POST /api/v1/nutrition-days/{date}/items` | create-slot-if-needed **and** add one item, in one call |
| `PUT /api/v1/meal-log-items/{id}` | change the amount/unit — rescales from the stored per-basis numbers |
| `DELETE /api/v1/meal-log-items/{id}` | remove an item; removes the `MealLog` too if it was the last one |

`internal static BuildDayAsync(...)` composes the day response and is reused
by the saved-meal and (later) analytics endpoints.

### The EF gotcha that produced a 500

Adding a child to a **tracked parent collection** with the FK already set:

```csharp
mealLog.Items.Add(new MealLogItem { MealLogId = mealLog.Id, ... }); // WRONG
```

EF sees a `MealLogItem` with a populated key and classifies it **Modified**,
not **Added** → it issues an `UPDATE … WHERE Id = …` that matches 0 rows →
`DbUpdateConcurrencyException` → 500. Fix: add through the `DbSet` and set
the **navigation**, not the id:

```csharp
db.MealLogItems.Add(new MealLogItem { MealLog = mealLog, ... }); // RIGHT
```

`db.Set<T>().Add` forces the whole subgraph to `Added`.

## Slice 2 — My Foods (`2e1041c`)

`GET /api/v1/foods/search?q=` (case-insensitive `EF.Functions.ILike`),
`POST /api/v1/foods/custom`, `DELETE /api/v1/foods/custom/{id}` (soft
archive — `ArchivedAt`, never a hard delete). Index on
`(UserId, ArchivedAt, Name)` so the search hits an index and archived rows
sort out.

Frontend: the Add-food dialog gained a "Search My Foods" box; picking a
result prefills the manual form. A "Save to My Foods" checkbox on submit
fires a second, best-effort `POST /foods/custom` (`.catch(() => {})` — a
failed save must not fail the log).

## Slice 3 — barcode lookup (`ebd03e2`)

The Open Food Facts client already existed (`56c21a5`). This slice wired it
into the dialog: a "Barcode" field + "Look up" button calls
`GET /api/v1/foods/barcode/{code}`, prefills the form, pre-ticks "Save to
My Foods", and tags the source. Manual entry stays the fallback when OFF
has nothing.

## Slice 4 — Saved Meals (`248a2d4`)

`GET/POST /api/v1/saved-meals`, `GET/PUT/DELETE /api/v1/saved-meals/{id}`,
`POST /api/v1/saved-meals/{id}/add-to-day`.

`UpdateAsync` replaces the item list in **two** `SaveChanges` calls:
`RemoveRange(meal.Items)` → save → build replacements with `SavedMealId`
set → `AddRange` → save. Doing it in one save re-triggers the
tracked-collection gotcha from slice 1.

`add-to-day` copies each `SavedMealItem` into a `MealLogItem` snapshot
scaled by `DefaultAmount × multiplier` (multiplier validated `0 < m ≤ 20`),
then returns the rebuilt day via `BuildDayAsync`.

Frontend: `SavedMealsBar` (a chip strip above the meal cards — name + kcal,
hidden when empty), `AddSavedMealDialog` (0.5× / 1× / 1.5× / 2×, target
slot), `SaveMealDialog` (turn a populated slot into a template).

## Slice 5 — adherence analytics + real dashboard totals (`52cdabd`)

**`GET /api/v1/analytics/nutrition?from=&to=`** (default: last 14 days).
`SelectMany` over `MealLogs.Items`, group by `LocalDate`, sum. The
**average is over logged days only** — a day with nothing logged is omitted
from `days` and does not drag the mean toward zero. Targets come from the
latest `UserGoal` (null for a fresh user).

New .NET note: `query.SelectMany(m => m.Items, (m, i) => new { m.LocalDate, i.Kcal, … })`
is a SQL join expressed in LINQ — the second lambda is the result selector,
projecting one flat row per (log, item) pair.

Frontend:
- Progress screen has a new **"Nutrition"** tab: a per-day calories bar
  chart (inline `<div>`s, no chart library — same approach as the
  workout-frequency bars), the bar turning `warning`-coloured when a day is
  >5 % over target, plus average kcal vs target and average macros/day.
  Empty state when `daysLogged === 0`.
- The dashboard **"Nutrition today"** card was showing hardcoded `0`s. It
  now calls `useNutritionDay(today)` and renders the real logged calories +
  macros, fills the rings (`value = logged / target`), and switches its CTA
  to "Log another meal" once something is logged.

## What is left in Phase 5

- **Customizable meal categories** (locked decision #19) — right now the 4
  slots are a `static readonly string[]` in `NutritionDayEndpoints`.
- **Multi-select copy / move / duplicate / delete with undo** for meal
  items and whole nutrition days (locked decisions #256–261, #368).
- USDA generic search — explicitly "after the MVP flow is stable", deferred.

## Test coverage added

- `MySelf.UnitTests` — `MealNutrientCalculatorTests` (amount factors,
  mass↔serving crossing, negative guards).
- `MySelf.IntegrationTests/Nutrition` — day CRUD, My Foods search/save,
  saved-meal round-trip + add-to-day, `NutritionAnalyticsEndpointTests`
  (per-day totals + average over logged days, empty range → 0 days).
- `frontend` — `NutritionScreen.test.tsx` (7), `ProgressScreen.test.tsx`
  nutrition tab (2), dashboard totals still covered by the existing 2.
  Full suite: 100 frontend, 60 backend unit, 128 backend integration.
