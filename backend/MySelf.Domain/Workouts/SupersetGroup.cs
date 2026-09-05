namespace MySelf.Domain.Workouts;

/// <summary>
/// Groups two or more <see cref="VariantExercise"/> records into a superset (locked decision
/// #22). It affects execution order and the rest-after-round timer only — it never changes
/// any exercise's prescriptions or calculated metrics. Rest-after-round semantics belong to
/// workout execution (Phase 3); here it is just stored.
/// </summary>
public class SupersetGroup
{
    public Guid Id { get; set; }

    public Guid DayId { get; set; }
    public WorkoutDay Day { get; set; } = null!;

    public int SortOrder { get; set; }
    public int RestAfterRoundSeconds { get; set; }
}
