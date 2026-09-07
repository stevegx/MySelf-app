namespace MySelf.Api.Me;

/// <summary>Body of <c>POST /api/v1/me/body-measurements</c>. Weight in kilograms;
/// <see cref="LocalDate"/> defaults to the caller's today when omitted.</summary>
public sealed record LogBodyMeasurementRequest(decimal WeightKg, DateOnly? LocalDate);

/// <summary>One stored reading (<c>GET /api/v1/me/body-measurements</c>), newest first.</summary>
public sealed record BodyMeasurementItem(Guid Id, decimal WeightKg, DateOnly LocalDate, DateTimeOffset MeasuredAt);

/// <summary>
/// <c>GET /api/v1/analytics/weight</c>: the daily-average line plus its 7-day rolling
/// average (docs/08 #30), and a headline latest value / 7-day direction for the dashboard.
/// </summary>
public sealed record WeightTrendResult(
    DateOnly? From,
    DateOnly? To,
    decimal? Latest,
    DateOnly? LatestOn,
    // Rolling-average change over the last seven days (kg). Negative = trending down. Null
    // until there are two rolling points to compare.
    decimal? SevenDayChangeKg,
    IReadOnlyList<WeightTrendPoint> Points);

public sealed record WeightTrendPoint(DateOnly Date, decimal Average, decimal RollingAverage);
