using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Nutrition;

/// <summary>
/// Multi-select copy / move / duplicate / delete for a day's logged items (docs/08 Story 7:
/// "select multiple items and copy, move, duplicate or delete them safely"). Copy always
/// creates independent ids; move keeps identity and is transactional (one SaveChanges);
/// delete is undone client-side by replaying the snapshots through <c>bulk-add</c>.
/// </summary>
public static class MealItemBulkEndpoints
{
    public static IEndpointRouteBuilder MapMealItemBulkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/nutrition-days/{date}/items")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        group.MapPost("/bulk-delete", DeleteAsync).WithName("BulkDeleteMealItems");
        group.MapPost("/bulk-move", MoveAsync).WithName("BulkMoveMealItems");
        group.MapPost("/bulk-copy", CopyAsync).WithName("BulkCopyMealItems");
        group.MapPost("/bulk-add", AddAsync).WithName("BulkAddMealItems");

        return app;
    }

    private static async Task<IResult> DeleteAsync(
        string date, BulkDeleteItemsRequest request, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }
        if (!TryParseDate(date, out var day))
        {
            return BadDate();
        }

        var (items, error) = await LoadPickedAsync(db, userId, day, request.Ids, ct);
        if (error is not null)
        {
            return error;
        }

        var removedIds = items.Select(i => i.Id).ToHashSet();
        db.MealLogItems.RemoveRange(items);
        DropEmptiedLogs(db, items, removedIds, keepLogId: null);

        await db.SaveChangesAsync(ct);
        return Results.Ok(await NutritionDayEndpoints.BuildDayAsync(db, userId, day, clock, ct));
    }

    private static async Task<IResult> MoveAsync(
        string date, BulkMoveItemsRequest request, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }
        if (!TryParseDate(date, out var day))
        {
            return BadDate();
        }

        var toCategory = await MealCategoryService.ResolveAsync(db, userId, clock, request.ToCategory, ct);
        if (toCategory is null)
        {
            return await BadCategory(db, userId, clock, ct);
        }

        var (items, error) = await LoadPickedAsync(db, userId, day, request.Ids, ct);
        if (error is not null)
        {
            return error;
        }

        var target = await GetOrCreateLogAsync(db, userId, day, toCategory, clock, ct);
        var nextSort = target.Items.Count == 0 ? 0 : target.Items.Max(i => i.SortOrder) + 1;

        foreach (var item in items.OrderBy(i => i.SortOrder))
        {
            item.MealLogId = target.Id;
            item.SortOrder = nextSort++;
        }

        DropEmptiedLogs(db, items, items.Select(i => i.Id).ToHashSet(), keepLogId: target.Id);

        await db.SaveChangesAsync(ct);
        return Results.Ok(await NutritionDayEndpoints.BuildDayAsync(db, userId, day, clock, ct));
    }

    private static async Task<IResult> CopyAsync(
        string date, BulkCopyItemsRequest request, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }
        if (!TryParseDate(date, out var day))
        {
            return BadDate();
        }

        var toDay = day;
        if (request.ToDate is not null && !TryParseDate(request.ToDate, out toDay))
        {
            return BadDate();
        }

        var toCategory = await MealCategoryService.ResolveAsync(db, userId, clock, request.ToCategory, ct);
        if (toCategory is null)
        {
            return await BadCategory(db, userId, clock, ct);
        }

        var (items, error) = await LoadPickedAsync(db, userId, day, request.Ids, ct);
        if (error is not null)
        {
            return error;
        }

        var target = await GetOrCreateLogAsync(db, userId, toDay, toCategory, clock, ct);
        var nextSort = target.Items.Count == 0 ? 0 : target.Items.Max(i => i.SortOrder) + 1;
        var now = clock.GetUtcNow();

        foreach (var src in items.OrderBy(i => i.SortOrder))
        {
            db.MealLogItems.Add(new MealLogItem
            {
                Id = Guid.NewGuid(),
                MealLog = target,
                SortOrder = nextSort++,
                Name = src.Name,
                ServingBasis = src.ServingBasis,
                ServingSizeGrams = src.ServingSizeGrams,
                BasisKcal = src.BasisKcal,
                BasisProteinG = src.BasisProteinG,
                BasisCarbG = src.BasisCarbG,
                BasisFatG = src.BasisFatG,
                Amount = src.Amount,
                Unit = src.Unit,
                Kcal = src.Kcal,
                ProteinG = src.ProteinG,
                CarbG = src.CarbG,
                FatG = src.FatG,
                LoggedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        // Always report the day named in the route; a cross-date copy leaves it unchanged and
        // the client refreshes the target day separately.
        return Results.Ok(await NutritionDayEndpoints.BuildDayAsync(db, userId, day, clock, ct));
    }

    private static async Task<IResult> AddAsync(
        string date, BulkAddItemsRequest request, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }
        if (!TryParseDate(date, out var day))
        {
            return BadDate();
        }
        if (request.Items is not { Count: > 0 })
        {
            return Validation("items", "Send at least one item to restore.");
        }

        var now = clock.GetUtcNow();
        var logByCategory = new Dictionary<string, MealLog>(StringComparer.Ordinal);

        foreach (var input in request.Items)
        {
            if (string.IsNullOrWhiteSpace(input.Category) || input.Category.Length > 40)
            {
                return Validation("items", "Each restored item needs a category.");
            }
            if (!Enum.TryParse<ServingBasis>(input.ServingBasis, ignoreCase: true, out var basis))
            {
                return Validation("items", "servingBasis must be Per100g or PerServing.");
            }
            if (!Enum.TryParse<MealAmountUnit>(input.Unit, ignoreCase: true, out var unit))
            {
                return Validation("items", "unit must be Grams, Millilitres or Serving.");
            }

            if (!logByCategory.TryGetValue(input.Category, out var log))
            {
                // Restore is faithful, not validated: an undo may land in a category the user
                // has since renamed/archived. That's fine — the day shows it as an orphan slot.
                log = await GetOrCreateLogAsync(db, userId, day, input.Category, clock, ct);
                logByCategory[input.Category] = log;
            }

            var nextSort = log.Items.Count == 0 ? 0 : log.Items.Max(i => i.SortOrder) + 1;
            var restored = new MealLogItem
            {
                Id = Guid.NewGuid(),
                MealLog = log,
                SortOrder = nextSort,
                Name = input.Name.Trim(),
                ServingBasis = basis,
                ServingSizeGrams = input.ServingSizeGrams,
                BasisKcal = input.BasisKcal,
                BasisProteinG = input.BasisProteinG,
                BasisCarbG = input.BasisCarbG,
                BasisFatG = input.BasisFatG,
                Amount = input.Amount,
                Unit = unit,
                Kcal = input.Kcal,
                ProteinG = input.ProteinG,
                CarbG = input.CarbG,
                FatG = input.FatG,
                LoggedAt = now,
            };
            log.Items.Add(restored);
            db.MealLogItems.Add(restored);
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await NutritionDayEndpoints.BuildDayAsync(db, userId, day, clock, ct));
    }

    // --- helpers ---

    /// <summary>Load the caller's own items for the given ids on the given day; error result
    /// if any id is missing / not theirs / not on that day.</summary>
    private static async Task<(List<MealLogItem> Items, IResult? Error)> LoadPickedAsync(
        MySelfDbContext db, Guid userId, DateOnly day, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        var wanted = (ids ?? []).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return ([], Validation("ids", "Choose at least one item."));
        }

        var items = await db.MealLogItems
            .Include(i => i.MealLog).ThenInclude(m => m.Items)
            .Where(i => wanted.Contains(i.Id) && i.MealLog.UserId == userId && i.MealLog.LocalDate == day)
            .ToListAsync(ct);

        return items.Count != wanted.Count
            ? ([], Validation("ids", "One or more items are not on this day."))
            : (items, null);
    }

    private static async Task<MealLog> GetOrCreateLogAsync(
        MySelfDbContext db, Guid userId, DateOnly day, string category, TimeProvider clock, CancellationToken ct)
    {
        var log = await db.MealLogs
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.LocalDate == day && m.Category == category, ct);
        if (log is not null)
        {
            return log;
        }

        log = new MealLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LocalDate = day,
            Category = category,
            CreatedAt = clock.GetUtcNow(),
        };
        db.MealLogs.Add(log);
        return log;
    }

    /// <summary>Remove any source log (except <paramref name="keepLogId"/>) whose every item
    /// is in <paramref name="removedIds"/> — i.e. nothing is left in that slot.</summary>
    private static void DropEmptiedLogs(
        MySelfDbContext db, IEnumerable<MealLogItem> movedOrRemoved, HashSet<Guid> removedIds, Guid? keepLogId)
    {
        var affected = movedOrRemoved
            .Select(i => i.MealLog)
            .DistinctBy(m => m.Id);

        foreach (var log in affected)
        {
            if (log.Id != keepLogId && log.Items.All(it => removedIds.Contains(it.Id)))
            {
                db.MealLogs.Remove(log);
            }
        }
    }

    private static async Task<IResult> BadCategory(
        MySelfDbContext db, Guid userId, TimeProvider clock, CancellationToken ct)
    {
        var names = await MealCategoryService.ActiveNamesAsync(db, userId, clock, ct);
        return Validation("toCategory", $"Category must be one of: {string.Join(", ", names)}.");
    }

    private static bool TryParseDate(string raw, out DateOnly date) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", out date);

    private static IResult BadDate() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest, title: "Invalid date", detail: "Use YYYY-MM-DD.");

    private static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Validation failed");
}
