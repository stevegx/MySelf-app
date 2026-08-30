using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySelf.Infrastructure.Nutrition;
using MySelf.Infrastructure.Persistence;

namespace MySelf.IntegrationTests.Nutrition;

/// <summary>
/// Exercises the read-through cache in <see cref="BarcodeLookupService"/> against the real
/// database. Every test runs in a transaction that is rolled back; the Open Food Facts call
/// is a stub so nothing leaves the machine.
/// </summary>
public class BarcodeLookupTests
{
    // Synthetic barcode so the test never collides with real cached rows in the dev DB.
    private const string Barcode = "9990000000017";
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    private static MySelfDbContext CreateDbContext()
    {
        Env.NoClobber().TraversePath().Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is not set (.env).");

        return new MySelfDbContext(
            new DbContextOptionsBuilder<MySelfDbContext>().UseNpgsql(connectionString).Options);
    }

    private static BarcodeLookupService CreateService(
        MySelfDbContext db,
        StubHttpMessageHandler handler,
        TimeProvider clock)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://world.openfoodfacts.org/") };
        var options = Options.Create(new OpenFoodFactsOptions { CacheHours = 24 });
        return new BarcodeLookupService(db, new OpenFoodFactsClient(http), options, clock);
    }

    [Fact]
    public async Task Miss_fetches_and_caches__hit_serves_cache__stale_refetches()
    {
        var handler = StubHttpMessageHandler.Json(OpenFoodFactsSampleData.ProductJson(Barcode));
        var clock = new TestTimeProvider(Start);

        await using var db = CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var service = CreateService(db, handler, clock);

        // miss -> fetch + cache
        var first = await service.LookupAsync(Barcode);
        Assert.Equal(LookupOutcome.Found, first.Outcome);
        Assert.False(first.FromCache);
        Assert.Equal(539m, first.Entry!.EnergyKcalPer100g);
        Assert.Null(first.Entry.FiberPer100g); // absent upstream -> stays null, not 0
        Assert.Equal(15m, first.Entry.ServingQuantityGrams);
        Assert.Equal(1, handler.CallCount);

        // hit -> no network
        db.ChangeTracker.Clear();
        var second = await service.LookupAsync(Barcode);
        Assert.True(second.FromCache);
        Assert.False(second.Stale);
        Assert.Equal(1, handler.CallCount);

        // past TTL -> refetch
        clock.Advance(TimeSpan.FromHours(25));
        db.ChangeTracker.Clear();
        var third = await service.LookupAsync(Barcode);
        Assert.False(third.FromCache);
        Assert.Equal(2, handler.CallCount);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Unknown_barcode_returns_NotFound_and_writes_nothing()
    {
        var handler = StubHttpMessageHandler.Json(OpenFoodFactsSampleData.NotFoundJson);

        await using var db = CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var service = CreateService(db, handler, new TestTimeProvider(Start));

        var result = await service.LookupAsync("00000000");

        Assert.Equal(LookupOutcome.NotFound, result.Outcome);
        Assert.False(await db.FoodCacheEntries.AnyAsync(e => e.Barcode == "00000000"));

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Upstream_failure_with_no_cache_returns_UpstreamError()
    {
        var handler = StubHttpMessageHandler.Json("upstream down", System.Net.HttpStatusCode.ServiceUnavailable);

        await using var db = CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var service = CreateService(db, handler, new TestTimeProvider(Start));

        var result = await service.LookupAsync("12345678");

        Assert.Equal(LookupOutcome.UpstreamError, result.Outcome);

        await tx.RollbackAsync();
    }
}
