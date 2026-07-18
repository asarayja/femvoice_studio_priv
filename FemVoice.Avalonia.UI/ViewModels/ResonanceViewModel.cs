using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        _engine.FormantsUpdated += OnFormants;

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

    public string Title => Localized.Get("ResonanceWindow_Title", "Resonansanalyse");
    public string Intro => Localized.Get("ResonanceWindow_Subtitle", "Sanntids formantvisualisering med resonansfokus");
    public string LevelLabel => Localized.Get("ResonanceWindow_ResonanceScore", "Resonansscore");
    // Live formant readouts (WPF ResonanceWindow shows F1/F2/F3 + category).
    [ObservableProperty] private string _formantF1 = "—";
    [ObservableProperty] private string _formantF2 = "—";
    [ObservableProperty] private string _formantF3 = "—";
    public string FormantsHeading => Localized.Get("ResonanceWindow_Formants", "Formanter");
    public string CategoryLabel => Localized.Get("ResonanceWindow_Category", "Kategori");
    public string DeviceLabel => Localized.Get("MicCal_Device", "Enhet");
    public string StartLabel => Localized.Get("MicCal_Start", "Start");
    public string StopLabel => Localized.Get("MicCal_Stop", "Stopp");
    public string ResetLabel => Localized.Get("MicCalibration_Restart", "Nullstill");

    // ── F1/F2 scatter + formant timeline (WPF ResonanceWindow charts) — real formant data from the Core engine ────
    /// <summary>Logical scatter canvas + timeline sizes (px) and the formant Hz ranges they map into.</summary>
    public const double ScatterWidthPx = 240, ScatterHeightPx = 160, TimelineHeightPx = 80;
    private const double F1MinHz = 250, F1MaxHz = 1000;   // F1 → X (openness)
    private const double F2MinHz = 700, F2MaxHz = 2800;   // F2 → Y (frontness / brightness)
    private const int MaxFormantPoints = 60;

    /// <summary>One plotted formant sample: X from F1 (openness), Y from F2 (frontness), both in scatter px.</summary>
    public sealed record FormantPoint(double XPx, double YPx);
    /// <summary>Live F1/F2 scatter (vowel-space) points — newest appended, bounded.</summary>
    public ObservableCollection<FormantPoint> FormantScatter { get; } = new();
    /// <summary>Formant timeline: F2 (the resonance-relevant formant) over time, as px-from-bottom heights.</summary>
    public ObservableCollection<double> FormantTimelinePx { get; } = new();
    public string ScatterHeading => Localized.Get("ResonanceWindow_FormantMap", "Formantkart (F1/F2)");
    public string TimelineHeading => Localized.Get("ResonanceWindow_FormantTimeline", "Formant-tidslinje (F2)");
    /// <summary>Live resonance category readout (Lys/Nøytral/Mørk) — WPF's bright/neutral/dark category.</summary>
    [ObservableProperty] private string _categoryText = "—";

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
            string cat = pct >= 67 ? "Lys" : pct >= 34 ? "Nøytral" : "Mørk";
            LevelLabelText = $"{cat} ({pct})";
            CategoryText = cat;
        });
    }

    // Live formant snapshot (F1/F2/F3 in Hz) from the Core engine — numeric readout + F1/F2 scatter + F2 timeline.
    private void OnFormants(FemVoiceStudio.Audio.FormantSnapshot f)
    {
        _ui.Post(() =>
        {
            if (_disposed || !Running) return;
            FormantF1 = f.F1 > 0 ? $"{f.F1:F0} Hz" : "—";
            FormantF2 = f.F2 > 0 ? $"{f.F2:F0} Hz" : "—";
            FormantF3 = f.F3 > 0 ? $"{f.F3:F0} Hz" : "—";

            // Plot only real formant frames (a formant-less sine yields 0 — legitimately no point).
            if (f.F1 > 0 && f.F2 > 0)
            {
                double x = Math.Clamp((f.F1 - F1MinHz) / (F1MaxHz - F1MinHz), 0, 1) * ScatterWidthPx;
                double y = Math.Clamp((f.F2 - F2MinHz) / (F2MaxHz - F2MinHz), 0, 1) * ScatterHeightPx;
                FormantScatter.Add(new FormantPoint(x, y));
                while (FormantScatter.Count > MaxFormantPoints) FormantScatter.RemoveAt(0);
                FormantTimelinePx.Add(y / ScatterHeightPx * TimelineHeightPx);
                while (FormantTimelinePx.Count > MaxFormantPoints) FormantTimelinePx.RemoveAt(0);
            }
        });
    }

    /// <summary>Clear the formant charts + readouts (WPF ResonanceWindow's Reset). Keeps capture running.</summary>
    [RelayCommand]
    private void Reset()
    {
        FormantScatter.Clear();
        FormantTimelinePx.Clear();
        FormantF1 = FormantF2 = FormantF3 = "—";
        CategoryText = "—";
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
        CategoryText = "—";
        FormantF1 = FormantF2 = FormantF3 = "—";
        FormantScatter.Clear();
        FormantTimelinePx.Clear();
        StatusMessage = Localized.Get("Resonance_Stopped", "Stoppet.");
    }

    private void OnFrame(object? sender, AudioFrameAvailableEventArgs e) => _engine.ProcessSamples(e.Samples);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.ResonanceScoreUpdated -= OnResonanceScore;
        _engine.FormantsUpdated -= OnFormants;
        if (_capture is not null) { _capture.FrameAvailable -= OnFrame; _ = _capture.StopAsync(); }
        _engine.Stop();
        _engine.Dispose();
        if (_backend is not null && !ReferenceEquals(_backend, _capture)) { }
        if (_backend is IDisposable d) d.Dispose();
    }
}
