namespace MySelf.Domain.Workouts;

public readonly record struct ProgramDayStatResult(Guid DayId, string DayName, int Sessions, DateOnly? LastPerformedOn);

/// <summary>Aggregate numbers for a program's Overview tab (docs/02 §7 "metrics").</summary>
public readonly record struct ProgramStatsResult(
    int TotalSessions,
    DateOnly? FirstPerformedOn,
    DateOnly? LastPerformedOn,
    int SessionsThisWeek,
    int SessionsThisMonth,
    double WeeklyAverage,
    decimal TotalVolumeKg,
    int? AvgDurationSeconds,
    int CompletedSets,
    int SkippedSets,
    double SkippedSetRate,
    IReadOnlyList<ProgramDayStatResult> PerDay);

/// <summary>
/// Rolls a program's completed <see cref="WorkoutSession"/>s up into the Overview numbers.
/// Pure — same style as <see cref="SessionSummaryCalculator"/> / <c>PersonalRecordDetector</c>,
/// so it is unit-tested without EF.
///
/// <para>Deliberately <b>no</b> adherence %, missed-workout rate or planned-vs-completed
/// score — docs/02 forbids those without a fixed plan. Weekly average is
/// <c>completed sessions in range / weeks in range</c> (docs/02 "Workout frequency"),
/// the range being the weeks the program has existed, capped at 8.</para>
/// </summary>
public static class ProgramStatsCalculator
{
    public static ProgramStatsResult Of(
        IReadOnlyCollection<WorkoutSession> sessions,
        IReadOnlyList<(Guid Id, string Name)> days,
        DateOnly today)
    {
        var performed = sessions
            .Where(s => s.PerformedOnLocalDate is not null)
            .Select(s => new Entry(s, s.PerformedOnLocalDate!.Value, SessionSummaryCalculator.Of(s)))
            .ToList();

        var emptyPerDay = days.Select(d => new ProgramDayStatResult(d.Id, d.Name, 0, null)).ToList();
        if (performed.Count == 0)
        {
            return new ProgramStatsResult(0, null, null, 0, 0, 0, 0m, null, 0, 0, 0, emptyPerDay);
        }

        var first = performed.Min(p => p.Date);
        var last = performed.Max(p => p.Date);

        var weekStart = StartOfWeek(today);
        var sessionsThisWeek = performed.Count(p => p.Date >= weekStart && p.Date <= today);
        var sessionsThisMonth = performed.Count(p => p.Date.Year == today.Year && p.Date.Month == today.Month);

        var weeksLive = (int)Math.Ceiling((today.DayNumber - first.DayNumber + 1) / 7.0);
        var weeks = Math.Clamp(weeksLive, 1, 8);
        var windowStart = today.AddDays(-7 * weeks + 1);
        var inWindow = performed.Count(p => p.Date >= windowStart && p.Date <= today);
        var weeklyAverage = Math.Round((double)inWindow / weeks, 1);

        var totalVolume = performed.Sum(p => p.Summary.TotalVolumeKg);

        var durations = performed
            .Where(p => p.Summary.DurationSeconds is not null)
            .Select(p => p.Summary.DurationSeconds!.Value)
            .ToList();
        int? avgDuration = durations.Count > 0 ? (int)Math.Round(durations.Average()) : null;

        var completedSets = performed.Sum(p => p.Summary.CompletedSetCount);
        var skippedSets = performed.Sum(p => p.Summary.SkippedSetCount);
        var actedSets = completedSets + skippedSets;
        var skippedRate = actedSets > 0 ? Math.Round((double)skippedSets / actedSets, 3) : 0;

        var byDay = performed
            .Where(p => p.Session.SourceDayId is not null)
            .GroupBy(p => p.Session.SourceDayId!.Value)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Last: g.Max(x => x.Date)));

        var perDay = days
            .Select(d => byDay.TryGetValue(d.Id, out var v)
                ? new ProgramDayStatResult(d.Id, d.Name, v.Count, v.Last)
                : new ProgramDayStatResult(d.Id, d.Name, 0, null))
            .ToList();

        return new ProgramStatsResult(
            performed.Count, first, last, sessionsThisWeek, sessionsThisMonth,
            weeklyAverage, totalVolume, avgDuration, completedSets, skippedSets, skippedRate, perDay);
    }

    /// <summary>Monday-based week start — matches the dashboard bars and the calendar grid.</summary>
    private static DateOnly StartOfWeek(DateOnly d)
    {
        var offset = ((int)d.DayOfWeek + 6) % 7; // Mon=0 … Sun=6
        return d.AddDays(-offset);
    }

    private readonly record struct Entry(WorkoutSession Session, DateOnly Date, SessionSummaryResult Summary);
}
