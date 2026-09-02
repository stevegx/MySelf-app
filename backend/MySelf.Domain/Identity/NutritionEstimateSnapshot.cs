using MySelf.Domain.Nutrition;

namespace MySelf.Domain.Identity;

/// <summary>
/// An immutable record of one calorie-estimate calculation and the answers behind it
/// (docs/04 §11: "immutable audit/explanation snapshot"). Written only when a
/// <see cref="UserGoal"/> is created from the estimator; never updated or deleted on its own.
/// Keeps a suggested target explainable even after the formula constants change and
/// <see cref="CalorieEstimator.FormulaVersion"/> moves on.
/// </summary>
public class NutritionEstimateSnapshot
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The goal this calculation produced.</summary>
    public Guid UserGoalId { get; set; }

    // --- inputs at the time of the estimate ---
    public decimal WeightKg { get; set; }
    public decimal HeightCm { get; set; }
    public int AgeYears { get; set; }
    public CalculationSex CalculationSex { get; set; }
    public ActivityLevel ActivityLevel { get; set; }

    // --- formula identity + outputs ---
    public required string FormulaName { get; set; }
    public required string FormulaVersion { get; set; }
    public int Bmr { get; set; }
    public int Tdee { get; set; }
    public int SelectedAdjustment { get; set; }
    public int SuggestedTarget { get; set; }

    public DateTimeOffset CalculatedAt { get; set; }
}
