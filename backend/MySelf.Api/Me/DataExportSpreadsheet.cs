using ClosedXML.Excel;

namespace MySelf.Api.Me;

/// <summary>
/// Renders a <see cref="DataExport"/> as a presentation-quality .xlsx: a Summary sheet plus
/// one sheet each for nutrition and workouts (docs/05 §13 — the human-friendly companion to
/// the raw JSON export). Uses ClosedXML (MIT) over the OpenXML SDK.
/// </summary>
public static class DataExportSpreadsheet
{
    private static readonly XLColor Brand = XLColor.FromHtml("#4F46E5");
    private static readonly XLColor HeaderText = XLColor.White;

    public static byte[] Build(DataExport export)
    {
        using var wb = new XLWorkbook();
        wb.Properties.Author = "MySelf App";
        wb.Properties.Title = "MySelf data export";

        BuildSummary(wb.Worksheets.Add("Summary"), export);
        BuildNutrition(wb.Worksheets.Add("Nutrition"), export);
        BuildWorkouts(wb.Worksheets.Add("Workouts"), export);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void BuildSummary(IXLWorksheet ws, DataExport e)
    {
        ws.Cell("B2").Value = "MySelf — Data export";
        ws.Range("B2:E2").Merge();
        ws.Cell("B2").Style.Font.SetBold().Font.SetFontSize(20).Font.SetFontColor(Brand);

        var rows = new (string Label, string Value)[]
        {
            ("Account", $"{e.Account.Username} <{e.Account.Email}>"),
            ("Exported", e.ExportedAt.ToString("yyyy-MM-dd HH:mm 'UTC'")),
            ("", ""),
            ("Meals logged", e.MealLogs.Sum(l => l.Items.Count).ToString()),
            ("Days with a food log", e.MealLogs.Select(l => l.LocalDate).Distinct().Count().ToString()),
            ("Custom foods saved", e.CustomFoods.Count.ToString()),
            ("Saved meals", e.SavedMeals.Count.ToString()),
            ("", ""),
            ("Workout sessions", e.WorkoutSessions.Count.ToString()),
            ("Completed sessions", e.WorkoutSessions.Count(s => s.Status == "Completed").ToString()),
            ("Personal records", e.PersonalRecords.Count.ToString()),
            ("Programs", e.WorkoutPrograms.Count.ToString()),
            ("", ""),
            ("Calorie target", e.Goals.LastOrDefault()?.CalorieTarget?.ToString() ?? "—"),
            ("Latest body weight (kg)", e.BodyMeasurements.LastOrDefault()?.WeightKg.ToString("0.0") ?? "—"),
        };

        var r = 4;
        foreach (var (label, value) in rows)
        {
            ws.Cell(r, 2).Value = label;
            ws.Cell(r, 2).Style.Font.SetBold();
            ws.Cell(r, 3).Value = value;
            r++;
        }

        ws.Cell(r + 1, 2).Value = "The Nutrition and Workouts sheets hold the detail. "
            + "A machine-readable copy of everything is in the JSON export.";
        ws.Cell(r + 1, 2).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);

        ws.Column(2).Width = 26;
        ws.Column(3).Width = 44;
        ws.SheetView.FreezeRows(1);
    }

    private static void BuildNutrition(IXLWorksheet ws, DataExport e)
    {
        string[] headers = ["Date", "Category", "Food", "Amount", "Unit", "Calories", "Protein (g)", "Carbs (g)", "Fat (g)"];
        WriteTitle(ws, "Nutrition log", headers.Length);

        var top = 3;
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(top, c + 1).Value = headers[c];
        }

        var row = top + 1;
        var ordered = e.MealLogs
            .OrderBy(l => l.LocalDate).ThenBy(l => l.Category)
            .SelectMany(l => l.Items.Select(i => (l.LocalDate, l.Category, Item: i)));

        foreach (var (date, category, i) in ordered)
        {
            ws.Cell(row, 1).Value = date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, 2).Value = category;
            ws.Cell(row, 3).Value = i.Name;
            ws.Cell(row, 4).Value = i.Amount;
            ws.Cell(row, 5).Value = i.Unit;
            ws.Cell(row, 6).Value = i.Kcal;
            ws.Cell(row, 7).Value = i.ProteinG;
            ws.Cell(row, 8).Value = i.CarbG;
            ws.Cell(row, 9).Value = i.FatG;
            row++;
        }

        var lastRow = Math.Max(row - 1, top + 1); // keep a valid (possibly empty) table body
        var table = ws.Range(top, 1, lastRow, headers.Length).CreateTable("NutritionTable");
        table.Theme = XLTableTheme.TableStyleMedium2;

        if (row > top + 1)
        {
            table.SetShowTotalsRow(true);
            table.Field("Calories").TotalsRowFunction = XLTotalsRowFunction.Sum;
            table.Field("Protein (g)").TotalsRowFunction = XLTotalsRowFunction.Sum;
            table.Field("Carbs (g)").TotalsRowFunction = XLTotalsRowFunction.Sum;
            table.Field("Fat (g)").TotalsRowFunction = XLTotalsRowFunction.Sum;
            table.Field("Food").TotalsRowLabel = "Total";
        }

        ws.Range(top + 1, 1, lastRow, 1).Style.NumberFormat.Format = "yyyy-mm-dd";
        ws.Range(top, 6, lastRow, 6).Style.NumberFormat.Format = "#,##0";
        ws.Range(top, 7, lastRow, 9).Style.NumberFormat.Format = "0.0";
        ws.Range(top, 4, lastRow, 4).Style.NumberFormat.Format = "0.###";

        ws.SheetView.FreezeRows(top);
        ws.Columns(1, headers.Length).AdjustToContents();
    }

    private static void BuildWorkouts(IXLWorksheet ws, DataExport e)
    {
        string[] headers =
            ["Date", "Status", "Program", "Day", "Exercise", "Set", "Type", "Weight (kg)", "Reps", "RIR", "Notes"];
        WriteTitle(ws, "Workout log", headers.Length);

        var top = 3;
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(top, c + 1).Value = headers[c];
        }

        var row = top + 1;
        foreach (var s in e.WorkoutSessions.OrderBy(s => s.PerformedOnLocalDate ?? DateOnly.FromDateTime(s.StartedAt.UtcDateTime)))
        {
            var date = s.PerformedOnLocalDate ?? DateOnly.FromDateTime(s.StartedAt.UtcDateTime);
            foreach (var ex in s.Exercises)
            {
                foreach (var set in ex.Sets)
                {
                    ws.Cell(row, 1).Value = date.ToDateTime(TimeOnly.MinValue);
                    ws.Cell(row, 2).Value = s.Status;
                    ws.Cell(row, 3).Value = s.ProgramName ?? "—";
                    ws.Cell(row, 4).Value = s.DayName ?? "—";
                    ws.Cell(row, 5).Value = ex.ExerciseName;
                    ws.Cell(row, 6).Value = set.SortOrder + 1;
                    ws.Cell(row, 7).Value = set.Kind + (set.ReachedFailure ? " · to failure" : "");
                    if (set.WeightKg is { } w) ws.Cell(row, 8).Value = w;
                    if (set.Reps is { } reps) ws.Cell(row, 9).Value = reps;
                    if (set.Rir is { } rir) ws.Cell(row, 10).Value = rir;
                    ws.Cell(row, 11).Value = set.SkippedAt is not null
                        ? $"skipped{(set.SkippedReason is { } sr ? $" — {sr}" : "")}"
                        : "";
                    row++;
                }
            }
        }

        var lastRow = Math.Max(row - 1, top + 1);
        var table = ws.Range(top, 1, lastRow, headers.Length).CreateTable("WorkoutTable");
        table.Theme = XLTableTheme.TableStyleMedium4;

        ws.Range(top + 1, 1, lastRow, 1).Style.NumberFormat.Format = "yyyy-mm-dd";
        ws.Range(top, 8, lastRow, 8).Style.NumberFormat.Format = "0.###";
        ws.SheetView.FreezeRows(top);
        ws.Columns(1, headers.Length).AdjustToContents();
    }

    private static void WriteTitle(IXLWorksheet ws, string title, int span)
    {
        ws.Cell(1, 1).Value = title;
        ws.Range(1, 1, 1, span).Merge();
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(HeaderText);
        ws.Range(1, 1, 1, span).Style.Fill.BackgroundColor = Brand;
        ws.Row(1).Height = 22;
    }
}
