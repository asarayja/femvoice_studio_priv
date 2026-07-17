using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FemVoice.Avalonia.Views;

/// <summary>
/// The shared application shell (header · status strip · nav rail | content | info sidebar). Hosted by the desktop
/// <see cref="MainWindow"/> (inside a Window) and by the Android head (as the single-view root MainView), so both
/// heads render the same navigation, pages, and theme. The DataContext (a ShellViewModel) is supplied by the host.
/// </summary>
public partial class ShellView : UserControl
{
    public ShellView() => AvaloniaXamlLoader.Load(this);
}
