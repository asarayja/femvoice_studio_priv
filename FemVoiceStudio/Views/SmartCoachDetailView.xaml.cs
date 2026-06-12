using System.Windows;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Code-behind for SmartCoachDetailView
    /// </summary>
    public partial class SmartCoachDetailWindow : Window
    {
        private SmartCoachViewModel? _viewModel;

        public SmartCoachDetailWindow()
        {
            try
            {
                InitializeComponent();
                _viewModel = App.Services.GetRequiredService<SmartCoachViewModel>();
                DataContext = _viewModel;
                Loaded += OnLoaded;
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("SmartCoach", $"DetailWindowLoad FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General), 
                    LocalizationService.Instance.GetString("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
                await _viewModel.InitializeAsync();
        }
    }
}
