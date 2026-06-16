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
}
