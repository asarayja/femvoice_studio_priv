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
        // ONLY a valid user-saved preference; otherwise the App.axaml dark baseline stands. No WPF ThemeManager, no DB.
        FemVoice.Avalonia.Theming.ThemeActivation.ApplyFromStore();
        // Stage 2B: apply the saved language preference to the Avalonia-LOCAL resolver before the window renders.
        // Fail-safe — applies only a valid saved preference; no Core SetLanguage, no global thread-culture change.
        // Reduce-motion remains persisted-only.
        FemVoice.Avalonia.Localization.LanguageActivation.ApplyFromStore();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
