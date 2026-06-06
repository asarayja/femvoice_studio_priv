using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Converters;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Code-behind for ExerciseWindow — post-refactor.
    ///
    /// Ansvarsfordeling etter refaktor:
    ///   - All biofeedback-logikk lever i ExerciseDetailViewModel.
    ///   - Code-behind håndterer kun: database-tjenester, navigasjon (liste/detalj),
    ///     timer-visning, Start/Stop lifecycle og PropertyChanged-speiling til de
    ///     navngitte UI-elementene (ResonanceBar, StabilityBar, ShieldPanel, HoldPanel,
    ///     CoachHintPanel).
    ///   - Ingen ExerciseIntelligenceCoordinator-logikk gjenstår her.
    ///   - ExerciseDetailViewModel er eneste bindeledd mellom koordinatoren og UI.
    ///
    /// Ingen WPF DataContext-binding er brukt for Live Feedback — dette unngår
    /// konflikt med den eksisterende ItemsControl-bindingen på listesiden.
    /// </summary>
    public partial class ExerciseWindow : Window
    {
        private double _lastHoldProgress = 0.0;
        // ── Eksisterende felter (uendret) ────────────────────────────────────────
        private ExerciseDataService?    _exerciseService;
        private ExerciseTextService?    _exerciseTextService;
        private TrainingFrequencyService? _trainingService;
        private ObservableCollection<Exercise> _exercises = new();
        private Exercise? _currentExercise;

        private DispatcherTimer? _timer;
        private DateTime         _sessionStartTime;
        private int              _currentSessionId;
        private int              _elapsedSeconds;
        private bool             _isRunning;
        private SubjectiveReport? _lastSubjectiveReport;
        private AudioCaptureService? _exerciseAudioCapture;
        private PitchDetectionService? _exercisePitchDetector;
        private ResonanceProxyEngine? _exerciseResonanceEngine;
        private double _latestExerciseResonanceScore;
        private DateTime _lastExerciseMetricUpdateUtc = DateTime.MinValue;

        // ── Live Feedback — ViewModel (typed; resolved via DI) ──────────────────
        // Kept as dynamic to preserve graceful degradation when DI is unavailable.
        private ExerciseDetailViewModel? _viewModel;
        private IExerciseProfileFactory? _profileFactory;

        // ── Klinisk øktscoring (resolved via DI) ─────────────────────────────────
        private ExerciseSessionRecorder? _sessionRecorder;
        private ExerciseTargetProfile?   _activeProfile;
        private ExerciseSessionOutcome?  _lastSessionOutcome;

        // ── Adaptiv komfortsone (resolved via DI) ────────────────────────────────
        // Var dormant: InitializeAsync/UpdateZoneAsync ble aldri kalt, så ZoneUpdated
        // fyrte aldri og koordinatoren fikk kun statiske profilgrenser (audit-funn).
        private ComfortZoneController? _comfortZoneController;
        private bool _comfortZoneReady;

        // ── Adaptiv progresjon (fase 2 — resolved via DI) ────────────────────────
        // ProgressionOrchestrator var DI-registrert uten konsument, og
        // IExerciseProfileStore hadde aldri en skrive- eller lesesti (audit-funn).
        private ProgressionOrchestrator?   _progressionOrchestrator;
        private IExerciseProfileStore?     _profileStore;
        private ProgressionFeedbackMapper? _progressionFeedbackMapper;
        private FeedbackPipeline?          _feedbackPipeline;
        private FemVoiceScoreEngine?       _scoreEngine;

        /// <summary>Overrides eldre enn dette ignoreres — øvelsen returnerer naturlig
        /// til baseline etter en strain-fri periode (review-funn: ingen utløp gjorde
        /// nedjustering permanent).</summary>
        private static readonly TimeSpan ProfileOverrideMaxAge = TimeSpan.FromDays(28);

        // ── REMOVED: _coordinator field — coordinator access now belongs entirely
        //    to ExerciseDetailViewModel. Code-behind no longer subscribes directly.

        public ExerciseWindow()
        {
            InitializeComponent();
            InitializeServices();
            InitializeLiveFeedback();
            LoadExercises();
        }

        // ────────────────────────────────────────────────────────────────────────
        // Initialisering
        // ────────────────────────────────────────────────────────────────────────

        private void InitializeServices()
        {
            try
            {
                var appDataPath      = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "FemVoiceStudio");
                var connectionString = $"Data Source={System.IO.Path.Combine(appDataPath, "femvoice.db")}";

                // ExerciseDataService er DI-singleton; fallback bruker samme connection string (integrasjonsaudit-funn).
                _exerciseService     = App.Services?.GetService(typeof(ExerciseDataService)) as ExerciseDataService
                                       ?? new ExerciseDataService(connectionString);
                _exerciseTextService = new ExerciseTextService();
                _trainingService     = new TrainingFrequencyService(_exerciseService);

                _exerciseService.InitializeExercises();

                _timer          = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick    += OnTimerTick;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Feil ved initialisering: {ex.Message}", "Feil",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Resolves ExerciseDetailViewModel from DI and subscribes to its
        /// PropertyChanged events so code-behind can mirror values to named
        /// UI elements (preserves existing pattern; avoids DataContext conflict).
        /// </summary>
        private void InitializeLiveFeedback()
        {
            // KRITISK REKKEFØLGE (runtime-feil funnet her): panel-DataContext MÅ
            // settes umiddelbart etter at ViewModel er resolvet — FØR de øvrige
            // tjeneste-resolvene. Flere av dem kjører SQLite-skjema/DB-lesing ved
            // FØRSTE resolve (SqliteExerciseProfileStore.EnsureSchema, analytics-
            // repoet via recorder-kjeden, VocalHealthBaselineProvider i options-
            // fabrikken). Tidligere lå alt i samme try/catch med DataContext-
            // koblingen NEDERST: én exception → catch svelget den → Guidance-/
            // LiveFeedback-panelene mistet bindingen → GuidanceItemsList rendret
            // 0 items selv om ViewModel-en bygde dem korrekt.
            try
            {
                _viewModel = App.Services.GetService(typeof(ExerciseDetailViewModel))
                    as ExerciseDetailViewModel;

                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                    LiveFeedbackPanel.DataContext     = _viewModel;
                    ExerciseGuidancePanel.DataContext = _viewModel;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LiveFeedback] Kunne ikke initialisere ViewModel: {ex.Message}");
            }

            // Valgfrie tjenester — isolert per resolve slik at én feil aldri
            // stopper de andre (og aldri river ned panel-koblingen over).
            _profileFactory            = SafeResolve<IExerciseProfileFactory>() ?? new ExerciseProfileFactory();
            _sessionRecorder           = SafeResolve<ExerciseSessionRecorder>();
            _comfortZoneController     = SafeResolve<ComfortZoneController>();
            _progressionOrchestrator   = SafeResolve<ProgressionOrchestrator>();
            _profileStore              = SafeResolve<IExerciseProfileStore>();
            _progressionFeedbackMapper = SafeResolve<ProgressionFeedbackMapper>();
            _feedbackPipeline          = SafeResolve<FeedbackPipeline>();
            _scoreEngine               = SafeResolve<FemVoiceScoreEngine>();
        }

        /// <summary>
        /// Resolver en valgfri tjeneste fra DI uten at en konstruksjonsfeil
        /// (f.eks. SQLite-feil ved første skjema-init) river ned resten av
        /// initialiseringen. Feilen logges med typenavn slik at den faktiske
        /// synderen er synlig i Debug-output i stedet for å forsvinne stille.
        /// </summary>
        private static T? SafeResolve<T>() where T : class
        {
            try
            {
                return App.Services?.GetService(typeof(T)) as T;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DI] Resolve av {typeof(T).Name} feilet: {ex.Message}");
                return null;
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // ViewModel PropertyChanged → named UI element mirror
        // All UI-specific display logic that cannot be expressed as a pure data
        // binding (arc geometry, Storyboard triggers) lives here.  Everything else
        // in this switch is a thin bridge; all decisions come from the ViewModel.
        // ────────────────────────────────────────────────────────────────────────

        private void OnViewModelPropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_viewModel == null) return;

            switch (e.PropertyName)
            {
                // ── Hold arc (geometry cannot be expressed in pure XAML binding) ─
                case nameof(ExerciseDetailViewModel.HoldProgress):
                case nameof(ExerciseDetailViewModel.IsHoldingCorrectly):
                    MirrorHoldArc();
                    break;

                case nameof(ExerciseDetailViewModel.IsInComfortZone):
                case nameof(ExerciseDetailViewModel.IsSafetyLocked):
                    MirrorShieldPanel();
                    break;

                // ── Coach hint (Storyboard trigger must fire from code-behind) ───
                case nameof(ExerciseDetailViewModel.CoachMessage):
                case nameof(ExerciseDetailViewModel.IsCoachMessageVisible):
                case nameof(ExerciseDetailViewModel.CoachSeverity):
                    MirrorCoachHint();
                    break;

                // Indicator visibility and metric values are now bound in XAML; no mirroring needed here.
                

                // ── Session timer (audio-driven elapsed time) ────────────────────
                case nameof(ExerciseDetailViewModel.SessionElapsedSeconds):
                    var secs = _viewModel.SessionElapsedSeconds;
                    TimerDisplay.Text = $"{secs / 60:00}:{secs % 60:00}";
                    break;
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // Mirror helpers — read-only delegation from ViewModel state
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Maps ViewModel.ShieldState enum to ShieldPanel appearance.
        /// No branching on raw bool pairs — all state is pre-computed in ViewModel.
        /// </summary>
        private void MirrorShieldPanel()
        {
            if (_viewModel == null) return;

            var iconKey   = _viewModel.ShieldIconGlyph;
            var brushKey  = _viewModel.ShieldBrushKey;
            var textKey   = _viewModel.ShieldTextKey;
            var bgAlpha   = _viewModel.IsSafetyLocked ? (byte)51 : (byte)0;

            // Animate shield background color softly between states.
            var targetBrush = (Brush)FindResource(brushKey);
            ShieldIcon.Text = iconKey;

            // Try to animate background color if brushes are SolidColorBrush
            try
            {
                if (ShieldPanel.Background is SolidColorBrush current && targetBrush is SolidColorBrush target)
                {
                    var anim = new ColorAnimation
                    {
                        From = current.Color,
                        To = target.Color,
                        Duration = (Duration)FindResource("Theme_ShortDuration")
                    };
                    var bg = new SolidColorBrush(current.Color);
                    ShieldPanel.Background = bg;
                    bg.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                }
                else
                {
                    ShieldPanel.Background = targetBrush;
                }
            }
            catch
            {
                ShieldPanel.Background = targetBrush;
            }

            ShieldIcon.Foreground = targetBrush;

            // Resolve localisation key → display text via LocalizationService.
            ShieldText.Text       = LocalizationService.Instance[textKey];
            ShieldText.Foreground = targetBrush;
        }

        /// <summary>
        /// Recomputes the hold arc geometry from ViewModel.HoldProgress.
        /// WPF ArcSegment cannot be driven by pure XAML animation for arbitrary angles.
        /// </summary>
        private void MirrorHoldArc()
        {
            if (_viewModel == null) return;

            var progress = _viewModel.HoldProgress;
            HoldPercentText.Text = _viewModel.HoldProgressPercent;
            HoldStatusText.Text = _viewModel.IsHoldingCorrectly
                ? LocalizationService.Instance["LiveFeedback_HoldSteady_Tone"]
                : LocalizationService.Instance["LiveFeedback_HoldStart_Tone"];

            const double radius  = 36;
            const double cx      = 40;
            const double cy      = 40;

            var angleDeg = progress * 360.0;
            if (angleDeg >= 359.99) angleDeg = 359.99;

            var angleRad = angleDeg * Math.PI / 180.0;
            var endX     = cx + radius * Math.Sin(angleRad);
            var endY     = cy - radius * Math.Cos(angleRad);

            HoldArcSegment.Point    = new Point(endX, endY);
            HoldArcSegment.IsLargeArc = angleDeg > 180;
            HoldArcPath.Stroke        = (Brush)FindResource(_viewModel.HoldArcBrushKey);

            // Trigger success pulse when progress crosses completion threshold.
            if (_lastHoldProgress < 0.999 && progress >= 0.999)
            {
                try
                {
                    var sb = (Storyboard)HoldPanel.Resources["HoldSuccessPulse"];
                    sb.Begin(HoldPanel);
                }
                catch { }
            }

            _lastHoldProgress = progress;
        }

        /// <summary>
        /// Triggers coach hint Storyboard animations and syncs text/background.
        /// Storyboard.Begin must be called from code-behind — it cannot be data-bound.
        /// </summary>
        private void MirrorCoachHint()
        {
            if (_viewModel == null) return;

            if (_viewModel.IsCoachMessageVisible && !string.IsNullOrEmpty(_viewModel.CoachMessage))
            {
                CoachHintText.Text        = _viewModel.CoachMessage;
                CoachHintPanel.Background = SeverityToBackground(_viewModel.CoachSeverity);
                CoachHintPanel.Visibility = Visibility.Visible;
                ((Storyboard)CoachHintPanel.Resources["CoachFadeIn"]).Begin(CoachHintPanel);
            }
            else
            {
                var fadeOut = (Storyboard)CoachHintPanel.Resources["CoachFadeOut"];
                fadeOut.Completed += (_, _) => CoachHintPanel.Visibility = Visibility.Collapsed;
                fadeOut.Begin(CoachHintPanel);
            }
        }

        private static Brush SeverityToBackground(MessageSeverity severity) => severity switch
        {
            MessageSeverity.Warning    => TryTintBrush("WarningBrush", 0.12),
            MessageSeverity.Suggestion => TryTintBrush("WarningBrush", 0.08),
            _                          => TryTintBrush("InfoBrush", 0.10)
        };

        private static Brush TryTintBrush(string resourceKey, double opacity)
        {
            try
            {
                if (Application.Current?.FindResource(resourceKey) is SolidColorBrush sb)
                {
                    var clone = sb.Clone();
                    clone.Opacity = opacity;
                    return clone;
                }
            }
            catch { }
            return new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0, 0, 0));
        }

        private void OnDismissCoachHint(object sender, RoutedEventArgs e)
            => _viewModel?.DismissCoachMessageCommand.Execute(null);

        // ────────────────────────────────────────────────────────────────────────
        // Exercise Start / Stop
        // ────────────────────────────────────────────────────────────────────────

        private async void OnStartClick(object sender, RoutedEventArgs e)
        {
            if (_currentExercise == null || _exerciseService == null) return;

            _sessionStartTime  = DateTime.Now;
            _currentSessionId  = _exerciseService.StartSession(_currentExercise.ExerciseId);
            _elapsedSeconds    = 0;
            _isRunning         = true;
            _timer?.Start();

            StartButton.IsEnabled = false;
            StopButton.IsEnabled  = true;
            FeedbackText.Text     = Loc.Feedback_ExerciseStarted;

            // Ny økt = ny selvrapport. Panelet vises igjen ved øktslutt.
            _lastSubjectiveReport = null;
            SubjectiveReportPanel.Visibility = Visibility.Collapsed;

            // Delegate exercise start to ViewModel command.
            if (_viewModel != null)
            {
                _viewModel.StartExerciseCommand.Execute(null);
                LiveFeedbackPanel.Visibility = Visibility.Visible;
            }

            // Start aggregering ETTER StartExerciseCommand (koordinatorens synthetiske
            // default-state ved SetExerciseContext skal ikke inn i øktsnittet) og FØR
            // lydstart, slik at alle reelle evalueringsticks fanges.
            _sessionRecorder?.BeginSession(_currentExercise.ExerciseId, _currentSessionId);

            StartExerciseAudio();

            // Adaptiv komfortsone: last brukerens sonetilstand. Soneoppdateringen
            // (UpdateZoneAsync) skjer ved øktslutt med øktsnittene; ZoneUpdated-eventet
            // seeder da koordinatorens pitch-grenser/sonelås for kommende økter.
            if (_comfortZoneController != null && !_comfortZoneReady)
            {
                try
                {
                    await _comfortZoneController.InitializeAsync(userId: 1);
                    if (_scoreEngine != null)
                        await _scoreEngine.SetUserAsync(1);   // kreves før CalculateScoreAsync ved øktslutt
                    _comfortZoneReady = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ComfortZone] Init failed: {ex.Message}");
                }
            }
        }

        private async void OnStopClick(object sender, RoutedEventArgs e)
        {
            if (_exerciseService == null) return;

            _timer?.Stop();
            _isRunning = false;

            // REENTRANS-GUARD (review-funn): metoden er async void og yielder til
            // dispatcheren under await — uten guard kunne dobbeltklikk på Stopp
            // dobbel-fullføre økten (TotalSessions+2, dobbel orchestrator-kjøring).
            // Snapshot av øktidentiteten tas FØR awaits slik at navigasjon under
            // ventetiden ikke kan flytte override-lagringen til feil øvelse.
            var sessionId = _currentSessionId;
            var exercise  = _currentExercise;
            var profile   = _activeProfile;
            _currentSessionId = 0;
            StopButton.IsEnabled  = false;
            StartButton.IsEnabled = true;

            if (sessionId > 0 && exercise != null)
            {
                var score = CompleteSessionAndCalculateScore();
                _exerciseService.CompleteSession(sessionId, _elapsedSeconds, score, "");
                FeedbackText.Text   = GetCompletionFeedback(score);
                DetailProgress.Text = string.Format(Loc.Exercise_StepsProgress,
                                          exercise.TotalSessions + 1, score.ToString("F0"));
                UpdateTodaysStatus();

                // Vis selvrapport-panelet — klinisk riktig tidspunkt («hvordan
                // føltes økten?»). Panelet var hardkodet Collapsed og hele
                // selvrapport-kjeden var dermed død (review-funn).
                SubjectiveReportPanel.Visibility = Visibility.Visible;

                // Driv FemVoiceScoreEngine med øktsnittene (0-100): uten persisterte
                // score-snapshots var consecutiveStableDays alltid 0 og komfortsonen
                // kunne aldri UTVIDES (review-funn).
                if (_scoreEngine != null && _lastSessionOutcome != null
                    && _lastSessionOutcome.EvaluatedTicks > 0)
                {
                    try
                    {
                        await _scoreEngine.CalculateScoreAsync(
                            _lastSessionOutcome.AverageResonance * 100,
                            _lastSessionOutcome.ComfortCompliance * 100,
                            _lastSessionOutcome.AverageStability * 100,
                            _lastSessionOutcome.SessionHealthScore);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ScoreEngine] Calculate failed: {ex.Message}");
                    }
                }

                // Adaptiv komfortsone: oppdater sonen med øktsnittene (0-100-skala).
                // SessionHealthScore er øktrepresentativ (per-tick-snitt, cappet ved
                // lås-episode) — siste-tick-verdien lot transient strain på slutten
                // kontrahere sonen urettmessig (review-funn).
                if (_comfortZoneController != null && _comfortZoneReady && _lastSessionOutcome != null
                    && _lastSessionOutcome.EvaluatedTicks > 0)
                {
                    try
                    {
                        await _comfortZoneController.UpdateZoneAsync(
                            _lastSessionOutcome.AverageResonance * 100,
                            _lastSessionOutcome.ComfortCompliance * 100,
                            _lastSessionOutcome.AverageStability * 100,
                            score,
                            _lastSessionOutcome.SessionHealthScore);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ComfortZone] Update failed: {ex.Message}");
                    }
                }

                // Vent på at øktjournalføringen er committet før orchestratoren leser
                // historikken (review-funn: lese-etter-skriv-race mot fire-and-forget).
                if (_sessionRecorder?.LastPersistTask is { } persistTask)
                {
                    try { await persistTask; } catch { /* persist-feil svelges i recorderen */ }
                }

                // Adaptiv progresjon (fase 2): orchestratoren leser den persisterte
                // analytics-historikken (inkl. økten som nettopp ble journalført) og
                // foreslår profil-justering eller pause/regresjon. Forslaget
                // persisteres som override og lastes ved neste åpning av øvelsen.
                await EvaluateAdaptiveProgressionAsync(exercise, profile);
            }

            // Delegate stop to ViewModel command.
            if (_viewModel != null)
            {
                _viewModel.StopExerciseCommand.Execute(null);
                LiveFeedbackPanel.Visibility = Visibility.Collapsed;
            }

            StopExerciseAudio();
        }

        private void StopInternalExercise()
        {
            _timer?.Stop();
            _isRunning = false;
            StopExerciseAudio();
            _sessionRecorder?.AbortSession();   // avbrutt økt journalføres ikke
            if (_currentSessionId > 0)
            {
                _exerciseService?.CancelSession(_currentSessionId);
                _currentSessionId = 0;
            }
            StartButton.IsEnabled        = true;
            StopButton.IsEnabled         = false;
            LiveFeedbackPanel.Visibility = Visibility.Collapsed;
            _viewModel?.StopExerciseCommand.Execute(null);
        }

        // ────────────────────────────────────────────────────────────────────────
        // Timer tick
        // ────────────────────────────────────────────────────────────────────────

        private void OnTimerTick(object? sender, EventArgs e)
        {
            // Increment the local fallback counter on every tick when the session is
            // running. This value is used by CalculateScore() and by the display below
            // whenever the ViewModel (audio-driven) is not yet available.
            if (_isRunning)
    _elapsedSeconds++;

var secs = Math.Max(_elapsedSeconds, _viewModel?.SessionElapsedSeconds ?? 0);
TimerDisplay.Text = $"{secs / 60:00}:{secs % 60:00}";

            if (_currentExercise != null && secs >= _currentExercise.DurationMinutes * 60)
                FeedbackText.Text = Loc.Feedback_TimeReached;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Detail view / navigation (uendret fra original)
        // ────────────────────────────────────────────────────────────────────────

        private async void ShowExerciseDetail(Exercise exercise)
{
    _currentExercise = exercise;
    var locKey = exercise.SortOrder + 100;

    DetailIcon.Text = exercise.Icon;

    var localizedName = _exerciseTextService?.GetLocalizedTitle(locKey);
    DetailName.Text = !string.IsNullOrEmpty(localizedName) ? localizedName : exercise.Name;

    DetailDifficulty.Text = GetDifficultyText(exercise.DifficultyLevel);
    DetailDuration.Text = string.Format(Loc.Exercise_DurationFormat, exercise.DurationMinutes);

    var localizedDescription = _exerciseTextService?.GetLocalizedDescription(locKey);
    DetailDescription.Text = !string.IsNullOrEmpty(localizedDescription)
                                ? localizedDescription
                                : exercise.Description;

    DetailProgress.Text = string.Format(
        Loc.Exercise_StepsProgress,
        exercise.TotalSessions,
        exercise.AverageScore.ToString("F0"));

    TimerHint.Text = string.Format(
        Loc.Exercise_RecommendedTime,
        exercise.DurationMinutes);

    var localizedRationale = _exerciseTextService?.GetLocalizedRationale(locKey);
    DetailRationale.Text = !string.IsNullOrEmpty(localizedRationale)
                                ? localizedRationale
                                : !string.IsNullOrEmpty(exercise.ScientificRationale)
                                    ? exercise.ScientificRationale
                                    : Loc.Tip_Beginner;

    // Build step list
    var steps = exercise.DisplaySteps.Count > 0
                    ? exercise.DisplaySteps
                    : exercise.GetStepsList();

    var stepItems = new ObservableCollection<StepItem>();
    for (int i = 0; i < steps.Count; i++)
    {
        stepItems.Add(new StepItem
        {
            Number = (i + 1).ToString(),
            Text = steps[i]
        });
    }
    StepsList.ItemsSource = stepItems;

    // Reset session UI state
    _elapsedSeconds = 0;
    _isRunning = false;
    _timer?.Stop();

    TimerDisplay.Text = "00:00";
    StartButton.IsEnabled = true;
    StopButton.IsEnabled = false;

    FeedbackText.Text = "";
    LiveFeedbackPanel.Visibility = Visibility.Collapsed;
    SubjectiveReportPanel.Visibility = Visibility.Collapsed;   // ny øvelse = nytt skjema
    _lastSubjectiveReport = null;

    // 🔧 ENSURE VIEWMODEL EXISTS BEFORE APPLYING PROFILE
    if (_viewModel == null)
        InitializeLiveFeedback();

    // Apply profile to ViewModel — drives indicators + Guidance
    var profile = (_profileFactory ?? new ExerciseProfileFactory()).CreateProfile(exercise.ProfileType);

    // Fase 2: bruk persistert profil-override fra ProgressionOrchestrator hvis den
    // finnes — dette er leddet som gjør adaptiv per-øvelse-vanskelighet reell.
    // Overrides eldre enn ProfileOverrideMaxAge ignoreres: profilen returnerer
    // naturlig til fabrikk-baseline etter en strain-fri periode (review-funn:
    // uten utløp kunne en regresjons-nedjustering bli permanent).
    if (_profileStore != null)
    {
        try
        {
            var profileOverride = await _profileStore.GetAsync(userId: 1, exerciseId: exercise.ExerciseId);
            if (profileOverride?.Profile != null
                && DateTime.UtcNow - profileOverride.UpdatedAt <= ProfileOverrideMaxAge)
            {
                profile = profileOverride.Profile;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdaptiveProgression] Override load failed: {ex.Message}");
        }
    }

    _activeProfile = profile;   // brukes av ClinicalSessionScore ved øktslutt

    _viewModel?.ApplyProfile(profile, exercise.ExerciseId);

    ListView.Visibility = Visibility.Collapsed;
    DetailView.Visibility = Visibility.Visible;
}

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (_isRunning) StopInternalExercise();
            DetailView.Visibility = Visibility.Collapsed;
            ListView.Visibility   = Visibility.Visible;
            LoadExercises();
        }

        private void OnExerciseClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Exercise exercise)
                ShowExerciseDetail(exercise);
        }

        // ────────────────────────────────────────────────────────────────────────
        // Load / filter exercises (uendret)
        // ────────────────────────────────────────────────────────────────────────

        private void LoadExercises()
        {
            try
            {
                if (_exerciseService == null) return;
                var exercises = _exerciseService.GetAllExercises();
                _exercises.Clear();

                foreach (var ex in exercises)
                {
                    var localizationKey    = ex.SortOrder + 100;
                    var localizedName      = _exerciseTextService?.GetLocalizedTitle(localizationKey);
                    if (!string.IsNullOrEmpty(localizedName)) ex.Name = localizedName;

                    var localizedDesc      = _exerciseTextService?.GetLocalizedDescription(localizationKey);
                    if (!string.IsNullOrEmpty(localizedDesc)) ex.Description = localizedDesc;

                    var locService         = LocalizationService.Instance;
                    ex.DisplayDifficulty   = ex.DifficultyLevel switch
                    {
                        DifficultyLevel.Nybegynner => locService["Difficulty_Beginner"],
                        DifficultyLevel.Middels    => locService["Difficulty_Intermediate"],
                        DifficultyLevel.Avansert   => locService["Difficulty_Advanced"],
                        _                          => locService["Difficulty_Beginner"]
                    };
                    ex.FrequencyText = ex.FrequencyText switch
                    {
                        "Daglig"   => locService["Frequency_Daily"],
                        "3×/uke"   => locService["Frequency_3xWeek"],
                        "2×/uke"   => locService["Frequency_2xWeek"],
                        "Ukentlig" => locService["Frequency_Weekly"],
                        _          => ex.FrequencyText
                    };

                    var localizedSteps = _exerciseTextService?.GetLocalizedStepsList(localizationKey);
                    ex.DisplaySteps    = (localizedSteps != null && localizedSteps.Count > 0)
                                        ? localizedSteps : ex.GetStepsList();

                    _exercises.Add(ex);
                }

                ExerciseList.ItemsSource = _exercises;
                UpdateTodaysStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Feil ved lasting av øvelser: {ex.Message}", "Feil",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateTodaysStatus()
        {
            if (_exerciseService == null) return;
            MinutesText.Text  = _exerciseService.GetTotalMinutesToday().ToString();
            SessionsText.Text = $" ({_exerciseService.GetCompletedSessionsToday()} {Loc.Exercise_SessionsCount})";
        }

        private void OnCategoryClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string category)
            {
                FilterByCategory(category);
                CatAll.Background        = Brushes.Transparent;
                CatPitch.Background      = Brushes.Transparent;
                CatResonance.Background  = Brushes.Transparent;
                CatIntonation.Background = Brushes.Transparent;
                CatBreathing.Background  = Brushes.Transparent;
                CatPractice.Background   = Brushes.Transparent;
                button.Background        = (Brush)FindResource("AccentPrimaryBrush");
            }
        }

        private void FilterByCategory(string category)
        {
            if (_exerciseService == null) return;
            var categoryIndex = int.TryParse(category, out var idx) ? idx : 0;
            var filtered = categoryIndex == 0
                ? _exerciseService.GetAllExercises()
                : _exerciseService.GetAllExercises().Where(e =>
                {
                    if (e == null || string.IsNullOrEmpty(e.Category)) return false;
                    var catLower = e.Category.ToLower();
                    if (categoryIndex == 1 && (catLower.Contains("pitch")    || catLower.Contains("kontroll"))) return true;
                    if (categoryIndex == 2 && (catLower.Contains("resonans") || catLower.Contains("vibrasjon"))) return true;
                    if (categoryIndex == 3 &&  catLower.Contains("intonasjon")) return true;
                    if (categoryIndex == 4 &&  catLower.Contains("pust"))       return true;
                    if (categoryIndex == 5 && (catLower.Contains("avansert") || catLower.Contains("praksis"))) return true;
                    return false;
                }).ToList();

            _exercises.Clear();
            foreach (var ex in filtered.Where(ex => ex != null))
                _exercises.Add(ex);
            ExerciseList.ItemsSource = _exercises;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Helpers (uendret)
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fullfører øktaggregeringen og beregner klinisk øktscore (0–100).
        ///
        /// Erstatter den gamle tidsbaserte CalculateScore (tid + oppmøtebonus) som
        /// gjorde at høy score — og dermed Mastery — kunne oppnås uten stemmedata.
        /// Nå scorer ClinicalSessionScore på resonans/stabilitet/komfort/hold med
        /// harde sikkerhetscaps; tid er kun et gulvkrav. Pitch teller bare som
        /// sone-compliance, aldri som høyde.
        /// </summary>
        private double CompleteSessionAndCalculateScore()
        {
            if (_currentExercise == null) return 0;

            var outcome = _sessionRecorder?.CompleteSession();
            _lastSessionOutcome = outcome;   // brukes av komfortsone-oppdateringen ved øktslutt
            if (outcome == null || _activeProfile == null)
                return 0;   // ingen verifiserte stemmedata → ingen score

            return ClinicalSessionScore.Calculate(
                outcome,
                _activeProfile,
                _elapsedSeconds,
                _currentExercise.DurationMinutes * 60);
        }

        private string GetCompletionFeedback(double score)
        {
            if (score >= 90) return Loc.Feedback_Excellent;
            if (score >= 70) return Loc.Feedback_Good;
            if (score >= 50) return Loc.Feedback_Nice;
            return Loc.Feedback_Completed;
        }

        private void OnSubmitSubjectiveReportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _lastSubjectiveReport = new SubjectiveReport
                {
                    UserId = 1,
                    SessionId = _currentSessionId > 0 ? _currentSessionId : null,
                    ComfortLevel = (int)Math.Round(SubjectiveComfortSlider.Value),
                    FatigueFeeling = (int)Math.Round(SubjectiveFatigueSlider.Value),
                    ExperiencedStrain = SubjectiveStrainCheckBox.IsChecked == true,
                    WantsToContinue = SubjectiveContinueCheckBox.IsChecked != false,
                    OptionalNotes = string.IsNullOrWhiteSpace(SubjectiveNotesTextBox.Text)
                        ? null
                        : SubjectiveNotesTextBox.Text.Trim(),
                    SubmittedAt = DateTime.Now
                };

                // Rapporten var tidligere write-only (audit-funn) — nå journalføres
                // helsebekymringer som PauseRecommended-events i analytics, slik at
                // MasteryEvaluator og ProgressionSafetyGate kan gate på dem.
                _sessionRecorder?.SubmitSubjectiveReport(_lastSubjectiveReport);

                SubjectiveReportStatusText.Text = Loc.Get("SubjectiveReport_Saved");
            }
            catch
            {
                SubjectiveReportStatusText.Text = Loc.Get("SubjectiveReport_SaveFailed");
            }
        }

        private void StartExerciseAudio()
        {
            StopExerciseAudio();

            try
            {
                _latestExerciseResonanceScore = 0;
                _lastExerciseMetricUpdateUtc = DateTime.MinValue;

                _exercisePitchDetector = new PitchDetectionService();
                _exerciseAudioCapture = new AudioCaptureService();
                _exerciseAudioCapture.InitializeLowLatency();
                _exerciseAudioCapture.HearOwnVoice = GetHearOwnVoiceSetting();
                if (_exerciseAudioCapture.CalibrationProfile != null)
                    _exercisePitchDetector.VoicedRmsThreshold = _exerciseAudioCapture.CalibrationProfile.VoicedRmsThreshold;

                _exerciseResonanceEngine = App.Services.GetService(typeof(ResonanceProxyEngine)) as ResonanceProxyEngine
                    ?? new ResonanceProxyEngine();
                if (_exerciseAudioCapture.CalibrationProfile != null)
                {
                    _exerciseResonanceEngine.RmsThreshold = Math.Clamp(
                        _exerciseAudioCapture.CalibrationProfile.VoicedRmsThreshold * 0.75,
                        0.001,
                        0.01);
                }

                _exerciseResonanceEngine.ResonanceScoreUpdated += OnExerciseResonanceScoreUpdated;
                _exerciseResonanceEngine.Start();

                _exerciseAudioCapture.AudioDataAvailable += OnExerciseAudioDataAvailable;
                _exerciseAudioCapture.ErrorOccurred += OnExerciseAudioError;
                _exerciseAudioCapture.StartRecording();
            }
            catch (Exception ex)
            {
                FeedbackText.Text = string.Format(Loc.Get("Audio_MicrophoneStartFailedFormat"), ex.Message);
                StopExerciseAudio();
            }
        }

        private void StopExerciseAudio()
        {
            if (_exerciseAudioCapture != null)
            {
                _exerciseAudioCapture.AudioDataAvailable -= OnExerciseAudioDataAvailable;
                _exerciseAudioCapture.ErrorOccurred -= OnExerciseAudioError;
                _exerciseAudioCapture.Dispose();
                _exerciseAudioCapture = null;
            }

            if (_exerciseResonanceEngine != null)
            {
                _exerciseResonanceEngine.ResonanceScoreUpdated -= OnExerciseResonanceScoreUpdated;
                _exerciseResonanceEngine.Stop();
                _exerciseResonanceEngine = null;
            }

            _exercisePitchDetector = null;
        }

        private void OnExerciseResonanceScoreUpdated(double score)
            => _latestExerciseResonanceScore = Math.Clamp(score, 0, 1);

        private void OnExerciseAudioDataAvailable(object? sender, float[] samples)
        {
            _exerciseResonanceEngine?.ProcessSamples(samples);

            if (_viewModel == null || _exercisePitchDetector == null)
                return;

            var now = DateTime.UtcNow;
            if ((now - _lastExerciseMetricUpdateUtc).TotalMilliseconds < 100)
                return;

            _lastExerciseMetricUpdateUtc = now;
            var pitch = _exercisePitchDetector.DetectPitch(samples);
            var rms = MicrophoneCalibrationService.CalculateRms(samples);
            var fallbackResonance = _exerciseAudioCapture?.CalibrationProfile != null
                ? Math.Clamp((rms - _exerciseAudioCapture.CalibrationProfile.NoiseFloorRms)
                    / Math.Max(0.001, _exerciseAudioCapture.CalibrationProfile.SpeechRms - _exerciseAudioCapture.CalibrationProfile.NoiseFloorRms), 0, 1)
                : Math.Clamp(rms / 0.05, 0, 1);
            var resonance = Math.Max(_latestExerciseResonanceScore, fallbackResonance * 0.45);
            var stability = pitch.IsVoiced
                ? Math.Clamp(pitch.Confidence, 0, 1)
                : Math.Clamp(resonance, 0, 1);
            // Health hentes fra VocalHealthSupervisor-tilstanden via recorderen
            // (tidligere hardkodet 100, som gjorde at <70-sikkerhetslåsen i
            // koordinatoren aldri kunne utløses fra øvelsesløkka).
            _viewModel.UpdateLiveMetrics(
                resonance,
                pitch.IsVoiced ? pitch.Pitch : 0,
                stability,
                _sessionRecorder?.CurrentHealthScore ?? 100);
        }

        /// <summary>
        /// Fase 2-aktivering: kjører ProgressionOrchestrator på øktens persisterte
        /// historikk (inkl. subjektiv rapport), persisterer foreslått profil som
        /// override via IExerciseProfileStore, og ruter beslutningen gjennom
        /// ProgressionFeedbackMapper → FeedbackPipeline → coach-panelet.
        /// </summary>
        private async Task EvaluateAdaptiveProgressionAsync(Exercise exercise, ExerciseTargetProfile? profile)
        {
            // Øktidentiteten kommer som SNAPSHOT-parametre tatt før awaits i OnStopClick —
            // _currentExercise/_activeProfile kan endres av navigasjon (Tilbake → annen
            // øvelse) mens orchestratorens SQLite-await pågår (review-funn: override
            // kunne persisteres mot feil øvelse).
            if (_progressionOrchestrator == null || profile == null)
                return;

            try
            {
                var decision = await _progressionOrchestrator.OnSessionCompletedAsync(
                    new ProgressionOrchestratorContext
                    {
                        UserId = 1,
                        ExerciseId = exercise.ExerciseId,
                        CurrentProfile = profile,
                        EvaluationTime = DateTime.Now,
                        SubjectiveReport = _lastSubjectiveReport
                    });

                // Persister foreslått profil — lastes igjen i ShowExerciseDetail.
                if (decision.SuggestedProfile != null
                    && decision.Kind is ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated
                        or ProgressionOrchestratorDecisionKind.RegressionTriggered
                    && _profileStore != null)
                {
                    // Recovery-gulv: RegressionTriggered skalerer fra GJELDENDE profil
                    // (som kan være en allerede nedjustert override) — uten gulv ville
                    // gjentatte regresjoner senke profilen kumulativt uten grense
                    // (review-funn). Gulvet er fabrikk-baseline minus to recovery-steg.
                    var profileToSave = decision.SuggestedProfile;
                    if (decision.Kind == ProgressionOrchestratorDecisionKind.RegressionTriggered)
                    {
                        var baseline = (_profileFactory ?? new ExerciseProfileFactory())
                            .CreateProfile(exercise.ProfileType);
                        profileToSave = ClampToRecoveryFloor(profileToSave, baseline);
                    }

                    await _profileStore.SaveAsync(new ExerciseProfileOverride
                    {
                        UserId = 1,
                        ExerciseId = exercise.ExerciseId,
                        Profile = profileToSave,
                        UpdatedAt = DateTime.UtcNow,
                        ReasonCode = decision.ReasonCode,
                        Source = "ProgressionOrchestrator"
                    });
                }

                // Brukerrettet progresjonsmelding (plateau/pause/regresjon/justering).
                if (_progressionFeedbackMapper != null && _feedbackPipeline != null)
                {
                    var candidate = _progressionFeedbackMapper.Map(decision);
                    if (candidate != null)
                        _feedbackPipeline.Submit(candidate, _progressionFeedbackMapper.BuildContext(decision));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AdaptiveProgression] Evaluate failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Begrenser en recovery-nedjustering til et klinisk gulv relativt til
        /// fabrikk-baselinen (maks to recovery-steg under: resonans/stabilitet -0.08,
        /// hold -2s) slik at gjentatte regresjoner konvergerer i stedet for å
        /// senke profilen ubegrenset.
        /// </summary>
        private static ExerciseTargetProfile ClampToRecoveryFloor(
            ExerciseTargetProfile suggested,
            ExerciseTargetProfile baseline)
        {
            var clamped = new ExerciseTargetProfile
            {
                UsesResonance = suggested.UsesResonance,
                UsesPitch = suggested.UsesPitch,
                UsesStability = suggested.UsesStability,
                UsesIntensity = suggested.UsesIntensity,
                ClinicalPurposeKey = suggested.ClinicalPurposeKey,
                PhysicalFocusKey = suggested.PhysicalFocusKey,
                CommonMistakesKey = suggested.CommonMistakesKey,
                SafetyInfoKey = suggested.SafetyInfoKey,
                FeedbackModeKey = suggested.FeedbackModeKey,
                ThresholdStrategyKey = suggested.ThresholdStrategyKey,
                IndicatorPackageSummaryKey = suggested.IndicatorPackageSummaryKey,
                MinPitch = suggested.MinPitch,
                MaxPitch = suggested.MaxPitch,
                TargetResonanceMin = Math.Max(suggested.TargetResonanceMin,
                    Math.Max(0, baseline.TargetResonanceMin - 0.08)),
                TargetResonanceMax = suggested.TargetResonanceMax,
                StabilityThreshold = Math.Max(suggested.StabilityThreshold,
                    Math.Max(0, baseline.StabilityThreshold - 0.08)),
                RequiredHoldSeconds = Math.Max(suggested.RequiredHoldSeconds,
                    Math.Max(0, baseline.RequiredHoldSeconds - 2))
            };

            clamped.Validate();
            return clamped;
        }

        private void OnExerciseAudioError(object? sender, string message)
        {
            Dispatcher.BeginInvoke(new Action(() => FeedbackText.Text = message));
        }

        private static bool GetHearOwnVoiceSetting()
        {
            try
            {
                // DatabaseService er DI-singleton; manuelle new re-kjørte skjema-init (integrasjonsaudit-funn).
                var db = App.Services?.GetService(typeof(DatabaseService)) as DatabaseService
                         ?? new DatabaseService();
                return db.GetUserSettings().HearOwnVoice;
            }
            catch
            {
                return false;
            }
        }

        private string GetDifficultyText(DifficultyLevel level) => level switch
        {
            DifficultyLevel.Nybegynner => Loc.Difficulty_Beginner,
            DifficultyLevel.Middels    => Loc.Difficulty_Intermediate,
            DifficultyLevel.Avansert   => Loc.Difficulty_Advanced,
            _                          => Loc.Difficulty_Beginner
        };

        // ────────────────────────────────────────────────────────────────────────
        // Window lifecycle
        // ────────────────────────────────────────────────────────────────────────

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            StopExerciseAudio();
            _sessionRecorder?.AbortSession();   // vindu lukket midt i økt → ikke journalfør
            _viewModel?.Dispose();   // Unsubscribes from coordinator events.
            // REMOVED: _coordinator.ExerciseUpdated -= ... (no direct coordinator ref)
            base.OnClosed(e);
        }
    }

    /// <summary>Hjelpeklasse for steg-visning (uendret).</summary>
    public class StepItem
    {
        public string Number { get; set; } = "";
        public string Text   { get; set; } = "";
    }
}
