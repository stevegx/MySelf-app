using MySelf.Infrastructure.Nutrition;

namespace MySelf.Api.Nutrition;

public static class FoodsEndpoints
{
    public static IEndpointRouteBuilder MapFoodsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/foods");

        group.MapGet("/barcode/{code}", GetByBarcodeAsync)
            .WithName("GetFoodByBarcode")
            .WithSummary("Look up a packaged food by barcode via Open Food Facts (cached).");

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

    private static bool IsPlausibleBarcode(string code) =>
        code.Length is >= 8 and <= 14 && code.All(char.IsAsciiDigit);
}
