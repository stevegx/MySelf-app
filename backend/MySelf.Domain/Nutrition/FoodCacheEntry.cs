namespace MySelf.Domain.Nutrition;

/// <summary>
/// A packaged food looked up from an external database (Open Food Facts) by barcode, and
/// cached (docs/03: "short-term caching for barcode results"). Nutrients are per 100 g and
/// nullable — missing data stays unknown, never 0 (docs/06).
/// </summary>
public class FoodCacheEntry
{
    public Guid Id { get; set; }

    public required string Source { get; set; } // "openfoodfacts"
    public required string Barcode { get; set; }

    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? SourceUrl { get; set; }

    public decimal? EnergyKcalPer100g { get; set; }
    public decimal? ProteinPer100g { get; set; }
    public decimal? CarbsPer100g { get; set; }
    public decimal? FatPer100g { get; set; }
    public decimal? SaturatedFatPer100g { get; set; }
    public decimal? SugarsPer100g { get; set; }
    public decimal? FiberPer100g { get; set; }
    public decimal? SaltPer100g { get; set; }
    public decimal? SodiumPer100g { get; set; }

    public string? ServingSizeRaw { get; set; } // e.g. "30 g"
    public decimal? ServingQuantityGrams { get; set; }
    public string? PackageQuantityRaw { get; set; } // e.g. "400 g"

    public DateTimeOffset? SourceLastModified { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>The raw upstream JSON, kept so the mapping can be reworked without re-fetching.</summary>
    public string? RawPayload { get; set; }
}
