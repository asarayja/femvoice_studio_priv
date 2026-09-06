using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoice.Avalonia.Data;
using L = FemVoice.Avalonia.Localization.Localized;

namespace FemVoice.Avalonia.Cloud;

/// <summary>Outcome of a sync operation, with a message ready for the UI.</summary>
public sealed record CloudSyncResult(bool Ok, string Message, int SessionsAdded = 0);

/// <summary>
/// Carries training progress between devices through an <see cref="ICloudBackupProvider"/>.
///
/// The one rule that matters: PULL MERGES, IT NEVER REPLACES. Sessions are immutable events, so bringing another
/// device's backup down is a UNION (see DatabaseService.MergeSessionsFrom) — train on the phone Monday and the PC
/// Tuesday and BOTH days survive in both places. A "download and restore" design would silently destroy a day, which
/// is precisely why the merge groundwork had to exist before any cloud transport.
///
/// All provider interaction goes through the interface, so every rule here is unit-testable without Google, a network
/// or credentials.
/// </summary>
public sealed class CloudSyncService
{
    private readonly ICloudBackupProvider _provider;
    private readonly SettingsDataService _data;
    private readonly FemVoiceStudio.Data.IDatabaseService? _database;

    /// <summary>Prefix for the files this app owns in the provider's folder.</summary>
    public const string RemotePrefix = "femvoice-";

    public CloudSyncService(ICloudBackupProvider provider, SettingsDataService data,
        FemVoiceStudio.Data.IDatabaseService? database)
    {
        _provider = provider;
        _data = data;
        _database = database;
    }

    public ICloudBackupProvider Provider => _provider;

    /// <summary>
    /// Back this device's database up and upload it. The remote name carries the device name and a timestamp so the
    /// other device can tell whose backup it is picking up.
    /// </summary>
    public async Task<CloudSyncResult> PushAsync(DateTime nowLocal, string deviceName, CancellationToken cancellationToken = default)
    {
        if (!_provider.IsConfigured) return NotConfigured();
        if (_provider.SignedInAccount is null) return NotSignedIn();

        var backup = _data.Backup(nowLocal);
        if (!backup.Ok) return new CloudSyncResult(false, backup.Message);

        // Back up writes into the local Backups folder; take the newest entry as what we just produced.
        var newest = _data.ListBackups().FirstOrDefault();
        if (newest is null || !File.Exists(newest.FilePath))
            return new CloudSyncResult(false, L.Get("Cloud_NoLocalBackup", "Fant ingen lokal sikkerhetskopi å laste opp."));

        string safeDevice = Sanitize(deviceName);
        string remoteName = $"{RemotePrefix}{safeDevice}-{nowLocal:yyyyMMdd-HHmmss}.db";
        try
        {
            string? id = await _provider.UploadAsync(newest.FilePath, remoteName, cancellationToken).ConfigureAwait(false);
            return id is null
                ? new CloudSyncResult(false, L.Get("Cloud_UploadFailed", "Opplasting feilet."))
                : new CloudSyncResult(true, string.Format(L.Get("Cloud_UploadedFormat", "Lastet opp «{0}»."), remoteName));
        }
        catch (Exception ex) { return new CloudSyncResult(false, Failed(ex)); }
    }

    /// <summary>
    /// Download every backup this app has stored EXCEPT ones this device just uploaded, and MERGE each into the local
    /// database. Merging is idempotent, so pulling repeatedly is safe and adds nothing the second time.
    /// </summary>
    public async Task<CloudSyncResult> PullAsync(CancellationToken cancellationToken = default)
    {
        if (!_provider.IsConfigured) return NotConfigured();
        if (_provider.SignedInAccount is null) return NotSignedIn();
        if (_database is not FemVoiceStudio.Data.DatabaseService concrete)
            return new CloudSyncResult(false, L.Get("Cloud_NeedsDatabase", "Synkronisering krever en aktiv database."));

        try
        {
            var remote = await _provider.ListAsync(cancellationToken).ConfigureAwait(false);
            var ours = remote.Where(f => f.Name.StartsWith(RemotePrefix, StringComparison.Ordinal)).ToList();
            if (ours.Count == 0)
                return new CloudSyncResult(true, L.Get("Cloud_NothingToPull", "Ingen sikkerhetskopier i skyen ennå."));

            string tempDir = Path.Combine(Path.GetTempPath(), "femvoice-cloud");
            Directory.CreateDirectory(tempDir);

            int addedTotal = 0, merged = 0;
            foreach (var file in ours)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string temp = Path.Combine(tempDir, $"pull-{file.Id}.db");
                try
                {
                    if (!await _provider.DownloadAsync(file.Id, temp, cancellationToken).ConfigureAwait(false)) continue;
                    // MERGE, never restore: the local device's own sessions must survive.
                    addedTotal += concrete.MergeSessionsFrom(temp);
                    merged++;
                }
                finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { /* temp file */ } }
            }

            return new CloudSyncResult(true, addedTotal > 0
                ? string.Format(L.Get("Cloud_PulledFormat", "Hentet {0} nye økter fra {1} sikkerhetskopi(er)."), addedTotal, merged)
                : L.Get("Cloud_PulledNothing", "Alt var allerede oppdatert — ingen nye økter."), addedTotal);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new CloudSyncResult(false, Failed(ex)); }
    }

    private static CloudSyncResult NotConfigured() =>
        new(false, L.Get("Cloud_NotConfigured", "Skysynkronisering er ikke satt opp i denne byggingen."));

    private static CloudSyncResult NotSignedIn() =>
        new(false, L.Get("Cloud_NotSignedIn", "Logg inn først."));

    private static string Failed(Exception ex) =>
        string.Format(L.Get("Cloud_FailedFormat", "Synkronisering feilet: {0}"), ex.Message);

    /// <summary>Device name reduced to something safe for a file name.</summary>
    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "device";
        var cleaned = new string(name.Trim().Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
        cleaned = string.Join("-", cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length == 0 ? "device" : cleaned[..Math.Min(cleaned.Length, 24)];
    }
}
