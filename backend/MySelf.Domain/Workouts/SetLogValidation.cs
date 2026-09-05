using MySelf.Domain.Exercises;

namespace MySelf.Domain.Workouts;

/// <summary>The performed values a user is submitting for one set. All optional at this layer.</summary>
public readonly record struct SetPerformance(
    decimal? WeightKg,
    decimal? AddedWeightKg,
    decimal? AssistanceKg,
    int? Reps,
    int? DurationSeconds,
    decimal? DistanceMeters);

/// <summary>
/// Which fields a performed set must carry before it can be marked complete, per the
/// exercise's <see cref="TrackingMode"/> (docs/02 "Strict set validation", locked decision
/// #10: both values required for Weight × Reps, no inheritance from the previous set). Pure —
/// same shape as <c>Nutrition/CalorieEstimator</c>.
/// </summary>
public static class SetLogValidation
{
    /// <summary>
    /// Null when the submitted values satisfy the tracking mode. Otherwise a short,
    /// user-facing reason the set can't be completed yet.
    /// </summary>
    public static string? MissingRequiredField(TrackingMode mode, SetPerformance p) => mode switch
    {
        TrackingMode.WeightAndReps =>
            p is { WeightKg: not null, Reps: not null } ? null : "Enter both weight and reps.",

        TrackingMode.BodyweightReps =>
            p.Reps is not null ? null : "Enter reps.",

        TrackingMode.BodyweightPlusWeight =>
            p is { AddedWeightKg: not null, Reps: not null } ? null : "Enter both added weight and reps.",

        TrackingMode.AssistanceReps =>
            p is { AssistanceKg: not null, Reps: not null } ? null : "Enter both assistance weight and reps.",

        TrackingMode.RepsOnly =>
            p.Reps is not null ? null : "Enter reps.",

        TrackingMode.Duration =>
            p.DurationSeconds is not null and > 0 ? null : "Enter a duration in seconds.",

        _ => null,
    };

    /// <summary>Cross-cutting sanity checks that apply whatever the tracking mode.</summary>
    public static string? OutOfRange(SetPerformance p)
    {
        if (p.Reps is < 0 or > 1000)
        {
            return "Reps are out of range.";
        }

        if (p.WeightKg is < 0 or > 2000 || p.AddedWeightKg is < 0 or > 2000 || p.AssistanceKg is < 0 or > 2000)
        {
            return "Weight is out of range.";
        }

        if (p.DurationSeconds is < 0 or > 86_400)
        {
            return "Duration is out of range.";
        }

        if (p.DistanceMeters is < 0 or > 1_000_000)
        {
            return "Distance is out of range.";
        }

        return null;
    }
}
