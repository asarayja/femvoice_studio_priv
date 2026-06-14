using System.Windows;
using System.Windows.Media;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Optional, NON-SCORED "Resonance Contrast" awareness demo (content only).
    ///
    /// This window contains no audio capture, no analysis, no scoring, and no progression
    /// impact — it simply presents a short, gentle, localized guide to feeling the contrast
    /// between larger/darker and smaller/brighter resonance. Start/Stop toggle a purely
    /// cosmetic "active" emphasis on the steps; they do not record or evaluate anything.
    /// </summary>
    public partial class ResonanceContrastDemoWindow : Window
    {
        public ResonanceContrastDemoWindow()
        {
            InitializeComponent();
        }

        private void OnStart(object sender, RoutedEventArgs e)
        {
            // Cosmetic only: emphasize the steps card while the user tries the demo.
            StepsCard.BorderBrush = TryFindBrush("AccentPrimaryBrush") ?? StepsCard.BorderBrush;
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
            StepsCard.BorderBrush = TryFindBrush("BorderPrimaryBrush") ?? StepsCard.BorderBrush;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private Brush? TryFindBrush(string key)
            => (TryFindResource(key) as Brush) ?? (Application.Current?.TryFindResource(key) as Brush);
    }
}
