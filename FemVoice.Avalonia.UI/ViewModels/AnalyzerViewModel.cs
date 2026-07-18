using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio;                  // PitchDetectionService
using FemVoiceStudio.Models;                 // PitchAnalysisResult
using FemVoiceStudio.Audio.Abstractions;     // IAudioCaptureService, AudioCaptureBackendFactory
using FemVoiceStudio.Core.Platform;          // IUiDispatcher
using FemVoice.Avalonia.Platform;            // InlineUiDispatcher
using FemVoice.Avalonia.Localization;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// REAL real-time voice analyzer, ported from the WPF AnalyzerWindow: live main frequency (pitch) + resonance
/// (Core ResonanceProxyEngine) against a selectable target frequency, plus running pitch statistics
/// (average / minimum / maximum / duration / sample count) accumulated over the recording. Uses the real capture
/// backend in production (synthetic in tests). Read-only measurement — no scoring gate, no persistence, no clinical
/// change. IDisposable stops capture on navigate-away.
/// </summary>
public sealed partial class AnalyzerViewModel : ObservableObject, IDisposable
{
    private const int SampleRate = 44100;
    private readonly IUiDispatcher _ui;
    private readonly PitchDetectionService _pitch = new(SampleRate);
    private readonly FemVoiceStudio.Audio.ResonanceProxyEngine _resonance = new(SampleRate);
    private IAudioCaptureService? _capture;
    private IAudioCaptureService? _backend;
    private readonly List<double> _pitches = new();
    private DateTime _startUtc;
    private volatile int _resonancePct;
    private bool _disposed;

    public AnalyzerViewModel() : this(null, null) { }

    public AnalyzerViewModel(IAudioCaptureService? capture, IUiDispatcher? ui)
    {
        _ui = ui ?? new InlineUiDispatcher();
        _capture = capture;
        _resonance.ResonanceScoreUpdated += OnResonance;
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
            ? Localized.Get("Analyzer_Ready", "Klar. Velg målfrekvens og trykk «Start».")
            : Localized.Get("MicCal_NoDevice", "Ingen mikrofon funnet i denne visningen.");
    }

    public string Title => Localized.Get("Analyzer_Title", "Analysator");
    public string Intro => Localized.Get("Analyzer_Subtitle",
        "Sanntids analyse: hovedfrekvens, resonans og løpende statistikk mot en valgt målfrekvens. Kun måling.");
    public string DeviceLabel => Localized.Get("MicCal_Device", "Enhet");
    public string TargetLabel => Localized.Get("Analyzer_TargetFrequency", "Målfrekvens (Hz)");
    public string MainFreqLabel => Localized.Get("Analyzer_MainFrequency", "Hovedfrekvens");
    public string ResonanceLabel => Localized.Get("Analyzer_ResonanceFocus", "Resonansfokus");
    public string StatsLabel => Localized.Get("Analyzer_BasicStats", "Statistikk");
    public string StartLabel => Localized.Get("MicCal_Start", "Start");
    public string StopLabel => Localized.Get("MicCal_Stop", "Stopp");

    public IReadOnlyList<string> Devices { get; }
    [ObservableProperty] private string? _selectedDevice;
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private double _targetFrequency = 200;

    [ObservableProperty] private double _mainFrequency;
    [ObservableProperty] private string _resonanceText = "—";
    [ObservableProperty] private string _targetDeltaText = "—";
    [ObservableProperty] private double _averagePitch;
    [ObservableProperty] private double _minPitch;
    [ObservableProperty] private double _maxPitch;
    [ObservableProperty] private int _sampleCount;
    [ObservableProperty] private string _durationText = "0 s";
    /// <summary>Pitch quantiles (5/10/25/50/75/90/95 %) — WPF Analyzer parity. Updated on Stop / when samples exist.</summary>
    [ObservableProperty] private IReadOnlyList<AnalysisSummaryMetric> _quantiles = System.Array.Empty<AnalysisSummaryMetric>();
    /// <summary>Range distribution (very-low … very-high buckets, % of voiced samples) — WPF Analyzer parity.</summary>
    [ObservableProperty] private IReadOnlyList<AnalysisSummaryMetric> _rangeDistribution = System.Array.Empty<AnalysisSummaryMetric>();
    [ObservableProperty] private bool _hasDistribution;
    public string QuantilesHeading => Localized.Get("Analyzer_Quantiles", "Kvantiler");
    public string RangeHeading => Localized.Get("Analyzer_RangeDistribution", "Områdefordeling");
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private bool _running;
    public bool IsRunning => Running;
    [ObservableProperty] private string _statusMessage = string.Empty;

    private void OnResonance(double s) => _resonancePct = (int)Math.Round(Math.Clamp(s, 0, 1) * 100);

    [RelayCommand]
    private void Start()
    {
        if (Running || !IsAvailable) return;
        _backend ??= AudioCaptureBackendFactory.CreateForRuntime();
        _capture = _backend;
        _pitches.Clear();
        AveragePitch = MinPitch = MaxPitch = 0;
        SampleCount = 0;
        _resonancePct = 0;
        _startUtc = DateTime.UtcNow;
        _resonance.Start();
        _capture.FrameAvailable += OnFrame;
        _ = _capture.StartAsync(new AudioCaptureOptions(SampleRate));
        Running = true;
        StatusMessage = Localized.Get("Analyzer_Recording", "Analyserer … snakk jevnt.");
    }

    [RelayCommand]
    private void Stop()
    {
        if (!Running || _capture is null) return;
        _capture.FrameAvailable -= OnFrame;
        _ = _capture.StopAsync();
        _resonance.Stop();
        Running = false;
        StatusMessage = Localized.Get("Analyzer_Done", "Ferdig. Statistikken viser hele opptaket.");
        ComputeDistribution();   // quantiles + range distribution over the full recording
    }

    // Pitch quantiles + range distribution over all voiced samples this recording (WPF Analyzer parity). Real data.
    private void ComputeDistribution()
    {
        var sorted = _pitches.Where(p => p > 0).OrderBy(p => p).ToList();
        if (sorted.Count == 0) { Quantiles = System.Array.Empty<AnalysisSummaryMetric>(); RangeDistribution = System.Array.Empty<AnalysisSummaryMetric>(); HasDistribution = false; return; }

        double Q(double f) { int i = (int)Math.Round(f * (sorted.Count - 1)); return sorted[Math.Clamp(i, 0, sorted.Count - 1)]; }
        Quantiles = new List<AnalysisSummaryMetric>
        {
            new("5 %", $"{Q(0.05):F0} Hz"), new("10 %", $"{Q(0.10):F0} Hz"), new("25 %", $"{Q(0.25):F0} Hz"),
            new("50 % (median)", $"{Q(0.50):F0} Hz"), new("75 %", $"{Q(0.75):F0} Hz"), new("90 %", $"{Q(0.90):F0} Hz"), new("95 %", $"{Q(0.95):F0} Hz"),
        };

        // Range buckets (Hz), roughly the WPF very-low … very-high bands.
        int n = sorted.Count;
        string Pct(System.Func<double, bool> inBand) => $"{100.0 * sorted.Count(inBand) / n:F0} %";
        RangeDistribution = new List<AnalysisSummaryMetric>
        {
            new(Localized.Get("Analyzer_RangeVeryLow", "Svært lav (< 145 Hz)"), Pct(p => p < 145)),
            new(Localized.Get("Analyzer_RangeLow", "Lav (145–165 Hz)"), Pct(p => p is >= 145 and < 165)),
            new(Localized.Get("Analyzer_RangeMiddle", "Midtre (165–196 Hz)"), Pct(p => p is >= 165 and < 196)),
            new(Localized.Get("Analyzer_RangeUpper", "Øvre (196–220 Hz)"), Pct(p => p is >= 196 and < 220)),
            new(Localized.Get("Analyzer_RangeVeryHigh", "Svært høy (≥ 220 Hz)"), Pct(p => p >= 220)),
        };
        HasDistribution = true;
    }

    private void OnFrame(object? sender, AudioFrameAvailableEventArgs e)
    {
        if (!Running) return;
        PitchAnalysisResult r = _pitch.DetectPitch(e.Samples);
        _resonance.ProcessSamples(e.Samples);
        double pitch = r.IsVoiced ? r.Pitch : 0;
        if (r.IsVoiced && pitch > 0) _pitches.Add(pitch);

        double avg = _pitches.Count > 0 ? _pitches.Average() : 0;
        double min = _pitches.Count > 0 ? _pitches.Min() : 0;
        double max = _pitches.Count > 0 ? _pitches.Max() : 0;
        int count = _pitches.Count;
        int elapsed = (int)(DateTime.UtcNow - _startUtc).TotalSeconds;
        int resPct = _resonancePct;

        _ui.Post(() =>
        {
            if (_disposed || !Running) return;
            MainFrequency = r.IsVoiced ? Math.Round(pitch, 1) : 0;
            ResonanceText = r.IsVoiced ? (resPct >= 67 ? $"Lys ({resPct})" : resPct >= 34 ? $"Nøytral ({resPct})" : $"Mørk ({resPct})") : "—";
            TargetDeltaText = r.IsVoiced && TargetFrequency > 0 ? $"{(pitch - TargetFrequency):+0;-0;0} Hz fra mål" : "—";
            AveragePitch = Math.Round(avg, 1);
            MinPitch = Math.Round(min, 1);
            MaxPitch = Math.Round(max, 1);
            SampleCount = count;
            DurationText = $"{elapsed} s";
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _resonance.ResonanceScoreUpdated -= OnResonance;
        if (_capture is not null) { _capture.FrameAvailable -= OnFrame; _ = _capture.StopAsync(); }
        _resonance.Stop();
        _resonance.Dispose();
        if (_backend is IDisposable d) d.Dispose();
    }
}
