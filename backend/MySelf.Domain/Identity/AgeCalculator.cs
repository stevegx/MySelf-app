namespace MySelf.Domain.Identity;

/// <summary>
/// Whole-years age from a date of birth. Pure — the caller passes in "today" (from the
/// request clock) rather than this reaching for <c>DateTime.Now</c>, so it stays testable.
/// </summary>
public static class AgeCalculator
{
    public static int Years(DateOnly dateOfBirth, DateOnly asOf)
    {
        var age = asOf.Year - dateOfBirth.Year;
        if (dateOfBirth > asOf.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
