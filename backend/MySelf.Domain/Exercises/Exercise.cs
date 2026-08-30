namespace MySelf.Domain.Exercises;

/// <summary>
/// A catalogue exercise. This MVP catalogue is imported from wger (docs/03: seed, not a
/// runtime dependency); provenance and licence are stored per row so it can be refreshed
/// and attributed.
/// </summary>
public class Exercise
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public int CategoryId { get; set; }
    public ExerciseCategory Category { get; set; } = null!;

    /// <summary>Sanitised HTML from the source. Null when the source had no description.</summary>
    public string? Instructions { get; set; }

    public TrackingMode DefaultTrackingMode { get; set; }

    // --- provenance ---
    public required string Source { get; set; } // e.g. "wger"
    public required string ExternalId { get; set; } // source's stable id (wger uuid)
    public string? VariationGroupExternalId { get; set; } // groups source-side variations
    public required string SourceVersion { get; set; } // source's last-update marker, for refresh
    public DateTimeOffset FetchedAt { get; set; }

    // --- licence / attribution ---
    public string? LicenseShortName { get; set; } // e.g. "CC-BY-SA 4"
    public string? LicenseUrl { get; set; }
    public string? Attribution { get; set; } // author(s)

    public ICollection<ExerciseMuscle> Muscles { get; set; } = [];
    public ICollection<ExerciseEquipment> Equipment { get; set; } = [];
}
