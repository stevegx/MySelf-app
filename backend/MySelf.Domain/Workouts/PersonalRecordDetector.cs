namespace MySelf.Domain.Workouts;

/// <summary>One completed weight-and-reps set, reduced to what PR detection needs.</summary>
public readonly record struct CompletedLift(Guid SetLogId, decimal WeightKg, int Reps);

/// <summary>
/// Given the weight-and-reps sets completed for one exercise in a session and the user's
/// existing PRs for that exercise, works out which new PRs the session set (docs/02 §7).
/// Pure. A result only counts when it is <em>strictly greater</em> than the prior best —
/// "matched previous best" is not a PR.
/// </summary>
public static class PersonalRecordDetector
{
    public static IReadOnlyList<PersonalRecord> Detect(
        Guid userId,
        Guid exerciseId,
        Guid sessionId,
        DateOnly achievedOn,
        DateTimeOffset now,
        IReadOnlyCollection<CompletedLift> lifts,
        IReadOnlyCollection<PersonalRecord> existing)
    {
        if (lifts.Count == 0)
        {
            return [];
        }

        var found = new List<PersonalRecord>();

        PersonalRecord New(PersonalRecordType type, decimal value, decimal? weightKg, int? reps, Guid? setLogId) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExerciseId = exerciseId,
            Type = type,
            Value = value,
            WeightKg = weightKg,
            Reps = reps,
            AchievedOn = achievedOn,
            SessionId = sessionId,
            SetLogId = setLogId,
            CreatedAt = now,
        };

        decimal Best(PersonalRecordType type, Func<PersonalRecord, bool>? where = null) =>
            existing.Where(p => p.Type == type && (where is null || where(p)))
                .Select(p => p.Value)
                .DefaultIfEmpty(0m)
                .Max();

        // Heaviest weight.
        var heaviest = lifts.MaxBy(l => l.WeightKg);
        if (heaviest.WeightKg > Best(PersonalRecordType.HeaviestWeight))
        {
            found.Add(New(PersonalRecordType.HeaviestWeight, heaviest.WeightKg, heaviest.WeightKg, heaviest.Reps, heaviest.SetLogId));
        }

        // Highest estimated 1RM.
        var topE1Rm = lifts
            .Select(l => (l, e1rm: StrengthMath.EstimatedOneRepMax(l.WeightKg, l.Reps)))
            .MaxBy(x => x.e1rm);
        if (topE1Rm.e1rm > Best(PersonalRecordType.BestEstimatedOneRepMax))
        {
            found.Add(New(PersonalRecordType.BestEstimatedOneRepMax, decimal.Round(topE1Rm.e1rm, 2),
                topE1Rm.l.WeightKg, topE1Rm.l.Reps, topE1Rm.l.SetLogId));
        }

        // Most reps at a given weight — best per distinct weight in this session.
        foreach (var group in lifts.GroupBy(l => l.WeightKg))
        {
            var bestAtWeight = group.MaxBy(l => l.Reps);
            var prior = Best(PersonalRecordType.MostRepsAtWeight, p => p.WeightKg == group.Key);
            if (bestAtWeight.Reps > prior)
            {
                found.Add(New(PersonalRecordType.MostRepsAtWeight, bestAtWeight.Reps, group.Key, bestAtWeight.Reps, bestAtWeight.SetLogId));
            }
        }

        // Highest single-session volume for the exercise.
        var sessionVolume = lifts.Sum(l => StrengthMath.SetVolume(l.WeightKg, l.Reps));
        if (sessionVolume > Best(PersonalRecordType.BestExerciseVolume))
        {
            found.Add(New(PersonalRecordType.BestExerciseVolume, sessionVolume, null, null, null));
        }

        return found;
    }
}
