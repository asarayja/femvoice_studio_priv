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
        _resonance.FormantsUpdated += OnFormants;
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

    // ── Live spectrum (real FFT of the mic frames) — the WPF Analyzer's spectrogram, as a live frequency spectrum ──
    private const int FftSize = 2048;
    private readonly float[] _fftBuffer = new float[FftSize];
    private int _fftFill;
    private const int SpectrumBarCount = 40;          // bars spanning ~80–1000 Hz (the vocal range)
    private const double SpectrumMinHz = 80, SpectrumMaxHz = 1000, SpectrumHeightPx = 90;
    /// <summary>Live spectrum bar heights (px) — magnitude per frequency band from the FFT.</summary>
    [ObservableProperty] private IReadOnlyList<double> _spectrumBars = System.Array.Empty<double>();
    public string SpectrumHeading => Localized.Get("Analyzer_Spectrogram", "Spektrum");

    // ── Spectrogram overlay (WPF Analyzer): main-freq line + target line + F1/F2 formant markers over the spectrum ──
    private const double BarWidthPx = 6, BarSpacingPx = 1, SpectrumPadPx = 4;
    /// <summary>Total logical spectrum width (px), matching the bar strip the markers overlay.</summary>
    public double SpectrumWidthPx => SpectrumPadPx * 2 + SpectrumBarCount * (BarWidthPx + BarSpacingPx);
    /// <summary>X px of the current main-frequency line over the spectrum (−1 = out of range / hidden).</summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasMainFreqMarker))] private double _mainFreqMarkerPx = -1;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasTargetMarker))] private double _targetMarkerPx = -1;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasF1Marker))] private double _formantF1MarkerPx = -1;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasF2Marker))] private double _formantF2MarkerPx = -1;
    public bool HasMainFreqMarker => MainFreqMarkerPx >= 0;
    public bool HasTargetMarker => TargetMarkerPx >= 0;
    public bool HasF1Marker => FormantF1MarkerPx >= 0;
    public bool HasF2Marker => FormantF2MarkerPx >= 0;
    // Live formant readouts (WPF Analyzer shows F1/F2/F3 alongside the spectrogram).
    [ObservableProperty] private string _formantF1 = "—";
    [ObservableProperty] private string _formantF2 = "—";
    [ObservableProperty] private string _formantF3 = "—";
    public string FormantsHeading => Localized.Get("ResonanceWindow_Formants", "Formanter");

    // Map a frequency (Hz) to an X px on the log-spaced spectrum strip; −1 when outside [SpectrumMinHz, SpectrumMaxHz].
    private static double FreqToSpectrumPx(double freq)
    {
        if (freq < SpectrumMinHz || freq > SpectrumMaxHz) return -1;
        double index = SpectrumBarCount * Math.Log(freq / SpectrumMinHz) / Math.Log(SpectrumMaxHz / SpectrumMinHz);
        return SpectrumPadPx + index * (BarWidthPx + BarSpacingPx);
    }

    /// <summary>One musical-note target choice (name + frequency) for the note picker.</summary>
    public sealed record NoteOption(string Label, double Frequency);
    public IReadOnlyList<NoteOption> NoteOptions { get; } = new[]
    {
        new NoteOption("E3", 165), new NoteOption("G3", 196), new NoteOption("A3", 220),
        new NoteOption("C4", 262), new NoteOption("E4", 330), new NoteOption("G4", 392),
    };
    public string SelectFrequencyLabel => Localized.Get("Analyzer_SelectTargetFrequency", "Velg målfrekvens");
    [RelayCommand] private void SelectNote(NoteOption note) { if (note is not null) TargetFrequency = note.Frequency; }

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

    // Real magnitude spectrum via an in-place radix-2 FFT, folded into SpectrumBarCount log-spaced bands over
    // [SpectrumMinHz, SpectrumMaxHz], scaled to px. A Hann window reduces spectral leakage.
    private static IReadOnlyList<double> ComputeSpectrumBars(float[] frame, int sampleRate)
    {
        int nfft = frame.Length;
        var re = new double[nfft];
        var im = new double[nfft];
        for (int i = 0; i < nfft; i++)
        {
            double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (nfft - 1));   // Hann window
            re[i] = frame[i] * w;
        }
        Fft(re, im);

        double hzPerBin = (double)sampleRate / nfft;
        var bars = new double[SpectrumBarCount];
        double maxMag = 1e-9;
        for (int b = 0; b < SpectrumBarCount; b++)
        {
            // Log-spaced band edges.
            double f0 = SpectrumMinHz * Math.Pow(SpectrumMaxHz / SpectrumMinHz, (double)b / SpectrumBarCount);
            double f1 = SpectrumMinHz * Math.Pow(SpectrumMaxHz / SpectrumMinHz, (double)(b + 1) / SpectrumBarCount);
            int k0 = Math.Max(1, (int)(f0 / hzPerBin));
            int k1 = Math.Max(k0 + 1, (int)(f1 / hzPerBin));
            double sum = 0; int cnt = 0;
            for (int k = k0; k < k1 && k < nfft / 2; k++) { sum += Math.Sqrt(re[k] * re[k] + im[k] * im[k]); cnt++; }
            double mag = cnt > 0 ? sum / cnt : 0;
            bars[b] = mag;
            if (mag > maxMag) maxMag = mag;
        }
        // Normalize to px (log scale for a natural spectrum look).
        for (int b = 0; b < SpectrumBarCount; b++)
        {
            double norm = Math.Log10(1 + 9 * bars[b] / maxMag);   // 0..1
            bars[b] = Math.Clamp(norm, 0, 1) * (SpectrumHeightPx - 2) + 2;
        }
        return bars;
    }

    // In-place iterative radix-2 Cooley–Tukey FFT (length must be a power of two).
    private static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -2 * Math.PI / len;
            double wRe = Math.Cos(ang), wIm = Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double curRe = 1, curIm = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = i + k + len / 2;
                    double tRe = re[b] * curRe - im[b] * curIm;
                    double tIm = re[b] * curIm + im[b] * curRe;
                    re[b] = re[a] - tRe; im[b] = im[a] - tIm;
                    re[a] += tRe; im[a] += tIm;
                    double nRe = curRe * wRe - curIm * wIm;
                    curIm = curRe * wIm + curIm * wRe; curRe = nRe;
                }
            }
        }
    }

    [RelayCommand]
    private void Start()
    {
        if (Running || !IsAvailable) return;
        _backend ??= AudioCaptureBackendFactory.CreateForRuntime();
        _capture = _backend;
        _pitches.Clear();
        _fftFill = 0; SpectrumBars = System.Array.Empty<double>();
        AveragePitch = MinPitch = MaxPitch = 0;
        SampleCount = 0;
        _resonancePct = 0;
        TargetMarkerPx = FreqToSpectrumPx(TargetFrequency);   // target line over the spectrogram
        MainFreqMarkerPx = FormantF1MarkerPx = FormantF2MarkerPx = -1;
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
        _fftFill = 0;
        SpectrumBars = System.Array.Empty<double>();
        MainFreqMarkerPx = TargetMarkerPx = FormantF1MarkerPx = FormantF2MarkerPx = -1;
        FormantF1 = FormantF2 = FormantF3 = "—";
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

        // Accumulate into the FFT buffer; when full, compute a live magnitude spectrum → bars (80–1000 Hz).
        var samples = e.Samples;
        for (int i = 0; i < samples.Length; i++)
        {
            _fftBuffer[_fftFill++] = samples[i];
            if (_fftFill >= FftSize)
            {
                _fftFill = 0;
                var bars = ComputeSpectrumBars(_fftBuffer, SampleRate);
                _ui.Post(() => { if (!_disposed && Running) SpectrumBars = bars; });
            }
        }

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
            MainFreqMarkerPx = r.IsVoiced ? FreqToSpectrumPx(pitch) : -1;   // main-freq line over the spectrogram
            ResonanceText = r.IsVoiced ? (resPct >= 67 ? $"Lys ({resPct})" : resPct >= 34 ? $"Nøytral ({resPct})" : $"Mørk ({resPct})") : "—";
            TargetDeltaText = r.IsVoiced && TargetFrequency > 0 ? $"{(pitch - TargetFrequency):+0;-0;0} Hz fra mål" : "—";
            AveragePitch = Math.Round(avg, 1);
            MinPitch = Math.Round(min, 1);
            MaxPitch = Math.Round(max, 1);
            SampleCount = count;
            DurationText = $"{elapsed} s";
        });
    }

    // Live formant snapshot (F1/F2/F3) → readouts + F1/F2 markers over the spectrogram (WPF Analyzer overlay).
    private void OnFormants(FemVoiceStudio.Audio.FormantSnapshot f)
    {
        _ui.Post(() =>
        {
            if (_disposed || !Running) return;
            FormantF1 = f.F1 > 0 ? $"{f.F1:F0} Hz" : "—";
            FormantF2 = f.F2 > 0 ? $"{f.F2:F0} Hz" : "—";
            FormantF3 = f.F3 > 0 ? $"{f.F3:F0} Hz" : "—";
            FormantF1MarkerPx = f.F1 > 0 ? FreqToSpectrumPx(f.F1) : -1;
            FormantF2MarkerPx = f.F2 > 0 ? FreqToSpectrumPx(f.F2) : -1;
        });
    }

    // Keep the target line positioned when the user changes the target frequency (note picker / spinner).
    partial void OnTargetFrequencyChanged(double value) => TargetMarkerPx = FreqToSpectrumPx(value);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _resonance.ResonanceScoreUpdated -= OnResonance;
        _resonance.FormantsUpdated -= OnFormants;
        if (_capture is not null) { _capture.FrameAvailable -= OnFrame; _ = _capture.StopAsync(); }
        _resonance.Stop();
        _resonance.Dispose();
        if (_backend is IDisposable d) d.Dispose();
    }
}
