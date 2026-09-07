namespace MySelf.Api.Nutrition;

/// <summary>A calorie + macro figure. Used for the day's running totals and, nullably, for
/// the user's targets (null components when no goal / a Track-only goal).</summary>
public sealed record NutrientTotals(decimal Kcal, decimal ProteinG, decimal CarbG, decimal FatG);

public sealed record NutrientTargets(int? Kcal, int? ProteinG, int? CarbG, int? FatG);

/// <summary>GET /api/v1/nutrition-days/{date} — the four meal slots for a day, their items,
/// the day's totals and the user's current targets.</summary>
public sealed record NutritionDayResponse(
    DateOnly Date,
    NutrientTargets Targets,
    NutrientTotals Totals,
    IReadOnlyList<MealResponse> Meals);

public sealed record MealResponse(
    Guid? MealLogId,
    string Category,
    NutrientTotals Subtotals,
    IReadOnlyList<MealItemResponse> Items);

public sealed record MealItemResponse(
    Guid Id,
    int SortOrder,
    string Name,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal Amount,
    string Unit,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbG,
    decimal FatG);

/// <summary>POST /api/v1/nutrition-days/{date}/items — log one food into a meal slot. The
/// per-basis nutrients are the food's declared values; the server scales them to the
/// eaten amount and snapshots everything.</summary>
public sealed record AddMealItemRequest(
    string Category,
    string Name,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal PerBasisKcal,
    decimal PerBasisProteinG,
    decimal PerBasisCarbG,
    decimal PerBasisFatG,
    decimal Amount,
    string Unit);

/// <summary>PUT /api/v1/meal-log-items/{id} — change how much was eaten; the food itself
/// (name, per-basis nutrients) is left as first logged.</summary>
public sealed record UpdateMealItemRequest(decimal Amount, string Unit, decimal? ServingSizeGrams);
