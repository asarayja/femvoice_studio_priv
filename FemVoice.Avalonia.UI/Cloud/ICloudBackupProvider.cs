using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoice.Avalonia.Cloud;

/// <summary>One backup file stored in the cloud.</summary>
public sealed record CloudBackupFile(string Id, string Name, DateTime ModifiedUtc, long SizeBytes);

/// <summary>
/// A place to put (and get back) a FemVoice backup file — the transport for carrying training progress between
/// devices. Deliberately a NARROW interface over "a private folder of files": sign in, list, upload, download.
///
/// It exists so <see cref="CloudSyncService"/> — where all the behaviour that can actually be got wrong lives — is
/// testable without Google, a network, or credentials. The real implementation is
/// <see cref="GoogleDriveBackupProvider"/>.
/// </summary>
public interface ICloudBackupProvider
{
    /// <summary>Human-readable provider name for the UI (e.g. "Google Drive").</summary>
    string DisplayName { get; }

    /// <summary>False when the build carries no real OAuth client credentials. The UI hides sign-in entirely rather
    /// than offering a button that can only fail — the same guard the book-writer app uses.</summary>
    bool IsConfigured { get; }

    /// <summary>The signed-in account, or null when signed out.</summary>
    string? SignedInAccount { get; }

    /// <summary>Interactive sign-in. Returns false if the user cancelled or it failed.</summary>
    Task<bool> SignInAsync(CancellationToken cancellationToken = default);

    /// <summary>Forget the stored credentials on this device.</summary>
    void SignOut();

    /// <summary>Backups already stored for this app, newest first.</summary>
    Task<IReadOnlyList<CloudBackupFile>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Upload a local file under <paramref name="remoteName"/>. Returns the stored file id, or null on failure.</summary>
    Task<string?> UploadAsync(string localPath, string remoteName, CancellationToken cancellationToken = default);

    /// <summary>Download a stored file to <paramref name="destinationPath"/>. Returns false on failure.</summary>
    Task<bool> DownloadAsync(string fileId, string destinationPath, CancellationToken cancellationToken = default);
}
