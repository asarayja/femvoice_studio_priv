using FemVoiceStudio.Models;
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>Read-only display formatting for the shared exercise catalog enums/metadata. Localized (reuses the
/// shared WPF Goal_/Difficulty_/Frequency_/Metrics_ keys where they exist). Pure presentation — does not alter
/// the exercise definitions.</summary>
internal static class ExerciseDisplay
{
    public static string Goal(GoalCategory g) => g switch
    {
        GoalCategory.Pitch => Localized.Get("Goal_Pitch", "Tonehøyde"),
        GoalCategory.Resonance => Localized.Get("Goal_Resonance", "Resonans"),
        GoalCategory.Intonation => Localized.Get("Goal_Intonation", "Intonasjon"),
        GoalCategory.Breathing => Localized.Get("Goal_Breathing", "Pust"),
        GoalCategory.Combined => Localized.Get("Goal_Combined", "Kombinert"),
        _ => g.ToString(),
    };

    public static string Difficulty(DifficultyLevel d) => d switch
    {
        DifficultyLevel.Nybegynner => Localized.Get("Difficulty_Beginner", "Nybegynner"),
        DifficultyLevel.Middels => Localized.Get("Difficulty_Intermediate", "Middels"),
        DifficultyLevel.Avansert => Localized.Get("Difficulty_Advanced", "Avansert"),
        _ => d.ToString(),
    };

    public static string Metric(MetricType m) => m switch
    {
        MetricType.Pitch => Localized.Get("Metrics_Pitch", "Tonehøyde"),
        MetricType.PitchVariability => Localized.Get("Metric_PitchVariability", "Tonehøyde-variasjon"),
        MetricType.Intonation => Localized.Get("Metrics_Intonation", "Intonasjon"),
        MetricType.Resonance => Localized.Get("Metrics_Resonance", "Resonans"),
        MetricType.Intensity => Localized.Get("Metric_Intensity", "Intensitet"),
        MetricType.Consistency => Localized.Get("Dimension_Consistency", "Konsistens"),
        MetricType.Duration => Localized.Get("ExRun_Lbl_Duration", "Varighet"),
        MetricType.Smoothness => Localized.Get("Metric_Smoothness", "Jevnhet"),
        _ => m.ToString(),
    };

    public static string TargetPitch(double min, double max)
        => (min > 0 && max > 0) ? $"{min:F0}–{max:F0} Hz" : "—";

    // Mirrors the WPF list's GetFrequencyText (VoiceFeminizationExerciseService), reusing the shared Frequency_ keys.
    public static string Frequency(FrequencyType f) => f switch
    {
        FrequencyType.Daglig => Localized.Get("Frequency_Daily", "Daglig"),
        FrequencyType.TreGangerUkentlig => Localized.Get("Frequency_3xWeek", "3x/uke"),
        FrequencyType.ToGangerUkentlig => Localized.Get("Frequency_2xWeek", "2x/uke"),
        FrequencyType.Ukentlig => Localized.Get("Frequency_Weekly", "Ukentlig"),
        FrequencyType.FlereGangerDaglig => Localized.Get("Frequency_MultipleDaily", "3–5× daglig"),
        _ => Localized.Get("Frequency_Daily", "Daglig"),
    };

    /// <summary>Display-only, focus-aware one-line summary (NOT clinical) derived from the exercise goal, so the
    /// page reflects the exercise's actual focus instead of always leading with pitch.</summary>
    public static string FocusSummary(GoalCategory g) => g switch
    {
        GoalCategory.Pitch => Localized.Get("ExDisp_Focus_Pitch", "Fokus: stabil tonehøyde i målområdet."),
        GoalCategory.Resonance => Localized.Get("ExDisp_Focus_Resonance", "Fokus: lysere resonans og fremre plassering — ikke bare tonehøyde."),
        GoalCategory.Intonation => Localized.Get("ExDisp_Focus_Intonation", "Fokus: melodi og setningsmelodi (intonasjon)."),
        GoalCategory.Breathing => Localized.Get("ExDisp_Focus_Breathing", "Fokus: pust, støtte og jevn, rolig luftstrøm."),
        GoalCategory.Combined => Localized.Get("ExDisp_Focus_Combined", "Fokus: kombinert — tonehøyde, resonans og kontroll."),
        _ => Localized.Get("ExDisp_Focus_Default", "Fokus: stemmetrening."),
    };

    /// <summary>Whether tonehøyde (pitch) is the PRIMARY focus (Pitch/Combined) — controls whether a pitch
    /// target is shown prominently vs. demoted to a secondary technical detail.</summary>
    public static bool IsPitchPrimary(GoalCategory g) => g is GoalCategory.Pitch or GoalCategory.Combined;
}
