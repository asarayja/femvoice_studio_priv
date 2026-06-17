using FemVoiceStudio.Models;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>Read-only Norwegian display formatting for the shared exercise catalog enums/metadata.
/// Pure presentation — does not alter the exercise definitions.</summary>
internal static class ExerciseDisplay
{
    public static string Goal(GoalCategory g) => g switch
    {
        GoalCategory.Pitch => "Tonehøyde",
        GoalCategory.Resonance => "Resonans",
        GoalCategory.Intonation => "Intonasjon",
        GoalCategory.Breathing => "Pust",
        GoalCategory.Combined => "Kombinert",
        _ => g.ToString(),
    };

    public static string Difficulty(DifficultyLevel d) => d switch
    {
        DifficultyLevel.Nybegynner => "Nybegynner",
        DifficultyLevel.Middels => "Middels",
        DifficultyLevel.Avansert => "Avansert",
        _ => d.ToString(),
    };

    public static string Metric(MetricType m) => m switch
    {
        MetricType.Pitch => "Tonehøyde",
        MetricType.PitchVariability => "Tonehøyde-variasjon",
        MetricType.Intonation => "Intonasjon",
        MetricType.Resonance => "Resonans",
        MetricType.Intensity => "Intensitet",
        MetricType.Consistency => "Konsistens",
        MetricType.Duration => "Varighet",
        MetricType.Smoothness => "Jevnhet",
        _ => m.ToString(),
    };

    public static string TargetPitch(double min, double max)
        => (min > 0 && max > 0) ? $"{min:F0}–{max:F0} Hz" : "—";

    // Mirrors the WPF list's GetFrequencyText (VoiceFeminizationExerciseService) so the guide list shows the same
    // compact Norwegian frequency text, not the raw enum name.
    public static string Frequency(FrequencyType f) => f switch
    {
        FrequencyType.Daglig => "Daglig",
        FrequencyType.TreGangerUkentlig => "3x/uke",
        FrequencyType.ToGangerUkentlig => "2x/uke",
        FrequencyType.Ukentlig => "Ukentlig",
        _ => "Daglig",
    };

    /// <summary>Display-only, focus-aware one-line summary (NOT clinical) derived from the exercise goal, so the
    /// page reflects the exercise's actual focus instead of always leading with pitch.</summary>
    public static string FocusSummary(GoalCategory g) => g switch
    {
        GoalCategory.Pitch => "Fokus: stabil tonehøyde i målområdet.",
        GoalCategory.Resonance => "Fokus: lysere resonans og fremre plassering — ikke bare tonehøyde.",
        GoalCategory.Intonation => "Fokus: melodi og setningsmelodi (intonasjon).",
        GoalCategory.Breathing => "Fokus: pust, støtte og jevn, rolig luftstrøm.",
        GoalCategory.Combined => "Fokus: kombinert — tonehøyde, resonans og kontroll.",
        _ => "Fokus: stemmetrening.",
    };

    /// <summary>Whether tonehøyde (pitch) is the PRIMARY focus (Pitch/Combined) — controls whether a pitch
    /// target is shown prominently vs. demoted to a secondary technical detail.</summary>
    public static bool IsPitchPrimary(GoalCategory g) => g is GoalCategory.Pitch or GoalCategory.Combined;
}
