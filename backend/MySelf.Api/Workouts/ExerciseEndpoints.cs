using Microsoft.EntityFrameworkCore;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Workouts;

/// <summary>
/// Read-only access to the seeded exercise catalogue (docs/04 §12). The builder searches
/// this to add exercises to a variant. Custom (user-created) exercises are a later addition.
/// </summary>
public static class ExerciseEndpoints
{
    private const int MaxPageSize = 50;

    public static IEndpointRouteBuilder MapExerciseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/exercises").RequireAuthorization();

        group.MapGet("", SearchAsync).WithName("SearchExercises");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetExercise");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        MySelfDbContext db,
        CancellationToken ct,
        string? q = null,
        int? categoryId = null,
        int page = 1,
        int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Exercises.AsNoTracking().Include(e => e.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            // ILIKE — case-insensitive contains. Backed by IX_exercises_Name for the common
            // prefix case; a leading-wildcard match still scans, which is acceptable at this
            // catalogue size (~900 rows).
            var term = $"%{q.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.Name, term));
        }

        if (categoryId is { } cat)
        {
            query = query.Where(e => e.CategoryId == cat);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(e => e.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ExerciseListItem(e.Id, e.Name, e.Category.Name, e.DefaultTrackingMode.ToString()))
            .ToListAsync(ct);

        return Results.Ok(new ExerciseSearchResult(items, page, pageSize, total));
    }

    private static async Task<IResult> GetAsync(Guid id, MySelfDbContext db, CancellationToken ct)
    {
        var exercise = await db.Exercises
            .AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.Id == id)
            .Select(e => new ExerciseListItem(e.Id, e.Name, e.Category.Name, e.DefaultTrackingMode.ToString()))
            .FirstOrDefaultAsync(ct);

        return exercise is null ? Results.NotFound() : Results.Ok(exercise);
    }
}
