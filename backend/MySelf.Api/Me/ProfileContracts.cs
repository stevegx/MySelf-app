using MySelf.Api.Auth;

namespace MySelf.Api.Me;

/// <summary>
/// The profile half of GET /api/v1/me. Null in <see cref="MeResponse"/> until the user saves
/// onboarding step 1. <c>CalculationSex</c>/<c>UnitSystem</c> are emitted as their enum names
/// ("Male", "Metric") — projected to string here so no global JSON enum-converter config is
/// needed for this one slice.
/// </summary>
public sealed record ProfileSummary(
    DateOnly DateOfBirth,
    decimal HeightCm,
    string? CalculationSex,
    string UnitSystem,
    string? Timezone,
    string? Locale,
    DateTimeOffset? OnboardingCompletedAt,
    bool WarnOffFocusExercises);

/// <summary>Body of PUT /api/v1/me/preferences — small UI toggles, no onboarding fields.</summary>
public sealed record UpdatePreferencesRequest(bool WarnOffFocusExercises);

/// <summary>
/// GET /api/v1/me — account summary, onboarding profile, and the current nutrition goal
/// (docs/04: "Current user + profile summary"). <see cref="Profile"/> is null until onboarding
/// step 1; <see cref="CurrentGoal"/> is null until onboarding is completed.
/// </summary>
public sealed record MeResponse(AuthUser User, ProfileSummary? Profile, GoalSummary? CurrentGoal);

/// <summary>
/// Body of PUT /api/v1/me/profile (onboarding step 1). Enums come in as strings and are
/// parsed in the handler so a bad value becomes a field error rather than a 400 from the
/// JSON binder. Every property is nullable so "absent" is distinguishable from a sent value
/// during validation; <c>CalculationSex</c>, <c>Timezone</c> and <c>Locale</c> stay optional
/// by product rule, the rest are required.
/// </summary>
public sealed record UpdateProfileRequest(
    string? UnitSystem,
    DateOnly? DateOfBirth,
    decimal? HeightCm,
    string? CalculationSex,
    string? Timezone,
    string? Locale);
