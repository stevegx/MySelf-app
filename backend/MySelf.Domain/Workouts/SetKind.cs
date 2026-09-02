namespace MySelf.Domain.Workouts;

/// <summary>
/// The shape of a prescribed set (locked decision #14). AMRAP and "to failure" are flags on
/// <see cref="SetPrescription"/> rather than kinds; there is deliberately no warm-up kind.
/// </summary>
public enum SetKind
{
    Standard,
    Drop,
}
