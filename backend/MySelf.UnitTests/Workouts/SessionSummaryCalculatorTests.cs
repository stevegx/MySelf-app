using MySelf.Domain.Workouts;

namespace MySelf.UnitTests.Workouts;

/// <summary>
/// Unit tests for the finish/history roll-up (docs/02). Volume never uses body weight
/// (locked decision #11); uncompleted sets are ignored.
/// </summary>
public class SessionSummaryCalculatorTests
{
    private static SetLog Set(
        DateTimeOffset? completedAt = null, DateTimeOffset? skippedAt = null,
        decimal? weightKg = null, decimal? addedWeightKg = null, int? reps = null) =>
        new()
        {
            CompletedAt = completedAt,
            SkippedAt = skippedAt,
            WeightKg = weightKg,
            AddedWeightKg = addedWeightKg,
            Reps = reps,
        };

    private static WorkoutSession Session(DateTimeOffset? completedAt, params SetLog[] sets) => new()
    {
        StartedAt = new DateTimeOffset(2026, 9, 4, 9, 0, 0, TimeSpan.Zero),
        CompletedAt = completedAt,
        ExerciseLogs = [new ExerciseLog { ExerciseName = "x", Sets = [.. sets] }],
    };

    [Fact]
    public void Sums_reps_and_volume_over_completed_sets_only()
    {
        var now = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
        var session = Session(
            completedAt: now,
            Set(completedAt: now, weightKg: 100m, reps: 8),   // 800
            Set(completedAt: now, weightKg: 90m, reps: 10),    // 900
            Set(skippedAt: now, weightKg: 90m, reps: 10),      // skipped -> ignored
            Set(reps: 12));                                     // pending -> ignored

        var r = SessionSummaryCalculator.Of(session);

        Assert.Equal(2, r.CompletedSetCount);
        Assert.Equal(1, r.SkippedSetCount);
        Assert.Equal(18, r.TotalReps);
        Assert.Equal(1700m, r.TotalVolumeKg);
        Assert.Equal(3600, r.DurationSeconds); // 09:00 -> 10:00
    }

    [Fact]
    public void Added_weight_counts_as_load_but_bodyweight_only_and_assisted_are_zero_volume()
    {
        var now = DateTimeOffset.UtcNow;
        var session = Session(
            completedAt: now,
            Set(completedAt: now, addedWeightKg: 20m, reps: 6),  // 120
            Set(completedAt: now, reps: 15));                     // bodyweight-only -> 0 volume, still 15 reps

        var r = SessionSummaryCalculator.Of(session);

        Assert.Equal(2, r.CompletedSetCount);
        Assert.Equal(21, r.TotalReps);
        Assert.Equal(120m, r.TotalVolumeKg);
    }

    [Fact]
    public void Duration_is_null_until_the_session_is_completed()
    {
        var r = SessionSummaryCalculator.Of(Session(completedAt: null, Set(reps: 5)));

        Assert.Null(r.DurationSeconds);
        Assert.Equal(0, r.CompletedSetCount);
    }
}
