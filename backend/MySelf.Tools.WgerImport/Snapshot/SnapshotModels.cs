namespace MySelf.Tools.WgerImport.Snapshot;

/// <summary>
/// Normalised, source-agnostic snapshot written to <c>backend/seed-data/wger-catalogue.json</c>
/// and read back by the importer. Committed to the repo so `import` needs no network.
/// </summary>
public record CatalogueSnapshot(
    DateTimeOffset FetchedAt,
    string Source,
    IReadOnlyList<SnapshotCategory> Categories,
    IReadOnlyList<SnapshotMuscle> Muscles,
    IReadOnlyList<SnapshotEquipment> Equipment,
    IReadOnlyList<SnapshotExercise> Exercises);

public record SnapshotCategory(int Id, string Name);

public record SnapshotMuscle(int Id, string Name, string LatinName, bool IsFront);

public record SnapshotEquipment(int Id, string Name);

public record SnapshotExercise(
    string ExternalId,
    string Name,
    int CategoryId,
    string? Instructions,
    string TrackingMode,
    string? VariationGroupExternalId,
    string SourceVersion,
    string? LicenseShortName,
    string? LicenseUrl,
    string? Attribution,
    IReadOnlyList<int> PrimaryMuscleIds,
    IReadOnlyList<int> SecondaryMuscleIds,
    IReadOnlyList<int> EquipmentIds,
    // Illustration, added by the `enrich-images` pass. Null when wger has no main image.
    string? ImageUrl = null,
    string? ImageThumbUrl = null,
    string? ImageAttribution = null);
