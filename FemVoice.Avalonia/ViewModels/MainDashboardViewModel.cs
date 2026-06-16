using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio;                 // PitchDetectionService
using FemVoiceStudio.Audio.Abstractions;    // IAudioCaptureService, SyntheticAudioMode
using FemVoiceStudio.Core.Platform;         // IUiDispatcher
using FemVoiceStudio.Models;                // PitchAnalysisResult, StabilityState, HealthState
using FemVoiceStudio.Services;              // PitchTraceStabilizer, LiveMetricsService, PitchTargetZonePolicy

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Avalonia-safe main dashboard view-model. It does NOT port the WPF MainViewModel (which is WPF-coupled);
/// instead it drives the SHARED, UI-free analysis services from the platform-neutral IAudioCaptureService.
/// On Linux it runs against the synthetic capture backend. No clinical/domain behaviour is changed — the
/// pitch/stability/health services are used read-only exactly as in the WPF baseline.
///
/// Deferred (documented placeholders — see docs/AVALONIA_MAIN_DASHBOARD_PLACEHOLDERS.md): full
/// FeedbackPipeline/FeedbackConsistencyGuard routing, FemVoiceScoreEngine, VocalHealthSupervisor, and
/// HydrationAdvisor wiring. The feedback string here is a simple descriptive derivation of the live
/// pitch/stability/health states, NOT a change to the FeedbackConsistencyGuard contract.
/// </summary>
public partial class MainDashboardViewModel : ObservableObject, IDisposable
{
    private readonly IAudioCaptureService _capture;
    private readonly IUiDispatcher _ui;
    private readonly PitchDetectionService _pitch;
    private readonly PitchTraceStabilizer _stabilizer = new();
    private readonly LiveMetricsService _metrics = new();
    private const int SampleRate = 44100;
    private const int MaxTracePoints = 200;

    public MainDashboardViewModel(IAudioCaptureService capture, IUiDispatcher ui)
    {
        _capture = capture;
        _ui = ui;
        _pitch = new PitchDetectionService(SampleRate);
        _capture.FrameAvailable += OnFrameAvailable;
        _capture.DeviceLost += OnDeviceLost;
        UpdateComfortZone();
    }

    // ── Live state (bound by the dashboard) ───────────────────────────────────
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private double _currentPitch;
    [ObservableProperty] private string _pitchStability = "—";
    [ObservableProperty] private string _currentSignalStatus = "Ingen stemme";
    [ObservableProperty] private string _currentFeedbackMessage = "Trykk Start for å begynne.";
    [ObservableProperty] private string _healthStatusDisplay = "—";
    [ObservableProperty] private double _comfortZoneLow = 150;
    [ObservableProperty] private double _comfortZoneHigh = 220;

    public Array DifficultyOptions { get; } = Enum.GetValues(typeof(DifficultyLevel));

    [ObservableProperty] private DifficultyLevel _selectedDifficulty = DifficultyLevel.Nybegynner;
    partial void OnSelectedDifficultyChanged(DifficultyLevel value) => UpdateComfortZone();

    public Array SyntheticAudioModes { get; } = Enum.GetValues(typeof(SyntheticAudioMode));

    [ObservableProperty] private SyntheticAudioMode _syntheticAudioMode = SyntheticAudioMode.StablePitch;
    partial void OnSyntheticAudioModeChanged(SyntheticAudioMode value)
    {
        if (_capture is SyntheticAudioCaptureService synth) synth.Mode = value;
    }

    /// <summary>Recent stabilized pitch values for the chart (oldest → newest).</summary>
    public ObservableCollection<double> PitchSamples { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task Start()
    {
        if (IsRecording) return;
        _stabilizer.Reset();
        _metrics.Reset();
        PitchSamples.Clear();
        if (_capture is SyntheticAudioCaptureService synth) synth.Mode = SyntheticAudioMode;
        await _capture.StartAsync(new AudioCaptureOptions(SampleRate)).ConfigureAwait(false);
        IsRecording = true;
        CurrentFeedbackMessage = "Lytter …";
    }

    [RelayCommand]
    private async Task Stop()
    {
        if (!IsRecording) return;
        await _capture.StopAsync().ConfigureAwait(false);
        IsRecording = false;
        CurrentSignalStatus = "Ingen stemme";
        CurrentFeedbackMessage = "Økt stoppet.";
    }

    // ── Analysis (shared services, read-only) ──────────────────────────────────
    private void OnFrameAvailable(object? sender, AudioFrameAvailableEventArgs e)
    {
        PitchAnalysisResult result = _pitch.DetectPitch(e.Samples);
        double smoothed = _metrics.CalculateSmoothedPitch(result.Pitch, result.IsVoiced);
        double stabilized = result.IsVoiced ? _stabilizer.Filter(smoothed, DateTime.Now) : 0;
        StabilityState stability = _metrics.CalculateStability();
        // strainLevel placeholder = 0 (full VocalHealthSupervisor wiring is deferred — see placeholders doc).
        HealthState health = _metrics.CalculateHealth(0, smoothed, result.Intensity);

        _ui.Post(() =>
        {
            CurrentPitch = result.IsVoiced ? Math.Round(stabilized, 1) : 0;
            CurrentSignalStatus = result.IsVoiced
                ? $"Stemme ({result.Confidence:P0} sikkerhet)"
                : "Ingen stemme";
            PitchStability = StabilityText(stability);
            HealthStatusDisplay = HealthText(health);
            CurrentFeedbackMessage = DeriveFeedback(result.IsVoiced, stability, health, stabilized);

            if (result.IsVoiced && stabilized > 0)
            {
                PitchSamples.Add(stabilized);
                while (PitchSamples.Count > MaxTracePoints) PitchSamples.RemoveAt(0);
            }
        });
    }

    private void OnDeviceLost(object? sender, AudioDeviceLostEventArgs e)
        => _ui.Post(() =>
        {
            IsRecording = false;
            CurrentSignalStatus = "Mikrofon utilgjengelig";
            CurrentFeedbackMessage = e.Reason ?? "Lydenhet mistet.";
        });

    private void UpdateComfortZone()
    {
        var range = PitchTargetZonePolicy.ForDifficulty(SelectedDifficulty);
        ComfortZoneLow = range.Min;
        ComfortZoneHigh = range.Max;
    }

    private static string StabilityText(StabilityState s) => s switch
    {
        StabilityState.NoVoice => "Ingen stemme",
        StabilityState.Unstable => "Ustabil",
        StabilityState.Developing => "Bygger stabilitet",
        StabilityState.Stable => "Stabil",
        StabilityState.VeryStable => "Veldig stabil",
        _ => "—",
    };

    private static string HealthText(HealthState h) => h switch
    {
        HealthState.NoVoice => "—",
        HealthState.Safe => "Trygg",
        HealthState.Monitor => "Følg med",
        HealthState.Warning => "Advarsel",
        HealthState.Danger => "Stopp og hvil",
        _ => "—",
    };

    // Simple, safe descriptive feedback (NOT the FeedbackConsistencyGuard pipeline — that is deferred).
    private string DeriveFeedback(bool voiced, StabilityState stability, HealthState health, double pitch)
    {
        if (!voiced) return "Ingen stemme oppdaget — prøv å snakke jevnt.";
        if (health is HealthState.Warning or HealthState.Danger) return "Ta en pause og slapp av i stemmen.";
        if (pitch < ComfortZoneLow) return "Litt under komfortsonen — løft tonen forsiktig.";
        if (pitch > ComfortZoneHigh) return "Litt over komfortsonen — slipp tonen litt ned.";
        return stability is StabilityState.Stable or StabilityState.VeryStable
            ? "Fin, stabil tone i komfortsonen."
            : "Hold tonen jevn i komfortsonen.";
    }

    public void Dispose()
    {
        _capture.FrameAvailable -= OnFrameAvailable;
        _capture.DeviceLost -= OnDeviceLost;
    }
}
