namespace MySelf.Domain.Identity;

/// <summary>
/// Where a <see cref="UserGoal"/>'s calorie/macro targets came from: the app's estimator
/// (docs/03 §8) or numbers the user entered themselves. A skipped nutrition step or a
/// Track-only goal is recorded as <see cref="Manual"/> with null targets.
/// </summary>
public enum GoalSource
{
    Estimated,
    Manual,
}
