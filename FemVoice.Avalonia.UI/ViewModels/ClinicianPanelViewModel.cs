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
/// </summary>
public sealed class ClinicianPanelViewModel
{
    public ClinicianPanelViewModel() : this(null, null) { }

    public ClinicianPanelViewModel(IDatabaseService? database, Action? onBack = null)
    {
        Title = Localized.Get("Clinician_Panel_Title", "Klinikerpanel");
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
    public string EmptyMessage { get; private set; } =
        Localized.Get("Clinician_Panel_Empty", "Ikke nok data ennå. Fullfør noen økter på dashbordet for å bygge en utfallsoversikt.");

    public string ReportTitle { get; private set; } = "";
    /// <summary>Headline outcome scalars (composite score, recovery status/score, data sufficiency).</summary>
    public IReadOnlyList<ClinicianRow> Overview { get; private set; } = Array.Empty<ClinicianRow>();
    public IReadOnlyList<ClinicianRow> Goals { get; private set; } = Array.Empty<ClinicianRow>();
    public IReadOnlyList<ClinicianRow> TopExercises { get; private set; } = Array.Empty<ClinicianRow>();

    public string OverviewHeading => Localized.Get("Clinician_Panel_Overview", "Utfallsoversikt");
    public string GoalsHeading => Localized.Get("Clinician_Panel_Goals", "Målfremgang");
    public string ExercisesHeading => Localized.Get("Clinician_Panel_Exercises", "Mest effektive øvelser");
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

            OutcomeReport report = new ReportAssembler().BuildOutcomeReport(outcome, now.AddDays(-90), now, now);
            ReportTitle = report.Title ?? "";

            var overview = new List<ClinicianRow>
            {
                // CompositeVoiceScore == 0 is the model's "insufficient long-term data" sentinel — show that, not "0/100".
                new(Localized.Get("Clinician_Composite", "Samlet stemmescore"),
                    report.CompositeVoiceScore > 0 ? report.CompositeVoiceScore.ToString("F0", CultureInfo.InvariantCulture) + " / 100"
                                                   : Localized.Get("Clinician_Insufficient", "Ikke nok data ennå")),
                new(Localized.Get("Clinician_Recovery", "Restitusjon"),
                    (string.IsNullOrWhiteSpace(report.RecoveryStatus) ? "—" : report.RecoveryStatus)
                    + $" ({report.RecoveryScore:F0}/100)"),
                new(Localized.Get("Clinician_DataSufficiency", "Datagrunnlag"),
                    report.HasEnoughData ? Localized.Get("Clinician_Enough", "Tilstrekkelig")
                                         : Localized.Get("Clinician_NotEnough", "Utilstrekkelig")),
            };
            Overview = overview;

            Goals = (report.GoalProgress ?? Array.Empty<GoalProgressEntry>())
                .Select(g => new ClinicianRow(
                    string.IsNullOrWhiteSpace(g.GoalType) ? g.PrimaryFocus.ToString() : g.GoalType,
                    $"{g.PercentComplete:F0}%" + (g.IsAchieved ? " ✓" : "")))
                .ToList();

            TopExercises = (report.TopExercises ?? Array.Empty<ExerciseEffectivenessProfile>())
                .Take(5)
                .Select(e => new ClinicianRow(
                    SafeExerciseName(e.ExerciseId),
                    e.CompositeEffectiveness.ToString("F0", CultureInfo.InvariantCulture)))
                .ToList();

            HasReport = report.HasEnoughData || Goals.Count > 0 || TopExercises.Count > 0;
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
