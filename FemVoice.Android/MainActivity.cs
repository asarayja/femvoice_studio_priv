using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using FemVoice.Avalonia;

namespace FemVoice.Android;

/// <summary>
/// The Android launcher activity. It bootstraps the SHARED Avalonia <see cref="App"/> (from FemVoice.Avalonia);
/// that app's single-view lifetime branch sets the shared <c>ShellView</c> as the root MainView, so the phone shows
/// the same navigation/pages/theme as the desktop head. No domain logic here — platform host only.
/// </summary>
[Activity(
    Label = "FemVoice Studio",
    Theme = "@android:style/Theme.Material.Light.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder);
}
