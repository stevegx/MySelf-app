using MySelf.Domain.Exercises;
using MySelf.Domain.Workouts;

namespace MySelf.UnitTests.Workouts;

/// <summary>
/// Unit tests for the per-tracking-mode required-field rules (docs/02 "Strict set
/// validation", locked decision #10). Pure logic, no database.
/// </summary>
public class SetLogValidationTests
{
    private static SetPerformance P(
        decimal? weightKg = null, decimal? addedWeightKg = null, decimal? assistanceKg = null,
        int? reps = null, int? durationSeconds = null, decimal? distanceMeters = null) =>
        new(weightKg, addedWeightKg, assistanceKg, reps, durationSeconds, distanceMeters);

    [Fact]
    public void WeightAndReps_needs_both_weight_and_reps()
    {
        Assert.Null(SetLogValidation.MissingRequiredField(TrackingMode.WeightAndReps, P(weightKg: 100m, reps: 8)));
        Assert.NotNull(SetLogValidation.MissingRequiredField(TrackingMode.WeightAndReps, P(weightKg: 100m)));
        Assert.NotNull(SetLogValidation.MissingRequiredField(TrackingMode.WeightAndReps, P(reps: 8)));
        Assert.NotNull(SetLogValidation.MissingRequiredField(TrackingMode.WeightAndReps, P()));
    }

    [Theory]
    [InlineData(TrackingMode.BodyweightReps)]
    [InlineData(TrackingMode.RepsOnly)]
    public void Reps_only_modes_need_reps(TrackingMode mode)
    {
        Assert.Null(SetLogValidation.MissingRequiredField(mode, P(reps: 12)));
        Assert.NotNull(SetLogValidation.MissingRequiredField(mode, P()));
    }

    [Fact]
    public void BodyweightPlusWeight_needs_added_weight_and_reps()
    {
        Assert.Null(SetLogValidation.MissingRequiredField(TrackingMode.BodyweightPlusWeight, P(addedWeightKg: 20m, reps: 8)));
        Assert.NotNull(SetLogValidation.MissingRequiredField(TrackingMode.BodyweightPlusWeight, P(reps: 8)));
        // A plain weightKg isn't the same field as addedWeightKg.
        Assert.NotNull(SetLogValidation.MissingRequiredField(TrackingMode.BodyweightPlusWeight, P(weightKg: 20m, reps: 8)));
    }

    [Fact]
    public void AssistanceReps_needs_assistance_and_reps()
    {
        Assert.Null(SetLogValidation.MissingRequiredField(TrackingMode.AssistanceReps, P(assistanceKg: 15m, reps: 8)));
        Assert.NotNull(SetLogValidation.MissingRequiredField(TrackingMode.AssistanceReps, P(assistanceKg: 15m)));
    }

    [Fact]
    public void Duration_needs_a_positive_duration()
    {
        Assert.Null(SetLogValidation.MissingRequiredField(TrackingMode.Duration, P(durationSeconds: 45)));
        Assert.NotNull(SetLogValidation.MissingRequiredField(TrackingMode.Duration, P(durationSeconds: 0)));
        Assert.NotNull(SetLogValidation.MissingRequiredField(TrackingMode.Duration, P()));
    }

    [Fact]
    public void OutOfRange_flags_impossible_values()
    {
        Assert.NotNull(SetLogValidation.OutOfRange(P(reps: -1)));
        Assert.NotNull(SetLogValidation.OutOfRange(P(reps: 2000)));
        Assert.NotNull(SetLogValidation.OutOfRange(P(weightKg: -5m)));
        Assert.NotNull(SetLogValidation.OutOfRange(P(weightKg: 5000m)));
        Assert.NotNull(SetLogValidation.OutOfRange(P(assistanceKg: -1m)));
        Assert.NotNull(SetLogValidation.OutOfRange(P(durationSeconds: 90_000)));
        Assert.NotNull(SetLogValidation.OutOfRange(P(distanceMeters: -1m)));
    }

    [Fact]
    public void OutOfRange_passes_realistic_values()
    {
        Assert.Null(SetLogValidation.OutOfRange(P(weightKg: 140m, reps: 10)));
        Assert.Null(SetLogValidation.OutOfRange(P(durationSeconds: 90)));
    }
}
