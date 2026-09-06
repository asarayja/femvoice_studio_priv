using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio;                 // PitchDetectionService
using FemVoiceStudio.Audio.Abstractions;    // IAudioCaptureService, SyntheticAudioMode
using FemVoiceStudio.Core.Platform;         // IUiDispatcher
using FemVoiceStudio.Models;                // PitchAnalysisResult, StabilityState, HealthState
using FemVoiceStudio.Services;              // PitchTraceStabilizer, LiveMetricsService, PitchTargetZonePolicy
using FemVoice.Avalonia.Localization;       // Localized (WPF-parity strings from the shared RESX)

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
    // "Hear your own voice" — routes captured frames to the speaker while recording (opt-in; no-op when off/unavailable).
    private readonly FemVoice.Avalonia.Audio.VoiceMonitor _voiceMonitor = new();
    private double? _resonanceBaseline;   // per-user calibrated relaxed-voice centroid (Hz); null = use fixed anchors
    private readonly List<double> _sessionResonance = new();   // per-session samples → saved average
    // Per-frame health/stability states mapped to 0–100, accumulated during a recording → per-dimension VI scores.
    private readonly List<double> _sessionHealth = new();
    private readonly List<double> _sessionStability = new();
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
        _capture.FrameAvailable += OnFrameAvailable;
        _capture.DeviceLost += OnDeviceLost;
        UpdateComfortZone();
        LoadExercise();   // seed the exercise-text panel with the first sentence for the default difficulty
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
        RefreshProgression();
        RefreshReminder();
    }

    // Recent session start times in LOCAL time (for the reminder scheduler): from the real DB when available, else the
    // display-only local history store. Best-effort — a read error yields an empty list (→ reminder simply "owed").
    private System.Collections.Generic.List<DateTime> RecentSessionLocalTimes()
    {
        var list = new System.Collections.Generic.List<DateTime>();
        try
        {
            if (_database is not null)
                foreach (var s in _database.GetRecentSessions(30)) list.Add(s.StartTime.ToLocalTime());
            else
                foreach (var r in _history.Recent(30)) list.Add(new DateTime(r.WhenUtcTicks, DateTimeKind.Utc).ToLocalTime());
        }
        catch { /* empty list is a safe default */ }
        return list;
    }

    // Evaluate the in-app daily reminder from the opt-in prefs + real history via the pure Core scheduler, and compose
    // an encouraging, streak-aware message. Never surfaces while recording (a nudge mid-session is pointless).
    private void RefreshReminder()
    {
        if (IsRecording) { ShowReminder = false; return; }
        var status = FemVoiceStudio.Services.TrainingReminderScheduler.Evaluate(
            FemVoice.Avalonia.Preferences.ReminderPreferences.RemindersEnabled(),
            _selectedFrequencyDays(),
            FemVoice.Avalonia.Preferences.ReminderPreferences.ReminderTimeOfDay(),
            RecentSessionLocalTimes(),
            DateTime.Now);

        ShowReminder = status.State == FemVoiceStudio.Services.ReminderState.Due;
        if (ShowReminder)
        {
            ReminderMessage = Localized.Get("Reminder_DueMessage", "Dagens økt gjenstår — noen minutter holder.");
            ReminderStreakNote = _currentStreak >= 2
                ? string.Format(Localized.Get("Reminder_StreakNote", "Behold rekken din på {0} dager."), _currentStreak)
                : "";
        }
    }

    // The user's weekly training goal (days/week) from the persisted onboarding/Settings preference; 3 when unreadable.
    private static int _selectedFrequencyDays()
    {
        try { return new FemVoice.Avalonia.Preferences.UiPreferencesStore().Load().TrainingFrequency; }
        catch { return 3; }
    }

    // ── Daily training reminder (in-app nudge) ────────────────────────────────────────────────────────────────────
    // A humane "today's session is still owed" banner, derived (no timer) from the weekly goal + real session history
    // by the pure Core TrainingReminderScheduler. Opt-in; only surfaces at/after the user's preferred time, never twice
    // a day, never past the weekly goal. Groundwork for a later OS-notification bridge.
    [ObservableProperty] private bool _showReminder;
    [ObservableProperty] private string _reminderMessage = "";
    [ObservableProperty] private string _reminderStreakNote = "";
    private int _currentStreak;
    public string ReminderHeading => Localized.Get("Reminder_Heading", "Påminnelse");

    // ── "Din progresjon" block (ported from the WPF MainWindow) — real level/streak/totals from the DB ──────────
    [ObservableProperty] private bool _hasProgression;
    [ObservableProperty] private string _progLevelName = "—";
    [ObservableProperty] private string _progTotalSessions = "0";
    [ObservableProperty] private string _progStreak = "0";
    [ObservableProperty] private string _progToNext = "";
    [ObservableProperty] private double _progPercent;
    public string ProgressionHeading => Localized.Get("Main_YourProgress", "Din progresjon");
    public string ProgTotalLabel => Localized.Get("Main_TotalSessions", "Totalt antall økter");
    public string ProgStreakLabel => Localized.Get("Main_Streak", "Dager på rad");
    public string ProgLevelLabel => Localized.Get("Main_CurrentLevel", "Nåværende nivå");

    private void RefreshProgression()
    {
        if (_database is null) { HasProgression = false; return; }
        try
        {
            var status = new FemVoiceStudio.Services.ProgressionService(_database, LocalizationService.Instance).GetProgressionStatus();
            var level = FemVoiceStudio.Services.LevelClassificationSystem.FromDifficultyLevel(status.CurrentLevel);
            ProgLevelName = FemVoiceStudio.Services.LevelClassificationSystem.GetLevelName(level);
            ProgTotalSessions = status.TotalSessions.ToString();
            ProgStreak = status.CurrentStreak.ToString();
            _currentStreak = status.CurrentStreak;
            if (status.SessionsRequiredForPromotion > 0)
            {
                ProgPercent = System.Math.Round(System.Math.Clamp(100.0 * status.SessionsAtCurrentLevel / status.SessionsRequiredForPromotion, 0, 100));
                ProgToNext = Localized.Get("Main_SessionsToNextLevel", "Økter til neste nivå") +
                             $": {System.Math.Max(0, status.SessionsRequiredForPromotion - status.SessionsAtCurrentLevel)}";
            }
            HasProgression = true;
        }
        catch { HasProgression = false; }
    }

    // ── Live state (bound by the dashboard) ───────────────────────────────────
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private double _currentPitch;
    [ObservableProperty] private string _pitchStability = "—";
    [ObservableProperty] private string _currentSignalStatus = Localized.Get("Signal_NoVoice", "Ingen stemme");
    [ObservableProperty] private string _currentFeedbackMessage = Localized.Get("Dash_PressStartFeedback", "Trykk Start for å begynne.");
    [ObservableProperty] private string _healthStatusDisplay = "—";
    /// <summary>Live real resonance readout (brightness via VoiceBrightnessMeter), e.g. "Lys (72)". "—" when no voice.</summary>
    [ObservableProperty] private string _resonanceDisplay = "—";
    [ObservableProperty] private double _comfortZoneLow = 150;
    [ObservableProperty] private double _comfortZoneHigh = 220;

    // Comfort-zone COMPLIANCE (WPF principle A7: the UI exposes NO raw Hz — the zone is communicated as
    // in-zone/below/above compliance, not a number to hit). Replaces the raw current-pitch Hz readout.
    [ObservableProperty] private string _comfortZoneStatus = "—";
    [ObservableProperty] private bool _inComfortZone;

    // ── Perceived-voice mirror ────────────────────────────────────────────────────────────────────────────────────
    // The question a trainee actually asks — "does my voice read feminine yet, and if not what do I fix?" — answered
    // honestly from the two strongest cues (pitch + calibrated resonance) via the pure Core VoicePerceptionEstimator.
    // Both ingredient scores are surfaced (not a black box), plus the single highest-leverage next step.
    [ObservableProperty] private bool _hasPerception;
    [ObservableProperty] private string _perceptionLabel = "—";
    [ObservableProperty] private string _perceptionTip = "";
    [ObservableProperty] private int _perceptionScore;
    [ObservableProperty] private int _perceptionPitchScore;
    [ObservableProperty] private int _perceptionResonanceScore;
    /// <summary>View-styling token for the current band: feminine / androgynous / masculine / none.</summary>
    [ObservableProperty] private string _perceptionBand = "none";
    public string PerceptionHeading => Localized.Get("Dash_Perception_Heading", "Slik leser stemmen din nå");
    public string PerceptionExplainer => Localized.Get("Dash_Perception_Explainer",
        "Et anslag ut fra tonehøyde + resonans — ikke en fasit. Kalibrer mikrofonen for et mer presist anslag.");
    public string PerceptionPitchLabel => Localized.Get("Dash_Perception_PitchLabel", "Tonehøyde");
    public string PerceptionResonanceLabel => Localized.Get("Dash_ResonanceLabel", "Resonans");

    // Localized section headings (shared RESX keys WPF uses) so the dashboard text matches WPF and relocalizes.
    public string ComfortZoneLabel => Localized.Get("Main_ComfortZone", "Komfortsone");
    public string FeedbackHeading => Localized.Get("Main_Feedback", "Tilbakemelding");
    public string PitchGraphHeading => Localized.Get("Main_PitchGraph", "Pitch-graf");

    /// <summary>One difficulty choice + its localized label (shared Difficulty_* keys, like WPF's buttons).</summary>
    public sealed record DifficultyOption(DifficultyLevel Value, string Label);
    public IReadOnlyList<DifficultyOption> DifficultyOptions { get; } = new[]
    {
        new DifficultyOption(DifficultyLevel.Nybegynner, Localized.Get("Difficulty_Beginner", "Nybegynner")),
        new DifficultyOption(DifficultyLevel.Middels, Localized.Get("Difficulty_Intermediate", "Middels")),
        new DifficultyOption(DifficultyLevel.Avansert, Localized.Get("Difficulty_Advanced", "Avansert")),
    };

    [ObservableProperty] private DifficultyLevel _selectedDifficulty = DifficultyLevel.Nybegynner;
    partial void OnSelectedDifficultyChanged(DifficultyLevel value)
    {
        UpdateComfortZone();
        OnPropertyChanged(nameof(SelectedDifficultyOption));
        OnPropertyChanged(nameof(ExerciseDifficultyBadge));
        _exerciseIndex = 0;   // first matching sentence for the newly chosen difficulty
        LoadExercise();
    }

    /// <summary>Selected difficulty as its display option (two-way bound by the ComboBox; keeps SelectedDifficulty in sync).</summary>
    public DifficultyOption SelectedDifficultyOption
    {
        get => DifficultyOptions.FirstOrDefault(o => o.Value == SelectedDifficulty) ?? DifficultyOptions[0];
        set { if (value is not null && value.Value != SelectedDifficulty) SelectedDifficulty = value.Value; }
    }

    // ── Exercise-text panel (WPF front-page parity) ───────────────────────────────────────────────────────────────
    // WPF's dashboard shows a sentence to READ at the current difficulty plus a difficulty badge (MainWindow.xaml
    // "Exercise Text at Bottom"). Sentences come from the SHARED Core ExerciseTextService — the same catalogue WPF
    // uses — with localized content and the model's Norwegian seed as fallback. Changing difficulty or pressing
    // "Neste tekst" loads a matching sentence. Read-only; no clinical/scoring behaviour.
    private readonly FemVoiceStudio.Services.ExerciseTextService _exercise = new();
    private int _exerciseIndex;   // deterministic cycle index (not random) so the panel + smokes are stable

    public string ExerciseTextHeading => Localized.Get("Main_ExerciseText", "Øvelsestekst");
    public string NextExerciseLabel => Localized.Get("Main_NextText", "Neste tekst");
    public string ExerciseCategoryLabel => Localized.Get("Main_Category", "Kategori");
    /// <summary>Difficulty label shown as a badge beside the exercise text (mirrors WPF's difficulty chip).</summary>
    public string ExerciseDifficultyBadge => SelectedDifficultyOption.Label;

    [ObservableProperty] private string _currentExerciseText = string.Empty;
    [ObservableProperty] private string _currentExerciseTitle = string.Empty;
    [ObservableProperty] private string _currentExerciseCategory = string.Empty;

    /// <summary>Advance to the next sentence for the current difficulty (deterministic cycle).</summary>
    [RelayCommand]
    private void NextExercise() { _exerciseIndex++; LoadExercise(); }

    /// <summary>Load the exercise sentence at the current cycle index for the selected difficulty. Prefers localized
    /// content/title/category; falls back to the model's Norwegian seed when a resource key is absent.</summary>
    private void LoadExercise()
    {
        var texts = _exercise.GetTextsByDifficulty(SelectedDifficulty);
        var ex = texts.Count > 0
            ? texts[((_exerciseIndex % texts.Count) + texts.Count) % texts.Count]
            : _exercise.GetRandomText(SelectedDifficulty);   // GetDefaultText fallback when the catalogue is empty
        CurrentExerciseText = LocalizedOrSeed(_exercise.GetLocalizedContent(ex.Id), $"Exercise_{ex.Id}_Content", ex.Content);
        CurrentExerciseTitle = LocalizedOrSeed(_exercise.GetLocalizedTitle(ex.Id), $"Exercise_{ex.Id}_Title", ex.Title);
        CurrentExerciseCategory = LocalizedOrSeed(_exercise.GetLocalizedCategory(ex.Id), $"Exercise_{ex.Id}_Category", ex.Category);
    }

    // The Core localization indexer echoes the key back when a string is missing; treat that (or empty) as "no
    // translation" and use the model's seed text so a real sentence always shows.
    private static string LocalizedOrSeed(string localized, string key, string seed)
        => string.IsNullOrWhiteSpace(localized) || localized == key ? seed : localized;

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
        // Snapshot the per-user resonance-brightness baseline once per session (no prefs read per frame); null when
        // the user hasn't calibrated → the meter uses its fixed provisional anchors.
        _resonanceBaseline = FemVoice.Avalonia.Preferences.CapturePreferences.ResonanceBaselineCentroidHz();
        _stabilizer.Reset();
        _metrics.Reset();
        _sessionResonance.Clear();
        _sessionHealth.Clear();
        _sessionStability.Clear();
        PitchSamples.Clear();
        PitchTracePx.Clear();
        DashboardChart = RuntimeChartDisplay.Empty(ChartHeightPx, _chartMin, _chartMax, ComfortZoneLow, ComfortZoneHigh);
        if (_capture is SyntheticAudioCaptureService synth) synth.Mode = SyntheticAudioMode;
        await _capture.StartAsync(new AudioCaptureOptions(SampleRate, DeviceId: FemVoice.Avalonia.Preferences.CapturePreferences.SelectedMicDeviceId())).ConfigureAwait(false);
        _voiceMonitor.Start(SampleRate);   // hear-own-voice (opt-in; no-op when off)
        _sessionStart = System.DateTime.Now;
        IsRecording = true;
        ShowReminder = false;   // a nudge mid-session is pointless; re-evaluated after the session is saved
        CurrentFeedbackMessage = Localized.Get("Dash_Listening", "Lytter …");
    }

    [RelayCommand]
    private async Task Stop()
    {
        if (!IsRecording) return;
        await _capture.StopAsync().ConfigureAwait(false);
        _voiceMonitor.Stop();
        IsRecording = false;
        CurrentSignalStatus = Localized.Get("Signal_NoVoice", "Ingen stemme");
        CurrentFeedbackMessage = Localized.Get("Dash_SessionStopped", "Økt stoppet.");
        ResonanceDisplay = "—";
        UpdatePerception(false, 0, 0);   // clear the perceived-voice mirror when the session ends

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
                    // Real pitch variation (prosody): population std-dev of the voiced pitch samples (Hz). A genuine
                    // measurement of the real pitch — persisted via the INSERT (PitchVariation column). No fabrication.
                    double pitchVariation = 0;
                    if (voiced.Count > 1)
                    {
                        double mean = voiced.Average();
                        pitchVariation = System.Math.Sqrt(voiced.Sum(p => (p - mean) * (p - mean)) / voiced.Count);
                    }
                    var session = new FemVoiceStudio.Models.TrainingSession
                    {
                        UserId = 1,
                        StartTime = _sessionStart.ToUniversalTime(),
                        EndTime = System.DateTime.UtcNow,
                        AveragePitch = System.Math.Round(avg, 1),
                        MinPitch = voiced.Count > 0 ? System.Math.Round(voiced.Min(), 1) : 0,
                        MaxPitch = voiced.Count > 0 ? System.Math.Round(voiced.Max(), 1) : 0,
                        PitchVariation = System.Math.Round(pitchVariation, 1),   // real prosody metric (std-dev of pitch)
                        OverallScore = System.Math.Round(inZone),   // comfort-zone adherence (display-only score)
                        ResonanceScore = System.Math.Round(avgResonance, 1),   // real resonance from the Core DSP engine
                        DifficultyLevel = SelectedDifficulty,
                        Feedback = "Avalonia dashboard-økt",
                    };
                    // Core's INSERT (SaveTrainingSession) persists the base columns; ResonanceScore is only written by
                    // UpdateTrainingSession (the intended create-then-enrich two-step). Save to get the Id, then update
                    // with the real resonance so it round-trips. Both are existing Core APIs — no Core change.
                    int savedId = _database.SaveTrainingSession(session);
                    if (savedId > 0 && avgResonance > 0)
                    {
                        session.Id = savedId;
                        _database.UpdateTrainingSession(session);
                    }

                    // Per-dimension Voice-Intelligence record (the write the Avalonia head used to skip) so the WPF-parity
                    // per-dimension screens light up with REAL data. Best-effort, never blocks the session save.
                    WriteSessionAnalytics(savedId, session.StartTime, System.DateTime.UtcNow, inZone, avgResonance, pitchVariation);
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
    private void OnFrameAvailable(object? sender, AudioFrameAvailableEventArgs e)
    {
        _voiceMonitor.Feed(e.Samples);                 // hear-own-voice: play the frame back (no-op when off)
        PitchAnalysisResult result = _pitch.DetectPitch(e.Samples);
        // Live BRIGHTNESS via the monotonic VoiceBrightnessMeter (proper spectral centroid → 0–100). Replaces the old
        // ResonanceProxyEngine score, which stuck low ("always Mørk") on real mics because its formant peak detection
        // fell back to fixed values and froze the score. The meter responds to the actual voice and is loudness-independent.
        int resonancePct = result.IsVoiced ? FemVoiceStudio.Audio.VoiceBrightnessMeter.BrightnessPercent(e.Samples, SampleRate, _resonanceBaseline) : 0;
        if (IsRecording && result.IsVoiced) _sessionResonance.Add(resonancePct);
        double smoothed = _metrics.CalculateSmoothedPitch(result.Pitch, result.IsVoiced);
        double stabilized = result.IsVoiced ? _stabilizer.Filter(smoothed, DateTime.Now) : 0;
        StabilityState stability = _metrics.CalculateStability();
        // Strain is treated as ABSENT (Avalonia has no strain sensor) — a truthful state, not a fabricated value;
        // CalculateHealth still reflects real pitch/intensity extremity.
        HealthState health = _metrics.CalculateHealth(0, smoothed, result.Intensity);

        // Accumulate the REAL per-frame health/stability (voiced frames only) → averaged into the per-dimension VI
        // scores written on Stop. No UI-thread dependency; plain numeric aggregation.
        if (IsRecording && result.IsVoiced && stabilized > 0)
        {
            _sessionHealth.Add(HealthTo100(health));
            _sessionStability.Add(StabilityTo100(stability));
        }

        _ui.Post(() =>
        {
            CurrentPitch = result.IsVoiced ? Math.Round(stabilized, 1) : 0;
            CurrentSignalStatus = result.IsVoiced
                ? string.Format(Localized.Get("Signal_Voiced", "Stemme ({0} sikkerhet)"), result.Confidence.ToString("P0"))
                : Localized.Get("Signal_NoVoice", "Ingen stemme");
            // Comfort-zone COMPLIANCE (no raw Hz exposed — WPF principle A7): in-zone / below / above.
            if (result.IsVoiced && stabilized > 0)
            {
                InComfortZone = stabilized >= ComfortZoneLow && stabilized <= ComfortZoneHigh;
                ComfortZoneStatus = InComfortZone
                    ? Localized.Get("Main_InComfortZone", "I komfortsonen")
                    : stabilized < ComfortZoneLow
                        ? Localized.Get("Main_BelowComfortZone", "Under komfortsonen")
                        : Localized.Get("Main_AboveComfortZone", "Over komfortsonen");
            }
            else { InComfortZone = false; ComfortZoneStatus = "—"; }
            PitchStability = StabilityText(stability);
            HealthStatusDisplay = HealthText(health);
            ResonanceDisplay = result.IsVoiced ? ResonanceText(resonancePct) : "—";
            CurrentFeedbackMessage = DeriveFeedback(result.IsVoiced, stability, health, stabilized);

            bool voiced = result.IsVoiced && stabilized > 0;
            UpdatePerception(voiced, stabilized, resonancePct);   // perceived-voice mirror (pitch + resonance → band + tip)
            // Display-only chart snapshot (axis + comfort band fixed; marker follows current pitch). No data change.
            DashboardChart = RuntimeChartDisplay.From(
                ChartHeightPx, _chartMin, _chartMax, ComfortZoneLow, ComfortZoneHigh,
                stabilized, voiced,
                voiced ? Localized.Get("Chart_VoiceDetected", "Stemme registrert")
                       : Localized.Get("Chart_WaitingForVoice", "Venter på stemme …"));
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
            CurrentSignalStatus = Localized.Get("Signal_MicUnavailable", "Mikrofon utilgjengelig");
            CurrentFeedbackMessage = e.Reason ?? Localized.Get("Audio_DeviceLost", "Lydenhet mistet.");
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

    // Compute the perceived-voice mirror from the current voiced frame (pure Core estimator), or clear it when there
    // is no voice. Runs on the UI thread (called inside the _ui.Post block / from Stop).
    private void UpdatePerception(bool voiced, double pitch, int resonancePct)
    {
        if (!voiced || pitch <= 0)
        {
            HasPerception = false;
            PerceptionLabel = "—";
            PerceptionTip = "";
            PerceptionBand = "none";
            PerceptionScore = PerceptionPitchScore = PerceptionResonanceScore = 0;
            return;
        }
        var p = FemVoiceStudio.Audio.VoicePerceptionEstimator.Estimate(pitch, resonancePct);
        PerceptionScore = p.Score;
        PerceptionPitchScore = p.PitchScore;
        PerceptionResonanceScore = p.ResonanceScore;
        PerceptionLabel = PerceptionBandText(p.Band);
        PerceptionBand = p.Band switch
        {
            VoicePerceptionBand.Feminine => "feminine",
            VoicePerceptionBand.Androgynous => "androgynous",
            _ => "masculine",
        };
        PerceptionTip = PerceptionHintText(p.Hint);
        HasPerception = true;
    }

    private static string PerceptionBandText(VoicePerceptionBand band) => band switch
    {
        VoicePerceptionBand.Feminine => Localized.Get("Perception_Feminine", "Feminin"),
        VoicePerceptionBand.Androgynous => Localized.Get("Perception_Androgynous", "Androgyn"),
        _ => Localized.Get("Perception_Masculine", "Maskulin"),
    };

    private static string PerceptionHintText(VoicePerceptionHint hint) => hint switch
    {
        VoicePerceptionHint.RaisePitch => Localized.Get("Perception_TipRaisePitch", "Løft tonehøyden litt for et lysere inntrykk."),
        VoicePerceptionHint.BrightenResonance => Localized.Get("Perception_TipBrighten", "Gjør resonansen lysere og mer fremover i munnen."),
        VoicePerceptionHint.HoldSteady => Localized.Get("Perception_TipHold", "Fint — hold denne klangen jevnt."),
        _ => "",
    };

    // Qualitative label + value for the live resonance readout (0–100). Mirrors WPF's bright/neutral/dark buckets.
    private static string ResonanceText(int pct) => pct switch
    {
        >= 67 => string.Format(Localized.Get("Resonance_Bright", "Lys ({0})"), pct),
        >= 34 => string.Format(Localized.Get("Resonance_Neutral", "Nøytral ({0})"), pct),
        _ => string.Format(Localized.Get("Resonance_Dark", "Mørk ({0})"), pct),
    };

    // Stability / health labels resolve the SAME shared RESX keys WPF uses ({loc:Loc Stability_*/Health_*}) so the
    // text matches WPF and relocalizes with the culture.
    // Compute the 7 per-dimension VI scores from this session's REAL signals and persist a SessionAnalyticsRecord so
    // the WPF-parity per-dimension screens (Progression parameter-graph, Clinician voice-metrics/trends, Analysis
    // rings) read real data. Concrete DB only; fully guarded — never blocks or fails the session save.
    private void WriteSessionAnalytics(int sessionId, System.DateTime startUtc, System.DateTime endUtc,
        double pitchComfortPercent, double avgResonance, double pitchVariation)
    {
        if (_database is not FemVoiceStudio.Data.DatabaseService concrete) return;
        try
        {
            var analytics = new FemVoiceStudio.Services.SessionAnalyticsStore(
                new FemVoiceStudio.Services.SqliteSessionAnalyticsRepository(concrete.ConnectionString));

            double avgHealth = _sessionHealth.Count > 0 ? _sessionHealth.Average() : 0;
            double avgStability = _sessionStability.Count > 0 ? _sessionStability.Average() : 0;

            // Recovery from the real cross-session history (100 − debt). Empty history → rested; guarded.
            double recovery100 = 100;
            try
            {
                var forecast = new FemVoiceStudio.Services.RecoveryIntelligenceService()
                    .ForecastFromHistoryAsync(analytics, System.DateTime.UtcNow, 1).GetAwaiter().GetResult();
                recovery100 = System.Math.Clamp(100 - forecast.RecoveryDebt, 0, 100);
            }
            catch { /* no history yet → rested default */ }

            var d = FemVoice.Avalonia.Audio.SessionAnalyticsScorer.Compute(
                pitchComfortPercent, avgResonance, pitchVariation, avgStability, avgHealth, recovery100);

            analytics.RecordSessionCompletedAsync(new FemVoiceStudio.Services.SessionAnalyticsRecord
            {
                SessionId = sessionId,
                UserId = 1,
                StartedAt = startUtc,
                EndedAt = endUtc,
                ExerciseCount = 1,
                AverageResonance = d.ResonanceScore100,
                AverageStability = d.ConsistencyScore100,
                AveragePitchComfort = d.ComfortScore100,
                AverageHealthScore = d.HealthScore100,
                ResonanceScore100 = d.ResonanceScore100,
                ComfortScore100 = d.ComfortScore100,
                ConsistencyScore100 = d.ConsistencyScore100,
                IntonationScore100 = d.IntonationScore100,
                VocalWeightScore100 = d.VocalWeightScore100,
                RecoveryScore100 = d.RecoveryScore100,
                PitchScore100 = d.PitchScore100,
                CompositeVoiceScore = d.CompositeVoiceScore,
            }).GetAwaiter().GetResult();
        }
        catch { /* analytics write is best-effort — never affects the session save */ }
    }

    // Map the Core health/stability enums to 0–100 for the per-dimension VI scores (real state → numeric).
    private static double HealthTo100(HealthState h) => h switch
    {
        HealthState.Safe => 100, HealthState.Monitor => 70, HealthState.Warning => 45, HealthState.Danger => 20, _ => 0,
    };
    private static double StabilityTo100(StabilityState s) => s switch
    {
        StabilityState.VeryStable => 100, StabilityState.Stable => 80, StabilityState.Developing => 55,
        StabilityState.Unstable => 30, _ => 0,
    };

    private static string StabilityText(StabilityState s) => s switch
    {
        StabilityState.NoVoice => Localized.Get("Stability_NoVoice", "Ingen stemme"),
        StabilityState.Unstable => Localized.Get("Stability_Unstable", "Ustabil"),
        StabilityState.Developing => Localized.Get("Stability_Developing", "Utvikler seg"),
        StabilityState.Stable => Localized.Get("Stability_Stable", "Stabil"),
        StabilityState.VeryStable => Localized.Get("Stability_VeryStable", "Veldig stabil"),
        _ => "—",
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

    // Simple, safe descriptive feedback (NOT the FeedbackConsistencyGuard pipeline — that is deferred).
    private string DeriveFeedback(bool voiced, StabilityState stability, HealthState health, double pitch)
    {
        if (!voiced) return Localized.Get("Feedback_NoVoice", "Ingen stemme oppdaget — prøv å snakke jevnt.");
        if (health is HealthState.Warning or HealthState.Danger) return Localized.Get("Feedback_TakeBreak", "Ta en pause og slapp av i stemmen.");
        if (pitch < ComfortZoneLow) return Localized.Get("Feedback_BelowZone", "Litt under komfortsonen — løft tonen forsiktig.");
        if (pitch > ComfortZoneHigh) return Localized.Get("Feedback_AboveZone", "Litt over komfortsonen — slipp tonen litt ned.");
        return stability is StabilityState.Stable or StabilityState.VeryStable
            ? Localized.Get("Feedback_Stable", "Fin, stabil tone i komfortsonen.")
            : Localized.Get("Feedback_KeepSteady", "Hold tonen jevn i komfortsonen.");
    }

    public void Dispose()
    {
        _capture.FrameAvailable -= OnFrameAvailable;
        _capture.DeviceLost -= OnDeviceLost;
        _voiceMonitor.Dispose();
    }
}
