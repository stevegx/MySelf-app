using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Me;

/// <summary>
/// Drives body-weight logging and the trend (docs/08 Phase 4, #30): POST/GET/DELETE
/// /api/v1/me/body-measurements and GET /api/v1/analytics/weight, against the shared dev
/// database. Each test registers a unique user; the cascade FK removes the readings with
/// the account.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class BodyMeasurementEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Logs_a_reading_and_reads_it_back_newest_first()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var first = await client.PostAsJsonAsync("/api/v1/me/body-measurements", new { weightKg = 82.4m, localDate = "2026-09-01" });
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            var body = await first.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(82.4m, body.GetProperty("weightKg").GetDecimal());
            Assert.Equal("2026-09-01", body.GetProperty("localDate").GetString());

            await client.PostAsJsonAsync("/api/v1/me/body-measurements", new { weightKg = 81.9m, localDate = "2026-09-03" });

            var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/body-measurements");
            var items = list.EnumerateArray().ToList();
            Assert.Equal(2, items.Count);
            Assert.Equal("2026-09-03", items[0].GetProperty("localDate").GetString()); // newest first
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Analytics_averages_a_day_then_rolls_it_over_seven_days()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            // Two readings on day 1 (avg 80.5), one each on days 2 and 8.
            await client.PostAsJsonAsync("/api/v1/me/body-measurements", new { weightKg = 80m, localDate = "2026-09-01" });
            await client.PostAsJsonAsync("/api/v1/me/body-measurements", new { weightKg = 81m, localDate = "2026-09-01" });
            await client.PostAsJsonAsync("/api/v1/me/body-measurements", new { weightKg = 80m, localDate = "2026-09-02" });
            await client.PostAsJsonAsync("/api/v1/me/body-measurements", new { weightKg = 79m, localDate = "2026-09-08" });

            var trend = await client.GetFromJsonAsync<JsonElement>(
                "/api/v1/analytics/weight?from=2026-09-01&to=2026-09-30");

            var points = trend.GetProperty("points").EnumerateArray().ToList();
            Assert.Equal(3, points.Count);
            Assert.Equal(80.5m, points[0].GetProperty("average").GetDecimal());
            Assert.Equal("2026-09-01", points[0].GetProperty("date").GetString());

            // Day 8's window is days 2..8 -> only days 2 (80) and 8 (79) fall in it -> 79.5.
            Assert.Equal(79.5m, points[^1].GetProperty("rollingAverage").GetDecimal());
            Assert.Equal(79m, trend.GetProperty("latest").GetDecimal());
            Assert.Equal("2026-09-08", trend.GetProperty("latestOn").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Rejects_an_out_of_range_weight()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var tooLight = await client.PostAsJsonAsync("/api/v1/me/body-measurements", new { weightKg = 5m });
            Assert.Equal(HttpStatusCode.BadRequest, tooLight.StatusCode);

            var tooHeavy = await client.PostAsJsonAsync("/api/v1/me/body-measurements", new { weightKg = 900m });
            Assert.Equal(HttpStatusCode.BadRequest, tooHeavy.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Deletes_only_the_callers_own_reading()
    {
        var (alice, aliceEmail) = await factory.RegisterAndAuthenticateAsync();
        var (bob, bobEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var created = await alice.PostAsJsonAsync("/api/v1/me/body-measurements", new { weightKg = 77m });
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/me/body-measurements/{id}")).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/v1/me/body-measurements/{id}")).StatusCode);
            var afterDelete = await alice.GetFromJsonAsync<JsonElement>("/api/v1/me/body-measurements");
            Assert.Empty(afterDelete.EnumerateArray());
        }
        finally
        {
            await factory.DeleteUsersAsync(aliceEmail, bobEmail);
        }
    }
}
