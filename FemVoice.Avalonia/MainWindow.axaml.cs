using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FemVoice.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        var status = this.FindControl<TextBlock>("StatusText");
        if (status is not null)
            status.Text = "Shared FemVoice.Core services resolved via DI. (Parity dashboard not yet ported.)";
    }
}
