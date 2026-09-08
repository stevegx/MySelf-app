using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Nutrition;

/// <summary>
/// Drives GET /api/v1/analytics/nutrition (docs/04 §12): logged calories/macros per day over
/// a window, plus the average across days that actually have a log.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class NutritionAnalyticsEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private static async Task LogAsync(HttpClient client, string date, double kcal, double amount) =>
        (await client.PostAsJsonAsync($"/api/v1/nutrition-days/{date}/items", new
        {
            category = "Lunch",
            name = "Test food",
            servingBasis = "Per100g",
            servingSizeGrams = (double?)null,
            perBasisKcal = kcal,
            perBasisProteinG = 10.0,
            perBasisCarbG = 5.0,
            perBasisFatG = 2.0,
            amount,
            unit = "Grams",
        })).EnsureSuccessStatusCode();

    [Fact]
    public async Task Reports_per_day_totals_and_the_average_over_logged_days()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            await LogAsync(client, "2026-09-05", kcal: 100, amount: 200); // 200 kcal
            await LogAsync(client, "2026-09-07", kcal: 100, amount: 400); // 400 kcal

            var result = await client.GetFromJsonAsync<JsonElement>(
                "/api/v1/analytics/nutrition?from=2026-09-01&to=2026-09-10");

            var days = result.GetProperty("days").EnumerateArray().ToList();
            Assert.Equal(2, days.Count); // the empty days in between are omitted
            Assert.Equal("2026-09-05", days[0].GetProperty("date").GetString());
            Assert.Equal(200m, days[0].GetProperty("kcal").GetDecimal());
            Assert.Equal(400m, days[1].GetProperty("kcal").GetDecimal());

            Assert.Equal(2, result.GetProperty("daysLogged").GetInt32());
            Assert.Equal(300m, result.GetProperty("average").GetProperty("kcal").GetDecimal());
            // No goal for a fresh user -> null target.
            Assert.Equal(JsonValueKind.Null, result.GetProperty("targets").GetProperty("kcal").ValueKind);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task An_empty_range_reports_zero_days()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var result = await client.GetFromJsonAsync<JsonElement>(
                "/api/v1/analytics/nutrition?from=2020-01-01&to=2020-01-31");
            Assert.Equal(0, result.GetProperty("daysLogged").GetInt32());
            Assert.Empty(result.GetProperty("days").EnumerateArray());
            Assert.Equal(0m, result.GetProperty("average").GetProperty("kcal").GetDecimal());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }
}
