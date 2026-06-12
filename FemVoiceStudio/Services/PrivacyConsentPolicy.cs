using System;
using System.Collections.Generic;
using System.IO;

namespace FemVoiceStudio.Services
{
    public sealed record PrivacyReadinessSnapshot(
        bool CloudUploadEnabledByDefault,
        bool HiddenTelemetryEnabled,
        bool DiagnosticsExportRequiresUserAction,
        bool ResearchExportAnonymizedByDefault,
        bool ProfessionalNotesExcludedFromResearchByDefault,
        bool ProfessionalNotesExcludedFromSupportPackageByDefault,
        string LocalDataFolder,
        string SettingsPath,
        string DiagnosticsFolder,
        string ReportsExportBehavior,
        string DiagnosticsConsentMessage,
        string ResearchExportWarning,
        string ProfessionalNotesPrivacyWarning);

    public static class PrivacyConsentPolicy
    {
        public static PrivacyReadinessSnapshot Snapshot() => new(
            CloudUploadEnabledByDefault: false,
            HiddenTelemetryEnabled: false,
            DiagnosticsExportRequiresUserAction: true,
            ResearchExportAnonymizedByDefault: true,
            ProfessionalNotesExcludedFromResearchByDefault: true,
            ProfessionalNotesExcludedFromSupportPackageByDefault: true,
            LocalDataFolder: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio"),
            SettingsPath: ThemeManager.SettingsPath,
            DiagnosticsFolder: DiagnosticsNaming.PrimaryRoot,
            ReportsExportBehavior: "Reports are saved only to the path selected by the user.",
            DiagnosticsConsentMessage: LocalizationService.Instance.GetString("Privacy_DiagnosticsConsent"),
            ResearchExportWarning: LocalizationService.Instance.GetString("Privacy_ResearchWarning"),
            ProfessionalNotesPrivacyWarning: LocalizationService.Instance.GetString("Privacy_ProfessionalNotesWarning"));

        public static IReadOnlyDictionary<string, object> BuildSettingsSummary(
            AppSettings settings,
            bool includeDebugFlags = false)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var summary = new Dictionary<string, object>
            {
                [nameof(AppSettings.SettingsVersion)] = settings.SettingsVersion,
                [nameof(AppSettings.Language)] = settings.Language,
                [nameof(AppSettings.Theme)] = settings.Theme.ToString(),
                [nameof(AppSettings.HearOwnVoice)] = settings.HearOwnVoice,
                [nameof(AppSettings.FirstTimeSetupCompleted)] = settings.FirstTimeSetupCompleted
            };

            if (includeDebugFlags && settings.Debug is not null)
            {
                summary["Debug.EnablePitchDebug"] = settings.Debug.EnablePitchDebug;
                summary["Debug.EnableAnalyzerDebug"] = settings.Debug.EnableAnalyzerDebug;
                summary["Debug.EnableRc0Diagnostics"] = settings.Debug.EnableRc0Diagnostics;
            }

            return summary;
        }

        public static bool IsSensitiveSettingsKey(string key) =>
            key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("password", StringComparison.OrdinalIgnoreCase)
            || key.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || key.Contains("api_key", StringComparison.OrdinalIgnoreCase);
    }
}
