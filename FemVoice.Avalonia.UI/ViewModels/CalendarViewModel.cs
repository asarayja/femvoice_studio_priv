using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One day cell in the month grid: date, per-day aggregates (sessions/minutes/score/pitch/resonance), an
/// intensity heat-map colour, and a hover tooltip — the WPF CalendarWindow's day cell. Read-only.</summary>
public sealed class CalendarDayCell
{
    public CalendarDayCell(DateTime date, bool inMonth, bool isToday, int sessions,
        int minutes, double score, double pitch, double resonance, string tooltip, IRelayCommand open)
    {
        Date = date; InMonth = inMonth; IsToday = isToday; Sessions = sessions;
        Minutes = minutes; Score = score; Pitch = pitch; Resonance = resonance; Tooltip = tooltip; Open = open;
    }
    public DateTime Date { get; }
    public string DayNumber => Date.Day.ToString(CultureInfo.InvariantCulture);
    public bool InMonth { get; }
    /// <summary>Dim out-of-month days (converter-free opacity for the day number).</summary>
    public double NumberOpacity => InMonth ? 1.0 : 0.3;
    public bool IsToday { get; }
    public int Sessions { get; }
    public int Minutes { get; }
    public double Score { get; }
    public double Pitch { get; }
    public double Resonance { get; }
    public bool HasSessions => Sessions > 0;
    /// <summary>Session-count dot text (e.g. "•" / "2") shown under the day number when there are sessions.</summary>
    public string SessionMark => Sessions == 0 ? "" : Sessions == 1 ? "•" : Sessions.ToString(CultureInfo.InvariantCulture);
    /// <summary>Hover tooltip: sessions / minutes / avg score / pitch / resonance (WPF parity).</summary>
    public string Tooltip { get; }
    public IRelayCommand Open { get; }

    // Training intensity = (score/100) × min(1, sessions/2) — the WPF CalendarDay formula.
    private double Intensity => !HasSessions ? 0 : Math.Clamp(Score / 100.0, 0, 1) * Math.Min(1.0, Sessions / 2.0);

    /// <summary>Heat-map fill (WPF IntensityColor): red spectrum &lt;50, yellow/orange &lt;75, green ≥75; faint when idle.</summary>
    public IBrush IntensityBrush
    {
        get
        {
            double i = Intensity;
            if (i <= 0 || !HasSessions) return new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));   // faint idle
            if (Score < 50)
            {
                double t = i * 0.5;
                return new SolidColorBrush(Color.FromRgb((byte)(255 * (0.3 + t * 0.7)), (byte)(50 * t), (byte)(50 * t)));
            }
            if (Score < 75)
                return new SolidColorBrush(Color.FromRgb(255, (byte)(200 * i), (byte)(50 * i)));
            return new SolidColorBrush(Color.FromRgb((byte)(50 * (1 - i)), (byte)(200 * (0.3 + i * 0.7)), (byte)(50 + 100 * i)));
        }
    }
}

/// <summary>
/// Calendar / history page — a MONTH GRID (ported from the WPF CalendarWindow): weekday headers, prev/next/today
/// navigation, and day cells that highlight training days (session count) and today, from the REAL database. Clicking
/// a day with sessions opens the day-details panel. Read-only; no clinical logic changed. Fails safe with no DB.
/// </summary>
public sealed partial class CalendarViewModel : ObservableObject
{
    private readonly IDatabaseService? _database;
    private readonly Action<DateTime>? _openDay;

    public string Title => Localized.Get("Calendar_Title", "Kalender / historikk");
    public bool EngineAvailable { get; }
    public string UnavailableNote { get; } = "";
    public string DataNote => Localized.Get("Calendar_RealDataNote",
        "Ekte treningshistorikk fra dine lagrede økter. Klikk en dag for detaljer.");
    public string TodayLabel => Localized.Get("Calendar_Today", "I dag");
    public IReadOnlyList<string> WeekdayHeaders { get; } = new[]
    {
        Localized.Get("Calendar_Mon", "Man"), Localized.Get("Calendar_Tue", "Tir"), Localized.Get("Calendar_Wed", "Ons"),
        Localized.Get("Calendar_Thu", "Tor"), Localized.Get("Calendar_Fri", "Fre"), Localized.Get("Calendar_Sat", "Lør"),
        Localized.Get("Calendar_Sun", "Søn"),
    };

    [ObservableProperty] private DateTime _month;
    [ObservableProperty] private string _monthLabel = "";
    [ObservableProperty] private IReadOnlyList<CalendarDayCell> _days = Array.Empty<CalendarDayCell>();

    public CalendarViewModel(IDatabaseService? database, Action<DateTime>? openDay = null)
    {
        _database = database;
        _openDay = openDay;
        if (database is null)
        {
            UnavailableNote = Localized.Get("Calendar_NoDb", "Kalender krever databasen, som ikke er tilgjengelig i denne visningen.");
            return;
        }
        EngineAvailable = true;
        var today = DateTime.Now.Date;
        _month = new DateTime(today.Year, today.Month, 1);
        BuildGrid();
    }

    [RelayCommand] private void PrevMonth() { Month = Month.AddMonths(-1); BuildGrid(); }
    [RelayCommand] private void NextMonth() { Month = Month.AddMonths(1); BuildGrid(); }
    [RelayCommand] private void GoToday() { var t = DateTime.Now.Date; Month = new DateTime(t.Year, t.Month, 1); BuildGrid(); }

    // Build a 6×7 month grid (Monday-first). Each in-range day carries its REAL per-day aggregates from the WPF
    // GetCalendarData source (sessions/minutes/score/pitch/resonance) → intensity heat-map + tooltip. A day with
    // sessions is clickable → openDay(date). Fails safe (empty grid) on any DB error.
    private void BuildGrid()
    {
        MonthLabel = Month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        try
        {
            // Grid starts on the Monday on/before the 1st of the month; 42 cells (6 weeks).
            int offset = ((int)Month.DayOfWeek + 6) % 7;   // Mon=0 … Sun=6
            var gridStart = Month.AddDays(-offset);
            var gridEnd = gridStart.AddDays(42);

            // Per-day aggregates from the REAL saved sessions (WPF surfaces the same sessions/minutes/score/pitch/
            // resonance; its GetCalendarData reads a separate CalendarData aggregate table that the Avalonia head does
            // not maintain, so we aggregate the real TrainingSessions directly — same data, no demo data).
            var byDay = new Dictionary<DateTime, CalendarDayData>();
            if (_database is not null)
            {
                foreach (var g in _database.GetTrainingSessions(gridStart.ToUniversalTime(), gridEnd.ToUniversalTime())
                             .GroupBy(s => s.StartTime.ToLocalTime().Date))
                {
                    var list = g.ToList();
                    byDay[g.Key] = new CalendarDayData
                    {
                        Sessions = list.Count,
                        Minutes = (int)Math.Round(list.Sum(s => s.DurationSeconds) / 60.0),
                        Score = list.Average(s => s.OverallScore),
                        PitchScore = list.Where(s => s.AveragePitch > 0).Select(s => s.AveragePitch).DefaultIfEmpty(0).Average(),
                        ResonanceScore = list.Where(s => s.ResonanceScore > 0).Select(s => s.ResonanceScore).DefaultIfEmpty(0).Average(),
                    };
                }
            }

            var today = DateTime.Now.Date;
            var cells = new List<CalendarDayCell>(42);
            for (int i = 0; i < 42; i++)
            {
                var d = gridStart.AddDays(i);
                byDay.TryGetValue(d, out var agg);
                int count = agg?.Sessions ?? 0;
                var cellDate = d;
                string tip = count > 0
                    ? string.Format(CultureInfo.CurrentCulture,
                        Localized.Get("Calendar_DayTooltipFormat", "{0:d}\\n{1} økter · {2} min\\nSnitt score {3:F0} · pitch {4:F0} · resonans {5:F0}").Replace("\\n", "\n"),
                        d, count, agg!.Minutes, agg.Score, agg.PitchScore, agg.ResonanceScore)
                    : d.ToString("d", CultureInfo.CurrentCulture);
                cells.Add(new CalendarDayCell(d, d.Month == Month.Month, d == today, count,
                    agg?.Minutes ?? 0, agg?.Score ?? 0, agg?.PitchScore ?? 0, agg?.ResonanceScore ?? 0, tip,
                    new RelayCommand(() => { if (count > 0) _openDay?.Invoke(cellDate); })));
            }
            Days = cells;
        }
        catch { Days = Array.Empty<CalendarDayCell>(); }
    }

    // ── Heat-map legend (WPF parity): explains the session-intensity colour scale ─────────────────────────────────
    public string LegendHeading => Localized.Get("Calendar_Legend", "Intensitet");
    public sealed record LegendItem(IBrush Swatch, string Label);
    public IReadOnlyList<LegendItem> Legend { get; } = new[]
    {
        new LegendItem(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), Localized.Get("Calendar_LegendNone", "Ingen økt")),
        new LegendItem(new SolidColorBrush(Color.FromRgb(220, 60, 60)), Localized.Get("Calendar_LegendLow", "Lav score")),
        new LegendItem(new SolidColorBrush(Color.FromRgb(255, 180, 40)), Localized.Get("Calendar_LegendMed", "Middels")),
        new LegendItem(new SolidColorBrush(Color.FromRgb(60, 190, 120)), Localized.Get("Calendar_LegendHigh", "Høy score")),
    };
}
