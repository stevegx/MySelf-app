using System.Runtime.CompilerServices;

namespace MySelf.IntegrationTests;

internal static class TestBootstrap
{
    /// <summary>
    /// Runs once when the test assembly loads, before any <see cref="Xunit.FactAttribute"/>.
    /// The whole suite drives one in-process server from the same loopback address, so the
    /// real per-IP "auth" limit would throttle a fast run. Turn rate limiting off by default;
    /// <c>RateLimitingEndpointTests</c> switches it back on for its own factory.
    /// </summary>
    [ModuleInitializer]
    internal static void DisableRateLimitingForTests()
    {
        Environment.SetEnvironmentVariable("RateLimiting__Enabled", "false");
    }
}
