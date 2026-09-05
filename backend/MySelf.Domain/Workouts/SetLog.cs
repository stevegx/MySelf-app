namespace MySelf.Domain.Workouts;

/// <summary>
/// One set within an <see cref="ExerciseLog"/> (docs/04 §11, docs/02 "Strict set
/// validation"). Pre-created from the source <see cref="SetPrescription"/> at session start
/// so the UI can show the target immediately; <see cref="CompletedAt"/>/<see
/// cref="SkippedAt"/> stay null until the user acts, and an uncompleted set never enters
/// analytics. A set added mid-session (add exercise / ad-hoc) has null targets.
/// </summary>
public class SetLog
{
    public Guid Id { get; set; }

    public Guid ExerciseLogId { get; set; }
    public ExerciseLog ExerciseLog { get; set; } = null!;

    public int SortOrder { get; set; }

    public SetKind Kind { get; set; }
    public bool IsAmrap { get; set; }
    public bool TargetToFailure { get; set; }

    // --- target, snapshotted from the SetPrescription at start ---
    public int? TargetRepsMin { get; set; }
    public int? TargetRepsMax { get; set; }
    public decimal? TargetWeightKg { get; set; }
    public int? TargetRir { get; set; }

    // --- performed, filled in on complete (locked decision #10: both required, no inheritance) ---
    public decimal? WeightKg { get; set; }
    public decimal? AddedWeightKg { get; set; }
    public decimal? AssistanceKg { get; set; }
    public int? Reps { get; set; }
    public int? DurationSeconds { get; set; }
    public decimal? DistanceMeters { get; set; }
    public int? Rir { get; set; }
    public bool ReachedFailure { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? SkippedAt { get; set; }
    public string? SkippedReason { get; set; }
}
