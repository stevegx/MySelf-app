# Phase 2 — closing the gaps

Written 2026-09-03. The [Phase 2 program-builder write-up](./2026-09-02-phase2-program-builder.md)
ended with an honest list of weak spots. This session worked through that list in eleven
small, separately-committed slices. Each slice: analyse → smallest change → tests → commit.

Audience: comfortable with React/TypeScript and REST, newer to C#/EF Core/ASP.NET Core.

One decision up front: **custom (user-created) exercises are not being built.** The builder
stays catalogue-only for the MVP. Everything below is about hardening and finishing what was
already there.

---

## 1. Input caps and range checks (`WorkoutLimits.cs`)

**Problem:** nothing stopped a script creating 10,000 programs, or sending a multi-megabyte
`?q=` search term, or a `restSeconds` of two billion.

**Change:** one `WorkoutLimits` static class holds every cap. The endpoints check them:

- `GET /exercises` — `q` longer than 100 chars → `400`.
- `POST /programs` / `…/groups` / `…/variants` → `409 Conflict` once the caller hits
  50 programs / 30 groups / 20 variants.
- `PUT /workout-variants/{id}` — every numeric field is now range-checked
  (`restSeconds` 0–3600, `targetRir` 0–10, sort orders, estimated duration, …).

**.NET notes:**

- `409 Conflict` vs `400 Bad Request`: `400` means "your request is malformed"; `409` means
  "the request is fine, but the resource state won't allow it". Hitting a cap is `409`.
- `Results.Problem(statusCode: 409, …)` produces the same RFC 7807 JSON shape
  (`type`/`title`/`status`/`detail`) as the existing `Results.ValidationProblem(…)`. The
  frontend's `ApiError` already normalises both.
- The cap check is `CountAsync()` (→ `SELECT count(*)`) *before* the insert. Two parallel
  `POST`s at exactly the limit could both pass and produce 51 rows — but the worst case is
  "one row over", not corruption, so a DB constraint isn't worth it here.

## 2. Rate limiting (`RateLimiting.cs`)

**Problem:** no rate limiting anywhere — the auth endpoints included.

**Change:** ASP.NET Core's built-in limiter (`builder.Services.AddRateLimiter(…)`, no NuGet
package — it's in the framework). Three layers:

- **Global**: 300 requests/minute per caller (JWT user id, or IP when anonymous). `/health`
  is exempt.
- **`"auth"` policy** (20/min per IP) on `/api/v1/auth/*` — the credential-stuffing target.
- **`"write"` policy** (90/min per user) on the program/group/variant endpoint groups.

Limits are bound from a `RateLimiting` config section (`RateLimitOptions`), so they're
tunable without a rebuild. `OnRejected` returns `429` with a `Retry-After` header.

**.NET notes:**

- A *partitioned* limiter keeps a separate token bucket per key (per user / per IP), so one
  noisy caller can't spend everyone else's budget. `RateLimitPartition.GetFixedWindowLimiter`.
- `.RequireRateLimiting("write")` chained onto a `MapGroup(…)` applies the policy to every
  endpoint in that group — same pattern as `.RequireAuthorization()`.
- The integration suite runs with `RateLimiting__Enabled=false` (set by a
  `[ModuleInitializer]` in the test project) because the whole suite hammers one in-process
  server from one loopback address and would trip the per-IP `auth` limit. One dedicated
  test re-enables it on its own factory with a tiny budget and asserts the `429`.
- `[ModuleInitializer]` is a C# method attribute — the runtime calls that method once when
  the assembly loads, before any test. Rough analogue: a top-level side effect in a JS
  module that everything else imports.

## 3. One place for the ownership filter (`OwnedWorkouts.cs`)

**Problem:** every handler re-wrote `.Where(x => x.Group.Program.UserId == userId)` by hand.
One forgotten filter on a future endpoint = an IDOR hole (reading someone else's data).

**Change:** `db.OwnedPrograms(userId)` / `OwnedGroups(userId)` / `OwnedVariants(userId)` —
each returns an `IQueryable<T>` already filtered to the caller. Handlers start from one of
these and still chain `.Include(…)` / `.Where(x => x.Id == id)` / `.FirstOrDefaultAsync(…)`
as before. No behaviour change; the ownership tests are the regression guard.

**.NET note:** `IQueryable<T>` is a *lazy query description*, not results. Returning one from
a helper and adding more `.Where`/`.Include` downstream just builds up the expression tree;
EF turns the whole thing into **one** SQL statement when you `await` a terminal operator
(`ToListAsync`, `FirstOrDefaultAsync`, …). This is why the helper composes for free.

## 4. Enforcing the `xmin` concurrency token

**Problem:** the token was stored (`WorkoutProgram.RowVersion` → Postgres' system `xmin`
column) but never checked, so two tabs editing the same program silently overwrote each
other.

**Change:** it's now enforced on `PUT /programs/{id}` **and** `PUT /workout-variants/{id}`.
The token lives only on the **program** (the aggregate root, matching docs/04's wording).
Editing any variant bumps the program's version.

`TrySaveWithRowVersionAsync(db, program, clientRowVersion)`:

- If the client sent no token → save unconditionally (backward compatible, last-write-wins).
- If it sent one → `db.Entry(program).Property(p => p.RowVersion).OriginalValue = clientRowVersion`
  and force `EntityState.Modified`, so EF emits
  `UPDATE workout_programs … WHERE "Id" = @id AND xmin = @original`. Zero rows affected →
  `DbUpdateConcurrencyException` → the endpoint returns `409` with a "reload and reapply"
  message. The frontend's `VariantEditor` shows that message on a `409`.

**Why the token isn't also on the variant row:** Npgsql's migration model-differ throws a
false `PendingModelChangesWarning` forever if you add `xmin` to a table that already exists
(it's fine only inside the original `CreateTable`). Rather than suppress that safety check
project-wide, the program-level token does the job — and "any edit in the tree moves the
program version" is arguably the cleaner model anyway.

**.NET / EF notes:**

- `xmin` is a hidden column Postgres keeps on every row; it changes on every `UPDATE`.
  `IsRowVersion()` maps a `uint` property to it, for free (no schema change).
- Setting `.OriginalValue` is how you tell EF "this is the version I read"; EF puts it in
  the `WHERE`. This is the standard optimistic-concurrency pattern for `[Timestamp]`/
  rowversion columns.
- A concurrency failure rolls back the whole `SaveChanges` transaction — the child
  add/remove for the variant is undone too.

## 5. Program clone — `POST /programs/{id}/clone`

Deep-copies the whole tree (groups → variants → exercises → set prescriptions → superset
groups) with brand-new `Guid`s, in one transaction. Name becomes `"… (copy)"`, `IsActive`
is `false` (a clone is always a draft), and it counts against the 50-program cap. Catalogue
*references* (`VariantExercise.ExerciseId`) are shared, not copied — they point at the
read-only catalogue. Superset links are remapped per variant (old group id → new
`SupersetGroup`). Frontend: a "Duplicate" button on the program detail header.

**EF note:** if you build a fresh object graph with navigation properties set
(`clone.Groups = [...]`, each with `.Variants = [...]`, …) and call `db.Add(clone)`, EF walks
the graph and `INSERT`s everything, filling in the foreign keys itself. You don't set
`ProgramId`/`GroupId` by hand.

## 6. Bulk copy / move exercises between variants

`POST /workout-variants/{id}/exercises/bulk-copy` and `…/bulk-move` (docs/08 Story 7). Both
are scoped to the caller (source *and* destination must be owned, else `404`), both guard on
the destination program's token, both append after the current exercises and respect the
50-exercise cap.

- **copy** creates new `VariantExercise` + `SetPrescription` rows ("copy creates independent
  ids"). A source superset is recreated in the destination only if 2+ of its members are in
  the selection.
- **move** reparents the existing rows (`e.VariantId = destination.Id`) — identity and sets
  are kept ("move preserves identity where the parent change allows"). Moved exercises leave
  their superset; a source superset left with <2 members is deleted (the FK is
  `OnDelete(SetNull)`, so the lone survivor is un-grouped automatically).

Frontend: `useBulkExercises(programId)` hook; the UI is in the variant editor (next slice).

## 7. The variant editor, rebuilt

The old editor modelled an exercise as "N identical sets (one rep range, one weight)".
Loading a variant that had drop sets, an AMRAP last set, or per-set weights and re-saving
**silently flattened** all of that. Rebuilt so it round-trips per-set detail losslessly:

- **Per-set grid**: kind (Standard/Drop), rep range, weight, RIR, AMRAP, To-failure;
  add / remove / "fill down from set 1".
- **"More options"** per exercise (locked decision #20): rest-after-exercise, notes, and
  superset assignment.
- **Superset editing**: assign an exercise to "No superset" / an existing group / "New
  superset"; a group-level rest-after-round input; a group with <2 members is flagged and
  dropped on save (the backend would reject it anyway).
- **Bulk copy/move**: a checkbox per persisted exercise + a "Copy to… / Move to…" menu
  listing the program's other variants. Disabled while the editor has unsaved edits (the
  server acts on saved state).
- **Unsaved-changes guard**: an "Unsaved changes" marker, a `beforeunload` warning, and an
  inline "Discard / Keep editing" bar instead of `window.confirm`.

**React notes:** the editor keeps its own editable copy of the data (`useState`), seeded
once from the server response. Dirtiness is `fingerprint(current) !== fingerprint(baseline)`
where `fingerprint` is a `JSON.stringify` of the meaningful fields (not the volatile React
keys). This is a plain, debuggable approach — no form library needed for this shape.

## 8. Drag-and-drop reorder (`@dnd-kit`) + wiring the reorder endpoints

`PUT /programs/{id}` (`groupOrder`) and `PUT /workout-groups/{id}` (`variantOrder`) have
accepted an ordered id list since Phase 2 — the UI never sent one. Now:

- Added `@dnd-kit` (core/sortable/utilities) and a small `SortableList` helper (pointer +
  keyboard sensors, a grip handle per row, a 4px activation threshold so a click on a button
  inside a row still registers).
- Groups and variants are drag-reorderable in the program screen; exercises in the variant
  editor (the up/down arrows stay as a keyboard-friendly fallback).
- `UpdateGroupRequest` gained a `RowVersion` so group rename/reorder also participates in
  the concurrency check.

## 9. Un-archive — `POST /programs/{id}/restore`

`DELETE /programs/{id}` was one-way (archive with no way back). Added:

- `GET /programs/archived` — the caller's archived programs, newest-archived first.
- `POST /programs/{id}/restore` — clears `ArchivedAt` (restored as a *draft*, not
  auto-activated); `404` if it isn't an archived program of the caller; `409` if restoring
  would exceed the 50-program cap.

Frontend: a collapsed "Archived programs" section at the bottom of the list, with a
"Restore" button. The list query only fires when you expand the section.

## 10. Styled confirm dialogs (`useConfirm.tsx`)

`window.confirm` is blocking and unstyled (docs/07 wants better for destructive actions).
`useConfirm()` is a small promise-based replacement: `await confirm({ title, message })`
resolves `true`/`false`; render its `dialog` node once. Wired into archive-program and the
two hard deletes (group, variant). The delete dialogs spell out that the action can't be
undone; archive notes that it can be restored.

## 11. Isolated integration tests (Respawn) + catalogue seed in CI

The suite ran against the shared dev database and cleaned up with a per-test `finally` block
that a crashed test would skip.

- **`DatabaseFixture`** (an xUnit *collection fixture* — one instance shared by every test in
  the collection) builds a **Respawn** `Respawner` that deletes every application table
  between tests, keeping only `__EFMigrationsHistory` and the seeded reference catalogue
  (`exercises`, `exercise_categories`, `muscles`, `equipment`, and the two join tables).
- **`DatabaseTest`** base class calls `Database.ResetAsync()` in `InitializeAsync` (xUnit
  runs that before *each* test), so every test starts from an empty DB.
- **`[assembly: CollectionBehavior(DisableTestParallelization = true)]`** — the tests share
  one database, so they must run serially or one test's reset pulls data out from under
  another. Suite time went ~3s → ~10s. Acceptable for the correctness win.
- **CI** gained a "Seed exercise catalogue" step (the idempotent `WgerImport` tool) after
  migrations — the workout tests need catalogue rows and CI never had them.

**Heads-up:** running the integration suite now **wipes non-seed data in the target
database**. That's normal for an integration-test DB; CI uses a throwaway container. Locally,
point tests at a database you don't mind being reset.

**xUnit notes for a JS dev:**

- `IClassFixture<T>` = one shared instance per test class (like a `beforeAll` that produces
  an object). `ICollectionFixture<T>` = one shared instance across many classes.
  `IAsyncLifetime.InitializeAsync` = an async `beforeEach`.
- The now-redundant per-test `try/finally` cleanup was left in the test bodies to keep this
  slice's diff small; a later sweep can delete it.

---

## Where Phase 2 stands now

Everything from the risk review is closed **except** the items that were deliberately
dropped or deferred by decision:

- **Custom user-created exercises** — explicitly out of the MVP.
- **Undo for hard deletes** (a `BulkOperation`-backed undo) — Phase 3, when there's session
  history to protect.
- **Hard-delete → archive guard for groups/variants with session history** — also Phase 3
  (there are no sessions yet).
- **`ILIKE '%…%'` catalogue search** — still a full scan; fine at ~900 rows, revisit with a
  `pg_trgm` index if the catalogue grows.
- **OpenAPI-generated TS client** — still hand-written types, project-wide.

Test totals: **74 integration + 30 unit + 41 frontend**.
