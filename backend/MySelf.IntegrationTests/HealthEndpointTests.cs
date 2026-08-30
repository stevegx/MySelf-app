using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MySelf.IntegrationTests;

/// <summary>
/// Boots the real API in memory (WebApplicationFactory) and calls GET /health.
/// Protects the wiring: DbContext DI registration, the ConnectionStrings:DefaultConnection
/// key, UseNpgsql, AddDbContextCheck and MapHealthChecks all have to line up.
/// Requires the local PostgreSQL service to be running.
/// </summary>
public class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_returns_200_and_Healthy_when_database_is_reachable()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
