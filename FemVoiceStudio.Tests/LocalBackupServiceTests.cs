using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public sealed class LocalBackupServiceTests
    {
        [Fact]
        public void CreateBackup_IncludesLocalPersistence_AndExcludesDiagnosticsByDefault()
        {
            using var temp = new TempBackupFolder();
            var paths = temp.SourcePaths();
            File.WriteAllText(paths.SettingsPath, "{\"SettingsVersion\":2,\"Language\":\"nb\"}");
            File.WriteAllText(paths.DatabasePath, "sqlite bytes");
            File.WriteAllText(paths.ReportsMetadataPath, "{\"ClinicalReportStatus\":\"PASS\"}");
            File.WriteAllText(Path.Combine(temp.SourceRoot, "diagnostics.txt"), "not included");

            var service = new LocalBackupService(paths);
            var result = service.CreateBackup(temp.BackupRoot, new DateTime(2026, 6, 12, 10, 0, 0));

            Assert.True(result.Success);
            Assert.NotNull(result.BackupPath);
            Assert.Equal("PASS", result.Status);
            Assert.DoesNotContain(Environment.UserName, Path.GetFileName(result.BackupPath), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("settings/settings.json", result.IncludedFiles);
            Assert.Contains("data/femvoice.db", result.IncludedFiles);
            Assert.Contains("reports/REPORT_VERIFICATION_STATUS.json", result.IncludedFiles);
            Assert.Contains("diagnostics folders", result.ExcludedByDefault);
            Assert.Equal(new BackupDataCounts(1, 1, 1), result.Counts);

            using var archive = ZipFile.OpenRead(result.BackupPath!);
            Assert.NotNull(archive.GetEntry("manifest.json"));
            Assert.Null(archive.GetEntry("diagnostics.txt"));
        }

        [Fact]
        public void RestoreBackup_IntoCleanInstall_RestoresFiles_AndCountsMatch()
        {
            using var temp = new TempBackupFolder();
            var source = temp.SourcePaths();
            File.WriteAllText(source.SettingsPath, "{\"SettingsVersion\":2,\"Theme\":\"Dark\"}");
            File.WriteAllText(source.DatabasePath, "source database");
            File.WriteAllText(source.ReportsMetadataPath, "{\"VerifiedReportCount\":4}");

            var backup = new LocalBackupService(source).CreateBackup(temp.BackupRoot);
            Assert.True(backup.Success);

            var target = temp.TargetPaths();
            var restore = new LocalBackupService(target).RestoreBackup(backup.BackupPath!, confirmOverwrite: false);

            Assert.True(restore.Success);
            Assert.False(restore.RequiresOverwriteConfirmation);
            Assert.Equal("PASS", restore.Status);
            Assert.Equal(backup.Counts, restore.SourceCounts);
            Assert.True(restore.SourceCounts.Matches(restore.RestoredCounts));
            Assert.Equal("source database", File.ReadAllText(target.DatabasePath));
            Assert.Contains("\"Theme\":\"Dark\"", File.ReadAllText(target.SettingsPath));
        }

        [Fact]
        public void RestoreBackup_OverExistingData_RequiresExplicitConfirmation()
        {
            using var temp = new TempBackupFolder();
            var source = temp.SourcePaths();
            File.WriteAllText(source.SettingsPath, "{\"SettingsVersion\":2,\"Language\":\"en\"}");
            File.WriteAllText(source.DatabasePath, "new database");
            var backup = new LocalBackupService(source).CreateBackup(temp.BackupRoot);
            Assert.True(backup.Success);

            var target = temp.TargetPaths();
            File.WriteAllText(target.SettingsPath, "{\"SettingsVersion\":2,\"Language\":\"nb\"}");
            File.WriteAllText(target.DatabasePath, "existing database");

            var restore = new LocalBackupService(target).RestoreBackup(backup.BackupPath!, confirmOverwrite: false);

            Assert.False(restore.Success);
            Assert.True(restore.RequiresOverwriteConfirmation);
            Assert.Equal("CONFIRM_OVERWRITE_REQUIRED", restore.Status);
            Assert.Contains(target.SettingsPath, restore.FilesThatWouldBeOverwritten);
            Assert.Equal("existing database", File.ReadAllText(target.DatabasePath));
            Assert.Contains("\"Language\":\"nb\"", File.ReadAllText(target.SettingsPath));
        }

        [Fact]
        public void RestoreBackup_WithConfirmation_OverwritesAndCreatesSafetyBackup()
        {
            using var temp = new TempBackupFolder();
            var source = temp.SourcePaths();
            File.WriteAllText(source.SettingsPath, "{\"SettingsVersion\":2,\"Language\":\"en\"}");
            File.WriteAllText(source.DatabasePath, "new database");
            var backup = new LocalBackupService(source).CreateBackup(temp.BackupRoot);
            Assert.True(backup.Success);

            var target = temp.TargetPaths();
            File.WriteAllText(target.SettingsPath, "{\"SettingsVersion\":2,\"Language\":\"nb\"}");
            File.WriteAllText(target.DatabasePath, "existing database");
            File.WriteAllText(target.ReportsMetadataPath, "old report metadata");

            var restore = new LocalBackupService(target).RestoreBackup(backup.BackupPath!, confirmOverwrite: true);

            Assert.True(restore.Success);
            Assert.True(restore.SafetyBackupCreated);
            Assert.Equal("new database", File.ReadAllText(target.DatabasePath));
            Assert.Contains("\"Language\":\"en\"", File.ReadAllText(target.SettingsPath));
            Assert.False(File.Exists(target.ReportsMetadataPath));
            Assert.True(restore.SourceCounts.Matches(restore.RestoredCounts));
            Assert.NotEmpty(Directory.GetFiles(target.SafetyBackupDirectory, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public void RestoreBackup_WithNewerVersion_FailsSafelyWithoutChangingExistingData()
        {
            using var temp = new TempBackupFolder();
            var source = temp.SourcePaths();
            File.WriteAllText(source.SettingsPath, "{\"SettingsVersion\":2}");
            File.WriteAllText(source.DatabasePath, "new database");
            var backup = new LocalBackupService(source).CreateBackup(temp.BackupRoot);
            Assert.True(backup.Success);
            RewriteManifest(backup.BackupPath!, backupFormatVersion: 999, settingsVersion: 2);

            var target = temp.TargetPaths();
            File.WriteAllText(target.DatabasePath, "existing database");

            var restore = new LocalBackupService(target).RestoreBackup(backup.BackupPath!, confirmOverwrite: true);

            Assert.False(restore.Success);
            Assert.Equal("FAIL", restore.Status);
            Assert.Contains("Backup format is newer", restore.Errors[0]);
            Assert.Equal("existing database", File.ReadAllText(target.DatabasePath));
        }

        [Fact]
        public void RestoreBackup_WithInvalidArchive_FailsSafelyWithoutCorruptingExistingData()
        {
            using var temp = new TempBackupFolder();
            var target = temp.TargetPaths();
            File.WriteAllText(target.SettingsPath, "{\"SettingsVersion\":2,\"Language\":\"nb\"}");
            File.WriteAllText(target.DatabasePath, "existing database");
            var badArchive = Path.Combine(temp.BackupRoot, "FemVoiceStudio_Backup_invalid.zip");
            File.WriteAllText(badArchive, "not a zip");

            var restore = new LocalBackupService(target).RestoreBackup(badArchive, confirmOverwrite: true);

            Assert.False(restore.Success);
            Assert.Equal("FAIL", restore.Status);
            Assert.Equal("existing database", File.ReadAllText(target.DatabasePath));
            Assert.Contains("\"Language\":\"nb\"", File.ReadAllText(target.SettingsPath));
            Assert.Equal("Noe gikk galt med gjenoppretting, men eksisterende data ser ut til å være trygge.", restore.SafeUserMessage);
        }

        private static void RewriteManifest(string backupPath, int backupFormatVersion, int settingsVersion)
        {
            using var archive = ZipFile.Open(backupPath, ZipArchiveMode.Update);
            var oldEntry = archive.GetEntry("manifest.json");
            oldEntry?.Delete();

            var manifest = new BackupManifest(
                AppId: "FemVoiceStudio",
                BackupFormatVersion: backupFormatVersion,
                CreatedAtUtc: DateTime.UtcNow,
                SettingsVersion: settingsVersion,
                IncludedFiles: new[] { "settings/settings.json", "data/femvoice.db" },
                ExcludedByDefault: new[] { "research exports", "diagnostics folders", "raw support packages" },
                Counts: new BackupDataCounts(1, 1, 0));

            var entry = archive.CreateEntry("manifest.json");
            using var stream = entry.Open();
            JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions { WriteIndented = true });
        }

        private sealed class TempBackupFolder : IDisposable
        {
            public TempBackupFolder()
            {
                Root = Path.Combine(Path.GetTempPath(), "FemVoiceBackupTests_" + Guid.NewGuid().ToString("N"));
                SourceRoot = Path.Combine(Root, "source");
                TargetRoot = Path.Combine(Root, "target");
                BackupRoot = Path.Combine(Root, "backups");
                Directory.CreateDirectory(SourceRoot);
                Directory.CreateDirectory(TargetRoot);
                Directory.CreateDirectory(BackupRoot);
            }

            public string Root { get; }
            public string SourceRoot { get; }
            public string TargetRoot { get; }
            public string BackupRoot { get; }

            public LocalBackupPaths SourcePaths() => CreatePaths(SourceRoot);
            public LocalBackupPaths TargetPaths() => CreatePaths(TargetRoot);

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root))
                        Directory.Delete(Root, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup in tests.
                }
            }

            private static LocalBackupPaths CreatePaths(string root)
            {
                var settings = Path.Combine(root, "settings.json");
                var database = Path.Combine(root, "femvoice.db");
                var reports = Path.Combine(root, "REPORT_VERIFICATION_STATUS.json");
                var safety = Path.Combine(root, "restore-safety-backups");
                Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
                return new LocalBackupPaths(settings, database, reports, safety);
            }
        }
    }
}
