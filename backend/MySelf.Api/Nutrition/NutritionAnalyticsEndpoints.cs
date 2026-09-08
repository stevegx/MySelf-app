using Microsoft.EntityFrameworkCore;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Nutrition;

public sealed record NutritionDayPoint(DateOnly Date, decimal Kcal, decimal ProteinG, decimal CarbG, decimal FatG);

/// <summary>
/// <c>GET /api/v1/analytics/nutrition</c> — logged calories and macros per day over a window,
/// the current targets, and the average across the days that actually have a log (docs/04
/// §12 "analytics/nutrition"). Days with nothing logged are omitted, not counted as zero.
/// </summary>
public sealed record NutritionAnalyticsResult(
    DateOnly From,
    DateOnly To,
    NutrientTargets Targets,
    int DaysLogged,
    NutrientTotals Average,
    IReadOnlyList<NutritionDayPoint> Days);

public static class NutritionAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapNutritionAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/analytics/nutrition", GetAsync)
            .WithName("NutritionAnalytics")
            .RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetAsync(
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var windowFrom = from ?? today.AddDays(-13);
        var windowTo = to ?? today;

        var rows = await db.MealLogs
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.LocalDate >= windowFrom && m.LocalDate <= windowTo)
            .SelectMany(m => m.Items, (m, i) => new { m.LocalDate, i.Kcal, i.ProteinG, i.CarbG, i.FatG })
            .ToListAsync(ct);

        var days = rows
            .GroupBy(r => r.LocalDate)
            .Select(g => new NutritionDayPoint(
                g.Key,
                g.Sum(x => x.Kcal),
                g.Sum(x => x.ProteinG),
                g.Sum(x => x.CarbG),
                g.Sum(x => x.FatG)))
            .OrderBy(d => d.Date)
            .ToList();

        var goal = await db.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        var average = days.Count == 0
            ? new NutrientTotals(0, 0, 0, 0)
            : new NutrientTotals(
                Math.Round(days.Average(d => d.Kcal), 1),
                Math.Round(days.Average(d => d.ProteinG), 1),
                Math.Round(days.Average(d => d.CarbG), 1),
                Math.Round(days.Average(d => d.FatG), 1));

        return Results.Ok(new NutritionAnalyticsResult(
            windowFrom,
            windowTo,
            new NutrientTargets(goal?.CalorieTarget, goal?.ProteinGrams, goal?.CarbGrams, goal?.FatGrams),
            days.Count,
            average,
            days));
    }
}
