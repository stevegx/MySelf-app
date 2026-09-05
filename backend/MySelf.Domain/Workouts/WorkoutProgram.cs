namespace MySelf.Domain.Workouts;

/// <summary>
/// A user-built workout program (docs/02, locked decision #4/#12). The library of groups and
/// variants the user trains from — no weekdays, no schedule slots. Exactly one program per
/// user may be <see cref="IsActive"/>; any number of drafts and archived programs are allowed.
///
/// <see cref="RowVersion"/> maps to PostgreSQL's system <c>xmin</c> column (Npgsql
/// <c>IsRowVersion()</c>): a concurrency token so a stale edit fails loudly instead of
/// silently clobbering a newer one (docs/04 API conventions).
/// </summary>
public class WorkoutProgram
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public required string Name { get; set; }

    /// <summary>Free-text split label, e.g. "PPL" or "Upper/Lower". Optional.</summary>
    public string? SplitLabel { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Non-null once archived. Archived programs are hidden from the default list but kept.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    public uint RowVersion { get; set; }

    public List<WorkoutDay> Days { get; set; } = [];
}
