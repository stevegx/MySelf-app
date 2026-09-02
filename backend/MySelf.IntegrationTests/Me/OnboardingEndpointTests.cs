using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySelf.Infrastructure.Persistence;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Me;

/// <summary>
/// Drives POST /api/v1/me/onboarding/complete and GET /api/v1/me/goals. Covers the three
/// completion modes (estimate / manual / skip), the transaction (goal + snapshot + profile
/// stamp land together), the once-only guard, and the not-available branches.
/// </summary>
public class OnboardingEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static object Profile(string dateOfBirth = "1994-03-21", string? calculationSex = "Female") => new
    {
        unitSystem = "Metric",
        dateOfBirth,
        heightCm = 170.0,
        calculationSex,
    };

    private static object EstimateComplete(string goalType = "Lose", string? pace = "Standard") => new
    {
        goalType,
        estimate = new { weightKg = 68.0, activityLevel = "Moderate", pace },
    };

    /// <summary>Register + authenticate + save a profile — the state onboarding/complete needs.</summary>
    private async Task<(HttpClient Client, string Email)> UserWithProfileAsync(object? profile = null)
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        var put = await client.PutAsJsonAsync("/api/v1/me/profile", profile ?? Profile());
        put.EnsureSuccessStatusCode();
        return (client, email);
    }

    [Fact]
    public async Task Estimate_path_writes_goal_plus_snapshot_and_marks_onboarding_complete()
    {
        var (client, email) = await UserWithProfileAsync();

        try
        {
            var response = await client.PostAsJsonAsync("/api/v1/me/onboarding/complete", EstimateComplete());

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var goal = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Lose", goal.GetProperty("goalType").GetString());
            Assert.Equal("Estimated", goal.GetProperty("source").GetString());
            Assert.True(goal.GetProperty("calorieTarget").GetInt32() > 0);

            // GET /me now reflects the completion.
            var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/me");
            Assert.NotEqual(JsonValueKind.Null, me.GetProperty("currentGoal").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, me.GetProperty("profile").GetProperty("onboardingCompletedAt").ValueKind);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            var stored = await db.UserGoals.SingleAsync(g => g.UserId == user.Id);
            var snapshot = await db.NutritionEstimateSnapshots.SingleAsync(s => s.UserGoalId == stored.Id);
            Assert.Equal(stored.CalorieTarget, snapshot.SuggestedTarget); // goal target came from the snapshotted calc
            Assert.Equal("1.0", snapshot.FormulaVersion);
            Assert.True(snapshot.Bmr > 0 && snapshot.Tdee > snapshot.SuggestedTarget);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Manual_path_writes_a_manual_goal_and_no_snapshot()
    {
        var (client, email) = await UserWithProfileAsync();

        try
        {
            var response = await client.PostAsJsonAsync("/api/v1/me/onboarding/complete", new
            {
                goalType = "Maintain",
                manualTarget = new { calorieTarget = 2100, proteinGrams = 150, carbGrams = 210, fatGrams = 60 },
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var goal = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Manual", goal.GetProperty("source").GetString());
            Assert.Equal(2100, goal.GetProperty("calorieTarget").GetInt32());

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.False(await db.NutritionEstimateSnapshots.AnyAsync(s => s.UserId == user.Id));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Skip_path_records_a_goal_with_null_targets_and_completes()
    {
        var (client, email) = await UserWithProfileAsync();

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/me/onboarding/complete",
                new { goalType = "TrackOnly" });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var goal = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("TrackOnly", goal.GetProperty("goalType").GetString());
            Assert.Equal(JsonValueKind.Null, goal.GetProperty("calorieTarget").ValueKind);

            var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/me");
            Assert.NotEqual(JsonValueKind.Null, me.GetProperty("profile").GetProperty("onboardingCompletedAt").ValueKind);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Complete_without_a_saved_profile_returns_400()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();

        try
        {
            var response = await client.PostAsJsonAsync("/api/v1/me/onboarding/complete", EstimateComplete());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Second_complete_returns_409()
    {
        var (client, email) = await UserWithProfileAsync();

        try
        {
            var first = await client.PostAsJsonAsync("/api/v1/me/onboarding/complete", EstimateComplete());
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var second = await client.PostAsJsonAsync("/api/v1/me/onboarding/complete", EstimateComplete());
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Estimate_for_a_minor_returns_400_and_persists_nothing()
    {
        var fifteenYearsAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-15)).ToString("yyyy-MM-dd");
        var (client, email) = await UserWithProfileAsync(Profile(dateOfBirth: fifteenYearsAgo));

        try
        {
            var response = await client.PostAsJsonAsync("/api/v1/me/onboarding/complete", EstimateComplete());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("estimate", out _));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.False(await db.UserGoals.AnyAsync(g => g.UserId == user.Id));
            var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user.Id);
            Assert.Null(profile.OnboardingCompletedAt);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Lose_estimate_without_a_pace_returns_400_with_a_pace_error()
    {
        var (client, email) = await UserWithProfileAsync();

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/me/onboarding/complete",
                EstimateComplete(goalType: "Lose", pace: null));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("pace", out _));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Get_goals_is_empty_before_completion_and_has_the_goal_after()
    {
        var (client, email) = await UserWithProfileAsync();

        try
        {
            var before = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/goals");
            Assert.Equal(0, before.GetArrayLength());

            await client.PostAsJsonAsync("/api/v1/me/onboarding/complete", EstimateComplete());

            var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/goals");
            Assert.Equal(1, after.GetArrayLength());
            Assert.Equal("Estimated", after[0].GetProperty("source").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task No_token_returns_401_on_both_routes()
    {
        var client = factory.CreateClient();

        var complete = await client.PostAsJsonAsync("/api/v1/me/onboarding/complete", EstimateComplete());
        var goals = await client.GetAsync("/api/v1/me/goals");

        Assert.Equal(HttpStatusCode.Unauthorized, complete.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, goals.StatusCode);
    }
}
