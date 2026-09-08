using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Nutrition;

public sealed record MealCategoryResponse(Guid Id, string Name, int SortOrder);

public sealed record UpsertMealCategoryRequest(string Name);

public sealed record ReorderMealCategoriesRequest(IReadOnlyList<Guid> Ids);

/// <summary>
/// The user-defined meal slots (docs/08 #19). Seeded lazily with Breakfast/Lunch/Dinner/
/// Snacks on first use. Renaming or archiving a slot never touches days already logged —
/// <see cref="MealLog"/> stores the category name by value.
/// </summary>
public static class MealCategoryEndpoints
{
    private const int MaxCategories = 12;

    public static IEndpointRouteBuilder MapMealCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/meal-categories").RequireAuthorization();

        group.MapGet("", ListAsync).WithName("ListMealCategories");
        group.MapPost("", CreateAsync).WithName("CreateMealCategory").RequireRateLimiting(RateLimiting.WritePolicy);
        group.MapPut("/reorder", ReorderAsync).WithName("ReorderMealCategories").RequireRateLimiting(RateLimiting.WritePolicy);
        group.MapPut("/{id:guid}", RenameAsync).WithName("RenameMealCategory").RequireRateLimiting(RateLimiting.WritePolicy);
        group.MapDelete("/{id:guid}", ArchiveAsync).WithName("ArchiveMealCategory").RequireRateLimiting(RateLimiting.WritePolicy);

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await MealCategoryService.EnsureSeededAsync(db, userId, clock, ct);
        var rows = await db.MealCategories
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.ArchivedAt == null)
            .OrderBy(c => c.SortOrder)
            .Select(c => new MealCategoryResponse(c.Id, c.Name, c.SortOrder))
            .ToListAsync(ct);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateAsync(
        UpsertMealCategoryRequest request, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40)
        {
            return Validation("name", "Enter a category name (1–40 characters).");
        }

        await MealCategoryService.EnsureSeededAsync(db, userId, clock, ct);
        var active = await db.MealCategories
            .Where(c => c.UserId == userId && c.ArchivedAt == null)
            .ToListAsync(ct);

        if (active.Count >= MaxCategories)
        {
            return Validation("name", $"You can have at most {MaxCategories} meal categories.");
        }
        if (active.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Validation("name", "You already have a category with that name.");
        }

        var row = new MealCategory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            SortOrder = active.Count == 0 ? 0 : active.Max(c => c.SortOrder) + 1,
            CreatedAt = clock.GetUtcNow(),
        };
        db.MealCategories.Add(row);
        await db.SaveChangesAsync(ct);

        return Results.Json(new MealCategoryResponse(row.Id, row.Name, row.SortOrder),
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> RenameAsync(
        Guid id, UpsertMealCategoryRequest request, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40)
        {
            return Validation("name", "Enter a category name (1–40 characters).");
        }

        var row = await db.MealCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.ArchivedAt == null, ct);
        if (row is null)
        {
            return Results.NotFound();
        }

        var clashes = await db.MealCategories.AnyAsync(
            c => c.UserId == userId && c.ArchivedAt == null && c.Id != id && c.Name.ToLower() == name.ToLower(), ct);
        if (clashes)
        {
            return Validation("name", "You already have a category with that name.");
        }

        row.Name = name;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new MealCategoryResponse(row.Id, row.Name, row.SortOrder));
    }

    private static async Task<IResult> ReorderAsync(
        ReorderMealCategoriesRequest request, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var rows = await db.MealCategories
            .Where(c => c.UserId == userId && c.ArchivedAt == null)
            .ToListAsync(ct);

        var ids = request.Ids ?? [];
        if (ids.Count != rows.Count || ids.Distinct().Count() != rows.Count || ids.Any(i => rows.All(r => r.Id != i)))
        {
            return Validation("ids", "Send every active category id exactly once.");
        }

        for (var i = 0; i < ids.Count; i++)
        {
            rows.First(r => r.Id == ids[i]).SortOrder = i;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(rows.OrderBy(r => r.SortOrder)
            .Select(r => new MealCategoryResponse(r.Id, r.Name, r.SortOrder)).ToList());
    }

    private static async Task<IResult> ArchiveAsync(
        Guid id, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var active = await db.MealCategories
            .Where(c => c.UserId == userId && c.ArchivedAt == null)
            .ToListAsync(ct);

        var row = active.FirstOrDefault(c => c.Id == id);
        if (row is null)
        {
            return Results.NotFound();
        }
        if (active.Count <= 1)
        {
            return Validation("id", "Keep at least one meal category.");
        }

        row.ArchivedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Validation failed");
}
