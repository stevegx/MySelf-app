using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Nutrition;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Nutrition;

/// <summary>
/// Food lookup for the meal-logging flow (docs/03 §8.7, docs/04 §12): the caller's saved
/// "My Foods" (search + create + archive) and packaged-food barcode lookup via Open Food
/// Facts. External generic text search is a later slice.
/// </summary>
public static class FoodsEndpoints
{
    public static IEndpointRouteBuilder MapFoodsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/foods");

        group.MapGet("/barcode/{code}", GetByBarcodeAsync)
            .WithName("GetFoodByBarcode")
            .WithSummary("Look up a packaged food by barcode via Open Food Facts (cached).")
            .RequireRateLimiting(RateLimiting.LookupPolicy);

        group.MapGet("/search", SearchMyFoodsAsync)
            .WithName("SearchMyFoods")
            .WithSummary("Search the caller's saved My Foods by name.")
            .RequireAuthorization();

        group.MapPost("/custom", CreateCustomFoodAsync)
            .WithName("CreateCustomFood")
            .WithSummary("Save a food to My Foods for reuse.")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        group.MapDelete("/custom/{id:guid}", ArchiveCustomFoodAsync)
            .WithName("ArchiveCustomFood")
            .WithSummary("Remove a saved food from the picker (soft delete).")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        return app;
    }

    private static async Task<IResult> GetByBarcodeAsync(
        string code,
        BarcodeLookupService lookup,
        CancellationToken ct)
    {
        if (!IsPlausibleBarcode(code))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid barcode",
                detail: "A barcode is 8 to 14 digits.");
        }

        var result = await lookup.LookupAsync(code, ct);

        return result.Outcome switch
        {
            LookupOutcome.Found => Results.Ok(
                BarcodeFoodResponse.From(result.Entry!, result.FromCache, result.Stale)),

            LookupOutcome.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product not found",
                detail: "No product for this barcode in Open Food Facts. It can be added manually."),

            _ => Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Food data source unavailable",
                detail: "Open Food Facts could not be reached. Please try again shortly."),
        };
    }

    private static async Task<IResult> SearchMyFoodsAsync(
        HttpContext http, MySelfDbContext db, CancellationToken ct, string? q = null)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var term = q?.Trim() ?? "";
        var query = db.CustomFoods
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.ArchivedAt == null);

        if (term.Length > 0)
        {
            query = query.Where(f => EF.Functions.ILike(f.Name, $"%{term}%")
                || (f.Brand != null && EF.Functions.ILike(f.Brand, $"%{term}%")));
        }

        var foods = await query
            .OrderBy(f => f.Name)
            .Take(50)
            .Select(f => CustomFoodResponse.From(f))
            .ToListAsync(ct);

        return Results.Ok(foods);
    }

    private static async Task<IResult> CreateCustomFoodAsync(
        CreateCustomFoodRequest request,
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
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160)
        {
            return Validation("name", "Enter a food name (1–160 characters).");
        }
        if (!Enum.TryParse<ServingBasis>(request.ServingBasis, ignoreCase: true, out var basis))
        {
            return Validation("servingBasis", "servingBasis must be Per100g or PerServing.");
        }
        if (request.Kcal < 0 || request.ProteinG < 0 || request.CarbG < 0 || request.FatG < 0)
        {
            return Validation("kcal", "Nutrient values cannot be negative.");
        }

        var food = new CustomFood
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Brand = Trimmed(request.Brand),
            Barcode = Trimmed(request.Barcode),
            ServingBasis = basis,
            ServingSizeGrams = request.ServingSizeGrams,
            Kcal = request.Kcal,
            ProteinG = request.ProteinG,
            CarbG = request.CarbG,
            FatG = request.FatG,
            CreatedAt = clock.GetUtcNow(),
        };
        db.CustomFoods.Add(food);
        await db.SaveChangesAsync(ct);

        return Results.Json(CustomFoodResponse.From(food), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ArchiveCustomFoodAsync(
        Guid id, HttpContext http, MySelfDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var food = await db.CustomFoods.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);
        if (food is null || food.ArchivedAt is not null)
        {
            return Results.NotFound();
        }

        food.ArchivedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static string? Trimmed(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool IsPlausibleBarcode(string code) =>
        code.Length is >= 8 and <= 14 && code.All(char.IsAsciiDigit);

    private static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, title: "Validation failed");
}
