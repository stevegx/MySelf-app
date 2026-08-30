namespace MySelf.Domain.Exercises;

/// <summary>
/// Best-effort default <see cref="TrackingMode"/> for an imported exercise, from its wger
/// category and equipment. Deliberately conservative: machine / unknown / no-equipment cases
/// fall back to <see cref="TrackingMode.WeightAndReps"/>. Hand corrections live in
/// <c>backend/seed-data/tracking-mode-overrides.json</c>.
/// </summary>
public static class TrackingModeClassifier
{
    private const string CardioCategory = "Cardio";

    // wger equipment ids that don't add an external load — an exercise using only these
    // (e.g. a mat, a pull-up bar) is bodyweight.
    private static readonly HashSet<int> BodyweightCompatibleWgerIds =
    [
        7, // none (bodyweight exercise)
        4, // Gym mat
        6, // Pull-up bar
        5, // Swiss Ball
    ];

    public static TrackingMode Classify(string categoryName, IReadOnlyCollection<int> equipmentWgerIds)
    {
        if (string.Equals(categoryName, CardioCategory, StringComparison.OrdinalIgnoreCase))
        {
            return TrackingMode.Duration;
        }

        var isBodyweight =
            equipmentWgerIds.Count > 0
            && equipmentWgerIds.All(BodyweightCompatibleWgerIds.Contains);

        return isBodyweight ? TrackingMode.BodyweightReps : TrackingMode.WeightAndReps;
    }
}
