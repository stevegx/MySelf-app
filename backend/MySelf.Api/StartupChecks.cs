namespace MySelf.Api;

/// <summary>
/// Fail-fast configuration validation. The connection string and <c>Jwt:Key</c> are already
/// checked unconditionally in <c>Program.cs</c> (the app can't run at all without them);
/// this adds the checks that only matter for a real deployment — a dev-only value left in
/// place in Production is the classic footgun. Pure function so it can be unit-tested.
/// </summary>
public static class StartupChecks
{
    /// <summary>
    /// Problems that should stop a Production start. Empty list = good to go. Not run outside
    /// Production (dev/test intentionally use localhost origins, short keys, etc.).
    /// </summary>
    public static IReadOnlyList<string> ProductionConfigProblems(IConfiguration config)
    {
        var problems = new List<string>();

        var jwtKey = config["Jwt:Key"] ?? "";
        if (jwtKey.Length < 32)
        {
            problems.Add("Jwt:Key must be at least 32 characters in Production (generate one with `openssl rand -base64 48`).");
        }

        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0)
        {
            problems.Add("Cors:AllowedOrigins must list the deployed frontend origin in Production.");
        }
        if (origins.Any(IsLoopback))
        {
            problems.Add("Cors:AllowedOrigins still contains a localhost/127.0.0.1 origin — set it to the real frontend URL in Production.");
        }

        if (string.Equals(config["AllowedHosts"], "*", StringComparison.Ordinal))
        {
            problems.Add("AllowedHosts is \"*\" — set it to the API's real host name(s) in Production.");
        }

        if (config.GetValue($"{RateLimitOptions.SectionName}:Enabled", true) == false)
        {
            problems.Add("RateLimiting:Enabled is false — rate limiting must be on in Production.");
        }

        var frontendBase = config["Frontend:BaseUrl"] ?? "";
        if (IsLoopback(frontendBase))
        {
            problems.Add("Frontend:BaseUrl points at localhost — set it to the deployed frontend URL (used in password-reset links).");
        }

        return problems;
    }

    private static bool IsLoopback(string url) =>
        url.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || url.Contains("127.0.0.1", StringComparison.Ordinal);
}
