namespace MySelf.Domain.Workouts;

/// <summary>
/// The strength formulas from docs/02 §7. Estimated 1RM is Epley and is only meaningful for
/// weight-and-reps sets in the ~1–10 rep range — it is an <em>estimate</em>, never a real max.
/// Bodyweight / assisted modes get no e1RM (docs: "no bodyweight-based e1RM").
/// </summary>
public static class StrengthMath
{
    /// <summary>Epley: <c>weightKg × (1 + reps / 30)</c>. A single rep returns the bar weight.</summary>
    public static decimal EstimatedOneRepMax(decimal weightKg, int reps) =>
        weightKg * (1m + (reps / 30m));

    /// <summary>Volume load for one set: <c>weightKg × reps</c>.</summary>
    public static decimal SetVolume(decimal weightKg, int reps) => weightKg * reps;
}
