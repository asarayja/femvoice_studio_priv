using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Statistics page (ported from the WPF StatisticsWindow). Shows REAL training statistics from the real database —
/// total sessions, current streak, average pitch/consistency (from `GetProgressionStats`), average score, days
/// trained, and total time. Read-only; no clinical logic changed. Fails safe with no DB.
/// </summary>
public sealed class StatisticsViewModel
{
    public string Title => Localized.Get("Statistics_Title", "Statistikk");

    public bool EngineAvailable { get; }
    public string UnavailableNote { get; } = "";
    public IReadOnlyList<AnalysisSummaryMetric> Tiles { get; } = Array.Empty<AnalysisSummaryMetric>();
    public string DataNote => Localized.Get("Statistics_RealDataNote", "Ekte statistikk fra dine lagrede økter.");

    public StatisticsViewModel(IDatabaseService? database)
    {
        if (database is null)
        {
            UnavailableNote = Localized.Get("Statistics_NoDb", "Statistikk krever databasen, som ikke er tilgjengelig i denne visningen.");
            return;
        }
        try
        {
            var sessions = database.GetRecentSessions(500);
            var (avgPitch, consistency, streak) = database.GetProgressionStats();
            int daysTrained = database.GetTrainingDaysCount(DateTime.UtcNow.AddYears(-1), DateTime.UtcNow);
            double avgScore = sessions.Count > 0 ? sessions.Average(s => s.OverallScore) : 0;
            int totalMinutes = (int)Math.Round(sessions.Sum(s => s.DurationSeconds) / 60.0);

            Tiles = new List<AnalysisSummaryMetric>
            {
                new("Totalt økter", sessions.Count.ToString()),
                new("Streak", $"{streak} dager"),
                new("Dager trent", daysTrained.ToString()),
                new("Total tid", $"{totalMinutes} min"),
                new("Snitt tonehøyde", avgPitch > 0 ? $"{avgPitch:F0} Hz" : "—"),
                new("Konsistens", $"{consistency:F0} %"),
                new("Snitt score", $"{avgScore:F0} / 100"),
            };
            EngineAvailable = true;
        }
        catch (Exception ex)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("Statistics_Error", "Statistikk er midlertidig utilgjengelig.") + $" ({ex.GetType().Name})";
        }
    }
}
