using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly IUiDispatcher _ui;

    public ShellViewModel(MainDashboardViewModel dashboard, VoiceFeminizationExerciseService exercises, IUiDispatcher ui)
    {
        _dashboard = dashboard;
        _ui = ui;
        _guide = new ExerciseGuideViewModel(exercises, OpenExerciseDetail);
        _currentPage = dashboard;

        // Navigation surface: the two implemented top-level destinations, then deferred placeholders for
        // the missing WPF surfaces. Deferred items navigate ONLY to a static DeferredSurfaceViewModel.
        NavItems = new List<ShellNavItem>
        {
            new("Dashbord", true, ShowDashboardCommand),
            new("Øvelsesguide", true, ShowGuideCommand),
            new("Innstillinger — senere", false, new RelayCommand(() => ShowDeferred("Innstillinger"))),
            new("Analyse — senere", false, new RelayCommand(() => ShowDeferred("Analyse"))),
            new("Rapporter — senere", false, new RelayCommand(() => ShowDeferred("Rapporter"))),
            new("Diagnostikk — senere", false, new RelayCommand(() => ShowDeferred("Diagnostikk"))),
            new("Progresjon — senere", false, new RelayCommand(() => ShowDeferred("Progresjon"))),
            new("SmartCoach — senere", false, new RelayCommand(() => ShowDeferred("SmartCoach"))),
            new("Mikrofonkalibrering — senere", false, new RelayCommand(() => ShowDeferred("Mikrofonkalibrering"))),
        };
    }

    /// <summary>Navigation entries for the shell rail (implemented destinations + deferred placeholders).</summary>
    public IReadOnlyList<ShellNavItem> NavItems { get; }

    [ObservableProperty] private object _currentPage;

    // ── Display-only status strip (no real mic, no persistence, no clinical change) ──
    /// <summary>Display-only microphone/signal status: the Avalonia head uses synthetic audio only.</summary>
    public string MicStatusText => "Mikrofon: syntetisk (kun visning)";
    /// <summary>Display-only mode banner stating the safety posture of the Avalonia head.</summary>
    public string ModeText => "Kun visning · ingen lagring · ingen klinisk endring";
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
            && oldValue is System.IDisposable disposable)
            disposable.Dispose();
    }

    partial void OnCurrentPageChanged(object value)
    {
        CurrentDestinationLabel = value switch
        {
            MainDashboardViewModel => "Dashbord",
            ExerciseGuideViewModel => "Øvelsesguide",
            ExerciseDetailViewModel => "Øvelsesdetalj",
            ExerciseRuntimeViewModel => "Øvelse kjører",
            DeferredSurfaceViewModel d => $"{d.SurfaceName} (utsatt)",
            _ => "—",
        };
    }

    [RelayCommand] private void ShowDashboard() => CurrentPage = _dashboard;
    [RelayCommand] private void ShowGuide() => CurrentPage = _guide;

    // Deferred destinations open a purely static placeholder — no services, no side effects.
    private void ShowDeferred(string surface) => CurrentPage = new DeferredSurfaceViewModel(surface);

    private void OpenExerciseDetail(EnhancedExercise exercise)
        => CurrentPage = new ExerciseDetailViewModel(exercise, ShowGuide, () => ShowRuntime(exercise));

    private void ShowRuntime(EnhancedExercise exercise)
        => CurrentPage = new ExerciseRuntimeViewModel(exercise, _ui, () => OpenExerciseDetail(exercise));
}
