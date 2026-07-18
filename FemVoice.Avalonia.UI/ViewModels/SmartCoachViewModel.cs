using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Engine-backed SmartCoach page. Runs the REAL Core <see cref="SmartCoachEngine"/> (read-only) on the REAL database
/// to produce the daily recommendation, weekly session target, and status summary — the exact WPF logic, presented
/// in Avalonia. No clinical logic is changed. Fails safe: with no database (headless/tests) or an engine error it
/// shows a truthful "unavailable" state instead of throwing.
/// </summary>
public sealed class SmartCoachViewModel
{
    public string Title => Localized.Get("SmartCoach_Title", "Smart Coach");

    public bool EngineAvailable { get; }
    public string UnavailableNote { get; } = "";

    public string FocusLabel { get; } = "—";
    public string RecommendationText { get; } = "";
    public string DurationText { get; } = "";
    public string WeeklyTargetText { get; } = "";
    public string StatusSummary { get; } = "";
    public bool HasHealthWarning { get; }
    public string HealthWarningText { get; } = "";
    public string DataNote => Localized.Get("SmartCoach_RealDataNote",
        "Beregnet av den ekte SmartCoach-motoren på dine lagrede økter.");

    // ── Detail metrics (ported from the WPF SmartCoachDetailView) ─────────────────────────────────────────────
    /// <summary>Detail rows: day streak, sessions this week, total time this week, consistency (real DB stats).</summary>
    public IReadOnlyList<AnalysisSummaryMetric> DetailMetrics { get; private set; } = Array.Empty<AnalysisSummaryMetric>();
    /// <summary>Per-day session counts for the last 7 days (weekly history).</summary>
    public IReadOnlyList<AnalysisSummaryMetric> WeeklyHistory { get; private set; } = Array.Empty<AnalysisSummaryMetric>();
    public bool HasDetail => DetailMetrics.Count > 0;
    public string DetailHeading => Localized.Get("SmartCoach_Detail", "Detaljer");
    /// <summary>Today's-focus card heading — the real WPF key (WPF shows "Dagens fokus").</summary>
    public string TodaysFocusHeading => Localized.Get("SmartCoach_TodaysFocus", "Dagens fokus");
    public string WeeklyHistoryHeading => Localized.Get("SmartCoach_WeeklyHistory", "Ukeshistorikk");

    public SmartCoachViewModel(IDatabaseService? database, ILocalizationService? localization = null)
    {
        if (database is null)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("SmartCoach_NoDb",
                "SmartCoach krever databasen, som ikke er tilgjengelig i denne visningen.");
            return;
        }
        try
        {
            var engine = new SmartCoachEngine(database, localization ?? LocalizationService.Instance);
            var rec = engine.GenerateDailyRecommendation(1);
            FocusLabel = FocusText(rec.FocusArea);
            RecommendationText = rec.RecommendationText;
            DurationText = $"{rec.RecommendedDurationMinutes} min";
            HasHealthWarning = rec.HealthWarning;
            HealthWarningText = rec.HealthWarningText ?? "";
            WeeklyTargetText = $"{engine.GetWeeklySessionTarget(1)} økter/uke (mål)";
            StatusSummary = engine.GetStatusSummary(1);
            BuildDetail(database);
            EngineAvailable = true;
        }
        catch (Exception ex)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("SmartCoach_Error", "SmartCoach er midlertidig utilgjengelig.") + $" ({ex.GetType().Name})";
        }
    }

    // Real detail metrics + weekly history from the DB (day streak, sessions/time this week, consistency). Guarded.
    private void BuildDetail(IDatabaseService database)
    {
        try
        {
            var (_, consistency, streak) = database.GetProgressionStats();
            DateTime weekStart = DateTime.UtcNow.Date.AddDays(-7);
            var week = database.GetTrainingSessions(weekStart, DateTime.UtcNow).ToList();
            int totalMin = (int)Math.Round(week.Sum(s => s.DurationSeconds) / 60.0);
            // Voice-health: average of the recorded VoiceHealthScore over the week's sessions (WPF's 4th quick-stat).
            var health = week.Select(s => s.VoiceHealthScore).Where(h => h > 0).ToList();
            double avgHealth = health.Count > 0 ? health.Average() : 0;

            DetailMetrics = new List<AnalysisSummaryMetric>
            {
                new(Localized.Get("SmartCoach_DayStreak", "Dager på rad"), $"{streak}"),
                new(Localized.Get("SmartCoach_SessionsThisWeek", "Økter denne uken"), $"{week.Count}"),
                new(Localized.Get("SmartCoach_TotalTimeThisWeek", "Total tid denne uken"), $"{totalMin} min"),
                new(Localized.Get("SmartCoach_VoiceHealth", "Stemmehelse"), health.Count > 0 ? $"{avgHealth:F0} / 100" : "—"),
                new(Localized.Get("SmartCoach_Consistency", "Jevnhet"), $"{consistency:F0} %"),
            };

            // Weekly history: per local day for the last 7 days (newest first) — session count + average score.
            var byDay = week.GroupBy(s => s.StartTime.ToLocalTime().Date).ToDictionary(g => g.Key, g => g.ToList());
            var hist = new List<AnalysisSummaryMetric>();
            for (int i = 0; i < 7; i++)
            {
                var day = DateTime.Now.Date.AddDays(-i);
                if (byDay.TryGetValue(day, out var daySessions) && daySessions.Count > 0)
                    hist.Add(new AnalysisSummaryMetric(day.ToString("ddd dd.MM"),
                        $"{daySessions.Count} økter · snitt {daySessions.Average(s => s.OverallScore):F0}"));
                else
                    hist.Add(new AnalysisSummaryMetric(day.ToString("ddd dd.MM"), "—"));
            }
            WeeklyHistory = hist;
        }
        catch { DetailMetrics = Array.Empty<AnalysisSummaryMetric>(); }
    }

    private static string FocusText(string? focusArea) => (focusArea ?? "").ToLowerInvariant() switch
    {
        "resonance" => "Resonans",
        "pitch" => "Tonehøyde",
        "intonation" => "Intonasjon",
        "breathing" => "Pust",
        _ => string.IsNullOrWhiteSpace(focusArea) ? "—" : focusArea!,
    };
}
