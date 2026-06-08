using System.Windows.Controls;
using FemVoiceStudio.ViewModels;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Code-behind for <see cref="CoachDashboard"/> UserControl.
    /// Sets DataContext to a <see cref="CoachDashboardViewModel"/> resolved from
    /// App.Services and triggers an initial Refresh on load.
    /// </summary>
    public partial class CoachDashboard : UserControl
    {
        private readonly CoachDashboardViewModel _viewModel;

        public CoachDashboard()
        {
            InitializeComponent();
            _viewModel = new CoachDashboardViewModel();
            DataContext = _viewModel;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.Refresh();
        }
    }
}
