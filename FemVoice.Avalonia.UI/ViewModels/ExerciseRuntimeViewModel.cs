using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio;                 // PitchDetectionService
using FemVoiceStudio.Audio.Abstractions;    // SyntheticAudioCaptureService, IAudioCaptureService
using FemVoiceStudio.Core.Platform;         // IUiDispatcher
using FemVoiceStudio.Models;                // PitchAnalysisResult
using FemVoiceStudio.Services;              // PitchTraceStabilizer, LiveMetricsService, EnhancedExercise

namespace FemVoice.Avalonia.ViewModels;

/// <summary>Display-only runtime lifecycle phase (synthetic; no persistence/clinical meaning).</summary>
public enum RuntimePhase
{
    /// <summary>Before the first Start (or after a fresh navigation) — nothing is running.</summary>
    Inactive,
    /// <summary>A synthetic session is running.</summary>
    Active,
    /// <summary>The session was stopped — a display-only session-ended summary is shown.</summary>
    Stopped,
}

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

    // ── Converter-free runtime chart (DISPLAY-ONLY; no OxyPlot, no converters) ───────
    private const double ChartHeightPx = 200;   // fixed chart surface height; px == "distance from bottom"
    private const int MaxTracePoints = 120;      // recent-pitch window (capped like the dashboard trace)
    private double _chartMin;                    // fixed axis range for the session (from the target band)
    private double _chartMax;

    // ── VM-local coordinator readout (DISPLAY-ONLY) ──────────────────────────────────
    // The parameterless ExerciseIntelligenceCoordinator is wired READ-ONLY as a readout source only:
    // it holds no DB/recorder/gate/SmartCoach (those live only in the full ctor, never used here).
    // We feed it synthetic-derived metrics via UpdateMetrics and surface its in-memory hold/state.
    // Nothing here is persisted, gated, scored, or enforced — the safety-lock value is shown as text.
    private const int    SyntheticUserId           = 1;     // positive probe id; coordinator writes no DB
    private const double  CoordResonancePlaceholder = 60.0;  // neutral placeholder (pitch is the only real feed)
    private const double  CoordStabilityPlaceholder = 0.8;   // neutral placeholder
    private const double  CoordHealthPlaceholder    = 100.0; // safe → never trips a health-threshold lock
    private readonly ExerciseIntelligenceCoordinator _coordinator = new();
    private readonly ExerciseTargetProfile? _coordinatorProfile;
    private readonly bool _coordinatorEnabled;
    private ExerciseLiveState? _latestLiveState;   // last snapshot from ExerciseUpdated (display only)

    private readonly IUiDispatcher _ui;
    private readonly Action _back;
    private readonly IAudioCaptureService _capture;
    /// <summary>True when this session is driven by the SYNTHETIC target-tuned source (no real mic).</summary>
    public bool IsSyntheticSource { get; }
    private readonly PitchDetectionService _pitch = new(SampleRate);
    private readonly PitchTraceStabilizer _stabilizer = new();
    private readonly LiveMetricsService _metrics = new();
    // Real cross-platform resonance DSP (frozen Core engine, same as WPF/dashboard). Emits 0–1 per frame; feeds the
    // live readout + the coordinator's resonance input (replacing the old neutral placeholder). Read-only use.
    private readonly FemVoiceStudio.Audio.ResonanceProxyEngine _resonanceEngine = new(SampleRate);
    private volatile int _latestResonancePercent;   // 0–100 latest real resonance (volatile: written on capture thread)

    private DateTime _startUtc;
    private DateTime _lastFrameUtc;
    private double _holdRaw;   // unrounded hold accumulator (display value is rounded separately)
    private double _peakHoldPercent;   // best hold % this session (for the display-only session-ended summary)

    // Optional Avalonia-local, display-only history (no clinical scoring, no WPF DB). Null in tests/smokes so they
    // never touch the real file; the production shell passes a real store so finished exercises are logged locally.
    private readonly History.SessionHistoryStore? _history;

    public ExerciseRuntimeViewModel(EnhancedExercise exercise, IUiDispatcher ui, Action back,
        History.SessionHistoryStore? history = null, bool useRealMic = false)
    {
        Exercise = exercise;
        _ui = ui;
        _back = back;
        _history = history;

        TargetPitchMin = exercise.TargetPitchMin;
        TargetPitchMax = exercise.TargetPitchMax;
        SelectedExerciseName = exercise.Name;
        Category = string.IsNullOrWhiteSpace(exercise.Category) ? "Øvelse" : exercise.Category;
        Difficulty = ExerciseDisplay.Difficulty(exercise.Difficulty);
        Steps = exercise.Steps ?? new List<string>();
        MetricsText = exercise.Metrics is { Count: > 0 }
            ? string.Join(", ", exercise.Metrics.Select(ExerciseDisplay.Metric))
            : "—";

        // Read-only target-profile metadata (display only — no clinical decision/enforcement).
        TargetProfile = ExerciseRuntimeTargetProfileDisplay.From(exercise);
        // Use the profile's RequiredHoldSeconds as the (display-only) hold target when available.
        _holdTargetSeconds = TargetProfile.HasProfile && TargetProfile.RequiredHoldSecondsValue > 0
            ? TargetProfile.RequiredHoldSecondsValue
            : DefaultHoldTargetSeconds;

        // Resolve the pure ExerciseTargetProfile for the VM-local, display-only coordinator readout.
        // Enabled only when a profile is mapped; otherwise the readout shows "unavailable" (documented).
        // Subscribe BEFORE Begin() so the coordinator's initial default-state event is captured.
        _coordinatorProfile = ExerciseRuntimeTargetProfileDisplay.ResolveProfile(exercise);
        _coordinatorEnabled = _coordinatorProfile is not null;
        if (_coordinatorEnabled)
            _coordinator.ExerciseUpdated += OnCoordinatorState;

        // Fixed chart axis range for the session (pure, portable PitchChartAxisRangeCalculator over the
        // target band). Keeping it stable means the target band stays put and the trace scrolls under it.
        var axis = PitchChartAxisRangeCalculator.Calculate(Array.Empty<double>(), TargetPitchMin, TargetPitchMax);
        _chartMin = axis.Minimum;
        _chartMax = axis.Maximum;
        _runtimeChart = RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, TargetPitchMin, TargetPitchMax);

        // Audio source. In production (real mic available) the exercise is driven by the REAL microphone — the same
        // pitch/coordinator pipeline, just real frames. Otherwise (no mic / tests) a dedicated synthetic source aimed
        // at the middle of the target band drives it so the scaffold visibly sits "in target". Only the frame SOURCE
        // differs — no clinical logic changes.
        double mid = (TargetPitchMin > 0 && TargetPitchMax > 0)
            ? (TargetPitchMin + TargetPitchMax) / 2.0
            : 200.0;
        if (useRealMic)
        {
            var real = AudioCaptureBackendFactory.CreateForRuntime();   // real-when-available, else synthetic
            _capture = real;
            IsSyntheticSource = real is SyntheticAudioCaptureService;
        }
        else
        {
            _capture = new SyntheticAudioCaptureService { BaseFrequency = mid, Mode = SyntheticAudioMode.StablePitch };
            IsSyntheticSource = true;
        }
        _capture.FrameAvailable += OnFrameAvailable;
        _resonanceEngine.ResonanceScoreUpdated += OnResonanceScore;

        // Explicit lifecycle: start Inactive (no auto-start). The user presses Start (BeginCommand) to run
        // the synthetic session; the FrameAvailable handler is subscribed once here and only fires while the
        // capture is started (in Begin) — re-Start does not re-subscribe.
        RuntimeStatusMessage = "Klar — trykk Start for å begynne (syntetisk, kun visning).";
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

    // ── Pre-start exercise info (display-only; from the shared catalog — the single exercise page now shows
    //    this directly, replacing the old separate detail page). Nothing computed clinically here. ──
    public string Purpose => Exercise.Description;
    public string Rationale => Exercise.ScientificRationale;
    public IReadOnlyList<string> Steps { get; }
    public string GoalText => ExerciseDisplay.Goal(Exercise.Goal);
    public string FrequencyText => Exercise.Frequency.ToString();
    public string DurationText => $"{Exercise.DurationMinutes} min";
    public string MetricsText { get; }
    // General, non-clinical reminder (the catalog has no per-exercise safety field) — NOT a Voice-Health gate.
    public string SafetyNote =>
        "Øv uten press: stopp ved ubehag, slapp av i hals/skuldre, og ta pauser. " +
        "Helse og sikkerhet går alltid foran tonehøyde.";

    // ── Focus-aware wording (display-only — reflects the exercise's goal/category, NOT always pitch) ──
    /// <summary>Focus label from the exercise goal (Tonehøyde/Resonans/Intonasjon/Pust/Kombinert).</summary>
    public string FocusLabel => ExerciseDisplay.Goal(Exercise.Goal);
    /// <summary>Focus-specific one-line summary so non-pitch exercises don't read as pitch-centric.</summary>
    public string FocusSummary => ExerciseDisplay.FocusSummary(Exercise.Goal);
    /// <summary>True when tonehøyde is the PRIMARY focus (Pitch/Combined) — show the pitch target prominently.</summary>
    public bool IsPitchFocused => ExerciseDisplay.IsPitchPrimary(Exercise.Goal);
    /// <summary>For non-pitch exercises, the pitch range is shown only as a SECONDARY technical detail.</summary>
    public bool ShowSecondaryPitch => !IsPitchFocused && TargetPitchMin > 0 && TargetPitchMax > 0;
    public string SecondaryPitchText => $"Tekniske mål (tonehøyde): {TargetPitchText}";

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private double _currentPitch;
    /// <summary>Live real resonance readout (from the Core ResonanceProxyEngine), e.g. "Lys (72)". "—" when no voice.</summary>
    [ObservableProperty] private string _currentResonance = "—";
    [ObservableProperty] private string _pitchStatus = "—";
    [ObservableProperty] private int _elapsedSeconds;
    public string ElapsedText => $"{ElapsedSeconds / 60}:{ElapsedSeconds % 60:00}";
    partial void OnElapsedSecondsChanged(int value) => OnPropertyChanged(nameof(ElapsedText));
    [ObservableProperty] private double _holdSeconds;
    [ObservableProperty] private double _holdProgressPercent;
    [ObservableProperty] private string _runtimeStatusMessage = "Gjør deg klar …";

    // ── Lifecycle (display-only; synthetic — no persistence/clinical meaning) ─────────
    [ObservableProperty] private RuntimePhase _phase = RuntimePhase.Inactive;
    [ObservableProperty] private string _sessionEndedSummary = "";

    /// <summary>True before the first Start (synthetic session not yet running).</summary>
    public bool IsInactive => Phase == RuntimePhase.Inactive;
    /// <summary>True after Stop — the session-ended summary panel is shown.</summary>
    public bool IsStopped => Phase == RuntimePhase.Stopped;
    /// <summary>Short phase label for the lifecycle bar.</summary>
    public string PhaseText => Phase switch
    {
        RuntimePhase.Inactive => "Klar",
        RuntimePhase.Active => "Aktiv (syntetisk)",
        RuntimePhase.Stopped => "Stoppet",
        _ => "—",
    };
    /// <summary>Display-only recommended duration from the exercise definition (read-only).</summary>
    public string RecommendedDurationText => Exercise.DurationMinutes > 0
        ? $"Anbefalt varighet: {Exercise.DurationMinutes} min (veiledende)"
        : "Anbefalt varighet: —";
    /// <summary>Static note: the synthetic session is never saved.</summary>
    public string NotSavedNote => "Økten lagres ikke — visning-bare syntetisk kjøring.";

    partial void OnPhaseChanged(RuntimePhase value)
    {
        OnPropertyChanged(nameof(IsInactive));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(PhaseText));
    }

    /// <summary>
    /// DISPLAY-ONLY readout of the VM-local ExerciseIntelligenceCoordinator's in-memory state, shown
    /// alongside the derived hold for comparison. Never enforced, persisted, gated, or scored.
    /// </summary>
    [ObservableProperty] private ExerciseCoordinatorReadoutDisplay _coordinatorReadout =
        ExerciseCoordinatorReadoutDisplay.Inactive();

    // ── Runtime chart + live feedback (DISPLAY-ONLY) ────────────────────────────────
    /// <summary>Recent pitch trace as px-from-bottom heights (converter-free; appended on the UI thread).</summary>
    public ObservableCollection<double> RuntimePitchSamples { get; } = new();

    /// <summary>Scalar chart state (axis range, target band, current-pitch marker) in chart px space.</summary>
    [ObservableProperty] private RuntimeChartDisplay _runtimeChart =
        RuntimeChartDisplay.Empty(ChartHeightPx, 60, 110, 0, 0);

    /// <summary>Local, display-only live feedback text (NOT FeedbackConsistencyGuard / SmartCoach).</summary>
    [ObservableProperty] private string _liveFeedbackMessage = "Gjør deg klar …";
    /// <summary>Short, display-only severity label for the feedback (text only — no clinical meaning).</summary>
    [ObservableProperty] private string _liveFeedbackSeverity = "Nøytral";

    /// <summary>Derived (in-VM, pitch-band) hold for the visual bar — same value as HoldProgressPercent.</summary>
    public double DerivedHoldVisualPercent => HoldProgressPercent;
    /// <summary>Coordinator's display-only hold for the visual bar (0 when inactive).</summary>
    public double CoordinatorHoldVisualPercent => CoordinatorReadout.CoordinatorHoldProgressPercent;
    /// <summary>Display-only comparison of coordinator vs derived hold.</summary>
    public string HoldComparisonText => CoordinatorReadout.IsCoordinatorActive
        ? CoordinatorReadout.HoldDifferenceDisplay
        : "Koordinator inaktiv";

    partial void OnHoldProgressPercentChanged(double value) => OnPropertyChanged(nameof(DerivedHoldVisualPercent));
    partial void OnCoordinatorReadoutChanged(ExerciseCoordinatorReadoutDisplay value)
    {
        OnPropertyChanged(nameof(CoordinatorHoldVisualPercent));
        OnPropertyChanged(nameof(HoldComparisonText));
    }

    // Local, display-only feedback derivation (mirrors the dashboard's DeriveFeedback approach).
    private (string message, string severity) DeriveLiveFeedback(bool voiced, double pitch, ExerciseLiveState? live)
    {
        if (live?.IsSafetyLocked == true)
            return ("Koordinator varsler lås — kun visning, ikke håndhevet", "Lås (visning)");
        if (!voiced || pitch <= 0)
            return ("Ingen stabil stemme registrert", "Ingen stemme");
        if (pitch < TargetPitchMin) return ("Litt under målområdet", "Juster");
        if (pitch > TargetPitchMax) return ("Litt over målområdet", "Juster");
        return ("Innenfor målområdet", "I mål");
    }

    [RelayCommand]
    private void Begin()
    {
        if (IsRunning) return;
        _stabilizer.Reset();
        _metrics.Reset();
        _resonanceEngine.Start();          // real resonance DSP (Reset()s internally)
        _latestResonancePercent = 0;
        _holdRaw = 0;
        _peakHoldPercent = 0;
        HoldSeconds = 0;
        HoldProgressPercent = 0;
        ElapsedSeconds = 0;
        RuntimePitchSamples.Clear();
        RuntimeChart = RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, TargetPitchMin, TargetPitchMax);
        LiveFeedbackMessage = "Si en jevn tone i målområdet.";
        LiveFeedbackSeverity = "Nøytral";
        SessionEndedSummary = "";
        _startUtc = DateTime.UtcNow;
        _lastFrameUtc = _startUtc;
        IsRunning = true;
        Phase = RuntimePhase.Active;
        RuntimeStatusMessage = "Øvelse i gang — hold tonen i målområdet.";
        StartCoordinatorReadout();
        _ = _capture.StartAsync(new AudioCaptureOptions(SampleRate));
    }

    /// <summary>
    /// Starts the VM-local coordinator in read-only readout mode (in-memory only). When no profile is
    /// mapped the readout stays "Inactive" (documented). StartExercise internally stops any prior
    /// session first, so re-Begin() is safe. No persistence/gate/SmartCoach is touched.
    /// </summary>
    private void StartCoordinatorReadout()
    {
        if (!_coordinatorEnabled || _coordinatorProfile is null)
        {
            CoordinatorReadout = ExerciseCoordinatorReadoutDisplay.Inactive();
            return;
        }
        _latestLiveState = null;
        _coordinator.StartExercise(_coordinatorProfile, SyntheticUserId);
        CoordinatorReadout = ExerciseCoordinatorReadoutDisplay.From(
            _coordinator.IsExerciseActive, _coordinator.GetHoldProgress(),
            _holdTargetSeconds, _latestLiveState, _holdRaw);
    }

    /// <summary>Captures the latest coordinator state snapshot (display only; benign cross-thread ref set).</summary>
    private void OnCoordinatorState(ExerciseLiveState state) => _latestLiveState = state;

    [RelayCommand]
    private async System.Threading.Tasks.Task Stop()
    {
        if (!IsRunning) return;
        await _capture.StopAsync().ConfigureAwait(false);
        _resonanceEngine.Stop();
        // Clear the VM-local coordinator's in-memory state (safe no-op when inactive; persists nothing).
        if (_coordinatorEnabled) _coordinator.StopExercise();
        _ui.Post(() =>
        {
            // Build the display-only session-ended summary from the last live values BEFORE clearing them.
            SessionEndedSummary =
                $"Økt fullført (kun visning) · varighet {ElapsedText} · beste hold {_peakHoldPercent:F0} %. {NotSavedNote}";

            // Log a display-only local record (no clinical scoring, no progression, no WPF DB). Skips trivial (<2 s).
            if (_history is not null && ElapsedSeconds >= 2)
            {
                _history.Append(new History.SessionRecord
                {
                    WhenUtcTicks = DateTime.UtcNow.Ticks,
                    Source = SelectedExerciseName,
                    DurationSeconds = ElapsedSeconds,
                    Note = "Øvelse · kun visning · lokal historikk",
                });
            }
            IsRunning = false;
            Phase = RuntimePhase.Stopped;
            PitchStatus = "Stoppet";
            RuntimeStatusMessage = "Øvelse stoppet.";
            CoordinatorReadout = ExerciseCoordinatorReadoutDisplay.Inactive();
            RuntimePitchSamples.Clear();
            RuntimeChart = RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, TargetPitchMin, TargetPitchMax);
            LiveFeedbackMessage = "Øvelse stoppet.";
            LiveFeedbackSeverity = "Stoppet";
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
        _resonanceEngine.ProcessSamples(e.Samples);   // real resonance DSP → _latestResonancePercent (display-only readout)
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

        // Feed the VM-local coordinator READ-ONLY: pitch is the real signal; resonance/stability/health
        // are documented neutral placeholders. Read its in-memory hold/state for the display-only readout.
        ExerciseCoordinatorReadoutDisplay? readout = null;
        if (_coordinatorEnabled && _coordinator.IsExerciseActive)
        {
            _coordinator.UpdateMetrics(CoordResonancePlaceholder, pitch, CoordStabilityPlaceholder, CoordHealthPlaceholder);
            readout = ExerciseCoordinatorReadoutDisplay.From(
                _coordinator.IsExerciseActive, _coordinator.GetHoldProgress(),
                _holdTargetSeconds, _latestLiveState, hold);
        }

        // Converter-free chart snapshot + local display-only feedback (computed off the UI thread).
        bool hasVoice = result.IsVoiced && pitch > 0;
        double samplePx = hasVoice ? RuntimeChartDisplay.ToPx(pitch, _chartMin, _chartMax, ChartHeightPx) : 0;
        var chartSnap = RuntimeChartDisplay.From(
            ChartHeightPx, _chartMin, _chartMax, TargetPitchMin, TargetPitchMax,
            pitch, hasVoice, hasVoice ? status : "Ingen stemme");
        (string fbMsg, string fbSev) = DeriveLiveFeedback(result.IsVoiced, pitch,
            _coordinatorEnabled ? _latestLiveState : null);

        int resonancePct = _latestResonancePercent;
        _ui.Post(() =>
        {
            CurrentPitch = result.IsVoiced ? Math.Round(pitch, 1) : 0;
            CurrentResonance = result.IsVoiced ? ResonanceText(resonancePct) : "—";
            PitchStatus = status;
            HoldSeconds = Math.Round(hold, 1);
            HoldProgressPercent = Math.Round(hold / _holdTargetSeconds * 100.0, 0);
            if (HoldProgressPercent > _peakHoldPercent) _peakHoldPercent = HoldProgressPercent;
            ElapsedSeconds = elapsed;
            RuntimeStatusMessage = inRange
                ? (hold >= _holdTargetSeconds ? "Flott — du holder målområdet!" : "Bra — hold tonen rolig i målområdet.")
                : status == "Ingen stemme" ? "Si en jevn tone for å begynne." : "Juster tonen mot målområdet.";
            if (readout is not null) CoordinatorReadout = readout;
            RuntimeChart = chartSnap;
            LiveFeedbackMessage = fbMsg;
            LiveFeedbackSeverity = fbSev;
            if (hasVoice)
            {
                RuntimePitchSamples.Add(samplePx);
                while (RuntimePitchSamples.Count > MaxTracePoints) RuntimePitchSamples.RemoveAt(0);
            }
        });
    }

    // Real resonance score (0–1 from the Core engine) → 0–100. Fires on the capture thread; stored volatile.
    private void OnResonanceScore(double score0to1)
        => _latestResonancePercent = (int)Math.Round(Math.Clamp(score0to1, 0, 1) * 100);

    // Qualitative label + value for the live resonance readout (0–100). Mirrors WPF's bright/neutral/dark buckets.
    private static string ResonanceText(int pct) => pct switch
    {
        >= 67 => $"Lys ({pct})",
        >= 34 => $"Nøytral ({pct})",
        _ => $"Mørk ({pct})",
    };

    public void Dispose()
    {
        IsRunning = false;                       // mark stopped (also when navigated away via top nav)
        _capture.FrameAvailable -= OnFrameAvailable;
        _ = _capture.StopAsync();                // stops the capture loop (synthetic or real) — no more frames
        (_capture as IDisposable)?.Dispose();    // release a real capture backend (e.g. ALSA) if used
        _resonanceEngine.ResonanceScoreUpdated -= OnResonanceScore;
        _resonanceEngine.Dispose();
        if (_coordinatorEnabled) _coordinator.ExerciseUpdated -= OnCoordinatorState;
        _coordinator.StopExercise();             // in-memory clear; no persistence
        _coordinator.Dispose();
    }
}
