using System.Text.Json;
using MySelf.Domain.Nutrition;

namespace MySelf.Infrastructure.Nutrition;

public static class OpenFoodFactsMapper
{
    public const string SourceName = "openfoodfacts";

    /// <summary>True when the response describes a real, mappable product.</summary>
    public static bool IsProduct(OffResponse? response) =>
        response is { Status: 1, Product: not null };

    /// <summary>
    /// Applies an Open Food Facts response onto a cache entry. Only the mapped fields are
    /// touched — <c>Id</c>, <c>Source</c>, <c>Barcode</c>, <c>FetchedAt</c>, <c>RawPayload</c>
    /// stay the caller's responsibility.
    /// </summary>
    public static void Apply(OffResponse response, FoodCacheEntry target)
    {
        var product = response.Product
            ?? throw new InvalidOperationException("Response has no product; check IsProduct first.");
        var n = product.Nutriments;

        target.Name = Trimmed(product.ProductName);
        target.Brand = Trimmed(product.Brands);
        target.PackageQuantityRaw = Trimmed(product.Quantity);
        target.ServingSizeRaw = Trimmed(product.ServingSize);
        target.ServingQuantityGrams = ReadNumber(product.ServingQuantity);
        target.SourceUrl = response.Code is { Length: > 0 } code
            ? $"https://world.openfoodfacts.org/product/{code}"
            : null;
        target.SourceLastModified = product.LastModifiedT is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(product.LastModifiedT.Value)
            : null;

        target.EnergyKcalPer100g = Nutrient(n, "energy-kcal_100g");
        target.ProteinPer100g = Nutrient(n, "proteins_100g");
        target.CarbsPer100g = Nutrient(n, "carbohydrates_100g");
        target.FatPer100g = Nutrient(n, "fat_100g");
        target.SaturatedFatPer100g = Nutrient(n, "saturated-fat_100g");
        target.SugarsPer100g = Nutrient(n, "sugars_100g");
        target.FiberPer100g = Nutrient(n, "fiber_100g");
        target.SaltPer100g = Nutrient(n, "salt_100g");
        target.SodiumPer100g = Nutrient(n, "sodium_100g");
    }

    private static string? Trimmed(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static decimal? Nutrient(Dictionary<string, JsonElement>? nutriments, string key) =>
        nutriments is not null && nutriments.TryGetValue(key, out var element)
            ? ReadNumber(element)
            : null;

    private static decimal? ReadNumber(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
        JsonValueKind.String when decimal.TryParse(
            element.GetString(),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var s) => s,
        _ => null,
    };
}
