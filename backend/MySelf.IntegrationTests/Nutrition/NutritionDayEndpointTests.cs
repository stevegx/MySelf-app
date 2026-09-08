using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Nutrition;

/// <summary>
/// Drives manual food logging (docs/03 §8.7, docs/04 §12): GET /nutrition-days/{date},
/// POST /nutrition-days/{date}/items, PUT/DELETE /meal-log-items/{id}. Runs against the
/// shared dev database; each test registers a unique user and the cascade FK removes the
/// meal logs with the account.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class NutritionDayEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private const string Day = "2026-09-07";

    private static object Item(string category, string name, double kcal, double p, double c, double f, double amount) =>
        new
        {
            category,
            name,
            servingBasis = "Per100g",
            servingSizeGrams = (double?)null,
            perBasisKcal = kcal,
            perBasisProteinG = p,
            perBasisCarbG = c,
            perBasisFatG = f,
            amount,
            unit = "Grams",
        };

    [Fact]
    public async Task Empty_day_lists_four_meal_slots_with_zero_totals()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var day = await client.GetFromJsonAsync<JsonElement>($"/api/v1/nutrition-days/{Day}");

            var meals = day.GetProperty("meals").EnumerateArray().ToList();
            Assert.Equal(
                new[] { "Breakfast", "Lunch", "Dinner", "Snacks" },
                meals.Select(m => m.GetProperty("category").GetString()).ToArray());
            Assert.All(meals, m => Assert.Equal(JsonValueKind.Null, m.GetProperty("mealLogId").ValueKind));
            Assert.Equal(0m, day.GetProperty("totals").GetProperty("kcal").GetDecimal());
            // No goal yet -> null targets.
            Assert.Equal(JsonValueKind.Null, day.GetProperty("targets").GetProperty("kcal").ValueKind);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Adds_items_scales_them_and_rolls_up_meal_and_day_totals()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            // 150 g of a 52 kcal / 3.4 P / 5 C / 1.7 F per-100g food -> ×1.5.
            var afterFirst = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items", Item("Lunch", "Plain yogurt", 52, 3.4, 5, 1.7, 150));
            Assert.Equal(HttpStatusCode.OK, afterFirst.StatusCode);

            var day = await afterFirst.Content.ReadFromJsonAsync<JsonElement>();
            var lunch = day.GetProperty("meals").EnumerateArray().Single(m => m.GetProperty("category").GetString() == "Lunch");
            var item = lunch.GetProperty("items").EnumerateArray().Single();
            Assert.Equal("Plain yogurt", item.GetProperty("name").GetString());
            Assert.Equal(78.0m, item.GetProperty("kcal").GetDecimal());
            Assert.Equal(5.1m, item.GetProperty("proteinG").GetDecimal());
            Assert.Equal(78.0m, lunch.GetProperty("subtotals").GetProperty("kcal").GetDecimal());
            Assert.NotEqual(JsonValueKind.Null, lunch.GetProperty("mealLogId").ValueKind);

            // Second item in the same meal.
            var second = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items", Item("Lunch", "Chicken breast", 165, 31, 0, 3.6, 200));
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            // And one in Breakfast.
            var afterThird = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items", Item("Breakfast", "Oats", 379, 13, 68, 7, 60));
            Assert.Equal(HttpStatusCode.OK, afterThird.StatusCode);

            day = await afterThird.Content.ReadFromJsonAsync<JsonElement>();
            var meals = day.GetProperty("meals").EnumerateArray().ToList();
            Assert.Equal(2, meals.Single(m => m.GetProperty("category").GetString() == "Lunch").GetProperty("items").GetArrayLength());
            Assert.Equal(1, meals.Single(m => m.GetProperty("category").GetString() == "Breakfast").GetProperty("items").GetArrayLength());

            // Day total = 78 (yogurt) + 330 (chicken ×2) + 227.4 (oats ×0.6) = 635.4
            Assert.Equal(635.4m, day.GetProperty("totals").GetProperty("kcal").GetDecimal());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Editing_the_amount_recomputes_the_item_and_the_totals()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var added = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items", Item("Dinner", "Rice", 130, 2.7, 28, 0.3, 100));
            var day = await added.Content.ReadFromJsonAsync<JsonElement>();
            var itemId = day.GetProperty("meals").EnumerateArray()
                .Single(m => m.GetProperty("category").GetString() == "Dinner")
                .GetProperty("items").EnumerateArray().Single()
                .GetProperty("id").GetGuid();

            var edited = await client.PutAsJsonAsync(
                $"/api/v1/meal-log-items/{itemId}", new { amount = 250.0, unit = "Grams", servingSizeGrams = (double?)null });
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

            day = await edited.Content.ReadFromJsonAsync<JsonElement>();
            var item = day.GetProperty("meals").EnumerateArray()
                .Single(m => m.GetProperty("category").GetString() == "Dinner")
                .GetProperty("items").EnumerateArray().Single();
            Assert.Equal(325.0m, item.GetProperty("kcal").GetDecimal()); // 130 × 2.5
            Assert.Equal(325.0m, day.GetProperty("totals").GetProperty("kcal").GetDecimal());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Removing_the_last_item_of_a_meal_drops_the_meal_slot()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var added = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items", Item("Snacks", "Apple", 52, 0.3, 14, 0.2, 180));
            var day = await added.Content.ReadFromJsonAsync<JsonElement>();
            var itemId = day.GetProperty("meals").EnumerateArray()
                .Single(m => m.GetProperty("category").GetString() == "Snacks")
                .GetProperty("items").EnumerateArray().Single()
                .GetProperty("id").GetGuid();

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/meal-log-items/{itemId}")).StatusCode);

            day = await client.GetFromJsonAsync<JsonElement>($"/api/v1/nutrition-days/{Day}");
            var snacks = day.GetProperty("meals").EnumerateArray().Single(m => m.GetProperty("category").GetString() == "Snacks");
            Assert.Equal(JsonValueKind.Null, snacks.GetProperty("mealLogId").ValueKind);
            Assert.Empty(snacks.GetProperty("items").EnumerateArray());
            Assert.Equal(0m, day.GetProperty("totals").GetProperty("kcal").GetDecimal());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Rejects_a_bad_date_and_an_unknown_category()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/nutrition-days/07-09-2026")).StatusCode);

            var badCategory = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items", Item("Elevenses", "Scone", 400, 6, 50, 20, 60));
            Assert.Equal(HttpStatusCode.BadRequest, badCategory.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task A_meal_item_is_only_editable_and_deletable_by_its_owner()
    {
        var (alice, aliceEmail) = await factory.RegisterAndAuthenticateAsync();
        var (bob, bobEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var added = await alice.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items", Item("Lunch", "Soup", 40, 2, 5, 1, 300));
            var itemId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("meals").EnumerateArray()
                .Single(m => m.GetProperty("category").GetString() == "Lunch")
                .GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();

            Assert.Equal(HttpStatusCode.NotFound,
                (await bob.PutAsJsonAsync($"/api/v1/meal-log-items/{itemId}", new { amount = 1.0, unit = "Grams", servingSizeGrams = (double?)null })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/meal-log-items/{itemId}")).StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(aliceEmail, bobEmail);
        }
    }
}
