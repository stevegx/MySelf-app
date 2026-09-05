namespace MySelf.Domain.Workouts;

/// <summary>
/// A workout session's lifecycle (docs/02 "Starting a workout"). Only one
/// <see cref="InProgress"/> session may exist per user (locked decision #27).
/// </summary>
public enum SessionStatus
{
    InProgress,
    Completed,
    Discarded,
}
