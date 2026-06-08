using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Services.Progression;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// ViewModel for Smart Coach-dashboardsider
    /// Håndterer daglig melding, økt-planer og brukerinteraksjon
    /// </summary>
    public partial class SmartCoachViewModel : ObservableObject
    {
        // Receives IDatabaseService via DI — must be the singleton already initialized
        // at startup. Never instantiate a new DatabaseService here; doing so would re-run
        // the full 4-step schema initialization on the UI thread.
        private readonly IDatabaseService _database;
        private readonly SmartCoachEngine _engine;
        
        // Concrete reference for the two callers that need the DatabaseService type directly:
        //   • GetUnreadMessages() — not on IDatabaseService (no virtual, not in interface)
        //   • ComplexityEngine constructor — requires DatabaseService, not IDatabaseService
        // All other calls go through _database (IDatabaseService).
        private readonly DatabaseService? _databaseConcrete;

        // Tilgjengelighet (Agent U): demper KUN presentasjonen av helsevarselet når
        // brukeren har slått på StressSensitiveMode / ReducedVisualFeedback. Resolves
        // null-safe via DI/App.Services. Klinisk invariant: HealthWarningText består
        // alltid uendret (Safety > UI) — kun farge/severity og antall sekundære
        // signaler endres. Null = ingen demping (full bakoverkompatibilitet).
        private readonly StressSensitiveExperience? _stressSensitive;

        [ObservableProperty]
        private string _todayFocus = "";
        
        [ObservableProperty]
        private string _todayRecommendation = "";
        
        [ObservableProperty]
        private string _focusArea = "";
        
        [ObservableProperty]
        private int _recommendedDuration = 5;
        
        [ObservableProperty]
        private bool _hasHealthWarning;
        
        [ObservableProperty]
        private string _healthWarningText = "";
        
        [ObservableProperty]
        private bool _isRecommendationCompleted;
        
        [ObservableProperty]
        private int _currentStreak;
        
        [ObservableProperty]
        private int _sessionsThisWeek;
        
        [ObservableProperty]
        private int _minutesThisWeek;
        
        [ObservableProperty]
        private double _healthScore = 100;
        
        [ObservableProperty]
        private string _statusSummary = "";
        
        [ObservableProperty]
        private double _pitchProgress;
        
        [ObservableProperty]
        private double _resonanceProgress;
        
        [ObservableProperty]
        private double _intonationProgress;
        
        // ============================
        // Complexity Properties
        // ============================
        
        [ObservableProperty]
        private SpeechComplexityLevel _currentComplexityLevel;
        
        [ObservableProperty]
        private string _currentComplexityLevelDisplay = "";
        
        [ObservableProperty]
        private int _sessionsAtCurrentComplexity;
        
        [ObservableProperty]
        private bool _isComplexityReadyForNext;
        
        [ObservableProperty]
        private string _complexityFeedback = "";
        
        [ObservableProperty]
        private List<ComplexityLevelStep> _complexityProgressionSteps = new();
        
        [ObservableProperty]
        private string _baselineConfidence = "";
        
        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private bool _hasData;

        // ============================
        // Anbefalt øvelse-surfacing (Agent VOL — Sprint C)
        // ============================
        //
        // SmartCoachs daglige anbefaling bærer en RecommendedExerciseId (øvelsesbibliotek
        // 1–50) som tidligere ALDRI ble eksponert — dashboardet viste kun FocusArea +
        // tekst. Vi surfacer den nå som et HINT/forslag (ikke et tvunget valg): forsidens
        // treningsløkke bruker lesetekster per vanskelighet, et annet domene, så å tvinge
        // ID-en inn der ville vært feil. Hintet vises ved siden av fokus-teksten.
        //
        // Recovery-orientering respekteres: når anbefalingen er helse-/recovery-drevet
        // (HealthWarning eller FocusArea == "recovery") settes IsRecommendationRecoveryOriented
        // slik at UI kan dempe/markere forslaget — Health > Goals.

        [ObservableProperty]
        private int _recommendedExerciseId;

        [ObservableProperty]
        private bool _hasRecommendedExercise;

        [ObservableProperty]
        private string _recommendedExerciseHint = "";

        [ObservableProperty]
        private bool _isRecommendationRecoveryOriented;

        // ============================
        // Adaptivt øktvolum (Agent VOL — Sprint C, 6-volum)
        // ============================
        //
        // Øvelsesvolum (antall øvelser + sett per økt) ble aldri foreslått. Vi beregner
        // det rent via AdaptiveDifficultyService.RecommendVolume ut fra Recovery/Comfort/
        // Consistency/Health og surfacer forslaget. Klinisk invariant: Health > Progression
        // — lav recovery/helse strammer ALLTID inn (IsVolumeReducedForRecovery), uansett
        // hvor god konsistensen er.

        [ObservableProperty]
        private int _suggestedExerciseCount = AdaptiveDifficultyService.BaselineExerciseCount;

        [ObservableProperty]
        private int _suggestedSetCount = 2;

        [ObservableProperty]
        private string _suggestedVolumeText = "";

        [ObservableProperty]
        private bool _isVolumeReducedForRecovery;

        [ObservableProperty]
        private string _volumeReason = "";
        
        [ObservableProperty]
        private int _unreadMessageCount;
        
        public ObservableCollection<SmartCoachGoal> ActiveGoals { get; } = new();
        public ObservableCollection<SmartCoachMessage> RecentMessages { get; } = new();
        public ObservableCollection<WeeklyProgressItem> WeeklyProgressList { get; } = new();

        // ============================
        // Tilgjengelighet — helsevarsel-presentasjon (Agent U)
        // ============================
        //
        // Klinisk invariant: disse egenskapene styrer KUN HVORDAN helsevarselet vises
        // (farge / severity / sekundære badges), ALDRI OM. HealthWarningText og
        // HasHealthWarning er uberørt — helsevarselet formidles alltid. Når
        // StressSensitiveMode er på dempes alarmfargen (ErrorBrush → WarningBrush) og
        // severity (Warning → Suggestion); når ReducedVisualFeedback er på reduseres
        // antall samtidige sekundære signaler MENS helsevarsel + safety beholdes.

        /// <summary>
        /// Brush-nøkkel for helsevarsel-teksten. Ruter "ErrorBrush" (rød alarm) gjennom
        /// <see cref="StressSensitiveExperience.SoftenBrushKey"/>: ved StressSensitiveMode
        /// dempes den til "WarningBrush" (varm). Ren/testbar — avhenger kun av tjenesten.
        /// </summary>
        public string HealthWarningBrushKey => Soften("ErrorBrush");

        /// <summary>
        /// Tema-resolvet pensel for helsevarsel-teksten, slått opp fra nøkkelen over.
        /// Null i test/design-kontekst uten Application-ressurser — XAML faller da trygt
        /// tilbake på arvet/standard farge uten å krasje.
        /// </summary>
        public Brush? HealthWarningBrush =>
            Application.Current?.TryFindResource(HealthWarningBrushKey) as Brush;

        /// <summary>
        /// Severity for helsevarselet. Et helsevarsel er klinisk en Warning; ved
        /// StressSensitiveMode dempes den til Suggestion via
        /// <see cref="StressSensitiveExperience.SoftenSeverity"/>. Innholdet beholdes.
        /// </summary>
        public MessageSeverity HealthWarningSeverity =>
            _stressSensitive?.SoftenSeverity(MessageSeverity.Warning) ?? MessageSeverity.Warning;

        /// <summary>
        /// Sann når brukeren har slått på ReducedVisualFeedback. UI bruker dette til å
        /// skjule/forenkle IKKE-kritiske sekundære badges. Helsevarsel og safety-info
        /// vises uansett (de bindes aldri til denne flaggen).
        /// </summary>
        public bool IsReducedVisual => _stressSensitive?.IsReducedVisual ?? false;

        /// <summary>
        /// Sann når sekundære (ikke-kritiske) visuelle badges skal vises. Invers av
        /// <see cref="IsReducedVisual"/>. Brukes for å dempe antall samtidige signaler i
        /// dashboardet uten å røre helse-/safety-indikatorene.
        /// </summary>
        public bool ShowSecondaryBadges => !IsReducedVisual;

        /// <summary>
        /// Ruter en brush-nøkkel gjennom StressSensitiveExperience når tjenesten finnes.
        /// Null-safe: uten tjenesten (eldre tester / ingen DI) returneres nøkkelen
        /// uendret — full bakoverkompatibilitet (ErrorBrush forblir ErrorBrush).
        /// </summary>
        private string Soften(string brushKey)
            => _stressSensitive?.SoftenBrushKey(brushKey) ?? brushKey;
        
        /// <summary>
        /// Design-time constructor (no-arg) for XAML tooling.
        /// For runtime use the DI constructor below.
        /// </summary>
        public SmartCoachViewModel()
            : this(null!, null!, null) { }

        /// <summary>
        /// Runtime constructor. Receives the already-initialized singleton services via DI.
        /// Does NOT call LoadDataAsync here — the View should call InitializeAsync() once it
        /// is loaded (e.g. from a Loaded event or an INavigationAware.OnNavigatedTo override).
        /// Keeping the constructor free of async/blocking calls ensures the UI thread stays
        /// responsive while the tab is being activated.
        /// </summary>
        public SmartCoachViewModel(
            IDatabaseService database,
            SmartCoachEngine engine,
            StressSensitiveExperience? stressSensitive = null)
        {
            // Accept null only from the design-time no-arg constructor path
            _database         = database!;
            _engine           = engine!;
            // Cache concrete type for methods not yet on IDatabaseService interface
            _databaseConcrete = database as DatabaseService;

            // Tilgjengelighets-tjenesten injiseres når DI har den (produksjon), ellers
            // resolves den null-safe fra App.Services. Mangler den helt (rene tester
            // uten DI), forblir _stressSensitive null → ingen demping. Kaster aldri.
            _stressSensitive = stressSensitive ?? ResolveStressSensitive();
            // Do NOT call LoadData() or LoadDataAsync() here.
        }

        private static StressSensitiveExperience? ResolveStressSensitive()
        {
            try
            {
                return App.Services?.GetService(typeof(StressSensitiveExperience))
                    as StressSensitiveExperience;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Entry point for the View — call this from the Loaded event or
        /// INavigationAware.OnNavigatedTo, never from the constructor.
        /// Fires-and-forgets the async load so the XAML binding system can call it
        /// without an await (e.g. from an event handler).
        /// </summary>
        public void Initialize()
        {
            // Guard: do not start a second load if one is already in progress
            if (IsLoading) return;
            _ = InitializeAsync();
        }

        /// <summary>
        /// Async entry point. Prefer this over Initialize() when the caller can await.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_database == null || _engine == null)
            {
                // Design-time / test path — nothing to load
                IsLoading = false;
                return;
            }
            await LoadDataAsync();
        }

        /// <summary>
        /// Loads all SmartCoach data on a background thread, then marshals each result
        /// back to the UI thread for property/collection writes.
        ///
        /// Threading contract:
        ///   • Task.Run block  → background thread pool (all DB + engine calls go here)
        ///   • Dispatcher.Invoke → UI thread (all ObservableProperty / ObservableCollection writes)
        ///
        /// This eliminates the original freeze where 8+ blocking DB operations ran
        /// synchronously on the UI thread inside the ViewModel constructor.
        /// </summary>
        private async Task LoadDataAsync()
        {
            Application.Current?.Dispatcher.Invoke(() => IsLoading = true);

            try
            {
                // ── All database / engine work runs on the thread pool ────────────────
                // Split across two Task.Run calls to stay within C#'s 7-element
                // value-tuple inference limit and avoid CS8130.

                // Batch 1: recommendation, goals, progress
                var (rec, baseline, goals, weeklyProgress, recentWeeks) =
                    await Task.Run(() =>
                    {
                        var recommendation = _engine.GenerateDailyRecommendation(1);
                        var baselineResult = _engine.GetOrCalculateBaseline(1);
                        var goalsList      = _database.GetSmartCoachGoals(1, true);
                        if (goalsList.Count == 0)
                            goalsList = _engine.GenerateGoals(1);

                        goalsList = RefreshGoalCurrentValues(goalsList, baselineResult, 1);
                        var thisWeekStart  = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                        var weekly         = _engine.CalculateWeeklyProgress(thisWeekStart, 1);
                        var recent         = _database.GetRecentWeeklyProgress(4, 1);
                        return (recommendation, baselineResult, goalsList, weekly, recent);
                    });

                // Batch 2: user stats, messages, status summary
                // Streak leses fra UserSettings (vedlikeholdt av ProgressionService.
                // UpdateStreak ved hver øktslutt) — IKKE fra UserProgress-tabellen,
                // som ingen aktiv kode oppdaterer (kun den døde GamificationService
                // skrev til den), slik at dashboardet alltid viste 0 i streak.
                var (userSettings, messages, statusSummary) =
                    await Task.Run(() =>
                    {
                        var settings = _database.GetUserSettings();
                        var msgs    = _database.GetUnreadMessages(1);
                        var summary = _engine.GetStatusSummary(1);
                        return (settings, msgs, summary);
                    });

                // ── Marshal all results to the UI thread for property writes ─────────
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    // Alias to the original variable names used below
                    var recommendation  = rec;

                    // Daily recommendation
                    TodayFocus              = GetFocusAreaDisplay(recommendation.FocusArea);
                    TodayRecommendation     = recommendation.RecommendationText;
                    FocusArea               = recommendation.FocusArea;
                    RecommendedDuration     = recommendation.RecommendedDurationMinutes;
                    HasHealthWarning        = recommendation.HealthWarning;
                    HealthWarningText       = recommendation.HealthWarningText ?? "";
                    IsRecommendationCompleted = recommendation.IsCompleted;

                    // Baseline + goals
                    if (baseline != null)
                    {
                        HasData = baseline.ConfidenceLevel != "low";
                        BaselineConfidence = baseline.ConfidenceLevel switch
                        {
                            "high"   => LocalizationService.Instance["SmartCoach_ConfidenceHigh"],
                            "medium" => LocalizationService.Instance["SmartCoach_ConfidenceMedium"],
                            _        => LocalizationService.Instance["SmartCoach_ConfidenceLow"]
                        };

                        ActiveGoals.Clear();
                        PitchProgress = 0;
                        ResonanceProgress = 0;
                        IntonationProgress = 0;

                        foreach (var goal in goals.Where(g => !g.IsAchieved).Take(3))
                        {
                            ActiveGoals.Add(goal);
                            var progress = goal.TargetValue > 0
                                ? (goal.CurrentValue / goal.TargetValue) * 100 : 0;
                            switch (goal.GoalType)
                            {
                                case "pitch":       PitchProgress      = Math.Min(100, progress); break;
                                case "resonance":   ResonanceProgress  = Math.Min(100, progress); break;
                                case "intonation":  IntonationProgress = Math.Min(100, progress); break;
                            }
                        }
                    }

                    // Weekly progress
                    SessionsThisWeek = weeklyProgress.SessionsCount;
                    MinutesThisWeek  = weeklyProgress.TotalMinutes;
                    HealthScore      = weeklyProgress.HealthScore;

                    // Anbefalt øvelse-surfacing + adaptivt volum (Agent VOL). Begge er rene
                    // beregninger over data vi alt har her — recovery-orientering tvinger
                    // inn-stramming (Health > Progression/Goals).
                    ApplyRecommendedExerciseSurfacing(recommendation);
                    ApplyVolumeSuggestion(recommendation, weeklyProgress.HealthScore);

                    WeeklyProgressList.Clear();
                    foreach (var week in recentWeeks.OrderBy(w => w.WeekStart))
                    {
                        WeeklyProgressList.Add(new WeeklyProgressItem
                        {
                            WeekLabel      = GetWeekLabel(week.WeekStart),
                            Sessions       = week.SessionsCount,
                            Minutes        = week.TotalMinutes,
                            AverageScore   = week.AverageScore,
                            PitchChange    = week.PitchChange,
                            ResonanceChange = week.ResonanceChange
                        });
                    }

                    // User stats — fra UserSettings (den reelt vedlikeholdte streaken)
                    CurrentStreak = userSettings.CurrentStreak;

                    // Messages
                    RecentMessages.Clear();
                    foreach (var msg in messages.Take(5))
                        RecentMessages.Add(msg);
                    UnreadMessageCount = messages.Count;

                    // Status
                    StatusSummary = statusSummary;
                });

                // Complexity data has its own internal DB calls — run separately
                // so its failure doesn't abort the rest of the load.
                await LoadComplexityDataAsync();
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusSummary = LocalizationService.Instance.GetFormattedString("SmartCoach_LoadFailedFormat", ex.Message);
                    HasData = false;
                    System.Diagnostics.Debug.WriteLine(
                        $"[FemVoice][SmartCoachViewModel] LoadDataAsync failed: {ex}");
                });
            }
            finally
            {
                Application.Current?.Dispatcher.Invoke(() => IsLoading = false);
            }
        }

        private List<SmartCoachGoal> RefreshGoalCurrentValues(List<SmartCoachGoal> goals, SmartCoachBaseline? baseline, int userId)
        {
            if (goals.Count == 0)
                return goals;

            var recentSessions = _database.GetRecentSessions(10, userId);
            var recentPitch = recentSessions.Where(s => s.AveragePitch > 0).Select(s => s.AveragePitch).DefaultIfEmpty(baseline?.BaselinePitch ?? 0).Average();
            var recentResonance = recentSessions.Where(s => s.ResonanceScore > 0).Select(s => s.ResonanceScore).DefaultIfEmpty(baseline?.BaselineResonanceScore ?? 0).Average();
            var recentIntonation = recentSessions.Where(s => s.IntonationScore > 0).Select(s => s.IntonationScore).DefaultIfEmpty(baseline?.BaselineIntonation ?? 0).Average();

            foreach (var goal in goals)
            {
                var currentValue = goal.GoalType switch
                {
                    "pitch" => recentPitch,
                    "resonance" => recentResonance,
                    "intonation" => recentIntonation,
                    _ => goal.CurrentValue
                };

                if (Math.Abs(goal.CurrentValue - currentValue) > 0.1)
                {
                    goal.CurrentValue = currentValue;
                    goal.IsAchieved = goal.TargetValue > 0 && currentValue >= goal.TargetValue;
                    goal.AchievedAt = goal.IsAchieved ? DateTime.Now : null;
                    _database.SaveSmartCoachGoal(goal);
                }
            }

            return goals.Where(g => !g.IsAchieved).ToList();
        }
        
        /// <summary>
        /// Fullfører dagens anbefaling
        /// </summary>
        [RelayCommand]
        private void CompleteRecommendation()
        {
            try
            {
                var recommendation = _database.GetDailyRecommendation(DateTime.Today, 1);
                if (recommendation != null)
                {
                    recommendation.IsCompleted = true;
                    recommendation.CompletedAt = DateTime.Now;
                    _database.SaveDailyRecommendation(recommendation);
                    IsRecommendationCompleted = true;
                }
            }
            catch
            {
                // Ignorer feil
            }
        }
        
        /// <summary>
        /// Markerer en melding som lest
        /// </summary>
        [RelayCommand]
        private void MarkMessageAsRead(int messageId)
        {
            try
            {
                // MarkMessageAsRead and GetUnreadMessageCount ARE on IDatabaseService
                _database.MarkMessageAsRead(messageId);
                UnreadMessageCount = _database.GetUnreadMessageCount(1);
                
                // Fjern fra listen
                var message = RecentMessages.FirstOrDefault(m => m.Id == messageId);
                if (message != null)
                {
                    RecentMessages.Remove(message);
                }
            }
            catch
            {
                // Ignorer feil
            }
        }
        
        /// <summary>
        /// Refresher alle data (async — safe to call from UI/command binding).
        /// </summary>
        [RelayCommand]
        private async Task Refresh()
        {
            await LoadDataAsync();
        }
        
        /// <summary>
        /// Skriver de UI-bundne egenskapene for anbefalt-øvelse-hintet. Må kalles på
        /// UI-tråden (gjør kun property-skriving). Null-safe via den rene helperen.
        /// </summary>
        private void ApplyRecommendedExerciseSurfacing(SmartCoachDailyRecommendation? recommendation)
        {
            var (hasExercise, exerciseId, hint, isRecoveryOriented) =
                BuildRecommendedExerciseHint(recommendation);

            HasRecommendedExercise          = hasExercise;
            RecommendedExerciseId           = exerciseId;
            RecommendedExerciseHint         = hint;
            IsRecommendationRecoveryOriented = isRecoveryOriented;
        }

        /// <summary>
        /// Skriver de UI-bundne egenskapene for volum-forslaget. Må kalles på UI-tråden.
        /// Konsistens-scoren utledes fra komplexitets-/intonasjons-progresjon vi alt har;
        /// vi bruker HealthScore som den dominerende health-aksen i dette laget.
        /// </summary>
        private void ApplyVolumeSuggestion(SmartCoachDailyRecommendation? recommendation, double healthScore)
        {
            // Komfort/konsistens er ikke direkte tilgjengelig som rene 0–100-scorer i
            // dette VM-laget; vi bruker IntonationProgress (allerede satt over) som en
            // forsiktig konsistens-proxy og lar komfort følge helse-scoren. Begge er
            // klampet i RecommendVolume. Health > Progression håndteres i helperen.
            var consistencyProxy = Math.Clamp(IntonationProgress, 0.0, 100.0);

            var volume = BuildVolumeSuggestion(
                recommendation: recommendation,
                healthScore: healthScore,
                comfortScore: healthScore,
                consistencyScore: consistencyProxy);

            SuggestedExerciseCount     = volume.ExerciseCount;
            SuggestedSetCount          = volume.SetCount;
            IsVolumeReducedForRecovery = volume.IsReducedForRecovery;
            VolumeReason               = volume.Reason;
            SuggestedVolumeText        = LocalizationService.Instance.GetFormattedString(
                "SmartCoach_SuggestedVolumeFormat", volume.ExerciseCount, volume.SetCount);
        }

        /// <summary>
        /// Ren surfacing av den daglige anbefalingens øvelses-ID som et HINT. Returnerer
        /// (harExercise, exerciseId, hintTekst, erRecoveryOrientert). Et helse-/recovery-
        /// drevet forslag (HealthWarning eller FocusArea == "recovery") markeres som
        /// recovery-orientert — Health &gt; Goals. Null-anbefaling ⇒ ingen hint, ingen krasj.
        /// </summary>
        internal static (bool HasExercise, int ExerciseId, string Hint, bool IsRecoveryOriented)
            BuildRecommendedExerciseHint(SmartCoachDailyRecommendation? recommendation)
        {
            if (recommendation?.RecommendedExerciseId is not int id || id <= 0)
                return (false, 0, "", IsRecoveryFocused(recommendation));

            var isRecovery = IsRecoveryFocused(recommendation);
            var hint = LocalizationService.Instance.GetFormattedString(
                "SmartCoach_RecommendedExerciseFormat", id);
            return (true, id, hint, isRecovery);
        }

        private static bool IsRecoveryFocused(SmartCoachDailyRecommendation? recommendation)
        {
            if (recommendation == null)
                return false;
            return recommendation.HealthWarning
                || string.Equals(recommendation.FocusArea, "recovery", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ren beregning av øktvolum-forslaget fra den daglige anbefalingen + helse-/
        /// progresjons-aksene. Recovery-orientering tvinger inn-stramming uavhengig av
        /// tallene (Health &gt; Progression): et helse-/recovery-drevet råd mapper alltid
        /// til Strained-status før <see cref="AdaptiveDifficultyService.RecommendVolume"/>
        /// kjøres, så konsistens aldri kan løfte volumet på en hvile-dag.
        /// </summary>
        internal static VolumeRecommendation BuildVolumeSuggestion(
            SmartCoachDailyRecommendation? recommendation,
            double healthScore,
            double comfortScore = 70,
            double consistencyScore = 70,
            AdaptiveDifficultyService? service = null)
        {
            service ??= new AdaptiveDifficultyService();

            // Helse-/recovery-drevet anbefaling ⇒ behandle recovery som Strained slik at
            // RecommendVolume-gaten alltid strammer inn (Health > Progression). Ellers
            // utleder vi en recovery-proxy fra helse-scoren (vi har ingen egen recovery-
            // score i dette VM-laget, men helse er den dominerende health-aksen her).
            var recoveryProxyScore = IsRecoveryFocused(recommendation)
                ? AdaptiveDifficultyService.LowRecoveryHealthThreshold / 2.0
                : healthScore;

            var recovery = new RecoveryResult
            {
                Score = recoveryProxyScore,
                Status = RecoveryScorer.ClassifyStatus(recoveryProxyScore),
                Explanation = ""
            };

            return service.RecommendVolume(
                recovery: recovery,
                comfortScore: comfortScore,
                consistencyScore: consistencyScore,
                healthScore: healthScore);
        }

        private static string GetFocusAreaDisplay(string focusArea)
        {
            return focusArea switch
            {
                "resonance" => LocalizationService.Instance["Dashboard_Resonance"],
                "pitch" => LocalizationService.Instance["Dashboard_Pitch"],
                "intonation" => LocalizationService.Instance["Dashboard_Intonation"],
                "breathing" => LocalizationService.Instance["Goal_Breathing"],
                "recovery" => LocalizationService.Instance["SmartCoach_Recovery"],
                "balanced" => LocalizationService.Instance["SmartCoach_Balanced"],
                _ => LocalizationService.Instance["SmartCoach_GeneralTraining"]
            };
        }
        
        private static string GetWeekLabel(DateTime weekStart)
        {
            var weekNum = GetWeekNumber(weekStart);
            return LocalizationService.Instance.GetFormattedString("SmartCoach_WeekFormat", weekNum);
        }
        
        private static int GetWeekNumber(DateTime date)
        {
            var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            return cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        }
        
        /// <summary>
        /// Loads complexity progression data asynchronously.
        /// ComplexityEngine.EvaluateCurrentLevel and GetProgressionSteps both hit the DB,
        /// so they must run on the thread pool.
        /// </summary>
        private async Task LoadComplexityDataAsync()
        {
            try
            {
                // All ComplexityEngine DB calls on the thread pool
                var result = await Task.Run(() =>
                {
                    // ComplexityEngine requires the concrete DatabaseService type.
                    // Fall back to a no-op path if the concrete reference is unavailable.
                    var complexityEngine = new ComplexityEngine(_databaseConcrete!);
                    var evaluation = complexityEngine.EvaluateCurrentLevel(1);
                    var feedback   = complexityEngine.GenerateComplexityFeedback(evaluation);
                    var steps      = complexityEngine.GetProgressionSteps(1);
                    return (evaluation, feedback, steps);
                });

                // Marshal back to UI thread for property writes
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CurrentComplexityLevel         = result.evaluation.CurrentLevel;
                    CurrentComplexityLevelDisplay  = ComplexityLevelStep.GetDisplayName(result.evaluation.CurrentLevel);
                    SessionsAtCurrentComplexity    = result.evaluation.SessionsAtCurrentLevel;
                    IsComplexityReadyForNext       = result.evaluation.IsReadyForNext;
                    ComplexityFeedback             = result.feedback;
                    ComplexityProgressionSteps     = result.steps;
                });
            }
            catch
            {
                // Fallback values on UI thread
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CurrentComplexityLevel        = SpeechComplexityLevel.IsolatedSounds;
                    CurrentComplexityLevelDisplay = LocalizationService.Instance["Complexity_IsolatedSounds"];
                    SessionsAtCurrentComplexity   = 0;
                    IsComplexityReadyForNext      = false;
                });
            }
        }
    }
    
    /// <summary>
    /// Helper class for weekly progress display
    /// </summary>
    public class WeeklyProgressItem
    {
        public string WeekLabel { get; set; } = "";
        public int Sessions { get; set; }
        public int Minutes { get; set; }
        public double AverageScore { get; set; }
        public double PitchChange { get; set; }
        public double ResonanceChange { get; set; }
    }
}
