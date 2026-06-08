using System.Windows;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Standalone window shell for the Coach Dashboard.
    /// Can be opened with <c>new CoachDashboardWindow().Show()</c> per the Wave 1
    /// frozen contract.  The inner <see cref="CoachDashboard"/> UserControl owns the
    /// ViewModel and DataContext setup.
    /// </summary>
    public partial class CoachDashboardWindow : Window
    {
        public CoachDashboardWindow()
        {
            InitializeComponent();
        }
    }
}
