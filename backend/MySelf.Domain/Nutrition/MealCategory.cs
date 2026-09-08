namespace MySelf.Domain.Nutrition;

/// <summary>
/// A user-defined meal slot (docs/08 decision #19 "customizable meal categories"). Every
/// user starts with four seeded rows — Breakfast, Lunch, Dinner, Snacks — and can rename,
/// reorder, add or archive them.
///
/// <see cref="MealLog"/> and <see cref="SavedMeal"/> keep their <c>Category</c> as a plain
/// string, not a foreign key here: a rename or archive must never rewrite or orphan a day
/// that was already logged. This table only drives which slots the day screen shows and
/// which category names new logs may use. <see cref="ArchivedAt"/> is a soft-delete for the
/// same reason as <see cref="CustomFood"/> — the name may still appear in historical logs.
/// <see cref="UserId"/> is a plain Guid + index, no navigation property.
/// </summary>
public class MealCategory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public required string Name { get; set; }

    /// <summary>Ascending display order on the nutrition day (0-based).</summary>
    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>The slots every new user is seeded with, in order.</summary>
    public static readonly string[] Defaults = ["Breakfast", "Lunch", "Dinner", "Snacks"];
}
