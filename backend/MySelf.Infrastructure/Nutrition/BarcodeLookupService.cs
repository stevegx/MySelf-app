using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Infrastructure.Nutrition;

public enum LookupOutcome
{
    Found,
    NotFound,
    UpstreamError,
}

public sealed record BarcodeLookupResult(
    LookupOutcome Outcome,
    FoodCacheEntry? Entry,
    bool FromCache,
    bool Stale,
    string? Error)
{
    public static BarcodeLookupResult Hit(FoodCacheEntry entry, bool fromCache, bool stale) =>
        new(LookupOutcome.Found, entry, fromCache, stale, null);

    public static BarcodeLookupResult Missing() =>
        new(LookupOutcome.NotFound, null, false, false, null);

    public static BarcodeLookupResult Upstream(string error) =>
        new(LookupOutcome.UpstreamError, null, false, false, error);
}

/// <summary>
/// Read-through cache over Open Food Facts (docs/03 "integration cache"). Fresh cache hits are
/// served without a network call; misses and stale entries trigger a fetch and upsert. When the
/// upstream is unavailable but a stale entry exists, the stale entry is served.
/// </summary>
public sealed class BarcodeLookupService(
    MySelfDbContext db,
    OpenFoodFactsClient client,
    IOptions<OpenFoodFactsOptions> options,
    TimeProvider clock)
{
    public async Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var ttl = TimeSpan.FromHours(options.Value.CacheHours);

        var existing = await db.Set<FoodCacheEntry>()
            .FirstOrDefaultAsync(e => e.Source == OpenFoodFactsMapper.SourceName && e.Barcode == barcode, ct);

        if (existing is not null && now - existing.FetchedAt < ttl)
        {
            return BarcodeLookupResult.Hit(existing, fromCache: true, stale: false);
        }

        OffFetch fetch;
        try
        {
            fetch = await client.GetProductAsync(barcode, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return existing is not null
                ? BarcodeLookupResult.Hit(existing, fromCache: true, stale: true)
                : BarcodeLookupResult.Upstream(ex.Message);
        }

        if (!OpenFoodFactsMapper.IsProduct(fetch.Response))
        {
            // Upstream no longer has it, but we cached it before — keep serving the stale copy.
            return existing is not null
                ? BarcodeLookupResult.Hit(existing, fromCache: true, stale: true)
                : BarcodeLookupResult.Missing();
        }

        var entry = existing ?? new FoodCacheEntry
        {
            Id = Guid.NewGuid(),
            Source = OpenFoodFactsMapper.SourceName,
            Barcode = barcode,
        };

        OpenFoodFactsMapper.Apply(fetch.Response!, entry);
        entry.FetchedAt = now;
        entry.RawPayload = string.IsNullOrEmpty(fetch.RawJson) ? entry.RawPayload : fetch.RawJson;

        if (existing is null)
        {
            db.Add(entry);
        }

        await db.SaveChangesAsync(ct);
        return BarcodeLookupResult.Hit(entry, fromCache: false, stale: false);
    }
}
