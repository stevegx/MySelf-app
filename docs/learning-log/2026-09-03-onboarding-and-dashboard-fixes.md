# Onboarding + dashboard fixes

Written 2026-09-03, same day as the [Phase 2 gap closure](./2026-09-03-phase2-gap-closure.md)
but a separate batch — bugs found by clicking through the running app. Four small,
separately-committed fixes.

Audience: comfortable with React/TypeScript, newer to the .NET side.

---

## 1. Onboarding never saved the profile → completing it 400'd

**What went wrong:** finishing onboarding failed with

> Save your profile (PUT /api/v1/me/profile) before completing onboarding.

**Why:** the API has three onboarding endpoints:

| Endpoint | Job |
|---|---|
| `PUT /api/v1/me/profile` | create/update the `UserProfile` (date of birth, height, sex, units) |
| `POST /api/v1/me/nutrition-estimate` | a *pure calculation* — takes all its inputs in the body, stores nothing |
| `POST /api/v1/me/onboarding/complete` | writes the `UserGoal`, stamps `OnboardingCompletedAt` |

`POST …/onboarding/complete` **reads the saved `UserProfile`** for the age / height / sex it
needs, and returns `400` if there's no profile row.

The wizard collected the step-1 fields and passed them straight to the *estimate* endpoint
(which doesn't persist), then called *complete*. It never called `PUT /me/profile` — in
fact **no code anywhere in the frontend called it**. The endpoint was built and tested in
Phase 1, but the wizard UI (a later slice) never wired it up.

**The fix:** a `useSaveProfile()` hook (`PUT /api/v1/me/profile`), called from the wizard's
`finish()` right before `POST …/onboarding/complete`:

```ts
await saveProfileMutation.mutateAsync({
  unitSystem: answers.unitSystem,
  dateOfBirth: answers.dateOfBirth,       // "YYYY-MM-DD"
  heightCm: Number(answers.heightCm),
  calculationSex: answers.useCalculationSex ? (answers.calculationSex || null) : null,
  timezone: Intl.DateTimeFormat().resolvedOptions().timeZone || null,
});
await completeMutation.mutateAsync(input);
```

`PUT /me/profile` is **create-or-update and idempotent** (a second call lands on the same row
via the shared primary key), so calling it once at the end of every finish path
(estimate / manual target / skip) is safe. All three of those buttons route through
`finish()`, so one call covers them.

**.NET note — why the estimate endpoint needs no profile but complete does:** the estimator
is a *pure function of its inputs* (Mifflin–St Jeor from age/height/weight/sex/activity), so
it takes them in the request and returns a number. Completing onboarding is the step that
*persists state*, and persisted goals are effective-dated history that reference the profile
— so the profile has to exist first. Keeping the pure calc separate from the write is a
deliberate split (`NutritionEstimateEndpoints` vs `OnboardingEndpoints`).

## 2. Date of birth: native `<input type="date">` → Day / Month / Year dropdowns

Paging a browser's native date calendar back ~30 years to a birth year is miserable.
`DateOfBirthPicker.tsx` is three `<select>`s (Day / Month / Year, year list going back 120
years). It emits the **same `"YYYY-MM-DD"` string** the wizard already stored, so nothing
downstream changed — `answers.dateOfBirth`, the zod schema, the estimate call all stayed as
they were.

**React note — partial state:** an ISO date string can't represent "year chosen, month not
yet". A naive controlled component that only lifts a complete date would lose the year the
moment you touch the month dropdown. So the picker keeps its **own** `useState({ y, m, d })`,
seeded once from `value`, and only calls `onChange` with a full `"YYYY-MM-DD"` (or `""` while
incomplete). It also clamps the day to the selected month's length (no 31 February).

## 3. Target weight has to move the right way for the goal

On the goal step, an entered target weight is now sanity-checked against current weight:

- **Lose** → target must be **below** current weight
- **Gain** → target must be **above** current weight
- Maintain / Track-only → the field isn't shown

Still optional — skipped entirely when no target is given.

**Zod note — cross-field rules:** a single field's validator can't see another field.
`goalSchema` now also takes `currentWeightKg` (already validated back on step 1) and uses
`.superRefine((v, ctx) => …)` — the hook that runs *after* the per-field checks and can
inspect the whole object, calling `ctx.addIssue({ path: ["targetWeightKg"], … })` to attach
the error to the right field. This is client-side only: the target weight is advisory ("for
context only", it doesn't feed the calorie estimate), not a data-integrity field.

## 4. Dashboard + Nutrition screens showed fake numbers

Both the dashboard's "Nutrition today" card and the Nutrition screen hardcoded
`2,104 kcal / 112g / 288g / 56g`. They now show the **real** targets from the goal set during
onboarding.

- `useNutritionTargets()` — a small hook that pulls `currentGoal` out of the cached
  `GET /api/v1/me` response and returns `{ hasTarget, calorieTarget, proteinGrams,
  carbGrams, fatGrams, goalType }`.
- `formatTarget(n)` — whole number with locale grouping (`toLocaleString`), `"—"` when null.
- When the goal has no calorie target (nutrition skipped, or a Track-only goal), the cards
  fall back to `0 kcal` / `"—"` with a "No calorie target — set one in Settings" hint instead
  of inventing numbers.

The *consumed* side stays `0` everywhere — meal logging is a later phase; only the targets
are real now.

**TanStack Query note:** `useNutritionTargets` just calls `useMe()` again. That doesn't fire
a second request — the query is keyed `["me", accessToken]`, so every component that calls
`useMe()` reads the same cached result. Sharing server state by cache key, not by prop
drilling, is the pattern.

---

## Files

| File | What |
|---|---|
| `features/onboarding/useSaveProfile.ts` | **new** — `PUT /api/v1/me/profile` mutation |
| `features/onboarding/DateOfBirthPicker.tsx` | **new** — Day/Month/Year dropdowns |
| `features/onboarding/OnboardingWizard.tsx` | save profile in `finish()`; use the new picker |
| `features/onboarding/onboardingSchema.ts` | `goalSchema` cross-checks target vs current weight |
| `features/nutrition/useNutritionTargets.ts` | **new** — real targets from `GET /me` + `formatTarget` |
| `features/dashboard/DashboardScreen.tsx` | calorie/macro numbers from the goal |
| `features/nutrition/NutritionScreen.tsx` | same |

Tests: +4 frontend (profile saved before complete; wrong-direction target blocked; dashboard
shows real targets; dashboard falls back with no goal). **45 frontend green.**
