namespace MySelf.Domain.Nutrition;

/// <summary>
/// Scales a food's declared nutrients to the amount actually eaten, and rolls items up into
/// meal / day totals (docs/03 §8.5, §8.7). Pure — no database, no clock — so it is
/// unit-tested directly. Energy conversions themselves (4/4/9 kcal per g) are not re-derived
/// here: the caller supplies the food's own kcal figure.
/// </summary>
public static class MealNutrientCalculator
{
    public readonly record struct Nutrients(decimal Kcal, decimal ProteinG, decimal CarbG, decimal FatG)
    {
        public static Nutrients Zero => default;

        public Nutrients Plus(Nutrients o) =>
            new(Kcal + o.Kcal, ProteinG + o.ProteinG, CarbG + o.CarbG, FatG + o.FatG);

        public Nutrients Scale(decimal f) =>
            new(Kcal * f, ProteinG * f, CarbG * f, FatG * f);

        public Nutrients Rounded(int decimals = 1) => new(
            Math.Round(Kcal, decimals),
            Math.Round(ProteinG, decimals),
            Math.Round(CarbG, decimals),
            Math.Round(FatG, decimals));
    }

    /// <summary>
    /// The multiplier applied to the per-basis nutrients for the eaten amount. A
    /// serving↔mass conversion needs a positive <paramref name="servingSizeGrams"/>;
    /// without it an <see cref="InvalidOperationException"/> is thrown.
    /// </summary>
    public static decimal AmountFactor(
        ServingBasis basis, MealAmountUnit unit, decimal amount, decimal? servingSizeGrams)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        }

        return (basis, unit) switch
        {
            (ServingBasis.Per100g, MealAmountUnit.Serving) => amount * RequireSize(servingSizeGrams) / 100m,
            (ServingBasis.Per100g, _) => amount / 100m,
            (ServingBasis.PerServing, MealAmountUnit.Serving) => amount,
            (ServingBasis.PerServing, _) => amount / RequireSize(servingSizeGrams),
            _ => throw new InvalidOperationException("Unknown basis/unit combination."),
        };

        static decimal RequireSize(decimal? grams) => grams is > 0
            ? grams.Value
            : throw new InvalidOperationException("A serving size in grams is required for this unit.");
    }

    /// <summary>Declared nutrients (per basis) scaled to the eaten amount, rounded to 0.1.</summary>
    public static Nutrients ForAmount(
        Nutrients perBasis,
        ServingBasis basis,
        MealAmountUnit unit,
        decimal amount,
        decimal? servingSizeGrams) =>
        perBasis.Scale(AmountFactor(basis, unit, amount, servingSizeGrams)).Rounded();

    /// <summary>Sum of already-scaled item nutrients, rounded to 0.1.</summary>
    public static Nutrients Total(IEnumerable<Nutrients> items) =>
        items.Aggregate(Nutrients.Zero, (acc, n) => acc.Plus(n)).Rounded();
}
