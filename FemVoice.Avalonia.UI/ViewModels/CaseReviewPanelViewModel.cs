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
        LoadSaved();   // populate the saved-reviews list (best-effort)
    }

    public IRelayCommand BackCommand { get; }
    public string Title => Localized.Get("CaseReview_Title", "Case-gjennomgang");
    public string BackLabel => Localized.Get("Common_Back", "Tilbake");
    public string EvaluateLabel => Localized.Get("CaseReview_Evaluate", "Bygg gjennomgang");
    public string Disclaimer => Localized.Get("CaseReview_Panel_Disclaimer2",
        "Saksgjennomgang sammenstilt fra dine lagrede økter for valgt periode. Beskrivende — aldri en sikkerhets- " +
        "eller treningsport. «Lagre gjennomgang» skriver en gjennomgangsoppføring (utfallssnapshot) til " +
        "gjennomgangsloggen. Ingen klinisk/sikkerhetslogikk er endret.");

    public IReadOnlyList<string> ReviewTypes { get; } = new[]
    {
        Localized.Get("CaseReview_Monthly", "Månedlig"),
        Localized.Get("CaseReview_Goal", "Mål"),
        Localized.Get("CaseReview_Progress", "Progresjon"),
        Localized.Get("CaseReview_Recovery", "Restitusjon"),
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

    // ── Persist the review (saved case-reviews store) ────────────────────────────────────────────────────────
    private OutcomeProfile? _lastOutcome;
    public string SaveLabel => Localized.Get("CaseReview_Create", "Opprett gjennomgang");
    [ObservableProperty] private string _saveStatus = "";
    public bool CanSave => HasReport && _lastOutcome is not null && _database is DatabaseService;
    [ObservableProperty] private IReadOnlyList<string> _savedReviews = Array.Empty<string>();
    public string SavedHeading => Localized.Get("CaseReview_SavedReviews", "Lagrede gjennomganger");
    public bool HasSaved => SavedReviews.Count > 0;

    private CaseReviewsStore? BuildStore()
        => _database is DatabaseService concrete
            ? new CaseReviewsStore(new SqliteCaseReviewsRepository(concrete.ConnectionString))
            : null;

    private ReviewType SelectedType() => Array.IndexOf(ReviewTypes.ToArray(), SelectedReviewType) switch
    {
        1 => ReviewType.Goal, 2 => ReviewType.Progress, 3 => ReviewType.Recovery, _ => ReviewType.Monthly
    };

    [RelayCommand]
    private void Save()
    {
        if (_lastOutcome is null) { SaveStatus = Localized.Get("CaseReview_NoData", "Ingen gjennomgang å lagre."); return; }
        var store = BuildStore();
        if (store is null) { SaveStatus = Localized.Get("CaseReview_NoDb2", "Databasen er ikke tilgjengelig i denne visningen."); return; }
        try
        {
            DateTime now = DateTime.UtcNow;
            DateTime start = DateTime.SpecifyKind(PeriodStart, DateTimeKind.Utc);
            DateTime end = DateTime.SpecifyKind(PeriodEnd, DateTimeKind.Utc);
            CaseReview review = new CaseReviewAssembler().Build(_lastOutcome, start, end, SelectedType(), now);
            store.SaveAsync(review).GetAwaiter().GetResult();
            SaveStatus = Localized.Get("CaseReview_Saved2", "Gjennomgang lagret.");
            LoadSaved(store);
            OnPropertyChanged(nameof(HasSaved));
        }
        catch (Exception ex) { SaveStatus = Localized.Get("CaseReview_SaveFailed", "Kunne ikke lagre: ") + ex.Message; }
    }

    private void LoadSaved(CaseReviewsStore? store = null)
    {
        store ??= BuildStore();
        if (store is null) return;
        try
        {
            SavedReviews = store.GetByUserAsync(1).GetAwaiter().GetResult()
                .OrderByDescending(r => r.CreatedAt).Take(10)
                .Select(r => $"{r.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm} · {r.ReviewType} · {r.PeriodStart:yyyy-MM-dd}–{r.PeriodEnd:yyyy-MM-dd} · {r.Status}")
                .ToList();
        }
        catch { /* best-effort */ }
    }

    partial void OnSelectedReviewTypeChanged(string value) => Build();
    partial void OnPeriodStartChanged(DateTime value) => Build();
    partial void OnPeriodEndChanged(DateTime value) => Build();

    [RelayCommand]
    private void Build()
    {
        HasReport = false;
        Overview = Array.Empty<ClinicianRow>();
        _lastOutcome = null;
        OnPropertyChanged(nameof(CanSave));
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
            _lastOutcome = outcome;

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
            OnPropertyChanged(nameof(CanSave));
        }
        catch { HasReport = false; }
    }
}
