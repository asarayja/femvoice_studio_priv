using System.Windows.Controls;
using FemVoiceStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Code-behind for SmartCoachDashboardView
    /// </summary>
    public partial class SmartCoachDashboardView : UserControl
    {
        private readonly SmartCoachViewModel? _viewModel;

        public SmartCoachDashboardView()
        {
            InitializeComponent();
            _viewModel = App.Services.GetService<SmartCoachViewModel>();
            DataContext = _viewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_viewModel != null)
                await _viewModel.InitializeAsync();
        }
    }
}
