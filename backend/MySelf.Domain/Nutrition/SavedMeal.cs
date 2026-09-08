namespace MySelf.Domain.Nutrition;

/// <summary>
/// A reusable combination of foods for quick logging (docs/03 §8.8, docs/04 §11 "SavedMeal").
/// It is a <em>template</em>: adding it to a day copies its items into independent
/// <see cref="MealLogItem"/> snapshots, so editing or archiving the saved meal later never
/// touches days already logged from it.
///
/// <see cref="ArchivedAt"/> is a soft-delete for the same reason as <see cref="CustomFood"/>.
/// <see cref="Category"/> is the meal slot it defaults into. <see cref="UserId"/> is a plain
/// Guid + index, no navigation property.
/// </summary>
public class SavedMeal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public required string Name { get; set; }

    /// <summary>The meal slot this saved meal drops into by default ("Breakfast"…"Snacks").</summary>
    public required string Category { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }

    public List<SavedMealItem> Items { get; set; } = [];
}
