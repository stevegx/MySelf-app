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
        var programs = app.MapGroup("/api/v1/programs").RequireAuthorization();

        programs.MapGet("", ListAsync).WithName("ListPrograms");
        programs.MapPost("", CreateAsync).WithName("CreateProgram");
        programs.MapGet("/{id:guid}", GetAsync).WithName("GetProgram");
        programs.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateProgram");
        programs.MapDelete("/{id:guid}", ArchiveAsync).WithName("ArchiveProgram");
        programs.MapPost("/{id:guid}/activate", ActivateAsync).WithName("ActivateProgram");
        programs.MapPost("/{id:guid}/groups", AddGroupAsync).WithName("AddWorkoutGroup");

        return app;
    }

    private static async Task<IResult> ListAsync(HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var items = await db.WorkoutPrograms
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.ArchivedAt == null)
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

        var programCount = await db.WorkoutPrograms
            .CountAsync(p => p.UserId == userId && p.ArchivedAt == null, ct);
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

        var program = await db.WorkoutPrograms
            .AsNoTracking()
            .Where(p => p.Id == id && p.UserId == userId && p.ArchivedAt == null)
            .Select(p => new ProgramDetail(
                p.Id,
                p.Name,
                p.SplitLabel,
                p.IsActive,
                p.CreatedAt,
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

        var program = await db.WorkoutPrograms
            .Include(p => p.Groups)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

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

        var program = await db.WorkoutPrograms.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
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

        var target = await db.WorkoutPrograms.FirstOrDefaultAsync(
            p => p.Id == id && p.UserId == userId && p.ArchivedAt == null, ct);
        if (target is null)
        {
            return Results.NotFound();
        }

        // The DB has a filtered unique index (one active program per user), and it is checked
        // per-statement — so we must never have two active rows even mid-operation. Deactivate
        // the old one first with its own UPDATE, then activate the target, both inside one
        // explicit transaction so a crash between them can't leave the user with none.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.WorkoutPrograms
            .Where(p => p.UserId == userId && p.IsActive && p.Id != target.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), ct);

        target.IsActive = true;
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return Results.NoContent();
    }

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

        var program = await db.WorkoutPrograms
            .Include(p => p.Groups)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
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

    internal static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

    internal static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Validation failed");

    /// <summary>A size cap was hit — the request is well-formed, the resource state won't allow it (409).</summary>
    internal static IResult TooMany(string message) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Limit reached", detail: message);

    internal static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
