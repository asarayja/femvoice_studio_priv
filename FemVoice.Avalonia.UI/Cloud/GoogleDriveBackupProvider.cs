using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoice.Avalonia.Cloud;

/// <summary>
/// Stores FemVoice backups in the user's Google Drive <b>appDataFolder</b> — a hidden, app-private folder that does
/// not appear in their Drive, is not shared, and is deleted when they remove the app's access.
///
/// The <c>drive.appdata</c> scope is chosen over the broader <c>drive.file</c> deliberately: this data reveals that
/// the user is transitioning, so the app should be able to see nothing but its own backups, and the folder should not
/// be visible to anyone browsing their Drive.
///
/// DESKTOP flow: loopback redirect on 127.0.0.1 + PKCE, the pattern Google documents for installed apps. The refresh
/// token is stored in app data. ANDROID/iOS need their own flows (custom tabs / ASWebAuthenticationSession) and are
/// not implemented here — <see cref="IsConfigured"/> plus the UI guard keep the feature hidden there.
///
/// NOT VERIFIED END-TO-END in this repository: signing in requires real OAuth credentials and an interactive browser,
/// neither of which exists in the headless build/test environment. The parts that encode product behaviour rather
/// than Google's protocol live in <see cref="CloudSyncService"/> and ARE tested, against a fake provider.
/// </summary>
public sealed class GoogleDriveBackupProvider : ICloudBackupProvider
{
    private const string AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string DriveFiles = "https://www.googleapis.com/drive/v3/files";
    private const string DriveUpload = "https://www.googleapis.com/upload/drive/v3/files";
    private const string Scope = "https://www.googleapis.com/auth/drive.appdata https://www.googleapis.com/auth/userinfo.email";

    private readonly GoogleClientConfig _config;
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTime _accessTokenExpiresUtc;

    public GoogleDriveBackupProvider(GoogleClientConfig? config = null, HttpClient? http = null)
    {
        _config = config ?? GoogleClientConfig.Load();
        _http = http ?? new HttpClient();
        var stored = LoadStored();
        SignedInAccount = stored?.Email;
    }

    public string DisplayName => "Google Drive";
    public bool IsConfigured => _config.IsConfigured;
    public string? SignedInAccount { get; private set; }

    private static string TokenPath => Path.Combine(
        FemVoiceStudio.Data.DatabaseService.ResolveAppDataDir(), "google_token.json");

    private sealed record StoredToken(string RefreshToken, string Email);

    private static StoredToken? LoadStored()
    {
        try
        {
            if (!File.Exists(TokenPath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(TokenPath));
            string refresh = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() ?? "" : "";
            string email = doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";
            return refresh.Length == 0 ? null : new StoredToken(refresh, email);
        }
        catch { return null; }
    }

    private static void SaveStored(StoredToken token)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TokenPath)!);
            File.WriteAllText(TokenPath, JsonSerializer.Serialize(new { refresh_token = token.RefreshToken, email = token.Email }));
        }
        catch { /* a lost token only means signing in again */ }
    }

    public void SignOut()
    {
        try { if (File.Exists(TokenPath)) File.Delete(TokenPath); } catch { /* best effort */ }
        _accessToken = null;
        SignedInAccount = null;
    }

    // ── OAuth (desktop: loopback + PKCE) ──────────────────────────────────────────────────────────────────────────
    public async Task<bool> SignInAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return false;
        try
        {
            // Loopback listener on a free port; Google redirects the browser back here with ?code=...
            var listener = new HttpListener();
            int port = FreePort();
            string redirect = $"http://127.0.0.1:{port}/";
            listener.Prefixes.Add(redirect);
            listener.Start();
            try
            {
                string verifier = RandomUrlSafe(64);
                string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
                string url = $"{AuthUrl}?response_type=code&client_id={Uri.EscapeDataString(_config.ClientId)}" +
                             $"&redirect_uri={Uri.EscapeDataString(redirect)}&scope={Uri.EscapeDataString(Scope)}" +
                             $"&code_challenge={challenge}&code_challenge_method=S256&access_type=offline&prompt=consent";
                OpenBrowser(url);

                var contextTask = listener.GetContextAsync();
                var completed = await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(5), cancellationToken)).ConfigureAwait(false);
                if (completed != contextTask) return false;   // timed out or cancelled

                var context = await contextTask.ConfigureAwait(false);
                string? code = context.Request.QueryString["code"];
                await RespondAsync(context, code is null
                    ? "FemVoice: innlogging avbrutt. Du kan lukke denne fanen."
                    : "FemVoice: innlogging fullført. Du kan lukke denne fanen.").ConfigureAwait(false);
                if (code is null) return false;

                using var response = await _http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _config.ClientId,
                    ["client_secret"] = _config.ClientSecret,
                    ["code_verifier"] = verifier,
                    ["grant_type"] = "authorization_code",
                    ["redirect_uri"] = redirect,
                }), cancellationToken).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                if (!doc.RootElement.TryGetProperty("refresh_token", out var refreshEl)) return false;
                string refresh = refreshEl.GetString() ?? "";
                if (refresh.Length == 0) return false;
                _accessToken = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
                _accessTokenExpiresUtc = DateTime.UtcNow.AddMinutes(50);

                string email = await FetchEmailAsync(cancellationToken).ConfigureAwait(false) ?? "";
                SaveStored(new StoredToken(refresh, email));
                SignedInAccount = email;
                return true;
            }
            finally { listener.Stop(); }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Debug.WriteLine($"Google sign-in failed: {ex.Message}"); return false; }
    }

    private async Task<string?> AccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTime.UtcNow < _accessTokenExpiresUtc) return _accessToken;
        var stored = LoadStored();
        if (stored is null) return null;
        try
        {
            using var response = await _http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _config.ClientId,
                ["client_secret"] = _config.ClientSecret,
                ["refresh_token"] = stored.RefreshToken,
                ["grant_type"] = "refresh_token",
            }), cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            _accessToken = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            _accessTokenExpiresUtc = DateTime.UtcNow.AddMinutes(50);
            return _accessToken;
        }
        catch (Exception ex) { Debug.WriteLine($"Token refresh failed: {ex.Message}"); return null; }
    }

    private async Task<string?> FetchEmailAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
        }
        catch { return null; }
    }

    // ── Drive appDataFolder ───────────────────────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<CloudBackupFile>> ListAsync(CancellationToken cancellationToken = default)
    {
        var token = await AccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null) return Array.Empty<CloudBackupFile>();

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{DriveFiles}?spaces=appDataFolder&orderBy=modifiedTime desc&pageSize=100" +
            "&fields=files(id,name,modifiedTime,size)");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return Array.Empty<CloudBackupFile>();

        var files = new List<CloudBackupFile>();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (doc.RootElement.TryGetProperty("files", out var arr))
            foreach (var f in arr.EnumerateArray())
            {
                string id = f.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                string name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                DateTime modified = f.TryGetProperty("modifiedTime", out var m) && m.TryGetDateTime(out var dt) ? dt.ToUniversalTime() : DateTime.MinValue;
                long size = f.TryGetProperty("size", out var s) && long.TryParse(s.GetString(), out var parsed) ? parsed : 0;
                if (id.Length > 0) files.Add(new CloudBackupFile(id, name, modified, size));
            }
        return files;
    }

    public async Task<string?> UploadAsync(string localPath, string remoteName, CancellationToken cancellationToken = default)
    {
        var token = await AccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null || !File.Exists(localPath)) return null;

        // Multipart upload: JSON metadata (name + appDataFolder parent) then the file bytes.
        var metadata = JsonSerializer.Serialize(new { name = remoteName, parents = new[] { "appDataFolder" } });
        using var content = new MultipartContent("related")
        {
            new StringContent(metadata, Encoding.UTF8, "application/json"),
            new ByteArrayContent(await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false))
            { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{DriveUpload}?uploadType=multipart&fields=id") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
    }

    public async Task<bool> DownloadAsync(string fileId, string destinationPath, CancellationToken cancellationToken = default)
    {
        var token = await AccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null || string.IsNullOrWhiteSpace(fileId)) return false;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{DriveFiles}/{fileId}?alt=media");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var file = File.Create(destinationPath);
        await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        return true;
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────────────
    private static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string RandomUrlSafe(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task RespondAsync(HttpListenerContext context, string message)
    {
        try
        {
            byte[] body = Encoding.UTF8.GetBytes($"<html><body style='font-family:sans-serif'><p>{message}</p></body></html>");
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = body.Length;
            await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
            context.Response.Close();
        }
        catch { /* the browser tab closing first is fine */ }
    }

    private static void OpenBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch
        {
            // UseShellExecute is unavailable on some Linux setups; fall back to xdg-open.
            try { Process.Start("xdg-open", url); } catch { /* the user can copy the URL from the UI */ }
        }
    }
}
