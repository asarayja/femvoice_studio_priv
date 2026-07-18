namespace FemVoice.Avalonia.Preferences;

/// <summary>
/// Bridges the persisted <see cref="UiPreferences.MicDeviceId"/> to the audio-capture pipeline. Capture callsites
/// (dashboard / exercise / analyzer / resonance) read the user's chosen input device here so the Settings
/// microphone selection actually routes capture. Fail-safe: any read error yields the system default (null).
/// </summary>
public static class CapturePreferences
{
    /// <summary>The user's chosen input-device id (backend-specific), or null for the system default. Never throws.</summary>
    public static string? SelectedMicDeviceId()
    {
        try { return new UiPreferencesStore().Load().MicDeviceId; }
        catch { return null; }
    }
}
