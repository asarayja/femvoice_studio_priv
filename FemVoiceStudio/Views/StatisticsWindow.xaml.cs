using System.Windows;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Statistics window for viewing user progress.
    /// </summary>
    public partial class StatisticsWindow : Window
    {
        private readonly StatisticsViewModel _viewModel;
        
        public StatisticsWindow()
        {
            InitializeComponent();
            
            _viewModel = new StatisticsViewModel();
            
            // Populate UI
            TotalSessionsText.Text = _viewModel.TotalSessions.ToString();
            StreakText.Text = _viewModel.CurrentStreak.ToString();
            AvgScoreText.Text = $"{_viewModel.AverageScore:F0}%";
            DetailedScoreText.Text = $"{_viewModel.AverageScore:F0}%";
            AvgPitchText.Text = $"{_viewModel.AveragePitch:F0}";
            LevelText.Text = _viewModel.CurrentLevel;
            LevelProgress.Value = _viewModel.ProgressToNextLevel;
            LevelProgressText.Text = LocalizationService.Instance.GetFormattedString("Statistics_LevelProgress", _viewModel.TotalSessions);
            
            RecentSessionsList.ItemsSource = _viewModel.RecentSessions;
        }
    }
}
