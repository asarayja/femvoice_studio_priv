using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using FemVoice.Avalonia.ViewModels;

namespace FemVoice.Avalonia;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Stage 2A: apply the saved theme preference (Avalonia-only) before the window renders. Fail-safe — applies
        // ONLY a valid user-saved preference; otherwise the App.axaml dark baseline stands. No WPF ThemeManager, no DB.
        FemVoice.Avalonia.Theming.ThemeActivation.ApplyFromStore();
        // Stage 2B: apply the saved language preference to the Avalonia-LOCAL resolver before the window renders.
        // Fail-safe — applies only a valid saved preference; no Core SetLanguage, no global thread-culture change.
        FemVoice.Avalonia.Localization.LanguageActivation.ApplyFromStore();
        // Stage 2C: apply the saved reduce-motion preference to the Avalonia-owned motion state (Avalonia-local;
        // no WPF/Core/DB). Future Avalonia motion effects gate on MotionActivation.ReduceMotion.
        FemVoice.Avalonia.Accessibility.MotionActivation.ApplyFromStore();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Desktop head (Windows/macOS/Linux): the shell lives in a Window that hosts the shared ShellView.
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // Mobile/single-view head (Android): no Window — set the SAME shared ShellView as the root MainView,
            // with the same ShellViewModel from the shared DI container. Design/behaviour reuse the desktop shell.
            singleView.MainView = new Views.ShellView
            {
                DataContext = Program.Services.GetRequiredService<ShellViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
