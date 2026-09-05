namespace MySelf.Api.Workouts;

// --- exercise catalogue ---

public sealed record ExerciseListItem(Guid Id, string Name, string Category, string DefaultTrackingMode);

public sealed record ExerciseSearchResult(IReadOnlyList<ExerciseListItem> Items, int Page, int PageSize, int Total);

// --- programs ---

public sealed record CreateProgramRequest(string? Name, string? SplitLabel);

/// <summary>
/// Rename / relabel a program and (optionally) reorder its days by id.
/// <paramref name="RowVersion"/> is the <c>xmin</c> token the client last read (from
/// <see cref="ProgramDetail"/>); when supplied, the update is rejected with 409 if the
/// program changed in the meantime. Omit it to accept last-write-wins.
/// </summary>
public sealed record UpdateProgramRequest(
    string? Name,
    string? SplitLabel,
    IReadOnlyList<Guid>? DayOrder,
    uint? RowVersion);

public sealed record ProgramListItem(
    Guid Id,
    string Name,
    string? SplitLabel,
    bool IsActive,
    int DayCount,
    int ExerciseCount,
    DateTimeOffset CreatedAt);

public sealed record ProgramDetail(
    Guid Id,
    string Name,
    string? SplitLabel,
    bool IsActive,
    DateTimeOffset CreatedAt,
    uint RowVersion,
    IReadOnlyList<DayListItem> Days);

public sealed record DayListItem(Guid Id, string Name, int SortOrder, int ExerciseCount);

public sealed record CreateDayRequest(string? Name);

// --- day detail ---

public sealed record DayDetail(
    Guid Id,
    string Name,
    int SortOrder,
    int? EstimatedDurationMinutes,
    // The owning program's xmin token — send it back on PUT to guard the edit.
    uint ProgramRowVersion,
    IReadOnlyList<DayExerciseDetail> Exercises,
    IReadOnlyList<SupersetDetail> Supersets);

public sealed record DayExerciseDetail(
    Guid Id,
    Guid ExerciseId,
    string ExerciseName,
    int SortOrder,
    Guid? SupersetGroupId,
    int SupersetMemberOrder,
    int? RestSeconds,
    string? Notes,
    IReadOnlyList<SetPrescriptionDetail> Sets);

public sealed record SetPrescriptionDetail(
    Guid Id,
    int SortOrder,
    string Kind,
    bool IsAmrap,
    bool TargetToFailure,
    int? TargetRepsMin,
    int? TargetRepsMax,
    decimal? TargetWeightKg,
    int? TargetRir);

public sealed record SupersetDetail(Guid Id, int SortOrder, int RestAfterRoundSeconds);

// --- PUT /workout-days/{id}: the whole desired state, replacing what's there ---

public sealed record UpdateDayRequest(
    string? Name,
    int? EstimatedDurationMinutes,
    IReadOnlyList<UpdateDayExercise>? Exercises,
    IReadOnlyList<UpdateSuperset>? Supersets,
    // The owning program's xmin token from the last read; 409 if it moved on. Optional.
    uint? RowVersion);

public sealed record UpdateDayExercise(
    Guid ExerciseId,
    int SortOrder,
    string? SupersetRef,
    int SupersetMemberOrder,
    int? RestSeconds,
    string? Notes,
    IReadOnlyList<UpdateSetPrescription>? Sets);

public sealed record UpdateSetPrescription(
    int SortOrder,
    string? Kind,
    bool IsAmrap,
    bool TargetToFailure,
    int? TargetRepsMin,
    int? TargetRepsMax,
    decimal? TargetWeightKg,
    int? TargetRir);

public sealed record UpdateSuperset(string Ref, int SortOrder, int RestAfterRoundSeconds);

// --- bulk copy / move exercises between days (docs/08 Story 7) ---

/// <summary>
/// Copy or move <paramref name="DayExerciseIds"/> from <paramref name="SourceDayId"/>
/// into the day named in the route. Both days must belong to the caller.
/// <paramref name="RowVersion"/> guards on the destination program's xmin (optional).
/// </summary>
public sealed record BulkExerciseRequest(
    Guid SourceDayId,
    IReadOnlyList<Guid>? DayExerciseIds,
    uint? RowVersion);

// --- workout sessions (docs/02 "Starting a workout", Story 3A) ---

/// <summary>Null <paramref name="DayId"/> starts an ad-hoc session with no exercises yet.</summary>
public sealed record StartSessionRequest(Guid? DayId);

/// <summary>Log or update the performed values for one set (docs/02 autosave). Marks it complete.</summary>
public sealed record LogSetRequest(
    Guid SetLogId,
    decimal? WeightKg,
    decimal? AddedWeightKg,
    decimal? AssistanceKg,
    int? Reps,
    int? DurationSeconds,
    decimal? DistanceMeters,
    int? Rir,
    bool ReachedFailure);

/// <summary>Explicitly skip a set (docs/07). Reason is free text; the UI offers pain/equipment/time/other.</summary>
public sealed record SkipSetRequest(Guid SetLogId, string? Reason);

/// <summary>
/// Finish the session. <see cref="LocalDate"/> is the user's local calendar date the session
/// counts against (locked decision #8) — the client sends it; the server falls back to the
/// UTC date.
/// </summary>
public sealed record CompleteSessionRequest(DateOnly? LocalDate, string? Notes);

public sealed record WorkoutSessionDetail(
    Guid Id,
    Guid? SourceDayId,
    string? DayName,
    string? ProgramName,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateOnly? PerformedOnLocalDate,
    string? Notes,
    IReadOnlyList<ExerciseLogDetail> Exercises);

public sealed record ExerciseLogDetail(
    Guid Id,
    Guid ExerciseId,
    string ExerciseName,
    string TrackingMode,
    int SortOrder,
    Guid? SupersetGroupSnapshotId,
    int SupersetMemberOrder,
    IReadOnlyList<SetLogDetail> Sets);

public sealed record SetLogDetail(
    Guid Id,
    int SortOrder,
    string Kind,
    bool IsAmrap,
    bool TargetToFailure,
    int? TargetRepsMin,
    int? TargetRepsMax,
    decimal? TargetWeightKg,
    int? TargetRir,
    decimal? WeightKg,
    decimal? AddedWeightKg,
    decimal? AssistanceKg,
    int? Reps,
    int? DurationSeconds,
    decimal? DistanceMeters,
    int? Rir,
    bool ReachedFailure,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? SkippedAt,
    string? SkippedReason);
