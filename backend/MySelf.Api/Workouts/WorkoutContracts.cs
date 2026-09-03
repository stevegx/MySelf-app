namespace MySelf.Api.Workouts;

// --- exercise catalogue ---

public sealed record ExerciseListItem(Guid Id, string Name, string Category, string DefaultTrackingMode);

public sealed record ExerciseSearchResult(IReadOnlyList<ExerciseListItem> Items, int Page, int PageSize, int Total);

// --- programs ---

public sealed record CreateProgramRequest(string? Name, string? SplitLabel);

/// <summary>
/// Rename / relabel a program and (optionally) reorder its groups by id.
/// <paramref name="RowVersion"/> is the <c>xmin</c> token the client last read (from
/// <see cref="ProgramDetail"/>); when supplied, the update is rejected with 409 if the
/// program changed in the meantime. Omit it to accept last-write-wins.
/// </summary>
public sealed record UpdateProgramRequest(
    string? Name,
    string? SplitLabel,
    IReadOnlyList<Guid>? GroupOrder,
    uint? RowVersion);

public sealed record ProgramListItem(
    Guid Id,
    string Name,
    string? SplitLabel,
    bool IsActive,
    int GroupCount,
    int VariantCount,
    DateTimeOffset CreatedAt);

public sealed record ProgramDetail(
    Guid Id,
    string Name,
    string? SplitLabel,
    bool IsActive,
    DateTimeOffset CreatedAt,
    uint RowVersion,
    IReadOnlyList<GroupDetail> Groups);

public sealed record GroupDetail(Guid Id, string Name, int SortOrder, IReadOnlyList<VariantListItem> Variants);

public sealed record VariantListItem(Guid Id, string Name, int SortOrder, int ExerciseCount);

public sealed record CreateGroupRequest(string? Name);

public sealed record UpdateGroupRequest(string? Name, IReadOnlyList<Guid>? VariantOrder, uint? RowVersion);

public sealed record CreateVariantRequest(string? Name);

// --- variant detail ---

public sealed record VariantDetail(
    Guid Id,
    string Name,
    int SortOrder,
    int? EstimatedDurationMinutes,
    // The owning program's xmin token — send it back on PUT to guard the edit.
    uint ProgramRowVersion,
    IReadOnlyList<VariantExerciseDetail> Exercises,
    IReadOnlyList<SupersetDetail> Supersets);

public sealed record VariantExerciseDetail(
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

// --- PUT /workout-variants/{id}: the whole desired state, replacing what's there ---

public sealed record UpdateVariantRequest(
    string? Name,
    int? EstimatedDurationMinutes,
    IReadOnlyList<UpdateVariantExercise>? Exercises,
    IReadOnlyList<UpdateSuperset>? Supersets,
    // The owning program's xmin token from the last read; 409 if it moved on. Optional.
    uint? RowVersion);

public sealed record UpdateVariantExercise(
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

// --- bulk copy / move exercises between variants (docs/08 Story 7) ---

/// <summary>
/// Copy or move <paramref name="VariantExerciseIds"/> from <paramref name="SourceVariantId"/>
/// into the variant named in the route. Both variants must belong to the caller.
/// <paramref name="RowVersion"/> guards on the destination program's xmin (optional).
/// </summary>
public sealed record BulkExerciseRequest(
    Guid SourceVariantId,
    IReadOnlyList<Guid>? VariantExerciseIds,
    uint? RowVersion);
