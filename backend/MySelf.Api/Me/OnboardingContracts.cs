using MySelf.Domain.Identity;

namespace MySelf.Api.Me;

/// <summary>
/// Body of POST /api/v1/me/onboarding/complete. Exactly one of <see cref="Estimate"/> /
/// <see cref="ManualTarget"/> may be set; both null means "nutrition skipped". Height, date
/// of birth and calculation sex are NOT here — they come from the profile saved earlier by
/// PUT /me/profile, which this endpoint requires to already exist.
/// </summary>
public sealed record CompleteOnboardingRequest(
    string? GoalType,
    decimal? TargetWeightKg,
    OnboardingEstimateChoice? Estimate,
    OnboardingManualTarget? ManualTarget);

/// <summary>
/// The answers still needed to run the estimator when the user accepts a calculated target.
/// The server recomputes from these + the stored profile and stores its own numbers.
/// </summary>
public sealed record OnboardingEstimateChoice(
    decimal? WeightKg,
    string? ActivityLevel,
    string? Pace,
    decimal? ProteinFactor,
    decimal? FatFactor);

/// <summary>A target the user typed in (docs/01 step 4 "Set manually"). Macros are optional.</summary>
public sealed record OnboardingManualTarget(
    int? CalorieTarget,
    int? ProteinGrams,
    int? CarbGrams,
    int? FatGrams);

/// <summary>One row of goal history (GET /me/goals) and the current goal on GET /me.</summary>
public sealed record GoalSummary(
    Guid Id,
    string GoalType,
    string Source,
    decimal? TargetWeightKg,
    int? CalorieTarget,
    int? ProteinGrams,
    int? CarbGrams,
    int? FatGrams,
    DateTimeOffset EffectiveFrom)
{
    public static GoalSummary From(UserGoal g) => new(
        g.Id,
        g.GoalType.ToString(),
        g.Source.ToString(),
        g.TargetWeightKg,
        g.CalorieTarget,
        g.ProteinGrams,
        g.CarbGrams,
        g.FatGrams,
        g.EffectiveFrom);
}
