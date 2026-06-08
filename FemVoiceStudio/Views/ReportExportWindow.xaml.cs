using System.Windows;
using FemVoiceStudio.ViewModels;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Code-behind for the Report Export window (Sprint E, Agent 3+10).
    /// All logic lives in <see cref="ReportExportViewModel"/>; this file only wires the
    /// DataContext and delegates Window events.
    /// </summary>
    public partial class ReportExportWindow : Window
    {
        private readonly ReportExportViewModel _viewModel;

        /// <summary>
        /// Parameterless constructor — self-wires its own VM via App.Services.
        /// Satisfies the frozen contract: <c>new ReportExportWindow().Show()</c>.
        /// </summary>
        public ReportExportWindow()
        {
            InitializeComponent();
            _viewModel = new ReportExportViewModel();
            DataContext = _viewModel;
        }
    }
}
