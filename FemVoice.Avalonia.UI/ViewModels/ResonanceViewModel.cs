using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio.Abstractions;    // IAudioCaptureService, AudioCaptureBackendFactory
using FemVoiceStudio.Core.Platform;          // IUiDispatcher
using FemVoice.Avalonia.Platform;            // InlineUiDispatcher
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// REAL resonance screen — ports the WPF ResonanceWindow (real-time resonance visualisation) AND the non-scored
/// ResonanceContrastDemoWindow (educational content). Runs the FROZEN Core <see cref="ResonanceProxyEngine"/> on
/// live capture frames (real backend in production, synthetic in tests) and shows a live resonance meter
/// (bright/neutral/dark), plus the optional "resonance contrast" awareness steps (content only — no scoring, no
/// persistence). Read-only use of the engine; IDisposable stops capture on navigate-away.
/// </summary>
public sealed partial class ResonanceViewModel : ObservableObject, IDisposable
{
    private readonly IUiDispatcher _ui;
    private readonly FemVoiceStudio.Audio.ResonanceProxyEngine _engine;
    private IAudioCaptureService? _capture;
    private bool _disposed;

    public ResonanceViewModel() : this(null, null) { }

    public ResonanceViewModel(IAudioCaptureService? capture, IUiDispatcher? ui)
    {
        _ui = ui ?? new InlineUiDispatcher();
        _capture = capture;
        _engine = new FemVoiceStudio.Audio.ResonanceProxyEngine(44100);
        _engine.ResonanceScoreUpdated += OnResonanceScore;

        try
        {
            var probe = _capture ?? AudioCaptureBackendFactory.CreateForRuntime();
            Devices = probe.GetInputDevices().Select(d => d.Name).ToList();
            _backend = _capture ?? probe;
            IsAvailable = Devices.Count > 0;
            if (_capture is null && probe is IDisposable dp && !ReferenceEquals(probe, _backend)) dp.Dispose();
        }
        catch { Devices = Array.Empty<string>(); IsAvailable = false; }

        SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
        StatusMessage = IsAvailable
            ? Localized.Get("Resonance_Ready", "Klar. Trykk «Start» og syng en jevn tone — mål mot lysere resonans.")
            : Localized.Get("Resonance_NoDevice", "Ingen mikrofon funnet i denne visningen.");
    }

    private IAudioCaptureService? _backend;

    public string Title => Localized.Get("Resonance_Title", "Resonans");
    public string Intro => Localized.Get("Resonance_Intro",
        "Sanntids resonansvisning. Måleren viser lys/mørk resonans mens du snakker (frosne Core-motoren). " +
        "Ingen lagring, ingen klinisk endring.");
    public string LevelLabel => Localized.Get("Resonance_Level", "Resonans");
    public string DeviceLabel => Localized.Get("MicCal_Device", "Enhet");
    public string StartLabel => Localized.Get("MicCal_Start", "Start");
    public string StopLabel => Localized.Get("MicCal_Stop", "Stopp");

    // ── Optional resonance-contrast awareness demo (content only — non-scored) ────────────────────────────────
    public string ContrastTitle => Localized.Get("ResonanceContrast_Title", "Resonanskontrast (valgfri øvelse)");
    public IReadOnlyList<string> ContrastSteps { get; } = new[]
    {
        Localized.Get("ResonanceContrast_StepLarge", "Forestill deg et stort, åpent rom bak i munnen — en «mørk» klang."),
        Localized.Get("ResonanceContrast_StepSmall", "Forestill deg så et lite, fremre rom — en «lysere» klang."),
        Localized.Get("ResonanceContrast_NoticeDifference", "Legg merke til forskjellen i klang — ikke i tonehøyde."),
        Localized.Get("ResonanceContrast_Safety", "Hold det avslappet og uanstrengt. Stopp hvis noe kjennes ubehagelig."),
    };
    public string ContrastNote => Localized.Get("ResonanceContrast_NoScoreNote",
        "Dette er en bevisstgjøringsøvelse — den scores ikke og er ikke påkrevd.");

    public IReadOnlyList<string> Devices { get; }
    [ObservableProperty] private string? _selectedDevice;
    [ObservableProperty] private bool _isAvailable;
    /// <summary>Live resonance 0–100 (from the Core engine).</summary>
    [ObservableProperty] private double _level;
    [ObservableProperty] private string _levelLabelText = "—";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private bool _running;
    public bool IsRunning => Running;
    [ObservableProperty] private string _statusMessage = string.Empty;

    private volatile int _latestPercent;

    private void OnResonanceScore(double score0to1)
    {
        int pct = (int)Math.Round(Math.Clamp(score0to1, 0, 1) * 100);
        _latestPercent = pct;
        _ui.Post(() =>
        {
            if (_disposed || !Running) return;
            Level = pct;
            LevelLabelText = pct >= 67 ? $"Lys ({pct})" : pct >= 34 ? $"Nøytral ({pct})" : $"Mørk ({pct})";
        });
    }

    [RelayCommand]
    private void Start()
    {
        if (Running || !IsAvailable) return;
        _backend ??= AudioCaptureBackendFactory.CreateForRuntime();
        _capture = _backend;
        _engine.Start();
        _capture.FrameAvailable += OnFrame;
        _ = _capture.StartAsync(new AudioCaptureOptions());
        Running = true;
        StatusMessage = Localized.Get("Resonance_Listening", "Lytter … syng en jevn tone.");
    }

    [RelayCommand]
    private void Stop()
    {
        if (!Running || _capture is null) return;
        _capture.FrameAvailable -= OnFrame;
        _ = _capture.StopAsync();
        _engine.Stop();
        Running = false;
        Level = 0;
        LevelLabelText = "—";
        StatusMessage = Localized.Get("Resonance_Stopped", "Stoppet.");
    }

    private void OnFrame(object? sender, AudioFrameAvailableEventArgs e) => _engine.ProcessSamples(e.Samples);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.ResonanceScoreUpdated -= OnResonanceScore;
        if (_capture is not null) { _capture.FrameAvailable -= OnFrame; _ = _capture.StopAsync(); }
        _engine.Stop();
        _engine.Dispose();
        if (_backend is not null && !ReferenceEquals(_backend, _capture)) { }
        if (_backend is IDisposable d) d.Dispose();
    }
}
