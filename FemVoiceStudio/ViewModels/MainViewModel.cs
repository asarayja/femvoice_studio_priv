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
        // Brukerens personlige profil (stilmål + kalibrert komfortsone). Hentes i ctor
        // og brukes til å personliggjøre forsidens pitch-målsone. null = ingen profil/DB
        // ⇒ PersonalizePitchZone faller tilbake til den statiske policy-sonen.
        // IKKE readonly: ReloadUserVoiceProfile() re-leser den etter Settings-lagring og
        // øktslutt-kalibrering, slik at forsidens sone reflekterer ny kalibrering uten
        // restart (ingen parallell state — dette ER den ene cachen).
        private UserVoiceProfile? _userVoiceProfile;

        // Cachet recovery-flagg: true når den persisterte kliniske gaten
        // (ProgressionSafetyGate) er blokkert (gjentatte safety-locks, stigende
        // strain/fatigue, gjentatte komfortbrudd/pause-anbefalinger). Driver
        // recovery-grenen i TargetProfileAdapter.PersonalizePitchZone på forsiden slik
        // at målsonen KRYMPER mens brukeren restituerer. Settes (1) én gang asynkront
        // ved oppstart og (2) ved hver øktslutt fra det gate-resultatet som alt hentes
        // for ApplyExternalSafetyBlock — ingen ny gate-evaluering, ingen parallell state.
        // Klinisk invariant: kan kun KRYMPE sonen, aldri utvide krav.
        private bool _recoveryActive;

        // Den siste BASE-pitch-målsonen (per-difficulty policy-sone, FØR personalisering).
        // Lagres slik at en recovery-/profil-endring kan re-personalisere fra et rent
        // utgangspunkt i stedet for å re-krympe en allerede personalisert sone (som ville
        // dobbelt-krympe når recovery er aktiv). ActivePitchTargetZone er resultatet;
        // dette er inndataen.
        private Range _basePitchTargetZone = new(165, 255, 210);

        private readonly FemVoiceScore _femVoiceScore;
        private readonly SmartCoachEngine _smartCoach;
        private readonly PitchSmoother _pitchSmoother;

        // ── Feedback-pipeline (Agent 6: forsidens kliniske feedback-autoritet) ──────
        // Forsiden hadde en komplett, aktiv klinisk feedback-vei (RealtimeFeedback /
        // CoachExplanation / Feedback / status-promotering/safety-lås) som omgikk
        // FeedbackPipeline → FeedbackConsistencyGuard fullstendig. Suppresjonsmatrisen
        // (helse/strain/pause/fatigue undertrykker ros & teknikk-prat) gjaldt derfor
        // ALDRI forsiden. Disse rutes nå gjennom pipelinen.
        //
        // Null-safe: uten DI (tester / oppstart uten container) er pipelinen null og
        // forsiden faller tilbake til den DIREKTE, dokumenterte dagens-oppførselen
        // (skriver de bundne egenskapene rett) — eksisterende tester knekker ikke.
        private readonly FeedbackPipeline? _feedbackPipeline;
        private readonly MainScreenFeedbackMapper _mainScreenFeedbackMapper = new();
        private readonly MainScreenFeedbackDebouncer _mainScreenDebouncer = new();
        
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

        // ── Anbefalt øvelse-HINT (Agent VOL — Sprint C) ─────────────────────────────
        // SmartCoachs daglige anbefaling bærer en RecommendedExerciseId som forsiden
        // tidligere aldri viste. Forsidens treningsløkke (LoadNextExercise → GetRandomText)
        // velger lesetekster per vanskelighet — et ANNET domene enn øvelsesbiblioteket
        // (ID 1–50) anbefaleren returnerer. Vi TVINGER derfor ikke ID-en inn i løkka
        // (det ville vært en domene-feil); vi SURFACER den som et ikke-bindende hint som
        // forsiden kan vise. Recovery-orientering respekteres (Health > Goals):
        // CoachRecommendationIsRecoveryOriented settes når rådet er helse-/recovery-drevet.
        [ObservableProperty]
        private bool _hasCoachRecommendation;

        [ObservableProperty]
        private string _coachRecommendationHint = "";

        [ObservableProperty]
        private bool _coachRecommendationIsRecoveryOriented;

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
            // Gi tjenesten DB-tilgang slik at den kan persistere en kalibrert komfortsone
            // til UserVoiceProfile ved øktslutt og lese baseline-snapshot ved kaldstart.
            _comfortZoneService = new AdaptiveComfortZoneService(_smartCoach, _database as IDatabaseService);

            // Hent brukerprofilen én gang (null-safe) for personlig pitch-målsone.
            // GetUserVoiceProfile er en synkron, indeksert enkeltlesning på samme
            // DI-singleton som allerede er åpnet — trygt i ctor.
            try
            {
                _userVoiceProfile = _database.GetUserVoiceProfile(1);
            }
            catch
            {
                _userVoiceProfile = null;
            }

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

            // Klinisk feedback-pipeline — samme DI-singleton som øvelsesvinduet og
            // SmartCoach bruker. Mapperen (MainScreenFeedbackMapper) er ren/tilstandsløs
            // og konstrueres direkte (ingen DI-avhengighet, samme mønster som
            // ExerciseProfileFactory-fallbacken). Vi abonnerer på godkjente meldinger og
            // demukser dem til riktig bundet egenskap. Null-safe: ingen pipeline ⇒
            // direkte fallback overalt.
            try
            {
                _feedbackPipeline = App.Services?.GetService(typeof(FeedbackPipeline)) as FeedbackPipeline;
            }
            catch
            {
                _feedbackPipeline = null;
            }

            if (_feedbackPipeline != null)
                _feedbackPipeline.FeedbackApproved += OnPipelineFeedbackApproved;

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

            // Hent persistert recovery-status én gang (DB-tung gate-evaluering) uten å
            // blokkere ctor — krymper forsidens målsone hvis brukeren alt er i recovery.
            _ = InitializeRecoveryStateAsync();

            // Surface SmartCoachs anbefalte øvelse som et ikke-bindende hint (DB-tung
            // GenerateDailyRecommendation kjøres på trådpuljen). Null-safe.
            _ = InitializeCoachRecommendationAsync();
        }

        /// <summary>
        /// Henter SmartCoachs daglige anbefaling én gang og surfacer den anbefalte øvelsen
        /// som et IKKE-bindende hint på forsiden. Vi TVINGER aldri ID-en inn i den
        /// tekstbaserte treningsløkka (domenene matcher ikke) — kun visning. DB-tungt
        /// arbeid på trådpuljen; egenskaps-skriving marshales til UI-tråden. Null-safe:
        /// uten SmartCoach (tester/ingen DI) skjer ingenting, og dagens oppførsel beholdes.
        /// </summary>
        private async Task InitializeCoachRecommendationAsync()
        {
            if (_smartCoach == null)
                return;

            try
            {
                var recommendation = await Task.Run(() => _smartCoach.GenerateDailyRecommendation(1))
                    .ConfigureAwait(false);

                var (hasHint, hint, isRecovery) = BuildCoachRecommendationHint(recommendation);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    HasCoachRecommendation                 = hasHint;
                    CoachRecommendationHint                = hint;
                    CoachRecommendationIsRecoveryOriented  = isRecovery;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FemVoice][MainViewModel] InitializeCoachRecommendationAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Ren mapping fra en daglig anbefaling til forsidens øvelses-hint. Returnerer
        /// (harHint, hintTekst, erRecoveryOrientert). Recovery-orientering (HealthWarning
        /// eller FocusArea == "recovery") propageres slik at UI kan markere/dempe forslaget
        /// — Health &gt; Goals. Null-anbefaling ⇒ ingen hint.
        /// </summary>
        internal static (bool HasHint, string Hint, bool IsRecoveryOriented)
            BuildCoachRecommendationHint(SmartCoachDailyRecommendation? recommendation)
        {
            var isRecovery = recommendation != null
                && (recommendation.HealthWarning
                    || string.Equals(recommendation.FocusArea, "recovery", StringComparison.OrdinalIgnoreCase));

            if (recommendation?.RecommendedExerciseId is not int id || id <= 0)
                return (false, "", isRecovery);

            var hint = LocalizationService.Instance.GetFormattedString(
                "SmartCoach_RecommendedExerciseFormat", id);
            return (true, hint, isRecovery);
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
        
        /// <summary>Sikkerhetslås engasjert → synlig statusmelding (var stille før).
        /// Rutes som SafetyFreeze-kandidat (prioritet 80) — passerer ALLTID guarden;
        /// safety-informasjon skal aldri forsvinne.</summary>
        private void OnSafetyLockEngaged(object? sender, SafetyLockEventArgs e)
        {
            // Diskriminator ENGAGED/RELEASED ⇒ distinkte ReasonCodes slik at en frigjøring
            // tett etter en engasjering ikke rate-limiteres bort (begge er SafetyFreeze,
            // begge MÅ vises).
            RouteMainScreenFeedback(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.SafetyLockNotice,
                Loc.Get("Progression_SafetyLockEngagedStatus"),
                ReasonDiscriminator: "ENGAGED"));
        }

        private void OnSafetyLockReleased(object? sender, EventArgs e)
        {
            RouteMainScreenFeedback(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.SafetyLockNotice,
                Loc.Get("Progression_SafetyLockReleasedStatus"),
                ReasonDiscriminator: "RELEASED"));
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
                // Tidligere det stille no-mic-hullet: feilen oppsto FØR første
                // capture-logglinje, så et manglende lydoppsett var usynlig i loggen.
                Rc0RuntimeLog.Write("FrontPagePitchMonitor", $"InitializeAudio FAILED; {ex.GetType().Name}: {ex.Message}");
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

                // Rydd gamle feilmeldinger — feilruta ble aldri tømt, så en
                // transient feil fra forrige økt/oppstart ble stående synlig
                // selv når den nye økten fungerte fint.
                ErrorMessage = "";

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

                // Nullstill forsidens feedback-debounce slik at ny økt re-emitter live-
                // feedback selv om teksten tilfeldigvis er identisk med forrige økts siste.
                _mainScreenDebouncer.Reset();
                
                // Reset score values
                CurrentScore = null;
                ResonanceScore = 0;
                PitchScore = 0;
                IntonationScore = 0;
                VoiceHealthScore = 0;
                CoachExplanation = "";
                
                // AudioAnalyzerService er eneste aktive mikrofon-pipeline på forsiden.
                // Pitch-grafen drives av OnPitchAnalyzed -> LivePitchUpdateSequence.
                // Å starte AudioAnalysisEngine samtidig åpnet samme capture device to
                // ganger og kunne gi WASAPI/device-feil ved Start Økt.
                _audioAnalyzer.StartAnalysis();
                IsRecording = true;
                _uiUpdateTimer.Start();
                StatusText = Loc.UI_Recording;
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("FrontPagePitchMonitor", $"StartRecording FAILED; {ex.GetType().Name}: {ex.Message}");
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
                
                // Close debug log files
                DebugSettingsService.Instance.CloseLogs();

                // RC-0: snapshot diagnostikk og tellere FØR awaits lenger ned — et
                // re-klikk på Start under await-ene nullstiller analyzer-telleverket.
                var rc0AudioSnapshot = _audioAnalyzer.CaptureDiagnostics;
                var rc0PitchCalled = _audioAnalyzer.PitchDetectorCalledCount;
                var rc0PitchSamples = _audioAnalyzer.PitchSamplesCount;
                var rc0PitchRejected = _audioAnalyzer.PitchRejectedCount;
                var rc0StartTime = _sessionStartTime;
                var analysis = _audioAnalyzer.StopAnalysis();
                
                // Vis resultater
                AveragePitch = analysis.AveragePitch;
                PitchVariation = analysis.PitchStandardDeviation;
                
                // Generer tilbakemelding — øktslutt-oppsummeringen rutes gjennom
                // FeedbackPipeline (kandidat: PerformancePraise når økten kvalifiserer for
                // avansement, ellers ProgressionUpdate). Begge undertrykkes klinisk korrekt
                // når helse-/strain-/recovery-konteksten er aktiv ved øktslutt.
                // OverallScore er ren visningsverdi (ikke feedback) og settes direkte.
                var feedbackCollection = _feedbackService.GenerateFeedback(analysis, CurrentExercise);
                OverallScore = feedbackCollection.OverallScore;
                // Den genererte oppsummeringen persisteres i øktraden uavhengig av om
                // pipelinen viser den (en suppresjon skal ikke gi en tom øktjournal).
                // Selve PIPELINE-rutingen av oppsummeringen utsettes til ETTER en
                // eventuell promoterings-feiring: ved en sterk økt (promotering) er
                // feiringen overskriften brukeren vil se, og guardens rate-limiter
                // («multiple simultaneous hints» for prioritet < HealthWarning) slipper
                // bare den FØRSTE godkjente lav-prioritets-meldingen i vinduet gjennom.
                // Feiringen rutes derfor først (under), oppsummeringen etterpå.
                var sessionSummaryText = feedbackCollection.GetFormattedFeedback();
                var sessionSummaryIsPraise = feedbackCollection.ShouldAdvanceLevel;
                
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
                    Feedback = sessionSummaryText,
                    DifficultyLevel = CurrentDifficulty
                };

                var rc0SessionDbId = _database.SaveTrainingSession(session);
                Rc0RuntimeLog.Write("FrontPagePitchMonitor",
                    $"SessionSaved; SessionId={rc0SessionDbId}; OverallScore={feedbackCollection.OverallScore:F1}; " +
                    $"AvgPitch={analysis.AveragePitch:F1}; PitchSamples={_audioAnalyzer.PitchSamplesCount}");

                // Klinisk helseanalyse av den nettopp lagrede økten (MUST-FIX):
                // AnalyzeSessionForStrain er den ENESTE produksjonsskriveren til
                // SmartCoachHealthMonitoring, men ble aldri kalt i prod — så recovery-
                // omdirigeringen i GenerateDailyRecommendation (som leser nettopp den
                // tabellen) var reelt uoppnåelig live. Vi kjører den nå ved øktslutt slik
                // at pitch_press/fatigue persisteres og health_warning rutes via pipelinen.
                // _smartCoach er DI-instansen (full feedback-graf) når containeren finnes,
                // ellers en degradert fallback — begge er trygge å kalle. Strengt try/catch:
                // en feil her skal ALDRI kaste/blokkere øktslutt-stien (Safety-stien over
                // alt annet); vi svelger trygt og logger kun til debug.
                try
                {
                    _smartCoach.AnalyzeSessionForStrain(session, userId: 1);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[FemVoice][MainViewModel] AnalyzeSessionForStrain failed: {ex.Message}");
                }

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

                    // Recovery-aware målsoner: cache gate-blokkeringen og krymp forsidens
                    // pitch-målsone uten restart hvis flagget skiftet (samme gate-resultat,
                    // ingen ny evaluering). Vi er på UI-tråden her (StopRecording-RelayCommand),
                    // så SetRecoveryActive kan trygt skrive de UI-bundne sone-egenskapene.
                    SetRecoveryActive(RecoveryActivationPolicy.ForHomeZone(gate.IsBlocked));
                }

                // Evaluer progresjon — WithSafety-varianten undertrykker promotering
                // (og feiring) når sikkerhetslåsen er aktiv, uansett score.
                var progressionResult = _progressionService.EvaluateProgressionWithSafety(session);

                // Nøytral øktslutt-status settes FØRST som baseline (ikke-klinisk —
                // direkte). Promoterings-/kompleksitets-feiringen rutes deretter gjennom
                // FeedbackPipeline som ProgressionCelebration-kandidat (ProgressionUpdate);
                // den overstyrer kun StatusText hvis den GODKJENNES. Undertrykkes den av
                // helse-/recovery-konteksten, beholder vi den nøytrale baselinen i stedet
                // for å vise jubel mens helse-gaten er aktiv.
                StatusText = string.Format(Loc.Get("Difficulty_SessionCompleteFormat"), OverallScore);

                if (progressionResult.ShouldShowCelebration)
                {
                    CurrentDifficulty = progressionResult.NewDifficulty;
                    DifficultyText = GetDifficultyText(CurrentDifficulty);
                    RouteMainScreenFeedback(new MainScreenFeedbackIntent(
                        MainScreenFeedbackKind.ProgressionCelebration,
                        string.Format(Loc.Get("Difficulty_PromotedFormat"), DifficultyText)));
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
                        RouteMainScreenFeedback(new MainScreenFeedbackIntent(
                            MainScreenFeedbackKind.ProgressionCelebration,
                            Loc.Get("Progression_ComplexityAdvanced")));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Complexity] Advance failed: {ex.Message}");
                }

                // Øktslutt-oppsummeringen rutes NÅ (etter feiringen) gjennom pipelinen:
                // PerformancePraise når økten kvalifiserer for avansement, ellers en
                // nøytral ProgressionUpdate. Klinisk kontekst (helse/strain/recovery)
                // undertrykker begge korrekt; ved suppresjon beholder Feedback-panelet
                // forrige verdi (tømt ved øktstart) — ingen ros mens helse-gaten er aktiv.
                RouteMainScreenFeedback(new MainScreenFeedbackIntent(
                    MainScreenFeedbackKind.SessionSummary,
                    sessionSummaryText,
                    IsPraise: sessionSummaryIsPraise));

                // Last progresjonsstatus
                ProgressionStatus = _progressionService.GetProgressionStatus();
                CurrentStreak = ProgressionStatus?.CurrentStreak ?? 0;
                TotalSessions = ProgressionStatus?.TotalSessions ?? 0;

                // Last neste øvelse
                LoadNextExercise();

                // Persister kalibrert komfortsone + baseline-snapshot til UserVoiceProfile
                // ÉN gang per økt (ikke per tick). Øktens komfort-ratio (hvor godt snitt-
                // pitchen lå i komfortsonen) og helsescore snapshotes som BaselineComfort/
                // BaselineHealth. Kjøres off UI-tråden (DB-skriving) og re-applier sonen
                // på forsiden når kalibreringen faktisk endret seg.
                var sessionComfortRatio = CalculateSessionComfortRatio(
                    analysis.AveragePitch, ComfortZone);
                var sessionHealthScore = session.VoiceHealthScore;
                _ = PersistComfortZoneCalibrationAsync(sessionComfortRatio, sessionHealthScore);

                // RC-0 evidence-eksport for forside-monitoren (Test B). Ren observasjon
                // over allerede beregnede verdier.
                if (DebugSettingsService.Instance.EnableRc0Diagnostics)
                {
                    try
                    {
                        var reportVerification = ReportVerificationTracker.Snapshot();
                        var evidence = new Rc0EvidenceExporter.SessionEvidence
                        {
                            SessionId = rc0SessionDbId,
                            ExerciseId = CurrentExercise?.Id ?? 0,
                            ExerciseName = "FrontPagePitchMonitor",
                            Language = LocalizationService.Instance.CurrentLanguage,
                            StartTime = rc0StartTime,
                            EndTime = DateTime.Now,
                            Duration = DateTime.Now - rc0StartTime,
                            CompletionStatus = "COMPLETED",
                            Score = feedbackCollection.OverallScore,
                            ScoreSource = "FEEDBACK_OVERALL_SCORE",
                            PitchDetectorCalledCount = rc0PitchCalled,
                            PitchSamplesCount = rc0PitchSamples,
                            PitchRejectedCount = rc0PitchRejected,
                            VoiceHealthEvaluated = true,
                            AnalyticsWritten = rc0SessionDbId > 0,
                            PersistenceSaved = rc0SessionDbId > 0,
                            PersistenceReadBack = false,
                            ClinicalReportStatus = reportVerification.ClinicalReportStatus,
                            CoachReportStatus = reportVerification.CoachReportStatus,
                            OutcomeReportStatus = reportVerification.OutcomeReportStatus,
                            TimelineReportStatus = reportVerification.TimelineReportStatus,
                            ReportVerificationErrors = reportVerification.ReportVerificationErrors,
                            ClinicalReportGenerated = reportVerification.ClinicalReportGenerated,
                            CoachReportGenerated = reportVerification.CoachReportGenerated,
                            OutcomeReportGenerated = reportVerification.OutcomeReportGenerated,
                            TimelineReportGenerated = reportVerification.TimelineReportGenerated,
                            Notes = new[]
                            {
                                "Front-page monitor evidence: ResonanceSamplesCount/GraphUpdateCount/GuidanceItemCount " +
                                "are not tracked by this pipeline; graph behavior is logged under FrontPageGraph in the runtime log.",
                            },
                        };
                        var folder = Rc0EvidenceExporter.Export(evidence, rc0AudioSnapshot);
                        Rc0RuntimeLog.Write("FrontPagePitchMonitor",
                            $"EvidenceExported; SessionId={rc0SessionDbId}; Folder=\"{folder}\"");
                    }
                    catch (Exception ex)
                    {
                        Rc0WriteFailureSink.Report("MainViewModel.EvidenceExport", null, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log full exception (including stack) to RC0 runtime log for post-mortem
                try { Rc0RuntimeLog.Write("FrontPagePitchMonitor", ex.ToString()); } catch { }
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
            // Husk BASE-sonen slik at en senere recovery-/profil-endring kan re-
            // personalisere fra et rent utgangspunkt (RepersonalizePitchTargetZone),
            // ikke fra en allerede krympet sone.
            _basePitchTargetZone = zone;

            // Personliggjør den statiske policy-sonen mot brukerens kalibrerte komfortsone
            // og recovery-status. recoveryActive er det cachede gate-flagget (oppdateres
            // asynkront ved oppstart og ved øktslutt) — vi unngår en synkron DB-lesning fra
            // UI-event-handlerne / ctor-pathen som driver denne metoden. Recovery KRYMPER
            // kun sonen (klinisk invariant), aldri utvider.
            var (min, max) = TargetProfileAdapter.PersonalizePitchZone(
                (zone.Min, zone.Max), _userVoiceProfile, recoveryActive: _recoveryActive);

            var personalized = new Range(min, max);

            TargetMinPitch = personalized.Min;
            TargetMaxPitch = personalized.Max;
            ActivePitchTargetZone = personalized;
        }

        /// <summary>
        /// Re-personaliserer forsidens pitch-målsone fra den siste BASE-sonen. Brukes når
        /// en inndata til personaliseringen endrer seg uten at base-sonen gjør det — dvs.
        /// når recovery-flagget skifter eller den cachede profilen re-leses. Re-bruker
        /// _basePitchTargetZone slik at recovery-krympingen aldri stables (dobbelt-krymper).
        /// </summary>
        private void RepersonalizePitchTargetZone()
        {
            ApplyPitchTargetZone(_basePitchTargetZone);
        }

        /// <summary>
        /// Oppdaterer det cachede recovery-flagget fra et ferdig gate-resultat. Re-applier
        /// forsidens pitch-målsone KUN når flagget faktisk skifter, slik at sonen krymper/
        /// utvides uten restart. Idempotent: samme verdi ⇒ ingen re-apply.
        /// </summary>
        private void SetRecoveryActive(bool recoveryActive)
        {
            if (_recoveryActive == recoveryActive)
                return;

            _recoveryActive = recoveryActive;
            RepersonalizePitchTargetZone();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Klinisk feedback-pipeline — forsidens autoritet (Agent 6)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bygger guard-konteksten fra forsidens EKSISTERENDE runtime-tilstand slik at
        /// suppresjonsmatrisen virker på forsiden. Kilder (kun LESING — ingen ny state):
        ///   • HealthIndicator == Danger        ⇒ IsHealthRiskActive (helse-risiko)
        ///   • HealthIndicator == Warning        ⇒ IsActiveStrainAlert (aktiv belastning)
        ///   • siste FemVoiceScore.WarningFlags  ⇒ CRITICAL_STRAIN ⇒ helse-risiko;
        ///                                          (HIGH_)?STRAIN ⇒ strain-varsel
        ///   • _recoveryActive (cachet gate)     ⇒ IsPauseRecommended (pause anbefalt)
        /// Resultat: promoteringsjubel og teknikk-prat undertrykkes når helse-gaten slo
        /// til — det som var umulig før på forsiden.
        /// </summary>
        private MainScreenClinicalState BuildMainScreenClinicalState()
        {
            var warningFlags = CurrentScore?.WarningFlags ?? string.Empty;
            var criticalStrain = warningFlags.Contains("CRITICAL_STRAIN");
            var strain = warningFlags.Contains("STRAIN"); // dekker CRITICAL_/MODERATE_/HIGH_PITCH_STRAIN

            var healthRisk = HealthIndicator == HealthState.Danger || criticalStrain;
            var strainAlert = HealthIndicator == HealthState.Warning || strain;

            return new MainScreenClinicalState(
                IsHealthRiskActive: healthRisk,
                IsActiveStrainAlert: strainAlert,
                IsPauseRecommended: _recoveryActive,
                IsFatigueActive: false);
        }

        /// <summary>
        /// Ruter en forside-feedback-intensjon gjennom FeedbackPipeline. Live-kilder
        /// (pitch-sone-coaching ~30 Hz, coach-forklaring ~1 Hz) debounces FØR Submit på
        /// meldings-tekst (kun ved endret tekst, maks ~1/sek) slik at guardens
        /// per-ReasonCode rate-limiter ikke spammes. Fallback: uten pipeline skrives den
        /// bundne egenskapen direkte (dagens oppførsel, dokumentert).
        /// </summary>
        private void RouteMainScreenFeedback(MainScreenFeedbackIntent intent)
        {
            var binding = MainScreenFeedbackMapper.BindingFor(intent.Kind);

            // Pipeline utilgjengelig ⇒ behold dagens direkte oppførsel.
            if (_feedbackPipeline == null)
            {
                ApplyMainScreenFeedback(binding, intent.ResolvedText);
                return;
            }

            // Debounce KUN de høyfrekvente live-kildene. Øktslutt/promotering/safety-lås
            // er sjeldne enkelthendelser og skal aldri svelges av debounceren.
            var isLiveSource = intent.Kind is MainScreenFeedbackKind.PitchZoneCoaching
                or MainScreenFeedbackKind.CoachExplanation;
            if (isLiveSource && !_mainScreenDebouncer.ShouldSubmit(binding, intent.ResolvedText))
                return;

            var candidate = _mainScreenFeedbackMapper.Map(intent);
            if (candidate == null)
                return;

            _feedbackPipeline.Submit(candidate, _mainScreenFeedbackMapper.BuildContext(BuildMainScreenClinicalState()));
        }

        /// <summary>
        /// MOTTAK: godkjente meldinger fra pipelinen rutes til riktig bundet egenskap.
        /// Kun forsidens egne meldinger (Source == MainScreen) håndteres her — andre
        /// kilder (SmartCoach/Helse/Hydrering/Progresjon) eies av sine respektive
        /// vinduer. For forsidens kilder ER candidate.Message allerede ferdig oppløst
        /// tekst (ikke en lokaliseringsnøkkel) — samme konvensjon som SmartCoach-mapperen.
        /// FeedbackSuppressed abonneres bevisst IKKE: ved suppresjon BEHOLDES forrige
        /// melding i den bundne egenskapen (klinisk roligst — ingen blafring der et
        /// teknikk-/ros-hint undertrykkes av helse-gaten).
        /// </summary>
        private void OnPipelineFeedbackApproved(object? sender, FeedbackDecision decision)
        {
            if (decision.Candidate.Source != MainScreenFeedbackMapper.SourceName)
                return;

            var binding = MainScreenFeedbackMapper.BindingForReasonCode(decision.Candidate.ReasonCode);

            void Apply() => ApplyMainScreenFeedback(binding, decision.Candidate.Message);

            if (Application.Current?.Dispatcher.CheckAccess() == false)
                Application.Current.Dispatcher.BeginInvoke((Action)Apply);
            else
                Apply();
        }

        /// <summary>Skriver en godkjent (eller fallback-) melding til den bundne egenskapen.</summary>
        private void ApplyMainScreenFeedback(MainScreenFeedbackBinding binding, string text)
        {
            switch (binding)
            {
                case MainScreenFeedbackBinding.RealtimeFeedback:
                    RealtimeFeedback = text;
                    break;
                case MainScreenFeedbackBinding.CoachExplanation:
                    CoachExplanation = text;
                    break;
                case MainScreenFeedbackBinding.SessionFeedback:
                    Feedback = text;
                    break;
                case MainScreenFeedbackBinding.StatusText:
                    StatusText = text;
                    break;
            }
        }

        private void OnPitchAnalyzed(object? sender, PitchAnalysisResult result)
        {
            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() => OnPitchAnalyzed(sender, result)));
                return;
            }

            CurrentPitch = result.Pitch;
            CurrentIntensity = result.RmsValue;
            LivePitchUpdateSequence++;
            
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
            
            // Realtime feedback — rutes gjennom FeedbackPipeline (pitch-sone-coaching er
            // klinisk teknikk-feedback; undertrykkes når helse-gaten slo til).
            if (result.IsVoiced)
            {
                RouteMainScreenFeedback(new MainScreenFeedbackIntent(
                    MainScreenFeedbackKind.PitchZoneCoaching,
                    _feedbackService.GetRealtimeFeedback(result, ComfortZone.Min, ComfortZone.Max)));

                // Update coach explanation
                UpdateCoachExplanation();
            }
            else if (result.RmsValue < WeakSignalFeedbackRmsThreshold)
            {
                RouteMainScreenFeedback(new MainScreenFeedbackIntent(
                    MainScreenFeedbackKind.PitchZoneCoaching,
                    Loc.Get("LiveFeedback_SpeakLouder")));
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
            
            // Provide realtime feedback — gjennom FeedbackPipeline (klinisk pitch-sone-
            // coaching; debounced + undertrykt av helse-gaten der den slo til).
            if (pitch > 0)
            {
                RouteMainScreenFeedback(new MainScreenFeedbackIntent(
                    MainScreenFeedbackKind.PitchZoneCoaching,
                    _feedbackService.GetRealtimeFeedback(new PitchAnalysisResult
                    {
                        Pitch = pitch,
                        RmsValue = CurrentIntensity,
                        Intensity = CurrentIntensity,
                        IsVoiced = true,
                        Confidence = 1,
                        Timestamp = DateTime.Now
                    }, ActivePitchTargetZone.Min, ActivePitchTargetZone.Max)));

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
            
            Application.Current?.Dispatcher.Invoke(() =>
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
            System.Diagnostics.Debug.WriteLine($"[FemVoice][MainViewModel] Redundant audio engine error: {error}");
        }

        /// <summary>
        /// Safety-first nedstenging ved tap av lydkilde under opptak. Stopper UI-timer
        /// og analyse, og forkaster resultatet — en enhet-tapt økt skal IKKE føre til
        /// progresjon eller lagring av en korrupt økt (safety over progression).
        /// </summary>
        private void OnAudioEngineDeviceLost(string reason)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // AudioAnalysisEngine er en ekstra pitch-stream for forsidens graf.
                // Den vanlige øktanalysen drives av AudioAnalyzerService. En transient
                // WASAPI/device-feil her skal derfor ikke stoppe en ellers fungerende økt
                // eller sette forsiden i Audio_Error.
                ErrorMessage = reason;
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

            // Rutes gjennom FeedbackPipeline. Coach-forklaringen kan være en HealthWarning
            // når konteksten beskriver en helse-/belastningstilstand (samme signal som
            // GenerateExplanation selv key-er på via context.Health) — da får den
            // HealthWarning-prioritet og overlever der teknikk-prat undertrykkes.
            var isHealthContext = HealthIndicator is HealthState.Warning or HealthState.Danger;
            RouteMainScreenFeedback(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.CoachExplanation,
                _comfortZoneService.GenerateExplanation(context),
                IsHealthContext: isHealthContext));
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
        /// Henter den persisterte recovery-statusen ÉN gang ved oppstart (fire-and-forget
        /// fra ctor; blokkerer aldri konstruksjonen) og krymper forsidens pitch-målsone hvis
        /// brukeren allerede er i recovery fra forrige økt. Gate-evalueringen er DB-tung og
        /// kjøres på trådpuljen; flagg-skrivingen + re-personaliseringen marshales tilbake
        /// til UI-tråden via SetRecoveryActive (skriver UI-bundne sone-egenskaper).
        /// Null-safe: uten gate (f.eks. tester/ingen DI) er recovery av (dagens oppførsel).
        /// </summary>
        private async Task InitializeRecoveryStateAsync()
        {
            if (_progressionSafetyGate == null)
                return;

            try
            {
                var gate = await Task.Run(() => _progressionSafetyGate.EvaluateAsync(DateTime.Now))
                    .ConfigureAwait(false);

                Application.Current?.Dispatcher.Invoke(
                    () => SetRecoveryActive(RecoveryActivationPolicy.ForHomeZone(gate.IsBlocked)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FemVoice][MainViewModel] InitializeRecoveryStateAsync failed: {ex.Message}");
            }
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

        /// <summary>
        /// Øktens komfort-ratio (0-1): hvor godt øktens snitt-pitch lå innenfor den
        /// aktive komfortsonen. 1.0 = midt i sonen / godt innenfor; faller lineært mot 0
        /// jo lenger utenfor sonen snittet lå (én sonebredde utenfor ⇒ 0). Ren, sideeffekt-
        /// fri beregning fra verdier som alt finnes i øktslutt-stien (ingen ny state).
        /// </summary>
        private static double CalculateSessionComfortRatio(double averagePitch, Range comfortZone)
        {
            var width = comfortZone.Max - comfortZone.Min;
            if (width <= 0 || averagePitch <= 0)
                return 0.0;

            if (averagePitch >= comfortZone.Min && averagePitch <= comfortZone.Max)
                return 1.0;

            var distanceOutside = averagePitch < comfortZone.Min
                ? comfortZone.Min - averagePitch
                : averagePitch - comfortZone.Max;

            // Lineær avtrapping: én full sonebredde utenfor ⇒ 0.
            var ratio = 1.0 - distanceOutside / width;
            return Math.Clamp(ratio, 0.0, 1.0);
        }

        /// <summary>
        /// Persisterer en kalibrert komfortsone + baseline-snapshot til UserVoiceProfile
        /// off UI-tråden (DB-skriving), og re-applier forsidens pitch-målsone hvis
        /// kalibreringen faktisk endret seg. Kalt ÉN gang per økt fra øktslutt-stien.
        /// </summary>
        private async Task PersistComfortZoneCalibrationAsync(double sessionComfortRatio, double sessionHealthScore)
        {
            try
            {
                var updated = await Task.Run(() =>
                {
                    var sessionType = _comfortZoneService.GetRecommendedSessionType(1);
                    return _comfortZoneService.PersistCalibratedProfile(
                        userId: 1,
                        sessionType: sessionType,
                        sessionComfortRatio: sessionComfortRatio,
                        sessionHealthScore: sessionHealthScore);
                });

                if (updated != null)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        // Oppdater den cachede profilen og re-applier sonen uten restart.
                        // Re-personaliser fra BASE-sonen (ikke den allerede personaliserte
                        // ActivePitchTargetZone) — ellers ville recovery-krympingen stables.
                        _userVoiceProfile = updated;
                        RepersonalizePitchTargetZone();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FemVoice][MainViewModel] PersistComfortZoneCalibrationAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Re-leser brukerprofilen (stilmål + kalibrert komfortsone) og re-applier
        /// forsidens pitch-målsone. Lett reload-mekanisme kalt etter at Settings lukkes
        /// med lagring — uten den så MainViewModel aldri Settings-endringer før restart.
        /// Ingen event-buss: én eksplisitt re-lesning av den ene cachede profilen.
        /// </summary>
        public void ReloadUserVoiceProfile()
        {
            try
            {
                _userVoiceProfile = _database.GetUserVoiceProfile(1);
            }
            catch
            {
                // DB utilgjengelig ⇒ behold eksisterende cache.
                return;
            }

            // Re-personaliser fra BASE-sonen (ikke den allerede krympede ActivePitchTargetZone).
            RepersonalizePitchTargetZone();
        }

        private void OnUiTimerTick(object? sender, EventArgs e)
        {
            // UI update timer is kept for non-pitch related updates
            // Pitch data points are now added via OnSmoothedPitchUpdated event handler
            // This timer can be used for score calculations or other periodic updates
        }
        
        private void OnError(object? sender, string error)
        {
            Rc0RuntimeLog.Write("FrontPagePitchMonitor", $"ErrorOccurred; {error}");
            ErrorMessage = error;
            StatusText = Loc.Get("Error_Occurred");
        }
        
        public void Dispose()
        {
            _uiUpdateTimer.Stop();
            LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;

            if (_feedbackPipeline != null)
                _feedbackPipeline.FeedbackApproved -= OnPipelineFeedbackApproved;
            
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
