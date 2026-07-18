using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One day cell in the month grid: date, label, session count, and flags for styling. Read-only.</summary>
public sealed class CalendarDayCell
{
    public CalendarDayCell(DateTime date, bool inMonth, bool isToday, int sessions, IRelayCommand open)
    {
        Date = date; InMonth = inMonth; IsToday = isToday; Sessions = sessions; Open = open;
    }
    public DateTime Date { get; }
    public string DayNumber => Date.Day.ToString(CultureInfo.InvariantCulture);
    public bool InMonth { get; }
    /// <summary>Dim out-of-month days (converter-free opacity for the day number).</summary>
    public double NumberOpacity => InMonth ? 1.0 : 0.3;
    public bool IsToday { get; }
    public int Sessions { get; }
    public bool HasSessions => Sessions > 0;
    /// <summary>Session-count dot text (e.g. "•" / "2") shown under the day number when there are sessions.</summary>
    public string SessionMark => Sessions == 0 ? "" : Sessions == 1 ? "•" : Sessions.ToString(CultureInfo.InvariantCulture);
    public IRelayCommand Open { get; }
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

    // Build a 6×7 month grid (Monday-first). Each in-range day carries its real session count; a day with sessions is
    // clickable → openDay(date). Fails safe (empty grid) on any DB error.
    private void BuildGrid()
    {
        MonthLabel = Month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        try
        {
            // Grid starts on the Monday on/before the 1st of the month; 42 cells (6 weeks).
            int offset = ((int)Month.DayOfWeek + 6) % 7;   // Mon=0 … Sun=6
            var gridStart = Month.AddDays(-offset);
            var gridEnd = gridStart.AddDays(42);

            var byDay = new Dictionary<DateTime, int>();
            if (_database is not null)
            {
                var sessions = _database.GetTrainingSessions(gridStart.ToUniversalTime(), gridEnd.ToUniversalTime());
                foreach (var g in sessions.GroupBy(s => s.StartTime.ToLocalTime().Date))
                    byDay[g.Key] = g.Count();
            }

            var today = DateTime.Now.Date;
            var cells = new List<CalendarDayCell>(42);
            for (int i = 0; i < 42; i++)
            {
                var d = gridStart.AddDays(i);
                int count = byDay.TryGetValue(d, out var c) ? c : 0;
                var cellDate = d;
                cells.Add(new CalendarDayCell(d, d.Month == Month.Month, d == today, count,
                    new RelayCommand(() => { if (count > 0) _openDay?.Invoke(cellDate); })));
            }
            Days = cells;
        }
        catch { Days = Array.Empty<CalendarDayCell>(); }
    }
}
