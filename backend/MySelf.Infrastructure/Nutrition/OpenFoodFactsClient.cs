using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MySelf.Infrastructure.Nutrition;

/// <summary>Typed <see cref="HttpClient"/> for the Open Food Facts product API (no key needed).</summary>
public sealed class OpenFoodFactsClient(HttpClient http)
{
    private const string Fields =
        "code,product_name,brands,quantity,serving_size,serving_quantity,last_modified_t,nutriments";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Fetches a product by barcode. Returns the parsed response and the raw JSON body.
    /// A 404 from the API is normalised to <c>Status = 0</c> rather than an exception.
    /// Throws <see cref="HttpRequestException"/> / <see cref="TaskCanceledException"/> for
    /// transport failures and 5xx.
    /// </summary>
    public async Task<OffFetch> GetProductAsync(string barcode, CancellationToken ct)
    {
        var path = $"api/v2/product/{Uri.EscapeDataString(barcode)}.json?fields={Fields}";
        using var response = await http.GetAsync(path, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new OffFetch(new OffResponse(barcode, 0, "product not found", null), string.Empty);
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<OffResponse>(body, Json);
        return new OffFetch(parsed, body);
    }
}

public sealed record OffFetch(OffResponse? Response, string RawJson);

// --- wire shapes (deserialised with JsonNamingPolicy.SnakeCaseLower) ---

public sealed record OffResponse(string? Code, int Status, string? StatusVerbose, OffProduct? Product);

public sealed record OffProduct(
    string? ProductName,
    string? Brands,
    string? Quantity,
    string? ServingSize,
    [property: JsonPropertyName("serving_quantity")] JsonElement ServingQuantity,
    long? LastModifiedT,
    Dictionary<string, JsonElement>? Nutriments);
