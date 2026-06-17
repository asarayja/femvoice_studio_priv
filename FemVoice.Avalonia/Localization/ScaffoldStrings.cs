using System;
using System.Collections.Generic;

namespace FemVoice.Avalonia.Localization;

/// <summary>
/// AVALONIA-OWNED localization overlay for the new display-only scaffold strings introduced during the
/// WPF→Avalonia port. This is intentionally SEPARATE from the shared Core <c>Strings.*.resx</c> (which is left
/// untouched — no risk to the Core localization/mojibake baseline tests, no WPF behaviour change). It is NOT a
/// new localization framework: it is a small per-culture lookup table that <see cref="Localized"/> consults
/// before the existing Core resolver/Norwegian-fallback.
///
/// Per the slice decision: REUSE-ONLY translations (no machine translation committed). Today only culture-
/// invariant trusted values are populated (the product name "SmartCoach"); every other scaffold key keeps the
/// Norwegian neutral fallback and is recorded in <see cref="NativeTranslationBacklog"/> as awaiting native
/// translation. Runtime language switching / persistence remain deferred, so only the default culture renders;
/// this establishes the 20-culture STRUCTURE + coverage checks without polluting the repo with unverified text.
/// </summary>
public static class ScaffoldStrings
{
    /// <summary>The 20 cultures supported by the original app (source of truth: WPF SettingsWindow language combo).</summary>
    public static readonly IReadOnlyList<string> Cultures = new[]
    {
        "nb-NO", "en-US", "sv-SE", "da-DK", "fi-FI", "de-DE", "fr-FR", "es-ES", "pt-BR", "it-IT",
        "hr-HR", "nl-NL", "pl-PL", "tr-TR", "uk-UA", "ro-RO", "cs-CZ", "hu-HU", "el-GR", "ar",
    };

    // Trusted reuse only — culture-invariant values that need no native review. The product name is preserved
    // identically across all cultures (FemVoice Studio / SmartCoach / Avalonia are not translated).
    private static readonly Dictionary<string, string> ProductInvariant = new(StringComparer.Ordinal)
    {
        ["SmartCoach_Scaffold_Title"] = "SmartCoach",
    };

    /// <summary>Avalonia scaffold keys that currently fall back to the Norwegian neutral text and AWAIT native
    /// translation for the non-Norwegian cultures (documented; not broken/missing). The coverage smoke treats
    /// these as a documented fallback, not a failure.</summary>
    public static readonly IReadOnlyList<string> NativeTranslationBacklog = new[]
    {
        // Stage-1 local UI-preference labels (Avalonia-only; awaiting native translation like the rest).
        "Settings_LocalPrefs_Title",
        "Settings_LocalPrefs_Note",
        "Settings_LocalPrefs_Save",
        "Settings_LocalPrefs_Reload",
        "Settings_LocalPrefs_ReduceMotion",
        "Settings_LocalPrefs_Saved",
        "Settings_LocalPrefs_SaveFailed",
        "Settings_LocalPrefs_Reloaded",
        "Analysis_Formant",
        "Analysis_FormantDesc",
        "Analysis_FormantSummary",
        "Analysis_Metric_AvgPitch",
        "Analysis_Metric_Resonance",
        "Analysis_Metric_ResonanceVal",
        "Analysis_Metric_Sessions",
        "Analysis_Metric_SessionsVal",
        "Analysis_Metric_Stability",
        "Analysis_Metric_StabilityVal",
        "Analysis_PitchTrend",
        "Analysis_PitchTrendDesc",
        "Analysis_PitchTrendSummary",
        "Analysis_Resonance",
        "Analysis_ResonanceDesc",
        "Analysis_ResonanceSummary",
        "Analysis_ScaffoldNotice",
        "Analysis_ScaffoldTitle",
        "Analysis_Stability",
        "Analysis_StabilityDesc",
        "Analysis_StabilitySummary",
        "Diag_AppDiagnostikk",
        "Diag_AppDiagnostikkDesc",
        "Diag_DataEksport",
        "Diag_DataEksportDesc",
        "Diag_DeferredStatus",
        "Diag_Feilsoking",
        "Diag_FeilsokingDesc",
        "Diag_Forskning",
        "Diag_ForskningDesc",
        "Diag_Gjenoppretting",
        "Diag_GjenopprettingDesc",
        "Diag_SampleStatus",
        "Diag_ScaffoldNotice",
        "Diag_ScaffoldTitle",
        "Diag_Sikkerhetskopi",
        "Diag_SikkerhetskopiDesc",
        "Diag_Stottepakke",
        "Diag_StottepakkeDesc",
        "Diag_SystemStatus",
        "Diag_SystemStatusDesc",
        "Progression_Scaffold_Intro",
        "Progression_Scaffold_LevelDescription",
        "Progression_Scaffold_LevelName",
        "Progression_Scaffold_ParamPitch",
        "Progression_Scaffold_SafetyNote",
        "Progression_Scaffold_ScoreLabel",
        "Reports_Calendar",
        "Reports_CalendarDesc",
        "Reports_Clinician",
        "Reports_ClinicianDesc",
        "Reports_Coach",
        "Reports_CoachDesc",
        "Reports_DeferredStatus",
        "Reports_Exports",
        "Reports_ExportsDesc",
        "Reports_Preview",
        "Reports_PreviewDesc",
        "Reports_ProgressSummary",
        "Reports_ProgressSummaryDesc",
        "Reports_Saksgjennomgang",
        "Reports_SaksgjennomgangDesc",
        "Reports_SampleStatus",
        "Reports_ScaffoldNotice",
        "Reports_ScaffoldTitle",
        "Reports_SessionHistory",
        "Reports_SessionHistoryDesc",
        "Scaffold_ComingSoon",
        "Scaffold_DeferredBadge",
        "Scaffold_Pending",
        "Scaffold_Synthetic",
        "Settings_AboutDesc",
        "Settings_AboutMode",
        "Settings_DeferredStatus",
        "Settings_FirstRun",
        "Settings_General",
        "Settings_GeneralDesc",
        "Settings_Gjenopprett",
        "Settings_LanguageRow",
        "Settings_Microphone",
        "Settings_Scaffold_Audio",
        "Settings_ScaffoldNotice",
        "Settings_Scaffold_PrivacyDiagnostics",
        "Settings_Scaffold_PrivacyResearch",
        "Settings_ScaffoldSafety",
        "Settings_Sikkerhetskopi",
        "Settings_ThemePreference",
        "Settings_TomDatabase",
        "Shell_DeferredFootnote",
        "Shell_MicStatus",
        "Shell_Mode",
        "Shell_Nav_Analysis",
        "Shell_Nav_Dashboard",
        "Shell_Nav_Diagnostics",
        "Shell_Nav_Guide",
        "Shell_Nav_Progresjon",
        "Shell_Nav_Reports",
        "Shell_Nav_Settings",
        "SmartCoach_Scaffold_HealthLabel",
        "SmartCoach_Scaffold_Intro",
        "SmartCoach_Scaffold_Recommendation",
        "SmartCoach_Scaffold_SafetyNote",
        "SmartCoach_Scaffold_SessionsLabel",
        "SmartCoach_Scaffold_StreakLabel",
        "SmartCoach_TodayFocus",
    };

    /// <summary>Keys with a trusted, culture-invariant translation already populated (no native review needed).</summary>
    public static readonly IReadOnlyList<string> TrustedKeys = new[] { "SmartCoach_Scaffold_Title" };

    /// <summary>
    /// Try to resolve a scaffold key for <paramref name="culture"/> from the Avalonia overlay. Only returns
    /// <c>true</c> for trusted, culture-invariant values; everything else defers to the Core resolver / Norwegian
    /// fallback in <see cref="Localized"/>. <paramref name="culture"/> is accepted for future per-culture entries.
    /// </summary>
    public static bool TryGet(string? culture, string key, out string value)
    {
        _ = culture; // reserved for future per-culture native translations
        return ProductInvariant.TryGetValue(key, out value!);
    }
}
