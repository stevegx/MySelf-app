namespace MySelf.Domain.Nutrition;

/// <summary>
/// The user's typical weekly activity, used to turn BMR into TDEE (docs/03 §8.3). Each level
/// maps to a fixed multiplier in <see cref="CalorieEstimator"/>. These are a coarse starting
/// estimate, not a measurement.
/// </summary>
public enum ActivityLevel
{
    Sedentary,
    Light,
    Moderate,
    VeryActive,
    ExtraActive,
}
