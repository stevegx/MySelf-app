# Learning log

Tutorial-style write-ups, one per working session, explaining what was built and why —
aimed at a developer new to C#/.NET (see `docs/07`'s learning protocol, which these
implement as a durable artifact instead of only living in chat).

| Date | Session | Covers |
| --- | --- | --- |
| 2026-08-31 | [Phase 1 — Auth](./2026-08-31-phase1-auth.md) | Register, login, JWT-bearer middleware + `/me`, trust-device + `/refresh` with cookie rotation, `/logout`, password reset (dev-sink) |
| 2026-09-02 | [Phase 1 — Onboarding](./2026-09-02-phase1-onboarding.md) | `UserProfile` + `PUT /me/profile`, the pure `CalorieEstimator` + `POST /me/nutrition-estimate`, `UserGoal` / `NutritionEstimateSnapshot` + `POST /me/onboarding/complete` (one transaction), the 4-step wizard UI + route gating |
| 2026-09-02 | [Phase 2 — Program builder](./2026-09-02-phase2-program-builder.md) | Program/group/variant/exercise/set-prescription/superset model + CRUD, ownership checks, `PUT /workout-variants/{id}` whole-body replace, activation transaction + filtered unique index, `GET /exercises` search, the builder UI. **Includes a full drawbacks/risks/gaps review.** |
| 2026-09-03 | [Phase 2 — Gap closure](./2026-09-03-phase2-gap-closure.md) | 11 slices: input caps + rate limiting, centralised ownership filter, `xmin` concurrency enforcement, program clone, bulk copy/move exercises, the rebuilt per-set variant editor + superset UI + unsaved guard, `@dnd-kit` reorder + wired reorder endpoints, un-archive, styled confirm dialogs, Respawn-isolated integration tests + CI catalogue seed |
| 2026-09-03 | [Onboarding + dashboard fixes](./2026-09-03-onboarding-and-dashboard-fixes.md) | Wizard now saves the profile (`PUT /me/profile`) before completing; Day/Month/Year date-of-birth dropdowns; target-weight direction check vs current weight; dashboard + nutrition screens show the real calorie/macro targets from `GET /me` instead of hardcoded numbers |
