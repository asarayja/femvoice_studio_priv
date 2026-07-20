using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FemVoice.Avalonia.Views;

/// <summary>
/// Main dashboard. Single-column, mobile-first: session controls (which collapse to just Stop while recording),
/// then the exercise text and the live pitch graph — the priority during a session — then the live metrics and
/// feedback. On phone widths a full-width Stop bar is shown while recording so Stop is always reachable.
/// </summary>
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += (_, e) => ApplyResponsive(e.NewSize.Width);
    }

    private void ApplyResponsive(double width)
    {
        // Phone widths get an extra full-width Stop bar after the graph (its inner button also gates on IsRecording,
        // so it only shows while a session runs). Desktop keeps the Start/Stop in the session card only.
        if (this.FindControl<Border>("MobileStopBar") is { } mobileStop)
            mobileStop.IsVisible = width < 560;
    }
}
