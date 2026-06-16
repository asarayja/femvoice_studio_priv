using System.Text.Json;

namespace FemVoiceStudio.Services
{
    // Extracted verbatim from ThemeManager.cs during the Linux portable-core split so that the
    // settings + localization family (ISettingsService, LocalizationService, DebugSettingsService,
    // SettingsMigrationService, FirstTimeSetupService) can live in the UI-free FemVoice.Core.
    // Behaviour is unchanged: same namespace, same types, same JSON options. The WPF ThemeManager
    // still consumes these via FemVoice.Core. DO NOT change serialization semantics.

    /// <summary>
    /// Theme options available in the application
    /// </summary>
    public enum AppTheme
    {
        System,  // Follow system theme
        Light,   // Always use light theme
        Dark     // Always use dark theme
    }

    /// <summary>
    /// Application settings model for JSON serialization
    /// </summary>
    public class AppSettings
    {
        public const int CurrentSettingsVersion = 2;

        public int SettingsVersion { get; set; } = CurrentSettingsVersion;
        public string Language { get; set; } = "nb";
        public AppTheme Theme { get; set; } = AppTheme.System;
        public bool HearOwnVoice { get; set; } = false;
        public bool FirstTimeSetupCompleted { get; set; } = false;
        public DebugSettings? Debug { get; set; }

        // Hand-edited keys this model doesn't know about must survive the
        // load-modify-save round-trips done by ThemeManager, DebugSettingsService
        // and FirstTimeSetupService.
        [System.Text.Json.Serialization.JsonExtensionData]
        public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>
    /// Debug settings model
    /// </summary>
    public class DebugSettings
    {
        public bool EnablePitchDebug { get; set; } = false;
        public bool EnableAnalyzerDebug { get; set; } = false;
        public bool EnableRc0Diagnostics { get; set; } = false;

        [System.Text.Json.Serialization.JsonExtensionData]
        public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>
    /// Shared serializer options for settings.json. All three settings writers
    /// (ThemeManager, DebugSettingsService, FirstTimeSetupService) must use the same
    /// options so a hand-edited file (e.g. "Theme": "Dark" as a string) never fails
    /// deserialization and gets reset to defaults.
    /// </summary>
    public static class AppSettingsJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            // Håndredigert fil er den dokumenterte måten å skru på debug-flaggene —
            // feil casing skal ikke gjøre et flagg stille false.
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }
}
