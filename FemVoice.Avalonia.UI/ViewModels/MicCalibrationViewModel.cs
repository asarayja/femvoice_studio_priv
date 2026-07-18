using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio.Abstractions;   // IAudioCaptureService, AudioCaptureBackendFactory, options
using FemVoiceStudio.Core.Platform;         // IUiDispatcher
using FemVoice.Avalonia.Platform;           // InlineUiDispatcher (headless/tests)
using FemVoice.Avalonia.Localization;       // Localized

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// REAL microphone CHECK (a safe subset of the WPF MicrophoneCalibrationWindow). Enumerates input devices and,
/// on Start, opens a capture backend and shows a LIVE input-level meter (RMS/peak) + a "signal detected"
/// indicator so the user can confirm their microphone works. It deliberately does NOT compute or persist the
/// clinical <c>MicrophoneCalibrationProfile</c> (noise-gate / voiced-RMS thresholds feed the frozen DSP) — that
/// stays deferred. Level is a plain RMS of the frame samples (signal metering, no DSP/pitch/scoring). Uses the
/// audio abstraction only (real backend in production, synthetic in headless/tests). IDisposable: stops capture
/// on navigate-away (mirrors the exercise runtime lifecycle). Null-safe: with no backend it shows "unavailable".
/// </summary>
public partial class MicCalibrationViewModel : ObservableObject, IDisposable
{
    private readonly IUiDispatcher _ui;
    private readonly IAudioCaptureService? _capture;
    private DateTime _startUtc;
    private double _peak;
    private bool _disposed;

    public MicCalibrationViewModel() : this(null, null) { }

    /// <param name="capture">Injected capture backend; when null a real-when-available backend is created lazily
    /// on Start (so construction/headless stays side-effect-free). </param>
    /// <param name="ui">Dispatcher to marshal capture-thread frames to the UI thread; an inline dispatcher is used
    /// when null (tests).</param>
    public MicCalibrationViewModel(IAudioCaptureService? capture, IUiDispatcher? ui)
    {
        _ui = ui ?? new InlineUiDispatcher();
        _capture = capture;   // may be null → a backend is created on first Start

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
        StatusMessage = IsAvailable
            ? Localized.Get("MicCal_Ready", "Klar. Trykk «Start test» og snakk i mikrofonen.")
            : Localized.Get("MicCal_NoDevice", "Ingen mikrofon funnet i denne visningen.");
    }

    // The backend actually used for Start/Stop. Kept so device probing and capture share one instance.
    private IAudioCaptureService? _backend;

    public string Title => Localized.Get("MicCal_Title", "Mikrofonkalibrering");
    public string Intro => Localized.Get("MicCal_Intro",
        "Kontroller at mikrofonen fanger stemmen din. Nivåmåleren viser inngangsnivået i sanntid. " +
        "Dette er en enkel mikrofonsjekk — terskler for støygrind kommer i en senere fase.");
    public string StartLabel => Localized.Get("MicCal_Start", "Start test");
    public string StopLabel => Localized.Get("MicCal_Stop", "Stopp");
    public string DeviceLabel => Localized.Get("MicCal_Device", "Enhet");
    public string LevelLabel => Localized.Get("MicCal_Level", "Inngangsnivå");
    public string DeferredNote => Localized.Get("MicCal_DeferredNote",
        "Full kalibreringsprofil (støygrind, SNR, klipping) lagres ikke ennå — kun sanntids nivåsjekk.");

    /// <summary>Input device names (display-only).</summary>
    public IReadOnlyList<string> Devices { get; }

    [ObservableProperty] private string? _selectedDevice;

    /// <summary>True when at least one input device exists (drives the Start button / meter visibility).</summary>
    [ObservableProperty] private bool _isAvailable;

    /// <summary>Live input level 0–100 (from frame RMS). Display-only.</summary>
    [ObservableProperty] private double _level;

    /// <summary>Peak level seen this session 0–100.</summary>
    [ObservableProperty] private double _peakLevel;

    /// <summary>True while a signal above the noise epsilon is present.</summary>
    [ObservableProperty] private bool _signalDetected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private bool _running;
    public bool IsRunning => Running;

    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Seconds elapsed since the current test started (display-only).</summary>
    [ObservableProperty] private int _elapsedSeconds;

    // Below this RMS-derived level, treat the input as silence (metering epsilon — NOT a clinical noise gate).
    private const double SignalEpsilon = 2.0;

    [RelayCommand]
    private void Start()
    {
        if (Running || !IsAvailable) return;
        _backend ??= AudioCaptureBackendFactory.CreateForRuntime();
        _peak = 0;
        PeakLevel = 0;
        Level = 0;
        SignalDetected = false;
        _startUtc = DateTime.UtcNow;
        ElapsedSeconds = 0;
        _backend.FrameAvailable += OnFrameAvailable;
        _ = _backend.StartAsync(new AudioCaptureOptions());
        Running = true;
        StatusMessage = Localized.Get("MicCal_Listening", "Lytter … snakk i mikrofonen.");
    }

    [RelayCommand]
    private void Stop()
    {
        if (!Running || _backend is null) return;
        _backend.FrameAvailable -= OnFrameAvailable;
        _ = _backend.StopAsync();
        Running = false;
        StatusMessage = _peak >= SignalEpsilon
            ? Localized.Get("MicCal_Ok", "Signal registrert — mikrofonen fungerer.")
            : Localized.Get("MicCal_NoSignal", "Ingen signal registrert. Sjekk at riktig mikrofon er valgt.");
    }

    private void OnFrameAvailable(object? sender, AudioFrameAvailableEventArgs e)
    {
        // Plain RMS of the mono float frame → a 0–100 level. Signal metering only (no pitch/DSP/scoring).
        double sumSq = 0;
        var s = e.Samples;
        for (int i = 0; i < s.Length; i++) sumSq += (double)s[i] * s[i];
        double rms = s.Length > 0 ? Math.Sqrt(sumSq / s.Length) : 0;
        double level = Math.Min(100.0, rms * 300.0);   // display scaling; ~full scale on normal speech
        if (level > _peak) _peak = level;
        int elapsed = (int)(DateTime.UtcNow - _startUtc).TotalSeconds;

        _ui.Post(() =>
        {
            if (_disposed) return;
            Level = Math.Round(level, 1);
            PeakLevel = Math.Round(_peak, 1);
            SignalDetected = level >= SignalEpsilon;
            ElapsedSeconds = elapsed;
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_backend is not null)
        {
            _backend.FrameAvailable -= OnFrameAvailable;
            _ = _backend.StopAsync();
            if (!ReferenceEquals(_backend, _capture)) (_backend as IDisposable)?.Dispose();
        }
    }
}
