using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySelf.Infrastructure.Persistence;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Me;

/// <summary>
/// Drives PUT /api/v1/me/profile (onboarding step 1) and its round-trip through GET /api/v1/me,
/// against the shared dev database. Each test registers a unique user and deletes it afterwards;
/// the cascade FK on user_profiles removes the profile row with the account.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ProfileEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly object ValidProfile = new
    {
        unitSystem = "Metric",
        dateOfBirth = "1994-03-21",
        heightCm = 178.5,
        calculationSex = "Female",
        timezone = "Europe/Athens",
        locale = "en-GB",
    };

    [Fact]
    public async Task Put_profile_then_get_me_returns_the_saved_profile()
    {
        var (client, email) = await RegisterAsync();

        try
        {
            var put = await client.PutAsJsonAsync("/api/v1/me/profile", ValidProfile);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);

            var putBody = await put.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Metric", putBody.GetProperty("unitSystem").GetString());
            Assert.Equal("Female", putBody.GetProperty("calculationSex").GetString());
            Assert.Equal("1994-03-21", putBody.GetProperty("dateOfBirth").GetString());
            Assert.Equal(178.5, putBody.GetProperty("heightCm").GetDouble());
            Assert.Equal(JsonValueKind.Null, putBody.GetProperty("onboardingCompletedAt").ValueKind);

            var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/me");
            var profile = me.GetProperty("profile");
            Assert.Equal(JsonValueKind.Object, profile.ValueKind);
            Assert.Equal("Europe/Athens", profile.GetProperty("timezone").GetString());
            Assert.Equal("en-GB", profile.GetProperty("locale").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_profile_twice_updates_the_same_row()
    {
        var (client, email) = await RegisterAsync();

        try
        {
            var first = await client.PutAsJsonAsync("/api/v1/me/profile", ValidProfile);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await client.PutAsJsonAsync(
                "/api/v1/me/profile",
                new { unitSystem = "Imperial", dateOfBirth = "1988-01-02", heightCm = 165.0, calculationSex = "Male" });
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);

            var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/me");
            var profile = me.GetProperty("profile");
            Assert.Equal("Imperial", profile.GetProperty("unitSystem").GetString());
            Assert.Equal(165.0, profile.GetProperty("heightCm").GetDouble());

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.Equal(1, await db.UserProfiles.CountAsync(p => p.UserId == user.Id));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_profile_without_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/me/profile", ValidProfile);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_profile_with_future_date_of_birth_returns_400_with_field_error()
    {
        var (client, email) = await RegisterAsync();

        try
        {
            var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd");
            var response = await client.PutAsJsonAsync(
                "/api/v1/me/profile",
                new { unitSystem = "Metric", dateOfBirth = tomorrow, heightCm = 170.0 });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("dateOfBirth", out var dobErrors));
            Assert.True(dobErrors.GetArrayLength() > 0);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_profile_omitting_calculation_sex_stores_null_the_estimate_opt_out()
    {
        var (client, email) = await RegisterAsync();

        try
        {
            var response = await client.PutAsJsonAsync(
                "/api/v1/me/profile",
                new { unitSystem = "Metric", dateOfBirth = "1994-03-21", heightCm = 178.0 });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Null, body.GetProperty("calculationSex").ValueKind);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    /// <summary>Registers a unique user and returns a client with its bearer token attached.</summary>
    private async Task<(HttpClient Client, string Email)> RegisterAsync()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { username, email, password = "Str0ng!Passw0rd" });
        register.EnsureSuccessStatusCode();

        var body = await register.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return (client, email);
    }
}
