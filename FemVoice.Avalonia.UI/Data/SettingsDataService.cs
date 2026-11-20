using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using L = FemVoice.Avalonia.Localization.Localized;

namespace FemVoice.Avalonia.Data;

/// <summary>One backup file the user can restore from (newest first).</summary>
public sealed record BackupEntry(string FilePath, string DisplayName, DateTime WhenLocal);

/// <summary>The outcome of a data operation, with a user-facing message.</summary>
public sealed record DataOpResult(bool Ok, string Message);

/// <summary>
/// REAL database backup / restore / clear for the Avalonia Settings page. Operates on the shared SQLite database
/// file (<c>&lt;MyDocuments&gt;/FemVoiceStudio/femvoice.db</c> — the same location <see cref="FemVoiceStudio.Data.DatabaseService"/>
/// uses) and a sibling <c>Backups</c> folder. These are genuine, non-simulated file operations:
/// <list type="bullet">
///   <item><b>Backup</b> — copies the live DB to <c>Backups/femvoice-backup-yyyyMMdd-HHmmss.db</c> (non-destructive).</item>
///   <item><b>Restore</b> — copies a chosen backup back over the live DB (destructive to current data; the caller
///         confirms first). A safety copy of the current DB is taken before overwriting.</item>
///   <item><b>Clear</b> — empties the database via <see cref="FemVoiceStudio.Data.DatabaseService.ResetDatabase"/>
///         (recreates the empty schema) when a concrete service is available, else deletes the file.</item>
/// </list>
/// Every method is fail-safe: it returns a <see cref="DataOpResult"/> with a message and never throws to the UI.
/// Restore/Clear take full effect on the next app start (the process may hold an open connection); the messages say so.
/// </summary>
public sealed class SettingsDataService
{
    private readonly FemVoiceStudio.Data.IDatabaseService? _database;
    private readonly string _dbPath;
    private readonly string _backupDir;

    public SettingsDataService(FemVoiceStudio.Data.IDatabaseService? database, string? dbPathOverride = null)
    {
        _database = database;
        _dbPath = dbPathOverride ?? DefaultDbPath();
        _backupDir = Path.Combine(Path.GetDirectoryName(_dbPath) ?? ".", "Backups");
    }

    /// <summary>The canonical DB location used by <see cref="FemVoiceStudio.Data.DatabaseService"/> (Android-safe).</summary>
    public static string DefaultDbPath() => Path.Combine(
        FemVoiceStudio.Data.DatabaseService.ResolveAppDataDir(), "femvoice.db");

    public string DatabasePath => _dbPath;
    public string BackupFolder => _backupDir;

    /// <summary>True when a database file actually exists to back up / restore / clear.</summary>
    public bool DatabaseExists => File.Exists(_dbPath);

    /// <summary>Existing backups, newest first (never throws; empty when none).</summary>
    public IReadOnlyList<BackupEntry> ListBackups()
    {
        try
        {
            if (!Directory.Exists(_backupDir)) return Array.Empty<BackupEntry>();
            return Directory.GetFiles(_backupDir, "femvoice-backup-*.db")
                .Select(p => new BackupEntry(p, Path.GetFileName(p), File.GetLastWriteTime(p)))
                .OrderByDescending(b => b.WhenLocal)
                .ToList();
        }
        catch { return Array.Empty<BackupEntry>(); }
    }

    /// <summary>Copy the live DB to a timestamped backup file. <paramref name="nowLocal"/> is supplied by the caller
    /// so the name is deterministic/testable. Never throws.</summary>
    public DataOpResult Backup(DateTime nowLocal)
    {
        try
        {
            if (!File.Exists(_dbPath))
                return new DataOpResult(false, L.Get("SettingsData_NoDbBackup", "Ingen database å sikkerhetskopiere ennå."));
            Directory.CreateDirectory(_backupDir);
            string name = $"femvoice-backup-{nowLocal:yyyyMMdd-HHmmss}.db";
            string dest = Path.Combine(_backupDir, name);
            Snapshot(_dbPath, dest);
            return new DataOpResult(true, string.Format(L.Get("SettingsData_BackupSavedFormat", "Sikkerhetskopi lagret: {0}"), name));
        }
        catch (Exception ex) { return new DataOpResult(false, string.Format(L.Get("SettingsData_BackupFailedFormat", "Sikkerhetskopi feilet: {0}"), ex.Message)); }
    }

    /// <summary>Restore the DB from a chosen backup (destructive — caller confirms). Takes a pre-restore safety copy
    /// of the current DB first. Effective fully on the next app start. Never throws.</summary>
    public DataOpResult Restore(string backupPath, DateTime nowLocal)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                return new DataOpResult(false, L.Get("SettingsData_BackupNotFound", "Fant ikke sikkerhetskopien."));
            // Refuse anything that is not a readable FemVoice database BEFORE touching the live one: replacing the
            // user's history with a truncated download or a stray file is the worst outcome this page can produce.
            if (!IsFemVoiceDatabase(backupPath))
                return new DataOpResult(false, L.Get("SettingsData_BackupNotFound", "Fant ikke sikkerhetskopien."));

            Directory.CreateDirectory(_backupDir);
            // Safety copy of the current DB before overwriting, so a restore is itself reversible. It is named like
            // an ordinary backup (…-pre-restore.db) so it SHOWS UP in the list — it used to be written under a name
            // the list never matched, i.e. an undo that could only be done by hand in the file manager.
            if (File.Exists(_dbPath))
                Snapshot(_dbPath, Path.Combine(_backupDir, $"femvoice-backup-{nowLocal:yyyyMMdd-HHmmss}-pre-restore.db"));

            string dbDir = Path.GetDirectoryName(_dbPath)!;
            Directory.CreateDirectory(dbDir);
            // Stage the restored database NEXT TO the live file, then swap it in with one rename. Writing over the
            // live file in place meant a failure halfway (disk full, lock) left a truncated database.
            string staged = _dbPath + ".restoring";
            Snapshot(backupPath, staged);

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();   // release our own pooled handles first
            // The old database's journal sidecars must not survive next to the new file: a leftover -wal from the
            // previous database would be replayed over the restored one ("database disk image is malformed").
            foreach (var sidecar in new[] { "-wal", "-shm", "-journal" })
            {
                try { File.Delete(_dbPath + sidecar); } catch { /* still held on Windows → handled by the restart */ }
            }
            try
            {
                File.Move(staged, _dbPath, overwrite: true);          // atomic on the same volume
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Windows refuses to rename over a file another handle still has open. Fall back to the in-place copy
                // (the previous behaviour) — the staged file is complete, so the source of the copy is known-good.
                File.Copy(staged, _dbPath, overwrite: true);
                try { File.Delete(staged); } catch { }
            }
            return new DataOpResult(true, L.Get("SettingsData_Restored", "Gjenopprettet. Start appen på nytt for at endringen skal gjelde fullt ut."));
        }
        catch (Exception ex) { return new DataOpResult(false, string.Format(L.Get("SettingsData_RestoreFailedFormat", "Gjenoppretting feilet: {0}"), ex.Message)); }
    }

    /// <summary>
    /// MERGE the sessions from a backup into the current database (non-destructive union) — the way to carry progress
    /// BETWEEN DEVICES. Unlike <see cref="Restore"/>, which replaces the whole file and would discard whatever the
    /// other device recorded, this only ADDS sessions that are not already present. Never throws.
    /// </summary>
    public DataOpResult Merge(string backupPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                return new DataOpResult(false, L.Get("SettingsData_BackupNotFound", "Fant ikke sikkerhetskopien."));
            if (_database is not FemVoiceStudio.Data.DatabaseService concrete)
                return new DataOpResult(false, L.Get("SettingsData_MergeUnavailable", "Sammenslåing krever en aktiv database."));

            int added = concrete.MergeSessionsFrom(backupPath);
            return new DataOpResult(true, added > 0
                ? string.Format(L.Get("SettingsData_MergedFormat", "La til {0} nye økter. Ingenting ble overskrevet."), added)
                : L.Get("SettingsData_MergedNothing", "Ingen nye økter å legge til — alt fantes allerede."));
        }
        catch (Exception ex) { return new DataOpResult(false, string.Format(L.Get("SettingsData_MergeFailedFormat", "Sammenslåing feilet: {0}"), ex.Message)); }
    }

    /// <summary>
    /// Write a CONSISTENT copy of a SQLite database to <paramref name="destPath"/> using SQLite's own online-backup
    /// API. A raw file copy of a live database is not a backup: in WAL mode the newest committed sessions still live
    /// in the "-wal" sidecar, so the copy silently lacked them (and the device they were pushed to never got them);
    /// and a copy taken while a write commits can be torn. The backup API reads one consistent snapshot, sidecars
    /// included. Falls back to a plain copy only when the source cannot be opened as a database at all — keeping the
    /// bytes of a damaged file is still better than keeping nothing.
    /// </summary>
    private static void Snapshot(string sourcePath, string destPath)
    {
        try { File.Delete(destPath); } catch { }
        try
        {
            using var source = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = sourcePath, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
            using var dest = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = destPath, Pooling = false }.ToString());
            source.Open();
            dest.Open();
            source.BackupDatabase(dest);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            try { File.Delete(destPath); } catch { }
            File.Copy(sourcePath, destPath, overwrite: true);
        }
    }

    /// <summary>True when the file opens as a SQLite database that has the FemVoice sessions table.</summary>
    private static bool IsFemVoiceDatabase(string path)
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = path, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TrainingSessions'";
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }
        catch { return false; }
    }

    /// <summary>Empty the database (destructive — caller confirms). Uses the concrete service's ResetDatabase when
    /// available (recreates the empty schema live), else deletes the file. Never throws.</summary>
    public DataOpResult Clear()
    {
        try
        {
            if (_database is FemVoiceStudio.Data.DatabaseService concrete)
            {
                concrete.ResetDatabase();   // drops + recreates the empty schema on the live connection
                return new DataOpResult(true, L.Get("SettingsData_Cleared", "Databasen er tømt (tom, klar til bruk)."));
            }
            if (File.Exists(_dbPath))
            {
                // Release pooled SQLite handles before deleting, or Windows reports the file as
                // "used by another process" (Microsoft.Data.Sqlite pools connections by default).
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(_dbPath);
                return new DataOpResult(true, L.Get("SettingsData_ClearedRestart", "Databasen er tømt. Start appen på nytt for å opprette en ny tom database."));
            }
            return new DataOpResult(false, L.Get("SettingsData_NoDbClear", "Ingen database å tømme."));
        }
        catch (Exception ex) { return new DataOpResult(false, string.Format(L.Get("SettingsData_ClearFailedFormat", "Tømming feilet: {0}"), ex.Message)); }
    }
}
