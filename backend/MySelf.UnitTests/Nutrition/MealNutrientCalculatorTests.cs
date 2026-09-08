using MySelf.Domain.Nutrition;
using N = MySelf.Domain.Nutrition.MealNutrientCalculator.Nutrients;

namespace MySelf.UnitTests.Nutrition;

/// <summary>
/// Unit tests for scaling a food's declared nutrients to the eaten amount (docs/03 §8.7)
/// and rolling items into totals.
/// </summary>
public class MealNutrientCalculatorTests
{
    // Per 100 g: 52 kcal, 3.4 P, 5.0 C, 1.7 F (a plain-yogurt-ish food).
    private static readonly N Yogurt100 = new(52m, 3.4m, 5.0m, 1.7m);

    [Fact]
    public void Amount_factor_scales_by_basis_and_unit()
    {
        decimal F(ServingBasis b, MealAmountUnit u, decimal amount, decimal? size) =>
            MealNutrientCalculator.AmountFactor(b, u, amount, size);

        Assert.Equal(1.5m, F(ServingBasis.Per100g, MealAmountUnit.Grams, 150m, null));
        Assert.Equal(2.0m, F(ServingBasis.Per100g, MealAmountUnit.Millilitres, 200m, null));
        Assert.Equal(2.4m, F(ServingBasis.Per100g, MealAmountUnit.Serving, 2m, 120m)); // 2 × 120 g / 100
        Assert.Equal(2.0m, F(ServingBasis.PerServing, MealAmountUnit.Serving, 2m, null));
        Assert.Equal(2.0m, F(ServingBasis.PerServing, MealAmountUnit.Grams, 300m, 150m)); // 300 g / 150 g-serving
    }

    [Fact]
    public void Per_100g_food_logged_in_grams_scales_and_rounds_to_a_tenth()
    {
        var n = MealNutrientCalculator.ForAmount(Yogurt100, ServingBasis.Per100g, MealAmountUnit.Grams, 175m, null);

        // 1.75× → 91, 5.95, 8.75, 2.975 → rounded to 0.1
        Assert.Equal(91.0m, n.Kcal);
        Assert.Equal(6.0m, n.ProteinG);
        Assert.Equal(8.8m, n.CarbG);
        Assert.Equal(3.0m, n.FatG);
    }

    [Fact]
    public void A_serving_to_mass_conversion_without_a_serving_size_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MealNutrientCalculator.AmountFactor(ServingBasis.PerServing, MealAmountUnit.Grams, 100m, null));

        Assert.Throws<InvalidOperationException>(() =>
            MealNutrientCalculator.AmountFactor(ServingBasis.Per100g, MealAmountUnit.Serving, 1m, 0m));
    }

    [Fact]
    public void A_negative_amount_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MealNutrientCalculator.AmountFactor(ServingBasis.Per100g, MealAmountUnit.Grams, -10m, null));
    }

    [Fact]
    public void Totals_sum_the_scaled_items_and_round()
    {
        var total = MealNutrientCalculator.Total([
            new N(91.0m, 6.0m, 8.8m, 3.0m),
            new N(120.5m, 20.0m, 0.3m, 4.1m),
        ]);

        Assert.Equal(211.5m, total.Kcal);
        Assert.Equal(26.0m, total.ProteinG);
        Assert.Equal(9.1m, total.CarbG);
        Assert.Equal(7.1m, total.FatG);
    }
}
