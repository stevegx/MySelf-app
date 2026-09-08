namespace MySelf.Domain.Nutrition;

/// <summary>
/// One logged food inside a <see cref="MealLog"/> (docs/04 §11 "MealLogItem: food snapshot,
/// amountGrams/servings, calculated nutrients"). Everything here is a snapshot taken when the
/// food was logged — editing or deleting the source food later never rewrites this row
/// (docs/03 "Κρίσιμη αρχή: snapshots").
///
/// The <c>Basis*</c> fields are the food's declared nutrients per <see cref="ServingBasis"/>;
/// the plain <c>Kcal</c>/<c>ProteinG</c>/… are those scaled to the eaten <see cref="Amount"/>
/// and stored so a day's totals are a simple SUM.
/// </summary>
public class MealLogItem
{
    public Guid Id { get; set; }
    public Guid MealLogId { get; set; }
    public MealLog MealLog { get; set; } = null!;

    /// <summary>Position within the meal (docs/04 "SortOrder on every reorderable child").</summary>
    public int SortOrder { get; set; }

    public required string Name { get; set; }

    public ServingBasis ServingBasis { get; set; }

    /// <summary>Grams one serving weighs. Needed only when the logged unit and the basis
    /// disagree (e.g. per-serving food logged in grams). Null otherwise.</summary>
    public decimal? ServingSizeGrams { get; set; }

    // Declared nutrients, per the basis above.
    public decimal BasisKcal { get; set; }
    public decimal BasisProteinG { get; set; }
    public decimal BasisCarbG { get; set; }
    public decimal BasisFatG { get; set; }

    public decimal Amount { get; set; }
    public MealAmountUnit Unit { get; set; }

    // Nutrients for the eaten amount (Basis* scaled by the amount factor).
    public decimal Kcal { get; set; }
    public decimal ProteinG { get; set; }
    public decimal CarbG { get; set; }
    public decimal FatG { get; set; }

    public DateTimeOffset LoggedAt { get; set; }
}
