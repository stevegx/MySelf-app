using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using MySelf.Api.Auth;
using MySelf.Infrastructure.Identity;

namespace MySelf.Api.Me;

/// <summary>
/// docs/04's "current user" endpoint. For now it only echoes what's on the JWT/Identity
/// user (id/username/email) — the "profile summary" half (docs/04's UserProfile: date of
/// birth, goals, etc.) is added once the onboarding slice creates that entity.
/// </summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me", GetMeAsync)
            .WithName("GetMe")
            .WithSummary("The signed-in user's account summary.")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager)
    {
        // "sub" survives as the literal JWT claim type (not remapped to a long Microsoft
        // URI) because Program.cs sets JwtSecurityTokenHandler.DefaultMapInboundClaims = false.
        var userId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);

        // A validly signed token for a since-deleted user is the only way this happens —
        // treat it the same as "not authenticated".
        if (user is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        return Results.Ok(new AuthUser(user.Id, user.UserName!, user.Email!));
    }
}
