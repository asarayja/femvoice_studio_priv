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

    /// <summary>Whether the user enabled "hear your own voice" (real-time mic→speaker monitoring). Never throws.</summary>
    public static bool HearOwnVoice()
    {
        try { return new UiPreferencesStore().Load().HearOwnVoice; }
        catch { return false; }
    }

    /// <summary>The user's calibrated relaxed-voice resonance baseline (spectral centroid Hz), or null when not yet
    /// calibrated (→ the VoiceBrightnessMeter falls back to fixed anchors). Never throws.</summary>
    public static double? ResonanceBaselineCentroidHz()
    {
        try { var v = new UiPreferencesStore().Load().ResonanceBaselineCentroidHz; return v > 0 ? v : (double?)null; }
        catch { return null; }
    }

    /// <summary>Persist the measured relaxed-voice resonance baseline (spectral centroid Hz). Fail-safe: swallows errors.</summary>
    public static void SetResonanceBaselineCentroidHz(double centroidHz)
    {
        try
        {
            var store = new UiPreferencesStore();
            var prefs = store.Load();
            prefs.ResonanceBaselineCentroidHz = centroidHz;
            store.Save(prefs);
        }
        catch { /* fail-safe: baseline just stays uncalibrated */ }
    }
}
