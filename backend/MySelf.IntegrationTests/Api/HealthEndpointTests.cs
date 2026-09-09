using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MySelf.IntegrationTests.Api;

/// <summary>The split liveness / readiness probes (docs/09).</summary>
[Collection(DatabaseCollection.Name)]
public class HealthEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task Probe_is_anonymous_and_healthy(string path)
    {
        var res = await factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Healthy", await res.Content.ReadAsStringAsync());
    }
}
