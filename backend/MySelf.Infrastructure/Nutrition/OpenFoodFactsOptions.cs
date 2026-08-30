namespace MySelf.Infrastructure.Nutrition;

public sealed class OpenFoodFactsOptions
{
    public const string SectionName = "OpenFoodFacts";

    public string BaseUrl { get; set; } = "https://world.openfoodfacts.org/";

    /// <summary>How long a cached barcode result is served before re-fetching.</summary>
    public int CacheHours { get; set; } = 24;

    /// <summary>Open Food Facts requires a descriptive User-Agent on every request.</summary>
    public string UserAgent { get; set; } = "MySelfApp/0.1 (learning project)";

    public int TimeoutSeconds { get; set; } = 10;
}
