using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public sealed class SettingsMigrationServiceTests
    {
        [Fact]
        public void OldSettings_MigrateWithBackup_AndPreservePreferences()
        {
            var folder = NewTempFolder();
            try
            {
                var path = Path.Combine(folder, "settings.json");
                File.WriteAllText(path, """
                {
                  "Language": "en",
                  "Theme": "Dark",
                  "HearOwnVoice": true,
                  "FirstTimeSetupCompleted": true,
                  "Debug": {
                    "EnablePitchDebug": true,
                    "EnableAnalyzerDebug": false,
                    "EnableRc0Diagnostics": true
                  }
                }
                """);

                var result = SettingsMigrationService.LoadOrRecover(path, "test.old");

                Assert.Equal(0, result.SettingsVersionBefore);
                Assert.Equal(AppSettings.CurrentSettingsVersion, result.SettingsVersionAfter);
                Assert.True(result.MigrationApplied);
                Assert.Equal("MIGRATED", result.MigrationStatus);
                Assert.True(result.SettingsBackupCreated);
                Assert.Empty(result.Errors);
                Assert.Equal("en", result.Settings.Language);
                Assert.Equal(AppTheme.Dark, result.Settings.Theme);
                Assert.True(result.Settings.HearOwnVoice);
                Assert.True(result.Settings.FirstTimeSetupCompleted);
                Assert.True(result.Settings.Debug!.EnablePitchDebug);
                Assert.True(result.Settings.Debug.EnableRc0Diagnostics);
                Assert.Single(Directory.GetFiles(Path.Combine(folder, "settings-backups"), "*.bak"));

                var migratedJson = File.ReadAllText(path);
                Assert.Contains("\"SettingsVersion\": 2", migratedJson);
            }
            finally
            {
                DeleteTempFolder(folder);
            }
        }

        [Fact]
        public void MissingFields_ReceiveSafeDefaults_AndPreserveKnownValues()
        {
            var folder = NewTempFolder();
            try
            {
                var path = Path.Combine(folder, "settings.json");
                File.WriteAllText(path, """
                {
                  "SettingsVersion": 1,
                  "Language": "da-DK"
                }
                """);

                var result = SettingsMigrationService.LoadOrRecover(path, "test.missing");

                Assert.True(result.MigrationApplied);
                Assert.Equal("MIGRATED", result.MigrationStatus);
                Assert.True(result.SettingsBackupCreated);
                Assert.Equal("da-DK", result.Settings.Language);
                Assert.Equal(AppTheme.System, result.Settings.Theme);
                Assert.False(result.Settings.HearOwnVoice);
                Assert.NotNull(result.Settings.Debug);
            }
            finally
            {
                DeleteTempFolder(folder);
            }
        }

        [Fact]
        public void CurrentSettings_LoadWithoutMigration()
        {
            var folder = NewTempFolder();
            try
            {
                var path = Path.Combine(folder, "settings.json");
                var settings = new AppSettings
                {
                    SettingsVersion = AppSettings.CurrentSettingsVersion,
                    Language = "nb",
                    Theme = AppTheme.Light,
                    Debug = new DebugSettings()
                };
                File.WriteAllText(path, JsonSerializer.Serialize(settings, AppSettingsJson.Options));

                var result = SettingsMigrationService.LoadOrRecover(path, "test.current");

                Assert.False(result.MigrationApplied);
                Assert.Equal("OK", result.MigrationStatus);
                Assert.False(result.SettingsBackupCreated);
                Assert.Equal(AppTheme.Light, result.Settings.Theme);
                Assert.False(Directory.Exists(Path.Combine(folder, "settings-backups")));
            }
            finally
            {
                DeleteTempFolder(folder);
            }
        }

        [Fact]
        public void CorruptSettings_RecoverWithBackup_AndSafeError()
        {
            var folder = NewTempFolder();
            try
            {
                var path = Path.Combine(folder, "settings.json");
                File.WriteAllText(path, "{ this is not valid json");

                var result = SettingsMigrationService.LoadOrRecover(path, "test.corrupt");

                Assert.Equal("RECOVERED_FROM_CORRUPT", result.MigrationStatus);
                Assert.True(result.SettingsBackupCreated);
                Assert.Equal(AppSettings.CurrentSettingsVersion, result.SettingsVersionAfter);
                Assert.NotNull(result.Settings);
                Assert.NotNull(result.Settings.Debug);
                Assert.NotEmpty(result.Errors);
                Assert.Contains(result.Errors, e => e.Contains("safe defaults", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(result.Errors, e => e.Contains("Json", StringComparison.OrdinalIgnoreCase));
                Assert.Equal("Noe gikk galt med innstillingene, men dataene dine ser ut til å være trygge.", result.SafeUserMessage);
                Assert.Single(Directory.GetFiles(Path.Combine(folder, "settings-backups"), "*.bak"));
                Assert.Contains("\"SettingsVersion\": 2", File.ReadAllText(path));
            }
            finally
            {
                DeleteTempFolder(folder);
            }
        }

        [Fact]
        public void ExtensionData_SurvivesMigration()
        {
            var folder = NewTempFolder();
            try
            {
                var path = Path.Combine(folder, "settings.json");
                File.WriteAllText(path, """
                {
                  "SettingsVersion": 1,
                  "Language": "nb",
                  "CustomUserKey": "keep-me",
                  "Debug": {
                    "EnablePitchDebug": false,
                    "CustomDebugKey": 42
                  }
                }
                """);

                SettingsMigrationService.LoadOrRecover(path, "test.extension");
                var json = File.ReadAllText(path);

                Assert.Contains("CustomUserKey", json);
                Assert.Contains("keep-me", json);
                Assert.Contains("CustomDebugKey", json);
            }
            finally
            {
                DeleteTempFolder(folder);
            }
        }

        [Fact]
        public void LastResult_ContainsRequiredEvidenceFields()
        {
            var folder = NewTempFolder();
            try
            {
                var path = Path.Combine(folder, "settings.json");
                File.WriteAllText(path, "{}");

                var result = SettingsMigrationService.LoadOrRecover(path, "test.evidence");

                Assert.Same(result, SettingsMigrationService.LastResult);
                Assert.True(result.SettingsVersionBefore >= 0);
                Assert.Equal(AppSettings.CurrentSettingsVersion, result.SettingsVersionAfter);
                Assert.True(result.MigrationApplied);
                Assert.False(string.IsNullOrWhiteSpace(result.MigrationStatus));
                Assert.True(result.SettingsBackupCreated);
                Assert.NotNull(result.Errors);
                Assert.NotNull(result.Warnings);
            }
            finally
            {
                DeleteTempFolder(folder);
            }
        }

        private static string NewTempFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "FemVoiceStudio.Tests", "SettingsMigration", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void DeleteTempFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
            catch
            {
            }
        }
    }
}
