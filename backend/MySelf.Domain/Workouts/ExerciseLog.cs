using MySelf.Domain.Exercises;

namespace MySelf.Domain.Workouts;

/// <summary>
/// One exercise as it appears in a session (docs/04 §11): a snapshot of the catalogue
/// <see cref="Exercise"/> — name and tracking mode — taken at session start, so later edits
/// to the catalogue or the source variant never rewrite this session's history.
/// </summary>
public class ExerciseLog
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }
    public WorkoutSession Session { get; set; } = null!;

    public Guid ExerciseId { get; set; }
    public required string ExerciseName { get; set; }
    public TrackingMode TrackingMode { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Rest after a set of this exercise, seconds — snapshotted from the day. Null = no timer.</summary>
    public int? RestSeconds { get; set; }

    /// <summary>Groups this log with its superset partners for round display; the id of the
    /// source <see cref="SupersetGroup"/> at snapshot time. Null outside a superset.</summary>
    public Guid? SupersetGroupSnapshotId { get; set; }
    public int SupersetMemberOrder { get; set; }

    /// <summary>Rest after a whole superset round, seconds — snapshotted; same for every member. Null outside a superset.</summary>
    public int? SupersetRestAfterRoundSeconds { get; set; }

    public List<SetLog> Sets { get; set; } = [];
}
