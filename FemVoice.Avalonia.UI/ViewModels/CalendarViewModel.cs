using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

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
    public IReadOnlyList<AnalysisSummaryMetric> Days { get; } = Array.Empty<AnalysisSummaryMetric>();
    public string DataNote => Localized.Get("Calendar_RealDataNote", "Ekte treningshistorikk fra dine lagrede økter (siste 90 dager).");

    public CalendarViewModel(IDatabaseService? database)
    {
        if (database is null)
        {
            UnavailableNote = Localized.Get("Calendar_NoDb", "Kalender krever databasen, som ikke er tilgjengelig i denne visningen.");
            return;
        }
        try
        {
            var sessions = database.GetTrainingSessions(DateTime.UtcNow.AddDays(-90), DateTime.UtcNow);
            var byDay = sessions
                .GroupBy(s => s.StartTime.ToLocalTime().Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new AnalysisSummaryMetric(
                    g.Key.ToString("yyyy-MM-dd"),
                    $"{g.Count()} økter · snitt score {g.Average(s => s.OverallScore):F0}"))
                .ToList();
            Days = byDay.Count > 0
                ? byDay
                : new List<AnalysisSummaryMetric> { new("Ingen økter", "Ingen registrerte økter siste 90 dager.") };
            EngineAvailable = true;
        }
        catch (Exception ex)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("Calendar_Error", "Kalender er midlertidig utilgjengelig.") + $" ({ex.GetType().Name})";
        }
    }
}
