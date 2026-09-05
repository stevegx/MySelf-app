namespace MySelf.Domain.Workouts;

/// <summary>The four PR kinds from docs/02 §7 ("PR types").</summary>
public enum PersonalRecordType
{
    /// <summary>Heaviest weight lifted for the exercise, any rep count.</summary>
    HeaviestWeight,

    /// <summary>Highest Epley estimated 1RM.</summary>
    BestEstimatedOneRepMax,

    /// <summary>Most reps at a given weight — compared per weight (see <see cref="WeightKg"/>).</summary>
    MostRepsAtWeight,

    /// <summary>Highest single-session volume load for the exercise.</summary>
    BestExerciseVolume,
}

/// <summary>
/// A personal best for one exercise (docs/04 §11). Immutable: a better result inserts a new
/// row, the previous one stays as history. Only weight-and-reps exercises produce PRs.
/// </summary>
public class PersonalRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ExerciseId { get; set; }

    public PersonalRecordType Type { get; set; }

    /// <summary>The headline number: kg for weight/e1RM, reps for MostRepsAtWeight, kg for volume.</summary>
    public decimal Value { get; set; }

    /// <summary>The working weight, when the type is defined relative to one (heaviest weight, reps-at-weight).</summary>
    public decimal? WeightKg { get; set; }
    public int? Reps { get; set; }

    public DateOnly AchievedOn { get; set; }
    public Guid SessionId { get; set; }
    public Guid? SetLogId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
