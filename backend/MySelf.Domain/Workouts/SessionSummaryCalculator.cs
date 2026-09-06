namespace MySelf.Domain.Workouts;

/// <summary>Roll-up numbers shown when a workout finishes and in the history list (docs/02).</summary>
public readonly record struct SessionSummaryResult(
    int? DurationSeconds,
    int CompletedSetCount,
    int SkippedSetCount,
    int TotalReps,
    decimal TotalVolumeKg);

/// <summary>
/// Derives a <see cref="WorkoutSession"/>'s summary from its logged sets. Pure — same style
/// as <c>CalorieEstimator</c> / <c>SetLogValidation</c>.
///
/// <para><b>Volume</b> (kg) = Σ over completed sets of <c>load × reps</c>, where
/// <c>load = WeightKg ?? AddedWeightKg ?? 0</c>. Bodyweight-only, assisted, reps-only and
/// duration sets contribute 0 to volume load (locked decision #11 — never the body weight)
/// but still count toward the set count and total reps. e1RM and PRs are a later slice.</para>
/// </summary>
public static class SessionSummaryCalculator
{
    public static SessionSummaryResult Of(WorkoutSession session)
    {
        var sets = session.ExerciseLogs.SelectMany(e => e.Sets).ToList();

        var completed = sets.Where(s => s.CompletedAt is not null).ToList();
        var skipped = sets.Count(s => s.SkippedAt is not null);

        var totalReps = completed.Sum(s => s.Reps ?? 0);
        var totalVolume = completed.Sum(s => (s.WeightKg ?? s.AddedWeightKg ?? 0m) * (s.Reps ?? 0));

        int? durationSeconds = session.CompletedAt is { } end
            ? (int)Math.Max(0, (end - session.StartedAt).TotalSeconds)
            : null;

        return new SessionSummaryResult(durationSeconds, completed.Count, skipped, totalReps, totalVolume);
    }
}
