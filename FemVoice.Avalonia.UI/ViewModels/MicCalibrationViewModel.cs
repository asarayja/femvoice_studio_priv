using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio;                 // MicrophoneCalibrationService, CalibrationQualityReport, flags
using FemVoiceStudio.Audio.Abstractions;   // IAudioCaptureService, AudioCaptureBackendFactory, options
using FemVoiceStudio.Core.Platform;         // IUiDispatcher
using FemVoice.Avalonia.Platform;           // InlineUiDispatcher (headless/tests)
using FemVoice.Avalonia.Localization;       // Localized

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// REAL microphone CALIBRATION wizard — ports the WPF MicrophoneCalibrationWindow. A two-phase guided flow:
/// (1) measure the background/silence, (2) measure a comfortable voice/hum. It then runs the FROZEN Core
/// <see cref="MicrophoneCalibrationService"/> to assess quality and, when the sample is usable, builds an ADAPTIVE
/// profile (blended with any previous one) and SAVES it (noise-gate / voiced-RMS thresholds that feed the frozen
/// DSP). No threshold logic is changed here — all math lives in the frozen service. When the sample is not usable,
/// it surfaces the same guidance as WPF and lets the user retry. Uses the audio abstraction only (real backend in
/// production, synthetic in headless/tests). IDisposable: stops capture on navigate-away. Null-safe: with no backend
/// it shows "unavailable". Profiles are written under LocalApplicationData (override the directory for tests).
/// </summary>
public partial class MicCalibrationViewModel : ObservableObject, IDisposable
{
    private readonly IUiDispatcher _ui;
    private readonly IAudioCaptureService? _capture;
    private IAudioCaptureService? _backend;
    private readonly MicrophoneCalibrationService _calibration;
    private readonly List<float> _phaseBuffer = new();
    private readonly object _bufferLock = new();
    private float[] _background = Array.Empty<float>();
    private float[] _voice = Array.Empty<float>();
    private Step _step = Step.NotStarted;
    private double _peak;
    private bool _disposed;

    private enum Step { NotStarted, SilenceReady, VoiceReady, Completed }

    /// <summary>Capture duration per phase, in seconds. Lowered by smokes to keep them fast; sensible default in prod.</summary>
    public double PhaseSeconds { get; set; } = 2.5;

    public MicCalibrationViewModel() : this(null, null) { }

    /// <param name="capture">Injected capture backend; when null a real-when-available backend is created lazily
    /// on the first capture phase (so construction/headless stays side-effect-free).</param>
    /// <param name="ui">Dispatcher to marshal capture-thread frames to the UI thread; inline when null (tests).</param>
    /// <param name="profileDirectory">Override the calibration-profile directory (tests write to a temp path so the
    /// real user profile is never touched); null → the frozen service's default LocalApplicationData location.</param>
    public MicCalibrationViewModel(IAudioCaptureService? capture, IUiDispatcher? ui, string? profileDirectory = null)
    {
        _ui = ui ?? new InlineUiDispatcher();
        _capture = capture;   // may be null → a backend is created on first capture
        _calibration = new MicrophoneCalibrationService(profileDirectory);

        // Device enumeration is safe and synchronous (no capture started). Falls back gracefully.
        try
        {
            var probe = _capture ?? AudioCaptureBackendFactory.CreateForRuntime();
            Devices = probe.GetInputDevices().Select(d => d.Name).ToList();
            _backend = _capture ?? probe;
            IsAvailable = Devices.Count > 0;
            if (_capture is null && probe is IDisposable dp && !ReferenceEquals(probe, _backend)) dp.Dispose();
        }
        catch
        {
            Devices = Array.Empty<string>();
            IsAvailable = false;
        }

        SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
        _instruction = Localized.Get("MicCalibration_Ready",
            "Trykk start. Deretter velger du selv når appen skal måle stille rom og når den skal måle komfortabel stemme eller humming.");
        _primaryActionLabel = Localized.Get("MicCalibration_RecordSilence", "Mål stille rom");
        StatusMessage = IsAvailable
            ? Localized.Get("MicCalibration_ManualReady", "Klar. Trykk «Mål stille rom» når du er stille og rommet er rolig.")
            : Localized.Get("MicCal_NoDevice", "Ingen mikrofon funnet i denne visningen.");
    }

    // ── Localized chrome (shared RESX; WPF keys → text parity, Norwegian fallbacks) ───────────────────────────────
    public string Title => Localized.Get("MicCalibration_Title", "Mikrofonkalibrering");
    public string Intro => Localized.Get("MicCalibration_Intro",
        "Kalibreringen tilpasser live feedback til mikrofonen din. Du kan kjøre den flere ganger, appen forbedrer profilen gradvis.");
    public string DeviceLabel => Localized.Get("MicCal_Device", "Enhet");
    public string LevelLabel => Localized.Get("MicCal_Level", "Inngangsnivå");
    public string RestartLabel => Localized.Get("MicCalibration_Restart", "Start på nytt");

    /// <summary>Input device names (display-only).</summary>
    public IReadOnlyList<string> Devices { get; }
    [ObservableProperty] private string? _selectedDevice;

    /// <summary>True when at least one input device exists (drives the record button / meter visibility).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    private bool _isAvailable;

    // ── Wizard-visible state ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>Current-step instruction (what to do now).</summary>
    [ObservableProperty] private string _instruction = string.Empty;
    /// <summary>Result / quality / saved-profile message for the current step.</summary>
    [ObservableProperty] private string _resultText = string.Empty;
    /// <summary>Live RMS / dBFS readout while a phase is recording.</summary>
    [ObservableProperty] private string _liveLevelText = string.Empty;
    /// <summary>Summary of the measured silence phase (RMS + dBFS), shown after step 1.</summary>
    [ObservableProperty] private string _noiseSummary = string.Empty;
    /// <summary>0–100 progress of the current capture phase.</summary>
    [ObservableProperty] private double _progress;
    /// <summary>Label of the primary action button (Measure silence → Measure voice → done).</summary>
    [ObservableProperty] private string _primaryActionLabel = string.Empty;
    /// <summary>True once a usable profile has been built and saved (wizard finished).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    private bool _isComplete;
    /// <summary>True while a capture phase is in progress (buttons disabled).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    private bool _capturing;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // Live meter (during a capture phase) — plain signal metering, no DSP/pitch/scoring.
    [ObservableProperty] private double _level;
    [ObservableProperty] private double _peakLevel;
    [ObservableProperty] private bool _signalDetected;

    /// <summary>The primary button is usable when a device exists, no phase is recording, and we're not finished.</summary>
    public bool CanRecord => IsAvailable && !Capturing && !IsComplete;

    // Below this RMS-derived level, treat the input as silence (metering epsilon — NOT a clinical noise gate).
    private const double SignalEpsilon = 2.0;

    /// <summary>Advance the wizard: record the silence phase, then the voice phase, then assess + save. Mirrors the
    /// WPF Next-button flow (its label changes per step). Re-entrant-safe via the Capturing guard.</summary>
    [RelayCommand]
    private async Task Next()
    {
        if (!CanRecord) return;
        try
        {
            if (_step is Step.NotStarted or Step.SilenceReady)
            {
                _background = await CapturePhaseAsync("MicCalibration_SilenceRecording",
                    "Måler bakgrunnsstøy. Vær stille.");
                if (_background.Length == 0)
                {
                    ResultText = Localized.Get("MicCalibration_NoSamples",
                        "Kalibrering kunne ikke fullføres fordi ingen lydprøver ble mottatt.");
                    return;   // stay on the silence step
                }
                _step = Step.VoiceReady;
                NoiseSummary = FormatPhaseSummary(Localized.Get("MicCalibration_NoiseLabel", "Støy"), _background);
                Instruction = Localized.Get("MicCalibration_VoiceInstruction",
                    "Bruk komfortabel stemme eller rolig humming. Ikke press stemmen. Trykk måleknappen når du er klar.");
                ResultText = Localized.Get("MicCalibration_SilenceCaptured",
                    "Stille rom er målt. Trykk «Mål stemme» når du er klar til å bruke komfortabel stemme eller rolig humming.");
                PrimaryActionLabel = Localized.Get("MicCalibration_RecordVoice", "Mål stemme");
                return;
            }

            if (_step == Step.VoiceReady)
            {
                _voice = await CapturePhaseAsync("MicCalibration_VoiceRecording",
                    "Måler komfortabel stemme eller humming. Hold nivået rolig og jevnt.");
                Finalize();
            }
        }
        catch (Exception ex)
        {
            Capturing = false;
            ResultText = string.Format(Localized.Get("MicCalibration_FailedFormat", "Kalibrering feilet: {0}"), ex.Message);
        }
    }

    // Assess quality via the FROZEN service; on a usable sample build + SAVE the adaptive profile, otherwise surface
    // the WPF guidance and let the user re-measure the voice phase. No threshold math here — all in the frozen service.
    private void Finalize()
    {
        var quality = _calibration.AssessCalibrationQuality(_background, _voice);
        if (!quality.IsUsable)
        {
            ResultText = Localized.Get(GetQualityMessageKey(quality.Status), "Prøv igjen.")
                + Environment.NewLine + FormatQuality(quality) + FormatCompatibility(quality.CompatibilityFlags);
            _step = Step.VoiceReady;
            Progress = 0;
            PrimaryActionLabel = Localized.Get("MicCalibration_RecordVoice", "Mål stemme");
            return;
        }

        var deviceName = string.IsNullOrWhiteSpace(SelectedDevice) ? "default-input" : SelectedDevice!;
        var profile = _calibration.BuildAdaptiveProfile(deviceName, _background, _voice);
        _calibration.Save(profile);

        // Calibration-completed telemetry line (WPF parity) — device + measured floor/speech + derived thresholds.
        try
        {
            FemVoiceStudio.Services.Rc0RuntimeLog.Write("Calibration",
                $"CalibrationCompleted; Device=\"{deviceName}\"; NoiseFloorRms={profile.NoiseFloorRms:F5}; " +
                $"SpeechRms={profile.SpeechRms:F5}; NoiseGateThreshold={profile.NoiseGateThreshold:F5}; " +
                $"VoicedRmsThreshold={profile.VoicedRmsThreshold:F5}; SnrDb={profile.SignalToNoiseDb:F1}");
        }
        catch { /* telemetry is best-effort; never affects calibration */ }

        _step = Step.Completed;
        IsComplete = true;
        Progress = 100;
        Instruction = Localized.Get("MicCalibration_Complete", "Kalibreringen er ferdig.");
        LiveLevelText = FormatQuality(quality);
        ResultText = string.Format(
            Localized.Get("MicCalibration_SavedFormat",
                "Kalibrering lagret og profilen oppdatert. Runder: {4}. Støy: {0:F4}, stemme: {1:F4}, noise gate: {2:F4}, stemmeterskel: {3:F4}, SNR: {5:F1} dB, peak: {6:F1} dBFS."),
            profile.NoiseFloorRms, profile.SpeechRms, profile.NoiseGateThreshold, profile.VoicedRmsThreshold,
            profile.CalibrationCount, profile.SignalToNoiseDb, profile.PeakDbFs)
            + FormatCompatibility(profile.CompatibilityFlags);
        StatusMessage = Localized.Get("MicCalibration_Complete", "Kalibreringen er ferdig.");
    }

    /// <summary>Restart the wizard from the silence phase (re-measure and re-save; the frozen service blends with the
    /// just-saved profile, exactly like WPF's "run it several times").</summary>
    [RelayCommand]
    private void Restart()
    {
        if (Capturing) return;
        _step = Step.NotStarted;
        _background = Array.Empty<float>();
        _voice = Array.Empty<float>();
        IsComplete = false;
        Progress = 0;
        Level = PeakLevel = 0;
        _peak = 0;
        SignalDetected = false;
        NoiseSummary = string.Empty;
        ResultText = string.Empty;
        LiveLevelText = string.Empty;
        Instruction = Localized.Get("MicCalibration_ManualReady",
            "Klar. Trykk «Mål stille rom» når du er stille og rommet er rolig.");
        PrimaryActionLabel = Localized.Get("MicCalibration_RecordSilence", "Mål stille rom");
    }

    // Record one phase for PhaseSeconds, accumulating the raw frames and driving the live meter/progress. Returns the
    // captured mono samples. Mirrors the WPF CapturePhaseAsync (fixed-duration timed capture).
    private async Task<float[]> CapturePhaseAsync(string recordingKey, string recordingFallback)
    {
        lock (_bufferLock) _phaseBuffer.Clear();
        Instruction = Localized.Get(recordingKey, recordingFallback);
        ResultText = string.Empty;
        Progress = 0;
        _peak = 0;
        Level = PeakLevel = 0;
        SignalDetected = false;
        Capturing = true;

        _backend ??= AudioCaptureBackendFactory.CreateForRuntime();
        _backend.FrameAvailable += OnPhaseFrame;
        // Calibration wants RAW input (WPF disables ApplyInputProcessing on Windows). The Avalonia capture path is
        // already raw PCM (ALSA/synthetic apply no AGC/noise-suppression), so no processing flag is needed here.
        await _backend.StartAsync(new AudioCaptureOptions());

        var startUtc = DateTime.UtcNow;
        while ((DateTime.UtcNow - startUtc).TotalSeconds < PhaseSeconds)
        {
            Progress = Math.Min(100, (DateTime.UtcNow - startUtc).TotalSeconds / PhaseSeconds * 100);
            await Task.Delay(50);
        }

        _backend.FrameAvailable -= OnPhaseFrame;
        await _backend.StopAsync();
        Capturing = false;
        Progress = 100;
        lock (_bufferLock) return _phaseBuffer.ToArray();
    }

    private void OnPhaseFrame(object? sender, AudioFrameAvailableEventArgs e)
    {
        var s = e.Samples;
        lock (_bufferLock) _phaseBuffer.AddRange(s);

        // Live meter + readout from the frozen service's own RMS/peak/dBFS math (metering only, no DSP/scoring).
        double rms = MicrophoneCalibrationService.CalculateRms(s);
        double peak = MicrophoneCalibrationService.CalculatePeak(s);
        double level = Math.Min(100.0, rms * 300.0);
        if (level > _peak) _peak = level;

        _ui.Post(() =>
        {
            if (_disposed) return;
            Level = Math.Round(level, 1);
            PeakLevel = Math.Round(_peak, 1);
            SignalDetected = level >= SignalEpsilon;
            LiveLevelText = string.Format(
                Localized.Get("MicCalibration_LiveLevelFormat", "Live nivå: RMS {0:F4} ({1:F1} dBFS), peak {2:F1} dBFS"),
                rms, MicrophoneCalibrationService.CalculateDbFs(rms), MicrophoneCalibrationService.CalculateDbFs(peak));
        });
    }

    // ── WPF-parity formatting helpers (text only; the numbers come from the frozen service) ───────────────────────
    private static string FormatPhaseSummary(string label, float[] samples)
    {
        var rms = MicrophoneCalibrationService.CalculateRms(samples);
        return string.Format("{0}: RMS {1:F4} ({2:F1} dBFS)", label, rms, MicrophoneCalibrationService.CalculateDbFs(rms));
    }

    private static string GetQualityMessageKey(CalibrationQualityStatus status) => status switch
    {
        CalibrationQualityStatus.NoSamples => "MicCalibration_NoSamples",
        CalibrationQualityStatus.TooLoud => "MicCalibration_TooLoud",
        CalibrationQualityStatus.TooQuiet => "MicCalibration_TooQuiet",
        CalibrationQualityStatus.TooCloseToNoise => "MicCalibration_TooCloseToNoise",
        _ => "MicCalibration_TooQuiet",
    };

    private static string FormatQuality(CalibrationQualityReport q) => string.Format(
        Localized.Get("MicCalibration_QualityFormat", "Støy RMS {0:F4}, stemme RMS {1:F4}, SNR {2:F1} dB, peak {3:F1} dBFS."),
        q.NoiseFloorRms, q.SpeechRms, q.SignalToNoiseDb, q.PeakDbFs);

    private static string FormatCompatibility(MicrophoneCompatibilityFlags flags)
    {
        if (flags == MicrophoneCompatibilityFlags.None) return "";
        var messages = new List<string>();
        if (flags.HasFlag(MicrophoneCompatibilityFlags.LowOutput))
            messages.Add(Localized.Get("MicCalibration_FlagLowOutput", "- Lav-output mikrofon: flytt den gjerne nærmere."));
        if (flags.HasFlag(MicrophoneCompatibilityFlags.HighNoiseFloor))
            messages.Add(Localized.Get("MicCalibration_FlagHighNoise", "- Høy bakgrunnsstøy: reduser romstøy."));
        if (flags.HasFlag(MicrophoneCompatibilityFlags.ClippingRisk))
            messages.Add(Localized.Get("MicCalibration_FlagClipping", "- Clipping-risiko: senk inputvolum eller mikrofon-gain."));
        if (flags.HasFlag(MicrophoneCompatibilityFlags.PossibleNoiseGate))
            messages.Add(Localized.Get("MicCalibration_FlagNoiseGate", "- Mulig noise gate: slå av aggressiv gate."));
        if (flags.HasFlag(MicrophoneCompatibilityFlags.PossibleAgcOrCompression))
            messages.Add(Localized.Get("MicCalibration_FlagProcessing", "- Mulig AGC/compressor: slå av automatisk gain."));
        return Environment.NewLine
            + Localized.Get("MicCalibration_CompatibilityHeader", "Mikrofonråd:")
            + Environment.NewLine + string.Join(Environment.NewLine, messages);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_backend is not null)
        {
            _backend.FrameAvailable -= OnPhaseFrame;
            _ = _backend.StopAsync();
            if (!ReferenceEquals(_backend, _capture)) (_backend as IDisposable)?.Dispose();
        }
    }
}
