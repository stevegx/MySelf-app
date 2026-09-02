using MySelf.Domain.Identity;
using MySelf.Domain.Nutrition;

namespace MySelf.UnitTests.Nutrition;

/// <summary>
/// Unit tests for the pure BMR → TDEE → adjustment → macro pipeline (docs/03 §8.2–8.5).
/// Every expected number here is computed by hand from the formulas in the doc, so a change
/// to a constant (activity factor, adjustment preset) breaks a test on purpose — that is the
/// signal to also bump <see cref="CalorieEstimator.FormulaVersion"/>.
/// </summary>
public class CalorieEstimatorTests
{
    private static CalorieEstimateInput Input(
        int age = 30,
        CalculationSex sex = CalculationSex.Male,
        decimal heightCm = 180m,
        decimal weightKg = 80m,
        ActivityLevel activity = ActivityLevel.Moderate,
        GoalType goal = GoalType.Maintain,
        GoalPace? pace = null,
        decimal proteinFactor = 1.6m,
        decimal fatFactor = 0.8m) =>
        new(age, sex, heightCm, weightKg, activity, goal, pace, proteinFactor, fatFactor);

    [Theory]
    // Male constant is +5, female is −161: same body, BMR differs by 166.
    [InlineData(CalculationSex.Male, 1659)]
    [InlineData(CalculationSex.Female, 1493)]
    public void Bmr_uses_the_sex_specific_constant(CalculationSex sex, int expectedBmr)
    {
        // W 70, H 175, A 28: 10*70 + 6.25*175 − 5*28 (+5 | −161)
        var result = CalorieEstimator.Estimate(Input(age: 28, sex: sex, heightCm: 175m, weightKg: 70m));

        Assert.Equal(expectedBmr, result.Bmr);
    }

    [Theory]
    // Base BMR (M, W80 H180 A30) = 1780. Maintenance = round(1780 * factor).
    [InlineData(ActivityLevel.Sedentary, 1.20, 2136)]
    [InlineData(ActivityLevel.Light, 1.375, 2448)]
    [InlineData(ActivityLevel.Moderate, 1.55, 2759)]
    [InlineData(ActivityLevel.VeryActive, 1.725, 3071)]
    [InlineData(ActivityLevel.ExtraActive, 1.90, 3382)]
    public void Tdee_applies_the_activity_factor(ActivityLevel level, double expectedFactor, int expectedMaintenance)
    {
        var result = CalorieEstimator.Estimate(Input(activity: level, goal: GoalType.Maintain));

        Assert.Equal(1780, result.Bmr);
        Assert.Equal((decimal)expectedFactor, result.ActivityFactor);
        Assert.Equal(expectedMaintenance, result.MaintenanceCalories);
    }

    [Theory]
    [InlineData(GoalType.Maintain, null, 0)]
    [InlineData(GoalType.Lose, GoalPace.Gentle, -250)]
    [InlineData(GoalType.Lose, GoalPace.Standard, -500)]
    [InlineData(GoalType.Gain, GoalPace.Gentle, 150)]
    [InlineData(GoalType.Gain, GoalPace.Standard, 300)]
    public void Goal_adjustment_matches_the_preset(GoalType goal, GoalPace? pace, int expectedAdjustment)
    {
        var result = CalorieEstimator.Estimate(Input(goal: goal, pace: pace));

        Assert.Equal(expectedAdjustment, result.GoalAdjustment);
        Assert.Equal(result.MaintenanceCalories + expectedAdjustment, result.SuggestedCalories);
    }

    [Fact]
    public void Macros_split_protein_and_fat_by_body_weight_then_carbs_take_the_rest()
    {
        // M, W80 H180 A30, Moderate, Lose/Standard: BMR 1780, maint 2759, target 2259.
        var result = CalorieEstimator.Estimate(Input(goal: GoalType.Lose, pace: GoalPace.Standard));

        var macros = Assert.IsType<MacroTargets>(result.Macros);
        Assert.Equal(128, macros.ProteinGrams); // 80 * 1.6
        Assert.Equal(64, macros.FatGrams);      // 80 * 0.8
        Assert.Equal(293, macros.CarbGrams);    // (2259 − 512 − 576) / 4, rounded

        var macroKcal = (macros.ProteinGrams * 4) + (macros.CarbGrams * 4) + (macros.FatGrams * 9);
        Assert.InRange(macroKcal, result.SuggestedCalories!.Value - 3, result.SuggestedCalories.Value + 3);
    }

    [Fact]
    public void Custom_macro_factors_are_used_and_echoed_back()
    {
        var result = CalorieEstimator.Estimate(Input(weightKg: 90m, proteinFactor: 2.0m, fatFactor: 0.6m));

        var macros = Assert.IsType<MacroTargets>(result.Macros);
        Assert.Equal(180, macros.ProteinGrams); // 90 * 2.0
        Assert.Equal(54, macros.FatGrams);      // 90 * 0.6
        Assert.Equal(2.0m, macros.ProteinFactor);
        Assert.Equal(0.6m, macros.FatFactor);
    }

    [Fact]
    public void Suggested_target_below_the_floor_attaches_a_warning_but_still_returns_numbers()
    {
        // Small, older, sedentary, losing hard: F, W45 H150 A65, Sedentary, Lose/Standard.
        // BMR 902, maintenance 1082, suggested 582 (< 1200).
        var result = CalorieEstimator.Estimate(Input(
            age: 65, sex: CalculationSex.Female, heightCm: 150m, weightKg: 45m,
            activity: ActivityLevel.Sedentary, goal: GoalType.Lose, pace: GoalPace.Standard));

        Assert.True(result.IsAvailable);
        Assert.Equal(582, result.SuggestedCalories);
        Assert.Contains(result.Warnings, w => w.Code == "low-calorie-floor");
        // Carbs would be negative here; they clamp at zero rather than going below.
        Assert.Equal(0, result.Macros!.CarbGrams);
    }

    [Fact]
    public void A_comfortable_target_has_no_warnings()
    {
        var result = CalorieEstimator.Estimate(Input(goal: GoalType.Maintain));

        Assert.True(result.IsAvailable);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Under_18_returns_no_estimate_and_no_numbers()
    {
        var result = CalorieEstimator.Estimate(Input(age: 16, goal: GoalType.Lose, pace: GoalPace.Standard));

        Assert.False(result.IsAvailable);
        Assert.Equal("under-18", result.UnavailableReason);
        Assert.Null(result.Bmr);
        Assert.Null(result.SuggestedCalories);
        Assert.Null(result.Macros);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Track_only_returns_no_estimate()
    {
        var result = CalorieEstimator.Estimate(Input(goal: GoalType.TrackOnly));

        Assert.False(result.IsAvailable);
        Assert.Equal("track-only", result.UnavailableReason);
        Assert.Null(result.SuggestedCalories);
    }

    [Fact]
    public void Age_is_checked_before_track_only_so_a_minor_never_sees_a_target_path()
    {
        var result = CalorieEstimator.Estimate(Input(age: 15, goal: GoalType.TrackOnly));

        Assert.Equal("under-18", result.UnavailableReason);
    }
}
