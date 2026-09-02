namespace MySelf.Domain.Workouts;

/// <summary>
/// A concrete workout inside a group (docs/02). Examples: "Legs #1", "Legs #2". Holds the
/// ordered list of exercises, their set prescriptions, and any superset groupings.
/// </summary>
public class WorkoutVariant
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }
    public WorkoutGroup Group { get; set; } = null!;

    public required string Name { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Optional user estimate, minutes. Not derived from anything.</summary>
    public int? EstimatedDurationMinutes { get; set; }

    public List<VariantExercise> Exercises { get; set; } = [];
    public List<SupersetGroup> Supersets { get; set; } = [];
}
