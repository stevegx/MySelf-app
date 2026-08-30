using MySelf.Domain.Nutrition;

namespace MySelf.Api.Nutrition;

public sealed record NutrientsPer100g(
    decimal? EnergyKcal,
    decimal? Protein,
    decimal? Carbs,
    decimal? Fat,
    decimal? SaturatedFat,
    decimal? Sugars,
    decimal? Fiber,
    decimal? Salt,
    decimal? Sodium);

public sealed record BarcodeFoodResponse(
    string Barcode,
    string? Name,
    string? Brand,
    string Source,
    string License,
    string? SourceUrl,
    NutrientsPer100g Per100g,
    string? ServingSize,
    decimal? ServingQuantityGrams,
    string? PackageQuantity,
    DateTimeOffset FetchedAt,
    DateTimeOffset? SourceLastModified,
    bool Cached,
    bool Stale)
{
    public static BarcodeFoodResponse From(FoodCacheEntry e, bool cached, bool stale) => new(
        e.Barcode,
        e.Name,
        e.Brand,
        Source: "Open Food Facts",
        License: "ODbL",
        e.SourceUrl,
        new NutrientsPer100g(
            e.EnergyKcalPer100g,
            e.ProteinPer100g,
            e.CarbsPer100g,
            e.FatPer100g,
            e.SaturatedFatPer100g,
            e.SugarsPer100g,
            e.FiberPer100g,
            e.SaltPer100g,
            e.SodiumPer100g),
        e.ServingSizeRaw,
        e.ServingQuantityGrams,
        e.PackageQuantityRaw,
        e.FetchedAt,
        e.SourceLastModified,
        cached,
        stale);
}
