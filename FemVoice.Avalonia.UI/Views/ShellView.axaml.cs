using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace FemVoice.Avalonia.Views;

/// <summary>
/// The shared application shell (header · status strip · nav pane | content | info sidebar). Hosted by the desktop
/// <see cref="MainWindow"/> and by the Android head (single-view root MainView). The layout is RESPONSIVE (driven by
/// width in <see cref="ApplyResponsive"/>): wide = nav rail + info sidebar inline; tablet = nav rail inline, sidebar
/// hidden; phone = nav pane overlays behind a hamburger, sidebar hidden. Pure display-only layout — no behaviour or
/// clinical change. The DataContext (a ShellViewModel) is supplied by the host.
/// </summary>
public partial class ShellView : UserControl
{
    public ShellView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += (_, e) => ApplyResponsive(e.NewSize.Width);
    }

    private void ApplyResponsive(double width)
    {
        var split = this.FindControl<SplitView>("NavSplit");
        var info = this.FindControl<Border>("InfoSidebar");
        var burger = this.FindControl<Button>("HamburgerButton");
        if (split is null) return;

        bool phone = width < 620;
        bool wide = width >= 900;

        if (phone)
        {
            // Phone: nav pane overlays the content and is toggled by the hamburger (closed by default).
            split.DisplayMode = SplitViewDisplayMode.Overlay;
            split.IsPaneOpen = false;
            if (burger is not null) burger.IsVisible = true;
        }
        else
        {
            // Tablet/desktop: nav rail is always visible inline.
            split.DisplayMode = SplitViewDisplayMode.Inline;
            split.IsPaneOpen = true;
            if (burger is not null) burger.IsVisible = false;
        }

        // The static info sidebar only fits on wide (desktop) widths.
        if (info is not null) info.IsVisible = wide;
    }

    private void OnHamburgerClick(object? sender, RoutedEventArgs e)
    {
        var split = this.FindControl<SplitView>("NavSplit");
        if (split is not null) split.IsPaneOpen = !split.IsPaneOpen;
    }

    // On phone (overlay pane), tapping a nav item closes the pane so the chosen page is visible.
    private void OnNavItemClick(object? sender, RoutedEventArgs e)
    {
        var split = this.FindControl<SplitView>("NavSplit");
        if (split is not null && split.DisplayMode == SplitViewDisplayMode.Overlay) split.IsPaneOpen = false;
    }
}
