namespace MySelf.Api.Workouts;

// --- exercise catalogue ---

public sealed record ExerciseListItem(
    Guid Id,
    string Name,
    string Category,
    string DefaultTrackingMode,
    IReadOnlyList<string> PrimaryMuscles,
    IReadOnlyList<string> SecondaryMuscles,
    IReadOnlyList<string> Equipment);

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

// --- program stats (Overview tab; docs/02 §7 metrics — no adherence %) ---

public sealed record ProgramStats(
    int TotalSessions,
    DateOnly? FirstPerformedOn,
    DateOnly? LastPerformedOn,
    int SessionsThisWeek,
    int SessionsThisMonth,
    double WeeklyAverage,
    decimal TotalVolumeKg,
    int? AvgDurationSeconds,
    int CompletedSets,
    int SkippedSets,
    double SkippedSetRate,
    IReadOnlyList<ProgramDayStat> PerDay,
    IReadOnlyList<ProgramPrStat> PersonalRecords);

public sealed record ProgramDayStat(Guid DayId, string DayName, int Sessions, DateOnly? LastPerformedOn);

public sealed record ProgramPrStat(string ExerciseName, string Type, double Value, DateOnly AchievedOn);

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

/// <summary>Add a catalogue exercise to the running session (docs/02: "Add exercise" adds to today's session).</summary>
public sealed record AddSessionExerciseRequest(Guid ExerciseId, int? Sets);

/// <summary>
/// Swap the movement for one logged exercise (docs/02: "Replace exercise" → Today only / Today
/// and future workouts). <see cref="Scope"/> is "TodayOnly" or "TodayAndFuture".
/// </summary>
public sealed record ReplaceSessionExerciseRequest(Guid ExerciseId, string? Scope);

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
    bool WasEdited,
    SessionSummary Summary,
    IReadOnlyList<PersonalRecordDetail> NewPersonalRecords,
    IReadOnlyList<ExerciseLogDetail> Exercises);

/// <summary>A personal best (docs/02 §7). Emitted by <c>complete</c> for records set this session, and by exercise history.</summary>
public sealed record PersonalRecordDetail(
    string Type,
    decimal Value,
    decimal? WeightKg,
    int? Reps,
    DateOnly AchievedOn);

// --- exercise history / strength analytics ---

public sealed record ExerciseHistoryResult(
    Guid ExerciseId,
    string ExerciseName,
    IReadOnlyList<PersonalRecordDetail> PersonalRecords,
    IReadOnlyList<ExerciseHistoryEntry> Sessions);

public sealed record ExerciseHistoryEntry(
    Guid SessionId,
    DateOnly PerformedOn,
    string? DayName,
    decimal? TopSetWeightKg,
    int? TopSetReps,
    decimal? EstimatedOneRepMax,
    decimal Volume,
    int CompletedSets);

public sealed record StrengthPoint(DateOnly Date, decimal? EstimatedOneRepMax, decimal Volume);

public sealed record StrengthAnalyticsResult(Guid ExerciseId, string Range, IReadOnlyList<StrengthPoint> Points);

/// <summary>Roll-up shown on the finish screen and in history (docs/02). PRs/e1RM come later.</summary>
public sealed record SessionSummary(
    int? DurationSeconds,
    int CompletedSetCount,
    int SkippedSetCount,
    int TotalReps,
    decimal TotalVolumeKg);

/// <summary>One row of the workout history list (GET /api/v1/workout-sessions).</summary>
public sealed record WorkoutSessionListItem(
    Guid Id,
    string? DayName,
    string? ProgramName,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateOnly? PerformedOnLocalDate,
    bool WasEdited,
    SessionSummary Summary);

public sealed record WorkoutSessionListResult(
    IReadOnlyList<WorkoutSessionListItem> Items,
    int Page,
    int PageSize,
    int Total);

/// <summary>Completed sessions on one local calendar date (docs/04 §12 /workout-calendar).</summary>
public sealed record CalendarDay(DateOnly Date, IReadOnlyList<WorkoutSessionListItem> Sessions);

public sealed record WorkoutCalendarResult(DateOnly From, DateOnly To, IReadOnlyList<CalendarDay> Days);

/// <summary>Correct which local date a completed session counts against (docs/01 §3 Story 3A).</summary>
public sealed record RescheduleSessionRequest(DateOnly LocalDate);

public sealed record ExerciseLogDetail(
    Guid Id,
    Guid ExerciseId,
    string ExerciseName,
    string TrackingMode,
    int SortOrder,
    int? RestSeconds,
    Guid? SupersetGroupSnapshotId,
    int SupersetMemberOrder,
    int? SupersetRestAfterRoundSeconds,
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
