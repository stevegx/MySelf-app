using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Nutrition;

/// <summary>
/// Shared helpers for the user-defined meal slots (docs/08 #19). Every category-aware
/// endpoint calls <see cref="EnsureSeededAsync"/> first so a user who predates this feature
/// (or a brand-new account) transparently gets the four default slots on first use.
/// </summary>
public static class MealCategoryService
{
    /// <summary>Insert the four default slots if this user has none yet (active or archived).</summary>
    public static async Task EnsureSeededAsync(
        MySelfDbContext db, Guid userId, TimeProvider clock, CancellationToken ct)
    {
        if (await db.MealCategories.AnyAsync(c => c.UserId == userId, ct))
        {
            return;
        }

        var now = clock.GetUtcNow();
        for (var i = 0; i < MealCategory.Defaults.Length; i++)
        {
            db.MealCategories.Add(new MealCategory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = MealCategory.Defaults[i],
                SortOrder = i,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>The user's active slot names, in display order. Seeds first.</summary>
    public static async Task<List<string>> ActiveNamesAsync(
        MySelfDbContext db, Guid userId, TimeProvider clock, CancellationToken ct)
    {
        await EnsureSeededAsync(db, userId, clock, ct);
        return await db.MealCategories
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.ArchivedAt == null)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Name)
            .ToListAsync(ct);
    }

    /// <summary>
    /// The canonical active name matching <paramref name="raw"/> case-insensitively, or null
    /// if the user has no active slot with that name. Seeds first.
    /// </summary>
    public static async Task<string?> ResolveAsync(
        MySelfDbContext db, Guid userId, TimeProvider clock, string? raw, CancellationToken ct)
    {
        var names = await ActiveNamesAsync(db, userId, clock, ct);
        return names.FirstOrDefault(n => string.Equals(n, raw, StringComparison.OrdinalIgnoreCase));
    }
}
