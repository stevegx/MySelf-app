using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Persistence;
using N = MySelf.Domain.Nutrition.MealNutrientCalculator.Nutrients;

namespace MySelf.Api.Nutrition;

/// <summary>
/// Reusable meals (docs/03 §8.8, docs/04 §12 "SavedMeal"). A saved meal is a template of
/// foods with default amounts; adding it to a day copies each item into an independent
/// <see cref="MealLogItem"/> snapshot (optionally scaled by a multiplier), so later edits to
/// the template never change days already logged from it.
/// </summary>
public static class SavedMealEndpoints
{
    private static readonly string[] Categories = ["Breakfast", "Lunch", "Dinner", "Snacks"];

    public static IEndpointRouteBuilder MapSavedMealEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/saved-meals").RequireAuthorization();

        group.MapGet("", ListAsync).WithName("ListSavedMeals");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetSavedMeal");
        group.MapPost("", CreateAsync).WithName("CreateSavedMeal").RequireRateLimiting(RateLimiting.WritePolicy);
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateSavedMeal").RequireRateLimiting(RateLimiting.WritePolicy);
        group.MapDelete("/{id:guid}", ArchiveAsync).WithName("ArchiveSavedMeal").RequireRateLimiting(RateLimiting.WritePolicy);
        group.MapPost("/{id:guid}/add-to-day", AddToDayAsync).WithName("AddSavedMealToDay").RequireRateLimiting(RateLimiting.WritePolicy);

        return app;
    }

    private static async Task<IResult> ListAsync(HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var meals = await db.SavedMeals
            .AsNoTracking()
            .Include(m => m.Items)
            .Where(m => m.UserId == userId && m.ArchivedAt == null)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

        return Results.Ok(meals.Select(ToResponse).ToList());
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var meal = await db.SavedMeals
            .AsNoTracking()
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId && m.ArchivedAt == null, ct);

        return meal is null ? Results.NotFound() : Results.Ok(ToResponse(meal));
    }

    private static async Task<IResult> CreateAsync(
        UpsertSavedMealRequest request, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }
        if (Validate(request) is { } bad)
        {
            return bad;
        }

        var meal = new SavedMeal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Category = CanonicalCategory(request.Category)!,
            Notes = Trimmed(request.Notes),
            CreatedAt = clock.GetUtcNow(),
            Items = BuildItems(request.Items),
        };
        db.SavedMeals.Add(meal);
        await db.SaveChangesAsync(ct);

        return Results.Json(ToResponse(meal), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpsertSavedMealRequest request, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }
        if (Validate(request) is { } bad)
        {
            return bad;
        }

        var meal = await db.SavedMeals
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId && m.ArchivedAt == null, ct);
        if (meal is null)
        {
            return Results.NotFound();
        }

        meal.Name = request.Name.Trim();
        meal.Category = CanonicalCategory(request.Category)!;
        meal.Notes = Trimmed(request.Notes);

        // Replace the items wholesale: drop the old rows first, then add the new ones with an
        // explicit db.Add so EF tracks them as inserts (same pattern as the workout-day PUT).
        db.SavedMealItems.RemoveRange(meal.Items);
        await db.SaveChangesAsync(ct);

        var replacements = BuildItems(request.Items);
        replacements.ForEach(i => i.SavedMealId = meal.Id);
        db.SavedMealItems.AddRange(replacements);
        await db.SaveChangesAsync(ct);

        meal.Items = replacements;
        return Results.Ok(ToResponse(meal));
    }

    private static async Task<IResult> ArchiveAsync(
        Guid id, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var meal = await db.SavedMeals.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
        if (meal is null || meal.ArchivedAt is not null)
        {
            return Results.NotFound();
        }

        meal.ArchivedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddToDayAsync(
        Guid id, AddSavedMealToDayRequest request, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }
        if (!DateOnly.TryParseExact(request.Date, "yyyy-MM-dd", out var day))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid date", detail: "Use YYYY-MM-DD.");
        }

        var meal = await db.SavedMeals
            .AsNoTracking()
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId && m.ArchivedAt == null, ct);
        if (meal is null)
        {
            return Results.NotFound();
        }

        var category = request.Category is null ? meal.Category : CanonicalCategory(request.Category);
        if (category is null)
        {
            return Validation("category", $"Category must be one of: {string.Join(", ", Categories)}.");
        }

        var multiplier = request.Multiplier ?? 1m;
        if (multiplier is <= 0m or > 20m)
        {
            return Validation("multiplier", "Multiplier must be between 0 and 20.");
        }

        var mealLog = await db.MealLogs
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.LocalDate == day && m.Category == category, ct);

        var now = clock.GetUtcNow();
        if (mealLog is null)
        {
            mealLog = new MealLog { Id = Guid.NewGuid(), UserId = userId, LocalDate = day, Category = category, CreatedAt = now };
            db.MealLogs.Add(mealLog);
        }

        var nextSort = mealLog.Items.Count == 0 ? 0 : mealLog.Items.Max(i => i.SortOrder) + 1;
        foreach (var item in meal.Items.OrderBy(i => i.SortOrder))
        {
            var amount = item.DefaultAmount * multiplier;
            var scaled = MealNutrientCalculator.ForAmount(
                new N(item.BasisKcal, item.BasisProteinG, item.BasisCarbG, item.BasisFatG),
                item.ServingBasis, item.Unit, amount, item.ServingSizeGrams);

            db.MealLogItems.Add(new MealLogItem
            {
                Id = Guid.NewGuid(),
                MealLog = mealLog,
                SortOrder = nextSort++,
                Name = item.Name,
                ServingBasis = item.ServingBasis,
                ServingSizeGrams = item.ServingSizeGrams,
                BasisKcal = item.BasisKcal,
                BasisProteinG = item.BasisProteinG,
                BasisCarbG = item.BasisCarbG,
                BasisFatG = item.BasisFatG,
                Amount = amount,
                Unit = item.Unit,
                Kcal = scaled.Kcal,
                ProteinG = scaled.ProteinG,
                CarbG = scaled.CarbG,
                FatG = scaled.FatG,
                LoggedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await NutritionDayEndpoints.BuildDayAsync(db, userId, day, ct));
    }

    // --- helpers ---

    private static IResult? Validate(UpsertSavedMealRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return Validation("name", "Enter a meal name (1–120 characters).");
        }
        if (CanonicalCategory(request.Category) is null)
        {
            return Validation("category", $"Category must be one of: {string.Join(", ", Categories)}.");
        }
        if (request.Items is not { Count: > 0 })
        {
            return Validation("items", "A saved meal needs at least one food.");
        }
        foreach (var i in request.Items)
        {
            if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 160)
            {
                return Validation("items", "Every food needs a name (1–160 characters).");
            }
            if (!Enum.TryParse<ServingBasis>(i.ServingBasis, ignoreCase: true, out _))
            {
                return Validation("items", "servingBasis must be Per100g or PerServing.");
            }
            if (!Enum.TryParse<MealAmountUnit>(i.Unit, ignoreCase: true, out _))
            {
                return Validation("items", "unit must be Grams, Millilitres or Serving.");
            }
            if (i.DefaultAmount <= 0)
            {
                return Validation("items", "Each food's amount must be greater than zero.");
            }
            if (i.PerBasisKcal < 0 || i.PerBasisProteinG < 0 || i.PerBasisCarbG < 0 || i.PerBasisFatG < 0)
            {
                return Validation("items", "Nutrient values cannot be negative.");
            }
        }
        return null;
    }

    private static List<SavedMealItem> BuildItems(IReadOnlyList<SaveMealItemInput> inputs) =>
        inputs.Select((i, index) => new SavedMealItem
        {
            Id = Guid.NewGuid(),
            SortOrder = index,
            Name = i.Name.Trim(),
            ServingBasis = Enum.Parse<ServingBasis>(i.ServingBasis, ignoreCase: true),
            ServingSizeGrams = i.ServingSizeGrams,
            BasisKcal = i.PerBasisKcal,
            BasisProteinG = i.PerBasisProteinG,
            BasisCarbG = i.PerBasisCarbG,
            BasisFatG = i.PerBasisFatG,
            DefaultAmount = i.DefaultAmount,
            Unit = Enum.Parse<MealAmountUnit>(i.Unit, ignoreCase: true),
        }).ToList();

    private static SavedMealResponse ToResponse(SavedMeal m)
    {
        var items = m.Items.OrderBy(i => i.SortOrder).Select(i =>
        {
            var n = MealNutrientCalculator.ForAmount(
                new N(i.BasisKcal, i.BasisProteinG, i.BasisCarbG, i.BasisFatG),
                i.ServingBasis, i.Unit, i.DefaultAmount, i.ServingSizeGrams);
            return new SavedMealItemResponse(
                i.Id, i.SortOrder, i.Name, i.ServingBasis.ToString(), i.ServingSizeGrams,
                i.BasisKcal, i.BasisProteinG, i.BasisCarbG, i.BasisFatG,
                i.DefaultAmount, i.Unit.ToString(), n.Kcal, n.ProteinG, n.CarbG, n.FatG);
        }).ToList();

        var totals = MealNutrientCalculator.Total(items.Select(i => new N(i.Kcal, i.ProteinG, i.CarbG, i.FatG)));
        return new SavedMealResponse(
            m.Id, m.Name, m.Category, m.Notes,
            new NutrientTotals(totals.Kcal, totals.ProteinG, totals.CarbG, totals.FatG),
            items);
    }

    private static string? CanonicalCategory(string? raw) =>
        Categories.FirstOrDefault(c => string.Equals(c, raw, StringComparison.OrdinalIgnoreCase));

    private static string? Trimmed(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Validation failed");
}
