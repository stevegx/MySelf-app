namespace MySelf.Domain.Identity;

/// <summary>
/// Turns raw body-weight readings into the trend the charts use (docs/08 #30): collapse to
/// one point per calendar day (the mean of that day's readings), then a trailing 7-calendar-
/// day rolling average of those daily points. Pure — no database, no clock — so it is
/// unit-tested directly.
/// </summary>
public static class WeightTrendCalculator
{
    public readonly record struct Reading(DateOnly Date, decimal WeightKg);

    public readonly record struct DayPoint(DateOnly Date, decimal Average, decimal RollingAverage);

    /// <summary>
    /// Ordered daily points. <see cref="DayPoint.Average"/> is that day's mean reading;
    /// <see cref="DayPoint.RollingAverage"/> is the mean of the daily averages over the last
    /// seven calendar days (this day inclusive), so early points simply track the daily line
    /// until a week of history exists.
    /// </summary>
    public static IReadOnlyList<DayPoint> Of(IEnumerable<Reading> readings)
    {
        var daily = readings
            .GroupBy(r => r.Date)
            .Select(g => (Date: g.Key, Average: Math.Round(g.Average(x => x.WeightKg), 2)))
            .OrderBy(x => x.Date)
            .ToList();

        var points = new List<DayPoint>(daily.Count);
        foreach (var (date, average) in daily)
        {
            var windowStart = date.AddDays(-6);
            var rolling = daily
                .Where(d => d.Date >= windowStart && d.Date <= date)
                .Average(d => d.Average);
            points.Add(new DayPoint(date, average, Math.Round(rolling, 2)));
        }

        return points;
    }

    /// <summary>
    /// Change in the rolling average over the last seven days: the final point's rolling
    /// average minus the rolling average of whichever earlier point sits closest to seven
    /// days before it. Null until there are two distinct points to compare.
    /// </summary>
    public static decimal? SevenDayChange(IReadOnlyList<DayPoint> points)
    {
        if (points.Count < 2)
        {
            return null;
        }

        var last = points[^1];
        var target = last.Date.AddDays(-7);
        var earlier = points
            .Take(points.Count - 1)
            .OrderBy(p => Math.Abs(p.Date.DayNumber - target.DayNumber))
            .First();

        return Math.Round(last.RollingAverage - earlier.RollingAverage, 2);
    }
}
