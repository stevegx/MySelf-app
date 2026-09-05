using Microsoft.EntityFrameworkCore;
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

    private static WorkoutDay CloneDay(WorkoutDay source)
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

    private static string Truncate(string value, int max) =>
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
