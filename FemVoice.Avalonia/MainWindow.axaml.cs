using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using FemVoice.Avalonia.ViewModels;

namespace FemVoice.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        // Resolve the shared-service-backed dashboard VM from the composition root.
        DataContext = Program.Services.GetRequiredService<MainDashboardViewModel>();
    }
}
