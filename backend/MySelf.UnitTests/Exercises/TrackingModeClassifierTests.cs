using MySelf.Domain.Exercises;

namespace MySelf.UnitTests.Exercises;

public class TrackingModeClassifierTests
{
    [Theory]
    // Cardio always wins, regardless of equipment.
    [InlineData("Cardio", new int[0], TrackingMode.Duration)]
    [InlineData("Cardio", new[] { 1 }, TrackingMode.Duration)]
    // Loaded equipment -> weight & reps.
    [InlineData("Legs", new[] { 1 }, TrackingMode.WeightAndReps)] // Barbell
    [InlineData("Back", new[] { 6, 3 }, TrackingMode.WeightAndReps)] // Pull-up bar + Dumbbell -> loaded
    // Only bodyweight-compatible equipment -> bodyweight reps.
    [InlineData("Abs", new[] { 7 }, TrackingMode.BodyweightReps)] // none (bodyweight)
    [InlineData("Abs", new[] { 4 }, TrackingMode.BodyweightReps)] // Gym mat
    [InlineData("Back", new[] { 6 }, TrackingMode.BodyweightReps)] // Pull-up bar
    // No equipment listed at all -> conservative default.
    [InlineData("Legs", new int[0], TrackingMode.WeightAndReps)]
    public void Classify_returns_expected(string category, int[] equipment, TrackingMode expected)
    {
        Assert.Equal(expected, TrackingModeClassifier.Classify(category, equipment));
    }
}
