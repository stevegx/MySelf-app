# MySelf App — 04 Architecture Domain Api

> Split documentation file. Purpose: Technical architecture, modular monolith structure, domain model, REST API surface and API conventions.
> Original source: `myself-app-blueprint-v1.1-learning-r1.md`

## 10. Technical architecture

### Recommended stack (August 2026)

- React 19.2 + TypeScript + Vite.
- React Router.
- TanStack Query για server state και caching.
- React Hook Form + Zod για forms/validation.
- Zustand μόνο για μικρό transient client state, όχι ως δεύτερη database.
- Recharts ή lightweight SVG charts.
- ASP.NET Core Web API on .NET 10 LTS.
- Entity Framework Core 10.
- PostgreSQL.
- ASP.NET Core Identity για users/password hashing.
- OpenAPI/Swagger.
- Docker Compose για API + PostgreSQL local development. _(Deferred 2026-08-30: local PostgreSQL runs as a native install for now; Docker Compose returns once containerization is set up.)_

Η React documentation εμφανίζει την [React 19.2 ως latest](https://react.dev/versions), ενώ η Microsoft ορίζει το [.NET 10 ως LTS έως τον Νοέμβριο 2028](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support).

### Backend: modular monolith

```text
src/
  MySelf.Api/
  MySelf.Application/
  MySelf.Domain/
  MySelf.Infrastructure/
tests/
  MySelf.UnitTests/
  MySelf.IntegrationTests/
```

Feature modules:

```text
Identity
Profiles
Exercises
Programs
WorkoutSessions
Nutrition
Progress
Dashboard
Integrations
```

Κρατάμε modular monolith. Είναι πιο κατάλληλο για learning project και MVP από microservices, αλλά τα boundaries παραμένουν καθαρά.

#### Learning architecture rule

Τα τέσσερα projects (`Api`, `Application`, `Domain`, `Infrastructure`) παραμένουν ως καθαρά boundaries, αλλά **δεν δημιουργούμε abstractions χωρίς πραγματική ανάγκη μόνο και μόνο για να μοιάζει το project “enterprise”**. Empty/minimal layers επιτρέπονται στις πρώτες φάσεις. Repository/service/interface abstractions προστίθενται μόνο όταν ένα πραγματικό feature ή testability requirement τις δικαιολογεί και αφού εξηγηθεί ο ρόλος τους.

Για κάθε νέο backend abstraction, το Claude πρέπει πρώτα να εξηγεί:

- ποιο συγκεκριμένο πρόβλημα λύνει,
- τι θα ήταν η απλούστερη εναλλακτική,
- γιατί αξίζει να προστεθεί τώρα,
- ποια αρχεία και dependencies επηρεάζει.

### Frontend structure

```text
src/
  app/                 # router, providers, app shell
  features/
    auth/
    dashboard/
    workouts/
    nutrition/
    progress/
  components/ui/       # reusable primitives
  lib/                 # API client, formatting, units
  styles/              # tokens and themes
  types/
```

Το API contract παράγει TypeScript client από OpenAPI ώστε τα frontend types να μην αντιγράφονται με το χέρι.

## 11. Domain model

### Identity and profile

- `User`
- `RefreshToken`
- `UserProfile`: dateOfBirth, calculationSex, heightCm, unitSystem, timezone, locale.
- `UserGoal`: goalType (`Lose`/`Maintain`/`Gain`/`TrackOnly`), optional targetWeightKg, calorieTarget, macroTargets, source (`Estimated`/`Manual`), effectiveFrom.
- `NutritionEstimateSnapshot`: input weight/height/age/calculationSex/activityLevel, formulaName/version, BMR, TDEE, selectedAdjustment, suggestedTarget, calculatedAt; immutable audit/explanation snapshot.
- `BodyMeasurement`: measuredAt, weightKg, optional waist/bodyFat.

### Workout

- `Exercise`: name, category, primaryMuscles, secondaryMuscles, equipment, defaultTrackingMode, instructions, source, attribution.
- `WorkoutProgram`: owner, name, splitLabel, isActive, createdAt, archivedAt.
- `WorkoutGroup`: program, name, order; examples `Push`, `Pull`, `Legs`.
- `WorkoutVariant`: group, name, variantNumber, estimatedDuration, order; examples `Legs #1`, `Legs #2`.
- `VariantExercise`: variant, exercise, order, optional supersetGroupId, supersetMemberOrder, restSeconds, notes.
- `SupersetGroup`: variant, displayOrder, restAfterRoundSeconds; groups two or more `VariantExercise` records and affects execution order only.
- `SetPrescription`: variantExercise, setKind (`Standard`/`Drop`), isAmrap, targetToFailure, targetRepsMin/Max, targetWeight, optional targetRir.
- `WorkoutSession`: user, optional sourceVariantId, complete variant snapshot, startedAt, completedAt, performedOnLocalDate, status, notes; only one `InProgress` session per user.
- `ExerciseLog`: session, exercise snapshot, order, optional supersetGroupSnapshotId, supersetMemberOrder.
- `SetLog`: exerciseLog, trackingMode snapshot, setKind, isAmrap, reachedFailure, weightKg/addedWeightKg/assistanceKg, reps, durationSeconds, distanceMeters, optional rir, skippedAt/skippedReason, completedAt; required fields depend on tracking mode.
- `PersonalRecord`: user, exercise, type, value, achievedAt, sourceSetLog.

### Nutrition

- `FoodItem`: canonical nutrients per 100g, serving metadata, source, externalId.
- `CustomFood`: user-owned overlay or record.
- `Recipe` and `RecipeItem`.
- `SavedMeal`: owner, name, defaultMealType, notes, isArchived.
- `SavedMealItem`: savedMeal, food/recipe reference, defaultQuantity, unit, order.
- `MealCategory`: user-owned or system default name/order; examples Breakfast, Lunch, Snack, Pre-workout.
- `MealLog`: user, mealCategory snapshot, consumedAt.
- `MealLogItem`: food snapshot, amountGrams/servings, calculated nutrients.
- `FavoriteFood`.
- `NutritionGoal`: calories, protein, carbs, fat, effectiveFrom.

### Editing and clipboard support

- `SortOrder` on every reorderable child.
- `RowVersion`/concurrency token on editable aggregate roots.
- `ArchivedAt` or soft-delete metadata for reusable user content.
- `BulkOperation`: operation id, user, type, status, affected entities, createdAt; useful for idempotency and undo.
- Clipboard selection itself μπορεί να μένει client-side, αλλά copy/move/delete commands εκτελούνται και επικυρώνονται server-side.

### Κρίσιμη αρχή: snapshots

Plan edits δεν πρέπει να αλλάζουν το ιστορικό. Τα workout sessions αποθηκεύουν snapshot των prescriptions και τα meal items snapshot των nutrients τη στιγμή της καταγραφής.

## 12. API surface v1

Όλα κάτω από `/api/v1`. Τα list endpoints έχουν pagination, search και stable sorting.

### Auth and profile

| Method | Endpoint | Purpose |
| --- | --- | --- |
| POST | `/auth/register` | Create account |
| POST | `/auth/login` | Start session |
| POST | `/auth/refresh` | Rotate refresh token |
| POST | `/auth/logout` | Revoke current refresh token |
| POST | `/auth/forgot-password` | Request reset |
| POST | `/auth/reset-password` | Complete reset |
| GET | `/me` | Current user + profile summary |
| PUT | `/me/profile` | Update profile/preferences |
| GET/POST | `/me/goals` | Goal history and new goal |
| POST | `/me/nutrition-estimate` | Return non-persisted BMR/TDEE/goal breakdown with warnings and formula version |
| POST | `/me/onboarding/complete` | Persist profile plus selected/manual goal or explicit nutrition skip |
| GET/POST | `/me/body-measurements` | Weight/progress logs |

### Exercises and programs

| Method | Endpoint | Purpose |
| --- | --- | --- |
| GET | `/exercises` | Search/filter catalogue |
| GET | `/exercises/{id}` | Exercise details |
| POST | `/exercises` | Create custom exercise |
| GET/POST | `/programs` | List/create user programs |
| GET/PUT/DELETE | `/programs/{id}` | Manage a program |
| POST | `/programs/{id}/groups` | Add Push/Pull/Legs or any custom group |
| POST | `/workout-groups/{id}/variants` | Add a concrete workout such as Legs #2 |
| PUT | `/workout-variants/{id}` | Update exercises, order and prescriptions |
| POST | `/programs/{id}/activate` | Activate chosen program |
| POST | `/programs/{id}/clone` | Clone before major changes |
| POST | `/workout-variants/{id}/exercises/bulk-copy` | Copy selected exercises into a target variant |
| POST | `/workout-variants/{id}/exercises/bulk-move` | Move selected exercises atomically |
| POST | `/workout-variants/{id}/supersets` | Group selected exercises and set member order/rest after round |
| PUT/DELETE | `/superset-groups/{id}` | Edit order/rest or ungroup without deleting exercises |
| PATCH | `/variant-exercises/bulk` | Bulk edit compatible fields/order |
| DELETE | `/variant-exercises/bulk` | Recoverable bulk removal |

### Sessions

| Method | Endpoint | Purpose |
| --- | --- | --- |
| POST | `/workout-sessions` | Start a manually selected variant or ad-hoc session; reject/resolve a second active session |
| GET | `/workout-sessions/active` | Resume active session |
| GET/PUT | `/workout-sessions/{id}` | Read/update session |
| POST | `/workout-sessions/{id}/set-logs` | Add/update a logged set |
| POST | `/workout-sessions/{id}/sets/{setId}/skip` | Explicitly skip with optional reason |
| POST | `/workout-sessions/{id}/exercises` | Add exercise to current session |
| POST | `/workout-sessions/{id}/exercises/{exerciseLogId}/replace` | Replace today-only or request future template update |
| POST | `/workout-sessions/{id}/complete` | Finalize and calculate PRs |
| GET | `/workout-sessions` | History with filters |
| GET | `/workout-calendar?from=&to=` | Sessions grouped by the user's local calendar date |
| GET | `/exercises/{id}/history?variantId=` | Performance trend across all sessions or one variant |

### Nutrition

| Method | Endpoint | Purpose |
| --- | --- | --- |
| GET | `/foods/search?q=` | Search the user's saved `My Foods`; external text search is later |
| GET | `/foods/barcode/{code}` | Barcode lookup |
| POST | `/foods/custom` | Create user food |
| GET/POST | `/recipes` | Manage recipes |
| GET/POST | `/saved-meals` | List/create reusable meals |
| GET/PUT/DELETE | `/saved-meals/{id}` | Edit/archive a saved meal |
| POST | `/saved-meals/{id}/add-to-day` | Create editable meal-log snapshot, optionally scaled |
| GET | `/nutrition-days/{date}` | Day totals and meals |
| POST | `/meal-logs` | Add meal |
| POST | `/meal-logs/{id}/items` | Add food item |
| PUT/DELETE | `/meal-log-items/{id}` | Edit/remove logged food |
| POST | `/nutrition-days/{date}/copy` | Copy from another date |
| POST | `/nutrition-days/{date}/copy-to` | Copy selected meals/items into one or more dates |
| POST | `/nutrition-days/{date}/move-to` | Move selected meals/items atomically |
| PATCH/DELETE | `/meal-log-items/bulk` | Bulk edit or recoverable removal |
| GET/POST | `/meal-categories` | List/create customizable meal categories |
| PUT/DELETE | `/meal-categories/{id}` | Rename/reorder/archive custom category |

### Dashboard

| Method | Endpoint | Purpose |
| --- | --- | --- |
| GET | `/dashboard?date=` | One aggregated response for first paint |
| GET | `/analytics/strength?exerciseId=&range=` | e1RM/volume trend |
| GET | `/analytics/weight?range=` | Raw + rolling-average weight |
| GET | `/analytics/nutrition?range=` | Calories/macros adherence |

### API conventions

- UTC timestamps in storage, user timezone only at display/boundaries.
- Decimal weights/nutrients; never binary float for persisted totals.
- `ProblemDetails` for errors.
- Server-side validation for every calculation-affecting field.
- Concurrency token on program and active session updates.
- Idempotency key on `complete workout` and copy operations.
- Bulk mutations are transactional and return affected ids plus an optional undo token.
- Copy creates new ids and independent snapshots; move preserves identity where the parent/date change allows it.
- Destructive paste over an occupied target requires explicit conflict strategy: `append`, `replace selected`, or `cancel`.
