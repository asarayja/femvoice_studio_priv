using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Services;   // EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Read-only detail of one catalog exercise. All fields come from the shared catalog
/// (EnhancedExercise); nothing is computed clinically here. "Start" is a documented placeholder —
/// the real exercise runtime workflow (live-state stream, safety gates, progression) is not wired
/// in this slice. See docs/AVALONIA_EXERCISE_GUIDE_PLACEHOLDERS.md.
/// </summary>
public partial class ExerciseDetailViewModel : ObservableObject
{
    private readonly Action _back;
    private readonly Action _startExercise;

    public ExerciseDetailViewModel(EnhancedExercise exercise, Action back, Action startExercise)
    {
        _back = back;
        _startExercise = startExercise;
        Exercise = exercise;
        Steps = exercise.Steps ?? new List<string>();
        Metrics = exercise.Metrics?.Select(ExerciseDisplay.Metric).ToList() ?? new List<string>();
    }

    public EnhancedExercise Exercise { get; }

    public string Title => Exercise.Name;
    public string Category => string.IsNullOrWhiteSpace(Exercise.Category) ? "Øvelse" : Exercise.Category;
    public string Purpose => Exercise.Description;
    public string Rationale => Exercise.ScientificRationale;
    public string DifficultyText => ExerciseDisplay.Difficulty(Exercise.Difficulty);
    public string GoalText => ExerciseDisplay.Goal(Exercise.Goal);
    public string FrequencyText => Exercise.Frequency.ToString();
    public string DurationText => $"{Exercise.DurationMinutes} min";
    public string TargetPitchText => ExerciseDisplay.TargetPitch(Exercise.TargetPitchMin, Exercise.TargetPitchMax);
    public IReadOnlyList<string> Steps { get; }
    public IReadOnlyList<string> Metrics { get; }
    public string MetricsText => Metrics.Count > 0 ? string.Join(", ", Metrics) : "—";

    // Safety/health note: the EnhancedExercise catalog has no per-exercise safety field, so this is a
    // general, non-clinical reminder (placeholder). It does NOT represent a Voice-Health gate decision.
    public string SafetyNote =>
        "Øv uten press: stopp ved ubehag, slapp av i hals/skuldre, og ta pauser. " +
        "Helse og sikkerhet går alltid foran tonehøyde.";

    [ObservableProperty] private string _startStatus = "";

    [RelayCommand]
    private void Start() => _startExercise();   // navigates to the Exercise Runtime view

    [RelayCommand]
    private void Back() => _back();
}
