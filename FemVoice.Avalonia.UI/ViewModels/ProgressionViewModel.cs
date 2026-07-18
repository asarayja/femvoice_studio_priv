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
/// Engine-backed Progression page. Reads the REAL training level (UserSettings + <see cref="LevelClassificationSystem"/>),
/// the REAL recent-session score/pitch averages, and the REAL <see cref="ProgressionService"/> summary from the real
/// database — no demo data, the same sources WPF uses. Read-only; no clinical logic changed. Fails safe with no DB.
///
/// NOTE on the WPF ProgressionDashboard "Parameter Graph" (per-dimension live 0–100 score bars for Resonance/Pitch/
/// Intonation/VoiceHealth/Comfort/Recovery/Consistency + direction arrows + quickest-improvement): that is driven by
/// the Voice-Intelligence trend written to <c>SessionAnalyticsStore</c>. The Avalonia capture path produces real PITCH
/// and RESONANCE only (intonation/health/comfort/recovery/consistency are not computed here), and does NOT write those
/// snapshots — so a per-dimension parameter graph would be empty or fabricated. It is intentionally omitted (no demo
/// data) pending real per-dimension DSP + snapshot persistence. This page shows the real level-based parameters instead.
/// </summary>
public sealed partial class ProgressionViewModel : ObservableObject
{
    private readonly Action? _startExercise;
    public string Title => Localized.Get("Main_Progression", "Progresjon");

    public bool EngineAvailable { get; }
    public string UnavailableNote { get; } = "";

    public string LevelName { get; } = "—";
    public string LevelEmoji { get; } = "";
    public string LevelDescription { get; } = "";
    public string FemVoiceScoreText { get; } = "—";
    public double FemVoiceScore { get; }
    public int SessionCount { get; }
    public string SessionCountText { get; } = "—";
    public string AveragePitchText { get; } = "—";
    public string Summary { get; } = "";
    public string RecommendedDifficultyText { get; } = "";
    public string DataNote => Localized.Get("Progression_RealDataNote",
        "Nivå, poeng og sammendrag fra dine faktiske lagrede økter (ekte progresjonsmotor).");

    // ── Richer detail (ported from WPF ProgressionDashboard) ──────────────────────────────────────────────────
    /// <summary>Progress toward the next level, 0–100 (sessions-at-level / sessions-required).</summary>
    public double ProgressPercent { get; private set; }
    public string ProgressToNextText { get; private set; } = "";
    public bool HasProgress { get; private set; }
    public string ProgressHeading => Localized.Get("Dashboard_Progress", "Fremgang til neste nivå");
    /// <summary>Real stat rows (total sessions, streak, avg pitch vs goal, consistency).</summary>
    public IReadOnlyList<AnalysisSummaryMetric> StatMetrics { get; private set; } = Array.Empty<AnalysisSummaryMetric>();
    /// <summary>Real target parameters for the current level (pitch range, resonance minimum, focus area).</summary>
    public IReadOnlyList<AnalysisSummaryMetric> Parameters { get; private set; } = Array.Empty<AnalysisSummaryMetric>();
    public string ParametersHeading => Localized.Get("Dashboard_Parameters", "Parametere");
    /// <summary>FemVoice-score history bars (px 0–100) from the saved sessions, oldest→newest.</summary>
    public IReadOnlyList<double> ScoreHistoryBars { get; private set; } = Array.Empty<double>();
    public bool HasScoreHistory => ScoreHistoryBars.Count > 0;
    public string ScoreHistoryHeading => Localized.Get("Dashboard_ScoreHistory", "Score-historikk");
    /// <summary>Weekly summary (sessions / minutes / average score this week).</summary>
    public string WeeklySummary { get; private set; } = "";
    public string WeeklyHeading => Localized.Get("Dashboard_WeeklySummary", "Denne uken");
    public const double HistoryHeightPx = 90;

    // ── Today's focus + Start exercise (WPF ProgressionDashboard parity) ──────────────────────────────────────
    /// <summary>The current level's focus area (real, from LevelClassificationSystem) — WPF "Dagens fokusområde".</summary>
    public string TodaysFocusText { get; private set; } = "";
    public bool HasTodaysFocus { get; private set; }
    public string TodaysFocusHeading => Localized.Get("Dashboard_TodaysFocus", "Dagens fokusområde");
    public string StartExerciseLabel => Localized.Get("Dashboard_StartExercise", "Start øvelse");

    // ── Per-dimension parameter graph (WPF ProgressionDashboard) — now REAL, from the VoiceIntelligence records the
    //    dashboard writes on each session (SessionAnalyticsStore). Dimensions with no data show honestly as "—". ─────
    public const double ParamBarHeightPx = 100;
    /// <summary>One parameter row: dimension label + 0–100 score + bar px + a trend direction arrow (↑/↓/→).</summary>
    public sealed record DimensionBar(string Label, double Score, double BarPx, bool HasData, string Direction);
    public IReadOnlyList<DimensionBar> ParameterGraph { get; private set; } = Array.Empty<DimensionBar>();
    public bool HasParameterGraph => ParameterGraph.Count > 0;
    public string ParameterGraphHeading => Localized.Get("Dashboard_Parameters", "Parametere");
    /// <summary>The most-improved dimension this window + how many points it gained (WPF "Quickest Improvement").</summary>
    public string QuickestImprovement { get; private set; } = "";
    public bool HasQuickestImprovement => QuickestImprovement.Length > 0;
    public string QuickestImprovementHeading => Localized.Get("Dashboard_QuickestImprovement", "Raskeste forbedring");
    /// <summary>Opens the exercise guide to begin training (WPF's "Start Exercise" action). No-op if not wired.</summary>
    [RelayCommand] private void StartExercise() => _startExercise?.Invoke();

    public ProgressionViewModel(IDatabaseService? database, ILocalizationService? localization = null, Action? startExercise = null)
    {
        _startExercise = startExercise;
        if (database is null)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("Progression_NoDb",
                "Progresjon krever databasen, som ikke er tilgjengelig i denne visningen.");
            return;
        }
        try
        {
            var settings = database.GetUserSettings();
            var level = (TrainingLevel)settings.CurrentDifficulty;
            LevelName = LevelClassificationSystem.GetLevelName(level);
            LevelEmoji = LevelClassificationSystem.GetLevelEmoji(level);
            LevelDescription = LevelClassificationSystem.GetLevelFocus(level);
            TodaysFocusText = LevelClassificationSystem.GetFocusArea(level);
            HasTodaysFocus = !string.IsNullOrWhiteSpace(TodaysFocusText);

            IReadOnlyList<FemVoiceStudio.Models.TrainingSession> recent = database.GetRecentSessions(20);
            SessionCount = recent.Count;
            SessionCountText = $"{recent.Count} lagrede økter";
            if (recent.Count > 0)
            {
                FemVoiceScore = Math.Round(recent.Average(s => s.OverallScore));
                FemVoiceScoreText = $"{FemVoiceScore:F0}";
                AveragePitchText = $"{recent.Average(s => s.AveragePitch):F0} Hz";
            }

            var ps = new ProgressionService(database, localization ?? LocalizationService.Instance);
            Summary = ps.GetProgressionSummary();
            RecommendedDifficultyText = $"Anbefalt nivå: {ps.GetRecommendedDifficulty()}";

            // Progress toward the next level + real stat rows (from the real ProgressionStatus).
            var status = ps.GetProgressionStatus();
            if (status.SessionsRequiredForPromotion > 0)
            {
                ProgressPercent = Math.Round(Math.Clamp(100.0 * status.SessionsAtCurrentLevel / status.SessionsRequiredForPromotion, 0, 100));
                ProgressToNextText = $"{status.SessionsAtCurrentLevel} / {status.SessionsRequiredForPromotion} økter på dette nivået";
                HasProgress = true;
            }
            StatMetrics = new List<AnalysisSummaryMetric>
            {
                new(Localized.Get("Main_TotalSessions", "Totalt antall økter"), status.TotalSessions.ToString()),
                new(Localized.Get("Main_Streak", "Dager på rad"), status.CurrentStreak.ToString()),
                new(Localized.Get("Dashboard_AveragePitch", "Snitt tonehøyde"),
                    status.AveragePitch > 0 ? $"{status.AveragePitch:F0} Hz (mål {status.AveragePitchGoal:F0} Hz)" : "—"),
                new(Localized.Get("Dashboard_Consistency", "Jevnhet"), $"{status.Consistency:F0} %"),
            };

            // Real target parameters for the current level.
            var (pMin, pMax) = LevelClassificationSystem.GetPitchRange(level);
            Parameters = new List<AnalysisSummaryMetric>
            {
                new(Localized.Get("Dashboard_TargetPitch", "Målområde tonehøyde"), $"{pMin:F0}–{pMax:F0} Hz"),
                new(Localized.Get("Dashboard_ResonanceMin", "Minste resonans"), $"{LevelClassificationSystem.GetResonanceMinimum(level):F0}"),
                new(Localized.Get("SmartCoach_TodaysFocus", "Dagens fokus"), LevelClassificationSystem.GetFocusArea(level)),
            };

            // FemVoice-score history (oldest→newest) as bar heights in px.
            var ordered = recent.AsEnumerable().Reverse().ToList();   // GetRecentSessions is newest-first
            ScoreHistoryBars = ordered.Select(s => Math.Clamp(s.OverallScore, 0, 100) / 100.0 * (HistoryHeightPx - 4) + 4).ToList();

            // Weekly summary (this week).
            var week = database.GetTrainingSessions(DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow);
            int weekMin = (int)Math.Round(week.Sum(s => s.DurationSeconds) / 60.0);
            double weekAvg = week.Count > 0 ? week.Average(s => s.OverallScore) : 0;
            WeeklySummary = $"{week.Count} økter · {weekMin} min · snitt score {weekAvg:F0}";

            BuildParameterGraph(database);

            EngineAvailable = true;
        }
        catch (Exception ex)
        {
            EngineAvailable = false;
            UnavailableNote = Localized.Get("Progression_Error", "Progresjon er midlertidig utilgjengelig.") + $" ({ex.GetType().Name})";
        }
    }

    // Real per-dimension parameter graph: average each dimension over the recent VoiceIntelligence records the
    // dashboard writes per session. A dimension with no records carrying a value shows honestly as "no data".
    private void BuildParameterGraph(IDatabaseService database)
    {
        try
        {
            if (database is not DatabaseService concrete) return;
            var analytics = new FemVoiceStudio.Services.SessionAnalyticsStore(
                new FemVoiceStudio.Services.SqliteSessionAnalyticsRepository(concrete.ConnectionString));
            var records = analytics.GetSessionsAsync(DateTime.UtcNow.AddDays(-90), DateTime.UtcNow.AddDays(1), 1)
                .GetAwaiter().GetResult();
            if (records.Count == 0) return;

            // Ordered oldest→newest for the trend (records come newest-first from the store).
            var ordered = records.OrderBy(r => r.StartedAt).ToList();
            int half = ordered.Count / 2;

            // Direction arrow + improvement delta per dimension: compare the recent half's average to the earlier half's.
            (string arrow, double delta) Trend(System.Func<FemVoiceStudio.Services.SessionAnalyticsRecord, double> sel)
            {
                if (ordered.Count < 2) return ("→", 0);
                var early = ordered.Take(Math.Max(1, half)).Select(sel).Where(v => v > 0).ToList();
                var late = ordered.Skip(half).Select(sel).Where(v => v > 0).ToList();
                if (early.Count == 0 || late.Count == 0) return ("→", 0);
                double d = late.Average() - early.Average();
                return (d > 2 ? "↑" : d < -2 ? "↓" : "→", d);
            }

            string bestDim = ""; double bestGain = 0;
            DimensionBar Bar(string label, System.Func<FemVoiceStudio.Services.SessionAnalyticsRecord, double> sel)
            {
                var vals = records.Select(sel).Where(v => v > 0).ToList();
                bool has = vals.Count > 0;
                double score = has ? Math.Clamp(vals.Average(), 0, 100) : 0;
                var (arrow, delta) = has ? Trend(sel) : ("→", 0);
                if (has && delta > bestGain) { bestGain = delta; bestDim = label; }
                return new DimensionBar(label, Math.Round(score), score / 100.0 * (ParamBarHeightPx - 4) + 4, has, arrow);
            }

            ParameterGraph = new List<DimensionBar>
            {
                Bar(Localized.Get("Dashboard_Resonance", "Resonans"), r => r.ResonanceScore100),
                Bar(Localized.Get("Dashboard_Pitch", "Tonehøyde"), r => r.PitchScore100),
                Bar(Localized.Get("Dashboard_Intonation", "Intonasjon"), r => r.IntonationScore100),
                Bar(Localized.Get("Dashboard_VoiceHealth", "Stemmehelse"), r => r.AverageHealthScore),
                Bar(Localized.Get("Dashboard_Comfort", "Komfort"), r => r.ComfortScore100),
                Bar(Localized.Get("Dashboard_Recovery", "Restitusjon"), r => r.RecoveryScore100),
                Bar(Localized.Get("Dashboard_Consistency", "Jevnhet"), r => r.ConsistencyScore100),
            };
            if (bestDim.Length > 0 && bestGain > 2)
                QuickestImprovement = $"{bestDim} (+{bestGain:F0})";
        }
        catch { ParameterGraph = Array.Empty<DimensionBar>(); }
    }
}
