namespace MySelf.Domain.Nutrition;

/// <summary>
/// A food the user saved for reuse — "My Foods" (docs/03 §8.7, docs/04 §11 "CustomFood").
/// Its nutrients are declared per <see cref="ServingBasis"/>, exactly like a
/// <see cref="MealLogItem"/>; logging one copies these values into an item snapshot, so
/// editing the food afterwards never changes past logs.
///
/// <see cref="ArchivedAt"/> is a soft-delete (docs/04 "ArchivedAt for reusable user
/// content") so a saved food can leave the picker without orphaning history that referenced
/// it by value. <see cref="UserId"/> is a plain Guid + index, no navigation property.
/// </summary>
public class CustomFood
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public required string Name { get; set; }
    public string? Brand { get; set; }

    /// <summary>Set when this food was created from a barcode result (docs/03 §8.7).</summary>
    public string? Barcode { get; set; }

    public ServingBasis ServingBasis { get; set; }
    public decimal? ServingSizeGrams { get; set; }

    // Declared nutrients, per the basis above.
    public decimal Kcal { get; set; }
    public decimal ProteinG { get; set; }
    public decimal CarbG { get; set; }
    public decimal FatG { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}
