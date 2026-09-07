using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Nutrition;

/// <summary>
/// Logging food into a day and reading the day back (docs/03 §8.7 "Create food manually",
/// docs/04 §12 Nutrition). The MVP has four fixed meal slots; a <see cref="MealLog"/> row is
/// created lazily the first time a food lands in a slot and removed when its last item goes.
/// Everything a <see cref="MealLogItem"/> stores is a snapshot — editing the source food
/// later never rewrites history.
/// </summary>
public static class NutritionDayEndpoints
{
    private static readonly string[] Categories = ["Breakfast", "Lunch", "Dinner", "Snacks"];

    public static IEndpointRouteBuilder MapNutritionDayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/nutrition-days/{date}", GetDayAsync)
            .WithName("GetNutritionDay").RequireAuthorization();

        app.MapPost("/api/v1/nutrition-days/{date}/items", AddItemAsync)
            .WithName("AddMealItem").RequireAuthorization().RequireRateLimiting(RateLimiting.WritePolicy);

        app.MapPut("/api/v1/meal-log-items/{id:guid}", UpdateItemAsync)
            .WithName("UpdateMealItem").RequireAuthorization().RequireRateLimiting(RateLimiting.WritePolicy);

        app.MapDelete("/api/v1/meal-log-items/{id:guid}", DeleteItemAsync)
            .WithName("DeleteMealItem").RequireAuthorization().RequireRateLimiting(RateLimiting.WritePolicy);

        return app;
    }

    private static async Task<IResult> GetDayAsync(string date, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }
        if (!TryParseDate(date, out var day))
        {
            return BadDate();
        }

        return Results.Ok(await BuildDayAsync(db, userId, day, ct));
    }

    private static async Task<IResult> AddItemAsync(
        string date,
        AddMealItemRequest request,
        HttpContext http,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }
        if (!TryParseDate(date, out var day))
        {
            return BadDate();
        }

        var category = Categories.FirstOrDefault(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase));
        if (category is null)
        {
            return Validation("category", $"Category must be one of: {string.Join(", ", Categories)}.");
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160)
        {
            return Validation("name", "Enter a food name (1–160 characters).");
        }

        if (!Enum.TryParse<ServingBasis>(request.ServingBasis, ignoreCase: true, out var basis))
        {
            return Validation("servingBasis", "servingBasis must be Per100g or PerServing.");
        }
        if (!Enum.TryParse<MealAmountUnit>(request.Unit, ignoreCase: true, out var unit))
        {
            return Validation("unit", "unit must be Grams, Millilitres or Serving.");
        }
        if (request.Amount <= 0)
        {
            return Validation("amount", "Amount must be greater than zero.");
        }
        if (request.PerBasisKcal < 0 || request.PerBasisProteinG < 0 || request.PerBasisCarbG < 0 || request.PerBasisFatG < 0)
        {
            return Validation("perBasisKcal", "Nutrient values cannot be negative.");
        }

        var perBasis = new MealNutrientCalculator.Nutrients(
            request.PerBasisKcal, request.PerBasisProteinG, request.PerBasisCarbG, request.PerBasisFatG);

        MealNutrientCalculator.Nutrients scaled;
        try
        {
            scaled = MealNutrientCalculator.ForAmount(perBasis, basis, unit, request.Amount, request.ServingSizeGrams);
        }
        catch (InvalidOperationException ex)
        {
            return Validation("servingSizeGrams", ex.Message);
        }

        var mealLog = await db.MealLogs
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.LocalDate == day && m.Category == category, ct);

        var now = clock.GetUtcNow();
        if (mealLog is null)
        {
            mealLog = new MealLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LocalDate = day,
                Category = category,
                CreatedAt = now,
            };
            db.MealLogs.Add(mealLog);
        }

        // db.Add forces the whole subgraph to Added state; adding to the tracked collection
        // with the FK already set can otherwise be mistaken for a Modified existing row.
        db.MealLogItems.Add(new MealLogItem
        {
            Id = Guid.NewGuid(),
            MealLog = mealLog,
            SortOrder = mealLog.Items.Count == 0 ? 0 : mealLog.Items.Max(i => i.SortOrder) + 1,
            Name = name,
            ServingBasis = basis,
            ServingSizeGrams = request.ServingSizeGrams,
            BasisKcal = request.PerBasisKcal,
            BasisProteinG = request.PerBasisProteinG,
            BasisCarbG = request.PerBasisCarbG,
            BasisFatG = request.PerBasisFatG,
            Amount = request.Amount,
            Unit = unit,
            Kcal = scaled.Kcal,
            ProteinG = scaled.ProteinG,
            CarbG = scaled.CarbG,
            FatG = scaled.FatG,
            LoggedAt = now,
        });

        await db.SaveChangesAsync(ct);
        return Results.Ok(await BuildDayAsync(db, userId, day, ct));
    }

    private static async Task<IResult> UpdateItemAsync(
        Guid id,
        UpdateMealItemRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var item = await db.MealLogItems
            .Include(i => i.MealLog)
            .FirstOrDefaultAsync(i => i.Id == id && i.MealLog.UserId == userId, ct);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (!Enum.TryParse<MealAmountUnit>(request.Unit, ignoreCase: true, out var unit))
        {
            return Validation("unit", "unit must be Grams, Millilitres or Serving.");
        }
        if (request.Amount <= 0)
        {
            return Validation("amount", "Amount must be greater than zero.");
        }

        var servingSize = request.ServingSizeGrams ?? item.ServingSizeGrams;
        var perBasis = new MealNutrientCalculator.Nutrients(
            item.BasisKcal, item.BasisProteinG, item.BasisCarbG, item.BasisFatG);

        MealNutrientCalculator.Nutrients scaled;
        try
        {
            scaled = MealNutrientCalculator.ForAmount(perBasis, item.ServingBasis, unit, request.Amount, servingSize);
        }
        catch (InvalidOperationException ex)
        {
            return Validation("servingSizeGrams", ex.Message);
        }

        item.Amount = request.Amount;
        item.Unit = unit;
        item.ServingSizeGrams = servingSize;
        item.Kcal = scaled.Kcal;
        item.ProteinG = scaled.ProteinG;
        item.CarbG = scaled.CarbG;
        item.FatG = scaled.FatG;

        await db.SaveChangesAsync(ct);
        return Results.Ok(await BuildDayAsync(db, userId, item.MealLog.LocalDate, ct));
    }

    private static async Task<IResult> DeleteItemAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var item = await db.MealLogItems
            .Include(i => i.MealLog).ThenInclude(m => m.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.MealLog.UserId == userId, ct);
        if (item is null)
        {
            return Results.NotFound();
        }

        db.MealLogItems.Remove(item);
        if (item.MealLog.Items.Count == 1) // the one we're removing
        {
            db.MealLogs.Remove(item.MealLog);
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // --- helpers ---

    private static async Task<NutritionDayResponse> BuildDayAsync(
        MySelfDbContext db, Guid userId, DateOnly day, CancellationToken ct)
    {
        var logs = await db.MealLogs
            .AsNoTracking()
            .Include(m => m.Items)
            .Where(m => m.UserId == userId && m.LocalDate == day)
            .ToListAsync(ct);

        var goal = await db.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        var meals = Categories.Select(category =>
        {
            var log = logs.FirstOrDefault(l => l.Category == category);
            var items = (log?.Items ?? [])
                .OrderBy(i => i.SortOrder)
                .Select(i => new MealItemResponse(
                    i.Id, i.SortOrder, i.Name, i.ServingBasis.ToString(), i.ServingSizeGrams, i.Amount,
                    i.Unit.ToString(), i.Kcal, i.ProteinG, i.CarbG, i.FatG))
                .ToList();

            var subtotal = new NutrientTotals(
                items.Sum(i => i.Kcal), items.Sum(i => i.ProteinG), items.Sum(i => i.CarbG), items.Sum(i => i.FatG));

            return new MealResponse(log?.Id, category, subtotal, items);
        }).ToList();

        var totals = new NutrientTotals(
            meals.Sum(m => m.Subtotals.Kcal),
            meals.Sum(m => m.Subtotals.ProteinG),
            meals.Sum(m => m.Subtotals.CarbG),
            meals.Sum(m => m.Subtotals.FatG));

        return new NutritionDayResponse(
            day,
            new NutrientTargets(goal?.CalorieTarget, goal?.ProteinGrams, goal?.CarbGrams, goal?.FatGrams),
            totals,
            meals);
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
