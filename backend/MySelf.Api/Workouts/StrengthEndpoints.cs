using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Exercises;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Persistence;
using static MySelf.Api.Workouts.ProgramEndpoints;

namespace MySelf.Api.Workouts;

/// <summary>
/// Per-exercise performance history and the strength trend (docs/04 §12
/// <c>/exercises/{id}/history</c>, <c>/analytics/strength</c>). Progress is tied to the
/// <em>exercise</em>, across every day it appears in (docs/02 §7).
/// </summary>
public static class StrengthEndpoints
{
    public static IEndpointRouteBuilder MapStrengthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/exercises/{id:guid}/history", HistoryAsync)
            .WithName("ExerciseHistory")
            .RequireAuthorization();

        app.MapGet("/api/v1/analytics/strength", StrengthTrendAsync)
            .WithName("StrengthAnalytics")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> HistoryAsync(
        Guid id,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct,
        Guid? dayId = null)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var exerciseName = await db.Exercises.Where(e => e.Id == id).Select(e => e.Name).FirstOrDefaultAsync(ct);
        if (exerciseName is null)
        {
            return Results.NotFound();
        }

        var logs = await db.ExerciseLogs
            .AsNoTracking()
            .Include(e => e.Session)
            .Include(e => e.Sets)
            .Where(e => e.ExerciseId == id
                && e.Session.UserId == userId
                && e.Session.Status == SessionStatus.Completed
                && (dayId == null || e.Session.SourceDayId == dayId))
            .ToListAsync(ct);

        var sessions = logs
            .GroupBy(e => e.Session)
            .Select(g => BuildEntry(g.Key, g.ToList()))
            .OrderByDescending(e => e.PerformedOn)
            .Take(50)
            .ToList();

        var prs = await CurrentPersonalRecordsAsync(db, userId, id, ct);

        return Results.Ok(new ExerciseHistoryResult(id, exerciseName, prs, sessions));
    }

    private static async Task<IResult> StrengthTrendAsync(
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct,
        Guid exerciseId,
        string range = "90d")
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var since = CutoffFor(range);

        var logs = await db.ExerciseLogs
            .AsNoTracking()
            .Include(e => e.Session)
            .Include(e => e.Sets)
            .Where(e => e.ExerciseId == exerciseId
                && e.Session.UserId == userId
                && e.Session.Status == SessionStatus.Completed
                && (since == null || e.Session.PerformedOnLocalDate >= since))
            .ToListAsync(ct);

        var points = logs
            .GroupBy(e => e.Session)
            .Select(g =>
            {
                var entry = BuildEntry(g.Key, g.ToList());
                return new StrengthPoint(entry.PerformedOn, entry.EstimatedOneRepMax, entry.Volume);
            })
            .OrderBy(p => p.Date)
            .ToList();

        return Results.Ok(new StrengthAnalyticsResult(exerciseId, range, points));
    }

    private static ExerciseHistoryEntry BuildEntry(WorkoutSession session, List<ExerciseLog> logs)
    {
        var completed = logs
            .SelectMany(l => l.Sets)
            .Where(s => s.CompletedAt is not null && s.WeightKg is not null && s.Reps is not null)
            .Select(s => (Weight: s.WeightKg!.Value, Reps: s.Reps!.Value))
            .ToList();

        (decimal Weight, int Reps)? topByE1Rm = completed.Count == 0
            ? null
            : completed.MaxBy(s => StrengthMath.EstimatedOneRepMax(s.Weight, s.Reps));

        var volume = completed.Sum(s => StrengthMath.SetVolume(s.Weight, s.Reps));

        return new ExerciseHistoryEntry(
            session.Id,
            session.PerformedOnLocalDate ?? DateOnly.FromDateTime(session.StartedAt.UtcDateTime),
            session.DayName,
            topByE1Rm?.Weight,
            topByE1Rm?.Reps,
            topByE1Rm is { } t ? decimal.Round(StrengthMath.EstimatedOneRepMax(t.Weight, t.Reps), 2) : null,
            volume,
            completed.Count);
    }

    private static async Task<List<PersonalRecordDetail>> CurrentPersonalRecordsAsync(
        MySelfDbContext db, Guid userId, Guid exerciseId, CancellationToken ct)
    {
        var all = await db.PersonalRecords
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.ExerciseId == exerciseId)
            .ToListAsync(ct);

        // The current best of each type (for MostRepsAtWeight, the best per weight).
        return all
            .GroupBy(p => (p.Type, p.WeightKg))
            .Select(g => g.OrderByDescending(p => p.Value).ThenByDescending(p => p.AchievedOn).First())
            .Select(p => new PersonalRecordDetail(p.Type.ToString(), p.Value, p.WeightKg, p.Reps, p.AchievedOn))
            .OrderBy(p => p.Type)
            .ToList();
    }

    private static DateOnly? CutoffFor(string range)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return range switch
        {
            "30d" => today.AddDays(-30),
            "90d" => today.AddDays(-90),
            "1y" => today.AddYears(-1),
            _ => null, // "all"
        };
    }
}
