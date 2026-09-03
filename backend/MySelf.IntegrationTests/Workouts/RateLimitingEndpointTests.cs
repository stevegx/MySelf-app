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
}
