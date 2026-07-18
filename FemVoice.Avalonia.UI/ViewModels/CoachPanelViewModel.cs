using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;         // IRelayCommand / RelayCommand (Back)
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;               // ProfessionalReports (CoachReport), OutcomeProfile
using FemVoiceStudio.Services;             // ReportAssembler, OutcomeProfileBuilder, engines, stores
using FemVoice.Avalonia.Localization;      // Localized

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// REAL coach panel, ported read-only from the WPF CoachDashboard. It assembles a real <c>OutcomeProfile</c> from
/// the saved sessions via the frozen Core pipeline (SmartCoach + effectiveness + longitudinal engines) and renders
/// the <c>CoachReport</c>'s focus areas, recommendations, and detected development state (breakthrough / plateau /
/// regression) — all derived from the user's real training history. NOTHING is written and NO clinical logic is
/// changed; every engine is used read-only and the whole assembly is wrapped so any gap in the (frozen) analytics
/// pipeline degrades to a truthful "not enough data" state instead of throwing. Not IDisposable.
/// </summary>
public sealed class CoachPanelViewModel
{
    public CoachPanelViewModel() : this(null, null) { }

    public CoachPanelViewModel(IDatabaseService? database, Action? onBack = null, Action? onRefresh = null)
    {
        Title = Localized.Get("Coach_Title", "Coach-oversikt");
        BackLabel = Localized.Get("Common_Back", "Tilbake");
        Disclaimer = Localized.Get("Coach_Panel_Disclaimer",
            "Sammenstilt fra dine lagrede økter (kun lesing). Ingen klinisk endring, ingenting lagres. " +
            "Per-dimensjons resonans/intonasjon fylles først når økter registrerer disse målingene.");
        BackCommand = new RelayCommand(() => onBack?.Invoke());
        // Refresh re-opens the panel (it re-assembles fresh each open, WPF's RefreshCommand behaviour).
        RefreshCommand = new RelayCommand(() => (onRefresh ?? onBack)?.Invoke());
        TryBuild(database);
    }

    public string Title { get; }
    public string BackLabel { get; }
    public string Disclaimer { get; }
    public IRelayCommand RefreshCommand { get; }
    public string RefreshLabel => Localized.Get("Common_Refresh", "Oppdater");
    /// <summary>Current learning-stage status line (WPF Coach StageSummary), from the SmartCoach engine.</summary>
    public string StageSummary { get; private set; } = "";
    public bool HasStageSummary => StageSummary.Length > 0;
    /// <summary>Returns to the Reports page (no-op when opened standalone).</summary>
    public IRelayCommand BackCommand { get; }

    /// <summary>True when a real coach report was assembled from saved sessions.</summary>
    public bool HasReport { get; private set; }
    /// <summary>The assembled CoachReport (or null) — used by the View to export via the Core ExportWriter.</summary>
    public object? Report { get; private set; }
    /// <summary>Truthful message shown when there is not enough data (no DB / no sessions / pipeline unavailable).</summary>
    public string EmptyMessage { get; private set; } =
        Localized.Get("SmartCoach_NoData", "Ingen data ennå");

    public string ReportTitle { get; private set; } = "";
    public IReadOnlyList<string> FocusAreas { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> Recommendations { get; private set; } = Array.Empty<string>();
    /// <summary>Short line summarising a detected breakthrough (positive long-term development). Empty when none.</summary>
    public string DevelopmentSummary { get; private set; } = "";

    // ── Recovery needs (WPF CoachDashboard: shown FIRST, per the Health > Development priority) ──────────────────
    /// <summary>Recovery guidance from the outcome profile's RecoveryProgress (status + debt + safe recommendation).</summary>
    public string RecoveryNeedsText { get; private set; } = "";
    public bool HasRecoveryNeeds => RecoveryNeedsText.Length > 0;
    public string RecoveryNeedsHeading => Localized.Get("Coach_RecoveryNeeds", "Restitusjonsbehov");

    // ── Plateau warnings (WPF CoachDashboard: dedicated card; also surfaces regressions) ────────────────────────
    /// <summary>Detected plateau/regression lines (per-dimension, severity-aware). Empty when none.</summary>
    public IReadOnlyList<string> PlateauWarnings { get; private set; } = Array.Empty<string>();
    public bool HasPlateauWarnings => PlateauWarnings.Count > 0;
    public string PlateauWarningsHeading => Localized.Get("Coach_PlateauWarnings", "Platåvarsler");

    public string FocusHeading => Localized.Get("Coach_FocusAreas", "Fokusområder");
    public string RecommendationsHeading => Localized.Get("Coach_Recommendations", "Anbefalinger");
    public string DevelopmentHeading => Localized.Get("Coach_Breakthroughs", "Gjennombrudd");
    public bool HasFocus => FocusAreas.Count > 0;
    public bool HasRecommendations => Recommendations.Count > 0;
    public bool HasDevelopment => DevelopmentSummary.Length > 0;

    // Assemble the real OutcomeProfile → CoachReport read-only. Every step is guarded: a null DB, an interface-only
    // DB (no concrete connection string), or any exception inside the frozen pipeline degrades to HasReport=false
    // with the truthful EmptyMessage. No writes, no clinical change.
    private void TryBuild(IDatabaseService? database)
    {
        if (database is null) return;
        try
        {
            // Prefer the real SQLite analytics store (per-dimension history) when we can reach the DB file; otherwise
            // fall back to an in-memory (empty) store — the pipeline still yields a valid, data-light profile.
            SessionAnalyticsStore analytics = database is DatabaseService concrete
                ? new SessionAnalyticsStore(new SqliteSessionAnalyticsRepository(concrete.ConnectionString))
                : new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());

            var smartCoach = new SmartCoachEngine(database);
            try { StageSummary = smartCoach.GetStatusSummary(1) ?? ""; } catch { StageSummary = ""; }
            var effectiveness = new ExerciseEffectivenessEngine(analytics);
            var insight = new LongitudinalInsightEngine();
            var recovery = new RecoveryIntelligenceService();
            var builder = new OutcomeProfileBuilder(smartCoach, effectiveness, insight);

            DateTime now = DateTime.UtcNow;
            OutcomeProfile outcome = builder
                .AssembleFromStoreAsync(database, new LocalVoiceGoalProfileStore(), recovery, analytics, now, userId: 1)
                .GetAwaiter().GetResult();

            var assembler = new ReportAssembler();
            CoachReport report = assembler.BuildCoachReport(outcome, now.AddDays(-30), now, now);   // WPF canonical 30-day window
            Report = report;

            ReportTitle = report.Title ?? "";
            FocusAreas = (report.FocusAreas ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            Recommendations = (report.Recommendations ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            DevelopmentSummary = BuildBreakthroughSummary(report);
            RecoveryNeedsText = BuildRecoveryNeeds(outcome);
            PlateauWarnings = BuildPlateauWarnings(report);

            // "Real" only when the profile actually has evidence; otherwise keep the truthful empty state.
            HasReport = outcome.HasEnoughData || FocusAreas.Count > 0 || Recommendations.Count > 0
                        || DevelopmentSummary.Length > 0 || RecoveryNeedsText.Length > 0 || PlateauWarnings.Count > 0;
        }
        catch
        {
            HasReport = false;
        }
    }

    // Breakthrough line only (positive development). Guarded — the state object/fields are optional across versions.
    private static string BuildBreakthroughSummary(CoachReport report)
    {
        try
        {
            if (report.Breakthrough is null) return "";
            var dim = report.Breakthrough.Dimension.ToString();
            return $"{dim}: {Localized.Get("Coach_Panel_Breakthrough", "gjennombrudd registrert")}";
        }
        catch { return ""; }
    }

    // Recovery guidance from the outcome profile's RecoveryProgress — shown FIRST in the panel (Health > Development).
    // Only surfaced when recovery actually wants to be heard (severity above None / overtraining / real debt).
    private static string BuildRecoveryNeeds(OutcomeProfile outcome)
    {
        try
        {
            var r = outcome.RecoveryProgress;
            if (r is null) return "";
            bool wantsHeard = r.OvertrainingPredicted || r.RecoveryDebt >= 1.0
                              || (!string.IsNullOrWhiteSpace(r.Severity) && !r.Severity.Equals("None", StringComparison.OrdinalIgnoreCase));
            if (!wantsHeard) return "";
            var text = !string.IsNullOrWhiteSpace(r.RecommendationText)
                ? r.RecommendationText
                : $"{r.Status} · restitusjonsgjeld {r.RecoveryDebt:F0}";
            return text;
        }
        catch { return ""; }
    }

    // Plateau + regression warnings, per-dimension and severity-aware (WPF's Plateau-Warnings card). Guarded.
    private static IReadOnlyList<string> BuildPlateauWarnings(CoachReport report)
    {
        var lines = new List<string>();
        try
        {
            if (report.Plateau is not null)
            {
                var p = report.Plateau;
                lines.Add($"{p.Dimension}: {Localized.Get("Coach_Panel_Plateau", "platå")} " +
                          $"({p.PlateauDurationDays} d, styrke {p.SeverityScore:F0})");
            }
            if (report.Regression is not null)
            {
                var g = report.Regression;
                lines.Add($"{g.Dimension}: {Localized.Get("Coach_Panel_Regression", "tilbakegang")} (styrke {g.SeverityScore:F0})");
            }
        }
        catch { return Array.Empty<string>(); }
        return lines;
    }
}
