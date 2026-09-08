using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Identity;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Me;

/// <summary>
/// Body-weight logging and its trend (docs/08 Phase 4, locked decisions #4, #30). Weight is
/// stored in kilograms with a UTC timestamp; several readings a day are allowed and the
/// trend collapses them to a daily average before the 7-day rolling average.
/// </summary>
public static class BodyMeasurementEndpoints
{
    private const decimal MinWeightKg = 20m;
    private const decimal MaxWeightKg = 500m;

    public static IEndpointRouteBuilder MapBodyMeasurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/me/body-measurements").RequireAuthorization();
        group.MapPost("", LogAsync).WithName("LogBodyMeasurement").RequireRateLimiting(RateLimiting.WritePolicy);
        group.MapGet("", ListAsync).WithName("ListBodyMeasurements");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteBodyMeasurement").RequireRateLimiting(RateLimiting.WritePolicy);

        app.MapGet("/api/v1/analytics/weight", TrendAsync).WithName("WeightAnalytics").RequireAuthorization();
        return app;
    }

    private static async Task<IResult> LogAsync(
        LogBodyMeasurementRequest request,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var weight = Math.Round(request.WeightKg, 2);
        if (weight < MinWeightKg || weight > MaxWeightKg)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["weightKg"] = [$"Enter a weight between {MinWeightKg:0} and {MaxWeightKg:0} kg."] },
                title: "Validation failed");
        }

        var now = clock.GetUtcNow();
        var localDate = request.LocalDate ?? DateOnly.FromDateTime(now.UtcDateTime);

        var measurement = new BodyMeasurement
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WeightKg = weight,
            LocalDate = localDate,
            MeasuredAt = now,
        };
        db.BodyMeasurements.Add(measurement);
        await db.SaveChangesAsync(ct);

        return Results.Json(
            new BodyMeasurementItem(measurement.Id, measurement.WeightKg, measurement.LocalDate, measurement.MeasuredAt),
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListAsync(
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var items = await db.BodyMeasurements
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Where(m => from == null || m.LocalDate >= from)
            .Where(m => to == null || m.LocalDate <= to)
            .OrderByDescending(m => m.LocalDate)
            .ThenByDescending(m => m.MeasuredAt)
            .Take(400)
            .Select(m => new BodyMeasurementItem(m.Id, m.WeightKg, m.LocalDate, m.MeasuredAt))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var measurement = await db.BodyMeasurements.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
        if (measurement is null)
        {
            return Results.NotFound();
        }

        db.BodyMeasurements.Remove(measurement);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> TrendAsync(
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
        var windowFrom = from ?? today.AddDays(-89);
        var windowTo = to ?? today;

        var readings = await db.BodyMeasurements
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.LocalDate >= windowFrom && m.LocalDate <= windowTo)
            .Select(m => new { m.LocalDate, m.WeightKg })
            .ToListAsync(ct);

        var points = WeightTrendCalculator.Of(
            readings.Select(r => new WeightTrendCalculator.Reading(r.LocalDate, r.WeightKg)));

        var last = points.Count > 0 ? points[^1] : (WeightTrendCalculator.DayPoint?)null;

        return Results.Ok(new WeightTrendResult(
            points.Count > 0 ? points[0].Date : null,
            last?.Date,
            last?.Average,
            last?.Date,
            WeightTrendCalculator.SevenDayChange(points),
            points.Select(p => new WeightTrendPoint(p.Date, p.Average, p.RollingAverage)).ToList()));
    }
}
