using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Persistence;
using static MySelf.Api.Workouts.ProgramEndpoints;

namespace MySelf.Api.Workouts;

/// <summary>
/// Starting and resuming a workout session (docs/02 "Starting a workout", Story 3A). Logging
/// individual sets, finishing and PR calculation are a later slice — this covers getting an
/// <see cref="SessionStatus.InProgress"/> session created (with its snapshot) and read back.
/// </summary>
public static class WorkoutSessionEndpoints
{
    public static IEndpointRouteBuilder MapWorkoutSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/v1/workout-sessions")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        sessions.MapPost("", StartAsync).WithName("StartWorkoutSession");
        sessions.MapGet("/active", GetActiveAsync).WithName("GetActiveWorkoutSession");

        return app;
    }

    private static async Task<IResult> StartAsync(
        StartSessionRequest request,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var existingId = await db.OwnedSessions(userId)
            .Where(s => s.Status == SessionStatus.InProgress)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
        if (existingId is { } activeId)
        {
            return AlreadyActive(activeId);
        }

        WorkoutDay? day = null;
        if (request.DayId is { } dayId)
        {
            day = await db.OwnedDays(userId)
                .Include(d => d.Program)
                .Include(d => d.Exercises).ThenInclude(e => e.Exercise)
                .Include(d => d.Exercises).ThenInclude(e => e.Sets)
                .FirstOrDefaultAsync(d => d.Id == dayId, ct);
            if (day is null)
            {
                return Results.NotFound();
            }
        }

        var session = new WorkoutSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceDayId = day?.Id,
            DayName = day?.Name,
            ProgramName = day?.Program.Name,
            Status = SessionStatus.InProgress,
            StartedAt = clock.GetUtcNow(),
            ExerciseLogs = day is null
                ? []
                : day.Exercises.OrderBy(e => e.SortOrder).Select(SnapshotExercise).ToList(),
        };

        db.WorkoutSessions.Add(session);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The filtered unique index caught a race: another request started a session
            // first between our check above and this insert.
            var raceId = await db.OwnedSessions(userId)
                .Where(s => s.Status == SessionStatus.InProgress)
                .Select(s => s.Id)
                .FirstAsync(ct);
            return AlreadyActive(raceId);
        }

        return Results.Json(ToDetail(session), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetActiveAsync(HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var session = await db.OwnedSessions(userId)
            .AsNoTracking()
            .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
            .Where(s => s.Status == SessionStatus.InProgress)
            .FirstOrDefaultAsync(ct);

        return session is null ? Results.NotFound() : Results.Ok(ToDetail(session));
    }

    private static ExerciseLog SnapshotExercise(DayExercise e) => new()
    {
        Id = Guid.NewGuid(),
        ExerciseId = e.ExerciseId,
        ExerciseName = e.Exercise.Name,
        TrackingMode = e.Exercise.DefaultTrackingMode,
        SortOrder = e.SortOrder,
        SupersetGroupSnapshotId = e.SupersetGroupId,
        SupersetMemberOrder = e.SupersetMemberOrder,
        Sets = e.Sets.OrderBy(s => s.SortOrder).Select(SnapshotSet).ToList(),
    };

    private static SetLog SnapshotSet(SetPrescription s) => new()
    {
        Id = Guid.NewGuid(),
        SortOrder = s.SortOrder,
        Kind = s.Kind,
        IsAmrap = s.IsAmrap,
        TargetToFailure = s.TargetToFailure,
        TargetRepsMin = s.TargetRepsMin,
        TargetRepsMax = s.TargetRepsMax,
        TargetWeightKg = s.TargetWeightKg,
        TargetRir = s.TargetRir,
    };

    private static WorkoutSessionDetail ToDetail(WorkoutSession s) => new(
        s.Id,
        s.SourceDayId,
        s.DayName,
        s.ProgramName,
        s.Status.ToString(),
        s.StartedAt,
        s.ExerciseLogs
            .OrderBy(e => e.SortOrder)
            .Select(e => new ExerciseLogDetail(
                e.Id,
                e.ExerciseId,
                e.ExerciseName,
                e.TrackingMode.ToString(),
                e.SortOrder,
                e.SupersetGroupSnapshotId,
                e.SupersetMemberOrder,
                e.Sets
                    .OrderBy(set => set.SortOrder)
                    .Select(set => new SetLogDetail(
                        set.Id,
                        set.SortOrder,
                        set.Kind.ToString(),
                        set.IsAmrap,
                        set.TargetToFailure,
                        set.TargetRepsMin,
                        set.TargetRepsMax,
                        set.TargetWeightKg,
                        set.TargetRir,
                        set.WeightKg,
                        set.Reps,
                        set.CompletedAt,
                        set.SkippedAt))
                    .ToList()))
            .ToList());

    private static IResult AlreadyActive(Guid sessionId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "A workout is already in progress",
            detail: "Resume, finish, or discard it before starting a new one.",
            extensions: new Dictionary<string, object?> { ["sessionId"] = sessionId });
}
