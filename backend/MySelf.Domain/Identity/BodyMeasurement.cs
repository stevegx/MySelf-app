namespace MySelf.Domain.Identity;

/// <summary>
/// A body-weight reading (docs/08 locked decisions #4 "canonical kg + UTC timestamps" and
/// #30 "multiple readings per day; charts use the daily average then a 7-day rolling
/// average"). <see cref="LocalDate"/> is the user's own calendar day the reading counts
/// against — set from the client's local date, the same choice as
/// <c>WorkoutSession.PerformedOnLocalDate</c>.
///
/// <see cref="UserId"/> is a plain Guid with an index and no navigation property; the
/// foreign key is declared in <c>BodyMeasurementConfiguration</c>, matching
/// <see cref="UserGoal"/> and <c>RefreshToken</c>.
/// </summary>
public class BodyMeasurement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Canonical weight in kilograms. Decimal, never binary float, per docs/04.</summary>
    public decimal WeightKg { get; set; }

    /// <summary>The user's local calendar date this reading belongs to.</summary>
    public DateOnly LocalDate { get; set; }

    /// <summary>When the reading was recorded, in UTC.</summary>
    public DateTimeOffset MeasuredAt { get; set; }
}
