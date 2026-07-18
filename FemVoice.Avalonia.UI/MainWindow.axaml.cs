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
        var shell = AppServices.Services.GetRequiredService<ShellViewModel>();
        DataContext = shell;
        shell.ShowOnboardingIfFirstRun();   // first-run onboarding (never a nav item; shown once)
    }
}
