using MySelf.Domain.Identity;

namespace MySelf.UnitTests.Identity;

/// <summary>
/// Unit tests for the body-weight trend (docs/08 #30): daily average first, then a trailing
/// 7-calendar-day rolling average of those daily points.
/// </summary>
public class WeightTrendCalculatorTests
{
    private static WeightTrendCalculator.Reading R(int dayOfMonth, decimal kg) =>
        new(new DateOnly(2026, 9, dayOfMonth), kg);

    [Fact]
    public void No_readings_produce_no_points()
    {
        Assert.Empty(WeightTrendCalculator.Of([]));
    }

    [Fact]
    public void Several_readings_on_a_day_collapse_to_that_day_s_average()
    {
        var points = WeightTrendCalculator.Of([R(1, 80.0m), R(1, 81.0m), R(1, 82.0m)]);

        var point = Assert.Single(points);
        Assert.Equal(new DateOnly(2026, 9, 1), point.Date);
        Assert.Equal(81.0m, point.Average);
        Assert.Equal(81.0m, point.RollingAverage); // only day in the window
    }

    [Fact]
    public void Rolling_average_covers_the_trailing_seven_calendar_days()
    {
        // Days 1..8, one reading each, ascending. Day 8's window is days 2..8.
        var points = WeightTrendCalculator.Of(
            Enumerable.Range(1, 8).Select(d => R(d, 80m + d)).ToList());

        Assert.Equal(8, points.Count);
        Assert.Equal(88m, points[^1].Average); // 80 + 8
        // mean of days 2..8 daily averages = mean(82..88) = 85
        Assert.Equal(85m, points[^1].RollingAverage);
    }

    [Fact]
    public void Rolling_average_ignores_gaps_wider_than_the_window()
    {
        // A reading on day 1, then nothing until day 20 — day 20's window is just day 20.
        var points = WeightTrendCalculator.Of([R(1, 90m), R(20, 84m)]);

        Assert.Equal(2, points.Count);
        Assert.Equal(84m, points[^1].RollingAverage);
    }

    [Fact]
    public void Seven_day_change_compares_the_rolling_average_to_a_week_earlier()
    {
        // Steady 1 kg/day loss over 14 days: rolling avg on day 14 vs day 7 is -7 kg.
        var points = WeightTrendCalculator.Of(
            Enumerable.Range(1, 14).Select(d => R(d, 100m - d)).ToList());

        var change = WeightTrendCalculator.SevenDayChange(points);
        Assert.Equal(-7m, change);
    }

    [Fact]
    public void Seven_day_change_is_null_with_a_single_point()
    {
        var points = WeightTrendCalculator.Of([R(1, 80m)]);
        Assert.Null(WeightTrendCalculator.SevenDayChange(points));
    }
}
