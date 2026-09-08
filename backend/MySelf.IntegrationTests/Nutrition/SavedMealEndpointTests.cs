using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Nutrition;

/// <summary>
/// Drives Saved Meals (docs/03 §8.8, docs/04 §12): CRUD + archive, and
/// POST /saved-meals/{id}/add-to-day which copies the template into independent, optionally
/// scaled meal-log snapshots.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SavedMealEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private static object ChickenAndRice() => new
    {
        name = "Chicken & Rice",
        category = "Lunch",
        notes = (string?)null,
        items = new[]
        {
            new { name = "Chicken breast", servingBasis = "Per100g", servingSizeGrams = (double?)null, perBasisKcal = 165.0, perBasisProteinG = 31.0, perBasisCarbG = 0.0, perBasisFatG = 3.6, defaultAmount = 200.0, unit = "Grams" },
            new { name = "Basmati rice", servingBasis = "Per100g", servingSizeGrams = (double?)null, perBasisKcal = 130.0, perBasisProteinG = 2.7, perBasisCarbG = 28.0, perBasisFatG = 0.3, defaultAmount = 150.0, unit = "Grams" },
        },
    };

    [Fact]
    public async Task Creates_lists_and_computes_the_meal_at_one_times()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var created = await client.PostAsJsonAsync("/api/v1/saved-meals", ChickenAndRice());
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var meal = await created.Content.ReadFromJsonAsync<JsonElement>();
            // 165×2 + 130×1.5 = 330 + 195 = 525
            Assert.Equal(525.0m, meal.GetProperty("totals").GetProperty("kcal").GetDecimal());
            Assert.Equal(2, meal.GetProperty("items").GetArrayLength());
            Assert.Equal(330.0m, meal.GetProperty("items")[0].GetProperty("kcal").GetDecimal());

            var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/saved-meals");
            Assert.Equal("Chicken & Rice", list.EnumerateArray().Single().GetProperty("name").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Adds_to_a_day_as_independent_snapshots_scaled_by_the_multiplier()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var id = (await (await client.PostAsJsonAsync("/api/v1/saved-meals", ChickenAndRice()))
                .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var applied = await client.PostAsJsonAsync(
                $"/api/v1/saved-meals/{id}/add-to-day", new { date = "2026-09-09", category = (string?)null, multiplier = 0.5 });
            Assert.Equal(HttpStatusCode.OK, applied.StatusCode);

            var day = await applied.Content.ReadFromJsonAsync<JsonElement>();
            var lunch = day.GetProperty("meals").EnumerateArray().Single(m => m.GetProperty("category").GetString() == "Lunch");
            var items = lunch.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(2, items.Count);
            Assert.Equal("Chicken breast", items[0].GetProperty("name").GetString());
            Assert.Equal(100.0m, items[0].GetProperty("amount").GetDecimal());  // 200 × 0.5
            Assert.Equal(165.0m, items[0].GetProperty("kcal").GetDecimal());    // 165/100g × 100g
            // Whole day: 262.5 (half of the 525 template)
            Assert.Equal(262.5m, day.GetProperty("totals").GetProperty("kcal").GetDecimal());

            // Archiving the template leaves the logged day untouched.
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/saved-meals/{id}")).StatusCode);
            var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/nutrition-days/2026-09-09");
            Assert.Equal(262.5m, after.GetProperty("totals").GetProperty("kcal").GetDecimal());
            Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/v1/saved-meals")).EnumerateArray());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Update_replaces_the_items_wholesale()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var id = (await (await client.PostAsJsonAsync("/api/v1/saved-meals", ChickenAndRice()))
                .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var put = await client.PutAsJsonAsync($"/api/v1/saved-meals/{id}", new
            {
                name = "Just rice",
                category = "Dinner",
                notes = "trimmed down",
                items = new[]
                {
                    new { name = "Basmati rice", servingBasis = "Per100g", servingSizeGrams = (double?)null, perBasisKcal = 130.0, perBasisProteinG = 2.7, perBasisCarbG = 28.0, perBasisFatG = 0.3, defaultAmount = 200.0, unit = "Grams" },
                },
            });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var meal = await put.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Just rice", meal.GetProperty("name").GetString());
            Assert.Equal("Dinner", meal.GetProperty("category").GetString());
            Assert.Equal(1, meal.GetProperty("items").GetArrayLength());
            Assert.Equal(260.0m, meal.GetProperty("totals").GetProperty("kcal").GetDecimal()); // 130 × 2
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Rejects_an_empty_meal_and_is_private_to_its_owner()
    {
        var (alice, aliceEmail) = await factory.RegisterAndAuthenticateAsync();
        var (bob, bobEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var empty = await alice.PostAsJsonAsync("/api/v1/saved-meals", new { name = "Nothing", category = "Lunch", notes = (string?)null, items = Array.Empty<object>() });
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

            var id = (await (await alice.PostAsJsonAsync("/api/v1/saved-meals", ChickenAndRice()))
                .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            Assert.Empty((await bob.GetFromJsonAsync<JsonElement>("/api/v1/saved-meals")).EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/saved-meals/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/saved-meals/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await bob.PostAsJsonAsync($"/api/v1/saved-meals/{id}/add-to-day", new { date = "2026-09-09", category = (string?)null, multiplier = 1.0 })).StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(aliceEmail, bobEmail);
        }
    }
}
