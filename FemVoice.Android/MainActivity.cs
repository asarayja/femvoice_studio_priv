using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using FemVoice.Avalonia;
using FemVoiceStudio.Audio.Abstractions;

namespace FemVoice.Android;

/// <summary>
/// The Android launcher activity. It bootstraps the SHARED Avalonia <see cref="App"/> (from FemVoice.Avalonia);
/// that app's single-view lifetime branch sets the shared <c>ShellView</c> as the root MainView, so the phone shows
/// the same navigation/pages/theme as the desktop head. No domain logic here — platform host only.
/// </summary>
[Activity(
    Label = "FemVoice Studio",
    Icon = "@mipmap/appicon",
    Theme = "@style/FemVoiceSplash",   // brand splash (logo on dark bg) shown while the app loads
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Provide the REAL Android capture backend (AudioRecord) to the shared DI, BEFORE Avalonia builds the shell
        // and resolves IAudioCaptureService. Without this, Android falls back to the synthetic display-only source.
        AudioCaptureBackendFactory.PlatformRealBackendFactory = () => new AndroidAudioCaptureService();
        // And the real Android speaker backend (AudioTrack) for "hear your own voice".
        AudioPlaybackBackendFactory.PlatformPlaybackFactory = () => new AndroidAudioPlaybackService();

        base.OnCreate(savedInstanceState);

        // Android 6+ needs the RECORD_AUDIO permission granted at RUNTIME (the manifest declaration is not enough).
        // Request it once on launch; capture only starts when the user presses Start, by which point it is granted.
        if (CheckSelfPermission(global::Android.Manifest.Permission.RecordAudio) != Permission.Granted)
            RequestPermissions(new[] { global::Android.Manifest.Permission.RecordAudio }, 100);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder);
}
