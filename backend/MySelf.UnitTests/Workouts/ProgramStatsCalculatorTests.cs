using MySelf.Domain.Workouts;

namespace MySelf.UnitTests.Workouts;

/// <summary>
/// Unit tests for the program Overview roll-up (docs/02 §7). No adherence %/missed-rate;
/// weekly average is sessions-in-range / weeks-in-range, range capped at 8 weeks.
/// </summary>
public class ProgramStatsCalculatorTests
{
    private static readonly Guid PushId = Guid.NewGuid();
    private static readonly Guid PullId = Guid.NewGuid();

    private static SetLog Set(DateTimeOffset? completedAt = null, DateTimeOffset? skippedAt = null,
        decimal? weightKg = null, int? reps = null) =>
        new() { CompletedAt = completedAt, SkippedAt = skippedAt, WeightKg = weightKg, Reps = reps };

    private static WorkoutSession Session(Guid? dayId, DateOnly performedOn, int durationMinutes, params SetLog[] sets)
    {
        var start = performedOn.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        return new WorkoutSession
        {
            Id = Guid.NewGuid(),
            SourceDayId = dayId,
            Status = SessionStatus.Completed,
            StartedAt = start,
            CompletedAt = start.AddMinutes(durationMinutes),
            PerformedOnLocalDate = performedOn,
            ExerciseLogs = [new ExerciseLog { ExerciseName = "x", Sets = [.. sets] }],
        };
    }

    private static readonly (Guid, string)[] Days = [(PushId, "Push"), (PullId, "Pull")];

    [Fact]
    public void Empty_program_reports_zeros_and_every_day_at_zero()
    {
        var r = ProgramStatsCalculator.Of([], Days, new DateOnly(2026, 9, 5));

        Assert.Equal(0, r.TotalSessions);
        Assert.Null(r.FirstPerformedOn);
        Assert.Null(r.AvgDurationSeconds);
        Assert.Equal(2, r.PerDay.Count);
        Assert.All(r.PerDay, d => Assert.Equal(0, d.Sessions));
    }

    [Fact]
    public void Aggregates_counts_volume_duration_and_per_day_breakdown()
    {
        var today = new DateOnly(2026, 9, 5); // a Saturday; week starts Mon 2026-08-31
        var sessions = new[]
        {
            Session(PushId, new DateOnly(2026, 9, 1), 60, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 100m, reps: 5)), // 500, this week + month
            Session(PushId, new DateOnly(2026, 9, 4), 40, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 80m, reps: 10), Set(skippedAt: DateTimeOffset.UtcNow)), // 800, this week + month
            Session(PullId, new DateOnly(2026, 8, 20), 50, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 60m, reps: 10)), // 600, earlier
        };

        var r = ProgramStatsCalculator.Of(sessions, Days, today);

        Assert.Equal(3, r.TotalSessions);
        Assert.Equal(new DateOnly(2026, 8, 20), r.FirstPerformedOn);
        Assert.Equal(new DateOnly(2026, 9, 4), r.LastPerformedOn);
        Assert.Equal(2, r.SessionsThisWeek);
        Assert.Equal(2, r.SessionsThisMonth);
        Assert.Equal(1900m, r.TotalVolumeKg);
        Assert.Equal(3000, r.AvgDurationSeconds); // (60+40+50)/3 min = 50 min
        Assert.Equal(3, r.CompletedSets);
        Assert.Equal(1, r.SkippedSets);
        Assert.Equal(0.25, r.SkippedSetRate);

        var push = r.PerDay.Single(d => d.DayName == "Push");
        Assert.Equal(2, push.Sessions);
        Assert.Equal(new DateOnly(2026, 9, 4), push.LastPerformedOn);
        var pull = r.PerDay.Single(d => d.DayName == "Pull");
        Assert.Equal(1, pull.Sessions);
    }

    [Fact]
    public void Weekly_average_divides_by_weeks_lived_capped_at_eight()
    {
        var today = new DateOnly(2026, 9, 5);

        // First session 10 days ago -> program has "lived" 2 weeks -> divide by 2, not 8.
        var recent = new[]
        {
            Session(PushId, new DateOnly(2026, 8, 26), 30, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 50m, reps: 5)),
            Session(PushId, new DateOnly(2026, 9, 1), 30, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 50m, reps: 5)),
            Session(PullId, new DateOnly(2026, 9, 3), 30, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 50m, reps: 5)),
        };
        Assert.Equal(1.5, ProgramStatsCalculator.Of(recent, Days, today).WeeklyAverage); // 3 / 2

        // First session ~100 days ago -> range capped at 8 weeks; only the 2 sessions inside
        // the last 8 weeks count. 2 / 8 = 0.25, rounded to one place (to-even) = 0.2.
        var spread = new[]
        {
            Session(PushId, new DateOnly(2026, 5, 28), 30, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 50m, reps: 5)),
            Session(PushId, new DateOnly(2026, 8, 15), 30, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 50m, reps: 5)),
            Session(PullId, new DateOnly(2026, 8, 29), 30, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 50m, reps: 5)),
        };
        Assert.Equal(0.2, ProgramStatsCalculator.Of(spread, Days, today).WeeklyAverage);
    }

    [Fact]
    public void Sessions_from_a_deleted_day_still_count_in_totals_but_not_per_day()
    {
        var today = new DateOnly(2026, 9, 5);
        var goneDay = Guid.NewGuid();
        var sessions = new[]
        {
            Session(goneDay, new DateOnly(2026, 9, 2), 30, Set(completedAt: DateTimeOffset.UtcNow, weightKg: 40m, reps: 10)),
        };

        var r = ProgramStatsCalculator.Of(sessions, Days, today);

        Assert.Equal(1, r.TotalSessions);
        Assert.Equal(400m, r.TotalVolumeKg);
        Assert.All(r.PerDay, d => Assert.Equal(0, d.Sessions));
    }
}
