namespace MySelf.Api.Me;

/// <summary>
/// The shape of <c>GET /api/v1/me/export</c> (docs/05 §13 "User can export … their data",
/// docs/01 "Data export σε CSV/JSON"). One self-contained JSON document of everything the
/// account owns, in plain fields — deliberately decoupled from the domain entities so the
/// export is a stable contract a user or tool can rely on. Refresh tokens and the shared
/// food cache are not user content and are excluded.
/// </summary>
public sealed record DataExport(
    DateTimeOffset ExportedAt,
    ExportAccount Account,
    ExportProfile? Profile,
    IReadOnlyList<ExportGoal> Goals,
    IReadOnlyList<ExportBodyMeasurement> BodyMeasurements,
    IReadOnlyList<ExportMealCategory> MealCategories,
    IReadOnlyList<ExportMealLog> MealLogs,
    IReadOnlyList<ExportCustomFood> CustomFoods,
    IReadOnlyList<ExportSavedMeal> SavedMeals,
    IReadOnlyList<ExportProgram> WorkoutPrograms,
    IReadOnlyList<ExportSession> WorkoutSessions,
    IReadOnlyList<ExportPersonalRecord> PersonalRecords);

public sealed record ExportAccount(Guid Id, string? Username, string? Email);

public sealed record ExportProfile(
    DateOnly DateOfBirth,
    decimal HeightCm,
    string? CalculationSex,
    string UnitSystem,
    string? Timezone,
    string? Locale,
    DateTimeOffset? OnboardingCompletedAt,
    bool WarnOffFocusExercises);

public sealed record ExportGoal(
    string GoalType,
    string Source,
    decimal? TargetWeightKg,
    int? CalorieTarget,
    int? ProteinGrams,
    int? CarbGrams,
    int? FatGrams,
    DateTimeOffset EffectiveFrom);

public sealed record ExportBodyMeasurement(DateOnly LocalDate, decimal WeightKg, DateTimeOffset MeasuredAt);

public sealed record ExportMealCategory(string Name, int SortOrder, DateTimeOffset? ArchivedAt);

public sealed record ExportMealLog(DateOnly LocalDate, string Category, IReadOnlyList<ExportMealItem> Items);

public sealed record ExportMealItem(
    string Name,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal Amount,
    string Unit,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbG,
    decimal FatG);

public sealed record ExportCustomFood(
    string Name,
    string? Brand,
    string? Barcode,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal Kcal,
    decimal ProteinG,
    decimal CarbG,
    decimal FatG,
    DateTimeOffset? ArchivedAt);

public sealed record ExportSavedMeal(
    string Name,
    string Category,
    string? Notes,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<ExportSavedMealItem> Items);

public sealed record ExportSavedMealItem(
    string Name,
    string ServingBasis,
    decimal? ServingSizeGrams,
    decimal PerBasisKcal,
    decimal PerBasisProteinG,
    decimal PerBasisCarbG,
    decimal PerBasisFatG,
    decimal DefaultAmount,
    string Unit);

public sealed record ExportProgram(
    string Name,
    string? SplitLabel,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<ExportProgramDay> Days);

public sealed record ExportProgramDay(
    string Name,
    int SortOrder,
    int? EstimatedDurationMinutes,
    IReadOnlyList<int> FocusMuscleIds,
    IReadOnlyList<ExportPrescribedExercise> Exercises);

public sealed record ExportPrescribedExercise(
    string ExerciseName,
    int SortOrder,
    int? RestSeconds,
    string? Notes,
    IReadOnlyList<ExportPrescribedSet> Sets);

public sealed record ExportPrescribedSet(
    int SortOrder,
    string Kind,
    bool IsAmrap,
    bool TargetToFailure,
    int? TargetRepsMin,
    int? TargetRepsMax,
    decimal? TargetWeightKg,
    int? TargetRir);

public sealed record ExportSession(
    string? DayName,
    string? ProgramName,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateOnly? PerformedOnLocalDate,
    string? Notes,
    bool WasEdited,
    IReadOnlyList<ExportSessionExercise> Exercises);

public sealed record ExportSessionExercise(
    string ExerciseName,
    string TrackingMode,
    int SortOrder,
    IReadOnlyList<ExportLoggedSet> Sets);

public sealed record ExportLoggedSet(
    int SortOrder,
    string Kind,
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

public sealed record ExportPersonalRecord(
    string ExerciseName,
    string Type,
    decimal Value,
    decimal? WeightKg,
    int? Reps,
    DateOnly AchievedOn);
