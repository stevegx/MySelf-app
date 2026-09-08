using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Nutrition;

/// <summary>
/// Drives multi-select copy / move / delete / restore on a day's items (docs/08 Story 7):
/// POST /api/v1/nutrition-days/{date}/items/bulk-{delete,move,copy,add}.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MealItemBulkEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
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

    private static async Task<Guid> LogAsync(HttpClient client, string date, string category, string name, double kcal, double amount)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/nutrition-days/{date}/items", Item(category, name, kcal, amount));
        res.EnsureSuccessStatusCode();
        var day = await res.Content.ReadFromJsonAsync<JsonElement>();
        return day.GetProperty("meals").EnumerateArray()
            .Single(m => m.GetProperty("category").GetString() == category)
            .GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();
    }

    private static IEnumerable<JsonElement> Items(JsonElement day, string category) =>
        day.GetProperty("meals").EnumerateArray()
            .Single(m => m.GetProperty("category").GetString() == category)
            .GetProperty("items").EnumerateArray();

    [Fact]
    public async Task Bulk_delete_removes_the_items_and_empties_the_slot()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var a = await LogAsync(client, Day, "Lunch", "Rice", 130, 200);
            var b = await LogAsync(client, Day, "Lunch", "Chicken", 165, 150);

            var res = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items/bulk-delete", new { ids = new[] { a, b } });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var day = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Empty(Items(day, "Lunch"));
            Assert.Equal(JsonValueKind.Null,
                day.GetProperty("meals").EnumerateArray()
                    .Single(m => m.GetProperty("category").GetString() == "Lunch")
                    .GetProperty("mealLogId").ValueKind);
            Assert.Equal(0m, day.GetProperty("totals").GetProperty("kcal").GetDecimal());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Bulk_move_relocates_items_and_clears_the_source_slot()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var a = await LogAsync(client, Day, "Lunch", "Rice", 130, 200); // 260 kcal
            var b = await LogAsync(client, Day, "Lunch", "Chicken", 165, 200); // 330 kcal

            var res = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items/bulk-move", new { ids = new[] { a, b }, toCategory = "Dinner" });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var day = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Empty(Items(day, "Lunch"));
            var dinner = Items(day, "Dinner").Select(i => i.GetProperty("name").GetString()).ToArray();
            Assert.Equal(new[] { "Rice", "Chicken" }, dinner);
            // Identity preserved on move.
            Assert.Contains(a, Items(day, "Dinner").Select(i => i.GetProperty("id").GetGuid()));
            Assert.Equal(590m, day.GetProperty("totals").GetProperty("kcal").GetDecimal());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Bulk_copy_duplicates_into_another_slot_with_new_ids()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var a = await LogAsync(client, Day, "Breakfast", "Oats", 380, 100);

            var res = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items/bulk-copy",
                new { ids = new[] { a }, toCategory = "Snacks", toDate = (string?)null });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var day = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Single(Items(day, "Breakfast")); // original still there
            var copy = Items(day, "Snacks").Single();
            Assert.Equal("Oats", copy.GetProperty("name").GetString());
            Assert.NotEqual(a, copy.GetProperty("id").GetGuid()); // independent id
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Bulk_copy_can_target_another_day()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var a = await LogAsync(client, Day, "Lunch", "Soup", 40, 300);

            var res = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items/bulk-copy",
                new { ids = new[] { a }, toCategory = "Lunch", toDate = "2026-09-09" });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var target = await client.GetFromJsonAsync<JsonElement>("/api/v1/nutrition-days/2026-09-09");
            Assert.Equal("Soup", Items(target, "Lunch").Single().GetProperty("name").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Bulk_add_restores_deleted_snapshots_verbatim()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var a = await LogAsync(client, Day, "Dinner", "Pasta", 350, 180); // 630 kcal
            var deleted = await client.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items/bulk-delete", new { ids = new[] { a } });
            deleted.EnsureSuccessStatusCode();

            var res = await client.PostAsJsonAsync($"/api/v1/nutrition-days/{Day}/items/bulk-add", new
            {
                items = new[]
                {
                    new
                    {
                        category = "Dinner", name = "Pasta", servingBasis = "Per100g", servingSizeGrams = (double?)null,
                        amount = 180.0, unit = "Grams",
                        kcal = 630.0, proteinG = 9.0, carbG = 18.0, fatG = 3.6,
                        basisKcal = 350.0, basisProteinG = 5.0, basisCarbG = 10.0, basisFatG = 2.0,
                    },
                },
            });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var day = await res.Content.ReadFromJsonAsync<JsonElement>();
            var restored = Items(day, "Dinner").Single();
            Assert.Equal("Pasta", restored.GetProperty("name").GetString());
            Assert.Equal(630m, restored.GetProperty("kcal").GetDecimal());
            Assert.Equal(630m, day.GetProperty("totals").GetProperty("kcal").GetDecimal());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Bulk_ops_reject_ids_that_are_not_the_callers()
    {
        var (alice, aliceEmail) = await factory.RegisterAndAuthenticateAsync();
        var (bob, bobEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var a = await LogAsync(alice, Day, "Lunch", "Rice", 130, 200);

            var res = await bob.PostAsJsonAsync(
                $"/api/v1/nutrition-days/{Day}/items/bulk-delete", new { ids = new[] { a } });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(aliceEmail, bobEmail);
        }
    }
}
