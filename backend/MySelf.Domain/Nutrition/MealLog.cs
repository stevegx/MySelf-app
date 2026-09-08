namespace MySelf.Domain.Nutrition;

/// <summary>
/// One meal on a nutrition day — the grouping row for its <see cref="MealLogItem"/>s
/// (docs/04 §11 "Nutrition"). Exactly one row per (user, local date, category); it is created
/// lazily the first time a food is added to that slot and removed when its last item goes.
///
/// <see cref="Category"/> is a plain string snapshot rather than a foreign key: the MVP has
/// four fixed categories, and the later "customizable meal categories" slice adds a
/// MealCategory table without needing to migrate this column. <see cref="UserId"/> is a
/// plain Guid with an index and no navigation property, matching <c>UserGoal</c>.
/// </summary>
public class MealLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The user's local calendar date this meal counts against.</summary>
    public DateOnly LocalDate { get; set; }

    /// <summary>"Breakfast" | "Lunch" | "Dinner" | "Snacks" in the MVP.</summary>
    public required string Category { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<MealLogItem> Items { get; set; } = [];
}
