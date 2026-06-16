using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio;                 // PitchDetectionService
using FemVoiceStudio.Audio.Abstractions;    // SyntheticAudioCaptureService, IAudioCaptureService
using FemVoiceStudio.Core.Platform;         // IUiDispatcher
using FemVoiceStudio.Models;                // PitchAnalysisResult
using FemVoiceStudio.Services;              // PitchTraceStabilizer, LiveMetricsService, EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Safe Avalonia exercise-runtime scaffold. Drives the SHARED, UI-free DSP services
/// (PitchDetectionService + PitchTraceStabilizer + LiveMetricsService) from a dedicated SYNTHETIC
/// capture (Linux/headless — no real mic, no Windows-only dep). It compares the synthetic pitch to the
/// exercise's own target band and shows a DISPLAY-ONLY hold/progress and elapsed time. It does NOT
/// persist sessions, update SmartCoach/progression, or make any clinical safety/health/recovery
/// decision. See docs/AVALONIA_EXERCISE_RUNTIME_PLACEHOLDERS.md.
/// </summary>
public partial class ExerciseRuntimeViewModel : ObservableObject, IDisposable
{
    private const int SampleRate = 44100;
    private const double DefaultHoldTargetSeconds = 5.0;
    private readonly double _holdTargetSeconds;

    private readonly IUiDispatcher _ui;
    private readonly Action _back;
    private readonly SyntheticAudioCaptureService _capture;
    private readonly PitchDetectionService _pitch = new(SampleRate);
    private readonly PitchTraceStabilizer _stabilizer = new();
    private readonly LiveMetricsService _metrics = new();

    private DateTime _startUtc;
    private DateTime _lastFrameUtc;
    private double _holdRaw;   // unrounded hold accumulator (display value is rounded separately)

    public ExerciseRuntimeViewModel(EnhancedExercise exercise, IUiDispatcher ui, Action back)
    {
        Exercise = exercise;
        _ui = ui;
        _back = back;

        TargetPitchMin = exercise.TargetPitchMin;
        TargetPitchMax = exercise.TargetPitchMax;
        SelectedExerciseName = exercise.Name;
        Category = string.IsNullOrWhiteSpace(exercise.Category) ? "Øvelse" : exercise.Category;
        Difficulty = ExerciseDisplay.Difficulty(exercise.Difficulty);

        // Read-only target-profile metadata (display only — no clinical decision/enforcement).
        TargetProfile = ExerciseRuntimeTargetProfileDisplay.From(exercise);
        // Use the profile's RequiredHoldSeconds as the (display-only) hold target when available.
        _holdTargetSeconds = TargetProfile.HasProfile && TargetProfile.RequiredHoldSecondsValue > 0
            ? TargetProfile.RequiredHoldSecondsValue
            : DefaultHoldTargetSeconds;

        // Dedicated synthetic source aimed at the middle of the target band so the scaffold visibly
        // sits "in target" by default. (Windows would inject the real IAudioCaptureService via DI.)
        double mid = (TargetPitchMin > 0 && TargetPitchMax > 0)
            ? (TargetPitchMin + TargetPitchMax) / 2.0
            : 200.0;
        _capture = new SyntheticAudioCaptureService { BaseFrequency = mid, Mode = SyntheticAudioMode.StablePitch };
        _capture.FrameAvailable += OnFrameAvailable;

        Begin();
    }

    public EnhancedExercise Exercise { get; }

    /// <summary>Read-only target-profile metadata for the "Mål-profil" panel (display only).</summary>
    public ExerciseRuntimeTargetProfileDisplay TargetProfile { get; }

    /// <summary>Display-only hold target (from the profile's RequiredHoldSeconds when available).</summary>
    public string HoldTargetDescription => $"Mål: hold i {_holdTargetSeconds:F0} s (visning)";

    [ObservableProperty] private string _selectedExerciseName = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _difficulty = "";
    public double TargetPitchMin { get; }
    public double TargetPitchMax { get; }
    public string TargetPitchText => ExerciseDisplay.TargetPitch(TargetPitchMin, TargetPitchMax);

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private double _currentPitch;
    [ObservableProperty] private string _pitchStatus = "—";
    [ObservableProperty] private int _elapsedSeconds;
    public string ElapsedText => $"{ElapsedSeconds / 60}:{ElapsedSeconds % 60:00}";
    partial void OnElapsedSecondsChanged(int value) => OnPropertyChanged(nameof(ElapsedText));
    [ObservableProperty] private double _holdSeconds;
    [ObservableProperty] private double _holdProgressPercent;
    [ObservableProperty] private string _runtimeStatusMessage = "Gjør deg klar …";

    [RelayCommand]
    private void Begin()
    {
        if (IsRunning) return;
        _stabilizer.Reset();
        _metrics.Reset();
        _holdRaw = 0;
        HoldSeconds = 0;
        HoldProgressPercent = 0;
        ElapsedSeconds = 0;
        _startUtc = DateTime.UtcNow;
        _lastFrameUtc = _startUtc;
        IsRunning = true;
        RuntimeStatusMessage = "Øvelse i gang — hold tonen i målområdet.";
        _ = _capture.StartAsync(new AudioCaptureOptions(SampleRate));
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Stop()
    {
        if (!IsRunning) return;
        await _capture.StopAsync().ConfigureAwait(false);
        _ui.Post(() =>
        {
            IsRunning = false;
            PitchStatus = "Stoppet";
            RuntimeStatusMessage = "Øvelse stoppet.";
        });
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Back()
    {
        await Stop();
        _back();
    }

    private void OnFrameAvailable(object? sender, AudioFrameAvailableEventArgs e)
    {
        var now = DateTime.UtcNow;
        double delta = (now - _lastFrameUtc).TotalSeconds;
        _lastFrameUtc = now;

        PitchAnalysisResult result = _pitch.DetectPitch(e.Samples);
        double smoothed = _metrics.CalculateSmoothedPitch(result.Pitch, result.IsVoiced);
        double pitch = result.IsVoiced ? _stabilizer.Filter(smoothed, now) : 0;

        bool inRange = result.IsVoiced && pitch >= TargetPitchMin && pitch <= TargetPitchMax;
        string status;
        if (!result.IsVoiced) status = "Ingen stemme";
        else if (pitch < TargetPitchMin) status = "Under målområde";
        else if (pitch > TargetPitchMax) status = "Over målområde";
        else status = "Innenfor målområde";

        _holdRaw = inRange ? Math.Min(_holdTargetSeconds, _holdRaw + delta) : 0;
        double hold = _holdRaw;
        int elapsed = (int)(now - _startUtc).TotalSeconds;

        _ui.Post(() =>
        {
            CurrentPitch = result.IsVoiced ? Math.Round(pitch, 1) : 0;
            PitchStatus = status;
            HoldSeconds = Math.Round(hold, 1);
            HoldProgressPercent = Math.Round(hold / _holdTargetSeconds * 100.0, 0);
            ElapsedSeconds = elapsed;
            RuntimeStatusMessage = inRange
                ? (hold >= _holdTargetSeconds ? "Flott — du holder målområdet!" : "Bra — hold tonen rolig i målområdet.")
                : status == "Ingen stemme" ? "Si en jevn tone for å begynne." : "Juster tonen mot målområdet.";
        });
    }

    public void Dispose()
    {
        _capture.FrameAvailable -= OnFrameAvailable;
        _ = _capture.StopAsync();
    }
}
