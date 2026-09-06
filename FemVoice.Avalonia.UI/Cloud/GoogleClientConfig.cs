using System;
using System.IO;
using System.Text.Json;

namespace FemVoice.Avalonia.Cloud;

/// <summary>
/// The Google OAuth client this build uses, loaded at RUNTIME from a file outside the source tree.
///
/// Deliberately NOT compiled in. The book-writer app keeps its credentials in a gitignored source file, which works
/// but means a fresh clone does not build until you copy a template. Reading them at runtime instead has two
/// advantages: the repository never contains a secret at all (not even in an ignored working-tree file that is easy
/// to paste into a diff or a support bundle), and the build always succeeds without setup.
///
/// Expected file — <c>&lt;app data&gt;/FemVoiceStudio/google_client.json</c>:
/// <code>
/// { "client_id": "....apps.googleusercontent.com", "client_secret": "GOCSPX-..." }
/// </code>
///
/// Create the client at https://console.cloud.google.com/apis/credentials :
///   1. Enable the Google Drive API for the project.
///   2. Credentials → Create credentials → OAuth client ID → Application type: <b>Desktop app</b>.
///   3. On the consent screen add scopes <c>drive.appdata</c> and <c>userinfo.email</c>; while the app is in
///      "Testing", add your own Google account under Test users.
/// Google treats the DESKTOP client secret as non-confidential (it is embedded in the app and cannot be kept secret),
/// which is why loopback + PKCE is the supported desktop pattern.
///
/// When the file is absent or incomplete, <see cref="IsConfigured"/> is false and the UI hides cloud sync entirely
/// rather than offering a button that could only fail with "invalid_client".
/// </summary>
public sealed class GoogleClientConfig
{
    public string ClientId { get; }
    public string ClientSecret { get; }

    private GoogleClientConfig(string clientId, string clientSecret)
    {
        ClientId = clientId;
        ClientSecret = clientSecret;
    }

    /// <summary>True when a usable client id/secret was found.</summary>
    public bool IsConfigured => ClientId.Length > 0 && ClientSecret.Length > 0
                                && !ClientId.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Where the credentials file is expected. Shown in the UI so the user knows where to put it.</summary>
    public static string ConfigPath => Path.Combine(
        FemVoiceStudio.Data.DatabaseService.ResolveAppDataDir(), "google_client.json");

    /// <summary>Load the configured client, or an unconfigured instance. Never throws.</summary>
    public static GoogleClientConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                string id = doc.RootElement.TryGetProperty("client_id", out var i) ? i.GetString() ?? "" : "";
                string secret = doc.RootElement.TryGetProperty("client_secret", out var s) ? s.GetString() ?? "" : "";
                return new GoogleClientConfig(id.Trim(), secret.Trim());
            }
        }
        catch { /* unreadable/malformed → simply unconfigured */ }
        return new GoogleClientConfig("", "");
    }
}
