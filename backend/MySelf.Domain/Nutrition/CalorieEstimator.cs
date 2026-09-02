using MySelf.Domain.Identity;

namespace MySelf.Domain.Nutrition;

/// <summary>Everything the calorie estimate needs. Assumed already validated by the caller.</summary>
public sealed record CalorieEstimateInput(
    int AgeYears,
    CalculationSex CalculationSex,
    decimal HeightCm,
    decimal WeightKg,
    ActivityLevel ActivityLevel,
    GoalType GoalType,
    GoalPace? Pace,
    decimal ProteinFactor,
    decimal FatFactor);

/// <summary>A protein/fat/carb split in grams, plus the g/kg factors that produced it.</summary>
public sealed record MacroTargets(
    int ProteinGrams,
    int FatGrams,
    int CarbGrams,
    decimal ProteinFactor,
    decimal FatFactor);

/// <summary>A non-blocking safety note attached to a result (docs/06 §15).</summary>
public sealed record EstimateWarning(string Code, string Message);

/// <summary>
/// The estimate breakdown. When <see cref="IsAvailable"/> is false (under-18 or track-only)
/// every numeric field is null and <see cref="UnavailableReason"/> explains why — the app
/// then continues with manual targets only (docs/01 step 1, docs/06 §15).
/// </summary>
public sealed record CalorieEstimateResult(
    bool IsAvailable,
    string? UnavailableReason,
    int? Bmr,
    decimal? ActivityFactor,
    int? MaintenanceCalories,
    int? GoalAdjustment,
    int? SuggestedCalories,
    MacroTargets? Macros,
    IReadOnlyList<EstimateWarning> Warnings);

/// <summary>
/// Turns onboarding answers into the BMR → TDEE → goal-adjustment → suggested-target
/// breakdown and a macro split (docs/03 §8.2–8.5). Pure: no I/O, no clock, no DI — the same
/// shape as <c>Exercises/TrackingModeClassifier</c>.
///
/// The constants here (activity factors, adjustment presets, macro defaults) ARE the formula:
/// <see cref="FormulaVersion"/> is bumped whenever any of them change, so a stored estimate
/// can always say which numbers produced it.
/// </summary>
public static class CalorieEstimator
{
    public const string FormulaName = "mifflin-st-jeor";
    public const string FormulaVersion = "1.0";

    public const int MinAdultAge = 18;

    /// <summary>Below this suggested target we attach a warning (not a block) — see docs/03 §8.4.</summary>
    public const int LowCalorieFloor = 1200;

    public const decimal DefaultProteinFactor = 1.6m;
    public const decimal MinProteinFactor = 1.4m;
    public const decimal MaxProteinFactor = 2.0m;

    public const decimal DefaultFatFactor = 0.8m;
    public const decimal MinFatFactor = 0.6m;
    public const decimal MaxFatFactor = 1.0m;

    private const string UnderageReason = "under-18";
    private const string TrackOnlyReason = "track-only";

    public static CalorieEstimateResult Estimate(CalorieEstimateInput input)
    {
        // Order matters: age is the stronger safety gate, so it wins if both apply.
        if (input.AgeYears < MinAdultAge)
        {
            return Unavailable(UnderageReason);
        }

        if (input.GoalType == GoalType.TrackOnly)
        {
            return Unavailable(TrackOnlyReason);
        }

        var bmrRaw = (10m * input.WeightKg)
            + (6.25m * input.HeightCm)
            - (5m * input.AgeYears)
            + SexConstant(input.CalculationSex);

        var activityFactor = ActivityFactor(input.ActivityLevel);
        var maintenanceRaw = bmrRaw * activityFactor;

        var adjustment = GoalAdjustment(input.GoalType, input.Pace);
        var suggestedRaw = maintenanceRaw + adjustment;

        var suggested = RoundToInt(suggestedRaw);
        var macros = BuildMacros(input.WeightKg, suggested, input.ProteinFactor, input.FatFactor);

        var warnings = new List<EstimateWarning>();
        if (suggested < LowCalorieFloor)
        {
            warnings.Add(new EstimateWarning(
                "low-calorie-floor",
                $"This works out below {LowCalorieFloor} kcal/day. Consider a gentler pace or advice from a qualified professional."));
        }

        return new CalorieEstimateResult(
            IsAvailable: true,
            UnavailableReason: null,
            Bmr: RoundToInt(bmrRaw),
            ActivityFactor: activityFactor,
            MaintenanceCalories: RoundToInt(maintenanceRaw),
            GoalAdjustment: adjustment,
            SuggestedCalories: suggested,
            Macros: macros,
            Warnings: warnings);
    }

    private static CalorieEstimateResult Unavailable(string reason) =>
        new(false, reason, null, null, null, null, null, null, []);

    private static int SexConstant(CalculationSex sex) =>
        sex == CalculationSex.Male ? 5 : -161;

    private static decimal ActivityFactor(ActivityLevel level) => level switch
    {
        ActivityLevel.Sedentary => 1.20m,
        ActivityLevel.Light => 1.375m,
        ActivityLevel.Moderate => 1.55m,
        ActivityLevel.VeryActive => 1.725m,
        ActivityLevel.ExtraActive => 1.90m,
        _ => 1.20m,
    };

    private static int GoalAdjustment(GoalType goal, GoalPace? pace) => (goal, pace ?? GoalPace.Standard) switch
    {
        (GoalType.Maintain, _) => 0,
        (GoalType.Lose, GoalPace.Gentle) => -250,
        (GoalType.Lose, GoalPace.Standard) => -500,
        (GoalType.Gain, GoalPace.Gentle) => 150,
        (GoalType.Gain, GoalPace.Standard) => 300,
        _ => 0,
    };

    private static MacroTargets BuildMacros(decimal weightKg, int calorieTarget, decimal proteinFactor, decimal fatFactor)
    {
        var proteinGrams = RoundToInt(weightKg * proteinFactor);
        var fatGrams = RoundToInt(weightKg * fatFactor);

        // Carbs are whatever energy is left after protein (4 kcal/g) and fat (9 kcal/g).
        // Clamp at zero: a very low target with a high protein/fat floor can leave nothing,
        // and the low-calorie-floor warning already covers that territory.
        var carbKcal = calorieTarget - (proteinGrams * 4) - (fatGrams * 9);
        var carbGrams = Math.Max(0, RoundToInt(carbKcal / 4m));

        return new MacroTargets(proteinGrams, fatGrams, carbGrams, proteinFactor, fatFactor);
    }

    private static int RoundToInt(decimal value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
