using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Exercises;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Workouts;

/// <summary>
/// The custom program builder's top level (docs/02, Story 3): a user's programs, their days,
/// and activation. Every handler is scoped to the caller — another user's program id returns
/// 404, never someone else's data.
/// </summary>
public static class ProgramEndpoints
{
    public static IEndpointRouteBuilder MapProgramEndpoints(this IEndpointRouteBuilder app)
    {
        var programs = app.MapGroup("/api/v1/programs")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        programs.MapGet("", ListAsync).WithName("ListPrograms");
        programs.MapGet("/archived", ListArchivedAsync).WithName("ListArchivedPrograms");
        programs.MapPost("", CreateAsync).WithName("CreateProgram");
        programs.MapPost("/{id:guid}/restore", RestoreAsync).WithName("RestoreProgram");
        programs.MapGet("/{id:guid}", GetAsync).WithName("GetProgram");
        programs.MapGet("/{id:guid}/stats", StatsAsync).WithName("GetProgramStats");
        programs.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateProgram");
        programs.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteProgram");
        programs.MapPost("/{id:guid}/archive", ArchiveAsync).WithName("ArchiveProgram");
        programs.MapPost("/{id:guid}/activate", ActivateAsync).WithName("ActivateProgram");
        programs.MapPost("/{id:guid}/clone", CloneAsync).WithName("CloneProgram");
        programs.MapPost("/{id:guid}/days", AddDayAsync).WithName("AddWorkoutDay");

        return app;
    }

    private static async Task<IResult> ListAsync(HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var items = await db.OwnedPrograms(userId)
            .AsNoTracking()
            .Where(p => p.ArchivedAt == null)
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new ProgramListItem(
                p.Id,
                p.Name,
                p.SplitLabel,
                p.IsActive,
                p.Days.Count,
                p.Days.SelectMany(d => d.Exercises).Count(),
                p.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    private static async Task<IResult> ListArchivedAsync(HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var items = await db.OwnedPrograms(userId)
            .AsNoTracking()
            .Where(p => p.ArchivedAt != null)
            .OrderByDescending(p => p.ArchivedAt)
            .Select(p => new ProgramListItem(
                p.Id,
                p.Name,
                p.SplitLabel,
                false,
                p.Days.Count,
                p.Days.SelectMany(d => d.Exercises).Count(),
                p.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateAsync(
        CreateProgramRequest request,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return Validation("name", "Enter a program name (1–120 characters).");
        }

        var programCount = await db.OwnedPrograms(userId)
            .CountAsync(p => p.ArchivedAt == null, ct);
        if (programCount >= WorkoutLimits.MaxProgramsPerUser)
        {
            return TooMany($"You can have at most {WorkoutLimits.MaxProgramsPerUser} programs. Archive one first.");
        }

        var program = new WorkoutProgram
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            SplitLabel = Trimmed(request.SplitLabel),
            CreatedAt = clock.GetUtcNow(),
        };
        db.WorkoutPrograms.Add(program);
        await db.SaveChangesAsync(ct);

        return Results.Json(
            new ProgramListItem(program.Id, program.Name, program.SplitLabel, false, 0, 0, program.CreatedAt),
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var program = await db.OwnedPrograms(userId)
            .AsNoTracking()
            .Where(p => p.Id == id && p.ArchivedAt == null)
            .Select(p => new ProgramDetail(
                p.Id,
                p.Name,
                p.SplitLabel,
                p.IsActive,
                p.CreatedAt,
                p.RowVersion,
                p.Days
                    .OrderBy(d => d.SortOrder)
                    .Select(d => new DayListItem(d.Id, d.Name, d.SortOrder, d.Exercises.Count))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return program is null ? Results.NotFound() : Results.Ok(program);
    }

    /// <summary>
    /// The program Overview tab (docs/02 §7): aggregate numbers over every completed session
    /// that was started from this program. Sessions are matched on the soft
    /// <see cref="WorkoutSession.SourceProgramId"/> snapshot, so later edits to the program or
    /// its days don't move history. <paramref name="today"/> is the caller's local date
    /// (locked decision #8); it defaults to the server's UTC date.
    /// </summary>
    private static async Task<IResult> StatsAsync(
        Guid id,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct,
        DateOnly? today = null)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var program = await db.OwnedPrograms(userId)
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                Days = p.Days.OrderBy(d => d.SortOrder).Select(d => new { d.Id, d.Name }).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (program is null)
        {
            return Results.NotFound();
        }

        var sessions = await db.OwnedSessions(userId)
            .AsNoTracking()
            .Include(s => s.ExerciseLogs).ThenInclude(e => e.Sets)
            .Where(s => s.SourceProgramId == id && s.Status == SessionStatus.Completed)
            .ToListAsync(ct);

        var localToday = today ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var stats = ProgramStatsCalculator.Of(
            sessions,
            program.Days.Select(d => (d.Id, d.Name)).ToList(),
            localToday);

        // Completed working sets per primary muscle ÷ weeks in range — the coverage signal.
        var exerciseIds = sessions.SelectMany(s => s.ExerciseLogs).Select(e => e.ExerciseId).Distinct().ToList();
        var primaryMusclesByExercise = await db.Exercises
            .AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                Muscles = e.Muscles
                    .Where(m => m.Role == MuscleRole.Primary)
                    .Select(m => m.Muscle.Name)
                    .ToList(),
            })
            .ToDictionaryAsync(x => x.Id, x => x.Muscles, ct);

        var setsByMuscle = new Dictionary<string, int>();
        foreach (var log in sessions.SelectMany(s => s.ExerciseLogs))
        {
            if (!primaryMusclesByExercise.TryGetValue(log.ExerciseId, out var muscles) || muscles.Count == 0)
            {
                continue;
            }
            var completed = log.Sets.Count(s => s.CompletedAt is not null);
            foreach (var m in muscles)
            {
                setsByMuscle[m] = setsByMuscle.GetValueOrDefault(m) + completed;
            }
        }

        var weeks = Math.Max(1, stats.WeeksInRange);
        var muscleWeeklySets = setsByMuscle
            .Select(kv => new MuscleWeeklySets(kv.Key, Math.Round((double)kv.Value / weeks, 1)))
            .OrderBy(x => x.SetsPerWeek)
            .ThenBy(x => x.Muscle)
            .ToList();

        // PRs achieved in those sessions (docs/02 §7 PR types), newest first.
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var prs = await db.PersonalRecords
            .AsNoTracking()
            .Where(p => p.UserId == userId && sessionIds.Contains(p.SessionId))
            .Join(
                db.Exercises,
                p => p.ExerciseId,
                e => e.Id,
                (p, e) => new { p.Type, p.Value, p.AchievedOn, ExerciseName = e.Name })
            .OrderByDescending(x => x.AchievedOn)
            .Take(50)
            .ToListAsync(ct);

        return Results.Ok(new ProgramStats(
            stats.TotalSessions,
            stats.FirstPerformedOn,
            stats.LastPerformedOn,
            stats.SessionsThisWeek,
            stats.SessionsThisMonth,
            stats.WeeklyAverage,
            stats.TotalVolumeKg,
            stats.AvgDurationSeconds,
            stats.CompletedSets,
            stats.SkippedSets,
            stats.SkippedSetRate,
            stats.PerDay
                .Select(d => new ProgramDayStat(d.DayId, d.DayName, d.Sessions, d.LastPerformedOn))
                .ToList(),
            prs
                .Select(x => new ProgramPrStat(x.ExerciseName, x.Type.ToString(), (double)x.Value, x.AchievedOn))
                .ToList(),
            muscleWeeklySets));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateProgramRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var program = await db.OwnedPrograms(userId)
            .Include(p => p.Days)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (program is null)
        {
            return Results.NotFound();
        }

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            {
                return Validation("name", "Enter a program name (1–120 characters).");
            }

            program.Name = name;
        }

        program.SplitLabel = Trimmed(request.SplitLabel);

        if (request.DayOrder is { Count: > 0 } order)
        {
            ApplyOrder(program.Days, d => d.Id, order, (d, i) => d.SortOrder = i);
        }

        if (!await TrySaveWithRowVersionAsync(db, program, request.RowVersion, ct))
        {
            return StaleWrite();
        }

        return Results.NoContent();
    }

    /// <summary>
    /// Hard delete (docs/07: recoverable removal where there is history — there is none here,
    /// a program only holds builder content). Cascades to its days, exercises, set
    /// prescriptions and superset groups. Completed <see cref="WorkoutSession"/>s are
    /// independent snapshots (their <c>SourceDayId</c> is a soft pointer, no FK), so they are
    /// untouched. Use <c>POST /{id}/archive</c> to keep it around instead.
    /// </summary>
    private static async Task<IResult> DeleteAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var program = await db.OwnedPrograms(userId).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null)
        {
            return Results.NotFound();
        }

        db.WorkoutPrograms.Remove(program);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ArchiveAsync(
        Guid id,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var program = await db.OwnedPrograms(userId).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null)
        {
            return Results.NotFound();
        }

        program.ArchivedAt = clock.GetUtcNow();
        program.IsActive = false; // can't have an archived program be the active one
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RestoreAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var program = await db.OwnedPrograms(userId)
            .FirstOrDefaultAsync(p => p.Id == id && p.ArchivedAt != null, ct);
        if (program is null)
        {
            return Results.NotFound();
        }

        var activeCount = await db.OwnedPrograms(userId).CountAsync(p => p.ArchivedAt == null, ct);
        if (activeCount >= WorkoutLimits.MaxProgramsPerUser)
        {
            return TooMany(
                $"You can have at most {WorkoutLimits.MaxProgramsPerUser} programs. Archive another before restoring this one.");
        }

        program.ArchivedAt = null; // restored as a draft — the user activates it explicitly
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ActivateAsync(
        Guid id,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var target = await db.OwnedPrograms(userId)
            .FirstOrDefaultAsync(p => p.Id == id && p.ArchivedAt == null, ct);
        if (target is null)
        {
            return Results.NotFound();
        }

        // The DB has a filtered unique index (one active program per user), and it is checked
        // per-statement — so we must never have two active rows even mid-operation. Deactivate
        // the old one first with its own UPDATE, then activate the target, both inside one
        // explicit transaction so a crash between them can't leave the user with none.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.OwnedPrograms(userId)
            .Where(p => p.IsActive && p.Id != target.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), ct);

        target.IsActive = true;
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CloneAsync(
        Guid id,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var source = await db.OwnedPrograms(userId)
            .AsNoTracking()
            .Where(p => p.Id == id && p.ArchivedAt == null)
            .Include(p => p.Days).ThenInclude(d => d.Exercises).ThenInclude(e => e.Sets)
            .Include(p => p.Days).ThenInclude(d => d.Supersets)
            .FirstOrDefaultAsync(ct);
        if (source is null)
        {
            return Results.NotFound();
        }

        var programCount = await db.OwnedPrograms(userId).CountAsync(p => p.ArchivedAt == null, ct);
        if (programCount >= WorkoutLimits.MaxProgramsPerUser)
        {
            return TooMany($"You can have at most {WorkoutLimits.MaxProgramsPerUser} programs. Archive one first.");
        }

        var clone = new WorkoutProgram
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = Truncate($"{source.Name} (copy)", 120),
            SplitLabel = source.SplitLabel,
            IsActive = false, // a clone is always a draft — the user activates it explicitly
            CreatedAt = clock.GetUtcNow(),
            Days = source.Days
                .OrderBy(d => d.SortOrder)
                .Select(CloneDay)
                .ToList(),
        };

        db.WorkoutPrograms.Add(clone);
        await db.SaveChangesAsync(ct);

        return Results.Json(
            new ProgramListItem(
                clone.Id, clone.Name, clone.SplitLabel, false,
                clone.Days.Count, clone.Days.Sum(d => d.Exercises.Count), clone.CreatedAt),
            statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Deep-copies a day's tree into detached new entities (fresh ids, shared catalogue
    /// references). Used by program clone and by <c>POST /workout-days/{id}/duplicate</c>;
    /// callers set <see cref="WorkoutDay.ProgramId"/> / <see cref="WorkoutDay.SortOrder"/>.
    /// </summary>
    internal static WorkoutDay CloneDay(WorkoutDay source)
    {
        // New superset rows, keyed by the source id so the exercises can point at the copies.
        var supersetByOldId = source.Supersets.ToDictionary(
            s => s.Id,
            s => new SupersetGroup
            {
                Id = Guid.NewGuid(),
                SortOrder = s.SortOrder,
                RestAfterRoundSeconds = s.RestAfterRoundSeconds,
            });

        return new WorkoutDay
        {
            Id = Guid.NewGuid(),
            Name = source.Name,
            SortOrder = source.SortOrder,
            EstimatedDurationMinutes = source.EstimatedDurationMinutes,
            FocusMuscleIds = [.. source.FocusMuscleIds],
            Supersets = supersetByOldId.Values.ToList(),
            Exercises = source.Exercises
                .OrderBy(e => e.SortOrder)
                .Select(e => new DayExercise
                {
                    Id = Guid.NewGuid(),
                    ExerciseId = e.ExerciseId, // catalogue reference — shared, not copied
                    SortOrder = e.SortOrder,
                    SupersetGroup = e.SupersetGroupId is { } oldId ? supersetByOldId[oldId] : null,
                    SupersetMemberOrder = e.SupersetMemberOrder,
                    RestSeconds = e.RestSeconds,
                    Notes = e.Notes,
                    Sets = e.Sets
                        .OrderBy(s => s.SortOrder)
                        .Select(s => new SetPrescription
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
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }

    internal static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static async Task<IResult> AddDayAsync(
        Guid id,
        CreateDayRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var program = await db.OwnedPrograms(userId)
            .Include(p => p.Days)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null)
        {
            return Results.NotFound();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
        {
            return Validation("name", "Enter a day name (1–80 characters).");
        }

        if (program.Days.Count >= WorkoutLimits.MaxDaysPerProgram)
        {
            return TooMany($"A program can have at most {WorkoutLimits.MaxDaysPerProgram} days.");
        }

        var day = new WorkoutDay
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Name = name,
            SortOrder = program.Days.Count == 0 ? 0 : program.Days.Max(d => d.SortOrder) + 1,
        };
        db.WorkoutDays.Add(day);
        await db.SaveChangesAsync(ct);

        return Results.Json(
            new DayListItem(day.Id, day.Name, day.SortOrder, 0),
            statusCode: StatusCodes.Status201Created);
    }

    // --- helpers shared with the day endpoints ---

    /// <summary>
    /// Renumbers <paramref name="items"/> by the position of their id in
    /// <paramref name="orderedIds"/>. Ids not in the list keep a stable order after the rest,
    /// so a partial or stale order list can't drop or duplicate a row.
    /// </summary>
    internal static void ApplyOrder<T>(
        IEnumerable<T> items,
        Func<T, Guid> idOf,
        IReadOnlyList<Guid> orderedIds,
        Action<T, int> setSort)
    {
        var position = orderedIds
            .Select((gid, index) => (gid, index))
            .ToDictionary(x => x.gid, x => x.index);

        var next = orderedIds.Count;
        foreach (var item in items)
        {
            setSort(item, position.TryGetValue(idOf(item), out var i) ? i : next++);
        }
    }

    /// <summary>
    /// Saves, optionally guarding on the program's <c>xmin</c> token. When
    /// <paramref name="clientRowVersion"/> is null the save is unconditional (last-write-wins,
    /// backward compatible). When it's supplied, the program row is forced to UPDATE with a
    /// <c>WHERE xmin = @original</c> clause; a 0-row result surfaces as
    /// <see cref="DbUpdateConcurrencyException"/>, which this maps to <c>false</c> so the
    /// caller returns 409. Editing any part of the tree bumps the program's token.
    /// </summary>
    internal static async Task<bool> TrySaveWithRowVersionAsync(
        MySelfDbContext db,
        WorkoutProgram program,
        uint? clientRowVersion,
        CancellationToken ct)
    {
        if (clientRowVersion is not { } expected)
        {
            await db.SaveChangesAsync(ct);
            return true;
        }

        var entry = db.Entry(program);
        entry.Property(p => p.RowVersion).OriginalValue = expected;
        entry.State = EntityState.Modified; // ensure an UPDATE is emitted even if no scalar changed

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    internal static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

    internal static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Validation failed");

    /// <summary>A size cap was hit — the request is well-formed, the resource state won't allow it (409).</summary>
    internal static IResult TooMany(string message) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Limit reached", detail: message);

    /// <summary>The client's concurrency token was stale — someone else changed the program first (409).</summary>
    internal static IResult StaleWrite() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Program changed",
            detail: "This program changed since you opened it. Reload to get the latest version, then reapply your changes.");

    internal static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
