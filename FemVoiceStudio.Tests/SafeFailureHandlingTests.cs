using System;
using System.IO;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public sealed class SafeFailureHandlingTests
    {
        [Theory]
        [InlineData(SafeFailureKind.General)]
        [InlineData(SafeFailureKind.MicrophoneUnavailable)]
        [InlineData(SafeFailureKind.ReportExport)]
        [InlineData(SafeFailureKind.DiagnosticsExport)]
        [InlineData(SafeFailureKind.EmptyAnalytics)]
        [InlineData(SafeFailureKind.PersistenceReadback)]
        [InlineData(SafeFailureKind.BackupRestore)]
        [InlineData(SafeFailureKind.SettingsRecovery)]
        public void SafeFailureMessages_DoNotExposeRawExceptionText(SafeFailureKind kind)
        {
            var message = SafeFailureMessages.For(kind);

            Assert.False(string.IsNullOrWhiteSpace(message));
            Assert.False(SafeFailureMessages.LooksLikeRawExceptionText(message));
        }

        [Fact]
        public void SafeFailureMessages_DetectRawExceptionText()
        {
            Assert.True(SafeFailureMessages.LooksLikeRawExceptionText(
                "System.InvalidOperationException: driver unavailable at C:\\secret\\file.cs"));
            Assert.True(SafeFailureMessages.LooksLikeRawExceptionText(
                "StackTrace: at FemVoiceStudio.Service.Method()"));
            Assert.False(SafeFailureMessages.LooksLikeRawExceptionText(
                "Mikrofonen er ikke tilgjengelig akkurat nå."));
        }

        [Fact]
        public void BackupCreation_RecreatesMissingDestinationFolder()
        {
            using var temp = new TempFolder();
            var source = temp.Paths("source");
            File.WriteAllText(source.SettingsPath, "{\"SettingsVersion\":2}");
            File.WriteAllText(source.DatabasePath, "database");
            var missingDestination = Path.Combine(temp.Root, "missing", "nested", "backups");

            var result = new LocalBackupService(source).CreateBackup(missingDestination);

            Assert.True(result.Success);
            Assert.True(Directory.Exists(missingDestination));
            Assert.True(File.Exists(result.BackupPath));
        }

        [Fact]
        public void BackupRestoreFailure_UsesSafeUserMessage_AndKeepsExistingData()
        {
            using var temp = new TempFolder();
            var target = temp.Paths("target");
            File.WriteAllText(target.SettingsPath, "{\"SettingsVersion\":2,\"Language\":\"nb\"}");
            File.WriteAllText(target.DatabasePath, "existing database");
            var badArchive = Path.Combine(temp.Root, "bad.zip");
            File.WriteAllText(badArchive, "not a zip");

            var result = new LocalBackupService(target).RestoreBackup(badArchive, confirmOverwrite: true);

            Assert.False(result.Success);
            Assert.Equal("FAIL", result.Status);
            Assert.False(SafeFailureMessages.LooksLikeRawExceptionText(result.SafeUserMessage));
            Assert.Equal("existing database", File.ReadAllText(target.DatabasePath));
            Assert.Contains("\"Language\":\"nb\"", File.ReadAllText(target.SettingsPath));
        }

        private sealed class TempFolder : IDisposable
        {
            public TempFolder()
            {
                Root = Path.Combine(Path.GetTempPath(), "FemVoiceSafeFailureTests_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            public string Root { get; }

            public LocalBackupPaths Paths(string name)
            {
                var folder = Path.Combine(Root, name);
                Directory.CreateDirectory(folder);
                return new LocalBackupPaths(
                    Path.Combine(folder, "settings.json"),
                    Path.Combine(folder, "femvoice.db"),
                    Path.Combine(folder, "REPORT_VERIFICATION_STATUS.json"),
                    Path.Combine(folder, "restore-safety-backups"));
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root))
                        Directory.Delete(Root, recursive: true);
                }
                catch
                {
                    // Best-effort test cleanup.
                }
            }
        }
    }
}
