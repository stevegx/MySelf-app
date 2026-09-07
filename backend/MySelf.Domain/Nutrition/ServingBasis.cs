namespace MySelf.Domain.Nutrition;

/// <summary>
/// What a food's declared nutrients are measured against (docs/03 §8.7): per 100 g/ml, or
/// per one serving. Stored as a string on <see cref="MealLogItem"/>.
/// </summary>
public enum ServingBasis
{
    Per100g,
    PerServing,
}
