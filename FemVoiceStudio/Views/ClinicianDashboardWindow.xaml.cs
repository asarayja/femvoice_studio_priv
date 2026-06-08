using System.Windows;
using FemVoiceStudio.ViewModels;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Wrapper window for the ClinicianDashboard UserControl (Sprint E Wave 1).
    ///
    /// Contract (frozen): parameterless ctor, openable with new ClinicianDashboardWindow().Show().
    /// The VM is resolved from App.Services (null-safe: App.Services may be null in
    /// design-time / test contexts — InitializeComponent still succeeds with no VM).
    /// </summary>
    public partial class ClinicianDashboardWindow : Window
    {
        private readonly ClinicianDashboardViewModel _viewModel;

        public ClinicianDashboardWindow()
        {
            InitializeComponent();

            _viewModel = new ClinicianDashboardViewModel();
            DataContext = _viewModel;
        }
    }
}
