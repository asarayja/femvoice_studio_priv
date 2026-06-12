using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Local-only backup/restore service for release hardening.
    /// It copies existing persistence files as opaque data and never interprets
    /// voice, scoring, SmartCoach, or health logic.
    /// </summary>
    public sealed class LocalBackupService
    {
        private const int CurrentBackupFormatVersion = 1;
        private const string ManifestEntryName = "manifest.json";
        private const string SettingsEntryName = "settings/settings.json";
        private const string DatabaseEntryName = "data/femvoice.db";
        private const string ReportsMetadataEntryName = "reports/REPORT_VERIFICATION_STATUS.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly LocalBackupPaths _paths;

        public LocalBackupService(LocalBackupPaths paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        public static LocalBackupService CreateDefault()
        {
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio");

            var databasePath = Path.Combine(appFolder, "femvoice.db");
            var reportsMetadataPath = Path.Combine(
                DiagnosticsNaming.PrimaryRoot,
                "REPORT_VERIFICATION_STATUS.json");

            return new LocalBackupService(new LocalBackupPaths(
                SettingsPath: ThemeManager.SettingsPath,
                DatabasePath: databasePath,
                ReportsMetadataPath: reportsMetadataPath,
                SafetyBackupDirectory: Path.Combine(appFolder, "restore-safety-backups")));
        }

        public BackupResult CreateBackup(string destinationDirectory, DateTime? timestamp = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationDirectory))
                    return BackupResult.Fail(null, SafeBackupFailureMessage, "Backup folder was not specified.");

                Directory.CreateDirectory(destinationDirectory);

                var createdAt = timestamp ?? DateTime.Now;
                var fileName = $"FemVoiceStudio_Backup_{createdAt:yyyyMMdd_HHmmss}.zip";
                var backupPath = Path.Combine(destinationDirectory, fileName);
                var included = new List<string>();
                var excluded = new List<string>
                {
                    "research exports",
                    "diagnostics folders",
                    "raw support packages"
                };

                using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    AddIfExists(archive, _paths.SettingsPath, SettingsEntryName, included);
                    AddIfExists(archive, _paths.DatabasePath, DatabaseEntryName, included);
                    AddIfExists(archive, _paths.ReportsMetadataPath, ReportsMetadataEntryName, included);

                    var manifest = new BackupManifest(
                        AppId: "FemVoiceStudio",
                        BackupFormatVersion: CurrentBackupFormatVersion,
                        CreatedAtUtc: DateTime.UtcNow,
                        SettingsVersion: ReadSettingsVersionNoThrow(_paths.SettingsPath),
                        IncludedFiles: included.ToArray(),
                        ExcludedByDefault: excluded.ToArray(),
                        Counts: BackupDataCounts.FromIncludedFiles(included));

                    var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                    using var stream = manifestEntry.Open();
                    JsonSerializer.Serialize(stream, manifest, JsonOptions);
                }

                var validation = ValidateBackupArchive(backupPath);
                if (!validation.Success)
                    return BackupResult.Fail(backupPath, SafeBackupFailureMessage, validation.Error ?? "Backup archive validation failed.");

                return BackupResult.Ok(
                    backupPath,
                    validation.Manifest!.IncludedFiles,
                    validation.Manifest.ExcludedByDefault,
                    validation.Manifest.Counts);
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("LocalBackupService.CreateBackup", destinationDirectory, ex);
                return BackupResult.Fail(null, SafeBackupFailureMessage, ex.GetType().Name);
            }
        }

        public RestoreResult RestoreBackup(string backupPath, bool confirmOverwrite)
        {
            var existingSettings = File.Exists(_paths.SettingsPath);
            var existingDatabase = File.Exists(_paths.DatabasePath);
            var existingReportsMetadata = File.Exists(_paths.ReportsMetadataPath);

            try
            {
                if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                    return RestoreResult.Fail(SafeRestoreFailureMessage, "Backup file was not found.", BackupDataCounts.Empty, BackupDataCounts.Empty);

                var validation = ValidateBackupArchive(backupPath);
                if (!validation.Success || validation.Manifest is null)
                    return RestoreResult.Fail(SafeRestoreFailureMessage, validation.Error ?? "Backup archive validation failed.", BackupDataCounts.Empty, BackupDataCounts.Empty);

                if (validation.Manifest.BackupFormatVersion > CurrentBackupFormatVersion)
                {
                    return RestoreResult.Fail(
                        SafeRestoreFailureMessage,
                        "Backup format is newer than this app version.",
                        validation.Manifest.Counts,
                        BackupDataCounts.Empty);
                }

                if (validation.Manifest.SettingsVersion > AppSettings.CurrentSettingsVersion)
                {
                    return RestoreResult.Fail(
                        SafeRestoreFailureMessage,
                        "Settings version is newer than this app version.",
                        validation.Manifest.Counts,
                        BackupDataCounts.Empty);
                }

                var wouldOverwrite = existingSettings || existingDatabase || existingReportsMetadata;
                if (wouldOverwrite && !confirmOverwrite)
                {
                    return RestoreResult.OverwriteRequired(
                        validation.Manifest.Counts,
                        new[] { _paths.SettingsPath, _paths.DatabasePath, _paths.ReportsMetadataPath });
                }

                var safetyBackup = CreateRestoreSafetyBackupNoThrow(existingSettings, existingDatabase, existingReportsMetadata);
                var stagingDirectory = CreateStagingDirectory();

                try
                {
                    ExtractArchiveSafely(backupPath, stagingDirectory);
                    RemoveDestinationIfNotInBackup(validation.Manifest, SettingsEntryName, _paths.SettingsPath);
                    RemoveDestinationIfNotInBackup(validation.Manifest, DatabaseEntryName, _paths.DatabasePath);
                    RemoveDestinationIfNotInBackup(validation.Manifest, ReportsMetadataEntryName, _paths.ReportsMetadataPath);
                    RestoreFileIfPresent(stagingDirectory, SettingsEntryName, _paths.SettingsPath);
                    RestoreFileIfPresent(stagingDirectory, DatabaseEntryName, _paths.DatabasePath);
                    RestoreFileIfPresent(stagingDirectory, ReportsMetadataEntryName, _paths.ReportsMetadataPath);

                    var restoredCounts = CountRestoredFiles();
                    if (!restoredCounts.Matches(validation.Manifest.Counts))
                    {
                        RestoreSafetyBackupNoThrow(safetyBackup);
                        return RestoreResult.Fail(
                            SafeRestoreFailureMessage,
                            "Restored data count did not match backup manifest.",
                            validation.Manifest.Counts,
                            restoredCounts,
                            safetyBackup.Created);
                    }

                    return RestoreResult.Ok(validation.Manifest.Counts, restoredCounts, safetyBackup.Created);
                }
                catch (Exception ex)
                {
                    RestoreSafetyBackupNoThrow(safetyBackup);
                    Rc0WriteFailureSink.Report("LocalBackupService.RestoreBackup.Apply", backupPath, ex);
                    return RestoreResult.Fail(
                        SafeRestoreFailureMessage,
                        ex.GetType().Name,
                        validation.Manifest.Counts,
                        CountRestoredFiles(),
                        safetyBackup.Created);
                }
                finally
                {
                    TryDeleteDirectory(stagingDirectory);
                }
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("LocalBackupService.RestoreBackup", backupPath, ex);
                return RestoreResult.Fail(SafeRestoreFailureMessage, ex.GetType().Name, BackupDataCounts.Empty, CountRestoredFiles());
            }
        }

        private static string SafeBackupFailureMessage =>
            "Sikkerhetskopien kunne ikke opprettes akkurat nå. Dataene dine er ikke endret.";

        private static string SafeRestoreFailureMessage =>
            "Noe gikk galt med gjenoppretting, men eksisterende data ser ut til å være trygge.";

        private static void AddIfExists(ZipArchive archive, string? sourcePath, string entryName, List<string> included)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return;

            archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
            included.Add(entryName);
        }

        private static int ReadSettingsVersionNoThrow(string? settingsPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settingsPath) || !File.Exists(settingsPath))
                    return AppSettings.CurrentSettingsVersion;

                using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (doc.RootElement.TryGetProperty(nameof(AppSettings.SettingsVersion), out var version)
                    && version.ValueKind == JsonValueKind.Number
                    && version.TryGetInt32(out var value))
                {
                    return Math.Max(0, value);
                }
            }
            catch
            {
                // Backup should still work if settings are present but partially unreadable.
            }

            return 0;
        }

        private static BackupValidation ValidateBackupArchive(string backupPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(backupPath);
                var manifestEntry = archive.GetEntry(ManifestEntryName);
                if (manifestEntry is null || manifestEntry.Length <= 0)
                    return BackupValidation.Fail("Backup manifest is missing.");

                BackupManifest? manifest;
                using (var stream = manifestEntry.Open())
                {
                    manifest = JsonSerializer.Deserialize<BackupManifest>(stream, JsonOptions);
                }

                if (manifest is null)
                    return BackupValidation.Fail("Backup manifest could not be read.");

                if (!string.Equals(manifest.AppId, "FemVoiceStudio", StringComparison.Ordinal))
                    return BackupValidation.Fail("Backup does not belong to FemVoiceStudio.");

                foreach (var file in manifest.IncludedFiles)
                {
                    var entry = archive.GetEntry(file);
                    if (entry is null || entry.Length <= 0)
                        return BackupValidation.Fail($"Backup entry missing or empty: {file}");
                }

                return BackupValidation.Ok(manifest);
            }
            catch (Exception ex)
            {
                return BackupValidation.Fail(ex.GetType().Name);
            }
        }

        private RestoreSafetyBackup CreateRestoreSafetyBackupNoThrow(
            bool includeSettings,
            bool includeDatabase,
            bool includeReportsMetadata)
        {
            var copied = new List<RestoreBackupCopy>();

            try
            {
                if (!includeSettings && !includeDatabase && !includeReportsMetadata)
                    return new RestoreSafetyBackup(false, copied);

                var folder = Path.Combine(_paths.SafetyBackupDirectory, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(folder);

                CopySafetyFile(_paths.SettingsPath, Path.Combine(folder, "settings.json"), copied, includeSettings);
                CopySafetyFile(_paths.DatabasePath, Path.Combine(folder, "femvoice.db"), copied, includeDatabase);
                CopySafetyFile(_paths.ReportsMetadataPath, Path.Combine(folder, "REPORT_VERIFICATION_STATUS.json"), copied, includeReportsMetadata);

                return new RestoreSafetyBackup(copied.Count > 0, copied);
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("LocalBackupService.CreateRestoreSafetyBackup", _paths.SafetyBackupDirectory, ex);
                return new RestoreSafetyBackup(false, copied);
            }
        }

        private static void CopySafetyFile(string? source, string destination, List<RestoreBackupCopy> copied, bool shouldCopy)
        {
            if (!shouldCopy || string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            copied.Add(new RestoreBackupCopy(source, destination));
        }

        private static string CreateStagingDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "FemVoiceStudioRestore_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void ExtractArchiveSafely(string backupPath, string stagingDirectory)
        {
            using var archive = ZipFile.OpenRead(backupPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;

                var destination = Path.GetFullPath(Path.Combine(stagingDirectory, entry.FullName));
                var root = Path.GetFullPath(stagingDirectory) + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Backup entry path is invalid.");

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
            }
        }

        private static void RestoreFileIfPresent(string stagingDirectory, string entryName, string destinationPath)
        {
            var stagedPath = Path.Combine(stagingDirectory, entryName.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(stagedPath))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(stagedPath, destinationPath, overwrite: true);
        }

        private static void RemoveDestinationIfNotInBackup(BackupManifest manifest, string entryName, string destinationPath)
        {
            if (manifest.IncludedFiles.Contains(entryName) || !File.Exists(destinationPath))
                return;

            File.Delete(destinationPath);
        }

        private BackupDataCounts CountRestoredFiles()
        {
            return new BackupDataCounts(
                SettingsFiles: File.Exists(_paths.SettingsPath) ? 1 : 0,
                DatabaseFiles: File.Exists(_paths.DatabasePath) ? 1 : 0,
                ReportsMetadataFiles: File.Exists(_paths.ReportsMetadataPath) ? 1 : 0);
        }

        private static void RestoreSafetyBackupNoThrow(RestoreSafetyBackup safetyBackup)
        {
            foreach (var copy in safetyBackup.Copies)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(copy.OriginalPath)!);
                    File.Copy(copy.BackupPath, copy.OriginalPath, overwrite: true);
                }
                catch
                {
                    // Do not throw while already handling a failed restore.
                }
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        private sealed record BackupValidation(bool Success, BackupManifest? Manifest, string? Error)
        {
            public static BackupValidation Ok(BackupManifest manifest) => new(true, manifest, null);
            public static BackupValidation Fail(string error) => new(false, null, error);
        }

        private sealed record RestoreSafetyBackup(bool Created, IReadOnlyList<RestoreBackupCopy> Copies);
        private sealed record RestoreBackupCopy(string OriginalPath, string BackupPath);
    }

    public sealed record LocalBackupPaths(
        string SettingsPath,
        string DatabasePath,
        string ReportsMetadataPath,
        string SafetyBackupDirectory);

    public sealed record BackupManifest(
        string AppId,
        int BackupFormatVersion,
        DateTime CreatedAtUtc,
        int SettingsVersion,
        string[] IncludedFiles,
        string[] ExcludedByDefault,
        BackupDataCounts Counts);

    public sealed record BackupDataCounts(
        int SettingsFiles,
        int DatabaseFiles,
        int ReportsMetadataFiles)
    {
        public static BackupDataCounts Empty { get; } = new(0, 0, 0);

        public static BackupDataCounts FromIncludedFiles(IReadOnlyCollection<string> includedFiles) =>
            new(
                includedFiles.Contains("settings/settings.json") ? 1 : 0,
                includedFiles.Contains("data/femvoice.db") ? 1 : 0,
                includedFiles.Contains("reports/REPORT_VERIFICATION_STATUS.json") ? 1 : 0);

        public bool Matches(BackupDataCounts other) =>
            SettingsFiles == other.SettingsFiles
            && DatabaseFiles == other.DatabaseFiles
            && ReportsMetadataFiles == other.ReportsMetadataFiles;
    }

    public sealed record BackupResult(
        bool Success,
        string? BackupPath,
        string Status,
        string SafeUserMessage,
        string[] IncludedFiles,
        string[] ExcludedByDefault,
        BackupDataCounts Counts,
        string[] Errors)
    {
        public static BackupResult Ok(
            string backupPath,
            string[] includedFiles,
            string[] excludedByDefault,
            BackupDataCounts counts) =>
            new(true, backupPath, "PASS", string.Empty, includedFiles, excludedByDefault, counts, Array.Empty<string>());

        public static BackupResult Fail(string? backupPath, string safeUserMessage, string error) =>
            new(false, backupPath, "FAIL", safeUserMessage, Array.Empty<string>(), Array.Empty<string>(), BackupDataCounts.Empty, new[] { error });
    }

    public sealed record RestoreResult(
        bool Success,
        bool RequiresOverwriteConfirmation,
        string Status,
        string SafeUserMessage,
        string[] FilesThatWouldBeOverwritten,
        BackupDataCounts SourceCounts,
        BackupDataCounts RestoredCounts,
        bool SafetyBackupCreated,
        string[] Errors)
    {
        public static RestoreResult Ok(
            BackupDataCounts sourceCounts,
            BackupDataCounts restoredCounts,
            bool safetyBackupCreated) =>
            new(true, false, "PASS", string.Empty, Array.Empty<string>(), sourceCounts, restoredCounts, safetyBackupCreated, Array.Empty<string>());

        public static RestoreResult OverwriteRequired(
            BackupDataCounts sourceCounts,
            string[] filesThatWouldBeOverwritten) =>
            new(false, true, "CONFIRM_OVERWRITE_REQUIRED", "Gjenoppretting vil erstatte eksisterende lokale data og må bekreftes først.", filesThatWouldBeOverwritten, sourceCounts, BackupDataCounts.Empty, false, Array.Empty<string>());

        public static RestoreResult Fail(
            string safeUserMessage,
            string error,
            BackupDataCounts sourceCounts,
            BackupDataCounts restoredCounts,
            bool safetyBackupCreated = false) =>
            new(false, false, "FAIL", safeUserMessage, Array.Empty<string>(), sourceCounts, restoredCounts, safetyBackupCreated, new[] { error });
    }
}
