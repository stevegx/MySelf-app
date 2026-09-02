using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySelf.Infrastructure.Identity;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Auth;

/// <summary>
/// docs/05: "email verification/reset can initially write a dev link to a safe local sink"
/// — no real email provider yet. The reset link is logged, and echoed back in the response
/// body only in Development, so a developer can click straight through without digging
/// through logs. Swapping in a real email provider later only touches ForgotPasswordAsync.
/// </summary>
public static class PasswordResetEndpoints
{
    private const string GenericSentMessage = "If an account exists for that email, a reset link has been sent.";

    public static IEndpointRouteBuilder MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .WithSummary("Request a password reset link.");

        group.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .WithSummary("Complete a password reset using the token from the reset link.");

        return app;
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IHostEnvironment env,
        IConfiguration configuration,
        ILogger<ForgotPasswordRequest> logger,
        CancellationToken ct)
    {
        var user = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : await userManager.FindByEmailAsync(request.Email);

        // Same response either way, whether or not the account exists — telling them apart
        // would turn this into an account-enumeration oracle (same principle as login).
        if (user is null)
        {
            return Results.Ok(new ForgotPasswordResponse(GenericSentMessage, null));
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var resetLink =
            $"{frontendBaseUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

        // The dev-sink itself: nothing sends an email, this is the only place the link goes.
        logger.LogInformation("Password reset link for {Email}: {ResetLink}", user.Email, resetLink);

        return Results.Ok(new ForgotPasswordResponse(GenericSentMessage, env.IsDevelopment() ? resetLink : null));
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return InvalidResetLink();
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // A token can never be valid for an email with no account — same outward
            // response as an actually-invalid/expired token, again to avoid leaking which.
            return InvalidResetLink();
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "InvalidToken"))
            {
                return InvalidResetLink();
            }

            // Anything left over here is a password-policy violation.
            var messages = result.Errors.Select(e => e.Description).ToArray();
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["newPassword"] = messages },
                title: "Validation failed");
        }

        // A password reset is a reasonable signal the old one may have been compromised
        // (or was simply forgotten and could've been guessed) — revoke every other active
        // session so a stolen cookie elsewhere stops working too, and clear any lockout
        // from failed attempts before the reset.
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);
        await userManager.ResetAccessFailedCountAsync(user);

        return Results.Ok(new ResetPasswordResponse("Your password has been reset. Please log in."));
    }

    private static IResult InvalidResetLink() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid reset link",
            detail: "This reset link is invalid or has expired. Request a new one.");
}
