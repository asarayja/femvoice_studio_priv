using FemVoice.Avalonia.Localization;
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

/// <summary>Runtime lifecycle phase.</summary>
public enum RuntimePhase
{
    /// <summary>Before the first Start (or after a fresh navigation) — nothing is running.</summary>
    Inactive,
    /// <summary>A session is running.</summary>
    Active,
    /// <summary>The session was stopped — a session-ended summary is shown.</summary>
    Stopped,
}

/// <summary>
/// Avalonia exercise runtime. Drives the SHARED, UI-free DSP services (PitchDetectionService +
/// PitchTraceStabilizer + LiveMetricsService + the VoiceBrightnessMeter) from the REAL microphone when one is
/// available (falling back to a target-tuned synthetic source only in headless/no-mic contexts). It compares the
/// live pitch to the exercise's own target band and shows a live hold/progress + elapsed time. When a real
/// microphone drives the session AND a database is present, the finished exercise is SAVED as a real
/// <see cref="FemVoiceStudio.Models.TrainingSession"/> (so it counts toward progression), exactly like the
/// dashboard. A synthetic (test-tone) run is honestly labelled and NOT saved as real data. It makes no clinical
/// safety/health/recovery decision — the coordinator readout is advisory only.
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
    private readonly bool _resonanceFocused;     // exercise judged on live brightness (resonance) rather than pitch
    private readonly double _targetResMinPct;    // resonance target band, 0–100 (profile 0–1 × 100)
    private readonly double _targetResMaxPct;

    // ── VM-local coordinator readout (DISPLAY-ONLY) ──────────────────────────────────
    // The parameterless ExerciseIntelligenceCoordinator is wired READ-ONLY as a readout source only:
    // it holds no DB/recorder/gate/SmartCoach (those live only in the full ctor, never used here).
    // We feed it synthetic-derived metrics via UpdateMetrics and surface its in-memory hold/state.
    // Nothing here is persisted, gated, scored, or enforced — the safety-lock value is shown as text.
    private const int    SyntheticUserId           = 1;     // positive probe id; coordinator writes no DB
    private const double  CoordStabilityPlaceholder = 0.8;   // neutral placeholder (runtime has no per-frame stability)
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
    // "Hear your own voice" — plays captured frames back to the speaker while running (opt-in; no-op when off).
    private readonly FemVoice.Avalonia.Audio.VoiceMonitor _voiceMonitor = new();
    private double? _resonanceBaseline;   // per-user calibrated relaxed-voice centroid (Hz); null = use fixed anchors

    private DateTime _startUtc;
    private DateTime _lastFrameUtc;
    private double _holdRaw;   // unrounded hold accumulator (display value is rounded separately)
    private double _peakHoldPercent;   // best hold % this session (for the display-only session-ended summary)

    // Optional Avalonia-local history (used as the fallback when no real DB is present — e.g. tests/smokes, or a
    // synthetic test-tone run). Null in smokes so they never touch the real file.
    private readonly History.SessionHistoryStore? _history;

    // Real database (production, via the shell). When present AND the source is a real microphone, the finished
    // exercise is saved as a real TrainingSession so it counts toward progression — same as the dashboard.
    private readonly FemVoiceStudio.Data.IDatabaseService? _database;
    // Per-session real samples accumulated during the run → session stats on Stop (voiced pitch Hz; resonance 0–100).
    private readonly List<double> _sessionPitch = new();
    private readonly List<double> _sessionResonance = new();
    private readonly List<double> _sessionHealth = new();   // real per-frame voice-health 0–100 → session average

    /// <summary>True when this run will be saved as a real session: a real microphone drives it AND a DB is present.</summary>
    public bool SavesRealSession => !IsSyntheticSource && _database is not null;

    public ExerciseRuntimeViewModel(EnhancedExercise exercise, IUiDispatcher ui, Action back,
        History.SessionHistoryStore? history = null, bool useRealMic = false,
        FemVoiceStudio.Data.IDatabaseService? database = null)
    {
        Exercise = exercise;
        _ui = ui;
        _back = back;
        _history = history;
        _database = database;

        TargetPitchMin = exercise.TargetPitchMin;
        TargetPitchMax = exercise.TargetPitchMax;
        SelectedExerciseName = exercise.Name;
        Category = string.IsNullOrWhiteSpace(exercise.Category) ? Localized.Get("ExRunVm_DefaultCategory", "Øvelse") : exercise.Category;
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

        // FOCUS-AWARE runtime: a resonance-profiled exercise (uses resonance, not pitch) is judged on live BRIGHTNESS
        // against the profile's resonance target band (0–1 → 0–100%), not on pitch. Everything else (pitch exercises)
        // keeps the pitch path. Determined once from the resolved profile.
        //
        // GUARD: the resonance chart is only taken when the exercise is NOT pitch-primary. A handful of exercises pair a
        // pitch/combined Goal (which shows the live Hz readout via IsPitchFocused) with a resonance-focused profile
        // (StabilityTraining, ResonanceVowels) — e.g. #5/#6 pitch-stability, #9/#10 combined phrase/conversation. Without
        // this guard the chart would plot resonance while the Hz number climbs, so the trace sits stuck in the target
        // band (resonance is a low, noisy signal on real mics) while Hz shows 200+. Keying the chart metric off the SAME
        // pitch-primary signal as the Hz readout keeps the two consistent: Hz shown ⇒ pitch chart; no Hz ⇒ resonance chart.
        bool profileResonanceFocused = _coordinatorProfile is { UsesResonance: true, UsesPitch: false };
        _resonanceFocused = profileResonanceFocused && !ExerciseDisplay.IsPitchPrimary(exercise.Goal);
        _targetResMinPct = (_coordinatorProfile?.TargetResonanceMin ?? 0.0) * 100.0;
        _targetResMaxPct = (_coordinatorProfile?.TargetResonanceMax ?? 1.0) * 100.0;

        // Exercise Guidance panel (WPF ExerciseWindow): the 4 guidance items (purpose/focus/mistakes/safety) + the
        // feedback-mode badge, resolved from the exercise's profile keys via the shared RESX. Display-only.
        var g = new List<GuidanceItem>();
        if (_coordinatorProfile is not null)
        {
            void Add(string headKey, string? bodyKey)
            {
                if (string.IsNullOrWhiteSpace(bodyKey)) return;
                var body = Localized.Get(bodyKey!, "");
                if (body.Length > 0) g.Add(new GuidanceItem(Localized.Get(headKey, headKey), body));
            }
            Add("Guidance_ClinicalPurpose", _coordinatorProfile.ClinicalPurposeKey);
            Add("Guidance_PhysicalFocus", _coordinatorProfile.PhysicalFocusKey);
            Add("Guidance_CommonMistakes", _coordinatorProfile.CommonMistakesKey);
            Add("Guidance_SafetyInfo", _coordinatorProfile.SafetyInfoKey);
            FeedbackModeText = string.IsNullOrWhiteSpace(_coordinatorProfile.FeedbackModeKey)
                ? "" : Localized.Get(_coordinatorProfile.FeedbackModeKey!, "");
        }
        GuidanceItems = g;

        // Fixed chart axis range for the session. Resonance exercises plot BRIGHTNESS on a fixed 0–100 axis with the
        // resonance target band; pitch exercises use the pure PitchChartAxisRangeCalculator over the pitch band.
        // Keeping it stable means the target band stays put and the trace scrolls under it.
        if (_resonanceFocused)
        {
            _chartMin = 0;
            _chartMax = 100;
            _runtimeChart = RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, _targetResMinPct, _targetResMaxPct);
        }
        else
        {
            var axis = PitchChartAxisRangeCalculator.Calculate(Array.Empty<double>(), TargetPitchMin, TargetPitchMax);
            _chartMin = axis.Minimum;
            _chartMax = axis.Maximum;
            _runtimeChart = RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, TargetPitchMin, TargetPitchMax);
        }

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

        // Explicit lifecycle: start Inactive (no auto-start). The user presses Start (BeginCommand) to run
        // the synthetic session; the FrameAvailable handler is subscribed once here and only fires while the
        // capture is started (in Begin) — re-Start does not re-subscribe.
        RuntimeStatusMessage = IsSyntheticSource
            ? Localized.Get("ExRunVm_ReadySynthetic", "Klar — trykk Start (syntetisk testlyd — ingen mikrofon funnet, økten lagres ikke).")
            : Localized.Get("ExRunVm_Ready", "Klar — trykk Start for å begynne.");
    }

    public EnhancedExercise Exercise { get; }

    /// <summary>Read-only target-profile metadata for the "Mål-profil" panel (display only).</summary>
    public ExerciseRuntimeTargetProfileDisplay TargetProfile { get; }

    // ── Exercise Guidance panel (WPF ExerciseWindow) ─────────────────────────────────────────────────────────────
    /// <summary>One guidance card: a localized heading + body (from the exercise's profile guidance keys).</summary>
    public sealed record GuidanceItem(string Heading, string Body);
    public IReadOnlyList<GuidanceItem> GuidanceItems { get; } = System.Array.Empty<GuidanceItem>();
    public bool HasGuidance => GuidanceItems.Count > 0;
    public string GuidanceHeading => Localized.Get("Guidance_PanelTitle", "Veiledning");
    /// <summary>The feedback-mode badge text (e.g. resonance/pitch feedback), from the profile's FeedbackModeKey.</summary>
    public string FeedbackModeText { get; } = "";
    public bool HasFeedbackMode => FeedbackModeText.Length > 0;

    /// <summary>Display-only hold target (from the profile's RequiredHoldSeconds when available).</summary>
    public string HoldTargetDescription => string.Format(Localized.Get("ExRunVm_HoldTarget", "Mål: hold i {0} s (visning)"), _holdTargetSeconds.ToString("F0"));

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
    public string SafetyNote => Localized.Get("ExRunVm_SafetyNote",
        "Øv uten press: stopp ved ubehag, slapp av i hals/skuldre, og ta pauser. " +
        "Helse og sikkerhet går alltid foran tonehøyde.");

    // ── Focus-aware wording (display-only — reflects the exercise's goal/category, NOT always pitch) ──
    /// <summary>Focus label from the exercise goal (Tonehøyde/Resonans/Intonasjon/Pust/Kombinert).</summary>
    public string FocusLabel => ExerciseDisplay.Goal(Exercise.Goal);
    /// <summary>Focus-specific one-line summary so non-pitch exercises don't read as pitch-centric.</summary>
    public string FocusSummary => ExerciseDisplay.FocusSummary(Exercise.Goal);
    /// <summary>True when tonehøyde is the PRIMARY focus (Pitch/Combined) — show the pitch target prominently.</summary>
    public bool IsPitchFocused => ExerciseDisplay.IsPitchPrimary(Exercise.Goal);

    // Focus-aware live-chart labels: a resonance exercise shows a brightness chart (Lysere/Mørkere), a pitch
    // exercise shows the pitch chart (Høyere/Lavere). Bound by the view; evaluated once (focus is fixed per session).
    public string LiveChartTitle => _resonanceFocused
        ? Localized.Get("ExRun_ResChartTitle", "Resonans (lysere til høyre)")
        : Localized.Get("Dash_PitchChartTitle", "Tonehøyde (nyeste til høyre)");
    public string AxisHighLabel => _resonanceFocused
        ? Localized.Get("ExRun_AxisBrighter", "Lysere")
        : Localized.Get("Dash_AxisHigher", "Høyere");
    public string AxisLowLabel => _resonanceFocused
        ? Localized.Get("ExRun_AxisDarker", "Mørkere")
        : Localized.Get("Dash_AxisLower", "Lavere");
    /// <summary>Empty-state hint under the live chart — focus-aware (brightness for resonance, pitch otherwise).</summary>
    public string LiveChartEmptyHint => _resonanceFocused
        ? Localized.Get("ExRun_ResHint", "Si en jevn tone — lysheten vises her.")
        : Localized.Get("ExRun_PitchHint", "Si en jevn tone — tonehøyden vises her.");
    /// <summary>For non-pitch exercises, the pitch range is shown only as a SECONDARY technical detail.</summary>
    public bool ShowSecondaryPitch => !IsPitchFocused && TargetPitchMin > 0 && TargetPitchMax > 0;
    public string SecondaryPitchText => string.Format(Localized.Get("ExRunVm_SecondaryPitch", "Tekniske mål (tonehøyde): {0}"), TargetPitchText);

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private double _currentPitch;
    /// <summary>Live real resonance readout (brightness via VoiceBrightnessMeter), e.g. "Lys (72)". "—" when no voice.</summary>
    [ObservableProperty] private string _currentResonance = "—";
    [ObservableProperty] private string _pitchStatus = "—";
    /// <summary>Live voice-health readout (same LiveMetricsService engine as the dashboard): Trygt/Observer/Advarsel/Fare.</summary>
    [ObservableProperty] private string _healthStatusDisplay = "—";
    /// <summary>True when live strain reaches Warning/Danger — the view shows a prominent take-a-break banner.</summary>
    [ObservableProperty] private bool _hasHealthWarning;
    /// <summary>The take-a-break message shown when strain is detected (safety-first, mirrors the dashboard).</summary>
    public string HealthWarningText => Localized.Get("Feedback_TakeBreak", "Ta en pause og slapp av i stemmen.");
    [ObservableProperty] private int _elapsedSeconds;
    public string ElapsedText => $"{ElapsedSeconds / 60}:{ElapsedSeconds % 60:00}";
    partial void OnElapsedSecondsChanged(int value) => OnPropertyChanged(nameof(ElapsedText));
    [ObservableProperty] private double _holdSeconds;
    [ObservableProperty] private double _holdProgressPercent;
    [ObservableProperty] private string _runtimeStatusMessage = Localized.Get("ExRunVm_GettingReady", "Gjør deg klar …");

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
        RuntimePhase.Inactive => Localized.Get("ExRunVm_Phase_Ready", "Klar"),
        RuntimePhase.Active => IsSyntheticSource ? Localized.Get("ExRunVm_Phase_ActiveSynth", "Aktiv (syntetisk testlyd)") : Localized.Get("ExRunVm_Phase_Active", "Aktiv"),
        RuntimePhase.Stopped => Localized.Get("ExRunVm_Phase_Stopped", "Stoppet"),
        _ => "—",
    };
    /// <summary>Display-only recommended duration from the exercise definition (read-only).</summary>
    public string RecommendedDurationText => Exercise.DurationMinutes > 0
        ? string.Format(Localized.Get("ExRunVm_RecDuration", "Anbefalt varighet: {0} min (veiledende)"), Exercise.DurationMinutes)
        : Localized.Get("ExRunVm_RecDurationNone", "Anbefalt varighet: —");
    /// <summary>Truthful save note: real runs are saved and count toward progression; synthetic test-tone runs are not.</summary>
    public string NotSavedNote => SavesRealSession
        ? Localized.Get("ExRunVm_SaveNote_Real", "Økten lagres og teller mot progresjonen din.")
        : IsSyntheticSource
            ? Localized.Get("ExRunVm_SaveNote_Synth", "Syntetisk testlyd — økten lagres ikke.")
            : Localized.Get("ExRunVm_SaveNote_Local", "Økten lagres lokalt.");

    // ── Truthful live-panel headings (bound by the view; conditional on real vs synthetic source) ─────────────────
    /// <summary>Live-readout heading — notes the synthetic test-tone only when there is no real mic.</summary>
    public string LiveHeading => IsSyntheticSource ? Localized.Get("ExRunVm_LiveHeading_Synth", "Sanntid (syntetisk testlyd)") : Localized.Get("Dash_LiveHeading", "Sanntid");
    /// <summary>Pre-start hint — truthful about the source and whether the run saves.</summary>
    public string ReadyToStartText => IsSyntheticSource
        ? Localized.Get("ExRunVm_ReadyToStart_Synth", "Ingen mikrofon funnet — Start kjører en syntetisk testlyd (lagres ikke).")
        : Localized.Get("ExRunVm_ReadyToStart_Real", "Klar til å starte — trykk Start for å øve med mikrofonen din.");

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
    [ObservableProperty] private string _liveFeedbackMessage = Localized.Get("ExRunVm_GettingReady", "Gjør deg klar …");
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
    // Focus-aware feedback: for a resonance exercise the message is about BRIGHTNESS (too dark/too bright/nice), for a
    // pitch exercise about pitch (under/over/in-range). focusValue/min/max are the resonance-% or pitch band already.
    private (string message, string severity) DeriveLiveFeedback(bool voiced, bool healthWarning, double focusValue, double focusMin, double focusMax, ExerciseLiveState? live)
    {
        // Voice health takes PRIORITY over pitch/resonance feedback — safety first (mirrors the dashboard).
        if (healthWarning)
            return (Localized.Get("Feedback_TakeBreak", "Ta en pause og slapp av i stemmen."), "Helse");
        if (live?.IsSafetyLocked == true)
            return (Localized.Get("ExRunVm_Fb_Pause", "Koordinator anbefaler en pause (veiledende)"), "Pause anbefalt");
        if (!voiced)
            return (Localized.Get("ExRunVm_Fb_NoStableVoice", "Ingen stabil stemme registrert"), "Ingen stemme");
        if (_resonanceFocused)
        {
            if (focusValue < focusMin) return (Localized.Get("ExRunVm_Fb_ResTooDark", "Litt for mørk — lysne klangen"), "Juster");
            if (focusValue > focusMax) return (Localized.Get("ExRunVm_Fb_ResTooBright", "Litt for lys — slipp litt tilbake"), "Juster");
            return (Localized.Get("ExRunVm_Fb_ResInRange", "Fin, lys resonans"), "I mål");
        }
        if (focusValue <= 0) return (Localized.Get("ExRunVm_Fb_NoStableVoice", "Ingen stabil stemme registrert"), "Ingen stemme");
        if (focusValue < focusMin) return (Localized.Get("ExRunVm_Fb_SlightlyUnder", "Litt under målområdet"), "Juster");
        if (focusValue > focusMax) return (Localized.Get("ExRunVm_Fb_SlightlyOver", "Litt over målområdet"), "Juster");
        return (Localized.Get("ExRunVm_Fb_InRange", "Innenfor målområdet"), "I mål");
    }

    // Empty chart with the focus-correct target band: resonance band (0–100 axis) for resonance exercises, pitch band
    // otherwise. Mirrors the ctor so a mid-session reset/stop keeps the same green band the running chart used.
    private RuntimeChartDisplay EmptyFocusChart() => _resonanceFocused
        ? RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, _targetResMinPct, _targetResMaxPct)
        : RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, TargetPitchMin, TargetPitchMax);

    [RelayCommand]
    private void Begin()
    {
        if (IsRunning) return;
        // Snapshot the per-user resonance-brightness baseline once per session (avoids a prefs file read per frame);
        // null when the user hasn't calibrated → the meter uses its fixed provisional anchors.
        _resonanceBaseline = FemVoice.Avalonia.Preferences.CapturePreferences.ResonanceBaselineCentroidHz();
        _stabilizer.Reset();
        _metrics.Reset();
        _sessionPitch.Clear();
        _sessionResonance.Clear();
        _sessionHealth.Clear();
        HealthStatusDisplay = "—";
        HasHealthWarning = false;
        _holdRaw = 0;
        _peakHoldPercent = 0;
        HoldSeconds = 0;
        HoldProgressPercent = 0;
        ElapsedSeconds = 0;
        RuntimePitchSamples.Clear();
        RuntimeChart = EmptyFocusChart();
        LiveFeedbackMessage = Localized.Get("ExRunVm_Fb_SteadyTone", "Si en jevn tone i målområdet.");
        LiveFeedbackSeverity = "Nøytral";
        SessionEndedSummary = "";
        _startUtc = DateTime.UtcNow;
        _lastFrameUtc = _startUtc;
        IsRunning = true;
        Phase = RuntimePhase.Active;
        RuntimeStatusMessage = Localized.Get("ExRunVm_Status_InProgress", "Øvelse i gang — hold tonen i målområdet.");
        StartCoordinatorReadout();
        _ = _capture.StartAsync(new AudioCaptureOptions(SampleRate, DeviceId: FemVoice.Avalonia.Preferences.CapturePreferences.SelectedMicDeviceId()));
        _voiceMonitor.Start(SampleRate);   // hear-own-voice (opt-in; no-op when off)
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
        _voiceMonitor.Stop();
        // Clear the VM-local coordinator's in-memory state (safe no-op when inactive; persists nothing).
        if (_coordinatorEnabled) _coordinator.StopExercise();
        // Persist the finished exercise (off the UI thread) BEFORE clearing live values. A real microphone run with a
        // DB saves a real TrainingSession that counts toward progression; otherwise a local history record is kept.
        bool saved = SaveFinishedSession();

        _ui.Post(() =>
        {
            // Build the truthful session-ended summary from the last live values BEFORE clearing them.
            string tail = saved
                ? Localized.Get("ExRunVm_Tail_Saved", "Lagret — teller mot progresjonen din.")
                : IsSyntheticSource ? Localized.Get("ExRunVm_Tail_Synth", "Syntetisk testlyd — ikke lagret.") : Localized.Get("ExRunVm_Tail_Local", "Lagret lokalt.");
            SessionEndedSummary = string.Format(
                Localized.Get("ExRunVm_SessionEnded", "Økt fullført · varighet {0} · beste hold {1} %. {2}"),
                ElapsedText, _peakHoldPercent.ToString("F0"), tail);

            IsRunning = false;
            Phase = RuntimePhase.Stopped;
            HasHealthWarning = false;
            HealthStatusDisplay = "—";
            PitchStatus = Localized.Get("ExRunVm_Phase_Stopped", "Stoppet");
            RuntimeStatusMessage = Localized.Get("ExRunVm_ExerciseStopped", "Øvelse stoppet.");
            CoordinatorReadout = ExerciseCoordinatorReadoutDisplay.Inactive();
            RuntimePitchSamples.Clear();
            RuntimeChart = EmptyFocusChart();
            LiveFeedbackMessage = Localized.Get("ExRunVm_ExerciseStopped", "Øvelse stoppet.");
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
        // Bail on any in-flight frame once the session has stopped / the VM is being disposed (IsRunning is cleared
        // first in Stop/Dispose). Prevents an orphan pitch sample being posted after navigate-away — the per-frame
        // work (pitch + resonance DSP) widened that race, so guard explicitly.
        if (!IsRunning) return;
        _voiceMonitor.Feed(e.Samples);                 // hear-own-voice: play the frame back (no-op when off)
        var now = DateTime.UtcNow;
        double delta = (now - _lastFrameUtc).TotalSeconds;
        _lastFrameUtc = now;

        PitchAnalysisResult result = _pitch.DetectPitch(e.Samples);
        // Live BRIGHTNESS via the monotonic VoiceBrightnessMeter (proper spectral centroid → 0–100). This replaces the
        // old ResonanceProxyEngine score, which stuck low ("always Mørk") on real mics because its formant peak
        // detection kept falling back to fixed values and froze the score. The meter responds to the actual voice and
        // is loudness-independent, so a resonance-focused exercise's target band is reachable by brightening the voice.
        int resonancePct = result.IsVoiced ? FemVoiceStudio.Audio.VoiceBrightnessMeter.BrightnessPercent(e.Samples, SampleRate, _resonanceBaseline) : 0;
        if (result.IsVoiced && IsRunning) _sessionResonance.Add(resonancePct);   // real per-session resonance → saved average
        double smoothed = _metrics.CalculateSmoothedPitch(result.Pitch, result.IsVoiced);
        double pitch = result.IsVoiced ? _stabilizer.Filter(smoothed, now) : 0;

        if (result.IsVoiced && pitch > 0) _sessionPitch.Add(pitch);   // real per-session sample → saved session stats

        // Real voice-HEALTH monitoring (same LiveMetricsService engine as the dashboard). Detects strain during the
        // exercise so we can warn the user to take a break — safety-first, exactly like the dashboard.
        HealthState health = result.IsVoiced ? _metrics.CalculateHealth(0, smoothed, result.Intensity) : HealthState.NoVoice;
        if (result.IsVoiced) _sessionHealth.Add(HealthTo100(health));
        bool healthWarning = health is HealthState.Warning or HealthState.Danger;

        // FOCUS-AWARE: a resonance exercise is judged on live brightness vs the resonance target band; a pitch
        // exercise on pitch vs the pitch band. focusValue/focusMin/focusMax drive in-range, hold, chart and feedback.
        double focusValue = _resonanceFocused ? resonancePct : pitch;
        double focusMin = _resonanceFocused ? _targetResMinPct : TargetPitchMin;
        double focusMax = _resonanceFocused ? _targetResMaxPct : TargetPitchMax;
        bool inRange = result.IsVoiced && focusValue >= focusMin && focusValue <= focusMax;
        // INTERNAL status token (Norwegian, used for comparison + chart snapshot); localized only at display time.
        string status;
        if (!result.IsVoiced) status = "Ingen stemme";
        else if (focusValue < focusMin) status = _resonanceFocused ? "Resonans: for mørk" : "Under målområde";
        else if (focusValue > focusMax) status = _resonanceFocused ? "Resonans: for lys" : "Over målområde";
        else status = "Innenfor målområde";

        _holdRaw = inRange ? Math.Min(_holdTargetSeconds, _holdRaw + delta) : 0;
        double hold = _holdRaw;
        int elapsed = (int)(now - _startUtc).TotalSeconds;

        // Feed the VM-local coordinator READ-ONLY: pitch, resonance AND health are now the REAL measured signals
        // (resonance/brightness from the VoiceBrightnessMeter, health from LiveMetricsService); only stability stays a
        // documented neutral placeholder. Read its in-memory hold/state for the advisory readout.
        ExerciseCoordinatorReadoutDisplay? readout = null;
        if (_coordinatorEnabled && _coordinator.IsExerciseActive)
        {
            _coordinator.UpdateMetrics(resonancePct, pitch, CoordStabilityPlaceholder, HealthTo100(health));
            readout = ExerciseCoordinatorReadoutDisplay.From(
                _coordinator.IsExerciseActive, _coordinator.GetHoldProgress(),
                _holdTargetSeconds, _latestLiveState, hold);
        }

        // Converter-free chart snapshot + local display-only feedback (computed off the UI thread). The chart plots
        // the FOCUS metric (brightness for resonance exercises, pitch otherwise) against its target band.
        bool hasVoice = result.IsVoiced && pitch > 0;
        double chartValue = _resonanceFocused ? resonancePct : pitch;
        double samplePx = hasVoice ? RuntimeChartDisplay.ToPx(chartValue, _chartMin, _chartMax, ChartHeightPx) : 0;
        var chartSnap = RuntimeChartDisplay.From(
            ChartHeightPx, _chartMin, _chartMax, focusMin, focusMax,
            chartValue, hasVoice, PitchStatusDisplay(hasVoice ? status : "Ingen stemme"));
        (string fbMsg, string fbSev) = DeriveLiveFeedback(result.IsVoiced, healthWarning, focusValue, focusMin, focusMax,
            _coordinatorEnabled ? _latestLiveState : null);
        _ui.Post(() =>
        {
            if (!IsRunning) return;   // VM stopped/disposed between capture and dispatch → don't apply orphan updates
            CurrentPitch = result.IsVoiced ? Math.Round(pitch, 1) : 0;
            CurrentResonance = result.IsVoiced ? ResonanceText(resonancePct) : "—";
            HealthStatusDisplay = HealthText(health);
            HasHealthWarning = healthWarning;
            PitchStatus = PitchStatusDisplay(status);
            HoldSeconds = Math.Round(hold, 1);
            HoldProgressPercent = Math.Round(hold / _holdTargetSeconds * 100.0, 0);
            if (HoldProgressPercent > _peakHoldPercent) _peakHoldPercent = HoldProgressPercent;
            ElapsedSeconds = elapsed;
            RuntimeStatusMessage = inRange
                ? (hold >= _holdTargetSeconds ? Localized.Get("ExRunVm_Status_HoldingGreat", "Flott — du holder målområdet!") : Localized.Get("ExRunVm_Status_HoldSteady", "Bra — hold tonen rolig i målområdet."))
                : status == "Ingen stemme" ? Localized.Get("ExRunVm_Status_SayTone", "Si en jevn tone for å begynne.") : Localized.Get("ExRunVm_Status_Adjust", "Juster tonen mot målområdet.");
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

    // Qualitative label + value for the live resonance readout (0–100). Mirrors WPF's bright/neutral/dark buckets.
    private static string ResonanceText(int pct) => pct switch
    {
        >= 67 => string.Format(Localized.Get("Resonance_Bright", "Lys ({0})"), pct),
        >= 34 => string.Format(Localized.Get("Resonance_Neutral", "Nøytral ({0})"), pct),
        _ => string.Format(Localized.Get("Resonance_Dark", "Mørk ({0})"), pct),
    };

    // Voice-health mapping — same buckets/keys as the dashboard so the exercise readout matches.
    private static double HealthTo100(HealthState h) => h switch
    {
        HealthState.Safe => 100, HealthState.Monitor => 70, HealthState.Warning => 45, HealthState.Danger => 20, _ => 0,
    };
    private static string HealthText(HealthState h) => h switch
    {
        HealthState.NoVoice => "—",
        HealthState.Safe => Localized.Get("Health_Safe", "Trygt"),
        HealthState.Monitor => Localized.Get("Health_Monitor", "Observer"),
        HealthState.Warning => Localized.Get("Health_Warning", "Advarsel"),
        HealthState.Danger => Localized.Get("Health_Danger", "Fare"),
        _ => "—",
    };

    // Localize an INTERNAL pitch-status token for DISPLAY only (the raw token stays Norwegian for comparison/chart).
    private static string PitchStatusDisplay(string token) => token switch
    {
        "Ingen stemme" => Localized.Get("Signal_NoVoice", "Ingen stemme"),
        "Under målområde" => Localized.Get("ExRun_Status_Under", "Under målområde"),
        "Over målområde" => Localized.Get("ExRun_Status_Over", "Over målområde"),
        "Innenfor målområde" => Localized.Get("ExRun_Status_In", "Innenfor målområde"),
        "Resonans: for mørk" => Localized.Get("ExRun_Status_ResTooDark", "Resonans: for mørk"),
        "Resonans: for lys" => Localized.Get("ExRun_Status_ResTooBright", "Resonans: for lys"),
        _ => token,
    };

    /// <summary>Persist the finished exercise. A real-microphone run with a DB saves a real TrainingSession (so it
    /// counts toward progression, streak and recent sessions — exactly like a dashboard session); otherwise it keeps a
    /// local history record. Returns true only when a real DB session was saved. Never throws to the app.</summary>
    private bool SaveFinishedSession()
    {
        int elapsed = ElapsedSeconds;
        if (elapsed < 2) return false;   // skip trivial runs (matches the dashboard threshold)

        if (SavesRealSession)
        {
            try
            {
                var voiced = _sessionPitch.Where(p => p > 0).ToList();
                double avg = voiced.Count > 0 ? voiced.Average() : 0;
                double inZone = voiced.Count > 0
                    ? 100.0 * voiced.Count(p => p >= TargetPitchMin && p <= TargetPitchMax) / voiced.Count : 0;
                double avgResonance = _sessionResonance.Count > 0 ? _sessionResonance.Average() : 0;
                double avgHealth = _sessionHealth.Count > 0 ? _sessionHealth.Average() : 0;
                double pitchVariation = 0;
                if (voiced.Count > 1)
                {
                    double mean = voiced.Average();
                    pitchVariation = Math.Sqrt(voiced.Sum(p => (p - mean) * (p - mean)) / voiced.Count);
                }
                var session = new FemVoiceStudio.Models.TrainingSession
                {
                    UserId = 1,
                    StartTime = _startUtc,
                    EndTime = DateTime.UtcNow,
                    AveragePitch = Math.Round(avg, 1),
                    MinPitch = voiced.Count > 0 ? Math.Round(voiced.Min(), 1) : 0,
                    MaxPitch = voiced.Count > 0 ? Math.Round(voiced.Max(), 1) : 0,
                    PitchVariation = Math.Round(pitchVariation, 1),          // real prosody metric (std-dev of pitch)
                    OverallScore = Math.Round(inZone),                       // adherence to THIS exercise's target band
                    ResonanceScore = Math.Round(avgResonance, 1),            // real resonance from the Core DSP engine
                    VoiceHealthScore = Math.Round(avgHealth, 1),            // real per-session voice-health average
                    DifficultyLevel = Exercise.Difficulty,
                    ExerciseTextId = Exercise.Id,   // catalog id (WPF parity) so per-exercise progress can be keyed by id
                    Feedback = $"Øvelse: {SelectedExerciseName}",
                };
                // Create-then-enrich two-step (ResonanceScore is only written by UpdateTrainingSession), same as the
                // dashboard. Both are existing Core APIs — no Core change.
                int savedId = _database!.SaveTrainingSession(session);
                if (savedId > 0 && avgResonance > 0)
                {
                    session.Id = savedId;
                    _database.UpdateTrainingSession(session);
                }
                return savedId > 0;
            }
            catch { return false; }   // never surface a session-save error to the app
        }

        // Fallback: local display history (no DB, or a synthetic test-tone run — not real training data).
        try
        {
            _history?.Append(new History.SessionRecord
            {
                WhenUtcTicks = DateTime.UtcNow.Ticks,
                Source = SelectedExerciseName,
                DurationSeconds = elapsed,
                Note = IsSyntheticSource ? "Øvelse · syntetisk testlyd · lokal historikk" : "Øvelse · lokal historikk",
            });
        }
        catch { /* best effort */ }
        return false;
    }

    public void Dispose()
    {
        IsRunning = false;                       // mark stopped (also when navigated away via top nav)
        _capture.FrameAvailable -= OnFrameAvailable;
        _voiceMonitor.Dispose();
        _ = _capture.StopAsync();                // stops the capture loop (synthetic or real) — no more frames
        (_capture as IDisposable)?.Dispose();    // release a real capture backend (e.g. ALSA) if used
        if (_coordinatorEnabled) _coordinator.ExerciseUpdated -= OnCoordinatorState;
        _coordinator.StopExercise();             // in-memory clear; no persistence
        _coordinator.Dispose();
    }
}
