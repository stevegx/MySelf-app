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
/// Drives data portability (docs/05 §13): GET /api/v1/me/export and DELETE /api/v1/me.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AccountDataEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private static object Meal(string category, string name, double kcal, double amount) => new
    {
        category,
        name,
        servingBasis = "Per100g",
        servingSizeGrams = (double?)null,
        perBasisKcal = kcal,
        perBasisProteinG = 5.0,
        perBasisCarbG = 10.0,
        perBasisFatG = 2.0,
        amount,
        unit = "Grams",
    };

    [Fact]
    public async Task Export_contains_the_callers_logged_meal_and_program()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/v1/nutrition-days/2026-09-08/items",
                Meal("Lunch", "Rice bowl", 130, 250));
            await client.PostAsJsonAsync("/api/v1/programs", new { name = "PPL", splitLabel = "Push/Pull/Legs" });

            var res = await client.GetAsync("/api/v1/me/export");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);
            Assert.Contains("attachment", res.Content.Headers.ContentDisposition?.ToString() ?? "");

            var export = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(email, export.GetProperty("account").GetProperty("email").GetString());

            var meal = export.GetProperty("mealLogs").EnumerateArray().Single();
            Assert.Equal("Lunch", meal.GetProperty("category").GetString());
            Assert.Equal("Rice bowl", meal.GetProperty("items").EnumerateArray().Single().GetProperty("name").GetString());

            var program = export.GetProperty("workoutPrograms").EnumerateArray().Single();
            Assert.Equal("PPL", program.GetProperty("name").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Export_is_scoped_to_the_caller()
    {
        var (alice, aliceEmail) = await factory.RegisterAndAuthenticateAsync();
        var (bob, bobEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            await alice.PostAsJsonAsync("/api/v1/nutrition-days/2026-09-08/items",
                Meal("Dinner", "Alice's pasta", 350, 200));

            var bobExport = await bob.GetFromJsonAsync<JsonElement>("/api/v1/me/export");
            Assert.Empty(bobExport.GetProperty("mealLogs").EnumerateArray());
        }
        finally
        {
            await factory.DeleteUsersAsync(aliceEmail, bobEmail);
        }
    }

    [Fact]
    public async Task Export_requires_authentication()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/me/export")).StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_account_and_cascades_its_data()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();
        try
        {
            var register = await client.PostAsJsonAsync("/api/v1/auth/register",
                new { username, email, password = "Str0ng!Passw0rd" });
            register.EnsureSuccessStatusCode();
            var token = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);

            await client.PostAsJsonAsync("/api/v1/nutrition-days/2026-09-08/items",
                Meal("Breakfast", "Oats", 380, 60));

            Guid userId;
            using (var scope = factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
                userId = (await ctx.Users.SingleAsync(u => u.Email == email)).Id;
            }

            var deleted = await client.DeleteAsync("/api/v1/me");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

            // The token is still validly signed, but the user is gone -> treated as unauthenticated.
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);

            // Logging back in with the same credentials fails — the account no longer exists.
            var relogin = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { emailOrUsername = email, password = "Str0ng!Passw0rd" });
            Assert.Equal(HttpStatusCode.Unauthorized, relogin.StatusCode);

            // And the cascade took the meal logs with it.
            using (var scope = factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
                Assert.False(await ctx.Users.AnyAsync(u => u.Id == userId));
                Assert.False(await ctx.MealLogs.AnyAsync(m => m.UserId == userId));
                Assert.False(await ctx.RefreshTokens.AnyAsync(t => t.UserId == userId));
            }
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Delete_requires_authentication()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.DeleteAsync("/api/v1/me")).StatusCode);
    }
}
