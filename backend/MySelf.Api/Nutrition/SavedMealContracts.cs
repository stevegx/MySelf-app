namespace MySelf.Api.Nutrition;

/// <summary>One food in a saved-meal template. The <c>PerBasis*</c> values are the declared
/// nutrients; <c>Kcal</c>/… are those at the default amount (multiplier 1×).</summary>
public sealed record SavedMealItemResponse(
    Guid Id,
    int SortOrder,
    string Name,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal PerBasisKcal,
    decimal PerBasisProteinG,
    decimal PerBasisCarbG,
    decimal PerBasisFatG,
    decimal DefaultAmount,
    string Unit,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbG,
    decimal FatG);

/// <summary>GET /api/v1/saved-meals[/{id}]. <c>Totals</c> is the meal at multiplier 1×.</summary>
public sealed record SavedMealResponse(
    Guid Id,
    string Name,
    string Category,
    string? Notes,
    NutrientTotals Totals,
    IReadOnlyList<SavedMealItemResponse> Items);

public sealed record SaveMealItemInput(
    string Name,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal PerBasisKcal,
    decimal PerBasisProteinG,
    decimal PerBasisCarbG,
    decimal PerBasisFatG,
    decimal DefaultAmount,
    string Unit);

/// <summary>POST / PUT /api/v1/saved-meals[/{id}] — the whole template.</summary>
public sealed record UpsertSavedMealRequest(
    string Name,
    string Category,
    string? Notes,
    IReadOnlyList<SaveMealItemInput> Items);

/// <summary>POST /api/v1/saved-meals/{id}/add-to-day. Category falls back to the saved
/// meal's own; multiplier defaults to 1×.</summary>
public sealed record AddSavedMealToDayRequest(string Date, string? Category, decimal? Multiplier);
