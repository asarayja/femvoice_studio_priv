using System.Collections.Generic;
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
        _showMicCalibrationCommand = new RelayCommand(() => ShowDeferred("Mikrofonkalibrering"));
        BuildPages();
        BuildNav();
        _currentPage = dashboard;

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
        _settings = new SettingsViewModel();
        _analysis = new AnalysisViewModel(_database);   // engine-backed: real session trends when a DB is present
        _reports = new ReportsViewModel(_database);      // real progress-summary preview when a DB is present
        _diagnostics = new DiagnosticsViewModel(_database);   // real system status when a DB is present
        _progression = new ProgressionScaffoldViewModel();
        _smartCoach = new SmartCoachScaffoldViewModel();
        _firstTimeSetup = new FirstTimeSetupViewModel();   // real onboarding — persists language/theme + completed flag
    }

    // (Re)build the navigation rail. Labels resolve through the read-only localization adapter for the current
    // Avalonia culture; missing keys fall back to Norwegian.
    private void BuildNav()
    {
        NavItems = new List<ShellNavItem>
        {
            new(Localized.Get("Shell_Nav_Dashboard", "Dashbord"), true, ShowDashboardCommand),
            new(Localized.Get("Shell_Nav_Guide", "Øvelsesguide"), true, ShowGuideCommand),
            new(Localized.Get("Shell_Nav_Settings", "Innstillinger"), true, ShowSettingsCommand),
            new(Localized.Get("Shell_Nav_Analysis", "Analyse"), true, ShowAnalysisCommand),
            new(Localized.Get("Shell_Nav_Reports", "Rapporter"), true, ShowReportsCommand),
            new(Localized.Get("Shell_Nav_Diagnostics", "Diagnostikk"), true, ShowDiagnosticsCommand),
            new(Localized.Get("Shell_Nav_Statistics", "Statistikk"), true, ShowStatisticsCommand),   // real DB stats
            new(Localized.Get("Shell_Nav_Calendar", "Kalender"), true, ShowCalendarCommand),         // real DB history
            new(Localized.Get("Shell_Nav_Progresjon", "Progresjon"), true, ShowProgressionCommand),   // engine-backed
            new(Localized.Get("Shell_Nav_SmartCoach", "SmartCoach"), true, ShowSmartCoachCommand),   // engine-backed
            new(Localized.Get("Shell_Nav_FirstSetup", "Førstegangsoppsett"), true, ShowFirstTimeSetupCommand),   // real onboarding
            new(DeferredLabel("Mikrofonkalibrering"), false, _showMicCalibrationCommand),
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

    [ObservableProperty] private object _currentPage;

    // ── Display-only status strip (no real mic, no persistence, no clinical change) ──
    /// <summary>Display-only microphone/signal status: the Avalonia head uses synthetic audio only.</summary>
    public string MicStatusText => _audioReadiness.StatusText;   // truthful, abstraction-backed (Stage 3A)
    /// <summary>Display-only mode banner stating the safety posture of the Avalonia head.</summary>
    public string ModeText => Localized.Get("Shell_Mode", "Kun visning · ingen lagring · ingen klinisk endring");

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
            ProgressionViewModel => Localized.Get("Shell_Nav_Progresjon", "Progresjon"),
            ProgressionScaffoldViewModel => $"{Localized.Get("Shell_Nav_Progresjon", "Progresjon")} (utsatt)",
            SmartCoachViewModel => Localized.Get("SmartCoach_Scaffold_Title", "SmartCoach"),
            SmartCoachScaffoldViewModel => $"{Localized.Get("SmartCoach_Title", "SmartCoach")} (utsatt)",
            FirstTimeSetupViewModel => Localized.Get("Shell_Nav_FirstSetup", "Førstegangsoppsett"),
            DeferredSurfaceViewModel d => $"{d.SurfaceName} (utsatt)",
            _ => "—",
        };
    }

    [RelayCommand] private void ShowDashboard() => CurrentPage = _dashboard;
    [RelayCommand] private void ShowGuide() => CurrentPage = _guide;
    [RelayCommand] private void ShowSettings() => CurrentPage = _settings;          // inert display-only page
    // Engine-backed: real pitch/score trends from the saved sessions (fresh each open; null-safe → synthetic).
    [RelayCommand] private void ShowAnalysis() => CurrentPage = new AnalysisViewModel(_database);
    // Real progress-summary preview from saved sessions (fresh each open; null-safe). Full export deferred.
    [RelayCommand] private void ShowReports() => CurrentPage = new ReportsViewModel(_database);
    // Real system status (runtime + DB facts) fresh each open; export/support-package/backup stay deferred.
    [RelayCommand] private void ShowDiagnostics() => CurrentPage = new DiagnosticsViewModel(_database);
    // Real training statistics from the saved sessions (fresh each open; null-safe).
    [RelayCommand] private void ShowStatistics() => CurrentPage = new StatisticsViewModel(_database);
    // Real training history (last 90 days) from the saved sessions (fresh each open; null-safe).
    [RelayCommand] private void ShowCalendar() => CurrentPage = new CalendarViewModel(_database);
    // Engine-backed: real training level + FemVoice score + ProgressionService summary on the real DB (null-safe).
    [RelayCommand] private void ShowProgression() => CurrentPage = new ProgressionViewModel(_database);
    // Engine-backed: run the REAL SmartCoachEngine on the REAL database (falls back to a truthful "unavailable"
    // state when no DB is injected, e.g. headless/tests). Fresh each open so it reflects the latest saved sessions.
    [RelayCommand] private void ShowSmartCoach() => CurrentPage = new SmartCoachViewModel(_database);

    // Real onboarding: retained VM so its persisted state (completed flag) survives re-navigation within a session.
    [RelayCommand] private void ShowFirstTimeSetup() => CurrentPage = _firstTimeSetup;

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
