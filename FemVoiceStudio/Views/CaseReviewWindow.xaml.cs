using System.Windows;
using FemVoiceStudio.ViewModels;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Code-behind for the Case Review window (Sprint E, Agent 8).
    /// All logic lives in <see cref="CaseReviewViewModel"/>; this file only wires the
    /// DataContext.
    /// </summary>
    public partial class CaseReviewWindow : Window
    {
        private readonly CaseReviewViewModel _viewModel;

        /// <summary>
        /// Parameterless constructor — self-wires its own VM via App.Services.
        /// Satisfies the frozen contract: <c>new CaseReviewWindow().Show()</c>.
        /// </summary>
        public CaseReviewWindow()
        {
            InitializeComponent();
            _viewModel = new CaseReviewViewModel();
            DataContext = _viewModel;
        }
    }
}
