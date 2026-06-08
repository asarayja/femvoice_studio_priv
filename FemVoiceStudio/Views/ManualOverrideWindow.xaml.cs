using System.Windows;
using FemVoiceStudio.ViewModels;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// SAFETY-CRITICAL Manual Override window (Sprint E, Agent 7). Lets a clinician/coach
    /// request an override; the bound <see cref="ManualOverrideViewModel"/> runs the
    /// two-stage safety/recovery clamp and presents ONLY the clamped outcome — the raw
    /// professional intent is never shown as the result.
    ///
    /// Follows the frozen Wave-1 contract: parameterless ctor that sets up its own VM
    /// (which self-resolves its services via <see cref="App.Services"/>), so the window can
    /// be opened with <c>new ManualOverrideWindow().Show()</c>.
    /// </summary>
    public partial class ManualOverrideWindow : Window
    {
        private readonly ManualOverrideViewModel _viewModel;

        public ManualOverrideWindow()
        {
            InitializeComponent();

            // The VM self-resolves ManualOverrideEngine / ManualOverridesStore /
            // AuditTrailStore / SessionAnalyticsStore (+ gate sources) null-safe from
            // App.Services in its parameterless ctor.
            _viewModel = new ManualOverrideViewModel();
            DataContext = _viewModel;
        }
    }
}
