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

    /// <summary>Language preference as a culture code. One of the 20 supported cultures. Default: en-US — a fresh
    /// install (and the first-run onboarding) starts in ENGLISH so international users understand it; they pick their
    /// own language in onboarding / Settings, which is then saved and applied.</summary>
    public string Language { get; set; } = "en-US";

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

    /// <summary>Preferred input-device id (backend-specific: the waveIn index string on Windows). Null/empty = the
    /// system-default input. Read by the capture pipeline (dashboard/exercise) to route to the chosen microphone.</summary>
    public string? MicDeviceId { get; set; }

    /// <summary>"Hear own voice" preference (real-time monitoring). Persisted like WPF's flag; live playback is honored
    /// only where an audio-output path exists.</summary>
    public bool HearOwnVoice { get; set; }

    /// <summary>Preferred voice-goal FOCUS token (balanced / pitch / resonance / intonation). Local preference. Default: balanced.</summary>
    public string VoiceGoalFocus { get; set; } = "balanced";

    /// <summary>Accessibility: stress-sensitive mode (gentler feedback). Persisted local preference. Default: off.</summary>
    public bool StressSensitiveMode { get; set; }

    /// <summary>Accessibility: reduced visual feedback (calmer live visuals). Persisted local preference. Default: off.</summary>
    public bool ReducedVisualFeedback { get; set; }

    /// <summary>Privacy: consent to local diagnostics. Persisted local preference. Default: off (opt-in).</summary>
    public bool DiagnosticsConsent { get; set; }

    /// <summary>Privacy: consent to anonymized research sharing. Persisted local preference. Default: off (opt-in).</summary>
    public bool ResearchSharingConsent { get; set; }

    /// <summary>Per-user resonance-brightness baseline: the spectral centroid (Hz) of the user's RELAXED voice, measured
    /// once during mic calibration. The <c>VoiceBrightnessMeter</c> scales the live resonance readout relative to this so
    /// the meter is robust to the microphone (absolute centroid Hz varies by hundreds of Hz across mics). 0 = not yet
    /// calibrated → the meter uses fixed provisional anchors. Persisted local preference.</summary>
    public double ResonanceBaselineCentroidHz { get; set; }

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
            lang = "en-US";   // default language is English (fresh install / first-run onboarding)
        return new UiPreferences
        {
            Theme = Enum.IsDefined(typeof(ThemePreference), Theme) ? Theme : ThemePreference.System,
            Language = lang,
            ReduceMotion = ReduceMotion,
            FirstTimeSetupCompleted = FirstTimeSetupCompleted,
            VoiceGoalStyle = string.IsNullOrWhiteSpace(VoiceGoalStyle) ? "soft_feminine" : VoiceGoalStyle,
            TrainingFrequency = TrainingFrequency is >= 2 and <= 5 ? TrainingFrequency : 3,
            MicDeviceId = string.IsNullOrWhiteSpace(MicDeviceId) ? null : MicDeviceId,
            HearOwnVoice = HearOwnVoice,
            VoiceGoalFocus = string.IsNullOrWhiteSpace(VoiceGoalFocus) ? "balanced" : VoiceGoalFocus,
            StressSensitiveMode = StressSensitiveMode,
            ReducedVisualFeedback = ReducedVisualFeedback,
            DiagnosticsConsent = DiagnosticsConsent,
            ResearchSharingConsent = ResearchSharingConsent,
            // Guard against absurd/corrupt values; a plausible voiced-speech centroid is a few hundred–few thousand Hz.
            ResonanceBaselineCentroidHz = ResonanceBaselineCentroidHz is >= 100 and <= 4000 ? ResonanceBaselineCentroidHz : 0,
            Version = Version <= 0 ? 1 : Version,
        };
    }
}
