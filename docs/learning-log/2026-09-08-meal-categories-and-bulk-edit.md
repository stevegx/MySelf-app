# 2026-09-08 — Customizable meal categories + multi-select bulk edit (Phase 5, slice 6)

Branch `phase-4-workouts-home`. Commit `fc6a24f`. This closes Phase 5: the two
features left after slices 1–5 (docs/08 decision #19, and Story 7 / #256–261).

Both shipped in one commit because they interlock at the file level — the bulk
move/copy pickers read the same live category list — and every intermediate
state has to build and stay green.

## Part 1 — customizable meal categories

Until now the four slots (`Breakfast`/`Lunch`/`Dinner`/`Snacks`) were a
`static readonly string[]` copied into two endpoint classes.

### The model choice: denormalised string, not a foreign key

`MealCategory` is a new table, but `MealLog.Category` and `SavedMeal.Category`
**stay plain strings**. A category is not a parent of the logs filed under it.
Why:

- Renaming "Snacks" → "Evening" must not rewrite last month's logs.
- Archiving a category must not orphan or cascade-delete history.

So the table only answers "which slots does this user want, and in what order".
`MealLog` keeps the name it was filed under, by value, forever.

New entity — `MealCategory { Id, UserId, Name, SortOrder, ArchivedAt?, CreatedAt }`,
table `meal_categories`. Two indexes:

- `(UserId, SortOrder)` — every read is "this user's slots, in order".
- `(UserId, Name)` **unique, filtered** `WHERE "ArchivedAt" IS NULL` — two *active*
  slots can't share a name, but archive-then-recreate-same-name is fine. This is a
  Postgres *partial index*; in EF you write
  `builder.HasIndex(...).IsUnique().HasFilter("\"ArchivedAt\" IS NULL")`. Same
  trick the workout sessions use for "one in-progress session per user".

### Lazy seeding

`MealCategoryService.EnsureSeededAsync` — `if (!await db.MealCategories.AnyAsync(userId))`
insert the four defaults. Every category-aware entry point calls it first:
`GET /meal-categories`, `BuildDayAsync`, `AddItemAsync`, the saved-meal endpoints.
An account created before this migration gets its four rows the first time it
loads the Nutrition screen — no data migration, no backfill script.

New .NET note: a `GET` handler that writes on first touch is a deliberate
*lazy-provisioning* pattern. It's safe here because the write is a one-time
idempotent seed guarded by `AnyAsync`; it is not something to reach for casually.

### Endpoints — `MealCategoryEndpoints`

| Route | Does |
|---|---|
| `GET /api/v1/meal-categories` | active slots, ordered; seeds on first call |
| `POST` | add one — trimmed, 1–40 chars, no active-name clash, cap 12, `SortOrder = max+1` |
| `PUT /{id}` | rename (same validation) |
| `PUT /reorder` | body `{ ids: [] }` — must list every active id exactly once; sets `SortOrder` by position |
| `DELETE /{id}` | archive; **refuses if it's the last active slot** |

### `BuildDayAsync` — active slots + orphans

```csharp
var activeCategories = await MealCategoryService.ActiveNamesAsync(db, userId, clock, ct);
var orphanCategories = logs
    .Select(l => l.Category)
    .Where(c => !activeCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
    .Distinct().OrderBy(c => c).ToList();
var meals = activeCategories.Concat(orphanCategories).Select(BuildSlot).ToList();
```

If you rename "Snacks" to "Evening" after logging a yoghurt into it, the day now
shows **both**: "Evening" (active, empty) and "Snacks" (orphan, with the yoghurt,
still editable). Nothing is hidden and nothing is silently moved.

`BuildDayAsync` gained a `TimeProvider clock` parameter (needed for the seed);
every caller already had one injected or now passes it through.

### Frontend

- `MealCategory` was a union of the four literals; it's now just `string`. That
  widens cleanly — every `useState<MealCategory>` / `Segmented<MealCategory>` /
  prop still type-checks. `MEAL_CATEGORIES` stays as the seed list / test fixture.
- `useMealCategories()` + `useCreateMealCategory` / `useRenameMealCategory` /
  `useReorderMealCategories` / `useDeleteMealCategory`. Each invalidates
  `["meal-categories"]` **and** `["nutrition-day"]` (prefix match — the day's
  slots come from the same list).
- `ManageCategoriesDialog` (new `MealCategories.tsx`) behind a "Manage" button in
  the Nutrition header: inline-rename on blur, ▲▼ reorder, add, archive (disabled
  at one row).
- The day screen itself needed no slot logic — it already renders `day.meals`
  straight from the server.

## Part 2 — multi-select copy / move / duplicate / delete + undo

Scope: the logged items on a nutrition day (select across meal cards).

### Endpoints — `MealItemBulkEndpoints`, all under `/nutrition-days/{date}/items/`

| Route | Semantics |
|---|---|
| `POST bulk-delete` `{ ids }` | remove items, drop emptied `MealLog`s |
| `POST bulk-move` `{ ids, toCategory }` | relocate within the same date — **keeps the item id**, one `SaveChanges` (transactional) |
| `POST bulk-copy` `{ ids, toCategory, toDate? }` | **new ids**, snapshot fields copied verbatim, appended; `toDate` can be another day |
| `POST bulk-add` `{ items: [...] }` | re-insert exact snapshots with fresh ids — the undo primitive for delete |

Shared helper `LoadPickedAsync` loads the items with
`.Where(i => wanted.Contains(i.Id) && i.MealLog.UserId == userId && i.MealLog.LocalDate == day)`
and returns a `ValidationProblem` if any id is missing / not the caller's / not on
that day — so ownership and scope are one check.

### The move gotcha (again, but the mirror image)

Slice 1 taught: adding a *new* child to a tracked parent collection with the FK
pre-set makes EF issue an UPDATE instead of an INSERT. Move is the opposite and
*safe*: the items are **existing tracked entities**, so setting
`item.MealLogId = target.Id` is exactly the UPDATE we want. The subtlety here was
`DropEmptiedLogs` — it must skip the move target, or a selection that includes an
item already in the destination slot would queue that slot's `MealLog` for
deletion right after we reassigned items into it. Hence the `keepLogId` parameter.

### Undo — client-side, no server tombstones

The client already holds every selected item's full payload. On delete it stashes
`RestoreMealItemInput[]` (category + all nutrient figures), fires `bulk-delete`,
then shows an `<UndoBar>` (`Removed 3 items · Undo`, auto-dismiss 8 s via
`setTimeout` in a `useEffect`). Undo → `bulk-add` replays the snapshots exactly.
Undo of a **move** groups the affected items by their pre-move slot and issues one
`bulk-move` back per slot.

Copy / Duplicate get no undo — they're additive; you undo them by selecting the
copies and deleting.

### Frontend shape

`NutritionScreen` gained a `selecting` boolean + `selected: Set<string>`. In
select mode each `ItemRow` renders a `<Checkbox>` instead of the amount editor,
and a fixed bottom action bar shows `N selected · Move · Copy to · Duplicate ·
Delete`. `PickSlotDialog` is a tiny shared modal listing the live categories,
reused for Move and Copy.

## Tests

- `MealCategoryEndpointTests` (6): seed-on-first-GET, added category becomes a day
  slot and accepts a log, duplicate-name rejected, rename leaves history as an
  orphan slot, archive drops the slot / last one refused, reorder drives day order.
- `MealItemBulkEndpointTests` (6): delete empties the slot, move relocates + keeps
  id + clears source, copy makes new ids, copy targets another day, bulk-add
  restores verbatim, cross-user ids rejected.
- `NutritionScreen.test.tsx` (+3): add a category from the manage dialog,
  bulk-delete two items then Undo → `bulk-add`, bulk-move one item to another slot.

Full suite green: **103 frontend, 60 backend unit, 140 backend integration.**

## Phase 5 is done

Remaining nutrition backlog is all explicitly deferred: USDA generic text search
("after the MVP flow is stable"), and the aggregated `GET /dashboard?date=`
endpoint (the dashboard composes client-side fine, and slice 5 wired the real
nutrition numbers straight in).
