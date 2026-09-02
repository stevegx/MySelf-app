using MySelf.Domain.Nutrition;

namespace MySelf.Api.Me;

/// <summary>
/// Body of POST /api/v1/me/nutrition-estimate. Enums arrive as strings and are parsed in the
/// handler so a bad value is a field error, not a raw 400 from the JSON binder. All fields
/// nullable so "absent" is distinguishable during validation; <c>Pace</c>/<c>ProteinFactor</c>/
/// <c>FatFactor</c> are genuinely optional (Pace only matters for Lose/Gain; the factors
/// default to 1.6 / 0.8 g/kg).
/// </summary>
public sealed record NutritionEstimateRequest(
    DateOnly? DateOfBirth,
    string? CalculationSex,
    decimal? HeightCm,
    decimal? WeightKg,
    string? ActivityLevel,
    string? GoalType,
    string? Pace,
    decimal? ProteinFactor,
    decimal? FatFactor);

/// <summary>
/// The non-persisted estimate breakdown (docs/01 step 4). When
/// <see cref="NutritionEstimateAvailable"/> is false the numeric fields are null and
/// <see cref="UnavailableReason"/> says why ("under-18" or "track-only"). <see cref="Macros"/>
/// and <see cref="Warnings"/> reuse the domain records directly — they are already plain data.
/// </summary>
public sealed record NutritionEstimateResponse(
    string FormulaName,
    string FormulaVersion,
    bool NutritionEstimateAvailable,
    string? UnavailableReason,
    int? Age,
    int? Bmr,
    decimal? ActivityFactor,
    int? MaintenanceCalories,
    int? GoalAdjustment,
    int? SuggestedCalories,
    MacroTargets? Macros,
    IReadOnlyList<EstimateWarning> Warnings,
    string Disclaimer);
