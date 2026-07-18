using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;               // OutcomeProfile, OutcomeReport
using FemVoiceStudio.Services;             // ReportAssembler, OutcomeProfileBuilder, engines, stores
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// REAL case-review panel, ported read-only from the WPF CaseReviewWindow. A professional picks a review TYPE and
/// PERIOD; this assembles a real <see cref="OutcomeProfile"/> → <see cref="OutcomeReport"/> for that period via the
/// frozen Core pipeline and presents the outcome overview for review. Opened from the Reports page. Fully guarded →
/// truthful "not enough data" state. DISPLAY-ONLY: it does NOT persist saved reviews (the ManualOverridesStore-style
/// write is deferred). No clinical logic changed.
/// </summary>
public sealed partial class CaseReviewPanelViewModel : ObservableObject
{
    private readonly IDatabaseService? _database;

    public CaseReviewPanelViewModel() : this(null, null) { }

    public CaseReviewPanelViewModel(IDatabaseService? database, Action? onBack = null)
    {
        _database = database;
        BackCommand = new RelayCommand(() => onBack?.Invoke());
        _selectedReviewType = ReviewTypes[0];
        _periodStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        _periodEnd = DateTime.UtcNow.Date;
        Build();
    }

    public IRelayCommand BackCommand { get; }
    public string Title => Localized.Get("CaseReview_Panel_Title", "Saksgjennomgang");
    public string BackLabel => Localized.Get("Common_Back", "Tilbake");
    public string EvaluateLabel => Localized.Get("CaseReview_Evaluate", "Bygg gjennomgang");
    public string Disclaimer => Localized.Get("CaseReview_Panel_Disclaimer",
        "Saksgjennomgang sammenstilt fra dine lagrede økter for valgt periode (kun lesing). Beskrivende — aldri en " +
        "sikkerhets- eller treningsport. Ingen klinisk endring; lagrede gjennomganger skrives ikke ennå.");

    public IReadOnlyList<string> ReviewTypes { get; } = new[]
    {
        Localized.Get("CaseReview_TypeMonthly", "Månedlig"),
        Localized.Get("CaseReview_TypeGoal", "Mål"),
        Localized.Get("CaseReview_TypeProgress", "Fremgang"),
        Localized.Get("CaseReview_TypeRecovery", "Restitusjon"),
    };
    [ObservableProperty] private string _selectedReviewType;
    [ObservableProperty] private DateTime _periodStart;
    [ObservableProperty] private DateTime _periodEnd;

    public string ReviewTypeLabel => Localized.Get("CaseReview_Type", "Gjennomgangstype");
    public string PeriodDisplay => $"{PeriodStart:yyyy-MM-dd} – {PeriodEnd:yyyy-MM-dd} (inneværende måned)";

    public bool HasReport { get; private set; }
    public string EmptyMessage { get; private set; } =
        Localized.Get("CaseReview_Empty", "Ikke nok data i valgt periode. Velg en periode med lagrede økter.");
    public string ReportTitle { get; private set; } = "";
    public IReadOnlyList<ClinicianRow> Overview { get; private set; } = Array.Empty<ClinicianRow>();
    public string OverviewHeading => Localized.Get("CaseReview_Overview", "Sammendrag for perioden");

    partial void OnSelectedReviewTypeChanged(string value) => Build();
    partial void OnPeriodStartChanged(DateTime value) => Build();
    partial void OnPeriodEndChanged(DateTime value) => Build();

    [RelayCommand]
    private void Build()
    {
        HasReport = false;
        Overview = Array.Empty<ClinicianRow>();
        if (_database is null) return;
        try
        {
            SessionAnalyticsStore analytics = _database is DatabaseService concrete
                ? new SessionAnalyticsStore(new SqliteSessionAnalyticsRepository(concrete.ConnectionString))
                : new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());

            var builder = new OutcomeProfileBuilder(
                new SmartCoachEngine(_database),
                new ExerciseEffectivenessEngine(analytics),
                new LongitudinalInsightEngine());

            DateTime now = DateTime.UtcNow;
            OutcomeProfile outcome = builder
                .AssembleFromStoreAsync(_database, null, new RecoveryIntelligenceService(), analytics, now, userId: 1)
                .GetAwaiter().GetResult();

            DateTime start = DateTime.SpecifyKind(PeriodStart, DateTimeKind.Utc);
            DateTime end = DateTime.SpecifyKind(PeriodEnd, DateTimeKind.Utc);
            OutcomeReport report = new ReportAssembler().BuildOutcomeReport(outcome, start, end, now);
            ReportTitle = $"{SelectedReviewType} · {PeriodStart:yyyy-MM-dd} – {PeriodEnd:yyyy-MM-dd}";

            // Count real sessions actually in the selected window (the review's evidence base).
            int sessionsInPeriod = _database.GetTrainingSessions(start.AddDays(-1), end.AddDays(1))
                .Count(s => s.StartTime >= start && s.StartTime <= end.AddDays(1));

            Overview = new List<ClinicianRow>
            {
                new(Localized.Get("CaseReview_Sessions", "Økter i perioden"), sessionsInPeriod.ToString()),
                new(Localized.Get("Clinician_Composite", "Samlet stemmescore"),
                    report.CompositeVoiceScore > 0 ? report.CompositeVoiceScore.ToString("F0", CultureInfo.InvariantCulture) + " / 100"
                                                   : Localized.Get("Clinician_Insufficient", "Ikke nok data ennå")),
                new(Localized.Get("Clinician_Recovery", "Restitusjon"),
                    (string.IsNullOrWhiteSpace(report.RecoveryStatus) ? "—" : report.RecoveryStatus) + $" ({report.RecoveryScore:F0}/100)"),
                new(Localized.Get("Clinician_DataSufficiency", "Datagrunnlag"),
                    report.HasEnoughData ? Localized.Get("Clinician_Enough", "Tilstrekkelig") : Localized.Get("Clinician_NotEnough", "Utilstrekkelig")),
            };
            HasReport = sessionsInPeriod > 0 || report.HasEnoughData;
        }
        catch { HasReport = false; }
    }
}
