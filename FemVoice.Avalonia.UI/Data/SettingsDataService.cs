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
            File.Copy(_dbPath, dest, overwrite: true);
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
            Directory.CreateDirectory(_backupDir);
            // Safety copy of the current DB before overwriting, so a restore is itself reversible.
            if (File.Exists(_dbPath))
                File.Copy(_dbPath, Path.Combine(_backupDir, $"femvoice-pre-restore-{nowLocal:yyyyMMdd-HHmmss}.db"), overwrite: true);
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            File.Copy(backupPath, _dbPath, overwrite: true);
            return new DataOpResult(true, L.Get("SettingsData_Restored", "Gjenopprettet. Start appen på nytt for at endringen skal gjelde fullt ut."));
        }
        catch (Exception ex) { return new DataOpResult(false, string.Format(L.Get("SettingsData_RestoreFailedFormat", "Gjenoppretting feilet: {0}"), ex.Message)); }
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
