using Microsoft.Extensions.Configuration;
using MySelf.Api;

namespace MySelf.IntegrationTests.Api;

/// <summary>Unit coverage for the Production fail-fast config validator (<see cref="StartupChecks"/>).</summary>
public class StartupChecksTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> DeploymentReady() => new()
    {
        ["Jwt:Key"] = new string('k', 40),
        ["Cors:AllowedOrigins:0"] = "https://app.example.com",
        ["AllowedHosts"] = "api.example.com",
        ["RateLimiting:Enabled"] = "true",
        ["Frontend:BaseUrl"] = "https://app.example.com",
    };

    [Fact]
    public void A_deployment_ready_configuration_has_no_problems()
    {
        Assert.Empty(StartupChecks.ProductionConfigProblems(Config(DeploymentReady())));
    }

    [Fact]
    public void It_flags_every_dev_only_value_left_in_place()
    {
        var problems = StartupChecks.ProductionConfigProblems(Config(new()
        {
            ["Jwt:Key"] = "too-short",
            ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
            ["AllowedHosts"] = "*",
            ["RateLimiting:Enabled"] = "false",
            ["Frontend:BaseUrl"] = "http://localhost:5173",
        }));

        Assert.Contains(problems, p => p.Contains("Jwt:Key"));
        Assert.Contains(problems, p => p.Contains("localhost", StringComparison.OrdinalIgnoreCase) && p.Contains("Cors"));
        Assert.Contains(problems, p => p.Contains("AllowedHosts"));
        Assert.Contains(problems, p => p.Contains("RateLimiting"));
        Assert.Contains(problems, p => p.Contains("Frontend:BaseUrl"));
    }

    [Fact]
    public void A_short_key_is_the_only_complaint_when_everything_else_is_fine()
    {
        var values = DeploymentReady();
        values["Jwt:Key"] = "still-not-long-enough";

        var problems = StartupChecks.ProductionConfigProblems(Config(values));

        Assert.Single(problems);
        Assert.Contains("Jwt:Key", problems[0]);
    }
}
