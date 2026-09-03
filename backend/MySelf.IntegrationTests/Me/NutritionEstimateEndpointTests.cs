using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Me;

/// <summary>
/// Drives POST /api/v1/me/nutrition-estimate through the real pipeline. The calculation itself
/// is covered exhaustively by CalorieEstimatorTests (unit); these tests check auth, request
/// validation and the response shape/contract (formula version, disclaimer, the
/// not-available branch).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class NutritionEstimateEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private static object ValidBody(string dateOfBirth = "1994-03-21", string goalType = "Lose", string? pace = "Standard") => new
    {
        dateOfBirth,
        calculationSex = "Female",
        heightCm = 170.0,
        weightKg = 68.0,
        activityLevel = "Moderate",
        goalType,
        pace,
    };

    [Fact]
    public async Task Valid_request_returns_200_with_the_breakdown_and_formula_version()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();

        try
        {
            var response = await client.PostAsJsonAsync("/api/v1/me/nutrition-estimate", ValidBody());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("mifflin-st-jeor", body.GetProperty("formulaName").GetString());
            Assert.False(string.IsNullOrEmpty(body.GetProperty("formulaVersion").GetString()));
            Assert.True(body.GetProperty("nutritionEstimateAvailable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("unavailableReason").ValueKind);
            Assert.True(body.GetProperty("bmr").GetInt32() > 0);
            Assert.True(body.GetProperty("maintenanceCalories").GetInt32() > body.GetProperty("suggestedCalories").GetInt32());
            Assert.Equal(-500, body.GetProperty("goalAdjustment").GetInt32());
            Assert.True(body.GetProperty("macros").GetProperty("proteinGrams").GetInt32() > 0);
            Assert.Contains("not medical advice", body.GetProperty("disclaimer").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Under_18_returns_200_but_no_estimate()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();

        try
        {
            var sixteenYearsAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16)).ToString("yyyy-MM-dd");
            var response = await client.PostAsJsonAsync(
                "/api/v1/me/nutrition-estimate",
                ValidBody(dateOfBirth: sixteenYearsAgo));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.GetProperty("nutritionEstimateAvailable").GetBoolean());
            Assert.Equal("under-18", body.GetProperty("unavailableReason").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("bmr").ValueKind);
            Assert.Equal(JsonValueKind.Null, body.GetProperty("macros").ValueKind);
            // The disclaimer still travels, even when there is nothing to disclaim about.
            Assert.False(string.IsNullOrEmpty(body.GetProperty("disclaimer").GetString()));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task No_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/me/nutrition-estimate", ValidBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lose_goal_without_a_pace_returns_400_with_a_pace_error()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/me/nutrition-estimate",
                ValidBody(goalType: "Lose", pace: null));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("pace", out var paceErrors));
            Assert.True(paceErrors.GetArrayLength() > 0);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Unknown_activity_level_returns_400()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/me/nutrition-estimate",
                new
                {
                    dateOfBirth = "1994-03-21",
                    calculationSex = "Female",
                    heightCm = 170.0,
                    weightKg = 68.0,
                    activityLevel = "SuperActive",
                    goalType = "Maintain",
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("activityLevel", out _));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Protein_factor_out_of_range_returns_400()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/me/nutrition-estimate",
                new
                {
                    dateOfBirth = "1994-03-21",
                    calculationSex = "Male",
                    heightCm = 180.0,
                    weightKg = 80.0,
                    activityLevel = "Light",
                    goalType = "Maintain",
                    proteinFactor = 3.5,
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("proteinFactor", out _));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }
}
