using System.Linq;
using FemVoiceStudio.Services;   // EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>Read-only summary of one catalog exercise for the Exercise Guide list.</summary>
public sealed class ExerciseCardViewModel
{
    public ExerciseCardViewModel(EnhancedExercise exercise)
    {
        Exercise = exercise;
    }

    public EnhancedExercise Exercise { get; }

    public int Id => Exercise.Id;
    public string Name => Exercise.Name;
    public string Category => string.IsNullOrWhiteSpace(Exercise.Category) ? "Øvelse" : Exercise.Category;
    public string ShortDescription => Exercise.Description;
    public string DifficultyText => ExerciseDisplay.Difficulty(Exercise.Difficulty);
    public string GoalText => ExerciseDisplay.Goal(Exercise.Goal);
    public string TargetPitchText => ExerciseDisplay.TargetPitch(Exercise.TargetPitchMin, Exercise.TargetPitchMax);
    public string FocusText => Exercise.Metrics.Count > 0
        ? string.Join(", ", Exercise.Metrics.Select(ExerciseDisplay.Metric))
        : "—";
    public string DurationText => $"{Exercise.DurationMinutes} min";
    public string FrequencyText => ExerciseDisplay.Frequency(Exercise.Frequency);
    // WPF parity: the list shows a per-exercise completed-session count ("N økter"). This Avalonia preview has NO
    // session persistence/analytics, so the truthful display-only value is 0 for every exercise (clearly labelled
    // as a display-only preview by the list's progress note). No DB/analytics read, no invented numbers.
    public string SessionCountText => "0 økter";
}
