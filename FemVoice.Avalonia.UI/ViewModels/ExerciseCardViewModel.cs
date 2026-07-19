using System.Linq;
using FemVoiceStudio.Services;   // EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>Read-only summary of one catalog exercise for the Exercise Guide list.</summary>
public sealed class ExerciseCardViewModel
{
    public ExerciseCardViewModel(EnhancedExercise exercise, int sessionCount = 0)
    {
        Exercise = exercise;
        SessionCount = sessionCount;
    }

    public EnhancedExercise Exercise { get; }

    /// <summary>Real count of completed sessions for THIS exercise (from the DB; 0 when none / no DB).</summary>
    public int SessionCount { get; }

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
    // WPF parity: the list shows a per-exercise completed-session count. Real value read from the DB by the guide
    // (sessions saved by this exercise's runtime), passed in via the ctor. 0 when the exercise hasn't been done.
    public string SessionCountText => $"{SessionCount} økter";
}
