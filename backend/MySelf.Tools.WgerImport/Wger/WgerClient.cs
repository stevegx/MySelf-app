using System.Net.Http.Json;
using System.Text.Json;

namespace MySelf.Tools.WgerImport.Wger;

/// <summary>Thin read-only client for the public wger REST API (no auth needed).</summary>
public sealed class WgerClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public const string BaseUrl = "https://wger.de/api/v2/";
    private const int EnglishLanguageId = 2;

    public Task<List<WgerRef>> GetCategoriesAsync(CancellationToken ct) =>
        GetAllPagesAsync<WgerRef>("exercisecategory/?limit=100", ct);

    public Task<List<WgerMuscle>> GetMusclesAsync(CancellationToken ct) =>
        GetAllPagesAsync<WgerMuscle>("muscle/?limit=100", ct);

    public Task<List<WgerRef>> GetEquipmentAsync(CancellationToken ct) =>
        GetAllPagesAsync<WgerRef>("equipment/?limit=100", ct);

    public Task<List<WgerExerciseInfo>> GetExercisesAsync(CancellationToken ct) =>
        GetAllPagesAsync<WgerExerciseInfo>(
            $"exerciseinfo/?format=json&language={EnglishLanguageId}&limit=100",
            ct);

    /// <summary>The "main" illustration per exercise base (~1/3 of exercises have one).</summary>
    public Task<List<WgerExerciseImage>> GetMainExerciseImagesAsync(CancellationToken ct) =>
        GetAllPagesAsync<WgerExerciseImage>("exerciseimage/?format=json&is_main=true&limit=100", ct);

    private async Task<List<T>> GetAllPagesAsync<T>(string relativeUrl, CancellationToken ct)
    {
        var all = new List<T>();
        var next = relativeUrl;

        while (next is not null)
        {
            var page = await http.GetFromJsonAsync<WgerPage<T>>(next, Json, ct)
                ?? throw new InvalidOperationException($"wger returned an empty body for {next}");

            all.AddRange(page.Results);
            next = page.Next; // absolute URL or null
        }

        return all;
    }
}
