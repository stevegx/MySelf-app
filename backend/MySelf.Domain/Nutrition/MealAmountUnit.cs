namespace MySelf.Domain.Nutrition;

/// <summary>
/// The unit a logged quantity is expressed in (docs/03 §8.7: "grams, millilitres or
/// servings"). Millilitres are treated as grams for the nutrient maths in the MVP.
/// </summary>
public enum MealAmountUnit
{
    Grams,
    Millilitres,
    Serving,
}
