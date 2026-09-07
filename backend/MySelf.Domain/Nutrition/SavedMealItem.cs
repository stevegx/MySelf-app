namespace MySelf.Domain.Nutrition;

/// <summary>
/// One food inside a <see cref="SavedMeal"/> template (docs/04 §11 "SavedMealItem"). The
/// nutrients are stored inline per <see cref="ServingBasis"/> — the same shape as a
/// <see cref="MealLogItem"/> or a <see cref="CustomFood"/> — with a default amount that the
/// user can scale (a multiplier) or fine-tune per day after adding.
/// </summary>
public class SavedMealItem
{
    public Guid Id { get; set; }
    public Guid SavedMealId { get; set; }
    public SavedMeal SavedMeal { get; set; } = null!;

    public int SortOrder { get; set; }

    public required string Name { get; set; }

    public ServingBasis ServingBasis { get; set; }
    public decimal? ServingSizeGrams { get; set; }

    // Declared nutrients, per the basis above.
    public decimal BasisKcal { get; set; }
    public decimal BasisProteinG { get; set; }
    public decimal BasisCarbG { get; set; }
    public decimal BasisFatG { get; set; }

    /// <summary>The amount this item contributes at multiplier 1×.</summary>
    public decimal DefaultAmount { get; set; }
    public MealAmountUnit Unit { get; set; }
}
