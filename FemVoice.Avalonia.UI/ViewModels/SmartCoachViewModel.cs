using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Engine-backed SmartCoach page. Runs the REAL Core <see cref="SmartCoachEngine"/> (read-only) on the REAL database
/// to produce the daily recommendation, weekly session target, status summary, PROGRESS-TO-GOAL bars + baseline
/// confidence (WPF SmartCoachViewModel parity), and lets the user mark the day's recommendation COMPLETE (persisted
/// via SaveDailyRecommendation, exactly like WPF). No clinical logic is changed. Fails safe: with no database
/// (headless/tests) or an engine error it shows a truthful "unavailable" state instead of throwing.
/// </summary>
public sealed partial class SmartCoachViewModel : ObservableObject
{
    private readonly IDatabaseService? _database;
    private const int UserId = 1;

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
    public string HealthWarningHeading => Localized.Get("SmartCoach_HealthWarning", "Stemmehelse-varsel");
    public string DataNote => Localized.Get("SmartCoach_RealDataNote",
        "Beregnet av den ekte SmartCoach-motoren på dine lagrede økter.");

    // ── Complete today's recommendation (WPF parity: persisted via SaveDailyRecommendation) ───────────────────────
    /// <summary>True once today's recommendation is marked completed (read from the DB / set by CompleteRecommendation).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    private bool _isRecommendationCompleted;
    /// <summary>The Complete button is usable when the engine is available, a recommendation exists, and it is not yet done.</summary>
    public bool CanComplete => EngineAvailable && _hasRecommendation && !IsRecommendationCompleted;
    private readonly bool _hasRecommendation;
    public string CompleteLabel => Localized.Get("SmartCoach_MarkComplete", "Fullfør");
    public string CompletedLabel => Localized.Get("SmartCoach_Completed", "Fullført ✓");

    // ── Progress toward goals (WPF parity) ────────────────────────────────────────────────────────────────────
    /// <summary>Progress 0–100 toward the pitch / resonance / intonation goals (current ÷ target).</summary>
    public double PitchProgress { get; private set; }
    public double ResonanceProgress { get; private set; }
    public double IntonationProgress { get; private set; }
    public bool HasProgressToGoal { get; private set; }
    public string BaselineConfidence { get; private set; } = "";
    public string ProgressToGoalHeading => Localized.Get("SmartCoach_ProgressToGoal", "Progresjon mot mål");
    public string BaselineConfidenceLabel => Localized.Get("SmartCoach_BaselineConfidence", "Baseline-tillit");
    public string BaselineConfidenceDisplay => $"{BaselineConfidenceLabel}: {BaselineConfidence}";

    // ── Messages panel (WPF SmartCoach) + building-baseline state ─────────────────────────────────────────────────
    /// <summary>One coach message: title + body.</summary>
    public sealed record CoachMessage(string Title, string Body);
    public IReadOnlyList<CoachMessage> Messages { get; private set; } = Array.Empty<CoachMessage>();
    public bool HasMessages => Messages.Count > 0;
    public string MessagesHeading => Localized.Get("SmartCoach_Messages", "Meldinger");
    public string NoMessagesText => Localized.Get("SmartCoach_NoMessages", "Ingen nye meldinger.");
    /// <summary>True while there isn't enough data to establish a baseline (WPF's "building baseline" state).</summary>
    public bool IsBuildingBaseline { get; private set; }
    public string BuildingBaselineText => Localized.Get("SmartCoach_BuildingBaseline",
        "Bygger baseline — fullfør noen økter så tilpasser Smart Coach seg til stemmen din.");
    public string PitchProgressLabel => Localized.Get("Dashboard_Pitch", "Tonehøyde");
    public string ResonanceProgressLabel => Localized.Get("Dashboard_Resonance", "Resonans");
    public string IntonationProgressLabel => Localized.Get("Dashboard_Intonation", "Intonasjon");

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
        _database = database;
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
            var rec = engine.GenerateDailyRecommendation(UserId);
            _hasRecommendation = rec is not null;
            FocusLabel = FocusText(rec.FocusArea);
            RecommendationText = rec.RecommendationText;
            DurationText = $"{rec.RecommendedDurationMinutes} min";
            HasHealthWarning = rec.HealthWarning;
            HealthWarningText = rec.HealthWarningText ?? "";
            IsRecommendationCompleted = rec.IsCompleted;
            WeeklyTargetText = string.Format(Localized.Get("SmartCoach_WeeklyTargetFormat", "{0} økter/uke (mål)"), engine.GetWeeklySessionTarget(UserId));
            StatusSummary = engine.GetStatusSummary(UserId);
            BuildProgressToGoal(database, engine);
            BuildDetail(database);
            BuildMessages(database, engine);
            EngineAvailable = true;
        }
        catch (Exception ex)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("SmartCoach_Error", "SmartCoach er midlertidig utilgjengelig.") + $" ({ex.GetType().Name})";
        }
    }

    /// <summary>Mark today's recommendation completed and PERSIST it (WPF parity: SaveDailyRecommendation writes back
    /// IsCompleted/CompletedAt so the state survives). Guarded; no clinical logic changed.</summary>
    [RelayCommand]
    private void CompleteRecommendation()
    {
        if (_database is null || IsRecommendationCompleted) return;
        try
        {
            var rec = _database.GetDailyRecommendation(DateTime.Today, UserId);
            if (rec is null) return;
            rec.IsCompleted = true;
            rec.CompletedAt = DateTime.Now;
            _database.SaveDailyRecommendation(rec);
            IsRecommendationCompleted = true;
        }
        catch { /* best-effort; leave state unchanged on failure */ }
    }

    // Progress toward the active pitch/resonance/intonation goals + baseline confidence — the WPF SmartCoach
    // "Progress to goal" section, read-only (goal current-values recomputed in-memory from recent sessions; not persisted).
    private void BuildProgressToGoal(IDatabaseService database, SmartCoachEngine engine)
    {
        try
        {
            var baseline = engine.GetOrCalculateBaseline(UserId);
            if (baseline is null) return;
            BaselineConfidence = baseline.ConfidenceLevel switch
            {
                "high" => Localized.Get("SmartCoach_ConfidenceHigh", "Høy"),
                "medium" => Localized.Get("SmartCoach_ConfidenceMedium", "Medium"),
                _ => Localized.Get("SmartCoach_ConfidenceLow", "Lav"),
            };

            var goals = database.GetSmartCoachGoals(UserId, true);
            if (goals.Count == 0) goals = engine.GenerateGoals(UserId);

            var recent = database.GetRecentSessions(10, UserId);
            double recentPitch = recent.Where(s => s.AveragePitch > 0).Select(s => s.AveragePitch).DefaultIfEmpty(baseline.BaselinePitch).Average();
            double recentReson = recent.Where(s => s.ResonanceScore > 0).Select(s => s.ResonanceScore).DefaultIfEmpty(baseline.BaselineResonanceScore).Average();
            double recentInton = recent.Where(s => s.IntonationScore > 0).Select(s => s.IntonationScore).DefaultIfEmpty(baseline.BaselineIntonation).Average();

            foreach (var goal in goals.Where(g => !g.IsAchieved).Take(3))
            {
                double current = goal.GoalType switch
                {
                    "pitch" => recentPitch,
                    "resonance" => recentReson,
                    "intonation" => recentInton,
                    _ => goal.CurrentValue,
                };
                double progress = goal.TargetValue > 0 ? Math.Clamp(current / goal.TargetValue * 100, 0, 100) : 0;
                switch (goal.GoalType)
                {
                    case "pitch": PitchProgress = progress; break;
                    case "resonance": ResonanceProgress = progress; break;
                    case "intonation": IntonationProgress = progress; break;
                }
            }
            HasProgressToGoal = baseline.ConfidenceLevel != "low"
                                && (PitchProgress > 0 || ResonanceProgress > 0 || IntonationProgress > 0);
        }
        catch { HasProgressToGoal = false; }
    }

    // Coach messages (WPF SmartCoach): generate real motivational messages from the real data, then read the unread
    // ones. Also derive the building-baseline state (low baseline confidence). Guarded, best-effort.
    private void BuildMessages(IDatabaseService database, SmartCoachEngine engine)
    {
        try
        {
            try { engine.GenerateMotivationalMessages(UserId); } catch { /* generation is best-effort */ }
            var unread = database.GetUnreadMessages(UserId);
            Messages = unread
                .OrderByDescending(m => m.CreatedAt ?? m.Date)
                .Take(5)
                .Select(m => new CoachMessage(string.IsNullOrWhiteSpace(m.Title) ? m.MessageType : m.Title, m.Message))
                .ToList();
        }
        catch { Messages = Array.Empty<CoachMessage>(); }

        try { IsBuildingBaseline = engine.GetOrCalculateBaseline(UserId)?.ConfidenceLevel == "low"; }
        catch { IsBuildingBaseline = false; }
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
                        string.Format(Localized.Get("SmartCoach_DayHistoryFormat", "{0} økter · snitt {1}"), daySessions.Count, daySessions.Average(s => s.OverallScore).ToString("F0"))));
                else
                    hist.Add(new AnalysisSummaryMetric(day.ToString("ddd dd.MM"), "—"));
            }
            WeeklyHistory = hist;
        }
        catch { DetailMetrics = Array.Empty<AnalysisSummaryMetric>(); }
    }

    private static string FocusText(string? focusArea) => (focusArea ?? "").ToLowerInvariant() switch
    {
        "resonance" => Localized.Get("Goal_Resonance", "Resonans"),
        "pitch" => Localized.Get("Goal_Pitch", "Tonehøyde"),
        "intonation" => Localized.Get("Goal_Intonation", "Intonasjon"),
        "breathing" => Localized.Get("Goal_Breathing", "Pust"),
        _ => string.IsNullOrWhiteSpace(focusArea) ? "—" : focusArea!,
    };
}
