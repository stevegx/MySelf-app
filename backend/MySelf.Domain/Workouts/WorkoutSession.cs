namespace MySelf.Domain.Workouts;

/// <summary>
/// An actual, manually-chosen workout, in progress or completed (docs/02 "Starting a
/// workout", docs/04 §11, locked decisions #26–27). Only one <see cref="SessionStatus.InProgress"/>
/// session may exist per user at a time — enforced by a filtered unique index, same pattern
/// as the one-active-program rule.
///
/// <see cref="SourceDayId"/> is kept only as a soft "started from this day" pointer;
/// nothing here re-reads the live day. <see cref="DayName"/>/<see cref="ProgramName"/> and
/// every <see cref="ExerciseLog"/>/<see cref="SetLog"/> below are captured at start, so later
/// edits to the program never rewrite this session's history.
/// </summary>
public class WorkoutSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Null for an ad-hoc session with no source day.</summary>
    public Guid? SourceDayId { get; set; }

    /// <summary>The program the source day belonged to at start time. A soft pointer like
    /// <see cref="SourceDayId"/> (no FK) — kept so a program's Overview can gather its own
    /// sessions even after the day or program is edited. Null for an ad-hoc session.</summary>
    public Guid? SourceProgramId { get; set; }

    public string? DayName { get; set; }
    public string? ProgramName { get; set; }

    public SessionStatus Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The user's local calendar date this session counts against (locked decision
    /// #8); set on completion, not at start.</summary>
    public DateOnly? PerformedOnLocalDate { get; set; }

    public string? Notes { get; set; }

    public List<ExerciseLog> ExerciseLogs { get; set; } = [];
}
