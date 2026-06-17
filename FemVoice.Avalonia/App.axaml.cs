using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace FemVoice.Avalonia;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Stage 2A: apply the saved theme preference (Avalonia-only) before the window renders. Fail-safe — applies
        // ONLY a valid user-saved preference; otherwise the App.axaml dark baseline stands. Theme only (language /
        // reduce-motion remain persisted-only). No WPF ThemeManager, no DB, no culture change.
        FemVoice.Avalonia.Theming.ThemeActivation.ApplyFromStore();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
