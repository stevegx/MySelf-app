using MySelf.Domain.Workouts;

namespace MySelf.UnitTests.Workouts;

/// <summary>
/// PR detection rules (docs/02 §7). A record only counts when it is strictly greater than
/// the prior best — "matched previous best" is not a PR.
/// </summary>
public class PersonalRecordDetectorTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Exercise = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly DateOnly On = new(2026, 9, 5);
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CompletedLift Lift(decimal weight, int reps) => new(Guid.NewGuid(), weight, reps);

    private static IReadOnlyList<PersonalRecord> Detect(
        IReadOnlyCollection<CompletedLift> lifts, IReadOnlyCollection<PersonalRecord> existing) =>
        PersonalRecordDetector.Detect(User, Exercise, Session, On, Now, lifts, existing);

    [Fact]
    public void From_nothing_the_first_session_sets_every_applicable_record()
    {
        var prs = Detect([Lift(100m, 5), Lift(90m, 8)], []);

        var types = prs.Select(p => p.Type).ToHashSet();
        Assert.Contains(PersonalRecordType.HeaviestWeight, types);
        Assert.Contains(PersonalRecordType.BestEstimatedOneRepMax, types);
        Assert.Contains(PersonalRecordType.BestExerciseVolume, types);
        Assert.Contains(PersonalRecordType.MostRepsAtWeight, types);

        Assert.Equal(100m, prs.Single(p => p.Type == PersonalRecordType.HeaviestWeight).Value);
        Assert.Equal(100m * 5 + 90m * 8, prs.Single(p => p.Type == PersonalRecordType.BestExerciseVolume).Value);
    }

    [Fact]
    public void Nothing_new_when_the_session_only_matches_the_prior_bests()
    {
        var existing = new[]
        {
            new PersonalRecord { Type = PersonalRecordType.HeaviestWeight, Value = 100m },
            new PersonalRecord { Type = PersonalRecordType.BestEstimatedOneRepMax, Value = StrengthMath.EstimatedOneRepMax(100m, 5) },
            new PersonalRecord { Type = PersonalRecordType.BestExerciseVolume, Value = 500m },
            new PersonalRecord { Type = PersonalRecordType.MostRepsAtWeight, Value = 5, WeightKg = 100m },
        };

        var prs = Detect([Lift(100m, 5)], existing);

        Assert.Empty(prs);
    }

    [Fact]
    public void A_heavier_top_set_is_a_heaviest_weight_and_e1rm_pr()
    {
        var existing = new[]
        {
            new PersonalRecord { Type = PersonalRecordType.HeaviestWeight, Value = 100m },
            new PersonalRecord { Type = PersonalRecordType.BestEstimatedOneRepMax, Value = 116.67m },
        };

        var prs = Detect([Lift(105m, 5)], existing);

        Assert.Contains(prs, p => p.Type == PersonalRecordType.HeaviestWeight && p.Value == 105m);
        Assert.Contains(prs, p => p.Type == PersonalRecordType.BestEstimatedOneRepMax);
    }

    [Fact]
    public void Reps_at_weight_is_compared_per_weight()
    {
        var existing = new[]
        {
            new PersonalRecord { Type = PersonalRecordType.MostRepsAtWeight, Value = 8, WeightKg = 100m },
        };

        // 9 reps at 100 kg beats the record; 5 reps at 80 kg has no prior record, so it sets one.
        var prs = Detect([Lift(100m, 9), Lift(80m, 5)], existing);

        var repRecords = prs.Where(p => p.Type == PersonalRecordType.MostRepsAtWeight).ToList();
        Assert.Contains(repRecords, p => p.WeightKg == 100m && p.Value == 9);
        Assert.Contains(repRecords, p => p.WeightKg == 80m && p.Value == 5);
    }
}
