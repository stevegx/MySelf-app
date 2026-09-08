using System.Text.Json;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Exercises;
using MySelf.Infrastructure.Persistence;
using MySelf.Tools.WgerImport;
using MySelf.Tools.WgerImport.Import;
using MySelf.Tools.WgerImport.Snapshot;
using MySelf.Tools.WgerImport.Wger;

var command = args.FirstOrDefault();

return command switch
{
    "fetch" => await FetchAsync(),
    "enrich-images" => await EnrichImagesAsync(),
    "import" => await ImportAsync(),
    _ => Usage(),
};

static int Usage()
{
    Console.WriteLine(
        """
        MySelf wger catalogue tool

          dotnet run --project backend/MySelf.Tools.WgerImport -- fetch
              Pull the wger catalogue and (re)write backend/seed-data/wger-catalogue.json

          dotnet run --project backend/MySelf.Tools.WgerImport -- enrich-images
              Add wger "main" image URLs to the existing wger-catalogue.json in place
              (everything else is left byte-for-byte). No DB, just network.

          dotnet run --project backend/MySelf.Tools.WgerImport -- import
              Load backend/seed-data/wger-catalogue.json into PostgreSQL (idempotent).
              Reads the connection string from .env (ConnectionStrings__DefaultConnection).
        """);
    return 1;
}

static async Task<int> EnrichImagesAsync()
{
    if (!File.Exists(RepoPaths.CatalogueSnapshot))
    {
        Console.Error.WriteLine($"Snapshot not found: {RepoPaths.CatalogueSnapshot}. Run `fetch` first.");
        return 1;
    }

    CatalogueSnapshot snapshot;
    await using (var readStream = File.OpenRead(RepoPaths.CatalogueSnapshot))
    {
        snapshot = await JsonSerializer.DeserializeAsync<CatalogueSnapshot>(readStream, SnapshotJson.Options)
            ?? throw new InvalidOperationException("Snapshot file was empty or invalid.");
    }

    using var http = new HttpClient { BaseAddress = new Uri(WgerClient.BaseUrl), Timeout = TimeSpan.FromSeconds(60) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("MySelfApp-wger-import/1.0 (learning project)");
    var client = new WgerClient(http);

    Console.WriteLine("Fetching wger main exercise images...");
    var images = await client.GetMainExerciseImagesAsync(default);

    // Key by the exercise-base uuid — that's what our ExternalId holds. Keep the first per base.
    var byUuid = new Dictionary<string, WgerExerciseImage>();
    foreach (var img in images)
    {
        if (!img.IsAiGenerated && !string.IsNullOrWhiteSpace(img.ExerciseUuid) && !string.IsNullOrWhiteSpace(img.Image))
        {
            byUuid.TryAdd(img.ExerciseUuid!, img);
        }
    }

    var matched = 0;
    var exercises = snapshot.Exercises
        .Select(e =>
        {
            if (!byUuid.TryGetValue(e.ExternalId, out var img))
            {
                return e;
            }
            matched++;
            var author = string.IsNullOrWhiteSpace(img.LicenseAuthor) ? null : img.LicenseAuthor!.Trim();
            return e with
            {
                ImageUrl = img.Image,
                ImageThumbUrl = string.IsNullOrWhiteSpace(img.Thumbnails?.Small) ? img.Image : img.Thumbnails!.Small,
                ImageAttribution = author is null ? "wger.de (CC BY-SA)" : $"{author} · wger.de (CC BY-SA)",
            };
        })
        .ToList();

    var updated = snapshot with { Exercises = exercises };

    await using (var writeStream = File.Create(RepoPaths.CatalogueSnapshot))
    {
        await JsonSerializer.SerializeAsync(writeStream, updated, SnapshotJson.Options);
    }

    Console.WriteLine($"Matched images for {matched} / {snapshot.Exercises.Count} exercises. Wrote {RepoPaths.CatalogueSnapshot}.");
    return 0;
}

static async Task<int> FetchAsync()
{
    using var http = new HttpClient { BaseAddress = new Uri(WgerClient.BaseUrl), Timeout = TimeSpan.FromSeconds(60) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("MySelfApp-wger-import/1.0 (learning project)");
    var client = new WgerClient(http);

    Console.WriteLine("Fetching wger reference data + exercises...");
    var categories = await client.GetCategoriesAsync(default);
    var muscles = await client.GetMusclesAsync(default);
    var equipment = await client.GetEquipmentAsync(default);
    var exercises = await client.GetExercisesAsync(default);
    Console.WriteLine($"  {categories.Count} categories, {muscles.Count} muscles, {equipment.Count} equipment, {exercises.Count} exercises");

    var builder = new SnapshotBuilder(LoadTrackingModeOverrides());
    var (snapshot, skipped) = builder.Build(categories, muscles, equipment, exercises);

    Directory.CreateDirectory(RepoPaths.SeedDataDir);
    await using var stream = File.Create(RepoPaths.CatalogueSnapshot);
    await JsonSerializer.SerializeAsync(stream, snapshot, SnapshotJson.Options);

    Console.WriteLine($"Wrote {snapshot.Exercises.Count} exercises to {RepoPaths.CatalogueSnapshot}");
    if (skipped > 0)
    {
        Console.WriteLine($"  ({skipped} exercises skipped: no English translation)");
    }
    return 0;
}

static async Task<int> ImportAsync()
{
    Env.NoClobber().TraversePath().Load();
    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException(
            "ConnectionStrings__DefaultConnection is not set. Add it to .env.");

    if (!File.Exists(RepoPaths.CatalogueSnapshot))
    {
        Console.Error.WriteLine($"Snapshot not found: {RepoPaths.CatalogueSnapshot}. Run `fetch` first.");
        return 1;
    }

    await using var readStream = File.OpenRead(RepoPaths.CatalogueSnapshot);
    var snapshot = await JsonSerializer.DeserializeAsync<CatalogueSnapshot>(readStream, SnapshotJson.Options)
        ?? throw new InvalidOperationException("Snapshot file was empty or invalid.");

    var options = new DbContextOptionsBuilder<MySelfDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    await using var db = new MySelfDbContext(options);
    await using var tx = await db.Database.BeginTransactionAsync();

    var result = await CatalogueImporter.ImportAsync(db, snapshot);
    await tx.CommitAsync();

    Console.WriteLine(
        $"Imported: {result.Categories} categories, {result.Muscles} muscles, " +
        $"{result.Equipment} equipment, {result.Exercises} exercises.");
    return 0;
}

static IReadOnlyDictionary<string, TrackingMode> LoadTrackingModeOverrides()
{
    if (!File.Exists(RepoPaths.TrackingModeOverrides))
    {
        return new Dictionary<string, TrackingMode>();
    }

    var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(
        File.ReadAllText(RepoPaths.TrackingModeOverrides),
        SnapshotJson.Options) ?? new();

    return raw.ToDictionary(kv => kv.Key, kv => Enum.Parse<TrackingMode>(kv.Value, ignoreCase: true));
}

file static class SnapshotJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
