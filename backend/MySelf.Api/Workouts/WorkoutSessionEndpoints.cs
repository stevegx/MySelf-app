using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Exercises;
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
        sessions.MapGet("", ListAsync).WithName("ListWorkoutSessions");
        sessions.MapGet("/active", GetActiveAsync).WithName("GetActiveWorkoutSession");
        sessions.MapGet("/{id:guid}", GetByIdAsync).WithName("GetWorkoutSession");
        sessions.MapPost("/{id:guid}/set-logs", LogSetAsync).WithName("LogWorkoutSet");
        sessions.MapPost("/{id:guid}/skip-set", SkipSetAsync).WithName("SkipWorkoutSet");
        sessions.MapPost("/{id:guid}/exercises", AddExerciseAsync).WithName("AddSessionExercise");
        sessions.MapPost("/{id:guid}/exercises/{exerciseLogId:guid}/replace", ReplaceExerciseAsync).WithName("ReplaceSessionExercise");
        sessions.MapPost("/{id:guid}/exercises/{exerciseLogId:guid}/add-set", AddSetAsync).WithName("AddSessionSet");
        sessions.MapDelete("/{id:guid}/exercises/{exerciseLogId:guid}", RemoveExerciseAsync).WithName("RemoveSessionExercise");
        sessions.MapPost("/{id:guid}/complete", CompleteAsync).WithName("CompleteWorkoutSession");
        sessions.MapPost("/{id:guid}/discard", DiscardAsync).WithName("DiscardWorkoutSession");
        sessions.MapPost("/{id:guid}/reschedule", RescheduleAsync).WithName("RescheduleWorkoutSession");

        app.MapGet("/api/v1/workout-calendar", CalendarAsync)
            .WithName("WorkoutCalendar")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> CalendarAsync(
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct,
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? programId = null)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? today.AddDays(-42);
        var end = to ?? today;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var rows = await db.OwnedSessions(userId)
            .AsNoTracking()
            .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
            .Where(s => s.Status == SessionStatus.Completed
                && s.PerformedOnLocalDate >= start
                && s.PerformedOnLocalDate <= end
                && (programId == null || s.SourceProgramId == programId))
            .ToListAsync(ct);

        var days = rows
            .GroupBy(s => s.PerformedOnLocalDate!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new CalendarDay(
                g.Key,
                g.OrderBy(s => s.StartedAt)
                    .Select(s => new WorkoutSessionListItem(
                        s.Id, s.DayName, s.ProgramName, s.Status.ToString(),
                        s.StartedAt, s.CompletedAt, s.PerformedOnLocalDate, s.WasEdited, ToSummary(s)))
                    .ToList()))
            .ToList();

        return Results.Ok(new WorkoutCalendarResult(start, end, days));
    }

    private static async Task<IResult> RescheduleAsync(
        Guid id,
        RescheduleSessionRequest request,
        HttpContext http,
        MySelfDbContext db,
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

        if (session.Status != SessionStatus.Completed)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Session is not completed",
                detail: "Only a completed session has a calendar date to move.");
        }

        session.PerformedOnLocalDate = request.LocalDate;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDetail(session));
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
                .Include(d => d.Supersets)
                .Include(d => d.Exercises).ThenInclude(e => e.Exercise)
                .Include(d => d.Exercises).ThenInclude(e => e.Sets)
                .FirstOrDefaultAsync(d => d.Id == dayId, ct);
            if (day is null)
            {
                return Results.NotFound();
            }
        }

        var supersetRest = day?.Supersets.ToDictionary(s => s.Id, s => s.RestAfterRoundSeconds)
            ?? new Dictionary<Guid, int>();

        var session = new WorkoutSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceDayId = day?.Id,
            SourceProgramId = day?.Program.Id,
            DayName = day?.Name,
            ProgramName = day?.Program.Name,
            Status = SessionStatus.InProgress,
            StartedAt = clock.GetUtcNow(),
            ExerciseLogs = day is null
                ? []
                : day.Exercises.OrderBy(e => e.SortOrder).Select(e => SnapshotExercise(e, supersetRest)).ToList(),
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

    private static async Task<IResult> ListAsync(
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct,
        string status = "Completed",
        int page = 1,
        int pageSize = 20)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<SessionStatus>(status, ignoreCase: true, out var wanted))
        {
            return Validation("status", "status must be InProgress, Completed or Discarded.");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.OwnedSessions(userId)
            .AsNoTracking()
            .Where(s => s.Status == wanted);

        var total = await query.CountAsync(ct);

        var sessions = await query
            .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
            .OrderByDescending(s => s.PerformedOnLocalDate)
            .ThenByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = sessions
            .Select(s => new WorkoutSessionListItem(
                s.Id, s.DayName, s.ProgramName, s.Status.ToString(),
                s.StartedAt, s.CompletedAt, s.PerformedOnLocalDate, s.WasEdited, ToSummary(s)))
            .ToList();

        return Results.Ok(new WorkoutSessionListResult(items, page, pageSize, total));
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

        var session = await LoadForSetEditAsync(db, userId, id, ct);
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

        if (session.Status == SessionStatus.Completed)
        {
            await RecomputeAfterEditAsync(db, userId, session, clock.GetUtcNow(), ct);
        }

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

        var session = await LoadForSetEditAsync(db, userId, id, ct);
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

        if (session.Status == SessionStatus.Completed)
        {
            await RecomputeAfterEditAsync(db, userId, session, clock.GetUtcNow(), ct);
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToSetDetail(set));
    }

    private static async Task<IResult> AddExerciseAsync(
        Guid id,
        AddSessionExerciseRequest request,
        HttpContext http,
        MySelfDbContext db,
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

        var exercise = await db.Exercises
            .Where(e => e.Id == request.ExerciseId)
            .Select(e => new { e.Name, e.DefaultTrackingMode })
            .FirstOrDefaultAsync(ct);
        if (exercise is null)
        {
            return Validation("exerciseId", "That exercise is not in the catalogue.");
        }

        var setCount = Math.Clamp(request.Sets ?? 3, 1, 20);
        var log = new ExerciseLog
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ExerciseId = request.ExerciseId,
            ExerciseName = exercise.Name,
            TrackingMode = exercise.DefaultTrackingMode,
            SortOrder = session.ExerciseLogs.Count == 0 ? 0 : session.ExerciseLogs.Max(e => e.SortOrder) + 1,
            Sets = Enumerable.Range(0, setCount)
                .Select(i => new SetLog { Id = Guid.NewGuid(), SortOrder = i, Kind = SetKind.Standard })
                .ToList(),
        };
        // Add through the DbSet (not the nav collection) so EF marks the graph Added even
        // though session is already tracked and the keys are client-set. EF's relationship
        // fixup then adds `log` to session.ExerciseLogs itself — doing both would duplicate it.
        db.ExerciseLogs.Add(log);

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDetail(session));
    }

    private static async Task<IResult> ReplaceExerciseAsync(
        Guid id,
        Guid exerciseLogId,
        ReplaceSessionExerciseRequest request,
        HttpContext http,
        MySelfDbContext db,
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

        var log = session.ExerciseLogs.FirstOrDefault(e => e.Id == exerciseLogId);
        if (log is null)
        {
            return Results.NotFound();
        }

        var replacement = await db.Exercises
            .Where(e => e.Id == request.ExerciseId)
            .Select(e => new { e.Name, e.DefaultTrackingMode })
            .FirstOrDefaultAsync(ct);
        if (replacement is null)
        {
            return Validation("exerciseId", "That exercise is not in the catalogue.");
        }

        var futureToo = string.Equals(request.Scope, "TodayAndFuture", StringComparison.OrdinalIgnoreCase);

        var originalExerciseId = log.ExerciseId;
        log.ExerciseId = request.ExerciseId;
        log.ExerciseName = replacement.Name;
        log.TrackingMode = replacement.DefaultTrackingMode;

        // Keep the set slots but wipe anything performed on the ones not yet completed.
        foreach (var s in log.Sets.Where(s => s.CompletedAt is null && s.SkippedAt is null))
        {
            s.WeightKg = s.AddedWeightKg = s.AssistanceKg = s.DistanceMeters = null;
            s.Reps = s.DurationSeconds = s.Rir = null;
        }

        if (futureToo && session.SourceDayId is { } dayId)
        {
            // Best-effort: update the first matching exercise on the source day.
            var dayExercise = await db.DayExercises
                .FirstOrDefaultAsync(de => de.DayId == dayId && de.ExerciseId == originalExerciseId, ct);
            if (dayExercise is not null)
            {
                dayExercise.ExerciseId = request.ExerciseId;
            }
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDetail(session));
    }

    private static async Task<IResult> AddSetAsync(
        Guid id,
        Guid exerciseLogId,
        HttpContext http,
        MySelfDbContext db,
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

        var log = session.ExerciseLogs.FirstOrDefault(e => e.Id == exerciseLogId);
        if (log is null)
        {
            return Results.NotFound();
        }

        if (log.Sets.Count >= 20)
        {
            return Validation("sets", "An exercise can hold at most 20 sets.");
        }

        var setLog = new SetLog
        {
            Id = Guid.NewGuid(),
            ExerciseLogId = log.Id,
            SortOrder = log.Sets.Count == 0 ? 0 : log.Sets.Max(s => s.SortOrder) + 1,
            Kind = SetKind.Standard,
        };
        db.SetLogs.Add(setLog); // fixup adds it to log.Sets

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDetail(session));
    }

    private static async Task<IResult> RemoveExerciseAsync(
        Guid id,
        Guid exerciseLogId,
        HttpContext http,
        MySelfDbContext db,
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

        var log = session.ExerciseLogs.FirstOrDefault(e => e.Id == exerciseLogId);
        if (log is null)
        {
            return Results.NotFound();
        }

        if (log.Sets.Any(s => s.CompletedAt is not null))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Exercise has logged sets",
                detail: "Skip its remaining sets instead of removing it.");
        }

        db.ExerciseLogs.Remove(log);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDetail(session));
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

        // Docs/06 edge cases: a session with nothing logged or skipped is an empty workout —
        // it must not land in history, the calendar or analytics. The client hides "Finish"
        // in this state; this guards the API directly.
        var actedAnySet = session.ExerciseLogs
            .Any(e => e.Sets.Any(s => s.CompletedAt is not null || s.SkippedAt is not null));
        if (!actedAnySet)
        {
            return Validation("session", "Log or skip at least one set before finishing this workout.");
        }

        var now = clock.GetUtcNow();
        session.Status = SessionStatus.Completed;
        session.CompletedAt = now;
        // The client knows its own calendar date; fall back to the UTC date.
        session.PerformedOnLocalDate = request.LocalDate ?? DateOnly.FromDateTime(now.UtcDateTime);
        session.Notes = Trimmed(request.Notes);

        // PRs are computed here, exactly once (docs/02: "finish calculates summary and PRs").
        var newPrs = await DetectPersonalRecordsAsync(db, userId, session, now, ct);
        db.PersonalRecords.AddRange(newPrs);

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDetail(session, newPrs.Select(ToPrDetail).ToList()));
    }

    /// <summary>
    /// For each weight-and-reps exercise in the just-finished session, compares its completed
    /// sets against the user's stored PRs for that exercise and returns the new records.
    /// </summary>
    private static async Task<List<PersonalRecord>> DetectPersonalRecordsAsync(
        MySelfDbContext db, Guid userId, WorkoutSession session, DateTimeOffset now, CancellationToken ct)
    {
        var performedOn = session.PerformedOnLocalDate ?? DateOnly.FromDateTime(now.UtcDateTime);
        var result = new List<PersonalRecord>();

        var weightRepsExercises = session.ExerciseLogs
            .Where(e => e.TrackingMode == TrackingMode.WeightAndReps)
            .GroupBy(e => e.ExerciseId);

        foreach (var group in weightRepsExercises)
        {
            var lifts = group
                .SelectMany(e => e.Sets)
                .Where(s => s.CompletedAt is not null && s.WeightKg is not null && s.Reps is not null)
                .Select(s => new CompletedLift(s.Id, s.WeightKg!.Value, s.Reps!.Value))
                .ToList();

            if (lifts.Count == 0)
            {
                continue;
            }

            var existing = await db.PersonalRecords
                .Where(p => p.UserId == userId && p.ExerciseId == group.Key)
                .ToListAsync(ct);

            result.AddRange(PersonalRecordDetector.Detect(
                userId, group.Key, session.Id, performedOn, now, lifts, existing));
        }

        return result;
    }

    private static PersonalRecordDetail ToPrDetail(PersonalRecord p) =>
        new(p.Type.ToString(), p.Value, p.WeightKg, p.Reps, p.AchievedOn);

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

    /// <summary>
    /// Loads a session for a set-level change: the running one, or a completed one being
    /// edited after the fact (docs/02 §7). A discarded session is not editable.
    /// </summary>
    private static Task<WorkoutSession?> LoadForSetEditAsync(
        MySelfDbContext db, Guid userId, Guid id, CancellationToken ct) =>
        db.OwnedSessions(userId)
            .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
            .Where(s => s.Id == id
                && (s.Status == SessionStatus.InProgress || s.Status == SessionStatus.Completed))
            .FirstOrDefaultAsync(ct);

    /// <summary>Distinguish "no such session for this user" (404) from "it exists but isn't running" (409).</summary>
    private static async Task<IResult> NotFoundOrConflict(MySelfDbContext db, Guid userId, Guid id, CancellationToken ct)
    {
        var exists = await db.OwnedSessions(userId).AnyAsync(s => s.Id == id, ct);
        return exists ? SessionNotInProgress() : Results.NotFound();
    }

    /// <summary>
    /// After a set on an already-completed session changes: flag it Edited and rebuild that
    /// session's personal records — an edit can create a new PR or invalidate one it held
    /// (docs/02 §7). Old rows for this session are dropped and re-detected against the rest
    /// of the user's history.
    /// </summary>
    private static async Task RecomputeAfterEditAsync(
        MySelfDbContext db, Guid userId, WorkoutSession session, DateTimeOffset now, CancellationToken ct)
    {
        session.WasEdited = true;

        var stale = await db.PersonalRecords.Where(p => p.SessionId == session.Id).ToListAsync(ct);
        db.PersonalRecords.RemoveRange(stale);
        await db.SaveChangesAsync(ct); // clear first so re-detection compares against a clean history

        var fresh = await DetectPersonalRecordsAsync(db, userId, session, now, ct);
        db.PersonalRecords.AddRange(fresh);
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

    private static ExerciseLog SnapshotExercise(DayExercise e, IReadOnlyDictionary<Guid, int> supersetRest) => new()
    {
        Id = Guid.NewGuid(),
        ExerciseId = e.ExerciseId,
        ExerciseName = e.Exercise.Name,
        TrackingMode = e.Exercise.DefaultTrackingMode,
        SortOrder = e.SortOrder,
        RestSeconds = e.RestSeconds,
        SupersetGroupSnapshotId = e.SupersetGroupId,
        SupersetMemberOrder = e.SupersetMemberOrder,
        SupersetRestAfterRoundSeconds = e.SupersetGroupId is { } gid && supersetRest.TryGetValue(gid, out var r) ? r : null,
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

    private static WorkoutSessionDetail ToDetail(WorkoutSession s, IReadOnlyList<PersonalRecordDetail>? newPrs = null) => new(
        s.Id,
        s.SourceDayId,
        s.DayName,
        s.ProgramName,
        s.Status.ToString(),
        s.StartedAt,
        s.CompletedAt,
        s.PerformedOnLocalDate,
        s.Notes,
        s.WasEdited,
        ToSummary(s),
        newPrs ?? [],
        s.ExerciseLogs
            .OrderBy(e => e.SortOrder)
            .Select(e => new ExerciseLogDetail(
                e.Id,
                e.ExerciseId,
                e.ExerciseName,
                e.TrackingMode.ToString(),
                e.SortOrder,
                e.RestSeconds,
                e.SupersetGroupSnapshotId,
                e.SupersetMemberOrder,
                e.SupersetRestAfterRoundSeconds,
                e.Sets.OrderBy(set => set.SortOrder).Select(ToSetDetail).ToList()))
            .ToList());

    private static SessionSummary ToSummary(WorkoutSession s)
    {
        var r = SessionSummaryCalculator.Of(s);
        return new SessionSummary(
            r.DurationSeconds, r.CompletedSetCount, r.SkippedSetCount, r.TotalReps, r.TotalVolumeKg);
    }

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
