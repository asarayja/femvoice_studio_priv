using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Services.Progression;
using FemVoiceStudio.Converters;
using Range = FemVoiceStudio.Models.Range;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// Hoved-ViewModel for FemVoice Studio.
    /// Håndterer lydanalyse, økter og navigasjon.
    /// Inkluderer FemVoiceScore, komfortsone og helseindikatorer.
    /// </summary>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly AudioAnalyzerService _audioAnalyzer;
        private readonly AudioAnalysisEngine _audioEngine; // Event-driven real-time pitch streaming
        private readonly DatabaseService _database;
        private readonly ExerciseTextService _exerciseTextService;
        private readonly FeedbackService _feedbackService;
        private readonly ProgressionService _progressionService;
        private readonly ProgressionSafetyGate? _progressionSafetyGate;
        private readonly LiveMetricsService _liveMetrics;
        private readonly AdaptiveComfortZoneService _comfortZoneService;
        private readonly FemVoiceScore _femVoiceScore;
        private readonly SmartCoachEngine _smartCoach;
        private readonly PitchSmoother _pitchSmoother;
        
        private readonly DispatcherTimer _uiUpdateTimer;
        private DateTime _sessionStartTime;
        
        // Debounce for UpdateCoachExplanation — called on every pitch event,
        // so we throttle to avoid unnecessary work on the UI thread.
        private DateTime _lastCoachExplanationUpdate = DateTime.MinValue;
        private static readonly TimeSpan CoachExplanationInterval = TimeSpan.FromSeconds(1);
        private const double WeakSignalFeedbackRmsThreshold = 0.002;
        
        // Collections for charts
        public ObservableCollection<PitchDataPoint> PitchHistory { get; } = new();
        public ObservableCollection<double> RecentPitches { get; } = new();
        public ObservableCollection<ScoreSnapshot> ScoreHistory { get; } = new();
        
        // Konstanter for visning
        private const int MaxPitchHistoryPoints = 300;
        private const int MaxScoreHistoryPoints = 100;
        
        [ObservableProperty]
        private bool _isRecording;
        
        [ObservableProperty]
        private double _currentPitch;
        
        [ObservableProperty]
        private double _smoothedPitch;
        
        [ObservableProperty]
        private double _currentIntensity;
        
        [ObservableProperty]
        private string _realtimeFeedback = Loc.Get("Main_ReadyToStart");
        
        [ObservableProperty]
        private string _debugInfo = "";
        
        [ObservableProperty]
        private ExerciseText? _currentExercise;
        
        [ObservableProperty]
        private string _currentExerciseText = "";
        
        [ObservableProperty]
        private int _currentExerciseIndex;
        
        [ObservableProperty]
        private DifficultyLevel _currentDifficulty = DifficultyLevel.Nybegynner;
        
        [ObservableProperty]
        private string _difficultyText = "";
        
        [ObservableProperty]
        private double _targetMinPitch = 165;
        
        [ObservableProperty]
        private double _targetMaxPitch = 255;
        
        [ObservableProperty]
        private double _averagePitch;
        
        [ObservableProperty]
        private double _pitchVariation;
        
        [ObservableProperty]
        private string _feedback = "";
        
        [ObservableProperty]
        private double _overallScore;
        
        [ObservableProperty]
        private int _currentStreak;
        
        [ObservableProperty]
        private int _totalSessions;
        
        [ObservableProperty]
        private string _statusText = Loc.UI_Ready;
        
        [ObservableProperty]
        private bool _isMicrophoneReady;
        
        [ObservableProperty]
        private string _errorMessage = "";
        
        [ObservableProperty]
        private ProgressionStatus? _progressionStatus;
        
        [ObservableProperty]
        private bool _hearOwnVoice = true;
        
        // FemVoiceScore integration
        [ObservableProperty]
        private FemVoiceScoreResult? _currentScore;
        
        [ObservableProperty]
        private Range _comfortZone = new(165, 255, 210);

        [ObservableProperty]
        private Range _activePitchTargetZone = new(165, 255, 210);
        
        // Live metrics
        [ObservableProperty]
        private StabilityState _pitchStability = StabilityState.NoVoice;
        
        [ObservableProperty]
        private HealthState _healthIndicator = HealthState.NoVoice;
        
        [ObservableProperty]
        private double _currentResonance;
        
        // SmartCoach explanation
        [ObservableProperty]
        private string _coachExplanation = "";

        [ObservableProperty]
        private int _livePitchUpdateSequence;
        
        // Score components for display (equal visual weight)
        [ObservableProperty]
        private double _resonanceScore;
        
        [ObservableProperty]
        private double _pitchScore;
        
        [ObservableProperty]
        private double _intonationScore;
        
        [ObservableProperty]
        private double _voiceHealthScore;
        
        public MainViewModel()
        {
            // Initialiser services
            // DatabaseService er DI-singleton; manuelle new re-kjørte skjema-init (integrasjonsaudit-funn).
            _database = App.Services?.GetService(typeof(DatabaseService)) as DatabaseService
                        ?? new DatabaseService();
            _exerciseTextService = new ExerciseTextService();
            _feedbackService = new FeedbackService();
            _progressionService = new ProgressionService(_database as IDatabaseService);
            _audioAnalyzer = new AudioAnalyzerService();
            
            // Initialize event-driven AudioAnalysisEngine for real-time pitch streaming
            _audioEngine = new AudioAnalysisEngine();
            _audioEngine.PitchUpdated += OnPitchUpdated;
            _audioEngine.SmoothedPitchUpdated += OnSmoothedPitchUpdated;
            _audioEngine.ErrorOccurred += OnAudioEngineError;
            
            // Initialize pitch smoother for visualization
            _pitchSmoother = new PitchSmoother();
            
            // Initialize FemVoiceScore and related services
            _femVoiceScore = new FemVoiceScore();
            // DI-instansen har full feedback-graf (pipeline/mappere/goal-provider); den manuelle var degradert.
            _smartCoach = App.Services?.GetService(typeof(SmartCoachEngine)) as SmartCoachEngine
                          ?? new SmartCoachEngine(_database as IDatabaseService);
            _liveMetrics = new LiveMetricsService();
            _comfortZoneService = new AdaptiveComfortZoneService(_smartCoach);

            // Klinisk progresjonsgate — leser persistert helsehistorikk fra DI-containeren.
            // Null-safe: uten DI (f.eks. i tester) er gaten av, og kun in-memory-låsen gjelder.
            try
            {
                _progressionSafetyGate = App.Services?.GetService(typeof(ProgressionSafetyGate))
                    as ProgressionSafetyGate;
            }
            catch
            {
                _progressionSafetyGate = null;
            }

            // Sikkerhetslås-varsling: eventene fyrte reelt men hadde ingen abonnenter
            // (audit-funn) — progresjonsblokkering var usynlig for brukeren.
            _progressionService.SafetyLockEngaged += OnSafetyLockEngaged;
            _progressionService.SafetyLockReleased += OnSafetyLockReleased;
            
            // Sett opp event handlers
            _audioAnalyzer.PitchAnalyzed += OnPitchAnalyzed;
            _audioAnalyzer.ErrorOccurred += OnError;
            
            // Subscribe to language changes
            LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
            
            // Timer for UI-oppdateringer (30 FPS)
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _uiUpdateTimer.Tick += OnUiTimerTick;
            
            // Last brukerinnstillinger
            LoadUserSettings();
            
            // Initialiser lyd
            InitializeAudio();
            
            // Defer UpdateComfortZone() off the UI thread — it calls into SmartCoachEngine
            // which may trigger database reads (GetOrCalculateBaseline, GetTrainingStats etc.).
            // We schedule it on the thread pool so the constructor returns immediately.
            _ = Task.Run(UpdateComfortZoneAsync);
        }
        
        /// <summary>
        /// Handle language changes to update exercise text
        /// </summary>
        private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Item[]" || e.PropertyName == nameof(LocalizationService.CurrentCulture))
            {
                // Update the exercise text when language changes
                if (CurrentExercise != null)
                {
                    CurrentExerciseText = _exerciseTextService.GetLocalizedContent(CurrentExercise.Id);
                }
            }
        }
        
        /// <summary>Sikkerhetslås engasjert → synlig statusmelding (var stille før).</summary>
        private void OnSafetyLockEngaged(object? sender, SafetyLockEventArgs e)
        {
            StatusText = Loc.Get("Progression_SafetyLockEngagedStatus");
        }

        private void OnSafetyLockReleased(object? sender, EventArgs e)
        {
            StatusText = Loc.Get("Progression_SafetyLockReleasedStatus");
        }

        public void LoadUserSettings()
        {
            var settings = _database.GetUserSettings();
            CurrentDifficulty = settings.CurrentDifficulty;
            DifficultyText = GetDifficultyText(CurrentDifficulty);
            TargetMinPitch = settings.PreferredMinPitch;
            TargetMaxPitch = settings.PreferredMaxPitch;
            HearOwnVoice = settings.HearOwnVoice;
            
            // Sett HearOwnVoice i audio analyzer
            _audioAnalyzer.HearOwnVoice = HearOwnVoice;
            
            ProgressionStatus = _progressionService.GetProgressionStatus();
            CurrentStreak = ProgressionStatus?.CurrentStreak ?? 0;
            TotalSessions = ProgressionStatus?.TotalSessions ?? 0;
        }
        
        private static string GetDifficultyText(DifficultyLevel level)
        {
            return level switch
            {
                DifficultyLevel.Nybegynner => Loc.Difficulty_Beginner,
                DifficultyLevel.Middels => Loc.Difficulty_Intermediate,
                DifficultyLevel.Avansert => Loc.Difficulty_Advanced,
                _ => level.ToString()
            };
        }
        
        private void InitializeAudio()
        {
            try
            {
                _audioAnalyzer.Initialize();
                IsMicrophoneReady = true;
                StatusText = Loc.UI_MicReady;
                
                // Last første øvelse
                LoadNextExercise();
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(Loc.Get("Audio_MicrophoneStartFailedFormat"), ex.Message);
                IsMicrophoneReady = false;
                StatusText = Loc.UI_MicNotReady;
            }
        }
        
        private void LoadNextExercise()
        {
            CurrentExercise = _exerciseTextService.GetRandomText(CurrentDifficulty);
            if (CurrentExercise != null)
            {
                // Use localized content based on current language
                CurrentExerciseText = _exerciseTextService.GetLocalizedContent(CurrentExercise.Id);
                ApplyPitchTargetZone(PitchTargetZonePolicy.ClampForDifficulty(
                    CurrentDifficulty,
                    CurrentExercise.TargetMinPitch,
                    CurrentExercise.TargetMaxPitch));
                CurrentExerciseIndex = _exerciseTextService.GetTextsByDifficulty(CurrentDifficulty)
                    .IndexOf(CurrentExercise);
            }
        }
        
        [RelayCommand]
        private void StartRecording()
        {
            if (!IsMicrophoneReady || IsRecording)
                return;
                
            try
            {
                // Reload debug settings before starting
                DebugSettingsService.Instance.Reload();
                
                _sessionStartTime = DateTime.Now;
                PitchHistory.Clear();
                RecentPitches.Clear();
                ScoreHistory.Clear();
                Feedback = "";
                OverallScore = 0;
                
                // Reset live metrics for new session
                _liveMetrics.Reset();
                
                // Reset pitch smoother for new session
                _pitchSmoother.Reset();
                
                // Reset score values
                CurrentScore = null;
                ResonanceScore = 0;
                PitchScore = 0;
                IntonationScore = 0;
                VoiceHealthScore = 0;
                CoachExplanation = "";
                
                // Start event-driven audio engine for real-time pitch streaming
                _audioEngine.Initialize();
                _audioEngine.Start();
                
                _audioAnalyzer.StartAnalysis();
                IsRecording = true;
                _uiUpdateTimer.Start();
                StatusText = Loc.UI_Recording;
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(Loc.Get("Recording_StartFailedFormat"), ex.Message);
            }
        }
        
        [RelayCommand]
        private async Task StopRecording()
        {
            if (!IsRecording)
                return;

            try
            {
                _uiUpdateTimer.Stop();
                IsRecording = false;
                DebugInfo = "";
                
                // Stop event-driven audio engine
                _audioEngine.Stop();
                
                // Close debug log files
                DebugSettingsService.Instance.CloseLogs();
                
                var analysis = _audioAnalyzer.StopAnalysis();
                
                // Vis resultater
                AveragePitch = analysis.AveragePitch;
                PitchVariation = analysis.PitchStandardDeviation;
                
                // Generer tilbakemelding
                var feedbackCollection = _feedbackService.GenerateFeedback(analysis, CurrentExercise);
                Feedback = feedbackCollection.GetFormattedFeedback();
                OverallScore = feedbackCollection.OverallScore;
                
                // Lagre økt
                // OverallScore: feedbackCollection.OverallScore er den reelle
                // FemVoiceScore-baserte verdien (samme som vises i UI). Tidligere ble
                // analysis.OverallScore brukt — den settes aldri og var alltid 0, så
                // promotering var umulig og degradering feilutløstes.
                var session = new TrainingSession
                {
                    StartTime = _sessionStartTime,
                    EndTime = DateTime.Now,
                    ExerciseTextId = CurrentExercise?.Id ?? 0,
                    AveragePitch = analysis.AveragePitch,
                    MinPitch = analysis.MinPitch,
                    MaxPitch = analysis.MaxPitch,
                    PitchVariation = analysis.PitchStandardDeviation,
                    IntonationScore = analysis.IntonationRiseScore,
                    OverallScore = feedbackCollection.OverallScore,
                    VoiceHealthScore = CurrentScore?.VoiceHealthScore ?? 100,
                    StrainLevel = HealthIndicator == HealthState.Warning ? 50
                                : HealthIndicator == HealthState.Danger ? 80 : 0,
                    Feedback = Feedback,
                    DifficultyLevel = CurrentDifficulty
                };

                _database.SaveTrainingSession(session);

                // Klinisk gate FØR progresjonsevaluering:
                // 1) Strain-flagg fra siste FemVoiceScore → registrer hendelse (engasjerer
                //    in-memory-låsen i ProgressionService).
                var warningFlags = CurrentScore?.WarningFlags;
                if (!string.IsNullOrEmpty(warningFlags))
                {
                    if (warningFlags.Contains("CRITICAL_STRAIN"))
                        _progressionService.RecordStrainIncident(80, "CRITICAL_STRAIN");
                    else if (warningFlags.Contains("STRAIN"))
                        _progressionService.RecordStrainIncident(50, "MODERATE_STRAIN");
                }

                // 2) Persistert helsehistorikk (safety-locks/strain/fatigue/komfortbrudd
                //    siste 7-14 dager) — overlever restart, i motsetning til in-memory-låsen.
                if (_progressionSafetyGate != null)
                {
                    var gate = await _progressionSafetyGate.EvaluateAsync(DateTime.Now);
                    if (gate.IsBlocked)
                        _progressionService.ApplyExternalSafetyBlock(gate.ReasonCode, gate.RecommendedRestDays);
                }

                // Evaluer progresjon — WithSafety-varianten undertrykker promotering
                // (og feiring) når sikkerhetslåsen er aktiv, uansett score.
                var progressionResult = _progressionService.EvaluateProgressionWithSafety(session);

                if (progressionResult.ShouldShowCelebration)
                {
                    CurrentDifficulty = progressionResult.NewDifficulty;
                    DifficultyText = GetDifficultyText(CurrentDifficulty);
                    StatusText = string.Format(Loc.Get("Difficulty_PromotedFormat"), DifficultyText);
                }
                else
                {
                    StatusText = string.Format(Loc.Get("Difficulty_SessionCompleteFormat"), OverallScore);
                }

                // Talekompleksitet: TryAdvanceLevelAsync ble aldri kalt (audit-funn) —
                // nivået kunne bare leses, aldri heves. Motoren har egne kliniske
                // gater (helse/strain/suksessrate/øktfrekvens), så hevingen er gatet.
                try
                {
                    var complexityEngine = new ComplexityEngine(_database);
                    if (await complexityEngine.TryAdvanceLevelAsync(userId: 1)
                        && !progressionResult.ShouldShowCelebration)
                    {
                        StatusText = Loc.Get("Progression_ComplexityAdvanced");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Complexity] Advance failed: {ex.Message}");
                }
                
                // Last progresjonsstatus
                ProgressionStatus = _progressionService.GetProgressionStatus();
                CurrentStreak = ProgressionStatus?.CurrentStreak ?? 0;
                TotalSessions = ProgressionStatus?.TotalSessions ?? 0;
                
                // Last neste øvelse
                LoadNextExercise();
                
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(Loc.Get("Recording_StopFailedFormat"), ex.Message);
            }
        }
        
        [RelayCommand]
        private void NextExercise()
        {
            if (CurrentExercise != null)
            {
                var texts = _exerciseTextService.GetTextsByDifficulty(CurrentDifficulty);
                CurrentExerciseIndex = (CurrentExerciseIndex + 1) % texts.Count;
                if (CurrentExerciseIndex < texts.Count)
                {
                    CurrentExercise = texts[CurrentExerciseIndex];
                    // Use localized content based on current language
                    CurrentExerciseText = _exerciseTextService.GetLocalizedContent(CurrentExercise.Id);
                    ApplyPitchTargetZone(PitchTargetZonePolicy.ClampForDifficulty(
                        CurrentDifficulty,
                        CurrentExercise.TargetMinPitch,
                        CurrentExercise.TargetMaxPitch));
                }
            }
        }
        
        [RelayCommand]
        private void SetDifficulty(string difficulty)
        {
            if (Enum.TryParse<DifficultyLevel>(difficulty, out var level))
            {
                CurrentDifficulty = level;
                DifficultyText = GetDifficultyText(level);
                ApplyPitchTargetZone(PitchTargetZonePolicy.ForDifficulty(level));
                
                // Last første øvelse på dette nivået
                CurrentExerciseIndex = 0;
                LoadNextExercise();
            }
        }

        private void ApplyPitchTargetZone(Range zone)
        {
            TargetMinPitch = zone.Min;
            TargetMaxPitch = zone.Max;
            ActivePitchTargetZone = zone;
        }
        
        private void OnPitchAnalyzed(object? sender, PitchAnalysisResult result)
        {
            CurrentPitch = result.Pitch;
            CurrentIntensity = result.RmsValue;
            
            // Calculate smoothed pitch
            SmoothedPitch = _liveMetrics.CalculateSmoothedPitch(result.Pitch, result.IsVoiced);
            
            // Estimate resonance (simplified - in production would use FFT formant detection)
            double estimatedF2 = _liveMetrics.EstimateF2(result.RmsValue * 5000); // Rough proxy
            CurrentResonance = _liveMetrics.EstimateResonance(result.RmsValue * 5000, result.Pitch, result.RmsValue);
            
            // Calculate stability
            PitchStability = _liveMetrics.CalculateStability();
            
            // Calculate health (strain level estimated from intensity + pitch)
            double estimatedStrain = result.RmsValue > 0.7 ? result.RmsValue * 50 : 0;
            HealthIndicator = _liveMetrics.CalculateHealth(estimatedStrain, result.Pitch, result.RmsValue);
            
            // Calculate real-time FemVoiceScore if we have enough data
            if (result.IsVoiced && SmoothedPitch > 0)
            {
                CalculateLiveScore(result);
            }
            
            // Log pitch data if debug is enabled
            var debugService = DebugSettingsService.Instance;
            if (debugService.EnablePitchDebug)
            {
                debugService.LogPitchData(result.Pitch, result.RmsValue, result.IsVoiced, result.Confidence, ComfortZone.Min, ComfortZone.Max);
                DebugInfo = $"Pitch: {result.Pitch:F1} Hz | Smoothed: {SmoothedPitch:F1} Hz | RMS: {result.RmsValue:F4} | Voiced: {result.IsVoiced} | Stability: {PitchStability}";
            }
            else
            {
                DebugInfo = "";
            }
            
            // Realtime feedback
            if (result.IsVoiced)
            {
                RealtimeFeedback = _feedbackService.GetRealtimeFeedback(result, ComfortZone.Min, ComfortZone.Max);
                
                // Update coach explanation
                UpdateCoachExplanation();
            }
            else if (result.RmsValue < WeakSignalFeedbackRmsThreshold)
            {
                RealtimeFeedback = Loc.Get("LiveFeedback_SpeakLouder");
            }
        }
        
        /// <summary>
        /// Handle real-time pitch updates from AudioAnalysisEngine.
        /// Invoked on UI thread via SynchronizationContext - no Dispatcher needed.
        /// </summary>
        private void OnPitchUpdated(double pitch)
        {
            // Already on UI thread via SynchronizationContext in AudioAnalysisEngine
            CurrentPitch = pitch;
            LivePitchUpdateSequence++;
            
            // Calculate smoothed pitch for visualization
            SmoothedPitch = _liveMetrics.CalculateSmoothedPitch(pitch, true);
            if (SmoothedPitch <= 0)
                SmoothedPitch = _pitchSmoother.Apply(pitch);
            
            // Update pitch stability
            PitchStability = _liveMetrics.CalculateStability();
            CurrentIntensity = CurrentIntensity > 0 ? CurrentIntensity : 0.02;
            var estimatedStrain = CurrentIntensity > 0.7 ? CurrentIntensity * 50 : 0;
            HealthIndicator = _liveMetrics.CalculateHealth(estimatedStrain, pitch, CurrentIntensity);
            
            // Update debug info
            var debugService = DebugSettingsService.Instance;
            if (debugService.EnablePitchDebug)
            {
                DebugInfo = $"Pitch: {pitch:F1} Hz | Smoothed: {SmoothedPitch:F1} Hz | Stability: {PitchStability}";
            }
            
            // Provide realtime feedback
            if (pitch > 0)
            {
                RealtimeFeedback = _feedbackService.GetRealtimeFeedback(new PitchAnalysisResult
                {
                    Pitch = pitch,
                    RmsValue = CurrentIntensity,
                    Intensity = CurrentIntensity,
                    IsVoiced = true,
                    Confidence = 1,
                    Timestamp = DateTime.Now
                }, ActivePitchTargetZone.Min, ActivePitchTargetZone.Max);

                CalculateLiveScore(new PitchAnalysisResult
                {
                    Pitch = pitch,
                    RmsValue = CurrentIntensity,
                    Intensity = CurrentIntensity,
                    IsVoiced = true,
                    Confidence = 1,
                    Timestamp = DateTime.Now
                });
            }
            
            // Trigger property change notifications
            OnPropertyChanged(nameof(CurrentPitch));
            OnPropertyChanged(nameof(SmoothedPitch));
        }
        
        /// <summary>
        /// Handle smoothed pitch updates from AudioAnalysisEngine.
        /// Used for smoother graph visualization.
        /// </summary>
        private void OnSmoothedPitchUpdated(double smoothedPitch)
        {
            // Already on UI thread via SynchronizationContext
            SmoothedPitch = smoothedPitch;
            LivePitchUpdateSequence++;
            
            // Add to pitch history for graphing (throttled)
            AddPitchDataPoint(smoothedPitch);
            
            OnPropertyChanged(nameof(SmoothedPitch));
        }
        
        /// <summary>
        /// Add pitch data point to history for OxyPlot visualization.
        /// </summary>
        private void AddPitchDataPoint(double smoothedPitch)
        {
            if (smoothedPitch <= 0 || !IsRecording)
                return;
            
            // Throttle data point additions
            var now = DateTime.Now;
            if (PitchHistory.Count > 0)
            {
                var lastPoint = PitchHistory[PitchHistory.Count - 1];
                if ((now - lastPoint.Time).TotalMilliseconds < 33) // ~30 FPS max
                    return;
            }
            
            Application.Current?.Dispatcher.Invoke(() =>
            {
                bool isInComfortZone = smoothedPitch >= ComfortZone.Min && smoothedPitch <= ComfortZone.Max;
                
                PitchHistory.Add(new PitchDataPoint
                {
                    Time = now,
                    Pitch = CurrentPitch,
                    SmoothedPitch = smoothedPitch,
                    IsInRange = isInComfortZone,
                    Stability = PitchStability,
                    Health = HealthIndicator
                });
                
                // Keep only last N points
                while (PitchHistory.Count > MaxPitchHistoryPoints)
                {
                    PitchHistory.RemoveAt(0);
                }
            });
        }
        
        /// <summary>
        /// Handle audio engine errors.
        /// </summary>
        private void OnAudioEngineError(string error)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ErrorMessage = error;
                StatusText = Loc.Get("Audio_Error");
            });
        }
        
        /// <summary>
        /// Calculate live FemVoiceScore based on current metrics
        /// </summary>
        private void CalculateLiveScore(PitchAnalysisResult result)
        {
            // Build score input from current live metrics
            var input = new FemVoiceScoreInput
            {
                AveragePitch = SmoothedPitch,
                MinPitch = SmoothedPitch,
                MaxPitch = SmoothedPitch,
                PitchVariation = _liveMetrics.GetPitchVariance(),
                AverageF1 = 500, // Simplified - would use formant detection
                AverageF2 = _liveMetrics.EstimateF2(result.RmsValue * 5000),
                AverageF3 = 2500,
                SpectralCentroid = result.RmsValue * 5000,
                IntonationRange = Math.Clamp(Math.Sqrt(_liveMetrics.GetPitchVariance()) * 4, 0, 120),
                IntonationRiseScore = Math.Clamp(Math.Sqrt(_liveMetrics.GetPitchVariance()), 0, 60),
                StrainLevel = HealthIndicator == HealthState.Warning ? 50 : HealthIndicator == HealthState.Danger ? 80 : 0,
                IntensityRms = result.RmsValue,
                TargetMinPitch = ActivePitchTargetZone.Min,
                TargetMaxPitch = ActivePitchTargetZone.Max,
                DifficultyLevel = CurrentDifficulty
            };
            
            // Calculate score
            var scoreResult = _femVoiceScore.Calculate(input);
            
            // Update UI-bound properties
            CurrentScore = scoreResult;
            ResonanceScore = scoreResult.ResonanceScore;
            PitchScore = scoreResult.PitchScore;
            IntonationScore = scoreResult.IntonationScore;
            VoiceHealthScore = scoreResult.VoiceHealthScore;
            OverallScore = scoreResult.OverallScore;
            
            // Add to score history for visualization
            if (ScoreHistory.Count == 0 || (DateTime.Now - ScoreHistory.Last().Timestamp).TotalMilliseconds > 500)
            {
                ScoreHistory.Add(new ScoreSnapshot
                {
                    Timestamp = DateTime.Now,
                    OverallScore = scoreResult.OverallScore,
                    ResonanceScore = scoreResult.ResonanceScore,
                    PitchScore = scoreResult.PitchScore,
                    IntonationScore = scoreResult.IntonationScore,
                    VoiceHealthScore = scoreResult.VoiceHealthScore,
                    CurrentPitch = SmoothedPitch,
                    CurrentResonance = CurrentResonance,
                    CurrentStability = (double)PitchStability / 4.0 * 100
                });
                
                while (ScoreHistory.Count > MaxScoreHistoryPoints)
                {
                    ScoreHistory.RemoveAt(0);
                }
            }
        }
        
        /// <summary>
        /// Update SmartCoach explanation based on current state.
        /// Debounced to fire at most once per second — this is called on every pitch
        /// event (~30 Hz) so without throttling it would saturate the UI thread.
        /// GenerateExplanation() is pure computation (no DB I/O), so calling it on the
        /// UI thread is acceptable at 1 Hz.
        /// </summary>
        private void UpdateCoachExplanation()
        {
            if (CurrentScore == null)
                return;

            // Throttle: skip if we updated less than CoachExplanationInterval ago
            var now = DateTime.Now;
            if (now - _lastCoachExplanationUpdate < CoachExplanationInterval)
                return;
            _lastCoachExplanationUpdate = now;

            var context = new CoachExplanationContext
            {
                ResonanceScore = CurrentScore.ResonanceScore,
                PitchScore = CurrentScore.PitchScore,
                IntonationScore = CurrentScore.IntonationScore,
                VoiceHealthScore = CurrentScore.VoiceHealthScore,
                CurrentPitch = SmoothedPitch,
                CurrentResonance = CurrentResonance,
                Stability = PitchStability,
                Health = HealthIndicator,
                CurrentSessionType = SessionType.Progressive
            };

            CoachExplanation = _comfortZoneService.GenerateExplanation(context);
        }
        
        /// <summary>
        /// Update comfort zone from SmartCoach.
        /// Must NOT be called synchronously from the constructor or any UI event handler
        /// because GetRecommendedSessionType / CalculateComfortZone both call into
        /// SmartCoachEngine which reads the database.
        /// </summary>
        private void UpdateComfortZone()
        {
            // Synchronous overload — only safe to call from a background thread.
            // For the startup path, use UpdateComfortZoneAsync().
            var sessionType = _comfortZoneService.GetRecommendedSessionType(1);
            var zone = _comfortZoneService.CalculateComfortZone(1, sessionType);

            // Marshal results back to the UI thread for property writes
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ComfortZone = zone;
            });
        }

        /// <summary>
        /// Async wrapper: runs the blocking SmartCoach/DB work on the thread pool,
        /// then updates UI-bound properties on the UI thread. Called from the constructor
        /// via Task.Run() so startup is non-blocking.
        /// </summary>
        private async Task UpdateComfortZoneAsync()
        {
            try
            {
                await Task.Run(UpdateComfortZone);
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FemVoice][MainViewModel] UpdateComfortZoneAsync failed: {ex.Message}");
                });
            }
        }
        
        private void OnUiTimerTick(object? sender, EventArgs e)
        {
            // UI update timer is kept for non-pitch related updates
            // Pitch data points are now added via OnSmoothedPitchUpdated event handler
            // This timer can be used for score calculations or other periodic updates
        }
        
        private void OnError(object? sender, string error)
        {
            ErrorMessage = error;
            StatusText = Loc.Get("Error_Occurred");
        }
        
        public void Dispose()
        {
            _uiUpdateTimer.Stop();
            LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
            
            // Unsubscribe from AudioAnalysisEngine events to prevent memory leaks
            _audioEngine.PitchUpdated -= OnPitchUpdated;
            _audioEngine.SmoothedPitchUpdated -= OnSmoothedPitchUpdated;
            _audioEngine.ErrorOccurred -= OnAudioEngineError;
            _audioEngine.Dispose();
            
            _audioAnalyzer.Dispose();
            _database.Dispose();
        }
    }
    
    /// <summary>
    /// Pitch smoother using exponential moving average for smooth visualization.
    /// </summary>
    public class PitchSmoother
    {
        private double _smoothedValue;
        private const double Alpha = 0.3;
        
        public double Apply(double value)
        {
            if (value <= 0)
                return _smoothedValue;
            
            if (_smoothedValue <= 0)
                _smoothedValue = value;
            else
                _smoothedValue = Alpha * value + (1 - Alpha) * _smoothedValue;
            
            return _smoothedValue;
        }
        
        public void Reset()
        {
            _smoothedValue = 0;
        }
        
        public double CurrentValue => _smoothedValue;
    }
    
    /// <summary>
    /// Data point for pitch chart visualization
    /// </summary>
    public class PitchDataPoint
    {
        public DateTime Time { get; set; }
        public double Pitch { get; set; }
        public double SmoothedPitch { get; set; }
        public bool IsInRange { get; set; }
        public StabilityState Stability { get; set; }
        public HealthState Health { get; set; }
    }
}
