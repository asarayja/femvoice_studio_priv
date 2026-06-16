using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Shared settings.json loader for release-hardening migration and corrupt-file recovery.
    /// It preserves user preferences where possible and never exposes raw exception text as
    /// normal user-facing state.
    /// </summary>
    public static class SettingsMigrationService
    {
        private const string StatusFileName = "SETTINGS_MIGRATION_STATUS.json";
        private static readonly object Sync = new();

        public static SettingsMigrationResult LastResult { get; private set; } =
            SettingsMigrationResult.NotRun();

        public static SettingsMigrationResult LoadOrRecover(string settingsPath, string component)
        {
            lock (Sync)
            {
                var errors = new List<string>();
                var warnings = new List<string>();
                var settingsVersionBefore = -1;
                var settingsVersionAfter = AppSettings.CurrentSettingsVersion;
                var migrationApplied = false;
                var backupCreated = false;
                var status = "OK";
                var settings = new AppSettings();

                try
                {
                    EnsureSettingsDirectory(settingsPath);

                    if (!File.Exists(settingsPath))
                    {
                        status = "CREATED_DEFAULTS";
                        migrationApplied = true;
                        warnings.Add("Settings file was missing; safe defaults were created.");
                        Save(settingsPath, settings, component);
                    }
                    else
                    {
                        var json = File.ReadAllText(settingsPath, System.Text.Encoding.UTF8);
                        settingsVersionBefore = ReadSettingsVersion(json);
                        settings = JsonSerializer.Deserialize<AppSettings>(json, AppSettingsJson.Options)
                            ?? new AppSettings();

                        settings.SettingsVersion = settingsVersionBefore;
                        settingsVersionBefore = settings.SettingsVersion;
                        var missingRequiredSections = MissingRequiredSections(settings);
                        NormalizeDefaults(settings);

                        if (settings.SettingsVersion < AppSettings.CurrentSettingsVersion
                            || missingRequiredSections)
                        {
                            backupCreated = TryCreateBackup(settingsPath, "migration");
                            migrationApplied = true;
                            status = backupCreated ? "MIGRATED" : "MIGRATED_BACKUP_FAILED";
                            if (!backupCreated)
                                warnings.Add("Settings backup could not be created before migration.");

                            settings.SettingsVersion = AppSettings.CurrentSettingsVersion;
                            Save(settingsPath, settings, component);
                        }
                    }
                }
                catch (Exception ex)
                {
                    status = "RECOVERED_FROM_CORRUPT";
                    errors.Add("Settings file could not be read; safe defaults were loaded.");
                    backupCreated = TryCreateBackup(settingsPath, "corrupt");
                    if (!backupCreated)
                        warnings.Add("Settings backup could not be created during recovery.");

                    settings = new AppSettings();
                    settingsVersionBefore = -1;
                    settingsVersionAfter = settings.SettingsVersion;

                    try
                    {
                        Save(settingsPath, settings, component);
                    }
                    catch (Exception saveEx)
                    {
                        status = "RECOVERY_SAVE_FAILED";
                        errors.Add("Recovered settings could not be saved.");
                        Rc0WriteFailureSink.Report($"{component}.RecoverySave", settingsPath, saveEx);
                    }

                    Rc0WriteFailureSink.Report(component, settingsPath, ex);
                }

                settingsVersionAfter = settings.SettingsVersion;
                var result = new SettingsMigrationResult(
                    settings,
                    settingsVersionBefore,
                    settingsVersionAfter,
                    migrationApplied,
                    status,
                    backupCreated,
                    errors.ToArray(),
                    warnings.ToArray(),
                    errors.Count == 0
                        ? string.Empty
                        : "Noe gikk galt med innstillingene, men dataene dine ser ut til å være trygge.");

                LastResult = result;
                WriteStatusNoThrow(result);
                return result;
            }
        }

        public static void Save(string settingsPath, AppSettings settings, string component)
        {
            try
            {
                EnsureSettingsDirectory(settingsPath);
                NormalizeDefaults(settings);
                settings.SettingsVersion = AppSettings.CurrentSettingsVersion;
                var json = JsonSerializer.Serialize(settings, AppSettingsJson.Options);
                File.WriteAllText(settingsPath, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report(component, settingsPath, ex);
                throw;
            }
        }

        private static void NormalizeDefaults(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Language))
                settings.Language = "nb";

            settings.Debug ??= new DebugSettings();
        }

        private static bool MissingRequiredSections(AppSettings settings) => settings.Debug is null;

        private static int ReadSettingsVersion(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(nameof(AppSettings.SettingsVersion), out var version)
                    && version.TryGetInt32(out var parsed))
                {
                    return parsed;
                }
            }
            catch
            {
                // The normal corrupt-settings recovery path handles the parse failure.
            }

            return 0;
        }

        private static bool TryCreateBackup(string settingsPath, string reason)
        {
            try
            {
                if (!File.Exists(settingsPath))
                    return false;

                var directory = Path.GetDirectoryName(settingsPath);
                if (string.IsNullOrWhiteSpace(directory))
                    return false;

                var backupDirectory = Path.Combine(directory, "settings-backups");
                Directory.CreateDirectory(backupDirectory);
                var fileName = $"settings.{reason}.{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.json.bak";
                File.Copy(settingsPath, Path.Combine(backupDirectory, fileName), overwrite: false);
                return true;
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("SettingsMigrationService.Backup", settingsPath, ex);
                return false;
            }
        }

        private static void EnsureSettingsDirectory(string settingsPath)
        {
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }

        private static void WriteStatusNoThrow(SettingsMigrationResult result)
        {
            try
            {
                Directory.CreateDirectory(DiagnosticsNaming.PrimaryRoot);
                var payload = new
                {
                    result.SettingsVersionBefore,
                    result.SettingsVersionAfter,
                    result.MigrationApplied,
                    result.MigrationStatus,
                    result.SettingsBackupCreated,
                    result.Errors,
                    result.Warnings
                };
                File.WriteAllText(
                    Path.Combine(DiagnosticsNaming.PrimaryRoot, StatusFileName),
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                    System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Rc0WriteFailureSink.Report("SettingsMigrationService.WriteStatus", DiagnosticsNaming.PrimaryRoot, ex);
            }
        }
    }

    public sealed record SettingsMigrationResult(
        AppSettings Settings,
        int SettingsVersionBefore,
        int SettingsVersionAfter,
        bool MigrationApplied,
        string MigrationStatus,
        bool SettingsBackupCreated,
        string[] Errors,
        string[] Warnings,
        string SafeUserMessage)
    {
        public static SettingsMigrationResult NotRun() => new(
            new AppSettings(),
            -1,
            AppSettings.CurrentSettingsVersion,
            false,
            "NOT_RUN",
            false,
            Array.Empty<string>(),
            Array.Empty<string>(),
            string.Empty);
    }
}
