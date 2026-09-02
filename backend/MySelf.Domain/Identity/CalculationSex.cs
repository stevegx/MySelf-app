namespace MySelf.Domain.Identity;

/// <summary>
/// The sex constant the Mifflin–St Jeor BMR equation needs (docs/03 §8.2: the formula has a
/// sex-specific term). It is deliberately narrow — only the calorie estimator ever reads it —
/// and is null on <see cref="UserProfile"/> when the user chose "I prefer not to use this
/// calculation" (docs/01 step 1).
/// </summary>
public enum CalculationSex
{
    Male,
    Female,
}
