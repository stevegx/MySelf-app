namespace MySelf.Domain.Workouts;

/// <summary>
/// A single prescribed set for a <see cref="DayExercise"/> (docs/04 §11). Targets are
/// all optional — the user prescribes as much or as little as they want. There is no
/// automatic inheritance between sets (locked decision #10).
/// </summary>
public class SetPrescription
{
    public Guid Id { get; set; }

    public Guid DayExerciseId { get; set; }
    public DayExercise DayExercise { get; set; } = null!;

    public int SortOrder { get; set; }

    public SetKind Kind { get; set; }

    /// <summary>As-many-reps-as-possible for this set.</summary>
    public bool IsAmrap { get; set; }

    /// <summary>Take this set to muscular failure.</summary>
    public bool TargetToFailure { get; set; }

    public int? TargetRepsMin { get; set; }
    public int? TargetRepsMax { get; set; }

    /// <summary>Canonical kilograms (docs/04: decimal, never binary float).</summary>
    public decimal? TargetWeightKg { get; set; }

    /// <summary>Reps in reserve, optional (locked decision #14).</summary>
    public int? TargetRir { get; set; }
}
