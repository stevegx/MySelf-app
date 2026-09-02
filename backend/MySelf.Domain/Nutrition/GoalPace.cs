namespace MySelf.Domain.Nutrition;

/// <summary>
/// How big a calorie deficit/surplus to apply for a Lose or Gain goal (docs/01 step 3):
/// Gentle is −250 / +150 kcal/day, Standard is −500 / +300. Not meaningful for Maintain or
/// TrackOnly. These are editable starting presets, not medical guidance.
/// </summary>
public enum GoalPace
{
    Gentle,
    Standard,
}
