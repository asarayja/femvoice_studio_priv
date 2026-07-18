using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoice.Avalonia.Localization;   // Localized (safe read-only localization resolver)
using FemVoiceStudio.Core.Platform;   // IUiDispatcher
using FemVoiceStudio.Services;   // VoiceFeminizationExerciseService, EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// One entry in the shell's navigation list. Implemented entries switch to a real page; deferred entries
/// open a purely static <see cref="DeferredSurfaceViewModel"/> placeholder (no services, no side effects).
/// </summary>
public sealed class ShellNavItem
{
    public ShellNavItem(string label, bool isImplemented, IRelayCommand command)
    {
        Label = label;
        IsImplemented = isImplemented;
        Command = command;
    }

    public string Label { get; }
    /// <summary><c>true</c> = a real ported destination; <c>false</c> = a deferred static placeholder.</summary>
    public bool IsImplemented { get; }
    public IRelayCommand Command { get; }
}

/// <summary>
/// Lightweight navigation shell (no nav framework): holds the current page and switches between the
/// dashboard, the exercise guide / detail / runtime, and static "deferred" placeholders for WPF surfaces
/// not yet ported. View-models are read-only over shared services; NO clinical/domain behaviour here.
/// Display-only: the status strip and deferred placeholders carry no persistence, mic, SmartCoach,
/// progression, or safety-gate behaviour.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly MainDashboardViewModel _dashboard;   // hardcoded strings (not localized) — never rebuilt
    private readonly VoiceFeminizationExerciseService _exercises;
    private readonly IUiDispatcher _ui;
    // Localized page VMs — rebuilt on a language change so their text re-resolves in the new culture.
    private ExerciseGuideViewModel _guide = null!;
    private SettingsViewModel _settings = null!;
    private AnalysisViewModel _analysis = null!;
    private ReportsViewModel _reports = null!;
    private DiagnosticsViewModel _diagnostics = null!;
    private ProgressionScaffoldViewModel _progression = null!;
    private SmartCoachScaffoldViewModel _smartCoach = null!;
    private FirstTimeSetupViewModel _firstTimeSetup = null!;
    private readonly RelayCommand _showMicCalibrationCommand;
    private readonly FemVoice.Avalonia.Audio.AudioReadiness _audioReadiness;
    // Real Core database (injected by DI in production; null in headless/tests → engine-backed pages fall back to a
    // truthful "unavailable" state rather than opening a DB). Enables the real SmartCoach engine, etc.
    private readonly FemVoiceStudio.Data.IDatabaseService? _database;

    public ShellViewModel(MainDashboardViewModel dashboard, VoiceFeminizationExerciseService exercises, IUiDispatcher ui,
        FemVoiceStudio.Audio.Abstractions.IAudioCaptureService? capture = null,
        FemVoiceStudio.Data.IDatabaseService? database = null)
    {
        _dashboard = dashboard;
        _exercises = exercises;
        _ui = ui;
        _database = database;
        // Stage 3A: truthful audio status via the approved capture abstraction (read-only; never starts capture).
        // The Avalonia app's default backend is synthetic; fall back to it when no service is injected (headless).
        _audioReadiness = new FemVoice.Avalonia.Audio.AudioReadiness(
            capture ?? new FemVoiceStudio.Audio.Abstractions.SyntheticAudioCaptureService());
        // Real mic-check page: a fresh, disposable MicCalibrationViewModel per open (its own capture backend,
        // stopped on navigate-away by the transient-page dispose guard). Real backend in production, synthetic in
        // headless/tests. No clinical calibration profile is computed/saved (deferred).
        _showMicCalibrationCommand = new RelayCommand(() => CurrentPage = new MicCalibrationViewModel(null, _ui));
        BuildPages();
        BuildNav();
        _currentPage = dashboard;
        RefreshInfoStats();   // real sidebar quick-stats from saved sessions (null-safe)

        // Stage 2B: live language refresh. When the Avalonia-local culture changes (startup or Save), rebuild the
        // localized nav rail + the current localized page + chrome so the UI re-renders in the new language without
        // a restart. The dashboard uses hardcoded strings and is intentionally not rebuilt.
        Localized.LanguageChanged += OnLanguageChanged;
    }

    // (Re)create the localized page view-models. Display-only / inert; none hold services or are IDisposable
    // except the dashboard (which is not rebuilt here).
    private void BuildPages()
    {
        _guide = new ExerciseGuideViewModel(_exercises, OpenExercise);
        _settings = new SettingsViewModel(ForceShowOnboarding, () => _showMicCalibrationCommand.Execute(null));
        _analysis = new AnalysisViewModel(_database);   // engine-backed: real session trends when a DB is present
        _reports = new ReportsViewModel(_database, OpenCoachPanel, OpenClinicianPanel, OpenTimelinePanel, OpenCaseReviewPanel);   // real preview/export + coach/clinician/timeline
        _diagnostics = new DiagnosticsViewModel(_database);   // real system status when a DB is present
        _progression = new ProgressionScaffoldViewModel();
        _smartCoach = new SmartCoachScaffoldViewModel();
        // Real onboarding — shown ONLY on first run (never a nav item). Completing/skipping calls back to the shell,
        // which then moves to the dashboard. Persists language/theme/style/frequency + the completed flag.
        _firstTimeSetup = new FirstTimeSetupViewModel(null, () => ShowDashboard());
    }

    // (Re)build the navigation rail. Labels resolve through the read-only localization adapter for the current
    // Avalonia culture; missing keys fall back to Norwegian.
    private void BuildNav()
    {
        // Grouped for the user: the TRAINING/EXERCISE screens come FIRST (right after Dashbord) — Øvelsesguide,
        // Analysator, Resonans, SmartCoach — then progress/history (Progresjon, Statistikk, Kalender, Analyse), then
        // the report/professional tools (Rapporter holds Kliniker/Coach/Timeline/CaseReview; Manuell overstyring),
        // then system (Diagnostikk, Mikrofonkalibrering), with Innstillinger ALWAYS last (per user request).
        NavItems = new List<ShellNavItem>
        {
            new(Localized.Get("Shell_Nav_Dashboard", "Dashbord"), true, ShowDashboardCommand),
            // ── Øvelser / trening (first) ──
            new(Localized.Get("Shell_Nav_Guide", "Øvelsesguide"), true, ShowGuideCommand),
            new(Localized.Get("Shell_Nav_Analyzer", "Analysator"), true, ShowAnalyzerCommand),   // real-time pitch/resonance analyzer
            new(Localized.Get("Shell_Nav_Resonance", "Resonans"), true, ShowResonanceCommand),   // real-time resonance + contrast demo
            new(Localized.Get("Shell_Nav_SmartCoach", "SmartCoach"), true, ShowSmartCoachCommand),   // engine-backed coaching
            // ── Fremgang / historikk ──
            new(Localized.Get("Shell_Nav_Progresjon", "Progresjon"), true, ShowProgressionCommand),   // engine-backed
            new(Localized.Get("Shell_Nav_Statistics", "Statistikk"), true, ShowStatisticsCommand),   // real DB stats
            new(Localized.Get("Shell_Nav_Calendar", "Kalender"), true, ShowCalendarCommand),         // real DB history
            new(Localized.Get("Shell_Nav_Analysis", "Analyse"), true, ShowAnalysisCommand),
            // ── Rapporter / profesjonelt ──
            new(Localized.Get("Shell_Nav_Reports", "Rapporter"), true, ShowReportsCommand),      // holds Kliniker/Coach/Timeline/CaseReview
            new(Localized.Get("Shell_Nav_ManualOverride", "Manuell overstyring"), true, ShowManualOverrideCommand),   // safety-clamp preview
            // ── System ──
            new(Localized.Get("Shell_Nav_Diagnostics", "Diagnostikk"), true, ShowDiagnosticsCommand),
            new(Localized.Get("Shell_Nav_MicCalibration", "Mikrofonkalibrering"), true, _showMicCalibrationCommand),
            new(Localized.Get("Shell_Nav_Settings", "Innstillinger"), true, ShowSettingsCommand),   // ALWAYS last
            // NOTE: Førstegangsoppsett is intentionally NOT a nav item — it is onboarding, shown once on first run only.
        };
    }

    // Live language refresh: rebuild pages + nav, re-point the current page to the freshly-built same-type VM
    // (so its localized text re-resolves), and re-raise chrome strings. The dashboard and a running exercise are
    // left in place (dashboard isn't localized; an in-progress exercise must not be torn down by a language change).
    private void OnLanguageChanged()
    {
        void Refresh()
        {
            var current = CurrentPage;
            BuildPages();
            BuildNav();
            OnPropertyChanged(nameof(MicStatusText));
            OnPropertyChanged(nameof(ModeText));
            CurrentPage = current switch
            {
                ExerciseGuideViewModel => _guide,
                SettingsViewModel => _settings,
                AnalysisViewModel => _analysis,
                ReportsViewModel => _reports,
                DiagnosticsViewModel => _diagnostics,
                ProgressionScaffoldViewModel => _progression,
                SmartCoachScaffoldViewModel => _smartCoach,
                FirstTimeSetupViewModel => _firstTimeSetup,
                DeferredSurfaceViewModel d => new DeferredSurfaceViewModel(d.SurfaceName),
                _ => current,   // dashboard / running exercise: leave in place
            };
        }
        if (_ui is not null) _ui.Post(Refresh); else Refresh();
    }

    /// <summary>Navigation entries for the shell rail (implemented destinations + deferred placeholders).</summary>
    [ObservableProperty] private IReadOnlyList<ShellNavItem> _navItems = new List<ShellNavItem>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnboarding))]
    [NotifyPropertyChangedFor(nameof(IsChromeVisible))]
    private object _currentPage;

    /// <summary>True while the first-run onboarding is the active page. Drives hiding the nav rail / info sidebar /
    /// status strip so nothing else in the app is reachable until setup has been chosen and saved (or skipped).</summary>
    public bool IsOnboarding => CurrentPage is FirstTimeSetupViewModel;
    /// <summary>Inverse of <see cref="IsOnboarding"/> — the normal app chrome (nav + sidebar + status) is shown.</summary>
    public bool IsChromeVisible => !IsOnboarding;

    // ── Display-only status strip (no real mic, no persistence, no clinical change) ──
    /// <summary>Display-only microphone/signal status: the Avalonia head uses synthetic audio only.</summary>
    public string MicStatusText => _audioReadiness.StatusText;   // truthful, abstraction-backed (Stage 3A)
    /// <summary>Mode banner stating the safety posture of the Avalonia head. The app now stores real sessions and
    /// reads real data, but changes NO clinical logic — the banner says exactly that.</summary>
    public string ModeText => Localized.Get("Shell_Mode", "Ekte data lokalt · ingen klinisk endring");

    // ── Right info sidebar: real quick-stats from the saved sessions (null-safe; empty with no DB) ──────────────
    /// <summary>True when real quick-stats were read from the DB (drives the sidebar's stats block vs the hint).</summary>
    [ObservableProperty] private bool _hasInfoStats;
    [ObservableProperty] private string _infoSessionsLine = string.Empty;
    [ObservableProperty] private string _infoStreakLine = string.Empty;
    [ObservableProperty] private string _infoLastScoreLine = string.Empty;

    /// <summary>Heading for the sidebar's real quick-stats block.</summary>
    public string InfoStatsHeading => Localized.Get("Shell_Info_YourProgress", "Din fremgang");
    /// <summary>Sidebar hint shown when there is no data yet (no DB / no sessions).</summary>
    public string InfoNoStatsHint => Localized.Get("Shell_Info_NoStats",
        "Fullfør en økt på dashbordet for å se fremgangen din her.");

    // Recompute the sidebar quick-stats from the real DB (total sessions, current streak, last score). Read-only,
    // null-safe, never throws. Called on construction and whenever the current page changes (so it reflects a
    // session just saved on the dashboard). No clinical calculation — plain aggregates over saved sessions.
    private void RefreshInfoStats()
    {
        if (_database is null) { HasInfoStats = false; return; }
        try
        {
            var sessions = _database.GetRecentSessions(1000);
            if (sessions.Count == 0) { HasInfoStats = false; return; }
            var (_, _, streak) = _database.GetProgressionStats();
            double lastScore = sessions.OrderByDescending(s => s.StartTime).First().OverallScore;
            InfoSessionsLine = Localized.Get("Shell_Info_Sessions", "Økter") + $": {sessions.Count}";
            InfoStreakLine = Localized.Get("Shell_Info_Streak", "Streak") + $": {streak} " + Localized.Get("Shell_Info_Days", "dager");
            InfoLastScoreLine = Localized.Get("Shell_Info_LastScore", "Siste score") + $": {lastScore:F0} / 100";
            HasInfoStats = true;
        }
        catch { HasInfoStats = false; }
    }

    // Deferred nav label: "<Surface> — senere", localization-ready with the current text as fallback.
    private static string DeferredLabel(string surface)
        => Localized.Get($"Shell_Nav_{surface}_Deferred", $"{surface} — senere");
    /// <summary>Label of the current destination, for the status strip.</summary>
    [ObservableProperty] private string _currentDestinationLabel = "Dashbord";

    public bool IsDashboard => CurrentPage == _dashboard;

    partial void OnCurrentPageChanging(object? oldValue, object newValue)
    {
        // Dispose a TRANSIENT, disposable outgoing page (e.g. the runtime VM) so a running exercise's
        // synthetic capture loop + VM-local coordinator stop when navigating away — including via the
        // shell nav rail. Retained singletons (_dashboard, _guide) and the static deferred placeholders
        // are never disposed. (Preserves the PR #7/#8 lifecycle fix.)
        if (!ReferenceEquals(oldValue, _dashboard) && !ReferenceEquals(oldValue, _guide)
            && !ReferenceEquals(oldValue, _settings) && !ReferenceEquals(oldValue, _analysis)
            && !ReferenceEquals(oldValue, _reports) && !ReferenceEquals(oldValue, _diagnostics)
            && !ReferenceEquals(oldValue, _progression) && !ReferenceEquals(oldValue, _smartCoach)
            && !ReferenceEquals(oldValue, _firstTimeSetup)
            && oldValue is System.IDisposable disposable)
            disposable.Dispose();
    }

    partial void OnCurrentPageChanged(object value)
    {
        RefreshInfoStats();   // keep the sidebar quick-stats fresh (e.g. after a session saved on the dashboard)
        CurrentDestinationLabel = value switch
        {
            MainDashboardViewModel => "Dashbord",
            ExerciseGuideViewModel => "Øvelsesguide",
            ExerciseRuntimeViewModel => "Øvelse",
            SettingsViewModel => Localized.Get("Settings_Title", "Innstillinger"),
            AnalysisViewModel => Localized.Get("Shell_Nav_Analysis", "Analyse"),
            ReportsViewModel => Localized.Get("Shell_Nav_Reports", "Rapporter"),
            DiagnosticsViewModel => Localized.Get("Shell_Nav_Diagnostics", "Diagnostikk"),
            StatisticsViewModel => Localized.Get("Statistics_Title", "Statistikk"),
            CalendarViewModel => Localized.Get("Calendar_Title", "Kalender / historikk"),
            DayDetailsViewModel => Localized.Get("DayDetails_Title", "Øktdetaljer"),
            ProgressionViewModel => Localized.Get("Shell_Nav_Progresjon", "Progresjon"),
            ProgressionScaffoldViewModel => $"{Localized.Get("Shell_Nav_Progresjon", "Progresjon")} (utsatt)",
            SmartCoachViewModel => Localized.Get("SmartCoach_Scaffold_Title", "SmartCoach"),
            SmartCoachScaffoldViewModel => $"{Localized.Get("SmartCoach_Title", "SmartCoach")} (utsatt)",
            FirstTimeSetupViewModel => Localized.Get("Shell_Nav_FirstSetup", "Førstegangsoppsett"),
            MicCalibrationViewModel => Localized.Get("Shell_Nav_MicCalibration", "Mikrofonkalibrering"),
            CoachPanelViewModel => Localized.Get("Coach_Title", "Coach-oversikt"),
            ClinicianPanelViewModel => Localized.Get("Clinician_Title", "Klinisk oversikt"),
            TimelinePanelViewModel => Localized.Get("Report_Timeline", "Tidslinje"),
            CaseReviewPanelViewModel => Localized.Get("CaseReview_Title", "Case-gjennomgang"),
            ManualOverridePanelViewModel => Localized.Get("Shell_Nav_ManualOverride", "Manuell overstyring"),
            ResonanceViewModel => Localized.Get("Shell_Nav_Resonance", "Resonans"),
            AnalyzerViewModel => Localized.Get("Shell_Nav_Analyzer", "Analysator"),
            DeferredSurfaceViewModel d => $"{d.SurfaceName} (utsatt)",
            _ => "—",
        };
    }

    /// <summary>Show the first-time onboarding as the current page IFF setup has not been completed. Called ONCE by
    /// the real app at startup (both heads) — NOT by headless smokes/snapshots, so those stay on the dashboard.
    /// Onboarding is never a nav item; after Complete/Skip it calls back to move to the dashboard and never re-shows.</summary>
    public void ShowOnboardingIfFirstRun()
    {
        try
        {
            if (!new FemVoice.Avalonia.Preferences.UiPreferencesStore().Load().FirstTimeSetupCompleted)
                CurrentPage = _firstTimeSetup;
        }
        catch { /* prefs unreadable → skip onboarding, land on dashboard */ }
    }

    /// <summary>Force the onboarding page to be the current page regardless of the saved completed flag. Used by the
    /// offscreen snapshot / smokes to exercise the first-run chrome-gating deterministically.</summary>
    public void ForceShowOnboarding() => CurrentPage = _firstTimeSetup;

    [RelayCommand] private void ShowDashboard() => CurrentPage = _dashboard;
    [RelayCommand] private void ShowGuide() => CurrentPage = _guide;
    [RelayCommand] private void ShowSettings() => CurrentPage = _settings;          // inert display-only page
    // Engine-backed: real pitch/score trends from the saved sessions (fresh each open; null-safe → synthetic).
    [RelayCommand] private void ShowAnalysis() => CurrentPage = new AnalysisViewModel(_database);
    // Real progress-summary preview + CSV/text export from saved sessions (fresh each open; null-safe).
    [RelayCommand] private void ShowReports() => CurrentPage = new ReportsViewModel(_database, OpenCoachPanel, OpenClinicianPanel, OpenTimelinePanel, OpenCaseReviewPanel);

    // Real read-only coach/clinician/timeline panels assembled from saved sessions; Back returns to the Reports page.
    private void OpenCoachPanel() => CurrentPage = new CoachPanelViewModel(_database, ShowReports, OpenCoachPanel);
    private void OpenClinicianPanel() => CurrentPage = new ClinicianPanelViewModel(_database, ShowReports);
    private void OpenTimelinePanel() => CurrentPage = new TimelinePanelViewModel(_database, ShowReports);
    private void OpenCaseReviewPanel() => CurrentPage = new CaseReviewPanelViewModel(_database, ShowReports);

    // SAFETY-CRITICAL: manual-override clamp PREVIEW. Runs the frozen two-stage clamp read-only and shows only the
    // clamped outcome; no persistence, no application. Fresh each open; Back returns to the dashboard.
    [RelayCommand] private void ShowManualOverride() => CurrentPage = new ManualOverridePanelViewModel(_database, ShowDashboard);

    // Real-time resonance screen (Core ResonanceProxyEngine) + contrast demo; fresh disposable page (own capture
    // backend, stopped on navigate-away). null → creates its own real-when-available backend (synthetic in tests).
    [RelayCommand] private void ShowResonance() => CurrentPage = new ResonanceViewModel(null, _ui);

    // Real-time analyzer (live pitch + resonance + running stats); fresh disposable page (own capture backend).
    [RelayCommand] private void ShowAnalyzer() => CurrentPage = new AnalyzerViewModel(null, _ui);
    // Real system status (runtime + DB facts) fresh each open; export/support-package/backup stay deferred.
    [RelayCommand] private void ShowDiagnostics() => CurrentPage = new DiagnosticsViewModel(_database);
    // Real training statistics from the saved sessions (fresh each open; null-safe).
    [RelayCommand] private void ShowStatistics() => CurrentPage = new StatisticsViewModel(_database);
    // Real training history (last 90 days) from the saved sessions (fresh each open; null-safe).
    [RelayCommand] private void ShowCalendar() => CurrentPage = new CalendarViewModel(_database, OpenDayDetails);

    // Real day-details from the Calendar: shows every session recorded on the clicked day; Back returns to Calendar.
    private void OpenDayDetails(System.DateTime date) => CurrentPage = new DayDetailsViewModel(_database, date, ShowCalendar);
    // Engine-backed: real training level + FemVoice score + ProgressionService summary on the real DB (null-safe).
    [RelayCommand] private void ShowProgression() => CurrentPage = new ProgressionViewModel(_database, null, ShowGuide);
    // Engine-backed: run the REAL SmartCoachEngine on the REAL database (falls back to a truthful "unavailable"
    // state when no DB is injected, e.g. headless/tests). Fresh each open so it reflects the latest saved sessions.
    [RelayCommand] private void ShowSmartCoach() => CurrentPage = new SmartCoachViewModel(_database);

    // (Førstegangsoppsett has no Show* command / nav item — it is shown once on first run via ShowOnboardingIfFirstRun.)

    // Deferred destinations open a purely static placeholder — no services, no side effects.
    private void ShowDeferred(string surface) => CurrentPage = new DeferredSurfaceViewModel(surface);

    // WPF parity: the exercise guide opens the exercise page DIRECTLY (one page, one Start) — there is no
    // separate detail page and no second Start. Back returns to the guide.
    private void OpenExercise(EnhancedExercise exercise)
        // useRealMic only when a real microphone is actually available (true in production via the DI-injected
        // backend; false in headless/tests → the exercise keeps its target-tuned synthetic source, so the exercise
        // smokes stay deterministic). Only the frame SOURCE differs — no clinical change.
        => CurrentPage = new ExerciseRuntimeViewModel(exercise, _ui, ShowGuide, new History.SessionHistoryStore(),
            useRealMic: _audioReadiness.IsRealCaptureAvailable);
}
