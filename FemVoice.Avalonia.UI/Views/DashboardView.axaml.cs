using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FemVoice.Avalonia.Views;

/// <summary>
/// Main dashboard. RESPONSIVE: the two top cards (session controls + live metrics) sit side-by-side when the content
/// area is wide, and stack vertically on narrow (phone) widths so nothing clips off-screen. Display-only layout.
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
        var session = this.FindControl<Border>("SessionCard");
        var live = this.FindControl<Border>("LiveCard");
        if (session is null || live is null) return;

        bool compact = width < 560;
        // Mobile-only Stop bar (after the graph). The inner button additionally gates on IsRecording, so it only
        // appears on phone widths WHILE a session is running.
        var mobileStop = this.FindControl<Border>("MobileStopBar");
        if (mobileStop is not null) mobileStop.IsVisible = compact;

        if (compact)   // stacked (phone): each card full-width, one above the other
        {
            Grid.SetRow(session, 0); Grid.SetColumn(session, 0); Grid.SetColumnSpan(session, 2);
            Grid.SetRow(live, 1); Grid.SetColumn(live, 0); Grid.SetColumnSpan(live, 2);
            session.Margin = new Thickness(0, 0, 0, 12);
            live.Margin = new Thickness(0);
        }
        else               // side-by-side (tablet/desktop)
        {
            Grid.SetRow(session, 0); Grid.SetColumn(session, 0); Grid.SetColumnSpan(session, 1);
            Grid.SetRow(live, 0); Grid.SetColumn(live, 1); Grid.SetColumnSpan(live, 1);
            session.Margin = new Thickness(0, 0, 6, 0);
            live.Margin = new Thickness(6, 0, 0, 0);
        }
    }
}
