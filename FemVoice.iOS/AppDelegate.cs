using Avalonia;
using Avalonia.iOS;
using Foundation;
using FemVoice.Avalonia;

namespace FemVoice.iOS;

/// <summary>
/// The iOS application delegate. It bootstraps the SHARED Avalonia <see cref="App"/> (from FemVoice.Avalonia); that
/// app's single-view lifetime branch sets the shared <c>ShellView</c> as the root MainView, so iPhone/iPad show the
/// same navigation/pages/theme/onboarding as the desktop and Android heads. No domain logic here — platform host only.
/// </summary>
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder);
}
