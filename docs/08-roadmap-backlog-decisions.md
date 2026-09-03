# MySelf App — 08 Roadmap Backlog Decisions

> Split documentation file. Purpose: Implementation roadmap, acceptance-criteria backlog, locked product decisions, open decisions and the long Claude handoff prompt.
> Original source: `myself-app-blueprint-v1.1-learning-r1.md`

## 17. Implementation roadmap

### Phase 0 — foundations and .NET orientation

- Monorepo/repository and solution structure.
- React shell, routing, themes and UI primitives.
- ASP.NET Core API with Swagger/OpenAPI.
- PostgreSQL through Docker Compose. _(Deferred 2026-08-30 — using a native local PostgreSQL install until Docker is available.)_
- EF Core connection and first migration.
- API health endpoint.
- Backend/frontend test project skeletons.
- CI skeleton for build/test/lint.

**Learning goals:**

- understand `.sln` and `.csproj`,
- understand the role of `Program.cs`,
- understand configuration / `appsettings`,
- understand dependency injection at a basic level,
- understand how an HTTP request enters ASP.NET Core,
- understand `DbContext`, migration and the PostgreSQL connection,
- understand the difference between application code, database service and Docker container.

No authentication or business-domain features yet. Phase 0 is complete only when the developer can run the stack, call the health endpoint and explain the basic request path.

### Phase 1 — identity and onboarding

_Complete 2026-09-02. Story 1 (auth) shipped 2026-08-31; Story 2 (onboarding) in four
slices — `UserProfile` + `PUT /me/profile`, the pure calorie estimator +
`POST /me/nutrition-estimate`, `UserGoal`/`NutritionEstimateSnapshot` +
`POST /me/onboarding/complete`, the wizard UI + route gating. Owner-based authorization is
per-endpoint ownership checks (no roles). See `docs/learning-log/`._

- ASP.NET Core Identity.
- Register/login/refresh/logout.
- Profile and goal wizard.
- Protected routes and app shell.
- Owner-based authorization foundations.
- Auth tests for the first critical flows.

**Learning goals:**

- understand how ASP.NET Core Identity fits into the application,
- understand authentication vs authorization,
- understand DTOs, validation and API responses,
- understand dependency injection in a real feature,
- understand how EF Core persists user/profile data to PostgreSQL,
- understand access/refresh session flow at a conceptual level,
- manually verify happy and failure paths before automating them.

Build this phase in small vertical slices: register first, then login, then one protected endpoint, then refresh/logout, then onboarding/profile. Do not implement the whole identity system in one task.

### Phase 2 — program builder

_Core shipped 2026-09-02: WorkoutProgram/Group/Variant/VariantExercise/SetPrescription/
SupersetGroup model + CRUD, per-owner authorization (404 on a miss), `PUT /workout-variants/{id}`
whole-body replace in one transaction, `POST /programs/{id}/activate` (explicit transaction +
filtered unique index for one-active-per-user), `GET /exercises` catalogue search, and a
functional builder UI._

_Gap closure shipped 2026-09-03 (11 slices — see
`docs/learning-log/2026-09-03-phase2-gap-closure.md`): input caps (`WorkoutLimits`) + `q`
length cap; rate limiting (global + `auth` + `write` policies, incl. the Phase 1 auth
endpoints); centralised ownership filter (`OwnedWorkouts`); **`xmin` concurrency token now
enforced** on `PUT /programs` and `PUT /workout-variants` (token on the program, returns
409); `POST /programs/{id}/clone`; bulk copy/move exercises
(`/workout-variants/{id}/exercises/bulk-copy|bulk-move`); rebuilt variant editor with the
full per-set prescription grid (drop sets / AMRAP / per-set weights round-trip losslessly),
superset editing UI, and an unsaved-changes guard; `@dnd-kit` drag-and-drop reorder for
groups/variants/exercises + the previously-unwired `groupOrder`/`variantOrder`;
`GET /programs/archived` + `POST /programs/{id}/restore` (un-archive); styled confirm
dialogs replacing `window.confirm`; integration tests isolated with Respawn (per-test DB
reset, serial) + CI now seeds the exercise catalogue._

_**Still deferred:** custom (user-created) exercises — **dropped from the MVP**, builder is
catalogue-only. Undo/`BulkOperation` for hard deletes, and the archive-instead-of-delete
guard for groups/variants with session history — both **Phase 3** (no sessions yet).
Templates — after the custom flow stabilises. `ILIKE '%…%'` catalogue search still full-scans
(fine at ~900 rows). OpenAPI-generated TS client still hand-written (project-wide)._

- Seed/curate the catalogue data needed by the builder.
- Blank custom program builder.
- User-defined groups/variants, item order and activation, without weekdays or schedule slots.
- Create, edit, reorder and ungroup optional supersets with group-level rest-after-round.
- Relational modelling, ownership checks, transactions and snapshot rules needed by the builder.
- Optional templates μόνο μετά τη σταθεροποίηση του custom flow.

Each backend capability is implemented as a small runnable vertical slice with manual verification and the minimum useful automated tests before moving on.

### Phase 3 — workout execution

- Active session, set logging, autosave, rest timer.
- Manual variant picker, single-active-session guard and workout calendar based on actual sessions.
- Completion, history, volume/e1RM and PR detection.
- Tracking-mode-aware inputs, strict required-field validation and `Copy previous set`.
- Multi-select item/set editing, copy/move/delete and undo.
- Standard/Drop sets plus AMRAP, To-failure, optional RIR and explicit Skip set.
- Superset grouping, per-round execution and rest-after-round timer, without changing per-item analytics.
- Add/replace movement during session.
- Offline local draft, recovery and sync status.

### Phase 4 — dashboard and progress

- Aggregated dashboard endpoint.
- Nutrition-first overview, workout frequency and body-weight trend.
- Detailed strength/performance charts remain in Progress.

### Phase 5 — nutrition

- Manual food creation and `My Foods`.
- Meal logs, arbitrary quantities, targets, day totals and favourites.
- Saved Meals, Recipes, scaling and editable day snapshots.
- Multi-select copy/move for meals, items and complete nutrition days.
- Customizable meal categories.
- Open Food Facts barcode lookup with manual fallback.
- USDA generic search after the MVP flow is stable.

### Phase 6 — polish and release

- Accessibility, localization, export/delete, rate limiting.
- Production config, monitoring and backups.

Κάθε phase πρέπει να κλείνει με working vertical slices. Δεν χτίζουμε όλο το database layer πριν εμφανιστεί λειτουργικό user flow.

## 18. Initial backlog with acceptance criteria

### Story 1 — register and login

**As a user**, I can create an account and return to my session securely.

- Duplicate email returns useful validation.
- Password rules appear before submit.
- Refresh survives a browser reload.
- Logout revokes refresh token.

### Story 2 — onboarding

**As a new user**, I can provide the minimum inputs for an understandable calorie estimate or continue without one.

- Inputs are units, date of birth, height, weight, calculation sex when estimating, activity and goal.
- Lose, Maintain, Gain and Track-only follow distinct branches.
- The result separates BMR, activity/TDEE, goal adjustment and suggested target.
- Calculated target is clearly labeled as an estimate and remains editable.
- Manual target, calculation opt-out and complete nutrition skip are available.
- Fixed workout days are not collected; program setup creates reusable groups/variants instead.
- Logged workout calories never silently increase the nutrition budget.
- Under-18 flow does not generate nutrition targets.
- Accepting a new target creates an effective-dated snapshot without changing past logs.
- Disclaimer is visible before `Use this estimate`.

### Story 3 — build a program

**As a user**, I can build a workout program exactly as I want it.

- I can create any number of named workout groups and variants without assigning weekdays.
- I can add custom or catalogue exercises with my own set targets.
- I can reorder groups, variants, exercises and sets.
- I can group two or more exercises as a superset, reorder its members or ungroup them without changing their prescriptions.
- The app does not force a split or overwrite my choices.

### Story 3A — choose and calendar-log a workout

**As a user**, I manually choose what I trained and see the actual session on my calendar.

- `Start workout` opens the variant picker with no preselected or suggested workout.
- I can choose any variant or start an ad-hoc workout.
- Only one session can be active, but I can complete multiple sessions on the same day.
- Completing a session places it on the calendar using my local date.
- I can correct the completed session date and its calendar position updates.

### Story 4 — log a workout

**As a user**, I see target and previous values and can complete sets quickly.

- Every set change autosaves.
- Previous performance remains visible as reference.
- For `Weight × Reps`, both weight and reps are required before complete.
- `Copy previous set` fills both fields but never completes the set automatically.
- Set options include Standard/Drop, AMRAP, To failure and optional RIR; no warm-up set type.
- Superset groups display exercises together by round; rest starts only after every scheduled set in that round is completed or skipped.
- Superset grouping never changes the calculation of any exercise metric.
- I can explicitly skip a set, add an exercise or replace it for today/future workouts.
- An uncompleted set never enters progress analytics.
- Refresh/offline recovery resumes the local active-session draft and syncs safely.
- Finish calculates summary and PRs exactly once.
- Editing the completed session recalculates all affected derived metrics.

### Story 5 — log a meal

**As a user**, I can search food, choose amount and see daily totals update.

- Source and serving basis are visible.
- Missing nutrient is not treated as zero.
- Logged item keeps a nutrient snapshot.

### Story 6 — use a saved meal

**As a user**, I can add my usual Chicken & Rice meal without rebuilding it every day.

- Adding the template creates an editable snapshot for that day.
- I can scale the whole meal and then edit individual quantities.
- I can add extras or remove items without changing the saved template.
- Template edits never rewrite past meal logs.

### Story 7 — bulk edit and reuse content

**As a user**, I can select multiple items and copy, move, duplicate or delete them safely.

- Supported in workout exercises/sets, program structures and nutrition meals/items/days.
- Copy creates independent ids; move is transactional.
- Paste validates the destination type and handles conflicts explicitly.
- Delete/move offers undo and does not erase unrelated history.

### Story 8 — understand the dashboard quickly

**As a user**, I can see my daily nutrition, training frequency and body-weight direction without opening multiple pages.

- Nutrition today is the most prominent dashboard section.
- Workout frequency shows completed sessions for the chosen range.
- Body weight uses latest value plus rolling trend.
- Detailed exercise analytics remain in Progress instead of cluttering Home.

## 19. Locked product decisions

1. Product name: **MySelf App**.
2. Launch UI language: **English**.
3. Product tone: **clean, accessible and neutral for a broad audience**.
4. Workout planning: **user-designed program with split label, workout groups and variants**; e.g. PPL → Legs → Legs #1 / Legs #2.
5. Initial platform: **responsive web/PWA**; native is a later decision.
6. Nutrition MVP: **barcode scan or manual food creation**, with arbitrary quantity, `My Foods` and Saved Meals; full Recipes follow in V1.
7. Editing model: **fully editable with multi-select, copy, cut/move, paste, duplicate, bulk edit, delete and undo**.
8. Workout calendar: **records actual manually selected sessions**, not planned occurrences; completed dates remain editable.
9. Brand palette: **Balanced Indigo**, with the full light/dark semantic palette in section 14.
10. Set validation: **no automatic inheritance**; `Weight × Reps` requires both values before completion.
11. Bodyweight analytics: track reps and optional extra/assistance weight only; never add the user's body weight.
12. Program state: **one active program**; any number of drafts/archived programs and full editability.
13. Dashboard priority: **nutrition today, workout frequency and body-weight trend**; detailed lift analytics live in Progress.
14. Set options: **Standard/Working, AMRAP, Drop set and To failure**, optional RIR, with no warm-up set type.
15. Active workout editing: explicit Skip set plus add/replace exercise for today-only or future workouts.
16. Unilateral exercises: reps are logged as **total across both sides**.
17. Offline MVP: local autosave/recovery for active workout and unfinished meal draft, followed by safe sync.
18. Historical edits: completed-session changes automatically recalculate volume, e1RM, PRs and charts.
19. Nutrition organization: customizable meal categories.
20. Builder UX: `More options` per exercise instead of a separate global Basic/Advanced mode.
21. Paste conflicts: non-empty targets always show preview with `Append / Replace selected / Cancel`.
22. Supersets: **included in MVP as optional program/execution grouping**; exercises retain independent sets and analytics, and rest starts after the round.
23. Onboarding: **minimum calculator inputs with Lose/Maintain/Gain/Track-only branching**, transparent BMR → TDEE → adjustment breakdown, manual/skip paths and a clear non-medical disclaimer.
24. Calorie-budget behavior: workout calories are not automatically eaten back; recalculated goals require explicit confirmation and preserve effective-dated history.
25. Initial release context: **personal learning project**; architecture remains clean enough for a later public release without implementing unnecessary production services now.
26. Workout selection: **fully manual**; no fixed weekdays, automatic rotation, suggested-next logic or preselected variant.
27. Active sessions: **one InProgress workout per user**, while multiple completed workouts per day are allowed.
28. Nutrition targets: **same calorie and macro targets every day** in the MVP.
29. Target weight: **optional** for Lose and Gain goals.
30. Body weight: multiple measurements per day are allowed; charts use the daily average followed by a 7-day rolling average.
31. Rest timer: in-app timer with optional sound/vibration while open; **no browser notifications**.
32. Tracking modes: Weight × Reps, Bodyweight Reps, Bodyweight + Extra Weight, Assistance × Reps, Reps only and Duration are MVP; Distance + Duration is V1.

### Still open for later design rounds

- Exact logo/wordmark treatment.
- Which advanced training fields stay hidden by default.
- Whether barcode scanning launches from the central `Log` action or the Nutrition screen only.

## 20. Claude handoff prompt

Copy the section below into Claude together with this file:

```text
You are my senior pair programmer and .NET tutor for MySelf App, a responsive fitness planning, workout logging and nutrition tracking web app.

The attached MySelf App Blueprint v1.1 — Learning Edition is the source of truth for product requirements, domain rules, API behaviour, design decisions and the learning workflow.

MY BACKGROUND

I already understand:
- JavaScript / TypeScript,
- React and frontend development,
- REST APIs and general web concepts,
- basic authentication concepts,
- basic database concepts,
- manual/exploratory testing.

I am new to:
- C#,
- ASP.NET Core,
- Entity Framework Core,
- ASP.NET Core Identity,
- professional .NET solution/project structure.

I may not initially be able to write the required C# syntax myself. That is expected.

Your job is NOT simply to generate the application. Your job is to help me build it while ensuring that I understand the architecture, behaviour, important code and tests.

TECHNOLOGY CONSTRAINTS

- Frontend: React 19.2, TypeScript, Vite, React Router, TanStack Query, React Hook Form + Zod.
- Backend: .NET 10 ASP.NET Core Web API, EF Core 10, PostgreSQL, ASP.NET Core Identity.
- Architecture: modular monolith, REST API under /api/v1, OpenAPI-generated TypeScript client.
- Local environment: Docker Compose.
- Do not introduce microservices, paid APIs or unnecessary abstractions.
- All launch UI copy is English.

PRODUCT CONSTRAINTS

- Workout planning uses Program → Workout Group → Workout Variant. Example: PPL → Legs → Legs #1 / Legs #2. There is no Schedule Slot in the MVP.
- Workout selection is always manual. Never assign weekdays, suggest the next workout, rotate automatically or preselect a variant.
- Completed/in-progress sessions create calendar entries from actual activity. Only one session may be active, while multiple completed sessions per day are allowed.
- Exercise performance history works across variants, with an optional variant filter.
- Weight × Reps sets require both values; incomplete sets never enter analytics and `Copy previous set` only prefills.
- Exercise tracking mode controls required fields for bodyweight, assisted, timed and distance exercises.
- MVP tracking modes are Weight × Reps, Bodyweight Reps, Bodyweight + Extra Weight, Assistance × Reps, Reps only and Duration. Distance + Duration is V1.
- Bodyweight exercises use reps plus optional extra/assistance weight; never use the user's body weight in load, volume or e1RM.
- One active program; Standard/Drop sets plus AMRAP, To-failure and optional RIR; no warm-up set type.
- Supersets are optional execution groups in the MVP: show members together by round and start rest after the round; never alter per-exercise metrics, volume, e1RM or PR calculations.
- Unilateral exercises log total reps across both sides.
- Active workouts allow Skip set and today-only/future add or replacement of exercises.
- Active workout and unfinished meal drafts autosave locally and sync safely after reconnect.
- Completed-session edits recalculate derived metrics and PRs.
- Programs, exercises, sets, meals and nutrition days support transactional multi-select copy/move/paste/delete with undo.
- Nutrition MVP supports manual food creation, barcode lookup, My Foods and Saved Meals with arbitrary quantities; full Recipes are V1.
- Onboarding asks only for the inputs needed for the calorie estimator, branches into Lose/Maintain/Gain/Track-only, exposes the BMR/TDEE/adjustment breakdown, and always offers manual or skip paths.
- Treat calorie output as an estimate with the exact disclaimer in the blueprint. Never add logged-workout calories automatically to the daily budget, and never change an accepted target without confirmation.
- Calorie and macro targets are the same on training/rest days in the MVP. Target weight is optional.
- Weight logs allow multiple entries per day; charts use daily averages and then a 7-day rolling average.
- Rest timer may use in-app sound/vibration while open, but must not request or send browser notifications.
- Balanced Indigo light/dark semantic tokens in section 14 are the design source of truth.
- Home dashboard prioritizes nutrition today, workout frequency and body-weight trend; detailed lift analytics remain in Progress.

OWNERSHIP RULES

I own:
- product behaviour,
- acceptance criteria,
- expected results,
- API behaviour/contracts,
- edge cases,
- test scenarios and what counts as correct behaviour.

You may assist with:
- C# syntax and boilerplate,
- ASP.NET Core conventions,
- EF Core setup/migrations,
- test implementation,
- framework-specific debugging,
- refactoring after explaining why it is needed.

If behaviour is ambiguous or not specified by the blueprint, STOP and ask me. Do not invent requirements or silently change a locked product decision.

MANDATORY WORKFLOW FOR EVERY FEATURE

STEP 1 — ANALYZE; DO NOT CODE

Before changing any file:
1. Identify the relevant blueprint requirements.
2. Restate the behaviour we are implementing.
3. List acceptance criteria.
4. List the files you propose creating/changing.
5. Explain the complete request/data flow.
6. Explain every new C#/.NET concept that will appear.
7. Compare unfamiliar concepts to TypeScript/JavaScript when useful.
8. Explain database relationships, queries and migrations that will change.
9. Propose the tests and state what regression each one protects.

Do not modify files until this explanation is complete. Unless I explicitly tell you to continue automatically, wait for my approval before implementation.

STEP 2 — IMPLEMENT THE SMALLEST USEFUL SLICE

- Implement only the agreed behaviour.
- Do not implement unrelated future features.
- Do not silently refactor unrelated files.
- Prefer simple, conventional ASP.NET Core over clever or ceremonial architecture.
- Do not create repositories/services/interfaces merely because they are common in enterprise code. Introduce an abstraction only when it solves a specific problem, and explain the simpler alternative first.
- Keep the app runnable after the slice.

STEP 3 — TEACH ME THE IMPLEMENTATION

After coding:
1. Show every changed file and explain its responsibility.
2. Walk me through the important code.
3. Explain unfamiliar C# syntax.
4. Explain the request/data flow again using the actual implementation.
5. Explain conceptually what EF Core sends to PostgreSQL and what comes back.
6. Explain any dependency-injection registration that was added and why its lifetime was chosen.

STEP 4 — MANUAL VERIFICATION

Give me:
- exact commands to run the feature,
- one happy-path manual test,
- at least one failure/edge-case manual test,
- expected HTTP/UI/database result for each.

I will manually verify actual vs expected behaviour before we consider the slice complete.

STEP 5 — AUTOMATED TESTS

Before writing tests, explain for every proposed test:
- what behaviour it protects,
- why it is a unit, integration or E2E test,
- Arrange / Act / Assert,
- what realistic regression it would catch.

Then implement only the agreed tests.

Never change production behaviour just to make a failing test green without first diagnosing and explaining whether the problem is:
- an application bug,
- a test bug,
- a flaky test,
- or an environment/configuration issue.

After an important test passes, suggest one safe temporary production-code change that should make it fail so I can confirm the test actually protects that behaviour.

STEP 6 — KNOWLEDGE CHECK

When a slice introduces a new .NET concept, ask me 3–5 short comprehension questions.

I do not need to memorize framework syntax. I should be able to explain:
- what the feature does,
- which classes participate and why,
- how the request reaches PostgreSQL and returns,
- what the important automated test proves,
- at least one relevant failure/edge case.

Do not move to another feature until I explicitly request it.

GENERAL ENGINEERING RULES

1. Work in vertical slices and keep the app runnable after every slice.
2. Add migrations and appropriate automated tests with backend features, following the staged testing-learning progression in the blueprint.
3. Never put access/refresh tokens or external API keys in localStorage or the frontend bundle.
4. Store canonical weight in kg and timestamps in UTC.
5. Snapshot workout prescriptions and logged nutrition values so history never changes when plans or external records change.
6. Treat calorie/BMI/macro outputs as editable estimates, not medical advice.
7. Ask before changing product decisions marked as unresolved in the blueprint.
8. Do not optimize for code volume or completion speed at the expense of my understanding.

START ONLY WITH PHASE 0

For the first interaction, DO NOT write code yet.

First explain:
- what a .NET solution (`.sln`) is,
- what a `.csproj` is,
- what `Program.cs` does,
- what the four backend projects Api/Application/Domain/Infrastructure are intended to contain,
- what Docker Compose will do for PostgreSQL locally,
- how a browser/Swagger request will reach the health endpoint.

Then propose:
- the repository tree,
- the exact Phase 0 files to create,
- the order in which we should scaffold them,
- the commands we will eventually run,
- the Phase 0 acceptance criteria.

Wait for my approval before scaffolding.

Phase 0 will eventually include:
- React app shell/router/theme tokens,
- .NET solution/API scaffold,
- PostgreSQL through Docker Compose,
- EF Core database connection and first migration,
- health endpoint,
- backend/frontend test project skeletons,
- CI skeleton,
- root README with exact local run commands.

Do not implement auth, workout or nutrition domain features until Phase 0 is complete and I request the next phase.
```

---

Version: 1.1-learning-r1  
Date: 29 August 2026  
Status: product requirements preserved; .NET learning protocol, staged testing progression and tutor-mode Claude handoff retained; standalone Exercises learning slice removed from the roadmap.
