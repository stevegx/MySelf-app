namespace MySelf.Api.Workouts;

/// <summary>
/// Size and range caps for the program builder. They exist to stop a script (or a bug)
/// filling the database or sending a multi-megabyte query parameter — not to constrain a
/// real user, so the numbers are generous. Kept in one place so the endpoints and their
/// tests agree.
/// </summary>
internal static class WorkoutLimits
{
    // --- tree size (checked before an insert) ---
    public const int MaxProgramsPerUser = 50;
    public const int MaxGroupsPerProgram = 30;
    public const int MaxVariantsPerGroup = 20;
    public const int MaxExercisesPerVariant = 50;
    public const int MaxSetsPerExercise = 20;
    public const int MaxSupersetsPerVariant = 25;

    // --- free-text / search ---
    public const int MaxSearchTermLength = 100;

    // --- numeric ranges on PUT /workout-variants ---
    public const int MaxRestSeconds = 3600;
    public const int MaxSortOrder = 1000;
    public const int MaxSupersetMemberOrder = 50;
    public const int MaxSetSortOrder = 100;
    public const int MaxTargetRir = 10;
    public const int MaxEstimatedDurationMinutes = 600;
}
