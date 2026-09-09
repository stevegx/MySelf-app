using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Workouts;

/// <summary>
/// The rest of the suite runs with rate limiting off (see <see cref="TestBootstrap"/>). This
/// class opts back in with a deliberately tiny "write" budget so a short loop trips a 429.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RateLimitingEndpointTests(RateLimitingEndpointTests.ThrottledFactory factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<RateLimitingEndpointTests.ThrottledFactory>
{
    public sealed class ThrottledFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var overrides = new Dictionary<string, string?>
            {
                ["RateLimiting:Enabled"] = "true",
                ["RateLimiting:WindowSeconds"] = "60",
                ["RateLimiting:WritePermitLimit"] = "3",
                ["RateLimiting:LookupPermitLimit"] = "2",
                ["RateLimiting:AuthPermitLimit"] = "1000",
                ["RateLimiting:GlobalPermitLimit"] = "1000",
            };

            foreach (var (key, value) in overrides)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(overrides));
        }
    }

    [Fact]
    public async Task Write_policy_returns_429_once_the_budget_is_spent()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 8; i++)
            {
                var res = await client.PostAsJsonAsync(
                    "/api/v1/programs", new { name = $"P{i}", splitLabel = (string?)null });
                statuses.Add(res.StatusCode);
            }

            Assert.Contains(HttpStatusCode.Created, statuses);
            Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Write_policy_now_covers_the_profile_and_preferences_writes()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var statuses = new List<HttpStatusCode>();
            // A fresh user has no profile yet, so each call 409s — the point is that the
            // limiter runs first and starts returning 429 once the write budget (3) is spent.
            for (var i = 0; i < 8; i++)
            {
                var res = await client.PutAsJsonAsync(
                    "/api/v1/me/preferences", new { warnOffFocusExercises = true });
                statuses.Add(res.StatusCode);
            }

            Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Lookup_policy_caps_the_anonymous_barcode_endpoint()
    {
        var client = factory.CreateClient(); // barcode lookup is reachable without a token

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            // "123" fails the format check (8–14 digits) and 400s before any Open Food Facts
            // call — but the limiter still counts it, so the budget (2) is spent and 429s follow.
            var res = await client.GetAsync("/api/v1/foods/barcode/123");
            statuses.Add(res.StatusCode);
        }

        Assert.Contains(HttpStatusCode.BadRequest, statuses);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
