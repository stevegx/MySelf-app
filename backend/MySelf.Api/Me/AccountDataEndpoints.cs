using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySelf.Infrastructure.Identity;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Me;

/// <summary>
/// Data portability for the signed-in account (docs/05 §13 "User can export and delete
/// their data"). <c>GET /me/export</c> streams a single JSON document of everything the
/// account owns; <c>DELETE /me</c> hard-deletes the account and, by the FK cascade from
/// <c>AspNetUsers</c>, every row it owns — refresh tokens (which have no FK) are cleared
/// explicitly and the refresh cookie is dropped.
/// </summary>
public static class AccountDataEndpoints
{
    public static IEndpointRouteBuilder MapAccountDataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me/export", ExportAsync)
            .WithName("ExportMyData")
            .WithSummary("Download everything this account owns as one JSON document.")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        app.MapDelete("/api/v1/me", DeleteAccountAsync)
            .WithName("DeleteMyAccount")
            .WithSummary("Permanently delete this account and all of its data.")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        return app;
    }

    private static async Task<IResult> ExportAsync(
        HttpContext http, MySelfDbContext db, UserManager<ApplicationUser> users, CancellationToken ct,
        string? format = null)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var goals = await db.UserGoals.AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.EffectiveFrom)
            .Select(g => new ExportGoal(
                g.GoalType.ToString(), g.Source.ToString(), g.TargetWeightKg,
                g.CalorieTarget, g.ProteinGrams, g.CarbGrams, g.FatGrams, g.EffectiveFrom))
            .ToListAsync(ct);

        var measurements = await db.BodyMeasurements.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.MeasuredAt)
            .Select(m => new ExportBodyMeasurement(m.LocalDate, m.WeightKg, m.MeasuredAt))
            .ToListAsync(ct);

        var categories = await db.MealCategories.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ExportMealCategory(c.Name, c.SortOrder, c.ArchivedAt))
            .ToListAsync(ct);

        var mealLogs = await db.MealLogs.AsNoTracking()
            .Include(l => l.Items)
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.LocalDate).ThenBy(l => l.Category)
            .Select(l => new ExportMealLog(l.LocalDate, l.Category, l.Items
                .OrderBy(i => i.SortOrder)
                .Select(i => new ExportMealItem(
                    i.Name, i.ServingBasis.ToString(), i.ServingSizeGrams, i.Amount, i.Unit.ToString(),
                    i.Kcal, i.ProteinG, i.CarbG, i.FatG))
                .ToList()))
            .ToListAsync(ct);

        var customFoods = await db.CustomFoods.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .Select(f => new ExportCustomFood(
                f.Name, f.Brand, f.Barcode, f.ServingBasis.ToString(), f.ServingSizeGrams,
                f.Kcal, f.ProteinG, f.CarbG, f.FatG, f.ArchivedAt))
            .ToListAsync(ct);

        var savedMeals = await db.SavedMeals.AsNoTracking()
            .Include(m => m.Items)
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Name)
            .Select(m => new ExportSavedMeal(m.Name, m.Category, m.Notes, m.ArchivedAt, m.Items
                .OrderBy(i => i.SortOrder)
                .Select(i => new ExportSavedMealItem(
                    i.Name, i.ServingBasis.ToString(), i.ServingSizeGrams,
                    i.BasisKcal, i.BasisProteinG, i.BasisCarbG, i.BasisFatG, i.DefaultAmount, i.Unit.ToString()))
                .ToList()))
            .ToListAsync(ct);

        var programs = await db.WorkoutPrograms.AsNoTracking()
            .Include(p => p.Days).ThenInclude(d => d.Exercises).ThenInclude(e => e.Exercise)
            .Include(p => p.Days).ThenInclude(d => d.Exercises).ThenInclude(e => e.Sets)
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        var programExport = programs.Select(p => new ExportProgram(
            p.Name, p.SplitLabel, p.IsActive, p.CreatedAt, p.ArchivedAt,
            p.Days.OrderBy(d => d.SortOrder).Select(d => new ExportProgramDay(
                d.Name, d.SortOrder, d.EstimatedDurationMinutes, d.FocusMuscleIds,
                d.Exercises.OrderBy(e => e.SortOrder).Select(e => new ExportPrescribedExercise(
                    e.Exercise.Name, e.SortOrder, e.RestSeconds, e.Notes,
                    e.Sets.OrderBy(s => s.SortOrder).Select(s => new ExportPrescribedSet(
                        s.SortOrder, s.Kind.ToString(), s.IsAmrap, s.TargetToFailure,
                        s.TargetRepsMin, s.TargetRepsMax, s.TargetWeightKg, s.TargetRir)).ToList())).ToList())).ToList())).ToList();

        var sessions = await db.WorkoutSessions.AsNoTracking()
            .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.StartedAt)
            .ToListAsync(ct);

        var sessionExport = sessions.Select(s => new ExportSession(
            s.DayName, s.ProgramName, s.Status.ToString(), s.StartedAt, s.CompletedAt,
            s.PerformedOnLocalDate, s.Notes, s.WasEdited,
            s.ExerciseLogs.OrderBy(e => e.SortOrder).Select(e => new ExportSessionExercise(
                e.ExerciseName, e.TrackingMode.ToString(), e.SortOrder,
                e.Sets.OrderBy(x => x.SortOrder).Select(x => new ExportLoggedSet(
                    x.SortOrder, x.Kind.ToString(), x.WeightKg, x.AddedWeightKg, x.AssistanceKg, x.Reps,
                    x.DurationSeconds, x.DistanceMeters, x.Rir, x.ReachedFailure,
                    x.CompletedAt, x.SkippedAt, x.SkippedReason)).ToList())).ToList())).ToList();

        var prs = await db.PersonalRecords.AsNoTracking()
            .Join(db.Exercises.AsNoTracking(), pr => pr.ExerciseId, ex => ex.Id, (pr, ex) => new { pr, ex.Name })
            .Where(x => x.pr.UserId == userId)
            .OrderBy(x => x.pr.AchievedOn)
            .Select(x => new ExportPersonalRecord(
                x.Name, x.pr.Type.ToString(), x.pr.Value, x.pr.WeightKg, x.pr.Reps, x.pr.AchievedOn))
            .ToListAsync(ct);

        var export = new DataExport(
            DateTimeOffset.UtcNow,
            new ExportAccount(userId, user.UserName, user.Email),
            profile is null
                ? null
                : new ExportProfile(
                    profile.DateOfBirth, profile.HeightCm, profile.CalculationSex?.ToString(),
                    profile.UnitSystem.ToString(), profile.Timezone, profile.Locale,
                    profile.OnboardingCompletedAt, profile.WarnOffFocusExercises),
            goals, measurements, categories, mealLogs, customFoods, savedMeals,
            programExport, sessionExport, prs);

        var stamp = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Results.File(
                DataExportSpreadsheet.Build(export),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"myself-export-{stamp}.xlsx");
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, ExportJson);
        return Results.File(bytes, "application/json", $"myself-export-{stamp}.json");
    }

    private static async Task<IResult> DeleteAccountAsync(
        HttpContext http,
        HttpResponse response,
        MySelfDbContext db,
        UserManager<ApplicationUser> users,
        IHostEnvironment env,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        // Refresh tokens have no FK to AspNetUsers (Domain can't reference the Identity type),
        // so the user-delete cascade won't take them — clear them first.
        await db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Could not delete the account",
                detail: string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // Drop the refresh cookie (attributes must match how AuthEndpoints set it).
        response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
        });

        return Results.NoContent();
    }

    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
}
