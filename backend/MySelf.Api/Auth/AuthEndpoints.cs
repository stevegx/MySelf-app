using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Identity;
using MySelf.Infrastructure.Identity;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Auth;

public static partial class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").RequireRateLimiting(RateLimiting.AuthPolicy);

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Create an account and start a session.");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Start a session for an existing account, by username or email.");

        group.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .WithSummary("Exchange the refresh-token cookie for a new access token.");

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Revoke the current refresh token and clear its cookie.");

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        MySelfDbContext db,
        TokenService tokens,
        HttpResponse response,
        IHostEnvironment env,
        CancellationToken ct)
    {
        var fieldErrors = new Dictionary<string, List<string>>();

        // Length is checked here because Identity's UserName validation only covers
        // allowed characters + uniqueness (configured in Program.cs), not a minimum length.
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 3)
        {
            fieldErrors.Add("username", ["Must be at least 3 characters."]);
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !EmailPattern().IsMatch(request.Email))
        {
            fieldErrors.Add("email", ["Enter a valid email address."]);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            fieldErrors.Add("password", ["Password is required."]);
        }

        if (fieldErrors.Count > 0)
        {
            return ValidationProblem(fieldErrors);
        }

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
        };

        var createResult = await userManager.CreateAsync(user, request.Password!);

        if (!createResult.Succeeded)
        {
            // Every failure — duplicate username, duplicate email, bad password, disallowed
            // username characters — becomes a field-level error rather than one generic
            // conflict response, so the frontend can point at the specific input to fix.
            return ValidationProblem(GroupByField(createResult.Errors));
        }

        var session = await IssueSessionAsync(user, request.TrustThisDevice, db, tokens, response, env, ct);
        return Results.Json(session, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        MySelfDbContext db,
        TokenService tokens,
        HttpResponse response,
        IHostEnvironment env,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            return InvalidCredentials();
        }

        var identifier = request.Identifier.Trim();

        // Usernames can never contain '@' (Program.cs's AllowedUserNameCharacters excludes
        // it specifically so this check is unambiguous) — so this reliably tells the two apart.
        var user = identifier.Contains('@')
            ? await userManager.FindByEmailAsync(identifier)
            : await userManager.FindByNameAsync(identifier);

        // Same generic response whether the account doesn't exist or the password is
        // wrong — telling them apart would let an attacker enumerate accounts.
        if (user is null)
        {
            return InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Account temporarily locked",
                detail: "Too many failed attempts. Try again in a few minutes.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Counts this failure toward the lockout threshold (docs/05: "lockout/rate
            // limiting on auth endpoints") — AddIdentityCore's Lockout options in Program.cs
            // control how many attempts are allowed and for how long it then locks.
            await userManager.AccessFailedAsync(user);
            return InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var session = await IssueSessionAsync(user, request.TrustThisDevice, db, tokens, response, env, ct);
        return Results.Ok(session);
    }

    private static async Task<IResult> RefreshAsync(
        HttpRequest request,
        HttpResponse response,
        UserManager<ApplicationUser> userManager,
        MySelfDbContext db,
        TokenService tokens,
        TimeProvider clock,
        IHostEnvironment env,
        CancellationToken ct)
    {
        if (!request.Cookies.TryGetValue("refreshToken", out var rawToken) || string.IsNullOrEmpty(rawToken))
        {
            return InvalidCredentials();
        }

        var hash = TokenService.Hash(rawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        var now = clock.GetUtcNow();
        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt <= now)
        {
            return InvalidCredentials();
        }

        var user = await userManager.FindByIdAsync(existing.UserId.ToString());
        if (user is null)
        {
            return InvalidCredentials();
        }

        // Rotation (docs/05: "rotating refresh token"): this token is single-use. Revoking
        // it here means a captured/replayed cookie value stops working the moment the
        // legitimate client refreshes with it first.
        existing.RevokedAt = now;
        var session = await IssueSessionAsync(user, existing.IsPersistent, db, tokens, response, env, ct);
        return Results.Ok(session);
    }

    private static async Task<IResult> LogoutAsync(
        HttpRequest request,
        HttpResponse response,
        MySelfDbContext db,
        IHostEnvironment env,
        CancellationToken ct)
    {
        if (request.Cookies.TryGetValue("refreshToken", out var rawToken) && !string.IsNullOrEmpty(rawToken))
        {
            var hash = TokenService.Hash(rawToken);
            var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

            // Not found / already revoked isn't an error here — logout is idempotent:
            // calling it twice, or with a stale cookie, still ends in "logged out".
            if (existing is not null && existing.RevokedAt is null)
            {
                existing.RevokedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        // Clear the cookie regardless of whether a matching token was found — the browser
        // should end up with no refresh cookie either way. Attributes must match what
        // SetRefreshCookie used (Path in particular) or the browser won't recognize it as
        // the same cookie to remove.
        response.Cookies.Delete("refreshToken", CookieAttributes(env));

        return Results.NoContent();
    }

    private static IResult InvalidCredentials() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Invalid credentials",
            detail: "Invalid email or password.");

    /// <summary>
    /// Shared by register, login, and refresh: issues the JWT access token, persists a new
    /// refresh token (hashed), sets it as a cookie, and builds the response body.
    /// </summary>
    private static async Task<AuthResponse> IssueSessionAsync(
        ApplicationUser user,
        bool trustThisDevice,
        MySelfDbContext db,
        TokenService tokens,
        HttpResponse response,
        IHostEnvironment env,
        CancellationToken ct)
    {
        var accessToken = tokens.CreateAccessToken(user);
        var refreshToken = tokens.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshToken.Hash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = refreshToken.ExpiresAt,
            IsPersistent = trustThisDevice,
        });
        await db.SaveChangesAsync(ct);

        SetRefreshCookie(response, refreshToken.RawValue, refreshToken.ExpiresAt, trustThisDevice, env);

        return new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAt,
            new AuthUser(user.Id, user.UserName!, user.Email!));
    }

    /// <summary>
    /// "Trust this device" (trustThisDevice=true) sets Expires, so the browser keeps the
    /// cookie across a restart — a persistent cookie. Left false, no Expires/Max-Age is set
    /// at all, which makes it a *session* cookie: the browser discards it when the browser
    /// itself (not just the tab) closes, so the next visit needs a fresh login.
    /// </summary>
    private static void SetRefreshCookie(
        HttpResponse response,
        string rawToken,
        DateTimeOffset expiresAt,
        bool persistent,
        IHostEnvironment env)
    {
        var options = CookieAttributes(env);
        if (persistent)
        {
            options.Expires = expiresAt;
        }

        response.Cookies.Append("refreshToken", rawToken, options);
    }

    /// <summary>
    /// The attributes that identify "the refresh-token cookie" to the browser — shared by
    /// Set (register/login/refresh) and Delete (logout), since Delete only actually
    /// removes a cookie the browser recognizes as the same one (same Path in particular).
    /// </summary>
    private static CookieOptions CookieAttributes(IHostEnvironment env) => new()
    {
        HttpOnly = true,
        // Secure cookies are dropped by the browser over plain http, which is how the
        // API runs in local dev (see README) — relax only in Development, same spirit
        // as docs/05's dev-only allowance for email links. Always Secure elsewhere.
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        Path = "/api/v1/auth",
    };

    private static IResult ValidationProblem(IDictionary<string, List<string>> errors) =>
        Results.ValidationProblem(
            errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            title: "Validation failed");

    private static Dictionary<string, List<string>> GroupByField(IEnumerable<IdentityError> errors)
    {
        var grouped = new Dictionary<string, List<string>>();

        foreach (var error in errors)
        {
            var field = error.Code switch
            {
                _ when error.Code.StartsWith("Password", StringComparison.Ordinal) => "password",
                "DuplicateUserName" or "InvalidUserName" => "username",
                "DuplicateEmail" or "InvalidEmail" => "email",
                _ => "form",
            };

            if (!grouped.TryGetValue(field, out var list))
            {
                list = [];
                grouped[field] = list;
            }

            // Identity's default InvalidUserName message hardcodes "letters or digits",
            // which is wrong once AllowedUserNameCharacters (Program.cs) also permits
            // '_', '.', '-' — substitute an accurate message instead of shipping the lie.
            var description = error.Code == "InvalidUserName"
                ? "Only letters, numbers, dots, underscores and hyphens are allowed."
                : error.Description;

            list.Add(description);
        }

        return grouped;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
