using MySelf.Domain.Nutrition;

namespace MySelf.Api.Nutrition;

/// <summary>A saved "My Foods" entry (GET /api/v1/foods/search, POST /api/v1/foods/custom).
/// Nutrients are per <see cref="ServingBasis"/>.</summary>
public sealed record CustomFoodResponse(
    Guid Id,
    string Name,
    string? Brand,
    string? Barcode,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbG,
    decimal FatG)
{
    public static CustomFoodResponse From(CustomFood f) => new(
        f.Id, f.Name, f.Brand, f.Barcode, f.ServingBasis.ToString(), f.ServingSizeGrams,
        f.Kcal, f.ProteinG, f.CarbG, f.FatG);
}

/// <summary>POST /api/v1/foods/custom.</summary>
public sealed record CreateCustomFoodRequest(
    string Name,
    string? Brand,
    string? Barcode,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbG,
    decimal FatG);
