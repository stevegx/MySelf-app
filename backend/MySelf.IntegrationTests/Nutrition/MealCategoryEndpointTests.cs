using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Nutrition;

/// <summary>
/// Drives the user-defined meal slots (docs/08 #19): GET/POST/PUT/DELETE /api/v1/meal-categories,
/// lazy seeding of the four defaults, and how a renamed/archived slot behaves on the day.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MealCategoryEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private const string Day = "2026-09-08";

    private static object Item(string category, string name, double kcal, double amount) => new
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

    private static string[] Names(JsonElement rows) =>
        rows.EnumerateArray().Select(r => r.GetProperty("name").GetString()!).ToArray();

    private static string[] Slots(JsonElement day) =>
        day.GetProperty("meals").EnumerateArray().Select(m => m.GetProperty("category").GetString()!).ToArray();

    [Fact]
    public async Task First_call_seeds_the_four_default_slots()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var rows = await client.GetFromJsonAsync<JsonElement>("/api/v1/meal-categories");
            Assert.Equal(new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }, Names(rows));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Added_category_becomes_a_slot_on_the_day()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var created = await client.PostAsJsonAsync("/api/v1/meal-categories", new { name = "Pre-workout" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var day = await client.GetFromJsonAsync<JsonElement>($"/api/v1/nutrition-days/{Day}");
            Assert.Equal(
                new[] { "Breakfast", "Lunch", "Dinner", "Snacks", "Pre-workout" },
                Slots(day));

            // And it accepts a log.
            var logged = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items", Item("Pre-workout", "Banana", 89, 120));
            Assert.Equal(HttpStatusCode.OK, logged.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Duplicate_name_is_rejected()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var dup = await client.PostAsJsonAsync("/api/v1/meal-categories", new { name = "lunch" });
            Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Rename_updates_the_slot_without_touching_already_logged_history()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            // Log into "Snacks", then rename that slot to "Evening".
            await client.PostAsJsonAsync($"/api/v1/nutrition-days/{Day}/items", Item("Snacks", "Yoghurt", 60, 150));

            var rows = await client.GetFromJsonAsync<JsonElement>("/api/v1/meal-categories");
            var snacksId = rows.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Snacks")
                .GetProperty("id").GetGuid();

            var renamed = await client.PutAsJsonAsync($"/api/v1/meal-categories/{snacksId}", new { name = "Evening" });
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

            var day = await client.GetFromJsonAsync<JsonElement>($"/api/v1/nutrition-days/{Day}");
            var meals = day.GetProperty("meals").EnumerateArray().ToList();

            // "Evening" is now an active slot; the old "Snacks" data shows as an orphan slot so
            // nothing is lost, and both are present.
            Assert.Contains("Evening", meals.Select(m => m.GetProperty("category").GetString()));
            var snacks = meals.Single(m => m.GetProperty("category").GetString() == "Snacks");
            Assert.Equal("Yoghurt", snacks.GetProperty("items").EnumerateArray().Single().GetProperty("name").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Archive_drops_the_empty_slot_but_the_last_one_cannot_go()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var rows = await client.GetFromJsonAsync<JsonElement>("/api/v1/meal-categories");
            var ids = rows.EnumerateArray().ToDictionary(
                r => r.GetProperty("name").GetString()!, r => r.GetProperty("id").GetGuid());

            Assert.Equal(HttpStatusCode.NoContent,
                (await client.DeleteAsync($"/api/v1/meal-categories/{ids["Dinner"]}")).StatusCode);

            var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/meal-categories");
            Assert.Equal(new[] { "Breakfast", "Lunch", "Snacks" }, Names(after));

            // Remove down to the last, then the last delete is refused.
            await client.DeleteAsync($"/api/v1/meal-categories/{ids["Breakfast"]}");
            await client.DeleteAsync($"/api/v1/meal-categories/{ids["Lunch"]}");
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.DeleteAsync($"/api/v1/meal-categories/{ids["Snacks"]}")).StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Reorder_sets_the_slot_order_on_the_day()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var rows = await client.GetFromJsonAsync<JsonElement>("/api/v1/meal-categories");
            var ids = rows.EnumerateArray().ToDictionary(
                r => r.GetProperty("name").GetString()!, r => r.GetProperty("id").GetGuid());

            var reordered = await client.PutAsJsonAsync("/api/v1/meal-categories/reorder", new
            {
                ids = new[] { ids["Dinner"], ids["Breakfast"], ids["Snacks"], ids["Lunch"] },
            });
            Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);

            var day = await client.GetFromJsonAsync<JsonElement>($"/api/v1/nutrition-days/{Day}");
            Assert.Equal(
                new[] { "Dinner", "Breakfast", "Snacks", "Lunch" },
                Slots(day));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }
}
