using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;   // ProgressionService, LevelClassificationSystem
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One recent-session row for the statistics list (date / difficulty / duration / score). Read-only.</summary>
public sealed record StatSessionRow(string Date, string Difficulty, string Duration, string Score);

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

    // ── Current-level card + recent-sessions list (ported from WPF StatisticsWindow) ──────────────────────────
    public bool HasLevel { get; private set; }
    public string LevelName { get; private set; } = "—";
    public double LevelProgressPercent { get; private set; }
    public string LevelProgressText { get; private set; } = "";
    public string CurrentLevelHeading => Localized.Get("Statistics_CurrentLevel", "Nåværende nivå");
    public IReadOnlyList<StatSessionRow> RecentSessions { get; private set; } = Array.Empty<StatSessionRow>();
    public bool HasRecentSessions => RecentSessions.Count > 0;
    public string RecentHeading => Localized.Get("Statistics_RecentSessions", "Siste økter");

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
                new(Localized.Get("Statistics_TotalSessions", "Totalt antall økter"), sessions.Count.ToString()),
                new(Localized.Get("Statistics_CurrentStreak", "Nåværende streak"), $"{streak} dager"),
                new("Dager trent", daysTrained.ToString()),
                new("Total tid", $"{totalMinutes} min"),
                new(Localized.Get("Statistics_AveragePitch", "Gjennomsnittlig pitch"), avgPitch > 0 ? $"{avgPitch:F0} Hz" : "—"),
                new("Konsistens", $"{consistency:F0} %"),
                new("Snitt score", $"{avgScore:F0} / 100"),
            };

            // Current-level card (real level + progress toward promotion).
            try
            {
                var status = new ProgressionService(database, LocalizationService.Instance).GetProgressionStatus();
                var level = LevelClassificationSystem.FromDifficultyLevel(status.CurrentLevel);
                LevelName = LevelClassificationSystem.GetLevelName(level);
                if (status.SessionsRequiredForPromotion > 0)
                {
                    LevelProgressPercent = Math.Round(Math.Clamp(100.0 * status.SessionsAtCurrentLevel / status.SessionsRequiredForPromotion, 0, 100));
                    LevelProgressText = string.Format(Localized.Get("Level_SessionsAtLevelFormat", "{0} / {1} økter på dette nivået"), status.SessionsAtCurrentLevel, status.SessionsRequiredForPromotion);
                }
                HasLevel = true;
            }
            catch { HasLevel = false; }

            // Recent-sessions list (newest first): date / difficulty / duration / score.
            RecentSessions = sessions
                .OrderByDescending(s => s.StartTime).Take(10)
                .Select(s => new StatSessionRow(
                    s.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    s.DifficultyLevel.ToString(),
                    $"{(int)Math.Round(s.DurationSeconds / 60.0)} min",
                    $"{s.OverallScore:F0} / 100"))
                .ToList();

            EngineAvailable = true;
        }
        catch (Exception ex)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("Statistics_Error", "Statistikk er midlertidig utilgjengelig.") + $" ({ex.GetType().Name})";
        }
    }
}
