using System.Globalization;
using ClosedXML.Excel;

namespace MySelf.Api.Me;

/// <summary>
/// Renders a <see cref="DataExport"/> as a presentation-quality .xlsx built for a self
/// check-in (docs/05 §13 — the human-friendly companion to the raw JSON export):
///
///   Overview            — KPI dashboard, nutrition + training + body-weight at a glance
///   Nutrition — Daily    — per-day calories/macros vs target, adherence, weekly averages
///   Nutrition — Food log — every logged item (supporting detail, filterable)
///   Training — Sessions  — per-session volume, duration, top set, best e1RM
///   Training — Exercises — per-exercise progression: best weight / e1RM / volume
///   Training — Set log    — every logged set with its estimated 1RM (supporting detail)
///   Body weight           — daily weight, 7-day average, weekly rate of change
///
/// Uses ClosedXML (MIT). ClosedXML cannot emit native charts, so the tables are laid out
/// contiguous and header-first — "select a table, press Alt+F1" gives an instant chart —
/// and trends are shown with data-bar / colour-scale conditional formatting instead.
/// </summary>
public static class DataExportSpreadsheet
{
    private static readonly XLColor Brand = XLColor.FromHtml("#4F46E5");
    private static readonly XLColor BrandSoft = XLColor.FromHtml("#EEF2FF");
    private static readonly XLColor Ink = XLColor.FromHtml("#1E1B2E");
    private static readonly XLColor Muted = XLColor.FromHtml("#6B7280");
    private static readonly XLColor Good = XLColor.FromHtml("#C6F6D5");
    private static readonly XLColor Warn = XLColor.FromHtml("#FEF3C7");
    private static readonly XLColor Bad = XLColor.FromHtml("#FED7D7");
    private static readonly XLColor BarBlue = XLColor.FromHtml("#A5B4FC");
    private static readonly XLColor BarGreen = XLColor.FromHtml("#86EFAC");

    private const string FmtInt = "#,##0";
    private const string FmtOne = "#,##0.0";
    private const string FmtKg = "#,##0.0\" kg\"";
    private const string FmtPct = "0%";
    private const string FmtSignedInt = "+#,##0;-#,##0;0";
    private const string FmtSignedOne = "+#,##0.0;-#,##0.0;0.0";
    private const string FmtDate = "yyyy-mm-dd";

    public static byte[] Build(DataExport export)
    {
        var model = CheckInModel.From(export);

        using var wb = new XLWorkbook();
        wb.Properties.Author = "MySelf App";
        wb.Properties.Title = "MySelf check-in export";

        BuildOverview(wb.Worksheets.Add("Overview"), model);
        BuildNutritionDaily(wb.Worksheets.Add("Nutrition — Daily"), model);
        BuildFoodLog(wb.Worksheets.Add("Nutrition — Food log"), export);
        BuildSessions(wb.Worksheets.Add("Training — Sessions"), model);
        BuildExercises(wb.Worksheets.Add("Training — Exercises"), model);
        BuildSetLog(wb.Worksheets.Add("Training — Set log"), export);
        BuildBodyWeight(wb.Worksheets.Add("Body weight"), model);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ---------------------------------------------------------------- Overview

    private static void BuildOverview(IXLWorksheet ws, CheckInModel m)
    {
        ws.Column(2).Width = 30;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 4;
        ws.Column(5).Width = 30;
        ws.Column(6).Width = 18;

        Band(ws, "B2:F2", "MySelf — Check-in");
        ws.Cell("B3").Value = $"{m.Username}  <{m.Email}>";
        ws.Cell("B4").Value = m.Range is var (from, to) && from != default
            ? $"Covers {from:yyyy-MM-dd} → {to:yyyy-MM-dd}  ·  exported {m.ExportedAt:yyyy-MM-dd HH:mm} UTC"
            : $"Exported {m.ExportedAt:yyyy-MM-dd HH:mm} UTC";
        ws.Range("B3:F3").Merge();
        ws.Range("B4:F4").Merge();
        ws.Cell("B4").Style.Font.SetFontColor(Muted).Font.SetItalic();

        var row = 6;
        SectionTitle(ws, ref row, "Nutrition — last 7 logged days");
        var n7 = m.Nutrition7;
        Kpi(ws, ref row, "Avg calories", n7.AvgKcal, FmtInt,
            note: m.CalorieTarget is { } t ? $"target {t:#,##0}" : null);
        Kpi(ws, ref row, "Avg protein", n7.AvgProtein, FmtOne, note: "g / day");
        Kpi(ws, ref row, "Protein per kg", m.ProteinPerKg, FmtOne,
            note: m.LatestWeightKg is { } w ? $"at {w:0.0} kg" : "no weight logged");
        var adhRow = row;
        Kpi(ws, ref row, "Calorie adherence", n7.Adherence, FmtPct, note: "days within ±10% of target");
        Kpi(ws, ref row, "Macro split P / C / F", 0, "@", text: m.MacroSplit);
        StatusFill(ws.Cell(adhRow, 3), n7.Adherence is null ? (byte)1 : n7.Adherence >= 0.8m ? (byte)2 : n7.Adherence >= 0.6m ? (byte)1 : (byte)0);

        row++;
        SectionTitle(ws, ref row, "Nutrition — all logged days");
        Kpi(ws, ref row, "Days logged", m.NutritionAll.Days, FmtInt);
        Kpi(ws, ref row, "Avg calories", m.NutritionAll.AvgKcal, FmtInt);
        Kpi(ws, ref row, "Best day (closest to target)", 0, "@", text: m.BestDayLabel);

        row = 6;
        var c = 5;
        SectionTitleAt(ws, row, c, "Training — last 28 days");
        row++;
        KpiAt(ws, ref row, c, "Sessions", m.Training28.Sessions, FmtInt);
        KpiAt(ws, ref row, c, "Working sets", m.Training28.Sets, FmtInt);
        KpiAt(ws, ref row, c, "Total volume", m.Training28.Volume, FmtInt, "kg (weight × reps)");
        KpiAt(ws, ref row, c, "Avg session volume", m.Training28.AvgVolume, FmtInt, "kg");
        KpiAt(ws, ref row, c, "Avg session length", m.Training28.AvgDuration, FmtInt, "minutes");
        KpiAt(ws, ref row, c, "Personal records", m.PrCount, FmtInt, "all time");

        row++;
        SectionTitleAt(ws, row, c, "Body weight");
        row++;
        KpiAt(ws, ref row, c, "Latest", m.LatestWeightKg, FmtKg);
        KpiAt(ws, ref row, c, "7-day average", m.Weight7Avg, FmtKg);
        var rateRow = row;
        KpiAt(ws, ref row, c, "Weekly rate", m.WeeklyRateKg, FmtSignedOne, "kg / week");
        KpiAt(ws, ref row, c, "Change since first", m.WeightChangeKg, FmtSignedOne, "kg");
        if (m.WeeklyRateKg is { } rate)
        {
            StatusFill(ws.Cell(rateRow, c + 1), Math.Abs(rate) <= 1.0m ? (byte)2 : Math.Abs(rate) <= 1.5m ? (byte)1 : (byte)0);
        }

        var tipRow = Math.Max(row, 24) + 1;
        ws.Cell(tipRow, 2).Value = "How to read this workbook";
        ws.Cell(tipRow, 2).Style.Font.SetBold();
        ws.Cell(tipRow + 1, 2).Value =
            "Each sheet is a filterable table. Select any table and press Alt+F1 for an instant chart. "
            + "Coloured bars show relative size; green/amber/red flags show on-target / borderline / off-target.";
        ws.Range(tipRow + 1, 2, tipRow + 1, 6).Merge();
        ws.Cell(tipRow + 1, 2).Style.Alignment.SetWrapText().Font.SetFontColor(Muted);
        ws.Row(tipRow + 1).Height = 30;

        ws.SheetView.FreezeRows(1);
        ws.Cell("A1").Value = "";
    }

    // -------------------------------------------------------- Nutrition — Daily

    private static void BuildNutritionDaily(IXLWorksheet ws, CheckInModel m)
    {
        string[] headers =
        [
            "Date", "Weekday", "Calories", "Target", "Δ kcal", "% target",
            "Protein (g)", "Carbs (g)", "Fat (g)", "P %", "C %", "F %", "Items", "Status",
        ];
        Band(ws, RangeRef(1, 1, 1, headers.Length), "Nutrition — daily vs target");

        var top = 3;
        WriteHeaders(ws, top, headers);
        var r = top + 1;
        foreach (var d in m.DailyNutrition)
        {
            ws.Cell(r, 1).Value = d.Date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 2).Value = d.Date.DayOfWeek.ToString()[..3];
            ws.Cell(r, 3).Value = d.Kcal;
            if (d.TargetKcal is { } tk) ws.Cell(r, 4).Value = tk;
            if (d.TargetKcal is { } tk2) ws.Cell(r, 5).Value = d.Kcal - tk2;
            if (d.PctTarget is { } pct) ws.Cell(r, 6).Value = pct;
            ws.Cell(r, 7).Value = d.Protein;
            ws.Cell(r, 8).Value = d.Carb;
            ws.Cell(r, 9).Value = d.Fat;
            if (d.MacroPct is var (pp, cp, fp) && d.Kcal > 0)
            {
                ws.Cell(r, 10).Value = pp;
                ws.Cell(r, 11).Value = cp;
                ws.Cell(r, 12).Value = fp;
            }
            ws.Cell(r, 13).Value = d.Items;
            ws.Cell(r, 14).Value = d.Status;
            StatusFill(ws.Cell(r, 14), d.StatusRank);
            r++;
        }

        var last = Math.Max(r - 1, top + 1);
        var table = ws.Range(top, 1, last, headers.Length).CreateTable("NutritionDaily");
        table.Theme = XLTableTheme.TableStyleMedium2;
        if (r > top + 1)
        {
            table.SetShowTotalsRow(true);
            foreach (var col in new[] { "Calories", "Protein (g)", "Carbs (g)", "Fat (g)", "Items" })
            {
                table.Field(col).TotalsRowFunction = XLTotalsRowFunction.Average;
            }
            table.Field("Weekday").TotalsRowLabel = "Average";
        }

        ws.Range(top + 1, 1, last, 1).Style.NumberFormat.Format = FmtDate;
        ws.Range(top + 1, 3, last, 4).Style.NumberFormat.Format = FmtInt;
        ws.Range(top + 1, 5, last, 5).Style.NumberFormat.Format = FmtSignedInt;
        ws.Range(top + 1, 6, last, 6).Style.NumberFormat.Format = FmtPct;
        ws.Range(top, 7, last, 9).Style.NumberFormat.Format = FmtOne;
        ws.Range(top + 1, 10, last, 12).Style.NumberFormat.Format = FmtPct;

        if (r > top + 1)
        {
            ws.Range(top + 1, 3, last, 3).AddConditionalFormat().DataBar(BarBlue).LowestValue().HighestValue();
            ws.Range(top + 1, 6, last, 6).AddConditionalFormat().ColorScale()
                .LowestValue(Bad)
                .Midpoint(XLCFContentType.Number, "1", Good)
                .HighestValue(Bad);
            ws.Range(top + 1, 7, last, 7).AddConditionalFormat().DataBar(BarGreen).LowestValue().HighestValue();
        }

        ws.SheetView.FreezeRows(top);
        ws.Columns(1, headers.Length).AdjustToContents();
        ws.PageSetup.PagesWide = 1;

        // Weekly averages block, to the right.
        var wcol = headers.Length + 2;
        SectionTitleAt(ws, 1, wcol, "Weekly averages");
        string[] wk = ["Week", "Avg kcal", "Avg P", "Avg C", "Avg F", "Days", "Adherence"];
        WriteHeaders(ws, 3, wk, wcol);
        var wr = 4;
        foreach (var w in m.WeeklyNutrition)
        {
            ws.Cell(wr, wcol + 0).Value = w.Label;
            ws.Cell(wr, wcol + 1).Value = w.AvgKcal;
            ws.Cell(wr, wcol + 2).Value = w.AvgProtein;
            ws.Cell(wr, wcol + 3).Value = w.AvgCarb;
            ws.Cell(wr, wcol + 4).Value = w.AvgFat;
            ws.Cell(wr, wcol + 5).Value = w.Days;
            if (w.Adherence is { } a) ws.Cell(wr, wcol + 6).Value = a;
            wr++;
        }
        if (wr > 4)
        {
            var wtable = ws.Range(3, wcol, wr - 1, wcol + wk.Length - 1).CreateTable("NutritionWeekly");
            wtable.Theme = XLTableTheme.TableStyleMedium6;
            ws.Range(4, wcol + 1, wr - 1, wcol + 1).Style.NumberFormat.Format = FmtInt;
            ws.Range(4, wcol + 2, wr - 1, wcol + 4).Style.NumberFormat.Format = FmtOne;
            ws.Range(4, wcol + 6, wr - 1, wcol + 6).Style.NumberFormat.Format = FmtPct;
            ws.Range(4, wcol + 1, wr - 1, wcol + 1).AddConditionalFormat().DataBar(BarBlue).LowestValue().HighestValue();
        }
        ws.Columns(wcol, wcol + wk.Length - 1).AdjustToContents();
    }

    // ---------------------------------------------------- Nutrition — Food log

    private static void BuildFoodLog(IXLWorksheet ws, DataExport e)
    {
        string[] headers = ["Date", "Category", "Food", "Amount", "Unit", "Calories", "Protein (g)", "Carbs (g)", "Fat (g)"];
        Band(ws, RangeRef(1, 1, 1, headers.Length), "Nutrition — every logged item");

        var top = 3;
        WriteHeaders(ws, top, headers);
        var r = top + 1;
        foreach (var (date, category, i) in e.MealLogs
                     .OrderBy(l => l.LocalDate).ThenBy(l => l.Category)
                     .SelectMany(l => l.Items.Select(i => (l.LocalDate, l.Category, i))))
        {
            ws.Cell(r, 1).Value = date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 2).Value = category;
            ws.Cell(r, 3).Value = i.Name;
            ws.Cell(r, 4).Value = i.Amount;
            ws.Cell(r, 5).Value = i.Unit;
            ws.Cell(r, 6).Value = i.Kcal;
            ws.Cell(r, 7).Value = i.ProteinG;
            ws.Cell(r, 8).Value = i.CarbG;
            ws.Cell(r, 9).Value = i.FatG;
            r++;
        }

        var last = Math.Max(r - 1, top + 1);
        var table = ws.Range(top, 1, last, headers.Length).CreateTable("FoodLog");
        table.Theme = XLTableTheme.TableStyleMedium2;
        if (r > top + 1)
        {
            table.SetShowTotalsRow(true);
            foreach (var col in new[] { "Calories", "Protein (g)", "Carbs (g)", "Fat (g)" })
            {
                table.Field(col).TotalsRowFunction = XLTotalsRowFunction.Sum;
            }
            table.Field("Food").TotalsRowLabel = "Total";
        }

        ws.Range(top + 1, 1, last, 1).Style.NumberFormat.Format = FmtDate;
        ws.Range(top, 4, last, 4).Style.NumberFormat.Format = "0.###";
        ws.Range(top, 6, last, 6).Style.NumberFormat.Format = FmtInt;
        ws.Range(top, 7, last, 9).Style.NumberFormat.Format = FmtOne;
        ws.SheetView.FreezeRows(top);
        ws.Columns(1, headers.Length).AdjustToContents();
        ws.PageSetup.PagesWide = 1;
    }

    // -------------------------------------------------- Training — Sessions

    private static void BuildSessions(IXLWorksheet ws, CheckInModel m)
    {
        string[] headers =
        [
            "Date", "Program", "Day", "Status", "Duration (min)", "Exercises",
            "Working sets", "Volume (kg)", "Top set", "Best e1RM (kg)", "Notes",
        ];
        Band(ws, RangeRef(1, 1, 1, headers.Length), "Training — sessions");

        var top = 3;
        WriteHeaders(ws, top, headers);
        var r = top + 1;
        foreach (var s in m.Sessions)
        {
            ws.Cell(r, 1).Value = s.Date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 2).Value = s.Program;
            ws.Cell(r, 3).Value = s.Day;
            ws.Cell(r, 4).Value = s.Status;
            if (s.DurationMin is { } dm) ws.Cell(r, 5).Value = dm;
            ws.Cell(r, 6).Value = s.Exercises;
            ws.Cell(r, 7).Value = s.WorkingSets;
            ws.Cell(r, 8).Value = s.Volume;
            ws.Cell(r, 9).Value = s.TopSet;
            if (s.BestE1Rm is { } e) ws.Cell(r, 10).Value = e;
            ws.Cell(r, 11).Value = s.Notes;
            r++;
        }

        var last = Math.Max(r - 1, top + 1);
        var table = ws.Range(top, 1, last, headers.Length).CreateTable("Sessions");
        table.Theme = XLTableTheme.TableStyleMedium4;
        if (r > top + 1)
        {
            table.SetShowTotalsRow(true);
            table.Field("Volume (kg)").TotalsRowFunction = XLTotalsRowFunction.Sum;
            table.Field("Working sets").TotalsRowFunction = XLTotalsRowFunction.Sum;
            table.Field("Duration (min)").TotalsRowFunction = XLTotalsRowFunction.Average;
            table.Field("Day").TotalsRowLabel = "Total / avg";
        }

        ws.Range(top + 1, 1, last, 1).Style.NumberFormat.Format = FmtDate;
        ws.Range(top, 5, last, 8).Style.NumberFormat.Format = FmtInt;
        ws.Range(top, 10, last, 10).Style.NumberFormat.Format = FmtOne;
        if (r > top + 1)
        {
            ws.Range(top + 1, 8, last, 8).AddConditionalFormat().DataBar(BarBlue).LowestValue().HighestValue();
            ws.Range(top + 1, 10, last, 10).AddConditionalFormat().DataBar(BarGreen).LowestValue().HighestValue();
        }
        ws.SheetView.FreezeRows(top);
        ws.Columns(1, headers.Length).AdjustToContents();
        ws.PageSetup.PagesWide = 1;
    }

    // ------------------------------------------------- Training — Exercises

    private static void BuildExercises(IXLWorksheet ws, CheckInModel m)
    {
        string[] headers =
        [
            "Exercise", "Sessions", "Total sets", "Best weight (kg)", "Best e1RM (kg)",
            "Best session volume (kg)", "First done", "Last done",
        ];
        Band(ws, RangeRef(1, 1, 1, headers.Length), "Training — per-exercise progression");

        var top = 3;
        WriteHeaders(ws, top, headers);
        var r = top + 1;
        foreach (var x in m.Exercises)
        {
            ws.Cell(r, 1).Value = x.Name;
            ws.Cell(r, 2).Value = x.Sessions;
            ws.Cell(r, 3).Value = x.Sets;
            if (x.BestWeight is { } bw) ws.Cell(r, 4).Value = bw;
            if (x.BestE1Rm is { } be) ws.Cell(r, 5).Value = be;
            if (x.BestVolume is { } bv) ws.Cell(r, 6).Value = bv;
            ws.Cell(r, 7).Value = x.First.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 8).Value = x.Last.ToDateTime(TimeOnly.MinValue);
            r++;
        }

        var last = Math.Max(r - 1, top + 1);
        var table = ws.Range(top, 1, last, headers.Length).CreateTable("Exercises");
        table.Theme = XLTableTheme.TableStyleMedium4;

        ws.Range(top + 1, 4, last, 6).Style.NumberFormat.Format = FmtOne;
        ws.Range(top + 1, 7, last, 8).Style.NumberFormat.Format = FmtDate;
        if (r > top + 1)
        {
            ws.Range(top + 1, 5, last, 5).AddConditionalFormat().DataBar(BarGreen).LowestValue().HighestValue();
            ws.Range(top + 1, 6, last, 6).AddConditionalFormat().DataBar(BarBlue).LowestValue().HighestValue();
        }
        ws.SheetView.FreezeRows(top);
        ws.Columns(1, headers.Length).AdjustToContents();
        ws.PageSetup.PagesWide = 1;
    }

    // -------------------------------------------------- Training — Set log

    private static void BuildSetLog(IXLWorksheet ws, DataExport e)
    {
        string[] headers =
        [
            "Date", "Program", "Day", "Exercise", "Set", "Type",
            "Weight (kg)", "Reps", "RIR", "e1RM (kg)", "Notes",
        ];
        Band(ws, RangeRef(1, 1, 1, headers.Length), "Training — every logged set");

        var top = 3;
        WriteHeaders(ws, top, headers);
        var r = top + 1;
        foreach (var s in e.WorkoutSessions.OrderBy(SessionDate))
        {
            var date = SessionDate(s);
            foreach (var ex in s.Exercises)
            {
                foreach (var set in ex.Sets)
                {
                    ws.Cell(r, 1).Value = date.ToDateTime(TimeOnly.MinValue);
                    ws.Cell(r, 2).Value = s.ProgramName ?? "—";
                    ws.Cell(r, 3).Value = s.DayName ?? "—";
                    ws.Cell(r, 4).Value = ex.ExerciseName;
                    ws.Cell(r, 5).Value = set.SortOrder + 1;
                    ws.Cell(r, 6).Value = set.Kind + (set.ReachedFailure ? " · to failure" : "");
                    if (set.WeightKg is { } w) ws.Cell(r, 7).Value = w;
                    if (set.Reps is { } reps) ws.Cell(r, 8).Value = reps;
                    if (set.Rir is { } rir) ws.Cell(r, 9).Value = rir;
                    if (Epley(set.WeightKg, set.Reps) is { } e1) ws.Cell(r, 10).Value = e1;
                    ws.Cell(r, 11).Value = set.SkippedAt is not null
                        ? $"skipped{(set.SkippedReason is { } sr ? $" — {sr}" : "")}"
                        : "";
                    r++;
                }
            }
        }

        var last = Math.Max(r - 1, top + 1);
        var table = ws.Range(top, 1, last, headers.Length).CreateTable("SetLog");
        table.Theme = XLTableTheme.TableStyleMedium4;

        ws.Range(top + 1, 1, last, 1).Style.NumberFormat.Format = FmtDate;
        ws.Range(top, 7, last, 7).Style.NumberFormat.Format = "0.###";
        ws.Range(top, 10, last, 10).Style.NumberFormat.Format = FmtOne;
        ws.SheetView.FreezeRows(top);
        ws.Columns(1, headers.Length).AdjustToContents();
        ws.PageSetup.PagesWide = 1;
    }

    // ------------------------------------------------------------ Body weight

    private static void BuildBodyWeight(IXLWorksheet ws, CheckInModel m)
    {
        string[] headers = ["Date", "Weight (kg)", "7-day avg", "Δ vs 7d ago", "Weekly rate (kg/wk)"];
        Band(ws, RangeRef(1, 1, 1, headers.Length), "Body weight");

        var top = 3;
        WriteHeaders(ws, top, headers);
        var r = top + 1;
        foreach (var p in m.WeightSeries)
        {
            ws.Cell(r, 1).Value = p.Date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 2).Value = p.WeightKg;
            if (p.Avg7 is { } a) ws.Cell(r, 3).Value = a;
            if (p.DeltaVs7 is { } dv) ws.Cell(r, 4).Value = dv;
            if (p.WeeklyRate is { } wr2) ws.Cell(r, 5).Value = wr2;
            r++;
        }

        var last = Math.Max(r - 1, top + 1);
        var table = ws.Range(top, 1, last, headers.Length).CreateTable("BodyWeight");
        table.Theme = XLTableTheme.TableStyleMedium6;

        ws.Range(top + 1, 1, last, 1).Style.NumberFormat.Format = FmtDate;
        ws.Range(top + 1, 2, last, 3).Style.NumberFormat.Format = FmtOne;
        ws.Range(top + 1, 4, last, 5).Style.NumberFormat.Format = FmtSignedOne;
        if (r > top + 1)
        {
            ws.Range(top + 1, 2, last, 2).AddConditionalFormat().DataBar(BarBlue).LowestValue().HighestValue();
        }
        ws.SheetView.FreezeRows(top);
        ws.Columns(1, headers.Length).AdjustToContents();

        if (r == top + 1)
        {
            ws.Cell(top + 1, 1).Value = "No body-weight readings logged yet.";
            ws.Cell(top + 1, 1).Style.Font.SetItalic().Font.SetFontColor(Muted);
        }
    }

    // --------------------------------------------------------------- helpers

    private static void Band(IXLWorksheet ws, string range, string title)
    {
        ws.Cell(range.Split(':')[0]).Value = title;
        ws.Range(range).Merge().Style
            .Fill.SetBackgroundColor(Brand)
            .Font.SetBold().Font.SetFontColor(XLColor.White).Font.SetFontSize(14);
        ws.Row(int.Parse(new string(range.Split(':')[0].Where(char.IsDigit).ToArray()))).Height = 22;
    }

    private static void WriteHeaders(IXLWorksheet ws, int row, string[] headers, int startCol = 1)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, startCol + i);
            cell.Value = headers[i];
            cell.Style.Font.SetBold().Font.SetFontColor(Ink).Fill.SetBackgroundColor(BrandSoft);
        }
    }

    private static void SectionTitle(IXLWorksheet ws, ref int row, string text)
    {
        SectionTitleAt(ws, row, 2, text);
        row++;
    }

    private static void SectionTitleAt(IXLWorksheet ws, int row, int col, string text)
    {
        var cell = ws.Cell(row, col);
        cell.Value = text;
        cell.Style.Font.SetBold().Font.SetFontColor(Brand).Font.SetFontSize(11);
    }

    private static void Kpi(IXLWorksheet ws, ref int row, string label, decimal? value, string format,
        string? note = null, string? text = null)
    {
        KpiAt(ws, ref row, 2, label, value, format, note, text);
    }

    private static void KpiAt(IXLWorksheet ws, ref int row, int col, string label, decimal? value, string format,
        string? note = null, string? text = null)
    {
        ws.Cell(row, col).Value = label;
        ws.Cell(row, col).Style.Font.SetFontColor(Muted);
        var valueCell = ws.Cell(row, col + 1);
        if (text is not null)
        {
            valueCell.Value = text;
        }
        else if (value is { } v)
        {
            valueCell.Value = v;
            valueCell.Style.NumberFormat.Format = format;
        }
        else
        {
            valueCell.Value = "—";
        }

        valueCell.Style.Font.SetBold();
        if (note is not null)
        {
            ws.Cell(row, col + 2).Value = note;
            ws.Cell(row, col + 2).Style.Font.SetFontColor(Muted).Font.SetItalic().Font.SetFontSize(9);
        }
        row++;
    }

    /// <summary>0 = red, 1 = amber, 2 = green.</summary>
    private static void StatusFill(IXLCell cell, byte rank) =>
        cell.Style.Fill.SetBackgroundColor(rank switch { 2 => Good, 1 => Warn, _ => Bad });

    private static string RangeRef(int r1, int c1, int r2, int c2) =>
        $"{Col(c1)}{r1}:{Col(c2)}{r2}";

    private static string Col(int index)
    {
        var s = "";
        while (index > 0)
        {
            var m = (index - 1) % 26;
            s = (char)('A' + m) + s;
            index = (index - 1) / 26;
        }

        return s;
    }

    internal static DateOnly SessionDate(ExportSession s) =>
        s.PerformedOnLocalDate ?? DateOnly.FromDateTime(s.StartedAt.UtcDateTime);

    internal static decimal? Epley(decimal? weight, int? reps) =>
        weight is > 0 && reps is > 0 and <= 20
            ? Math.Round(weight.Value * (1m + reps.Value / 30m), 1)
            : null;
}
