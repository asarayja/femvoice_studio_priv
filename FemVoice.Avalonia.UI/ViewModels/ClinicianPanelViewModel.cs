using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Input;         // IRelayCommand / RelayCommand (Back)
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;               // OutcomeProfile, OutcomeReport
using FemVoiceStudio.Services;             // ReportAssembler, OutcomeProfileBuilder, engines, stores
using FemVoice.Avalonia.Localization;      // Localized

namespace FemVoice.Avalonia.ViewModels;

/// <summary>One display row in the clinician outcome overview (label + value). Read-only.</summary>
public sealed record ClinicianRow(string Label, string Value);

/// <summary>
/// REAL clinician panel, ported read-only from the WPF ClinicianDashboard. It assembles a real
/// <c>OutcomeProfile</c> → <c>OutcomeReport</c> from the saved sessions via the frozen Core pipeline and renders the
/// outcome overview: composite voice score, recovery status/score, per-goal progress, and the most-effective
/// exercises. DESCRIPTIVE/REPORTING only (the Core report itself is explicitly "never a safety or training gate").
/// Everything is read-only + totally guarded — any gap in the (frozen) analytics pipeline degrades to a truthful
/// "not enough data" state instead of throwing. Nothing is written; no clinical logic is changed. Not IDisposable.
///
/// The WPF Clinician "Voice Metrics" (7 per-dimension 0–100 scores) are now REAL: the dashboard writes per-dimension
/// VoiceIntelligence records to <c>SessionAnalyticsStore</c> each session, and they are averaged + rendered here.
/// The "Learning Path" (stage/strengths/weaknesses/focus) and per-window "Dimension Trends" additionally require the
/// VoiceIntelligenceService trend engine to be wired into the SmartCoachEngine; those remain a follow-up. The
/// insights + exercise-concerns below are assembled by the pipeline and rendered.
/// </summary>
public sealed class ClinicianPanelViewModel
{
    public ClinicianPanelViewModel() : this(null, null) { }

    public ClinicianPanelViewModel(IDatabaseService? database, Action? onBack = null)
    {
        Title = Localized.Get("Clinician_Title", "Klinisk oversikt");
        BackLabel = Localized.Get("Common_Back", "Tilbake");
        Disclaimer = Localized.Get("Clinician_Panel_Disclaimer",
            "Beskrivende utfallsoversikt sammenstilt fra dine lagrede økter (kun lesing). Aldri en sikkerhets- " +
            "eller treningsport. Ingen klinisk endring, ingenting lagres.");
        BackCommand = new RelayCommand(() => onBack?.Invoke());
        TryBuild(database);
    }

    public string Title { get; }
    public string BackLabel { get; }
    public string Disclaimer { get; }
    public IRelayCommand BackCommand { get; }

    public bool HasReport { get; private set; }
    /// <summary>The assembled OutcomeReport (or null) — used by the View to export via the Core ExportWriter.</summary>
    public object? Report { get; private set; }
    public string EmptyMessage { get; private set; } =
        Localized.Get("Clinician_Panel_Empty", "Ikke nok data ennå. Fullfør noen økter på dashbordet for å bygge en utfallsoversikt.");

    public string ReportTitle { get; private set; } = "";
    /// <summary>Headline outcome scalars (composite score, recovery status/score, data sufficiency).</summary>
    public IReadOnlyList<ClinicianRow> Overview { get; private set; } = Array.Empty<ClinicianRow>();
    public IReadOnlyList<ClinicianRow> Goals { get; private set; } = Array.Empty<ClinicianRow>();
    public IReadOnlyList<ClinicianRow> TopExercises { get; private set; } = Array.Empty<ClinicianRow>();
    /// <summary>Recovery-intelligence detail (debt / overtraining / ACWR / recommendation) — WPF parity.</summary>
    public IReadOnlyList<ClinicianRow> RecoveryDetail { get; private set; } = Array.Empty<ClinicianRow>();
    public bool HasRecoveryDetail => RecoveryDetail.Count > 0;
    public string RecoveryHeading => Localized.Get("Clinician_RecoveryStatus", "Restitusjonsstatus");

    // ── Longitudinal insights (assembled by the pipeline's LongitudinalInsightEngine — WPF renders these; Avalonia
    //    did not) + exercise de-prioritisation concerns. Real data when present; the sections hide when empty. ──────
    public IReadOnlyList<string> Insights { get; private set; } = Array.Empty<string>();
    public bool HasInsights => Insights.Count > 0;
    public string InsightsHeading => Localized.Get("Clinician_LongitudinalInsights", "Langtidsinnsikt");
    public IReadOnlyList<string> ExerciseConcerns { get; private set; } = Array.Empty<string>();
    public bool HasExerciseConcerns => ExerciseConcerns.Count > 0;
    public string ExerciseConcernsHeading => Localized.Get("Clinician_ExerciseConcerns", "Øvelsesmerknader");

    // ── Voice metrics: per-dimension 0–100 scores (WPF Clinician), now REAL from the VoiceIntelligence records the
    //    dashboard writes per session. Rendered as label + score rows; dimensions with no data show "—". ────────────
    public IReadOnlyList<ClinicianRow> VoiceMetrics { get; private set; } = Array.Empty<ClinicianRow>();
    public bool HasVoiceMetrics => VoiceMetrics.Count > 0;
    public string VoiceMetricsHeading => Localized.Get("Clinician_VoiceMetrics", "Stemmemålinger");

    public string OverviewHeading => Localized.Get("Clinician_Panel_Overview", "Utfallsoversikt");
    public string GoalsHeading => Localized.Get("Clinician_GoalProgress", "Målfremdrift");
    public string ExercisesHeading => Localized.Get("Clinician_ExerciseEffectiveness", "Øvelseseffektivitet");
    public bool HasGoals => Goals.Count > 0;
    public bool HasExercises => TopExercises.Count > 0;

    private void TryBuild(IDatabaseService? database)
    {
        if (database is null) return;
        try
        {
            SessionAnalyticsStore analytics = database is DatabaseService concrete
                ? new SessionAnalyticsStore(new SqliteSessionAnalyticsRepository(concrete.ConnectionString))
                : new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());

            var builder = new OutcomeProfileBuilder(
                new SmartCoachEngine(database),
                new ExerciseEffectivenessEngine(analytics),
                new LongitudinalInsightEngine());

            DateTime now = DateTime.UtcNow;
            OutcomeProfile outcome = builder
                .AssembleFromStoreAsync(database, null, new RecoveryIntelligenceService(), analytics, now, userId: 1)
                .GetAwaiter().GetResult();

            OutcomeReport report = new ReportAssembler().BuildOutcomeReport(outcome, now.AddDays(-30), now, now);   // WPF canonical 30-day window
            Report = report;
            ReportTitle = report.Title ?? "";

            var overview = new List<ClinicianRow>
            {
                // CompositeVoiceScore == 0 is the model's "insufficient long-term data" sentinel — show that, not "0/100".
                new(Localized.Get("Clinician_Composite", "Samlet stemmescore"),
                    report.CompositeVoiceScore > 0 ? report.CompositeVoiceScore.ToString("F0", CultureInfo.InvariantCulture) + " / 100"
                                                   : Localized.Get("Clinician_Insufficient", "Ikke nok data ennå")),
                new(Localized.Get("Clinician_RecoveryStatus", "Restitusjonsstatus"),
                    (string.IsNullOrWhiteSpace(report.RecoveryStatus) ? "—" : report.RecoveryStatus)
                    + $" ({report.RecoveryScore:F0}/100)"),
                new(Localized.Get("Clinician_DataSufficiency", "Datagrunnlag"),
                    report.HasEnoughData ? Localized.Get("Clinician_Enough", "Tilstrekkelig")
                                         : Localized.Get("Clinician_NotEnough", "Utilstrekkelig")),
            };
            Overview = overview;

            // Goals now show current → target (WPF grid columns), not just percent.
            Goals = (report.GoalProgress ?? Array.Empty<GoalProgressEntry>())
                .Select(g => new ClinicianRow(
                    string.IsNullOrWhiteSpace(g.GoalType) ? g.PrimaryFocus.ToString() : g.GoalType,
                    $"{g.PercentComplete:F0}%" + (g.IsAchieved ? " ✓" : "")
                    + (g.TargetValue > 0 ? $"  ({g.CurrentValue:F0} / {g.TargetValue:F0})" : "")))
                .ToList();

            TopExercises = (report.TopExercises ?? Array.Empty<ExerciseEffectivenessProfile>())
                .Take(5)
                .Select(e => new ClinicianRow(
                    SafeExerciseName(e.ExerciseId),
                    e.CompositeEffectiveness.ToString("F0", CultureInfo.InvariantCulture)))
                .ToList();

            // Recovery-intelligence detail from the assembled OutcomeProfile (WPF Clinician shows debt / overtraining
            // / workload ratio / recommendation). Read-only, guarded per-field.
            var rp = outcome.RecoveryProgress;
            if (rp is not null)
            {
                var rows = new List<ClinicianRow>
                {
                    new(Localized.Get("Clinician_RecoveryDebtLabel", "Restitusjonsgjeld"), rp.RecoveryDebt.ToString("F0", CultureInfo.InvariantCulture)),
                    new(Localized.Get("Clinician_OvertrainingPredicted", "Overtrening forutsett"),
                        rp.OvertrainingPredicted ? Localized.Get("Common_Yes", "Ja") : Localized.Get("Common_No", "Nei")),
                    new(Localized.Get("Clinician_Workload", "Arbeidsbelastning (akutt/kronisk)"), rp.AcuteChronicWorkloadRatio.ToString("0.00", CultureInfo.InvariantCulture)),
                };
                if (!string.IsNullOrWhiteSpace(rp.RecommendationText))
                    rows.Add(new(Localized.Get("Clinician_Recommendation", "Anbefaling"), rp.RecommendationText));
                RecoveryDetail = rows;
            }

            // Longitudinal insights (WPF Clinician renders these; the pipeline assembles them via LongitudinalInsightEngine).
            // Each carries a localized "What happened" + "Why it matters"; render "What — Why". Guarded, real-data-only.
            try
            {
                Insights = (outcome.LongTermDevelopment?.Insights ?? Array.Empty<LongitudinalInsight>())
                    .Where(i => !string.IsNullOrWhiteSpace(i.What))
                    .Select(i => string.IsNullOrWhiteSpace(i.Why) ? i.What : $"{i.What} — {i.Why}")
                    .ToList();
            }
            catch { Insights = Array.Empty<string>(); }

            // Exercise de-prioritisation concerns (taxing / comfort-eroding) — WPF shows these warnings; Avalonia did not.
            try
            {
                ExerciseConcerns = (outcome.ExerciseEffectiveness?.Concerns ?? Array.Empty<ExerciseEffectivenessFlag>())
                    .Where(c => !string.IsNullOrWhiteSpace(c.Explanation))
                    .Select(c => c.Explanation)
                    .ToList();
            }
            catch { ExerciseConcerns = Array.Empty<string>(); }

            // Voice metrics (per-dimension 0–100) — real, averaged over the VoiceIntelligence records the dashboard
            // writes per session. A dimension with no data shows "—" (no fabrication).
            try
            {
                var records = analytics.GetSessionsAsync(now.AddDays(-90), now.AddDays(1), 1).GetAwaiter().GetResult();
                if (records.Count > 0)
                {
                    ClinicianRow Metric(string label, System.Func<FemVoiceStudio.Services.SessionAnalyticsRecord, double> sel)
                    {
                        var vals = records.Select(sel).Where(v => v > 0).ToList();
                        return new ClinicianRow(label, vals.Count > 0
                            ? $"{System.Math.Round(vals.Average())} / 100"
                            : Localized.Get("Clinician_NoDimData", "—"));
                    }
                    VoiceMetrics = new List<ClinicianRow>
                    {
                        Metric(Localized.Get("Dashboard_Resonance", "Resonans"), r => r.ResonanceScore100),
                        Metric(Localized.Get("Dashboard_Pitch", "Tonehøyde"), r => r.PitchScore100),
                        Metric(Localized.Get("Dashboard_Intonation", "Intonasjon"), r => r.IntonationScore100),
                        Metric(Localized.Get("Dashboard_VoiceHealth", "Stemmehelse"), r => r.AverageHealthScore),
                        Metric(Localized.Get("Dashboard_Comfort", "Komfort"), r => r.ComfortScore100),
                        Metric(Localized.Get("Dashboard_Recovery", "Restitusjon"), r => r.RecoveryScore100),
                        Metric(Localized.Get("Dashboard_Consistency", "Jevnhet"), r => r.ConsistencyScore100),
                    };
                }
            }
            catch { VoiceMetrics = Array.Empty<ClinicianRow>(); }

            HasReport = report.HasEnoughData || Goals.Count > 0 || TopExercises.Count > 0 || RecoveryDetail.Count > 0
                        || Insights.Count > 0 || ExerciseConcerns.Count > 0 || VoiceMetrics.Count > 0;
        }
        catch
        {
            HasReport = false;
        }
    }

    private static string SafeExerciseName(int exerciseId)
    {
        try { return ReportAssembler.ResolveExerciseName(exerciseId); }
        catch { return $"#{exerciseId}"; }
    }
}
