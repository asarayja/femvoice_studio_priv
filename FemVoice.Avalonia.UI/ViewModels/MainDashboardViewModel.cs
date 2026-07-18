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
    // Real cross-platform resonance DSP (frozen Core engine) — same as WPF. Emits a 0–1 resonance score per frame
    // via ResonanceScoreUpdated; we surface it live and persist the session average. Read-only use of the engine.
    private readonly FemVoiceStudio.Audio.ResonanceProxyEngine _resonanceEngine;
    private volatile int _latestResonancePercent;   // 0–100, latest real resonance (volatile: written on capture thread)
    private readonly List<double> _sessionResonance = new();   // per-session samples → saved average
    private const int SampleRate = 44100;
    private const int MaxTracePoints = 200;
    private const double ChartHeightPx = 200;   // fixed chart surface height; px == "distance from bottom"
    private double _chartMin;                    // fixed axis range derived from the comfort zone (display-only)
    private double _chartMax;

    // Session history. When the real database is injected (production/DI), completed sessions are saved as real
    // TrainingSessions (so SmartCoach/Progression see real data). With no DB (headless/tests), a display-only local
    // JSON store is used instead. No clinical logic is changed — the dashboard writes a session row exactly as WPF.
    private readonly History.SessionHistoryStore _history;
    private readonly FemVoiceStudio.Data.IDatabaseService? _database;
    private System.DateTime _sessionStart;

    /// <summary>Recent sessions (newest first, display-only): from the real DB when available, else the local store.</summary>
    public ObservableCollection<History.SessionRecord> RecentSessions { get; } = new();

    [ObservableProperty] private bool _hasRecentSessions;

    // DI resolves this (capture + database injected). Smokes call the 2-arg form (database/history default null →
    // no DB save, local-store path). `history` is a test hook (inject a temp store).
    public MainDashboardViewModel(IAudioCaptureService capture, IUiDispatcher ui,
        FemVoiceStudio.Data.IDatabaseService? database = null, History.SessionHistoryStore? history = null)
    {
        _capture = capture;
        _ui = ui;
        _database = database;
        _history = history ?? new History.SessionHistoryStore();
        _pitch = new PitchDetectionService(SampleRate);
        _resonanceEngine = new FemVoiceStudio.Audio.ResonanceProxyEngine(SampleRate);
        _resonanceEngine.ResonanceScoreUpdated += OnResonanceScore;
        _capture.FrameAvailable += OnFrameAvailable;
        _capture.DeviceLost += OnDeviceLost;
        UpdateComfortZone();
        RefreshRecentSessions();
    }

    private void RefreshRecentSessions()
    {
        RecentSessions.Clear();
        if (_database is not null)
        {
            try
            {
                foreach (var s in _database.GetRecentSessions(5))
                    RecentSessions.Add(new History.SessionRecord
                    {
                        WhenUtcTicks = s.StartTime.ToUniversalTime().Ticks,
                        Source = "Dashbord",
                        DurationSeconds = s.DurationSeconds,
                        Note = "Lagret økt",
                    });
            }
            catch { /* display-only list: never surface a DB read error */ }
        }
        else
        {
            foreach (var r in _history.Recent(5)) RecentSessions.Add(r);
        }
        HasRecentSessions = RecentSessions.Count > 0;
    }

    // ── Live state (bound by the dashboard) ───────────────────────────────────
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private double _currentPitch;
    [ObservableProperty] private string _pitchStability = "—";
    [ObservableProperty] private string _currentSignalStatus = "Ingen stemme";
    [ObservableProperty] private string _currentFeedbackMessage = "Trykk Start for å begynne.";
    [ObservableProperty] private string _healthStatusDisplay = "—";
    /// <summary>Live real resonance readout (from the Core ResonanceProxyEngine), e.g. "Lys (72)". "—" when no voice.</summary>
    [ObservableProperty] private string _resonanceDisplay = "—";
    [ObservableProperty] private double _comfortZoneLow = 150;
    [ObservableProperty] private double _comfortZoneHigh = 220;

    public Array DifficultyOptions { get; } = Enum.GetValues(typeof(DifficultyLevel));

    [ObservableProperty] private DifficultyLevel _selectedDifficulty = DifficultyLevel.Nybegynner;
    partial void OnSelectedDifficultyChanged(DifficultyLevel value) => UpdateComfortZone();

    /// <summary>True when the active capture backend is the synthetic display-only source (no real microphone).
    /// Drives visibility of the synthetic test-tone selector — it is hidden when a real mic drives the dashboard.</summary>
    public bool IsSyntheticBackend => _capture is SyntheticAudioCaptureService;

    public Array SyntheticAudioModes { get; } = Enum.GetValues(typeof(SyntheticAudioMode));

    [ObservableProperty] private SyntheticAudioMode _syntheticAudioMode = SyntheticAudioMode.StablePitch;
    partial void OnSyntheticAudioModeChanged(SyntheticAudioMode value)
    {
        if (_capture is SyntheticAudioCaptureService synth) synth.Mode = value;
    }

    /// <summary>Recent stabilized pitch values (Hz) — kept for parity with the prior trace consumers.</summary>
    public ObservableCollection<double> PitchSamples { get; } = new();

    /// <summary>Recent pitch trace as px-from-bottom heights for the converter-free chart (oldest → newest).</summary>
    public ObservableCollection<double> PitchTracePx { get; } = new();

    /// <summary>Display-only scalar chart state (axis range, comfort-zone band, current-pitch marker) in chart
    /// px space — reuses the runtime chart's immutable helper. No OxyPlot, no converter, no clinical decision.</summary>
    [ObservableProperty] private RuntimeChartDisplay _dashboardChart =
        RuntimeChartDisplay.Empty(ChartHeightPx, 120, 260, 150, 220);

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task Start()
    {
        if (IsRecording) return;
        _stabilizer.Reset();
        _metrics.Reset();
        _resonanceEngine.Start();          // real resonance DSP (Reset()s internally)
        _sessionResonance.Clear();
        _latestResonancePercent = 0;
        PitchSamples.Clear();
        PitchTracePx.Clear();
        DashboardChart = RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, ComfortZoneLow, ComfortZoneHigh);
        if (_capture is SyntheticAudioCaptureService synth) synth.Mode = SyntheticAudioMode;
        await _capture.StartAsync(new AudioCaptureOptions(SampleRate)).ConfigureAwait(false);
        _sessionStart = System.DateTime.Now;
        IsRecording = true;
        CurrentFeedbackMessage = "Lytter …";
    }

    [RelayCommand]
    private async Task Stop()
    {
        if (!IsRecording) return;
        await _capture.StopAsync().ConfigureAwait(false);
        _resonanceEngine.Stop();
        IsRecording = false;
        CurrentSignalStatus = "Ingen stemme";
        CurrentFeedbackMessage = "Økt stoppet.";
        ResonanceDisplay = "—";

        // Record the session. Skip trivial (<2 s) sessions.
        int durationSeconds = (int)System.Math.Round((System.DateTime.Now - _sessionStart).TotalSeconds);
        if (durationSeconds >= 2)
        {
            if (_database is not null)
            {
                try
                {
                    var voiced = PitchSamples.Where(p => p > 0).ToList();
                    double avg = voiced.Count > 0 ? voiced.Average() : 0;
                    double inZone = voiced.Count > 0
                        ? 100.0 * voiced.Count(p => p >= ComfortZoneLow && p <= ComfortZoneHigh) / voiced.Count : 0;
                    double avgResonance = _sessionResonance.Count > 0 ? _sessionResonance.Average() : 0;
                    _database.SaveTrainingSession(new FemVoiceStudio.Models.TrainingSession
                    {
                        UserId = 1,
                        StartTime = _sessionStart.ToUniversalTime(),
                        EndTime = System.DateTime.UtcNow,
                        AveragePitch = System.Math.Round(avg, 1),
                        MinPitch = voiced.Count > 0 ? System.Math.Round(voiced.Min(), 1) : 0,
                        MaxPitch = voiced.Count > 0 ? System.Math.Round(voiced.Max(), 1) : 0,
                        OverallScore = System.Math.Round(inZone),   // comfort-zone adherence (display-only score)
                        ResonanceScore = System.Math.Round(avgResonance, 1),   // real resonance from the Core DSP engine
                        DifficultyLevel = SelectedDifficulty,
                        Feedback = "Avalonia dashboard-økt",
                    });
                }
                catch { /* never surface a session-save error to the app */ }
            }
            else
            {
                _history.Append(new History.SessionRecord
                {
                    WhenUtcTicks = System.DateTime.UtcNow.Ticks,
                    Source = "Dashbord",
                    DurationSeconds = durationSeconds,
                    Note = "Kun visning · lokal historikk",
                });
            }
            _ui.Post(RefreshRecentSessions);   // update the bound collection on the UI thread
        }
    }

    // ── Analysis (shared services, read-only) ──────────────────────────────────
    // Real resonance score (0–1 from the Core engine) → 0–100. Fires on the capture thread; store into a volatile
    // field read by the next UI post. When voiced this feeds the live readout + the per-session average.
    private void OnResonanceScore(double score0to1)
    {
        int pct = (int)Math.Round(Math.Clamp(score0to1, 0, 1) * 100);
        _latestResonancePercent = pct;
        if (IsRecording) _sessionResonance.Add(pct);
    }

    private void OnFrameAvailable(object? sender, AudioFrameAvailableEventArgs e)
    {
        PitchAnalysisResult result = _pitch.DetectPitch(e.Samples);
        _resonanceEngine.ProcessSamples(e.Samples);   // real resonance DSP (emits ResonanceScoreUpdated → _latestResonancePercent)
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
            ResonanceDisplay = result.IsVoiced ? ResonanceText(_latestResonancePercent) : "—";
            CurrentFeedbackMessage = DeriveFeedback(result.IsVoiced, stability, health, stabilized);

            bool voiced = result.IsVoiced && stabilized > 0;
            // Display-only chart snapshot (axis + comfort band fixed; marker follows current pitch). No data change.
            DashboardChart = RuntimeChartDisplay.From(
                ChartHeightPx, _chartMin, _chartMax, ComfortZoneLow, ComfortZoneHigh,
                stabilized, voiced, voiced ? "Stemme registrert" : "Venter på stemme …");
            if (voiced)
            {
                PitchSamples.Add(stabilized);
                while (PitchSamples.Count > MaxTracePoints) PitchSamples.RemoveAt(0);
                PitchTracePx.Add(RuntimeChartDisplay.ToPx(stabilized, _chartMin, _chartMax, ChartHeightPx));
                while (PitchTracePx.Count > MaxTracePoints) PitchTracePx.RemoveAt(0);
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
        // Fixed display axis derived from the comfort zone (pure, portable calculator). Display-only.
        var axis = PitchChartAxisRangeCalculator.Calculate(System.Array.Empty<double>(), ComfortZoneLow, ComfortZoneHigh);
        _chartMin = axis.Minimum;
        _chartMax = axis.Maximum;
        PitchTracePx.Clear();
        DashboardChart = RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, ComfortZoneLow, ComfortZoneHigh);
    }

    // Qualitative label + value for the live resonance readout (0–100). Mirrors WPF's bright/neutral/dark buckets.
    private static string ResonanceText(int pct) => pct switch
    {
        >= 67 => $"Lys ({pct})",
        >= 34 => $"Nøytral ({pct})",
        _ => $"Mørk ({pct})",
    };

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
        _resonanceEngine.ResonanceScoreUpdated -= OnResonanceScore;
        _resonanceEngine.Dispose();
    }
}
