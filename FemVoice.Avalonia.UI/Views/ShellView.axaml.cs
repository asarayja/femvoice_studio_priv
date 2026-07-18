using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FemVoice.Avalonia.ViewModels;

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
    private double _lastWidth = 1000;

    public ShellView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += (_, e) => { _lastWidth = e.NewSize.Width; ApplyResponsive(_lastWidth); };
        // Re-apply chrome visibility when the shell enters/leaves first-run onboarding (CurrentPage change).
        DataContextChanged += (_, _) => HookOnboarding();
        HookOnboarding();
    }

    private ShellViewModel? _hooked;

    private void HookOnboarding()
    {
        if (_hooked is not null) _hooked.PropertyChanged -= OnShellPropertyChanged;
        _hooked = DataContext as ShellViewModel;
        if (_hooked is not null) _hooked.PropertyChanged += OnShellPropertyChanged;
        ApplyResponsive(_lastWidth);
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.CurrentPage) or nameof(ShellViewModel.IsOnboarding))
            ApplyResponsive(_lastWidth);
    }

    private void ApplyResponsive(double width)
    {
        var split = this.FindControl<SplitView>("NavSplit");
        var info = this.FindControl<Border>("InfoSidebar");
        var burger = this.FindControl<Button>("HamburgerButton");
        if (split is null) return;

        // During first-run onboarding the rest of the app is unreachable: hide the nav pane, info sidebar and
        // hamburger entirely until setup has been chosen and saved (or skipped). The status strip hides via binding.
        if (_hooked?.IsOnboarding == true)
        {
            split.DisplayMode = SplitViewDisplayMode.Overlay;   // Overlay + closed = pane takes no layout width
            split.IsPaneOpen = false;
            if (burger is not null) burger.IsVisible = false;
            if (info is not null) info.IsVisible = false;
            return;
        }

        bool phone = width < 620;
        bool wide = width >= 900;

        // The hamburger is ALWAYS available so the nav menu can be hidden/shown on every width (user request).
        if (burger is not null) burger.IsVisible = true;

        if (phone)
        {
            // Phone: nav pane overlays the content and is toggled by the hamburger (closed by default).
            split.DisplayMode = SplitViewDisplayMode.Overlay;
            split.IsPaneOpen = false;
        }
        else
        {
            // Tablet/desktop: nav rail is inline and open by default, but collapsible via the hamburger. Preserve the
            // user's open/closed choice across resizes within the non-phone range (don't force it back open).
            split.DisplayMode = SplitViewDisplayMode.Inline;
            if (!_userToggledPane) split.IsPaneOpen = true;
        }

        // The static info sidebar only fits on wide (desktop) widths.
        if (info is not null) info.IsVisible = wide;
    }

    // Remembers that the user explicitly collapsed/expanded the pane so a later resize won't override their choice.
    private bool _userToggledPane;

    private void OnHamburgerClick(object? sender, RoutedEventArgs e)
    {
        var split = this.FindControl<SplitView>("NavSplit");
        if (split is null) return;
        split.IsPaneOpen = !split.IsPaneOpen;
        // On desktop (inline) treat an explicit toggle as a sticky preference; on phone (overlay) it's transient.
        if (split.DisplayMode == SplitViewDisplayMode.Inline) _userToggledPane = true;
    }

    // On phone (overlay pane), tapping a nav item closes the pane so the chosen page is visible.
    private void OnNavItemClick(object? sender, RoutedEventArgs e)
    {
        var split = this.FindControl<SplitView>("NavSplit");
        if (split is not null && split.DisplayMode == SplitViewDisplayMode.Overlay) split.IsPaneOpen = false;
    }
}
