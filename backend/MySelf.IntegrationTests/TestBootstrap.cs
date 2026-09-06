using System.Runtime.CompilerServices;
using DotNetEnv;
using Npgsql;

namespace MySelf.IntegrationTests;

internal static class TestBootstrap
{
    /// <summary>
    /// Runs once when the test assembly loads, before any <see cref="Xunit.FactAttribute"/>
    /// and before <c>Program.cs</c> reads configuration.
    /// </summary>
    [ModuleInitializer]
    internal static void Configure()
    {
        // The whole suite drives one in-process server from the same loopback address, so the
        // real per-IP "auth" limit would throttle a fast run. Turn rate limiting off by
        // default; RateLimitingEndpointTests switches it back on for its own factory.
        Environment.SetEnvironmentVariable("RateLimiting__Enabled", "false");

        // The tests wipe every application table (Respawn). Point them at a DEDICATED
        // database so a `dotnet test` run never touches the developer's dev data. We derive
        // it from the dev connection string (same host/creds) with "_test" appended to the
        // database name. Program.cs loads .env with NoClobber, so this value wins there too.
        Env.NoClobber().TraversePath().Load();

        var dev = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=myself;Username=myself;Password=myself";

        var builder = new NpgsqlConnectionStringBuilder(dev);
        if (!builder.Database!.EndsWith("_test", StringComparison.Ordinal))
        {
            builder.Database += "_test";
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", builder.ConnectionString);
    }
}
