using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace MySelf.Api;

/// <summary>
/// Rate-limit knobs, bound from the <c>RateLimiting</c> configuration section. Defaults are
/// generous — the point is to blunt scripted abuse (credential stuffing on <c>/auth</c>,
/// a loop hammering <c>POST /programs</c>), not to get in a real user's way. Integration
/// tests set <c>RateLimiting__Enabled=false</c> so the shared in-process server doesn't
/// throttle a fast test run.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>Length of the fixed window every limit below is measured over.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Ceiling for any single caller (per user id, or per IP when anonymous).</summary>
    public int GlobalPermitLimit { get; set; } = 300;

    /// <summary>Per-IP ceiling for the <c>/api/v1/auth</c> endpoints (login, register, refresh, password reset).</summary>
    public int AuthPermitLimit { get; set; } = 20;

    /// <summary>Per-user ceiling for builder mutations (program / group / variant writes).</summary>
    public int WritePermitLimit { get; set; } = 90;
}

/// <summary>
/// Wires ASP.NET Core's built-in rate limiter (no external package — it ships in the
/// framework). One generous global limiter, plus two stricter named policies applied to the
/// endpoints that are actually worth attacking.
/// </summary>
public static class RateLimiting
{
    public const string AuthPolicy = "auth";
    public const string WritePolicy = "write";

    public static WebApplicationBuilder AddAppRateLimiting(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        if (!options.Enabled)
        {
            return builder;
        }

        var window = TimeSpan.FromSeconds(options.WindowSeconds);

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Applies to every request except the health probe. Partitioned so one noisy
            // caller can't spend everyone else's budget.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/health"))
                {
                    return RateLimitPartition.GetNoLimiter("health");
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    CallerKey(context),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = options.GlobalPermitLimit, Window = window });
            });

            limiter.AddPolicy(AuthPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                $"auth:{ClientIp(context)}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = options.AuthPermitLimit, Window = window }));

            limiter.AddPolicy(WritePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                $"write:{CallerKey(context)}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = options.WritePermitLimit, Window = window }));

            limiter.OnRejected = async (rejected, cancellationToken) =>
            {
                var response = rejected.HttpContext.Response;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                response.StatusCode = StatusCodes.Status429TooManyRequests;
                await response.WriteAsJsonAsync(
                    new
                    {
                        title = "Too many requests",
                        status = 429,
                        detail = "You're sending requests too quickly. Try again shortly.",
                    },
                    cancellationToken);
            };
        });

        return builder;
    }

    /// <summary>Authenticated user id when present, otherwise the client IP.</summary>
    private static string CallerKey(HttpContext context) =>
        context.TryGetUserId(out var userId) ? userId.ToString() : $"ip:{ClientIp(context)}";

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
