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
        // Shell hosts the dashboard + exercise guide/detail via ContentControl + DataTemplates.
        DataContext = AppServices.Services.GetRequiredService<ShellViewModel>();
    }
}
