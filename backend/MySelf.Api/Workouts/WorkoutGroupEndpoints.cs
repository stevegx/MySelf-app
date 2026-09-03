using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Persistence;
using static MySelf.Api.Workouts.ProgramEndpoints;

namespace MySelf.Api.Workouts;

/// <summary>
/// Editing a single group: rename, reorder its variants, delete it, or add a variant.
/// Ownership is checked through the group's program (<c>g.Program.UserId == userId</c>).
/// </summary>
public static class WorkoutGroupEndpoints
{
    public static IEndpointRouteBuilder MapWorkoutGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = app.MapGroup("/api/v1/workout-groups")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        groups.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateWorkoutGroup");
        groups.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteWorkoutGroup");
        groups.MapPost("/{id:guid}/variants", AddVariantAsync).WithName("AddWorkoutVariant");

        return app;
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateGroupRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var group = await db.WorkoutGroups
            .Include(g => g.Variants)
            .FirstOrDefaultAsync(g => g.Id == id && g.Program.UserId == userId, ct);
        if (group is null)
        {
            return Results.NotFound();
        }

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
            {
                return Validation("name", "Enter a group name (1–80 characters).");
            }

            group.Name = name;
        }

        if (request.VariantOrder is { Count: > 0 } order)
        {
            ApplyOrder(group.Variants, v => v.Id, order, (v, i) => v.SortOrder = i);
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var group = await db.WorkoutGroups.FirstOrDefaultAsync(g => g.Id == id && g.Program.UserId == userId, ct);
        if (group is null)
        {
            return Results.NotFound();
        }

        // Hard delete: builder content only, no session history yet (that guard is Phase 3).
        db.WorkoutGroups.Remove(group);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddVariantAsync(
        Guid id,
        CreateVariantRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var group = await db.WorkoutGroups
            .Include(g => g.Variants)
            .FirstOrDefaultAsync(g => g.Id == id && g.Program.UserId == userId, ct);
        if (group is null)
        {
            return Results.NotFound();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
        {
            return Validation("name", "Enter a variant name (1–80 characters).");
        }

        if (group.Variants.Count >= WorkoutLimits.MaxVariantsPerGroup)
        {
            return TooMany($"A group can have at most {WorkoutLimits.MaxVariantsPerGroup} variants.");
        }

        var variant = new WorkoutVariant
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            Name = name,
            SortOrder = group.Variants.Count == 0 ? 0 : group.Variants.Max(v => v.SortOrder) + 1,
        };
        db.WorkoutVariants.Add(variant);
        await db.SaveChangesAsync(ct);

        return Results.Json(
            new VariantListItem(variant.Id, variant.Name, variant.SortOrder, 0),
            statusCode: StatusCodes.Status201Created);
    }
}
