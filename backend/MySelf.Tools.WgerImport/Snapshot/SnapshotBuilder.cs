using Ganss.Xss;
using MySelf.Domain.Exercises;
using MySelf.Tools.WgerImport.Wger;

namespace MySelf.Tools.WgerImport.Snapshot;

public sealed class SnapshotBuilder(IReadOnlyDictionary<string, TrackingMode> trackingModeOverrides)
{
    private const int EnglishLanguageId = 2;
    private const string SourceName = "wger";

    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    public (CatalogueSnapshot Snapshot, int SkippedNoEnglish) Build(
        IReadOnlyList<WgerRef> categories,
        IReadOnlyList<WgerMuscle> muscles,
        IReadOnlyList<WgerRef> equipment,
        IReadOnlyList<WgerExerciseInfo> exercises)
    {
        var snapshotCategories = categories
            .Select(c => new SnapshotCategory(c.Id, c.Name))
            .OrderBy(c => c.Id)
            .ToList();

        var snapshotMuscles = muscles
            .Select(m => new SnapshotMuscle(
                m.Id,
                Name: string.IsNullOrWhiteSpace(m.NameEn) ? m.Name : m.NameEn,
                LatinName: m.Name,
                m.IsFront))
            .OrderBy(m => m.Id)
            .ToList();

        var snapshotEquipment = equipment
            .Select(e => new SnapshotEquipment(e.Id, e.Name))
            .OrderBy(e => e.Id)
            .ToList();

        var snapshotExercises = new List<SnapshotExercise>();
        var skipped = 0;

        foreach (var ex in exercises)
        {
            var english = ex.Translations.FirstOrDefault(t => t.Language == EnglishLanguageId);
            if (english is null || string.IsNullOrWhiteSpace(english.Name))
            {
                skipped++;
                continue;
            }

            var primaryIds = ex.Muscles.Select(m => m.Id).Distinct().ToList();
            var secondaryIds = ex.MusclesSecondary
                .Select(m => m.Id)
                .Distinct()
                .Where(id => !primaryIds.Contains(id))
                .ToList();
            var equipmentIds = ex.Equipment.Select(e => e.Id).Distinct().ToList();

            var trackingMode = trackingModeOverrides.TryGetValue(ex.Uuid, out var overridden)
                ? overridden
                : TrackingModeClassifier.Classify(ex.Category.Name, equipmentIds);

            snapshotExercises.Add(new SnapshotExercise(
                ExternalId: ex.Uuid,
                Name: english.Name.Trim(),
                CategoryId: ex.Category.Id,
                Instructions: SanitiseInstructions(english.Description),
                TrackingMode: trackingMode.ToString(),
                VariationGroupExternalId: ex.VariationGroup,
                SourceVersion: ex.LastUpdate ?? string.Empty,
                LicenseShortName: ex.License?.ShortName?.Trim(),
                LicenseUrl: ex.License?.Url,
                Attribution: BuildAttribution(ex, english),
                PrimaryMuscleIds: primaryIds,
                SecondaryMuscleIds: secondaryIds,
                EquipmentIds: equipmentIds));
        }

        var snapshot = new CatalogueSnapshot(
            FetchedAt: DateTimeOffset.UtcNow,
            Source: SourceName,
            Categories: snapshotCategories,
            Muscles: snapshotMuscles,
            Equipment: snapshotEquipment,
            Exercises: snapshotExercises.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList());

        return (snapshot, skipped);
    }

    private string? SanitiseInstructions(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var clean = _sanitizer.Sanitize(html).Trim();
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }

    private static string? BuildAttribution(WgerExerciseInfo ex, WgerTranslation english)
    {
        var authors = (ex.TotalAuthorsHistory ?? [])
            .Concat([ex.LicenseAuthor, english.LicenseAuthor])
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return authors.Count == 0 ? null : string.Join(", ", authors);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "ul", "ol", "li", "strong", "b", "em", "i", "a" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("http");

        return sanitizer;
    }
}
