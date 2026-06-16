using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Core.Platform;   // IUiDispatcher
using FemVoiceStudio.Services;   // VoiceFeminizationExerciseService, EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Lightweight navigation shell (no nav framework): holds the current page and switches between the
/// dashboard, the exercise guide, and an exercise detail via a ContentControl + DataTemplates in
/// MainWindow. View-models are read-only over shared services; no clinical/domain behaviour here.
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
    }

    [ObservableProperty] private object _currentPage;

    public bool IsDashboard => CurrentPage == _dashboard;

    [RelayCommand] private void ShowDashboard() => CurrentPage = _dashboard;
    [RelayCommand] private void ShowGuide() => CurrentPage = _guide;

    private void OpenExerciseDetail(EnhancedExercise exercise)
        => CurrentPage = new ExerciseDetailViewModel(exercise, ShowGuide, () => ShowRuntime(exercise));

    private void ShowRuntime(EnhancedExercise exercise)
        => CurrentPage = new ExerciseRuntimeViewModel(exercise, _ui, () => OpenExerciseDetail(exercise));
}
