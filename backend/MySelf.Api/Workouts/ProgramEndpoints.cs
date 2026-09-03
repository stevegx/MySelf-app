using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Workouts;

/// <summary>
/// The custom program builder's top level (docs/02, Story 3): a user's programs, their
/// groups, and activation. Every handler is scoped to the caller — another user's program id
/// returns 404, never someone else's data.
/// </summary>
public static class ProgramEndpoints
{
    public static IEndpointRouteBuilder MapProgramEndpoints(this IEndpointRouteBuilder app)
    {
        var programs = app.MapGroup("/api/v1/programs")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        programs.MapGet("", ListAsync).WithName("ListPrograms");
        programs.MapPost("", CreateAsync).WithName("CreateProgram");
        programs.MapGet("/{id:guid}", GetAsync).WithName("GetProgram");
        programs.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateProgram");
        programs.MapDelete("/{id:guid}", ArchiveAsync).WithName("ArchiveProgram");
        programs.MapPost("/{id:guid}/activate", ActivateAsync).WithName("ActivateProgram");
        programs.MapPost("/{id:guid}/clone", CloneAsync).WithName("CloneProgram");
        programs.MapPost("/{id:guid}/groups", AddGroupAsync).WithName("AddWorkoutGroup");

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
                p.Groups.Count,
                p.Groups.SelectMany(g => g.Variants).Count(),
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
                p.Groups
                    .OrderBy(g => g.SortOrder)
                    .Select(g => new GroupDetail(
                        g.Id,
                        g.Name,
                        g.SortOrder,
                        g.Variants
                            .OrderBy(v => v.SortOrder)
                            .Select(v => new VariantListItem(v.Id, v.Name, v.SortOrder, v.Exercises.Count))
                            .ToList()))
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
            .Include(p => p.Groups)
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

        if (request.GroupOrder is { Count: > 0 } order)
        {
            ApplyOrder(program.Groups, g => g.Id, order, (g, i) => g.SortOrder = i);
        }

        if (!await TrySaveWithRowVersionAsync(db, program, request.RowVersion, ct))
        {
            return StaleWrite();
        }

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
            .Include(p => p.Groups).ThenInclude(g => g.Variants).ThenInclude(v => v.Exercises).ThenInclude(e => e.Sets)
            .Include(p => p.Groups).ThenInclude(g => g.Variants).ThenInclude(v => v.Supersets)
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
            Groups = source.Groups
                .OrderBy(g => g.SortOrder)
                .Select(CloneGroup)
                .ToList(),
        };

        db.WorkoutPrograms.Add(clone);
        await db.SaveChangesAsync(ct);

        return Results.Json(
            new ProgramListItem(
                clone.Id, clone.Name, clone.SplitLabel, false,
                clone.Groups.Count, clone.Groups.Sum(g => g.Variants.Count), clone.CreatedAt),
            statusCode: StatusCodes.Status201Created);
    }

    private static WorkoutGroup CloneGroup(WorkoutGroup source) => new()
    {
        Id = Guid.NewGuid(),
        Name = source.Name,
        SortOrder = source.SortOrder,
        Variants = source.Variants.OrderBy(v => v.SortOrder).Select(CloneVariant).ToList(),
    };

    private static WorkoutVariant CloneVariant(WorkoutVariant source)
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

        return new WorkoutVariant
        {
            Id = Guid.NewGuid(),
            Name = source.Name,
            SortOrder = source.SortOrder,
            EstimatedDurationMinutes = source.EstimatedDurationMinutes,
            Supersets = supersetByOldId.Values.ToList(),
            Exercises = source.Exercises
                .OrderBy(e => e.SortOrder)
                .Select(e => new VariantExercise
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

    private static async Task<IResult> AddGroupAsync(
        Guid id,
        CreateGroupRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var program = await db.OwnedPrograms(userId)
            .Include(p => p.Groups)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null)
        {
            return Results.NotFound();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
        {
            return Validation("name", "Enter a group name (1–80 characters).");
        }

        if (program.Groups.Count >= WorkoutLimits.MaxGroupsPerProgram)
        {
            return TooMany($"A program can have at most {WorkoutLimits.MaxGroupsPerProgram} groups.");
        }

        var groupEntity = new WorkoutGroup
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Name = name,
            SortOrder = program.Groups.Count == 0 ? 0 : program.Groups.Max(g => g.SortOrder) + 1,
        };
        db.WorkoutGroups.Add(groupEntity);
        await db.SaveChangesAsync(ct);

        return Results.Json(
            new GroupDetail(groupEntity.Id, groupEntity.Name, groupEntity.SortOrder, []),
            statusCode: StatusCodes.Status201Created);
    }

    // --- helpers shared with the group/variant endpoints ---

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
