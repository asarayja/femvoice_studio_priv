using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One clickable training-day row: date + summary + a command to open that day's details.</summary>
public sealed class CalendarDayItem
{
    public CalendarDayItem(DateTime date, string label, string summary, IRelayCommand open)
    {
        Date = date; Label = label; Summary = summary; Open = open;
    }
    public DateTime Date { get; }
    public string Label { get; }
    public string Summary { get; }
    public IRelayCommand Open { get; }
}

/// <summary>
/// Calendar / history page (ported from the WPF CalendarWindow). Lists the user's REAL training days from the real
/// database (last 90 days), each with its session count + average score. Read-only; no clinical logic changed.
/// Fails safe with no DB.
/// </summary>
public sealed class CalendarViewModel
{
    public string Title => Localized.Get("Calendar_Title", "Kalender / historikk");

    public bool EngineAvailable { get; }
    public string UnavailableNote { get; } = "";
    public IReadOnlyList<CalendarDayItem> Days { get; } = Array.Empty<CalendarDayItem>();
    public bool HasDays => Days.Count > 0;
    public string DataNote => Localized.Get("Calendar_RealDataNote", "Ekte treningshistorikk fra dine lagrede økter (siste 90 dager). Klikk en dag for detaljer.");

    public CalendarViewModel(IDatabaseService? database, Action<DateTime>? openDay = null)
    {
        if (database is null)
        {
            UnavailableNote = Localized.Get("Calendar_NoDb", "Kalender krever databasen, som ikke er tilgjengelig i denne visningen.");
            return;
        }
        try
        {
            var sessions = database.GetTrainingSessions(DateTime.UtcNow.AddDays(-90), DateTime.UtcNow);
            Days = sessions
                .GroupBy(s => s.StartTime.ToLocalTime().Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new CalendarDayItem(
                    g.Key,
                    g.Key.ToString("yyyy-MM-dd"),
                    $"{g.Count()} økter · snitt score {g.Average(s => s.OverallScore):F0}",
                    new RelayCommand(() => openDay?.Invoke(g.Key))))
                .ToList();
            EngineAvailable = true;
        }
        catch (Exception ex)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("Calendar_Error", "Kalender er midlertidig utilgjengelig.") + $" ({ex.GetType().Name})";
        }
    }
}
