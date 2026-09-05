namespace MySelf.Domain.Workouts;

/// <summary>
/// One trainable day inside a program (docs/02): a user-named bucket of exercises and their
/// set prescriptions, e.g. "Push" or "Legs A". A program is a flat, ordered list of days —
/// there is no separate grouping level. Multiple days that cover similar ground (alternating
/// leg days, say) are just two separate days with names the user chooses, not variants of
/// one type.
/// </summary>
public class WorkoutDay
{
    public Guid Id { get; set; }

    public Guid ProgramId { get; set; }
    public WorkoutProgram Program { get; set; } = null!;

    public required string Name { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Optional user estimate, minutes. Not derived from anything.</summary>
    public int? EstimatedDurationMinutes { get; set; }

    public List<DayExercise> Exercises { get; set; } = [];
    public List<SupersetGroup> Supersets { get; set; } = [];
}
