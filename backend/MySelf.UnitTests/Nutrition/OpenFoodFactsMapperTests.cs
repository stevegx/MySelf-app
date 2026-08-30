using System.Text.Json;
using System.Text.Json.Serialization;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Nutrition;

namespace MySelf.UnitTests.Nutrition;

public class OpenFoodFactsMapperTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private static OffResponse Parse(string body) =>
        JsonSerializer.Deserialize<OffResponse>(body, Json)!;

    [Fact]
    public void Apply_maps_nutrients_serving_and_metadata()
    {
        var response = Parse(
            """
            {
              "code": "3017620422003",
              "status": 1,
              "product": {
                "product_name": "  Nutella  ",
                "brands": "Ferrero",
                "quantity": "400 g",
                "serving_size": "15 g",
                "serving_quantity": "15",
                "last_modified_t": 1788100933,
                "nutriments": {
                  "energy-kcal_100g": 539,
                  "proteins_100g": 6.3,
                  "carbohydrates_100g": 57.5,
                  "fat_100g": 30.9,
                  "saturated-fat_100g": 10.6,
                  "sugars_100g": 56.3,
                  "salt_100g": 0.107,
                  "sodium_100g": 0.0428
                }
              }
            }
            """);

        var entry = new FoodCacheEntry { Source = "openfoodfacts", Barcode = "3017620422003" };
        OpenFoodFactsMapper.Apply(response, entry);

        Assert.Equal("Nutella", entry.Name); // trimmed
        Assert.Equal("Ferrero", entry.Brand);
        Assert.Equal("400 g", entry.PackageQuantityRaw);
        Assert.Equal("15 g", entry.ServingSizeRaw);
        Assert.Equal(15m, entry.ServingQuantityGrams); // string "15" -> decimal
        Assert.Equal(539m, entry.EnergyKcalPer100g);
        Assert.Equal(0.0428m, entry.SodiumPer100g);
        Assert.Null(entry.FiberPer100g); // key absent -> null, not 0
        Assert.Equal("https://world.openfoodfacts.org/product/3017620422003", entry.SourceUrl);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1788100933), entry.SourceLastModified);
    }

    [Fact]
    public void IsProduct_is_false_for_status_zero()
    {
        var response = Parse("""{ "code": "x", "status": 0, "status_verbose": "not found" }""");
        Assert.False(OpenFoodFactsMapper.IsProduct(response));
    }

    [Fact]
    public void Apply_tolerates_a_product_with_no_nutriments()
    {
        var response = Parse("""{ "code": "x", "status": 1, "product": { "product_name": "Mystery" } }""");

        var entry = new FoodCacheEntry { Source = "openfoodfacts", Barcode = "x" };
        OpenFoodFactsMapper.Apply(response, entry);

        Assert.Equal("Mystery", entry.Name);
        Assert.Null(entry.EnergyKcalPer100g);
        Assert.Null(entry.ProteinPer100g);
    }
}
