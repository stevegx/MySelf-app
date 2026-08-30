using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MySelf.Infrastructure.Nutrition;
using MySelf.Infrastructure.Persistence;

namespace MySelf.IntegrationTests.Nutrition;

/// <summary>
/// Drives GET /api/v1/foods/barcode/{code} through the real pipeline, with the Open Food Facts
/// HTTP client swapped for a stub. The one row it writes to the shared dev database is deleted
/// afterwards.
/// </summary>
public class BarcodeEndpointTests
{
    private static WebApplicationFactory<Program> CreateFactory(StubHttpMessageHandler offHandler) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<OpenFoodFactsClient>();
                services.AddHttpClient<OpenFoodFactsClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => offHandler);
            }));

    [Fact]
    public async Task Known_barcode_returns_200_with_nutrition_shape()
    {
        const string barcode = "1111111111116";
        var handler = StubHttpMessageHandler.Json(OpenFoodFactsSampleData.ProductJson(barcode));
        using var factory = CreateFactory(handler);
        var client = factory.CreateClient();

        try
        {
            var response = await client.GetAsync($"/api/v1/foods/barcode/{barcode}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Nutella", body.GetProperty("name").GetString());
            Assert.Equal("Open Food Facts", body.GetProperty("source").GetString());
            Assert.Equal(539m, body.GetProperty("per100g").GetProperty("energyKcal").GetDecimal());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("per100g").GetProperty("fiber").ValueKind);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
            await db.FoodCacheEntries.Where(e => e.Barcode == barcode).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Invalid_barcode_returns_400_problem_json()
    {
        using var factory = CreateFactory(StubHttpMessageHandler.Json("{}"));

        var response = await factory.CreateClient().GetAsync("/api/v1/foods/barcode/abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
