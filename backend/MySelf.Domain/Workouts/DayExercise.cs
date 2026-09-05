using MySelf.Domain.Exercises;

namespace MySelf.Domain.Workouts;

/// <summary>
/// One exercise as it appears in a day: a reference to a catalogue <see cref="Exercise"/>,
/// its position, its set prescriptions, and (optionally) its place in a superset. Custom
/// (user-created) exercises are a later addition; the builder uses the seeded catalogue for
/// now.
/// </summary>
public class DayExercise
{
    public Guid Id { get; set; }

    public Guid DayId { get; set; }
    public WorkoutDay Day { get; set; } = null!;

    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public int SortOrder { get; set; }

    /// <summary>Non-null when this exercise is part of a superset.</summary>
    public Guid? SupersetGroupId { get; set; }
    public SupersetGroup? SupersetGroup { get; set; }

    /// <summary>Order within the superset round. Ignored when <see cref="SupersetGroupId"/> is null.</summary>
    public int SupersetMemberOrder { get; set; }

    /// <summary>Rest after this exercise, seconds. Optional.</summary>
    public int? RestSeconds { get; set; }

    public string? Notes { get; set; }

    public List<SetPrescription> Sets { get; set; } = [];
}
