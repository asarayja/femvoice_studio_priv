using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.ViewModels
{
    // ── NOTE ─────────────────────────────────────────────────────────────────────
    // RelayCommand         → defined in ExerciseListViewModel.cs (shared)
    // IndicatorViewModel   → defined below (first and only definition)
    // GuidanceItem         → defined below (first and only definition)
    // ShieldDisplayState   → defined below (first and only definition)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// View-model for a single active indicator shown in the LiveFeedback panel.
    /// </summary>
    public sealed class IndicatorViewModel : INotifyPropertyChanged
    {
        private double _value;
        private bool   _isActive;

        /// <summary>Localisation key for the indicator label (e.g. "Indicator_Resonance").</summary>
        public string LabelKey  { get; init; } = "";

        /// <summary>Segoe MDL2 Assets glyph code for the indicator icon.</summary>
        public string IconGlyph { get; init; } = "\uE9D9";

        /// <summary>Normalised metric value (0–1) bound to the progress bar.</summary>
        public double Value
        {
            get => _value;
            set { if (Math.Abs(_value - value) > 0.001) { _value = value; OnPropertyChanged(); } }
        }

        /// <summary>Whether this indicator is currently active for the loaded profile.</summary>
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Items displayed in ExerciseGuidancePanel via ItemsControl + DataTemplate.
    /// All text content is a localisation key resolved in the View layer via {loc:Loc}.
    /// </summary>
    public sealed class GuidanceItem
    {
        /// <summary>Segoe MDL2 Assets glyph for the row icon.</summary>
        public string IconGlyph  { get; init; } = "";

        /// <summary>Localisation key for the section heading.</summary>
        public string HeadingKey { get; init; } = "";

        /// <summary>Localisation key for the body text.</summary>
        public string BodyKey    { get; init; } = "";
    }

    /// <summary>Drives ShieldPanel background and text via DataTrigger — no code-behind branching.</summary>
    public enum ShieldDisplayState { Safe, Warning, Locked }

    // ─────────────────────────────────────────────────────────────────────────────
    // ExerciseDetailViewModel
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Single bridge between <see cref="ExerciseIntelligenceCoordinator"/> and the
    /// ExerciseWindow Live Feedback UI.
    ///
    /// Responsibilities:
    ///   • Subscribe to coordinator events; marshal all state to the UI thread.
    ///   • Expose normalised 0–1 metric properties for data binding.
    ///   • Derive computed display properties (brush colours, visibility flags)
    ///     so zero logic leaks into XAML or code-behind.
    ///   • Manage IndicatorViewModel collection based on active ExerciseTargetProfile.
    ///   • Expose Start / Stop / Pause / DismissCoachMessage commands.
    ///
    /// What this class must NOT do:
    ///   • Touch any WPF element directly.
    ///   • Contain per-exercise if/else branches.
    ///   • Expose raw Hz values or internal engine state.
    /// </summary>
    public enum PerformanceQualityExtended { Poor, Fair, Good, VeryGood, Excellent }

    // MasteryLevel flyttet til Models/MasteryLevel.cs — beregnes nå av MasteryEvaluator
    // med kliniske gater (komfort/safety/fatigue), ikke av score-snitt alene.

    public sealed class ExerciseDetailViewModel : INotifyPropertyChanged, IDisposable
    {
        // ── Dependencies ──────────────────────────────────────────────────────────
        private readonly ExerciseIntelligenceCoordinator _coordinator;
        private readonly ILocalizationService            _localization;
        private readonly IExerciseProfileFactory?        _profileFactory;
        private readonly FeedbackPipeline?               _feedbackPipeline;
        private readonly InlineCoachFeedbackMapper?      _inlineCoachFeedbackMapper;
        private readonly MasteryEvaluator?               _masteryEvaluator;

        // ── State ────────────────────────────────────────────────────────────────
        private ExerciseTargetProfile _activeProfile = ExerciseTargetProfile.CreateResonanceHumming();
        private ExerciseLiveState?    _currentLiveState;
        private bool                  _isDisposed;

        // ── Backing fields ────────────────────────────────────────────────────────
        private double             _primaryMetricScore;
        private double             _secondaryMetricScore;
        private double             _stabilityScore;
        private double             _holdProgress;
        private bool               _isInComfortZone;
        private bool               _isHoldingCorrectly;
        private bool               _isSafetyLocked;
        private PerformanceQuality _quality = PerformanceQuality.Poor;
        private PerformanceQualityExtended _displayQuality = PerformanceQualityExtended.Poor;
        private MasteryLevel _mastery = MasteryLevel.Beginner;
        private int _completedSessions;
        private double _averageSessionScore;
        private int                _sessionElapsedSeconds;

        private bool _showResonanceBar;
        private bool _showStabilityMeter;
        private bool _showPitchDirection;
        private bool _showHoldProgress;
        private bool _showHealthShield;
        private bool _showAirflowIndicator;

        private string          _coachMessage         = "";
        private MessageSeverity _coachSeverity        = MessageSeverity.Info;
        private bool            _isCoachMessageVisible;

        private ObservableCollection<GuidanceItem> _guidanceItems = new();

        // ─────────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────────

        public ExerciseDetailViewModel(
            ExerciseIntelligenceCoordinator coordinator,
            ILocalizationService            localization,
            IExerciseProfileFactory?        profileFactory = null,
            FeedbackPipeline?               feedbackPipeline = null,
            InlineCoachFeedbackMapper?      inlineCoachFeedbackMapper = null,
            MasteryEvaluator?               masteryEvaluator = null)
        {
            _coordinator    = coordinator  ?? throw new ArgumentNullException(nameof(coordinator));
            _localization   = localization ?? throw new ArgumentNullException(nameof(localization));
            _profileFactory = profileFactory;
            _feedbackPipeline = feedbackPipeline;
            _inlineCoachFeedbackMapper = inlineCoachFeedbackMapper;
            _masteryEvaluator = masteryEvaluator;

            _coordinator.ExerciseUpdated    += OnExerciseUpdated;
            _coordinator.InlineCoachUpdated += OnInlineCoachUpdated;

            // Commands — use the existing RelayCommand from ExerciseListViewModel.cs
            StartExerciseCommand       = new RelayCommand(_ => ExecuteStart(null),   _ => CanStart(null));
            StopExerciseCommand        = new RelayCommand(_ => ExecuteStop(null),    _ => CanStop(null));
            PauseExerciseCommand       = new RelayCommand(_ => ExecutePause(null),   _ => CanPause(null));
            DismissCoachMessageCommand = new RelayCommand(_ => ExecuteDismissCoach(null));

            Indicators = new ObservableCollection<IndicatorViewModel>
            {
                new() { LabelKey = "Indicator_Resonance", IconGlyph = "\uE9D9" },
                new() { LabelKey = "Indicator_Stability", IconGlyph = "\uEA86" },
                new() { LabelKey = "Indicator_Pitch",     IconGlyph = "\uE8D6" },
                new() { LabelKey = "Indicator_Hold",      IconGlyph = "\uE916" },
                new() { LabelKey = "Indicator_Shield",    IconGlyph = "\uEA18" },
                new() { LabelKey = "Indicator_Airflow",   IconGlyph = "\uE81C" },
            };

            ApplyProfile(_activeProfile);
        }

        private Data.ExerciseDataService? _progressService;
        private void EnsureProgressService()
        {
            if (_progressService != null) return;
            try { _progressService = App.Services.GetService(typeof(Data.ExerciseDataService)) as Data.ExerciseDataService; }
            catch { _progressService = null; }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Profile management
        // ─────────────────────────────────────────────────────────────────────────

        public void ApplyProfile(ExerciseTargetProfile profile, int? exerciseId = null)
        {
            _activeProfile = profile ?? throw new ArgumentNullException(nameof(profile));

            ShowResonanceBar     = profile.UsesResonance;
            ShowStabilityMeter   = profile.UsesStability;
            ShowPitchDirection   = IsPitchPrimaryProfile(profile);
            ShowHoldProgress     = profile.RequiredHoldSeconds > 0;
            ShowHealthShield     = true;
            ShowAirflowIndicator = profile.UsesIntensity;

            if (Indicators.Count >= 6)
            {
                Indicators[0].IsActive = ShowResonanceBar;
                Indicators[1].IsActive = ShowStabilityMeter;
                Indicators[2].IsActive = ShowPitchDirection;
                Indicators[3].IsActive = ShowHoldProgress;
                Indicators[4].IsActive = ShowHealthShield;
                Indicators[5].IsActive = ShowAirflowIndicator;
            }

            RebuildGuidanceItems(profile);
            RaiseClinicalLoopPropertiesChanged();

            // Load persisted progress + clinically gated mastery asynchronously —
            // never blocks the UI thread on DB access (the old code did).
            if (exerciseId.HasValue)
            {
                EnsureProgressService();
                _ = LoadMasteryAsync(exerciseId.Value, profile);
            }

            OnPropertyChanged(nameof(ActiveIndicatorPackage));
            OnPropertyChanged(nameof(FeedbackModeKey));
            OnPropertyChanged(nameof(ThresholdStrategyKey));
            OnPropertyChanged(nameof(MasteryLabelKey));
            OnPropertyChanged(nameof(MasteryProgressPercent));
            OnPropertyChanged(nameof(CompletedSessionCount));
            OnPropertyChanged(nameof(AverageQualityLabelKey));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Metric properties
        // ─────────────────────────────────────────────────────────────────────────

        public double PrimaryMetricScore
        {
            get => _primaryMetricScore;
            private set
            {
                if (Math.Abs(_primaryMetricScore - value) > 0.001)
                {
                    _primaryMetricScore = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ResonanceBarBrush));
                    OnPropertyChanged(nameof(ResonanceBarBrushKey));
                    OnPropertyChanged(nameof(ResonanceStatusKey));
                    RaiseClinicalLoopPropertiesChanged();
                }
            }
        }

        public double SecondaryMetricScore
        {
            get => _secondaryMetricScore;
            private set { if (Math.Abs(_secondaryMetricScore - value) > 0.001) { _secondaryMetricScore = value; OnPropertyChanged(); } }
        }

        public double StabilityScore
        {
            get => _stabilityScore;
            private set
            {
                if (Math.Abs(_stabilityScore - value) > 0.001)
                {
                    _stabilityScore = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StabilityStatusKey));
                    RaiseClinicalLoopPropertiesChanged();
                }
            }
        }

        public double HoldProgress
        {
            get => _holdProgress;
            private set
            {
                if (Math.Abs(_holdProgress - value) > 0.001)
                {
                    _holdProgress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HoldProgressPercent));
                    RaiseClinicalLoopPropertiesChanged();
                }
            }
        }

        public string HoldProgressPercent => $"{(int)Math.Round(HoldProgress * 100)}%";

        public bool IsInComfortZone
        {
            get => _isInComfortZone;
            private set
            {
                if (_isInComfortZone != value)
                {
                    _isInComfortZone = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShieldState));
                    OnPropertyChanged(nameof(PitchDirectionValue));
                    OnPropertyChanged(nameof(PitchStatusKey));
                    RaiseClinicalLoopPropertiesChanged();
                }
            }
        }

        public bool IsHoldingCorrectly
        {
            get => _isHoldingCorrectly;
            private set
            {
                if (_isHoldingCorrectly != value)
                {
                    _isHoldingCorrectly = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HoldArcBrushKey));
                    OnPropertyChanged(nameof(HoldArcBrush));
                    OnPropertyChanged(nameof(StabilityBrushKey));
                    OnPropertyChanged(nameof(StabilityBrush));
                }
            }
        }

        public bool IsSafetyLocked
        {
            get => _isSafetyLocked;
            private set
            {
                if (_isSafetyLocked != value)
                {
                    _isSafetyLocked = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShieldState));
                    OnPropertyChanged(nameof(ShieldIconGlyph));
                    OnPropertyChanged(nameof(ShieldTextKey));
                    OnPropertyChanged(nameof(ShieldBrushKey));
                    OnPropertyChanged(nameof(ShieldBrush));
                    RaiseClinicalLoopPropertiesChanged();
                }
            }
        }

        public PerformanceQuality Quality
        {
            get => _quality;
            private set { if (_quality != value) { _quality = value; OnPropertyChanged(); } }
        }

        public PerformanceQualityExtended DisplayQuality
        {
            get => _displayQuality;
            private set { if (_displayQuality != value) { _displayQuality = value; OnPropertyChanged(); OnPropertyChanged(nameof(QualityBrushKey)); OnPropertyChanged(nameof(QualityLabelKey)); } }
        }

        public int SessionElapsedSeconds
        {
            get => _sessionElapsedSeconds;
            private set { if (_sessionElapsedSeconds != value) { _sessionElapsedSeconds = value; OnPropertyChanged(); } }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Computed display properties
        // ─────────────────────────────────────────────────────────────────────────

        public ShieldDisplayState ShieldState =>
            IsSafetyLocked  ? ShieldDisplayState.Locked  :
            IsInComfortZone ? ShieldDisplayState.Safe     :
                              ShieldDisplayState.Warning;

        public string ResonanceBarBrushKey =>
            PrimaryMetricScore > _activeProfile.TargetResonanceMax ? "WarningBrush" :
            PrimaryMetricScore >= _activeProfile.TargetResonanceMin ? "SuccessBrush" : "WarningBrush";

        public Brush? ResonanceBarBrush =>
            Application.Current?.TryFindResource(ResonanceBarBrushKey) as Brush;

        public string HoldArcBrushKey  => IsHoldingCorrectly ? "SuccessBrush" : "AccentPrimaryBrush";

        public Brush? HoldArcBrush => Application.Current?.TryFindResource(HoldArcBrushKey) as Brush;

        public string StabilityBrushKey => IsHoldingCorrectly ? "SuccessBrush" : "WarningBrush";
        public Brush? StabilityBrush => Application.Current?.TryFindResource(StabilityBrushKey) as Brush;
        public string QualityBrushKey  => Quality switch
        {
            _ => DisplayQuality switch
            {
                PerformanceQualityExtended.Excellent => "QualityBrush_Excellent",
                PerformanceQualityExtended.VeryGood  => "QualityBrush_VeryGood",
                PerformanceQualityExtended.Good     => "QualityBrush_Good",
                PerformanceQualityExtended.Fair     => "QualityBrush_Fair",
                _                                    => "QualityBrush_Poor"
            }
        };

        public string QualityLabelKey => DisplayQuality switch
        {
            PerformanceQualityExtended.Poor     => "Quality_Poor",
            PerformanceQualityExtended.Fair     => "Quality_Fair",
            PerformanceQualityExtended.Good     => "Quality_Good",
            PerformanceQualityExtended.VeryGood => "Quality_VeryGood",
            PerformanceQualityExtended.Excellent=> "Quality_Excellent",
            _ => "Quality_Poor"
        };

        public Brush? QualityBrush => Application.Current?.TryFindResource(QualityBrushKey) as Brush;

        public string ShieldIconGlyph  => IsSafetyLocked ? "\uE7BA" : "\uEA18";

        public string ShieldTextKey    => ShieldState switch
        {
            ShieldDisplayState.Locked  => "Shield_SafetyLocked",
            ShieldDisplayState.Safe    => "Shield_ComfortZoneOk",
            _                          => "Shield_OutsideComfortZone"
        };

        public string ShieldBrushKey   => ShieldState switch
        {
            ShieldDisplayState.Locked  => "ErrorBrush",
            ShieldDisplayState.Safe    => "SuccessBrush",
            _                          => "WarningBrush"
        };

        public Brush? ShieldBrush => Application.Current?.TryFindResource(ShieldBrushKey) as Brush;

        public double PitchDirectionValue => ShowPitchDirection
            ? (IsInComfortZone ? 1.0 : 0.0)
            : 0.0;

        public string PitchStatusKey => !ShowPitchDirection
            ? "LiveFeedback_Active"
            : IsInComfortZone
                ? "Shield_ComfortZoneOk"
                : "Shield_OutsideComfortZone";

        public string ResonanceStatusKey
        {
            get
            {
                if (!ShowResonanceBar)
                    return "LiveFeedback_Active";
                if (PrimaryMetricScore > _activeProfile.TargetResonanceMax)
                    return "LiveFeedback_Smoother";
                if (PrimaryMetricScore >= _activeProfile.TargetResonanceMin)
                    return "LiveFeedback_Good";
                return PrimaryMetricScore >= _activeProfile.TargetResonanceMin * 0.65
                    ? "LiveFeedback_OnTrack"
                    : "LiveFeedback_MoveForward";
            }
        }

        public string StabilityStatusKey
        {
            get
            {
                if (!ShowStabilityMeter)
                    return "LiveFeedback_Active";
                if (StabilityScore >= _activeProfile.StabilityThreshold)
                    return "LiveFeedback_Stable";
                return StabilityScore >= _activeProfile.StabilityThreshold * 0.75
                    ? "LiveFeedback_Smoother"
                    : "LiveFeedback_Unstable";
            }
        }

        public int LiveCompositeScorePercent
            => (int)Math.Round(CalculateLiveCompositeScore() * 100);

        public string LiveCompositeScoreText => $"{LiveCompositeScorePercent}%";

        public string LiveCompositeScoreBrushKey
        {
            get
            {
                if (IsSafetyLocked)
                    return "ErrorBrush";
                if (LiveCompositeScorePercent >= 80)
                    return "SuccessBrush";
                if (LiveCompositeScorePercent >= 55)
                    return "WarningBrush";
                return "InfoBrush";
            }
        }

        public Brush? LiveCompositeScoreBrush =>
            Application.Current?.TryFindResource(LiveCompositeScoreBrushKey) as Brush;

        public string ClinicalLoopStatusKey
        {
            get
            {
                if (IsSafetyLocked)
                    return "Shield_SafetyLocked";
                if (ShowHoldProgress && HoldProgress >= 0.999)
                    return "InlineCoachFeedback_HoldComplete";
                if (LiveCompositeScorePercent >= 80)
                    return "LiveFeedback_Good";
                if (LiveCompositeScorePercent >= 55)
                    return "LiveFeedback_OnTrack";
                return "LiveFeedback_BuildResonanceStability";
            }
        }

        public string ProgressExplanationKey
        {
            get
            {
                if (IsSafetyLocked)
                    return "ProgressionFeedback_Paused";
                if (ShowHoldProgress && HoldProgress >= 0.999)
                    return "ProgressionFeedback_Update";
                if (IsHoldingCorrectly)
                    return "LiveFeedback_OnTrack";
                return "LiveFeedback_BuildResonanceStability";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mastery / Progression
        // ─────────────────────────────────────────────────────────────────────

        public MasteryLevel Mastery => _mastery;

        public string MasteryLabelKey => _mastery switch
        {
            MasteryLevel.Beginner    => "Mastery_Beginner",
            MasteryLevel.Developing  => "Mastery_Developing",
            MasteryLevel.Stable      => "Mastery_Stable",
            MasteryLevel.Mastered    => "Mastery_Mastered",
            _ => "Mastery_Beginner"
        };

        public int CompletedSessionCount => _completedSessions;

        public string AverageQualityLabelKey
        {
            get
            {
                if (_averageSessionScore >= 85) return "Quality_Excellent";
                if (_averageSessionScore >= 70) return "Quality_VeryGood";
                if (_averageSessionScore >= 55) return "Quality_Good";
                if (_averageSessionScore >= 40) return "Quality_Fair";
                return "Quality_Poor";
            }
        }

        public int MasteryProgressPercent
        {
            get
            {
                // Simple visual progress: map average score (0-100) to percent
                return (int)Math.Round(Math.Clamp(_averageSessionScore, 0, 100));
            }
        }

        /// <summary>
        /// Loads persisted progress and evaluates mastery through MasteryEvaluator's
        /// clinical gates. Runs off the UI thread; property updates are marshalled back.
        /// On any failure the previous mastery is kept — never upgrade on error.
        /// </summary>
        private async System.Threading.Tasks.Task LoadMasteryAsync(int exerciseId, ExerciseTargetProfile profile)
        {
            try
            {
                var prog = await System.Threading.Tasks.Task.Run(
                    () => _progressService?.GetExerciseProgress(exerciseId)).ConfigureAwait(false);
                var completedSessions   = prog?.TotalSessions ?? 0;
                var averageSessionScore = prog?.AverageScore ?? 0;

                var mastery = MasteryLevel.Beginner;
                if (_masteryEvaluator != null)
                {
                    var evaluation = await _masteryEvaluator.EvaluateAsync(
                        exerciseId, completedSessions, profile, DateTime.Now).ConfigureAwait(false);
                    mastery = evaluation.Level;
                }
                else if (completedSessions >= 3)
                {
                    // Uten evaluator (f.eks. eldre tester): konservativ default —
                    // aldri over Developing uten verifiserte kliniske gater.
                    mastery = MasteryLevel.Developing;
                }

                void Apply()
                {
                    _completedSessions   = completedSessions;
                    _averageSessionScore = averageSessionScore;
                    _mastery             = mastery;
                    OnPropertyChanged(nameof(Mastery));
                    OnPropertyChanged(nameof(MasteryLabelKey));
                    OnPropertyChanged(nameof(MasteryProgressPercent));
                    OnPropertyChanged(nameof(CompletedSessionCount));
                    OnPropertyChanged(nameof(AverageQualityLabelKey));
                }

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                    await dispatcher.InvokeAsync(Apply);
                else
                    Apply();
            }
            catch
            {
                // Behold forrige mastery-nivå ved feil — aldri oppgrader på feil.
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Indicator visibility flags
        // ─────────────────────────────────────────────────────────────────────────

        public bool ShowResonanceBar
        {
            get => _showResonanceBar;
            private set { if (_showResonanceBar != value) { _showResonanceBar = value; OnPropertyChanged(); } }
        }

        public bool ShowStabilityMeter
        {
            get => _showStabilityMeter;
            private set { if (_showStabilityMeter != value) { _showStabilityMeter = value; OnPropertyChanged(); } }
        }

        public bool ShowPitchDirection
        {
            get => _showPitchDirection;
            private set
            {
                if (_showPitchDirection != value)
                {
                    _showPitchDirection = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PitchDirectionValue));
                    OnPropertyChanged(nameof(PitchStatusKey));
                    RaiseClinicalLoopPropertiesChanged();
                }
            }
        }

        public bool ShowHoldProgress
        {
            get => _showHoldProgress;
            private set { if (_showHoldProgress != value) { _showHoldProgress = value; OnPropertyChanged(); } }
        }

        public bool ShowHealthShield
        {
            get => _showHealthShield;
            private set { if (_showHealthShield != value) { _showHealthShield = value; OnPropertyChanged(); } }
        }

        public bool ShowAirflowIndicator
        {
            get => _showAirflowIndicator;
            private set { if (_showAirflowIndicator != value) { _showAirflowIndicator = value; OnPropertyChanged(); } }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Coach hint
        // ─────────────────────────────────────────────────────────────────────────

        public string CoachMessage
        {
            get => _coachMessage;
            private set { if (_coachMessage != value) { _coachMessage = value; OnPropertyChanged(); } }
        }

        public MessageSeverity CoachSeverity
        {
            get => _coachSeverity;
            private set { if (_coachSeverity != value) { _coachSeverity = value; OnPropertyChanged(); } }
        }

        public bool IsCoachMessageVisible
        {
            get => _isCoachMessageVisible;
            private set { if (_isCoachMessageVisible != value) { _isCoachMessageVisible = value; OnPropertyChanged(); } }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Profile metadata
        // ─────────────────────────────────────────────────────────────────────────

        public ExerciseLiveState? CurrentLiveState => _currentLiveState;

        public string FeedbackModeKey         => _activeProfile.FeedbackModeKey         ?? "FeedbackMode_Unknown";
        public string ThresholdStrategyKey    => _activeProfile.ThresholdStrategyKey    ?? "ThresholdStrategy_Default";
        public string ActiveIndicatorPackage  => _activeProfile.IndicatorPackageSummaryKey ?? "";

        public ObservableCollection<GuidanceItem>      GuidanceItems { get => _guidanceItems; private set { _guidanceItems = value; OnPropertyChanged(); } }
        public ObservableCollection<IndicatorViewModel> Indicators   { get; }

        // ─────────────────────────────────────────────────────────────────────────
        // Commands
        // ─────────────────────────────────────────────────────────────────────────

        public ICommand StartExerciseCommand       { get; }
        public ICommand StopExerciseCommand        { get; }
        public ICommand PauseExerciseCommand       { get; }
        public ICommand DismissCoachMessageCommand { get; }

        private bool _isExerciseActive;
        public bool IsExerciseActive
        {
            get => _isExerciseActive;
            private set { if (_isExerciseActive != value) { _isExerciseActive = value; OnPropertyChanged(); } }
        }

        private bool _isExercisePaused;
        public bool IsExercisePaused
        {
            get => _isExercisePaused;
            private set { if (_isExercisePaused != value) { _isExercisePaused = value; OnPropertyChanged(); } }
        }

        private bool CanStart(object? _)  => !IsExerciseActive;
        private bool CanStop(object? _)   =>  IsExerciseActive;
        private bool CanPause(object? _)  =>  IsExerciseActive;

        private void ExecuteStart(object? _)
        {
            if (!CanStart(null)) return;
            _coordinator.StartExercise(_activeProfile, userId: 1);
            IsExerciseActive = true;
            IsExercisePaused = false;
        }

        private void ExecuteStop(object? _)
        {
            if (!CanStop(null)) return;
            _coordinator.StopExercise();
            IsExerciseActive = false;
            IsExercisePaused = false;
            ResetMetricsToIdle();
        }

        private void ExecutePause(object? _)
        {
            if (!CanPause(null)) return;
            if (IsExercisePaused)
            {
                _coordinator.StartExercise(_activeProfile, userId: 1);
                IsExercisePaused = false;
            }
            else
            {
                _coordinator.StopExercise();
                IsExercisePaused = true;
            }
        }

        private void ExecuteDismissCoach(object? _)
        {
            IsCoachMessageVisible = false;
            CoachMessage          = "";
        }

        public void UpdateLiveMetrics(double resonanceScore, double pitch, double stability, double health)
            => _coordinator.UpdateMetrics(resonanceScore, pitch, stability, health);

        private double CalculateLiveCompositeScore()
        {
            if (IsSafetyLocked)
                return Math.Min(0.35, Math.Clamp(0.5 * PrimaryMetricScore + 0.5 * StabilityScore, 0, 1));

            var weightedTotal = 0.0;
            var weight = 0.0;

            if (ShowResonanceBar)
            {
                weightedTotal += Math.Clamp(PrimaryMetricScore, 0, 1) * 0.45;
                weight += 0.45;
            }

            if (ShowPitchDirection)
            {
                weightedTotal += PitchDirectionValue * 0.20;
                weight += 0.20;
            }

            if (ShowStabilityMeter)
            {
                weightedTotal += Math.Clamp(StabilityScore, 0, 1) * 0.25;
                weight += 0.25;
            }

            if (ShowHoldProgress)
            {
                weightedTotal += Math.Clamp(HoldProgress, 0, 1) * 0.10;
                weight += 0.10;
            }

            return weight <= 0 ? 0 : Math.Clamp(weightedTotal / weight, 0, 1);
        }

        private void RaiseClinicalLoopPropertiesChanged()
        {
            OnPropertyChanged(nameof(LiveCompositeScorePercent));
            OnPropertyChanged(nameof(LiveCompositeScoreText));
            OnPropertyChanged(nameof(LiveCompositeScoreBrushKey));
            OnPropertyChanged(nameof(LiveCompositeScoreBrush));
            OnPropertyChanged(nameof(ClinicalLoopStatusKey));
            OnPropertyChanged(nameof(ProgressExplanationKey));
        }

        private static bool IsPitchPrimaryProfile(ExerciseTargetProfile profile)
        {
            if (!profile.UsesPitch)
                return false;

            return profile.FeedbackModeKey is "FeedbackMode_Pitch"
                or "FeedbackMode_Glide"
                or "FeedbackMode_Intonation";
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Coordinator event handlers
        // ─────────────────────────────────────────────────────────────────────────

        private void OnExerciseUpdated(ExerciseLiveState state)
        {
            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ApplyLiveState(state)));
                return;
            }
            ApplyLiveState(state);
        }

        private void OnInlineCoachUpdated(InlineCoachMessage message)
        {
            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => SubmitInlineCoachMessage(message)));
                return;
            }
            SubmitInlineCoachMessage(message);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // State application — must run on UI thread
        // ─────────────────────────────────────────────────────────────────────────

        private void ApplyLiveState(ExerciseLiveState state)
        {
            _currentLiveState    = state;
            PrimaryMetricScore   = state.PrimaryMetricScore;
            SecondaryMetricScore = state.SecondaryMetricScore;
            StabilityScore       = state.StabilityScore;
            HoldProgress         = state.HoldProgress;
            IsInComfortZone      = state.IsInComfortZone;
            IsHoldingCorrectly   = state.IsHoldingCorrectly;
            IsSafetyLocked       = state.IsSafetyLocked;
            Quality              = state.Quality;
            SessionElapsedSeconds= state.SessionElapsedSeconds;
            OnPropertyChanged(nameof(PitchDirectionValue));
            OnPropertyChanged(nameof(PitchStatusKey));
            OnPropertyChanged(nameof(ResonanceStatusKey));
            OnPropertyChanged(nameof(StabilityStatusKey));
            RaiseClinicalLoopPropertiesChanged();

            if (Indicators.Count >= 6)
            {
                Indicators[0].Value = state.PrimaryMetricScore;
                Indicators[1].Value = state.StabilityScore;
                Indicators[2].Value = state.IsInComfortZone ? 1.0 : 0.0;
                Indicators[3].Value = state.HoldProgress;
                Indicators[4].Value = state.IsSafetyLocked  ? 0.0 : 1.0;
                Indicators[5].Value = state.PrimaryMetricScore; // intensity proxy
            }

            // Derive a 5-step quality label from numeric scores — all presentation mapping lives here
            // Weighted composite gives slightly more importance to primary metric.
            var primary = state.PrimaryMetricScore;
            var stability = state.StabilityScore;
            var composite = Math.Clamp(0.65 * primary + 0.35 * stability, 0.0, 1.0);

            var prev = DisplayQuality;
            if (state.IsSafetyLocked)
                DisplayQuality = PerformanceQualityExtended.Poor;
            else if (composite >= 0.9) DisplayQuality = PerformanceQualityExtended.Excellent;
            else if (composite >= 0.75) DisplayQuality = PerformanceQualityExtended.VeryGood;
            else if (composite >= 0.55) DisplayQuality = PerformanceQualityExtended.Good;
            else if (composite >= 0.35) DisplayQuality = PerformanceQualityExtended.Fair;
            else DisplayQuality = PerformanceQualityExtended.Poor;

            OnPropertyChanged(nameof(CurrentLiveState));
        }

        private void ApplyCoachMessage(InlineCoachMessage message)
        {
            CoachMessage          = message.ShortMessage ?? "";
            CoachSeverity         = message.Severity;
            IsCoachMessageVisible = !string.IsNullOrWhiteSpace(CoachMessage);
        }

        private void SubmitInlineCoachMessage(InlineCoachMessage message)
        {
            if (_feedbackPipeline == null || _inlineCoachFeedbackMapper == null)
            {
                ApplyCoachMessage(message);
                return;
            }

            var candidate = _inlineCoachFeedbackMapper.Map(message);
            if (candidate == null)
            {
                ApplyCoachMessage(message);
                return;
            }

            var context = _inlineCoachFeedbackMapper.BuildContext(message, _currentLiveState);
            var decision = _feedbackPipeline.Submit(candidate, context);
            if (decision.Kind != FeedbackDecisionKind.Approved)
                return;

            ApplyCoachMessage(new InlineCoachMessage
            {
                ShortMessage = _localization.GetString(decision.Candidate.Message),
                CoachingReason = message.CoachingReason,
                Severity = message.Severity,
                AutoDismissSeconds = message.AutoDismissSeconds
            });
        }

        private void ResetMetricsToIdle()
        {
            PrimaryMetricScore    = 0;
            SecondaryMetricScore  = 0;
            StabilityScore        = 0;
            HoldProgress          = 0;
            IsInComfortZone       = false;
            IsHoldingCorrectly    = false;
            IsSafetyLocked        = false;
            Quality               = PerformanceQuality.Poor;
            SessionElapsedSeconds = 0;
            IsCoachMessageVisible = false;
            CoachMessage          = "";
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Guidance panel builder
        // ─────────────────────────────────────────────────────────────────────────

        private void RebuildGuidanceItems(ExerciseTargetProfile profile)
        {
            var items = new ObservableCollection<GuidanceItem>();

            if (!string.IsNullOrEmpty(profile.ClinicalPurposeKey))
                items.Add(new GuidanceItem { IconGlyph = "\uE7BC", HeadingKey = "Guidance_ClinicalPurpose", BodyKey = profile.ClinicalPurposeKey });

            if (!string.IsNullOrEmpty(profile.PhysicalFocusKey))
                items.Add(new GuidanceItem { IconGlyph = "\uE9D9", HeadingKey = "Guidance_PhysicalFocus",   BodyKey = profile.PhysicalFocusKey });

            if (!string.IsNullOrEmpty(profile.CommonMistakesKey))
                items.Add(new GuidanceItem { IconGlyph = "\uE7BA", HeadingKey = "Guidance_CommonMistakes",  BodyKey = profile.CommonMistakesKey });

            if (!string.IsNullOrEmpty(profile.SafetyInfoKey))
                items.Add(new GuidanceItem { IconGlyph = "\uEA18", HeadingKey = "Guidance_SafetyInfo",      BodyKey = profile.SafetyInfoKey });

            GuidanceItems = items;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // INotifyPropertyChanged
        // ─────────────────────────────────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ─────────────────────────────────────────────────────────────────────────
        // IDisposable
        // ─────────────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _coordinator.ExerciseUpdated    -= OnExerciseUpdated;
            _coordinator.InlineCoachUpdated -= OnInlineCoachUpdated;
            if (IsExerciseActive) _coordinator.StopExercise();
        }
    }
}
