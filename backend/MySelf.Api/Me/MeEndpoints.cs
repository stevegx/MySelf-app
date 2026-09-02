using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySelf.Api.Auth;
using MySelf.Domain.Identity;
using MySelf.Infrastructure.Identity;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Me;

/// <summary>
/// docs/04's "current user" surface. <c>GET /me</c> returns the account summary, the
/// onboarding <see cref="UserProfile"/> (null until step 1) and the current
/// <see cref="UserGoal"/> (null until onboarding completes); <c>PUT /me/profile</c> creates
/// or updates that profile. The estimate and goal-completion endpoints live in
/// <see cref="NutritionEstimateEndpoints"/> and <see cref="OnboardingEndpoints"/>.
/// </summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        // Both routes are mapped with their full path (rather than via MapGroup) so
        // GET /api/v1/me keeps its exact existing pattern — no trailing slash.
        app.MapGet("/api/v1/me", GetMeAsync)
            .WithName("GetMe")
            .WithSummary("The signed-in user's account summary and onboarding profile.")
            .RequireAuthorization();

        app.MapPut("/api/v1/me/profile", UpdateProfileAsync)
            .WithName("UpdateProfile")
            .WithSummary("Create or update the signed-in user's profile (onboarding step 1).")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        MySelfDbContext db,
        CancellationToken ct)
    {
        // "sub" survives as the literal JWT claim type (not remapped to a long Microsoft URI)
        // because Program.cs sets JwtSecurityTokenHandler.DefaultMapInboundClaims = false.
        var userId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);

        // A validly signed token for a since-deleted user is the only way this happens —
        // treat it the same as "not authenticated".
        if (user is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        // "Current" goal = the one with the latest EffectiveFrom (docs/01 step 4: goals are
        // effective-dated history, never edited in place). Null until onboarding completes.
        var currentGoal = await db.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == user.Id)
            .OrderByDescending(g => g.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new MeResponse(
            new AuthUser(user.Id, user.UserName!, user.Email!),
            profile is null ? null : ToSummary(profile),
            currentGoal is null ? null : GoalSummary.From(currentGoal)));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        HttpContext httpContext,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        // Server-side validation of every calculation-affecting field (docs/04 API
        // conventions) — the estimator in a later slice depends on these being sane.
        var errors = new Dictionary<string, string[]>();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        if (!Enum.TryParse<UnitSystem>(request.UnitSystem, ignoreCase: true, out var unitSystem)
            || !Enum.IsDefined(unitSystem))
        {
            errors["unitSystem"] = ["Choose either Metric or Imperial."];
        }

        if (request.DateOfBirth is not { } dob)
        {
            errors["dateOfBirth"] = ["Date of birth is required."];
        }
        else if (dob >= today)
        {
            errors["dateOfBirth"] = ["Date of birth must be in the past."];
        }
        else if (AgeCalculator.Years(dob, today) > 120)
        {
            errors["dateOfBirth"] = ["Enter a valid date of birth."];
        }

        if (request.HeightCm is not { } heightCm)
        {
            errors["heightCm"] = ["Height is required."];
        }
        else if (heightCm is < 50m or > 260m)
        {
            errors["heightCm"] = ["Enter a height between 50 and 260 cm."];
        }

        // Optional by product rule (docs/01 step 1: "I prefer not to use this calculation").
        // Absent => null. Present but not Male/Female => a field error, not a silent drop.
        CalculationSex? calculationSex = null;
        if (request.CalculationSex is not null)
        {
            if (Enum.TryParse<CalculationSex>(request.CalculationSex, ignoreCase: true, out var parsedSex)
                && Enum.IsDefined(parsedSex))
            {
                calculationSex = parsedSex;
            }
            else
            {
                errors["calculationSex"] = ["Choose Male or Female, or omit it to skip the calorie estimate."];
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors, title: "Validation failed");
        }

        var now = clock.GetUtcNow();
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

        // Create-or-update: onboarding step 1 can be revisited and re-saved, so a second call
        // must land on the same row rather than inserting a duplicate (the shared PK would
        // reject that anyway — this just makes the intent explicit).
        if (profile is null)
        {
            profile = new UserProfile { UserId = userId, CreatedAt = now };
            db.UserProfiles.Add(profile);
        }

        profile.UnitSystem = unitSystem;
        profile.DateOfBirth = request.DateOfBirth!.Value;
        profile.HeightCm = request.HeightCm!.Value;
        profile.CalculationSex = calculationSex;
        profile.Timezone = Trimmed(request.Timezone);
        profile.Locale = Trimmed(request.Locale);
        profile.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        return Results.Ok(ToSummary(profile));
    }

    private static ProfileSummary ToSummary(UserProfile p) => new(
        p.DateOfBirth,
        p.HeightCm,
        p.CalculationSex?.ToString(),
        p.UnitSystem.ToString(),
        p.Timezone,
        p.Locale,
        p.OnboardingCompletedAt);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
