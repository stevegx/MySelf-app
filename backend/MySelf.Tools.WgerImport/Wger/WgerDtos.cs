namespace MySelf.Tools.WgerImport.Wger;

// Shapes of the wger REST API v2 responses we consume. Deserialised with
// JsonNamingPolicy.SnakeCaseLower, so snake_case fields map to these PascalCase names.

public record WgerPage<T>(int Count, string? Next, List<T> Results);

public record WgerRef(int Id, string Name);

public record WgerMuscle(int Id, string Name, string? NameEn, bool IsFront);

public record WgerLicense(int Id, string? ShortName, string? Url);

public record WgerTranslation(
    int Language,
    string? Name,
    string? Description,
    string? LicenseAuthor);

public record WgerExerciseInfo(
    int Id,
    string Uuid,
    string? LastUpdate,
    WgerRef Category,
    List<WgerMuscle> Muscles,
    List<WgerMuscle> MusclesSecondary,
    List<WgerRef> Equipment,
    WgerLicense? License,
    string? LicenseAuthor,
    List<WgerTranslation> Translations,
    string? VariationGroup,
    List<string>? TotalAuthorsHistory);
