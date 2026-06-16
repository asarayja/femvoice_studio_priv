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
}
