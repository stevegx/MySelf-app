# Phase 2 — the program builder (what shipped, and what to watch)

Written 2026-09-02, for a web dev who is comfortable with React/TypeScript and REST but
newer to C#/EF Core. Part 1 is a plain walkthrough of what was built. Part 2 is the honest
list of weak spots — performance, security, and gaps — that need your attention before this
is anything more than a local learning build.

---

# Part 1 — What was built

Phase 2 is "let a user build a workout program exactly how they want it" (docs/08 Story 3):
programs → groups → variants → exercises → set targets, with optional supersets, and no
fixed weekdays.

## The data model (six new tables)

Think of it as a tree:

```
WorkoutProgram        "PPL"                 (one per user can be "active")
└── WorkoutGroup      "Push", "Legs"        (ordered)
    └── WorkoutVariant "Legs #1", "Legs #2" (ordered)
        ├── VariantExercise   → points at a catalogue Exercise, ordered
        │   └── SetPrescription  "set 1: 8–12 reps @ 100kg", "set 2: AMRAP @ 90kg"
        └── SupersetGroup     groups 2+ VariantExercises + a rest-after-round time
```

Each entity is a plain C# class in `backend/MySelf.Domain/Workouts/`. The database mapping
(table names, column types, indexes, foreign keys) lives separately in
`backend/MySelf.Infrastructure/Persistence/Configurations/WorkoutConfigurations.cs`, so the
domain classes stay free of framework attributes. One EF migration
(`AddWorkoutProgramBuilder`) created all six tables.

Two mapping details worth knowing:

- **`WorkoutProgram.RowVersion` maps to PostgreSQL's hidden `xmin` column.** It's a
  concurrency token — a number that changes every time the row is updated. The idea is that
  a save can say "only if the row still looks like it did when I read it". *(We store it but
  don't check it yet — see Part 2.)*
- **"One active program per user" is a database rule, not just app code.** There's a
  *filtered unique index*: `UNIQUE (UserId) WHERE IsActive = true`. Even if the activate
  code had a bug, Postgres would refuse a second active row.

## The API (all under `/api/v1`, all require a logged-in user)

| Method & path | What it does |
|---|---|
| `GET /exercises?q=&categoryId=&page=&pageSize=` | Search the seeded exercise catalogue (~900 rows). Paginated, `pageSize` capped at 50. |
| `GET /exercises/{id}` | One catalogue exercise. |
| `GET /programs` | The caller's programs (active first, archived hidden), with group/variant counts. |
| `POST /programs` | Create a program `{ name, splitLabel? }`. |
| `GET /programs/{id}` | The whole tree: groups → variants (+ per-variant exercise count). |
| `PUT /programs/{id}` | Rename / relabel, and reorder groups by sending an ordered list of group ids. |
| `DELETE /programs/{id}` | **Archive** (soft delete): sets `ArchivedAt`, clears the active flag. |
| `POST /programs/{id}/activate` | Make this the active program; deactivates the previous one, in one transaction. |
| `POST /programs/{id}/groups` | Add a group `{ name }`. |
| `PUT /workout-groups/{id}` | Rename a group / reorder its variants. |
| `DELETE /workout-groups/{id}` | **Hard delete** the group and its variants. |
| `POST /workout-groups/{id}/variants` | Add a variant `{ name }`. |
| `GET /workout-variants/{id}` | One variant in full: exercises, their set prescriptions, supersets. |
| `PUT /workout-variants/{id}` | **Replace the whole variant body** — see below. |
| `DELETE /workout-variants/{id}` | **Hard delete** the variant. |

### The one endpoint that does the heavy lifting: `PUT /workout-variants/{id}`

Instead of a dozen fiddly endpoints (add-exercise, remove-exercise, reorder, set-a-rep-range,
group-into-superset, ungroup…), there is one endpoint that takes the **entire desired state**
of a variant — every exercise, in order, with all its sets, plus any superset groupings — and
makes the database match it:

1. Load the variant (scoped to the caller).
2. Validate: every `exerciseId` exists in the catalogue; each superset has ≥2 members; rep
   ranges and weights are sane; not more than 50 exercises or 20 sets each.
3. Delete all the variant's existing exercises + sets + superset rows.
4. Insert the new ones. A client-side string key (`supersetRef`) links exercises to the new
   superset rows.
5. `SaveChangesAsync()` — steps 3 and 4 commit together in one transaction.

So "reorder", "add", "remove", and "regroup" are all just *"send the new list"*. It's simple
and atomic. The cost: every save rewrites all the child rows (fine at this scale), and two
people editing the same variant would overwrite each other (see Part 2).

### How "you can only touch your own stuff" works

Every handler filters by the logged-in user id, walking up the tree:

```csharp
// a variant is yours only if its group's program's user is you
await db.WorkoutVariants
    .FirstOrDefaultAsync(v => v.Id == id && v.Group.Program.UserId == userId, ct);
```

If that returns nothing, the endpoint responds **404** (not 403) — so someone poking at
random ids can't even tell whether a program exists.

## The frontend

`frontend/src/features/workouts/`:

- **`api.ts`** — hand-written types + TanStack Query hooks (`usePrograms`, `useProgram`,
  `useVariant`, `useExerciseSearch`, `useCreateProgram`, `useMutateProgram`,
  `useUpdateVariant`). Mutations invalidate the relevant query so the screen refreshes.
- **`WorkoutBuilderScreen.tsx`** — three views in one component:
  1. **Program list** + a "new program" form.
  2. **Program detail** — groups, variants under each, buttons to add a group / add a
     variant / activate / archive.
  3. **Variant editor** (`VariantEditor.tsx`) — the exercise list for one variant. Add an
     exercise via a catalogue search panel (`ExercisePicker.tsx`), set "how many sets / rep
     range / weight", reorder with up/down arrows, remove, then **Save** (which calls the
     big PUT).

The old static mock screen was replaced.

## Tests

- **Backend: 10 new integration tests** (`WorkoutBuilderEndpointTests.cs`) — create a
  program and read the tree back; PUT a variant with exercises + prescriptions and round-trip
  it; PUT a superset and check both members share the group id; PUT again with fewer
  exercises and confirm the old rows (and their sets) are gone; activate deactivates the
  previous program; another user gets 404 on all of it; archive hides the program; PUT
  rejects an unknown exercise and a 1-member superset; exercise search paginates and filters;
  everything 401s without a token.
- **Frontend: 2 smoke tests** — the empty state renders; creating a program calls `POST` and
  opens the detail view.

Totals now: backend 30 unit + 63 integration, frontend 35.

---

# Part 2 — Drawbacks, risks, and gaps (please read)

Nothing here is on fire, but this is a **single-user local build**. Several of these must be
closed before it's multi-user or public.

## Database performance

| Concern | Detail | Severity now |
|---|---|---|
| **Leading-wildcard search** | `GET /exercises?q=press` runs `name ILIKE '%press%'`. The leading `%` means the name index can't be used — it's a full scan of the catalogue every search. | Low (~900 rows). Would need a `pg_trgm` GIN index or Postgres full-text search at ~100k rows. |
| **List endpoint counts** | `GET /programs` computes `groupCount` / `variantCount` with sub-selects per program. | Low (a user has a handful of programs). |
| **Variant PUT rewrites everything** | Every save deletes all child rows and re-inserts them, after loading the full variant graph. A diff would be leaner. | Low at 10–20 exercises; wasteful, not dangerous. |
| **No caching** | The static exercise catalogue is re-queried from Postgres on every new search term (react-query caches per term client-side, but a fresh term always hits the DB). | Low. An in-memory cache or HTTP cache headers would remove it entirely. |
| **No `AsSplitQuery`** | The tree reads use projected `Select` (no cartesian blow-up), so this is fine — but if anyone switches to `Include().ThenInclude()` for the tree, watch for row multiplication. | N/A today; a foot-gun for the next change. |

**No N+1 query problems** were introduced — the reads are single projected queries.

## SQL injection / input safety

- **SQL injection: not a risk here.** EF Core parameterises every query. There is **no**
  `FromSqlRaw` / `ExecuteSqlRaw` anywhere in the codebase. The search term is passed as a
  bound parameter; a user can influence the `LIKE` *pattern* (e.g. send `%` or `_`) but
  cannot inject SQL.
- **But `q` has no length limit.** A multi-megabyte `q` becomes a multi-megabyte query
  parameter. Cheap DoS. → **cap `q` at ~100 chars.**
- **Some numeric fields aren't range-checked** on `PUT /workout-variants`: `restSeconds`,
  `supersetMemberOrder`, `sortOrder`, `estimatedDurationMinutes`, `targetRir`. A client can
  send `restSeconds = -5` or `2_000_000_000`. `restAfterRoundSeconds` *is* clamped (0–3600);
  the others aren't. → add sane bounds.
- **No caps on tree size.** Nothing stops a script creating 10,000 programs, or a program
  with 10,000 groups. Storage + list-query DoS. → **cap programs/user, groups/program,
  variants/group.**

## Security threats

| Threat | Where it stands |
|---|---|
| **IDOR (accessing another user's data)** | Defended: every read and write filters by the JWT user id and returns 404 on a miss. **Weak point:** it's copy-pasted into each handler, not enforced centrally. One forgotten `.Where(... userId ...)` on a future endpoint = a hole. Consider an EF global query filter or a small `authorize this resource` helper. |
| **Mass assignment / privilege fields** | Safe: requests are explicit DTO records, not entities. `UserId`, `Id`, `IsActive`, `CreatedAt`, `ArchivedAt` are all server-set. `PUT /programs/{id}` cannot flip `IsActive` — only `/activate` can. |
| **Rate limiting** | **None**, anywhere (also true of the Phase 1 auth endpoints). `.NET`'s `AddRateLimiter` is not configured. `PUT /workout-variants` and `POST /programs` can be hammered. |
| **Auth** | JWT bearer, 15-minute access token, refresh token in an HttpOnly cookie (Phase 1). All builder routes call `RequireAuthorization()`. Signing key is environment-only, never committed. Known Phase 1 trade-off: a stolen access token is valid for its full 15 minutes (no per-request revocation list). |
| **CORS** | Locked to the single frontend origin, credentials allowed. Fine. |
| **Transport** | `UseHttpsRedirection()` is on; the refresh cookie is `Secure` outside Development. Local dev is plain HTTP by design. |
| **Catalogue endpoints require a token** | They aren't secret, so this is a choice, not a requirement — could be anonymous if you ever want an unauthenticated "browse exercises" page. |

## Things that could slow the app down under real load

1. **No rate limiting** (above) — the biggest one. A loop calling `POST /programs` fills the
   DB.
2. **Unbounded tree size** (above) — a pathological program makes its own `GET` slow.
3. **`ILIKE '%…%'` search** scales linearly with catalogue size.
4. **Variant PUT** does N deletes + M inserts per save; a very large variant + a user
   mashing "Save" is churn.
5. **Every mutation invalidates and refetches** on the frontend — no optimistic updates, so
   a slow network makes the builder feel laggy.
6. **Integration tests hit the shared dev Postgres** — running them while developing competes
   for the same connection pool.

## Data-integrity gaps

- **The `xmin` concurrency token is stored but never checked.** Two browser tabs editing the
  same program (or variant) → last save silently wins, the other's changes vanish. For a
  single-user MVP that's tolerable; for anything shared it isn't. Wiring it in means adding
  the token to the `PUT` request/response and handling `DbUpdateConcurrencyException`.
- **Groups and variants are hard-deleted**, including any content. That's fine *now* because
  there are no workout sessions yet. **Phase 3 adds logged sessions** — deleting a variant
  that has history must switch to *archive* (docs/06 §15). This guard does not exist yet.
- **No audit log** — nothing records who changed a program or when (beyond `CreatedAt`).
- **Archived programs are effectively permanent** — there's no un-archive endpoint, and
  `GET /programs/{id}` returns 404 for an archived one. Add un-archive if "restore" matters.
- **Superset `sortOrder` collisions** are allowed (two supersets with the same order) —
  cosmetic only.

## Testing / ops gaps

- **The integration test suite is local-only and not isolated.** It runs against your real
  dev database, creates real rows, and deletes them in a `finally` (via FK cascade). A test
  that crashes mid-run leaves orphan data. **CI runs build + lint only** — the integration
  tests never run in CI. Fixing this properly means a disposable Postgres (Testcontainers or
  a CI service container) and wrapping each test in a rolled-back transaction.
- **No test for the concurrency token** (nothing uses it).
- **No frontend tests for `VariantEditor` / `ExercisePicker`** — only a `WorkoutBuilderScreen`
  smoke test.
- **No load or performance testing.**

## Frontend-specific gaps

- **No autosave and no "unsaved changes" warning** in the variant editor. Navigating away
  mid-edit loses everything.
- **`window.confirm()`** is used for delete/archive — blocking, unstyled, and there's **no
  undo** (docs/07 wants undo for destructive actions).
- **Group and variant reordering is not wired up in the UI.** The API supports it
  (`groupOrder` / `variantOrder`); the screen doesn't send it. Exercise reorder within a
  variant works (up/down buttons); there's no drag-and-drop.
- **The variant editor flattens per-set detail.** It models an exercise as "N identical
  sets (one rep range, one weight)". If you load a variant that has drop sets, an AMRAP last
  set, or different weights per set, then save, **those differences are lost.** The backend
  supports full per-set prescriptions — the UI just doesn't expose them yet.
- **No superset editing UI** (backend-supported via the PUT, no controls to create groups).
- **Exercise search** has no min length and no "load more" (fixed 25 results).
- **Accessibility** of the picker, the confirm flows, and focus management is not audited.

## Gaps against the Phase 2 spec (docs/08 §17 / Story 3)

Built: custom program + groups + variants + exercises + set prescriptions + supersets
(backend) + ownership + transactions + activation.

**Not built:**

- `POST /programs/{id}/clone` (clone a program before big changes).
- Bulk copy / move exercises between variants
  (`/workout-variants/{id}/exercises/bulk-copy` / `bulk-move`), `PATCH` / `DELETE
  /variant-exercises/bulk`.
- **Custom (user-created) exercises** — the builder uses the seeded catalogue only. Adding
  them means a nullable `OwnerId` on `Exercise` and relaxing its provenance columns.
- Superset **editing UI**.
- Undo / `BulkOperation` tracking for destructive edits.
- Drag-and-drop reorder; group/variant reorder in the UI.
- Templates — *correctly* deferred (the spec says templates come only after the custom flow
  stabilises).
- OpenAPI-generated TypeScript client — still hand-written types (true project-wide, not
  just here).

---

# Decisions I made that you should confirm

1. **One big `PUT /workout-variants/{id}`** that replaces the whole variant body, instead of
   ~8 granular endpoints. Simpler and atomic; heavier writes; the client must always send
   the complete state.
2. **Programs archive (soft); groups and variants hard-delete.** Revisit for groups/variants
   in Phase 3 when sessions exist.
3. **Archived programs 404 on `GET`** and there's no un-archive endpoint — archive is
   one-way from the UI for now.
4. **Superset must have ≥2 members** (400 otherwise) — matches locked decision #22.
5. **Catalogue endpoints require auth** — could be anonymous; say if you want a public
   browse.
6. **`xmin` token stored but not enforced** — accepting last-write-wins for the single-user
   MVP.

# Questions for you

1. Do you want the deferred pieces (clone, bulk copy/move, custom exercises, superset UI,
   drag-and-drop) as follow-up slices **now**, or move on to **Phase 3 (workout execution)**
   and backfill these later?
2. Wire the **concurrency token** into the program/variant `PUT` contracts now, or leave
   last-write-wins until the app is multi-device?
3. Add the **cheap safety caps** now (programs/user, groups/program, `q` length,
   `restSeconds` range)? I'd recommend yes — small change, closes the DoS vectors.
4. Invest in an **isolated test database + integration tests in CI** now, or later?
5. Should the **variant editor expose full per-set prescriptions** (drop sets, per-set
   weights, AMRAP), or is "N identical sets" enough for the MVP?
