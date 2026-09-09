# 2026-09-08 — Export & delete my data (Phase 6, slice 1)

Branch `phase-6-polish-release` (off `master` `b4c7a19`, which now carries
Phases 0–5). Commit `1687499`.

First Phase 6 slice. Requirement: `docs/05` §13 *"User can export and delete
their data"*, `docs/01` *"Data export σε CSV/JSON και account deletion"*.

## `GET /api/v1/me/export`

One indented JSON document with everything the account owns:

- account (id / username / email), profile, goals, body measurements
- meal categories, meal logs + items, custom foods, saved meals + items
- workout programs → days → prescribed exercises → sets
- workout sessions → logged exercises → logged sets
- personal records

### Why an explicit DTO tree

`DataExportContracts.cs` is ~18 flat records — `ExportMealLog`, `ExportProgram`,
`ExportLoggedSet`, etc. It would have been fewer lines to serialize the loaded
domain entities directly, but a data-portability export is a **contract**: a user
or a script reads this file and depends on its shape. Tying it to the domain
entities would mean an unrelated refactor silently changes the export, and EF
navigation properties (`MealLogItem.MealLog`, `SetPrescription.DayExercise`) would
either serialize as cycles or need `ReferenceHandler.IgnoreCycles`, which emits
half-populated parent objects. Explicit records also let enums go out as readable
strings (`"Per100g"`, `"Completed"`) instead of integers.

### Mechanics

Each section is an `AsNoTracking` query with a `.Select` straight into the export
record. Sessions already snapshot `ExerciseName` on `ExerciseLog`, so those are
free; prescribed exercises need `.Include(e => e.Exercise)` for the name.
Personal records `.Join` the exercises table for the name.

Returned with `Results.File(bytes, "application/json", "myself-export-2026-09-08.json")`
— `Results.File` with a filename sets `Content-Disposition: attachment`, so the
browser saves it rather than rendering it. Rate-limited under `WritePolicy` (it's
a heavy read).

New .NET notes for a JS dev:
- `JsonSerializer.SerializeToUtf8Bytes(obj, options)` — serialize straight to a
  `byte[]`, no intermediate string.
- `new JsonSerializerOptions(JsonSerializerDefaults.Web)` — the same casing
  (camelCase) / case-insensitive rules ASP.NET uses for request/response bodies;
  `WriteIndented = true` makes the downloaded file human-readable.
- `Results.File` vs `Results.Json` — `File` is the one that triggers a download.

## `DELETE /api/v1/me`

Hard-deletes the account. The interesting part is what it does *not* have to do:

- **The FK cascade does the work.** Every user-owned table
  (`workout_programs`, `workout_sessions`, `user_goals`, `user_profiles`,
  `meal_logs`, `meal_categories`, `custom_foods`, `saved_meals`,
  `body_measurements`, `nutrition_estimate_snapshots`) was configured with
  `.OnDelete(DeleteBehavior.Cascade)` against `AspNetUsers`, so the migrations
  created real `ON DELETE CASCADE` constraints in Postgres. Deleting the one
  `AspNetUsers` row removes all of it. (The test-cleanup helper
  `DeleteUsersAsync` has quietly relied on this for the whole project.)
- **Refresh tokens are the exception** — `RefreshToken` deliberately has *no*
  navigation to `ApplicationUser` (Domain can't reference the Identity type), so
  no FK, so no cascade. They're cleared explicitly first:
  `await db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct)`.

Then `UserManager.DeleteAsync(user)` (Identity's own delete, which runs the
cascade), the refresh cookie is dropped with the same attributes AuthEndpoints
set it with (`Path = "/api/v1/auth"` matters — the browser won't clear a cookie
whose path doesn't match), and it returns `204`.

A validly-signed JWT for the now-deleted user still passes signature/lifetime
checks, so `GET /me` would still reach the handler — but `GetMeAsync` already
does `FindByIdAsync` → null → `401`, which is the behaviour we want.

New .NET note: `ExecuteDeleteAsync` issues a single `DELETE … WHERE …` and does
**not** load entities into the change tracker — the right tool for "remove all
rows matching a predicate".

## Frontend — Settings › "Your data"

- **Export** button → `useExportMyData` calls `apiFetch` (reusing its auth +
  error handling), then `new Blob([JSON.stringify(data, null, 2)])` +
  `URL.createObjectURL` + a temporary `<a download>` clicked and removed. A
  normal web app can start a client-side download like this (the Artifact sandbox
  restriction doesn't apply here).
- **Delete account** → a `type "DELETE" to confirm` gate; the confirm button is
  `disabled` until `confirmText === "DELETE"` (case-sensitive). `useDeleteAccount`
  → `DELETE /api/v1/me` → `onSuccess: setSession(null)`, and `RequireAuth`
  redirects to `/login` on the next render — no explicit `navigate()`, same
  pattern as `useLogout`.

## Tests

- `AccountDataEndpointTests` (5): export contains the caller's logged meal + a
  created program; export is caller-scoped (bob's export has no meal logs after
  alice logs one); both endpoints `401` without a token; delete → `204`, then the
  stale token `401`s on `/me`, re-login `401`s, and a scoped `DbContext` check
  confirms the user row, its meal logs and its refresh tokens are all gone.
- `SettingsScreen.test.tsx` (+2): Export calls `GET /me/export` and triggers
  `URL.createObjectURL` + an anchor click (both stubbed — jsdom has neither);
  Delete stays disabled until "DELETE" is typed exactly, then calls `DELETE /me`
  and clears the session.

Full suite green: **105 frontend, 60 backend unit, 145 backend integration.**

## Slice 1b — Excel export (`9e9f31d`)

`GET /api/v1/me/export?format=xlsx` returns a formatted `.xlsx` built from the
*same* `DataExport` object; JSON stays the default (no `format`, or
`?format=json`).

- **ClosedXML** (MIT, wraps the OpenXML SDK) added to `MySelf.Api` and
  `MySelf.IntegrationTests`. EPPlus was avoided — it went non-free (Polyform
  Noncommercial) at v5.
- `DataExportSpreadsheet.Build(DataExport) → byte[]`. Three sheets:
  - **Summary** — title, account, export date, and a label/value block of
    counts (meals logged, food-log days, sessions, PRs, programs), calorie
    target, latest body weight.
  - **Nutrition** — one row per logged food; a ClosedXML *table*
    (`range.CreateTable()`, `Theme = TableStyleMedium2`) gives banding + filter
    dropdowns for free; `SetShowTotalsRow(true)` +
    `table.Field("Calories").TotalsRowFunction = XLTotalsRowFunction.Sum` adds a
    summed footer. Frozen header row, `yyyy-mm-dd` / `#,##0` / `0.0` number
    formats, `Columns(...).AdjustToContents()`.
  - **Workouts** — one row per logged set, same table treatment.
- `Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "myself-export-2026-09-08.xlsx")`.

New .NET / ClosedXML notes:
- A worksheet range → table: `ws.Range(r1,c1,r2,c2).CreateTable("Name")`; the
  first row becomes the header, so you control the header text (unlike
  `InsertTable(IEnumerable<T>)` which uses property names — no spaces allowed).
- `XLCellValue` takes `string`/`double`/`decimal`/`bool`/`DateTime` implicitly;
  `DateOnly` is converted with `.ToDateTime(TimeOnly.MinValue)` and shown via a
  `yyyy-mm-dd` number format.
- `AdjustToContents()` measures text with SixLabors.Fonts — that's the transitive
  dependency ClosedXML pulls in.

Frontend: the binary file can't go through `apiFetch` (it parses JSON), so
`downloadExport()` does a raw `fetch` → `res.blob()` → object-URL anchor click.
`API_BASE_URL` is now exported from `lib/api.ts`. Settings › "Your data" leads
with **"Export to Excel"**; a small **"raw JSON"** link keeps the complete
machine-readable export one click away.

### Slice 1c — the workbook reworked as a check-in (`f1365ee`)

The first cut was a 3-sheet dump. This turns it into a 7-sheet review workbook
driven by a new pure `CheckInModel` (`DataExportCheckInModel.cs`) computed over
the `DataExport` — no new data, all derived:

| Sheet | What it answers |
|---|---|
| **Overview** | one screen: last-7-day avg calories / protein / **protein per kg** / **calorie adherence** (% of days within ±10% of target) / macro split; 28-day training volume, sets, sessions, avg duration, PRs; body-weight latest / 7-day avg / **weekly rate** / total change — with green/amber/red flags |
| **Nutrition — Daily** | per day: calories vs target (Δ, % of target), macros, macro %, items, status; data bar on calories, **diverging colour scale** centred on 100% of target; totals row shows the averages; a weekly-averages table sits alongside |
| **Nutrition — Food log** | every logged item (the old detail sheet, kept) |
| **Training — Sessions** | per session: duration, working sets, **volume = Σ(weight × reps)**, top set, **best e1RM** (Epley: `w × (1 + reps/30)`); data bars on volume + e1RM |
| **Training — Exercises** | progression per lift: sessions, best weight, best e1RM, best session volume, first/last done |
| **Training — Set log** | every set with its computed e1RM (the old detail sheet + a column) |
| **Body weight** | daily weight, **7-day rolling average**, Δ vs 7 days ago, weekly rate |

ClosedXML **cannot emit native charts** — a real limitation. Two ways around it:
(1) every sheet is a single contiguous, header-first table, so *"select it and
press Alt+F1"* gives an instant Excel chart; (2) trends are shown with **data-bar
and colour-scale conditional formatting** (`range.AddConditionalFormat().DataBar(color)`,
`.ColorScale().LowestValue(..).Midpoint(XLCFContentType.Number, "1", ..).HighestValue(..)`)
plus red/amber/green cell fills set in code.

New C# gotchas hit on the way:
- A positional `record` property and a `private static` method can't share a name
  (`BestDayLabel`) — `CS0102`.
- `x ? Math.Round(d) : null` where the true branch is `decimal` needs
  `: (decimal?)null` — `CS0173` otherwise.
- `decimal?` vs a `double` literal (`>= 0.8`) → `CS0019`; write `0.8m`.
- A `(string, int)` tuple literal won't implicitly convert to a declared
  `(string Label, byte Rank)` return type — cast the literal `(byte)0`.

## Not in these slices (deferred)

CSV variant, a soft-delete grace period / "download then delete" flow, and
emailed/scheduled exports.
