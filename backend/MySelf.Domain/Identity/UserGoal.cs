using MySelf.Domain.Nutrition;

namespace MySelf.Domain.Identity;

/// <summary>
/// An effective-dated nutrition goal (docs/04 §11, docs/01 step 4). Goals are never edited in
/// place: accepting a new target inserts a new row with a later <see cref="EffectiveFrom"/>,
/// so the history always shows which target applied on a given day. The "current" goal is the
/// row with the latest <see cref="EffectiveFrom"/> for the user.
///
/// A skipped nutrition step or a Track-only goal is still recorded as a row — with
/// <see cref="Source"/> = <see cref="GoalSource.Manual"/> and null calorie/macro targets.
/// <see cref="UserId"/> is a plain Guid with an index but no navigation property, the same
/// choice as <see cref="RefreshToken"/>.
/// </summary>
public class UserGoal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public GoalType GoalType { get; set; }
    public GoalSource Source { get; set; }

    /// <summary>Optional context for Lose/Gain (locked decision #29). Never a promised date.</summary>
    public decimal? TargetWeightKg { get; set; }

    /// <summary>All null when nutrition was skipped or the goal is Track-only.</summary>
    public int? CalorieTarget { get; set; }
    public int? ProteinGrams { get; set; }
    public int? CarbGrams { get; set; }
    public int? FatGrams { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
