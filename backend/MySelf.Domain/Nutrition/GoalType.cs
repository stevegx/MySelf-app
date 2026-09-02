namespace MySelf.Domain.Nutrition;

/// <summary>
/// The onboarding goal branch (docs/01 step 2, locked decision #23). <see cref="TrackOnly"/>
/// deliberately produces no calorie target at all. Lives here because the calorie estimator
/// is its first consumer; <c>UserGoal</c> (a later slice) will reference the same enum.
/// </summary>
public enum GoalType
{
    Lose,
    Maintain,
    Gain,
    TrackOnly,
}
