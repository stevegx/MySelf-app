namespace MySelf.IntegrationTests.Nutrition;

internal static class OpenFoodFactsSampleData
{
    /// <summary>An Open Food Facts v2 product response (trimmed). Fiber is intentionally absent.</summary>
    public static string ProductJson(string code) =>
        $$"""
        {
          "code": "{{code}}",
          "status": 1,
          "status_verbose": "product found",
          "product": {
            "product_name": "Nutella",
            "brands": "Nutella, Ferrero",
            "quantity": "400 g",
            "serving_size": "15 g",
            "serving_quantity": 15,
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
        """;

    public const string NotFoundJson = """{ "code": "x", "status": 0, "status_verbose": "product not found" }""";
}
