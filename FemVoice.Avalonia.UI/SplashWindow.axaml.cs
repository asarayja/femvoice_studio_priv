using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FemVoice.Avalonia;

public partial class SplashWindow : Window
{
    public SplashWindow() => AvaloniaXamlLoader.Load(this);
}
