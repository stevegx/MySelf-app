using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Persistence;
using static MySelf.Api.Workouts.ProgramEndpoints;

namespace MySelf.Api.Workouts;

/// <summary>
/// Running a workout session (docs/02 "Starting a workout" / "Workout logging", Story 3A):
/// start from a day (or ad-hoc), log or skip each set, then finish or discard. Everything is
/// scoped to the caller and every set change is its own request (docs/02 autosave). PR /
/// volume / e1RM calculation and the calendar are later slices.
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
        sessions.MapGet("/{id:guid}", GetByIdAsync).WithName("GetWorkoutSession");
        sessions.MapPost("/{id:guid}/set-logs", LogSetAsync).WithName("LogWorkoutSet");
        sessions.MapPost("/{id:guid}/skip-set", SkipSetAsync).WithName("SkipWorkoutSet");
        sessions.MapPost("/{id:guid}/complete", CompleteAsync).WithName("CompleteWorkoutSession");
        sessions.MapPost("/{id:guid}/discard", DiscardAsync).WithName("DiscardWorkoutSession");

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

    private static async Task<IResult> GetByIdAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var session = await db.OwnedSessions(userId)
            .AsNoTracking()
            .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return session is null ? Results.NotFound() : Results.Ok(ToDetail(session));
    }

    private static async Task<IResult> LogSetAsync(
        Guid id,
        LogSetRequest request,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var session = await LoadInProgressForEditAsync(db, userId, id, ct);
        if (session is null)
        {
            return await NotFoundOrConflict(db, userId, id, ct);
        }

        var (exercise, set) = FindSet(session, request.SetLogId);
        if (set is null)
        {
            return Results.NotFound();
        }

        var performance = new SetPerformance(
            request.WeightKg, request.AddedWeightKg, request.AssistanceKg,
            request.Reps, request.DurationSeconds, request.DistanceMeters);

        var problem = SetLogValidation.OutOfRange(performance)
            ?? SetLogValidation.MissingRequiredField(exercise!.TrackingMode, performance);
        if (problem is not null)
        {
            return Validation("set", problem);
        }

        set.WeightKg = request.WeightKg;
        set.AddedWeightKg = request.AddedWeightKg;
        set.AssistanceKg = request.AssistanceKg;
        set.Reps = request.Reps;
        set.DurationSeconds = request.DurationSeconds;
        set.DistanceMeters = request.DistanceMeters;
        set.Rir = request.Rir;
        set.ReachedFailure = request.ReachedFailure;
        set.CompletedAt = clock.GetUtcNow();
        set.SkippedAt = null;
        set.SkippedReason = null;

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToSetDetail(set));
    }

    private static async Task<IResult> SkipSetAsync(
        Guid id,
        SkipSetRequest request,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var session = await LoadInProgressForEditAsync(db, userId, id, ct);
        if (session is null)
        {
            return await NotFoundOrConflict(db, userId, id, ct);
        }

        var (_, set) = FindSet(session, request.SetLogId);
        if (set is null)
        {
            return Results.NotFound();
        }

        // A skipped set carries no performed data and never enters analytics (docs/06).
        set.SkippedAt = clock.GetUtcNow();
        set.SkippedReason = Trimmed(request.Reason);
        set.CompletedAt = null;
        set.WeightKg = set.AddedWeightKg = set.AssistanceKg = set.DistanceMeters = null;
        set.Reps = set.DurationSeconds = set.Rir = null;
        set.ReachedFailure = false;

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToSetDetail(set));
    }

    private static async Task<IResult> CompleteAsync(
        Guid id,
        CompleteSessionRequest request,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var session = await db.OwnedSessions(userId)
            .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
        {
            return Results.NotFound();
        }

        if (session.Status != SessionStatus.InProgress)
        {
            return SessionNotInProgress();
        }

        var now = clock.GetUtcNow();
        session.Status = SessionStatus.Completed;
        session.CompletedAt = now;
        // The client knows its own calendar date; fall back to the UTC date.
        session.PerformedOnLocalDate = request.LocalDate ?? DateOnly.FromDateTime(now.UtcDateTime);
        session.Notes = Trimmed(request.Notes);

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDetail(session));
    }

    private static async Task<IResult> DiscardAsync(Guid id, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var session = await db.OwnedSessions(userId).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null)
        {
            return Results.NotFound();
        }

        if (session.Status != SessionStatus.InProgress)
        {
            return SessionNotInProgress();
        }

        session.Status = SessionStatus.Discarded;
        session.CompletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // --- helpers ---

    private static Task<WorkoutSession?> LoadInProgressForEditAsync(
        MySelfDbContext db, Guid userId, Guid id, CancellationToken ct) =>
        db.OwnedSessions(userId)
            .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
            .Where(s => s.Id == id && s.Status == SessionStatus.InProgress)
            .FirstOrDefaultAsync(ct);

    /// <summary>Distinguish "no such session for this user" (404) from "it exists but isn't running" (409).</summary>
    private static async Task<IResult> NotFoundOrConflict(MySelfDbContext db, Guid userId, Guid id, CancellationToken ct)
    {
        var exists = await db.OwnedSessions(userId).AnyAsync(s => s.Id == id, ct);
        return exists ? SessionNotInProgress() : Results.NotFound();
    }

    private static (ExerciseLog? Exercise, SetLog? Set) FindSet(WorkoutSession session, Guid setLogId)
    {
        foreach (var exercise in session.ExerciseLogs)
        {
            var set = exercise.Sets.FirstOrDefault(s => s.Id == setLogId);
            if (set is not null)
            {
                return (exercise, set);
            }
        }

        return (null, null);
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
        s.CompletedAt,
        s.PerformedOnLocalDate,
        s.Notes,
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
                e.Sets.OrderBy(set => set.SortOrder).Select(ToSetDetail).ToList()))
            .ToList());

    private static SetLogDetail ToSetDetail(SetLog set) => new(
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
        set.AddedWeightKg,
        set.AssistanceKg,
        set.Reps,
        set.DurationSeconds,
        set.DistanceMeters,
        set.Rir,
        set.ReachedFailure,
        set.CompletedAt,
        set.SkippedAt,
        set.SkippedReason);

    private static IResult AlreadyActive(Guid sessionId) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "A workout is already in progress",
            detail: "Resume, finish, or discard it before starting a new one.",
            extensions: new Dictionary<string, object?> { ["sessionId"] = sessionId });

    private static IResult SessionNotInProgress() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Session is not in progress",
            detail: "This workout has already been finished or discarded.");
}
