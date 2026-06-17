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
    private readonly MainDashboardViewModel _dashboard;
    private readonly ExerciseGuideViewModel _guide;
    private readonly SettingsViewModel _settings;
    private readonly AnalysisViewModel _analysis;
    private readonly ReportsViewModel _reports;
    private readonly DiagnosticsViewModel _diagnostics;
    private readonly IUiDispatcher _ui;

    public ShellViewModel(MainDashboardViewModel dashboard, VoiceFeminizationExerciseService exercises, IUiDispatcher ui)
    {
        _dashboard = dashboard;
        _ui = ui;
        _guide = new ExerciseGuideViewModel(exercises, OpenExercise);
        _settings = new SettingsViewModel();        // inert, display-only; retained singleton, not IDisposable
        _analysis = new AnalysisViewModel();        // inert, display-only; retained singleton, not IDisposable
        _reports = new ReportsViewModel();          // inert, display-only; retained singleton, not IDisposable
        _diagnostics = new DiagnosticsViewModel();  // inert, display-only; retained singleton, not IDisposable
        _currentPage = dashboard;

        // Navigation surface: the two implemented top-level destinations, then deferred placeholders for
        // the missing WPF surfaces. Deferred items navigate ONLY to a static DeferredSurfaceViewModel.
        // Labels resolve through the safe read-only localization adapter; missing keys fall back to the
        // current Norwegian text (so behaviour is identical today, but the path is localization-ready).
        NavItems = new List<ShellNavItem>
        {
            new(Localized.Get("Shell_Nav_Dashboard", "Dashbord"), true, ShowDashboardCommand),
            new(Localized.Get("Shell_Nav_Guide", "Øvelsesguide"), true, ShowGuideCommand),
            new(Localized.Get("Shell_Nav_Settings", "Innstillinger"), true, ShowSettingsCommand),
            new(Localized.Get("Shell_Nav_Analysis", "Analyse"), true, ShowAnalysisCommand),
            new(Localized.Get("Shell_Nav_Reports", "Rapporter"), true, ShowReportsCommand),
            new(Localized.Get("Shell_Nav_Diagnostics", "Diagnostikk"), true, ShowDiagnosticsCommand),
            new(DeferredLabel("Progresjon"), false, new RelayCommand(() => ShowDeferred("Progresjon"))),
            new(DeferredLabel("SmartCoach"), false, new RelayCommand(() => ShowDeferred("SmartCoach"))),
            new(DeferredLabel("Mikrofonkalibrering"), false, new RelayCommand(() => ShowDeferred("Mikrofonkalibrering"))),
        };
    }

    /// <summary>Navigation entries for the shell rail (implemented destinations + deferred placeholders).</summary>
    public IReadOnlyList<ShellNavItem> NavItems { get; }

    [ObservableProperty] private object _currentPage;

    // ── Display-only status strip (no real mic, no persistence, no clinical change) ──
    /// <summary>Display-only microphone/signal status: the Avalonia head uses synthetic audio only.</summary>
    public string MicStatusText => Localized.Get("Shell_MicStatus", "Mikrofon: syntetisk (kun visning)");
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
            DeferredSurfaceViewModel d => $"{d.SurfaceName} (utsatt)",
            _ => "—",
        };
    }

    [RelayCommand] private void ShowDashboard() => CurrentPage = _dashboard;
    [RelayCommand] private void ShowGuide() => CurrentPage = _guide;
    [RelayCommand] private void ShowSettings() => CurrentPage = _settings;          // inert display-only page
    [RelayCommand] private void ShowAnalysis() => CurrentPage = _analysis;          // inert display-only page
    [RelayCommand] private void ShowReports() => CurrentPage = _reports;            // inert display-only page
    [RelayCommand] private void ShowDiagnostics() => CurrentPage = _diagnostics;    // inert display-only page

    // Deferred destinations open a purely static placeholder — no services, no side effects.
    private void ShowDeferred(string surface) => CurrentPage = new DeferredSurfaceViewModel(surface);

    // WPF parity: the exercise guide opens the exercise page DIRECTLY (one page, one Start) — there is no
    // separate detail page and no second Start. Back returns to the guide.
    private void OpenExercise(EnhancedExercise exercise)
        => CurrentPage = new ExerciseRuntimeViewModel(exercise, _ui, ShowGuide);
}
