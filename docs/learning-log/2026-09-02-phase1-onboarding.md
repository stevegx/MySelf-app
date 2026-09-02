# Phase 1 — Story 2: onboarding (profile, calorie estimate, goal persistence, wizard UI)

The tutorial version of what we built on 2026-09-02. Same ground as the in-chat
explanations, organised as one document. Written for someone comfortable with JS/TS/React
who is newer to C#, ASP.NET Core and EF Core — new concepts are explained the first time
they appear, and compared to the closest TypeScript/Node equivalent where that helps.

Story 2 shipped as **four vertical slices**, one commit each:

| Slice | Commit | What |
|---|---|---|
| 2.1 | `ee5d17f` | `UserProfile` entity + `GET /me` profile summary + `PUT /me/profile` |
| 2.2 | `36e559f` | `POST /me/nutrition-estimate` — the pure calorie calculator |
| 2.3 | `646d30a` | `UserGoal` + `NutritionEstimateSnapshot` + `POST /me/onboarding/complete` + `GET /me/goals` |
| 2.4 | `1f8d740` | the 4-step onboarding wizard UI + route gating |

**Where the code lives:**
- Backend: `MySelf.Domain/Identity/` (UserProfile, UserGoal, NutritionEstimateSnapshot, AgeCalculator, enums), `MySelf.Domain/Nutrition/` (CalorieEstimator, ActivityLevel/GoalType/GoalPace), `MySelf.Api/Me/` (MeEndpoints, NutritionEstimateEndpoints, OnboardingEndpoints + their contracts), `MySelf.Infrastructure/Persistence/Configurations/` (three new config classes), two migrations.
- Frontend: `frontend/src/features/onboarding/`, `frontend/src/app/RequireOnboarding.tsx` + `RedirectIfOnboarded.tsx`.

---

## 0. Concepts you'll see over and over

**Pure domain service** — a `static class` with no constructor, no dependency injection, no
I/O. It's just a function: values in, values out. `CalorieEstimator` and `AgeCalculator`
are both this. The payoff: their tests are plain xUnit with in-memory data — no database,
no `WebApplicationFactory`, milliseconds to run. Precedent in this repo:
`Exercises/TrackingModeClassifier`.

**Entity vs. DTO vs. contract record** — an *entity* (`UserProfile`, `UserGoal`) is a class
EF Core maps to a database table. A *contract record* (`ProfileSummary`,
`NutritionEstimateResponse`) is the shape that crosses the HTTP boundary as JSON. They're
kept separate so the database schema and the API surface can evolve independently. The
handler maps one to the other.

**`DateOnly` vs `DateTimeOffset`** — `DateOnly` is a calendar date with no time and no
zone (a birthday). `DateTimeOffset` is an instant with a UTC offset (an audit timestamp).
JavaScript collapses both into `Date`; C# keeps them distinct and EF maps them to Postgres
`date` vs `timestamptz`.

**`decimal` vs `double`** — `decimal` is exact base-10; `double` is binary floating point
where `0.1 + 0.2 !== 0.3`. Every stored measurement (height, weight) and every intermediate
in the calorie maths is `decimal`. `docs/04` bans binary float for persisted numbers
because the rounding errors compound.

**EF Core migration** — you change a C# entity, run `dotnet ef migrations add <Name>`, and
EF diffs your classes against its last snapshot to generate a C# file containing the SQL
(`CreateTable`, `AddColumn`, …). `dotnet ef database update` runs it. Like a Prisma/Knex
migration, except the diff is computed from C# class shapes rather than hand-written.

**One `SaveChangesAsync` = one transaction** — the `DbContext` tracks every entity you
`Add` or mutate, then `SaveChangesAsync` emits all the INSERT/UPDATE statements inside a
single database transaction. If one fails, none commit. You rarely call
`BeginTransaction()` yourself.

---

## 1. Slice 2.1 — `UserProfile`

### The entity (`MySelf.Domain/Identity/UserProfile.cs`)

```csharp
public class UserProfile
{
    public Guid UserId { get; set; }          // PK *and* FK to AspNetUsers
    public DateOnly DateOfBirth { get; set; }
    public decimal HeightCm { get; set; }
    public CalculationSex? CalculationSex { get; set; }   // null = opted out of the estimate
    public UnitSystem UnitSystem { get; set; }
    public string? Timezone { get; set; }
    public string? Locale { get; set; }
    public DateTimeOffset? OnboardingCompletedAt { get; set; }   // null until slice 2.3 stamps it
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Two decisions worth understanding:

**Why `UserProfile` is in `Domain` but `ApplicationUser` is in `Infrastructure`.**
`MySelf.Domain` has no NuGet references on purpose — it's plain C# describing the business.
`ApplicationUser : IdentityUser<Guid>` comes from a Microsoft package, so it lives in
`Infrastructure`. `UserProfile` is pure data, so it goes in `Domain`. The link between them
is declared in the EF configuration, not as a C# navigation property — the same choice
`RefreshToken` made in the auth slice.

**Shared primary key.** `UserId` is both the table's primary key and the foreign key to
`AspNetUsers`. That makes "at most one profile per user" a schema guarantee, not something
the code has to remember to check.

### The configuration (`UserProfileConfiguration.cs`)

```csharp
builder.HasKey(p => p.UserId);
builder.Property(p => p.UserId).ValueGeneratedNever();   // the app supplies it, not the DB
builder.Property(p => p.HeightCm).HasPrecision(5, 2);    // numeric(5,2) => max 999.99
builder.Property(p => p.UnitSystem).HasConversion<string>().HasMaxLength(10);   // "Metric", not 0
builder.HasOne<ApplicationUser>().WithOne()
    .HasForeignKey<UserProfile>(p => p.UserId)
    .OnDelete(DeleteBehavior.Cascade);
```

`HasConversion<string>()` stores an enum as its **name** ("Metric") rather than an int —
readable in the database and safe if the enum is ever reordered. `HasOne().WithOne()` with
no lambdas means "one-to-one, and neither class has a property pointing at the other".

### The endpoints (`MeEndpoints.cs`)

`GET /api/v1/me` now returns `{ user, profile, currentGoal }` — `profile` is `null` until
step 1 is saved. `PUT /api/v1/me/profile` is **create-or-update**:

```csharp
var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
if (profile is null)
{
    profile = new UserProfile { UserId = userId, CreatedAt = now };
    db.UserProfiles.Add(profile);   // tracked as Added -> INSERT
}
// mutate fields on the tracked entity -> if it already existed, tracked as Modified -> UPDATE
await db.SaveChangesAsync(ct);
```

One method, two possible SQL statements, decided by whether EF is tracking the entity as
new or loaded-and-changed.

Validation returns **field errors** (`Results.ValidationProblem`) the same way the auth
endpoints do — a bad enum value comes back as `errors.unitSystem`, not a raw 400, because
the request record takes `string?` and the handler parses it with `Enum.TryParse` +
`Enum.IsDefined` (the second check rejects `"999"`, which `TryParse` alone accepts).

---

## 2. Slice 2.2 — the calorie estimator

### The pure function (`MySelf.Domain/Nutrition/CalorieEstimator.cs`)

```csharp
public static CalorieEstimateResult Estimate(CalorieEstimateInput input)
{
    if (input.AgeYears < MinAdultAge) return Unavailable("under-18");   // docs/06 §15
    if (input.GoalType == GoalType.TrackOnly) return Unavailable("track-only");

    var bmrRaw = (10m * input.WeightKg) + (6.25m * input.HeightCm)
               - (5m * input.AgeYears) + SexConstant(input.CalculationSex);   // Mifflin–St Jeor
    var maintenanceRaw = bmrRaw * ActivityFactor(input.ActivityLevel);        // TDEE
    var suggestedRaw   = maintenanceRaw + GoalAdjustment(input.GoalType, input.Pace);
    // ...macros, low-calorie-floor warning, round to whole kcal/grams at the edge
}
```

New C# bits:

**`switch` expression on a tuple** — the lookup tables:

```csharp
private static int GoalAdjustment(GoalType goal, GoalPace? pace) => (goal, pace ?? GoalPace.Standard) switch
{
    (GoalType.Maintain, _)             => 0,
    (GoalType.Lose, GoalPace.Gentle)   => -250,
    (GoalType.Lose, GoalPace.Standard) => -500,
    (GoalType.Gain, GoalPace.Gentle)   => 150,
    (GoalType.Gain, GoalPace.Standard) => 300,
    _                                  => 0,
};
```

`(goal, pace)` builds a tuple; each arm pattern-matches both slots; `_` is "anything". It's
an *expression* — it returns a value. The nearest JS equivalent is a chain of ternaries;
there's no real match-on-tuple-shape in JS.

**`decimal` maths, rounded once at the end** with
`Math.Round(x, MidpointRounding.AwayFromZero)` so `x.5` rounds up instead of C#'s default
banker's rounding.

**Two-state result without a union type.** C# has no discriminated unions, so
`CalorieEstimateResult` is one record with `IsAvailable` + `UnavailableReason` + all the
numeric fields nullable. Under-18 / track-only return every number as `null`.

### Formula versioning

```csharp
public const string FormulaName = "mifflin-st-jeor";
public const string FormulaVersion = "1.0";
```

Every response carries these, and **every constant** (activity factors, ±250/±500 presets,
macro defaults) lives next to them. Change a constant → you bump the version → a stored
estimate (slice 2.3) can always say which numbers produced it. The unit tests double as the
guard: their expected values are hand-computed from the doc, so changing a constant fails a
test and reminds you to bump the version.

### The endpoint (`NutritionEstimateEndpoints.cs`)

`POST /api/v1/me/nutrition-estimate`, `RequireAuthorization()`, **stateless** — reads
nothing, writes nothing, synchronous handler. It exists so the wizard's review step can
recompute as the user edits answers. All inputs come in the request body (not from
`UserProfile`) so it works before step 1 is even saved.

### Tests

`[Theory]` + `[InlineData]` — one test method, many rows, each reported separately (all the
auth tests were `[Fact]`, no parameters). 19 unit tests pin every constant; 6 integration
tests cover auth + validation + the not-available branch.

---

## 3. Slice 2.3 — persisting the goal

### Two new entities (`MySelf.Domain/Identity/`)

**`UserGoal`** — effective-dated. Goals are **never edited in place**; accepting a new
target inserts a new row with a later `EffectiveFrom`. "Current" goal = the row with the
newest `EffectiveFrom`:

```csharp
var currentGoal = await db.UserGoals
    .Where(g => g.UserId == user.Id)
    .OrderByDescending(g => g.EffectiveFrom)
    .FirstOrDefaultAsync(ct);
```

No `IsCurrent` boolean to keep in sync. This is what `docs/01` step 4's "effective-dated
snapshot without changing past logs" means concretely.

**`NutritionEstimateSnapshot`** — write-once audit. The inputs + formula name/version +
BMR/TDEE/adjustment/suggested behind an estimated goal. Never updated or deleted on its
own. Not enforced by a keyword — it's a discipline, so a stored estimate stays a faithful
record even after `FormulaVersion` moves past `"1.0"`.

### First one-to-many in the codebase

`UserProfile` was one-to-one (shared PK). `UserGoal` is many-per-user:

```csharp
builder.HasIndex(g => new { g.UserId, g.EffectiveFrom });     // composite
builder.HasOne<ApplicationUser>().WithMany()
    .HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
```

`WithMany()` with no argument = "the user has many of these, but `ApplicationUser` has no
`Goals` collection". The composite index exists because every query is
`WHERE UserId = @x ORDER BY EffectiveFrom DESC` — Postgres satisfies both filter and sort
from that one index.

### `POST /api/v1/me/onboarding/complete`

Requires a saved profile (`400` otherwise), runs **once** (`409` on a repeat — the first
deliberate 409 in this app). Three modes:

| Request contains | Result |
|---|---|
| `estimate` block | server recomputes from the block + the stored profile, writes `UserGoal` (source Estimated) + `NutritionEstimateSnapshot` |
| `manualTarget` block | writes `UserGoal` (source Manual), no snapshot |
| neither | "skipped" / track-only: `UserGoal` with null targets |

The whole thing is one transaction:

```csharp
db.UserGoals.Add(goal);
if (snapshot is not null) db.NutritionEstimateSnapshots.Add(snapshot);
profile.OnboardingCompletedAt = now;      // profile was loaded (tracked) => UPDATE
await db.SaveChangesAsync(ct);            // goal + snapshot + stamp, atomically
```

The test `Estimate_for_a_minor_returns_400_and_persists_nothing` proves the rollback: when
the recompute comes back not-available, the 400 leaves **no** goal row and the stamp still
`null`.

**The client can't spoof numbers.** The `estimate` block only accepts `weightKg`,
`activityLevel`, `pace` and the macro *factors* — never a calorie or macro *result*. The
handler runs `CalorieEstimator` and persists *its* output into both the goal and the
snapshot. The test asserts `goal.CalorieTarget == snapshot.SuggestedTarget`.

### Rule of three

`AgeCalculator.Years(dob, asOf)` was pulled into `Domain` this slice — once `MeEndpoints`,
`NutritionEstimateEndpoints` and `OnboardingEndpoints` all needed it. Extracting on the
*first* duplication is often premature (you don't yet know the right shape); the third use
is the signal.

---

## 4. Slice 2.4 — the wizard UI

### Structure

`OnboardingWizard.tsx` is a single component with `useState` for the answers and the step
index, and **per-step Zod schemas** (`onboardingSchema.ts`) validated with `.safeParse` on
"Continue". We deliberately did *not* use React Hook Form here — a branching multi-step form
with conditional fields (pace only for Lose/Gain, sex hidden when opted out) is clearer as
plain state than fighting one big `useForm`.

Steps: **About you → Goal → Activity & pace → Review**. The middle steps are skipped when
there's nothing to estimate:

```ts
const estimateEligible =
  !isMinor && answers.useCalculationSex && answers.goalType !== "TrackOnly";
// step 1 "Continue": estimateEligible ? go to Activity : jump straight to Review
```

### The review step

On entering, if eligible, it calls `POST /me/nutrition-estimate` and renders the breakdown
card (BMR / activity factor / maintenance / adjustment / suggested) + warnings + the exact
`docs/01` disclaimer. Actions:

- **Use this estimate** → `POST /me/onboarding/complete` with the `estimate` block
- **Set a target manually** → inline fields, prefilled from the estimate, → `complete` with `manualTarget`
- **Skip nutrition setup** → `complete` with just `goalType`

("Adjust target" from the spec is folded into "Set manually" — same capability, one fewer
sub-flow.)

### Route gating

Two guard components, mirror images, both under `RequireAuth`:

```tsx
// RequireOnboarding — wraps the app shell
if (isLoading) return null;
if (!me?.profile?.onboardingCompletedAt) return <Navigate to="/onboarding" replace />;
return <Outlet />;

// RedirectIfOnboarded — wraps /onboarding
if (me?.profile?.onboardingCompletedAt) return <Navigate to="/dashboard" replace />;
```

The key detail that avoids a flash: `useCompleteOnboarding`'s `onSuccess` does
`queryClient.invalidateQueries({ queryKey: ["me"] })`, and `invalidateQueries` resolves
**only after** the refetch completes. So by the time `await mutateAsync(...)` returns and
the wizard calls `navigate("/dashboard")`, the `["me"]` cache already has the stamped
profile and `RequireOnboarding` lets it through.

`RegisterScreen` now navigates new accounts to `/onboarding` instead of `/dashboard`.

---

## 5. Full picture: one onboarding, start to finish

```
Register ─▶ navigate("/onboarding")
  Wizard step 1 ──▶ PUT /api/v1/me/profile              → user_profiles INSERT
  Wizard steps 2–3 (local state only)
  Wizard step 4 ──▶ POST /api/v1/me/nutrition-estimate  → CalorieEstimator (nothing stored)
       "Use this estimate"
         └─▶ POST /api/v1/me/onboarding/complete
               → CalorieEstimator recomputes from the saved profile
               → one SaveChanges: user_goals INSERT + nutrition_estimate_snapshots INSERT
                                  + user_profiles.OnboardingCompletedAt UPDATE
               → invalidate ["me"] (awaits refetch)
         └─▶ navigate("/dashboard")  → RequireOnboarding sees the stamp → app shell renders
```

---

## Self-test

If you can answer these without re-reading, the session's concepts have landed:

1. Why is `UserProfile` in `Domain` but `ApplicationUser` in `Infrastructure`? What breaks
   if you add a `public ApplicationUser User { get; set; }` navigation property to
   `UserProfile`?
2. `CalorieEstimator` isn't registered in `Program.cs` and has no constructor. Why can the
   endpoint call it, and why don't its tests need `WebApplicationFactory`?
3. What does `FormulaVersion` buy that just having the formula in code doesn't? What has to
   happen — in code *and* in tests — if you change the Moderate activity factor?
4. `onboarding/complete` adds a goal, maybe adds a snapshot, and mutates the profile, then
   calls `SaveChangesAsync` once. What guarantees the profile isn't stamped "completed" if
   the snapshot insert fails?
5. There's no `IsCurrent` column on `user_goals`. How is "the current goal" determined, and
   why is that better than a boolean when a later slice adds "change my target"?
6. Completing onboarding twice returns `409`, but `PUT /me/profile` twice returns `200`.
   Why the different treatment?
7. In the wizard, why does `useCompleteOnboarding` invalidate the `["me"]` query, and why
   does the *navigate* not cause a flash back to `/onboarding`?

---

## What's next

Story 2 (onboarding) is closed. Per `docs/08` the rest of Phase 1 — protected routes and
owner-based authorization foundations — is largely already in place from Story 1. The next
roadmap item is **Phase 2: the program builder** (`docs/02` + `docs/04` §11 workout
entities).
