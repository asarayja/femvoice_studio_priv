using System;
using System.Linq;

namespace FemVoice.Avalonia.Preferences;

/// <summary>The theme PREFERENCE the user has chosen. Display-only in Stage 1 — it is persisted but NOT applied
/// to the running app (runtime activation is a future, separately-approved Stage 2).</summary>
public enum ThemePreference { System, Light, Dark }

/// <summary>
/// HARMLESS, Avalonia-local UI preferences (Stage 1). Exactly three display-only preferences: theme choice,
/// language choice, and a single accessibility/display toggle (reduce-motion). These are persisted to an
/// Avalonia-owned app-data JSON file and are NOT applied at runtime in this stage — no theme switch, no culture
/// change, no audio/database/clinical/WPF interaction. This type intentionally carries no behaviour.
/// </summary>
public sealed class UiPreferences
{
    /// <summary>Theme preference (default: follow the system).</summary>
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>Language preference as a culture code (default: nb-NO). One of the 20 supported cultures.</summary>
    public string Language { get; set; } = "nb-NO";

    /// <summary>Accessibility/display preference: reduce motion/animation (default: off). Display-only.</summary>
    public bool ReduceMotion { get; set; }

    /// <summary>Whether the Avalonia first-time onboarding has been completed (or skipped). Default: false, so a
    /// fresh install shows onboarding once. Persisted locally; mirrors the WPF <c>FirstTimeSetupCompleted</c> flag
    /// but in the Avalonia-owned prefs file — no WPF settings/DB/clinical interaction.</summary>
    public bool FirstTimeSetupCompleted { get; set; }

    /// <summary>Preferred voice-goal STYLE token (soft_feminine / bright_neutral / androgynous / custom) captured in
    /// onboarding. Harmless local preference — NOT the clinical difficulty. Default: soft_feminine.</summary>
    public string VoiceGoalStyle { get; set; } = "soft_feminine";

    /// <summary>Preferred training days per week (2–5) captured in onboarding. Local preference only. Default: 3.</summary>
    public int TrainingFrequency { get; set; } = 3;

    /// <summary>Schema version for forward-compatible parsing of the local file.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Safe defaults, returned whenever the file is missing/empty/invalid.</summary>
    public static UiPreferences Defaults() => new();

    /// <summary>Return a sanitized copy: an unknown/blank language falls back to the default; everything else is
    /// already constrained by its type. Never throws.</summary>
    public UiPreferences Normalized()
    {
        var lang = Language;
        if (string.IsNullOrWhiteSpace(lang)
            || !FemVoice.Avalonia.Localization.ScaffoldStrings.Cultures.Contains(lang, StringComparer.OrdinalIgnoreCase))
            lang = "nb-NO";
        return new UiPreferences
        {
            Theme = Enum.IsDefined(typeof(ThemePreference), Theme) ? Theme : ThemePreference.System,
            Language = lang,
            ReduceMotion = ReduceMotion,
            FirstTimeSetupCompleted = FirstTimeSetupCompleted,
            VoiceGoalStyle = string.IsNullOrWhiteSpace(VoiceGoalStyle) ? "soft_feminine" : VoiceGoalStyle,
            TrainingFrequency = TrainingFrequency is >= 2 and <= 5 ? TrainingFrequency : 3,
            Version = Version <= 0 ? 1 : Version,
        };
    }
}
