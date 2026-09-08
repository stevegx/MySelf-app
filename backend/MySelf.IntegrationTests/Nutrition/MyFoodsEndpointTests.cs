using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Nutrition;

/// <summary>
/// Drives "My Foods" (docs/03 §8.7, docs/04 §12): GET /foods/search, POST /foods/custom,
/// DELETE /foods/custom/{id} (soft archive). Per-owner and against the shared dev database.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MyFoodsEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private static object Food(string name, string? brand = null) => new
    {
        name,
        brand,
        barcode = (string?)null,
        servingBasis = "Per100g",
        servingSizeGrams = (double?)null,
        kcal = 165.0,
        proteinG = 31.0,
        carbG = 0.0,
        fatG = 3.6,
    };

    [Fact]
    public async Task Creates_a_food_and_finds_it_by_name_or_brand()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var created = await client.PostAsJsonAsync("/api/v1/foods/custom", Food("Chicken breast", "Local Farm"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(165m, body.GetProperty("kcal").GetDecimal());
            Assert.Equal("Per100g", body.GetProperty("servingBasis").GetString());

            await client.PostAsJsonAsync("/api/v1/foods/custom", Food("Basmati rice"));

            var byName = await client.GetFromJsonAsync<JsonElement>("/api/v1/foods/search?q=chick");
            Assert.Equal("Chicken breast", byName.EnumerateArray().Single().GetProperty("name").GetString());

            var byBrand = await client.GetFromJsonAsync<JsonElement>("/api/v1/foods/search?q=farm");
            Assert.Equal("Chicken breast", byBrand.EnumerateArray().Single().GetProperty("name").GetString());

            var all = await client.GetFromJsonAsync<JsonElement>("/api/v1/foods/search");
            Assert.Equal(2, all.GetArrayLength());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Archiving_a_food_removes_it_from_search()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var id = (await (await client.PostAsJsonAsync("/api/v1/foods/custom", Food("Oats")))
                .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/foods/custom/{id}")).StatusCode);
            // A second archive is a no-op 404.
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/foods/custom/{id}")).StatusCode);

            var all = await client.GetFromJsonAsync<JsonElement>("/api/v1/foods/search");
            Assert.Empty(all.EnumerateArray());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Rejects_a_missing_name()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var res = await client.PostAsJsonAsync("/api/v1/foods/custom", Food("   "));
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task My_Foods_are_private_to_their_owner()
    {
        var (alice, aliceEmail) = await factory.RegisterAndAuthenticateAsync();
        var (bob, bobEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var id = (await (await alice.PostAsJsonAsync("/api/v1/foods/custom", Food("Alice's protein bar")))
                .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var bobSearch = await bob.GetFromJsonAsync<JsonElement>("/api/v1/foods/search?q=protein");
            Assert.Empty(bobSearch.EnumerateArray());

            Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/foods/custom/{id}")).StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(aliceEmail, bobEmail);
        }
    }
}
