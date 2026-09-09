using System.Globalization;

namespace MySelf.Api.Me;

/// <summary>
/// The derived numbers behind the check-in workbook (<see cref="DataExportSpreadsheet"/>):
/// daily/weekly nutrition vs target, per-session and per-exercise training rollups, and a
/// body-weight trend. Pure computation over a <see cref="DataExport"/>.
/// </summary>
internal sealed record CheckInModel(
    string? Username,
    string? Email,
    DateTimeOffset ExportedAt,
    (DateOnly From, DateOnly To) Range,
    int? CalorieTarget,
    decimal? LatestWeightKg,
    NutritionAgg Nutrition7,
    NutritionAgg NutritionAll,
    decimal? ProteinPerKg,
    string MacroSplit,
    string BestDayLabel,
    IReadOnlyList<DailyNutritionRow> DailyNutrition,
    IReadOnlyList<WeeklyNutritionRow> WeeklyNutrition,
    TrainingAgg Training28,
    int PrCount,
    IReadOnlyList<SessionRow> Sessions,
    IReadOnlyList<ExerciseRow> Exercises,
    decimal? Weight7Avg,
    decimal? WeeklyRateKg,
    decimal? WeightChangeKg,
    IReadOnlyList<WeightPoint> WeightSeries)
{
    public static CheckInModel From(DataExport e)
    {
        var today = DateOnly.FromDateTime(e.ExportedAt.UtcDateTime);
        var target = e.Goals.LastOrDefault()?.CalorieTarget;

        var weightSeries = BuildWeightSeries(e);
        var latestWeight = weightSeries.Count > 0 ? weightSeries[^1].WeightKg : (decimal?)null;

        var daily = BuildDailyNutrition(e, target);
        var last7 = daily.OrderByDescending(d => d.Date).Take(7).ToList();

        var nutrition7 = Aggregate(last7, target);
        var nutritionAll = Aggregate(daily, target);

        var avgP7 = last7.Count > 0 ? last7.Average(d => d.Protein) : (decimal?)null;
        var proteinPerKg = avgP7 is { } p && latestWeight is { } w and > 0 ? Math.Round(p / w, 2) : (decimal?)null;

        var sessions = BuildSessions(e);
        var t28 = AggregateTraining(sessions.Where(s => s.Date >= today.AddDays(-28)).ToList());

        var ranged = e.MealLogs.Select(l => l.LocalDate)
            .Concat(sessions.Select(s => s.Date))
            .Concat(weightSeries.Select(x => x.Date))
            .ToList();
        var range = ranged.Count > 0 ? (ranged.Min(), ranged.Max()) : (default, default);

        return new CheckInModel(
            e.Account.Username,
            e.Account.Email,
            e.ExportedAt,
            range,
            target,
            latestWeight,
            nutrition7,
            nutritionAll,
            proteinPerKg,
            MacroSplitLabel(last7),
            BestDayLabelFor(daily),
            daily.OrderBy(d => d.Date).ToList(),
            BuildWeeklyNutrition(daily),
            t28,
            e.PersonalRecords.Count,
            sessions.OrderBy(s => s.Date).ToList(),
            BuildExercises(e),
            weightSeries.Count > 0 ? weightSeries[^1].Avg7 : null,
            weightSeries.Count > 0 ? weightSeries[^1].WeeklyRate : null,
            weightSeries.Count > 0 ? weightSeries[^1].WeightKg - weightSeries[0].WeightKg : null,
            weightSeries);
    }

    // -------- nutrition

    private static List<DailyNutritionRow> BuildDailyNutrition(DataExport e, int? target) =>
        e.MealLogs
            .GroupBy(l => l.LocalDate)
            .Select(g =>
            {
                var items = g.SelectMany(l => l.Items).ToList();
                var kcal = items.Sum(i => i.Kcal);
                var protein = items.Sum(i => i.ProteinG);
                var carb = items.Sum(i => i.CarbG);
                var fat = items.Sum(i => i.FatG);

                decimal? pct = target is { } t and > 0 ? Math.Round(kcal / t, 3) : null;
                var (status, rank) = Status(kcal, target);

                var macroKcal = protein * 4 + carb * 4 + fat * 9;
                var split = macroKcal > 0
                    ? (Math.Round(protein * 4 / macroKcal, 2), Math.Round(carb * 4 / macroKcal, 2), Math.Round(fat * 9 / macroKcal, 2))
                    : (0m, 0m, 0m);

                return new DailyNutritionRow(
                    g.Key, kcal, protein, carb, fat, target, pct, split, items.Count, status, rank);
            })
            .OrderBy(d => d.Date)
            .ToList();

    private static (string Label, byte Rank) Status(decimal kcal, int? target)
    {
        if (target is not { } t || t <= 0)
        {
            return ("No target", 1);
        }

        var delta = kcal - t;
        var pct = kcal / t;
        if (Math.Abs(delta) <= t * 0.10m) return ("On target", (byte)2);
        if (delta > 0 && pct <= 1.20m) return ("Slightly over", (byte)1);
        if (delta < 0 && pct >= 0.80m) return ("Slightly under", (byte)1);
        return delta > 0 ? ("Over", (byte)0) : ("Under", (byte)0);
    }

    private static NutritionAgg Aggregate(IReadOnlyCollection<DailyNutritionRow> days, int? target)
    {
        if (days.Count == 0)
        {
            return new NutritionAgg(null, null, null, 0);
        }

        decimal? adherence = target is > 0
            ? Math.Round((decimal)days.Count(d => d.StatusRank == 2) / days.Count, 3)
            : null;

        return new NutritionAgg(
            Math.Round(days.Average(d => d.Kcal), 0),
            Math.Round(days.Average(d => d.Protein), 1),
            adherence,
            days.Count);
    }

    private static string MacroSplitLabel(IReadOnlyCollection<DailyNutritionRow> days)
    {
        if (days.Count == 0)
        {
            return "—";
        }

        var p = days.Average(d => d.Protein) * 4;
        var c = days.Average(d => d.Carb) * 4;
        var f = days.Average(d => d.Fat) * 9;
        var total = p + c + f;
        return total > 0
            ? $"{p / total:0%} / {c / total:0%} / {f / total:0%}"
            : "—";
    }

    private static string BestDayLabelFor(IReadOnlyList<DailyNutritionRow> days)
    {
        var withTarget = days.Where(d => d.TargetKcal is { } t && t > 0).ToList();
        if (withTarget.Count == 0)
        {
            return "set a calorie target";
        }

        var best = withTarget.OrderBy(d => Math.Abs(d.Kcal - d.TargetKcal!.Value)).First();
        return $"{best.Date:yyyy-MM-dd}  ({best.Kcal:#,##0} vs {best.TargetKcal:#,##0})";
    }

    private static List<WeeklyNutritionRow> BuildWeeklyNutrition(IReadOnlyList<DailyNutritionRow> days) =>
        days
            .GroupBy(d =>
            {
                var dt = d.Date.ToDateTime(TimeOnly.MinValue);
                return (ISOWeek.GetYear(dt), ISOWeek.GetWeekOfYear(dt));
            })
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var list = g.ToList();
                var hasTarget = list.Any(d => d.TargetKcal is > 0);
                return new WeeklyNutritionRow(
                    $"{g.Key.Item1}-W{g.Key.Item2:00}",
                    Math.Round(list.Average(d => d.Kcal), 0),
                    Math.Round(list.Average(d => d.Protein), 1),
                    Math.Round(list.Average(d => d.Carb), 1),
                    Math.Round(list.Average(d => d.Fat), 1),
                    list.Count,
                    hasTarget ? Math.Round((decimal)list.Count(d => d.StatusRank == 2) / list.Count, 3) : null);
            })
            .ToList();

    // -------- training

    private static List<SessionRow> BuildSessions(DataExport e) =>
        e.WorkoutSessions.Select(s =>
        {
            var working = s.Exercises.SelectMany(x => x.Sets).Where(set => set.SkippedAt is null).ToList();
            var volume = working.Sum(set => (set.WeightKg ?? 0) * (set.Reps ?? 0));
            var top = working
                .Where(set => set.WeightKg is > 0 && set.Reps is > 0)
                .OrderByDescending(set => set.WeightKg)
                .FirstOrDefault();
            var bestE1Rm = working
                .Select(set => DataExportSpreadsheet.Epley(set.WeightKg, set.Reps))
                .Where(x => x is not null)
                .DefaultIfEmpty(null)
                .Max();

            int? duration = s.CompletedAt is { } done
                ? (int)Math.Round((done - s.StartedAt).TotalMinutes)
                : null;

            return new SessionRow(
                DataExportSpreadsheet.SessionDate(s),
                s.ProgramName ?? "—",
                s.DayName ?? "—",
                s.Status,
                duration,
                s.Exercises.Count,
                working.Count,
                Math.Round(volume, 0),
                top is { WeightKg: { } tw, Reps: { } tr } ? $"{tw:0.#} kg × {tr}" : "—",
                bestE1Rm,
                s.Notes ?? "");
        }).ToList();

    private static TrainingAgg AggregateTraining(IReadOnlyCollection<SessionRow> sessions)
    {
        if (sessions.Count == 0)
        {
            return new TrainingAgg(0, 0, 0, null, null);
        }

        var volume = sessions.Sum(s => s.Volume);
        var durations = sessions.Where(s => s.DurationMin is not null).Select(s => (decimal)s.DurationMin!.Value).ToList();
        return new TrainingAgg(
            sessions.Count,
            sessions.Sum(s => s.WorkingSets),
            volume,
            Math.Round(volume / sessions.Count, 0),
            durations.Count > 0 ? Math.Round(durations.Average(), 0) : null);
    }

    private static List<ExerciseRow> BuildExercises(DataExport e)
    {
        var rows = new List<ExerciseRow>();

        var byName = e.WorkoutSessions
            .SelectMany(s => s.Exercises.Select(x => (Date: DataExportSpreadsheet.SessionDate(s), Exercise: x)))
            .GroupBy(t => t.Exercise.ExerciseName);

        foreach (var g in byName)
        {
            var perSession = g.ToList();
            var allSets = perSession.SelectMany(t => t.Exercise.Sets).Where(set => set.SkippedAt is null).ToList();

            var bestVolume = perSession
                .Select(t => t.Exercise.Sets
                    .Where(set => set.SkippedAt is null)
                    .Sum(set => (set.WeightKg ?? 0) * (set.Reps ?? 0)))
                .DefaultIfEmpty(0)
                .Max();

            rows.Add(new ExerciseRow(
                g.Key,
                perSession.Select(t => t.Date).Distinct().Count(),
                allSets.Count,
                allSets.Select(set => set.WeightKg).Where(x => x is not null).DefaultIfEmpty(null).Max(),
                allSets.Select(set => DataExportSpreadsheet.Epley(set.WeightKg, set.Reps)).Where(x => x is not null).DefaultIfEmpty(null).Max(),
                bestVolume > 0 ? Math.Round(bestVolume, 0) : null,
                perSession.Min(t => t.Date),
                perSession.Max(t => t.Date)));
        }

        return rows.OrderByDescending(r => r.Sessions).ThenBy(r => r.Name).ToList();
    }

    // -------- body weight

    private static List<WeightPoint> BuildWeightSeries(DataExport e)
    {
        var daily = e.BodyMeasurements
            .GroupBy(m => m.LocalDate)
            .Select(g => (Date: g.Key, Weight: Math.Round(g.Average(x => x.WeightKg), 2)))
            .OrderBy(p => p.Date)
            .ToList();

        var points = new List<WeightPoint>();
        foreach (var (date, weight) in daily)
        {
            var window = daily.Where(p => p.Date > date.AddDays(-7) && p.Date <= date).ToList();
            var avg7 = Math.Round(window.Average(p => p.Weight), 2);

            var weekAgo = daily.Where(p => p.Date <= date.AddDays(-7)).ToList();
            decimal? deltaVs7 = weekAgo.Count > 0 ? weight - weekAgo[^1].Weight : null;

            decimal? weeklyRate = null;
            var priorPoint = points.LastOrDefault(p => p.Date <= date.AddDays(-7));
            if (priorPoint is not null)
            {
                weeklyRate = Math.Round(avg7 - priorPoint.Avg7 ?? 0, 2);
            }

            points.Add(new WeightPoint(date, weight, avg7, deltaVs7, weeklyRate));
        }

        return points;
    }
}

internal sealed record NutritionAgg(decimal? AvgKcal, decimal? AvgProtein, decimal? Adherence, int Days);

internal sealed record TrainingAgg(int Sessions, int Sets, decimal Volume, decimal? AvgVolume, decimal? AvgDuration);

internal sealed record DailyNutritionRow(
    DateOnly Date,
    decimal Kcal,
    decimal Protein,
    decimal Carb,
    decimal Fat,
    int? TargetKcal,
    decimal? PctTarget,
    (decimal P, decimal C, decimal F) MacroPct,
    int Items,
    string Status,
    byte StatusRank);

internal sealed record WeeklyNutritionRow(
    string Label,
    decimal AvgKcal,
    decimal AvgProtein,
    decimal AvgCarb,
    decimal AvgFat,
    int Days,
    decimal? Adherence);

internal sealed record SessionRow(
    DateOnly Date,
    string Program,
    string Day,
    string Status,
    int? DurationMin,
    int Exercises,
    int WorkingSets,
    decimal Volume,
    string TopSet,
    decimal? BestE1Rm,
    string Notes);

internal sealed record ExerciseRow(
    string Name,
    int Sessions,
    int Sets,
    decimal? BestWeight,
    decimal? BestE1Rm,
    decimal? BestVolume,
    DateOnly First,
    DateOnly Last);

internal sealed record WeightPoint(
    DateOnly Date,
    decimal WeightKg,
    decimal? Avg7,
    decimal? DeltaVs7,
    decimal? WeeklyRate);
