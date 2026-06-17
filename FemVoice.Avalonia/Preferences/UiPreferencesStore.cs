using System;
using System.IO;
using System.Text.Json;

namespace FemVoice.Avalonia.Preferences;

/// <summary>
/// Avalonia-OWNED, file-backed store for the harmless <see cref="UiPreferences"/> (Stage 1). Reads/writes a small
/// JSON file under an Avalonia-specific app-data folder that does NOT overlap the WPF settings files or the
/// SQLite database. Loading is fail-safe: a missing, empty, or invalid/corrupt file yields <see
/// cref="UiPreferences.Defaults"/> and never throws (so startup/UI cannot crash). It performs NO runtime
/// activation — it only serialises the three display-only preferences. No DB, no WPF, no clinical/audio code.
/// </summary>
public sealed class UiPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>The Avalonia-local file path. Distinct from any WPF settings file / DB (own "FemVoiceAvalonia" dir).</summary>
    public string FilePath { get; }

    /// <summary>Default app-data location, or an explicit path (used by tests/smokes to avoid touching user data).</summary>
    public UiPreferencesStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultPath();
    }

    /// <summary>Avalonia-owned default path: &lt;ApplicationData&gt;/FemVoiceAvalonia/ui-preferences.json.</summary>
    public static string DefaultPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
            appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(appData, "FemVoiceAvalonia", "ui-preferences.json");
    }

    /// <summary>Load preferences; return safe defaults on any missing/empty/invalid/corrupt file (never throws).</summary>
    public UiPreferences Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return UiPreferences.Defaults();
            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return UiPreferences.Defaults();
            var loaded = JsonSerializer.Deserialize<UiPreferences>(json);
            return (loaded ?? UiPreferences.Defaults()).Normalized();
        }
        catch (Exception)
        {
            // Corrupt/unreadable file → safe defaults, no crash. (Intentionally does not rethrow.)
            return UiPreferences.Defaults();
        }
    }

    /// <summary>Persist the (normalized) preferences to the Avalonia-local file. Creates the folder if needed.
    /// Fail-safe (symmetric with <see cref="Load"/>): an I/O error (permission denied, disk full, locked file,
    /// invalid path / parent-is-a-file) is swallowed and reported as <c>false</c> rather than thrown, so the UI
    /// cannot crash on Save. Returns <c>true</c> when the file was written.</summary>
    public bool Save(UiPreferences preferences)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(preferences.Normalized(), JsonOptions));
            return true;
        }
        catch (Exception)
        {
            // App-data preference file: a failed write is non-fatal and must never crash the UI.
            return false;
        }
    }
}
