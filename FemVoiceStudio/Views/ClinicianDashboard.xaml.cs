using System.Windows.Controls;
using FemVoiceStudio.ViewModels;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Code-behind for the ClinicianDashboard UserControl.
    ///
    /// The DataContext is a <see cref="ClinicianDashboardViewModel"/> set by the host
    /// window (<see cref="ClinicianDashboardWindow"/>). This file intentionally contains
    /// no logic — all behaviour lives in the ViewModel.
    /// </summary>
    public partial class ClinicianDashboard : UserControl
    {
        public ClinicianDashboard()
        {
            InitializeComponent();
        }
    }
}
