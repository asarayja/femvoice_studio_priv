using System;
using System.Threading.Tasks;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Aggregated clinical outcome of a single exercise session, built from the
    /// stream of <see cref="ExerciseLiveState"/> snapshots. All metric values are 0–1.
    /// </summary>
    public sealed record ExerciseSessionOutcome
    {
        public double AverageResonance { get; init; }
        public double AverageStability { get; init; }

        /// <summary>Share of evaluated ticks spent inside the comfort zone (0–1).</summary>
        public double ComfortCompliance { get; init; }

        /// <summary>Highest HoldProgress reached during the session (0–1).</summary>
        public double HoldCompletion { get; init; }

        /// <summary>Number of distinct safety-lock engagements (false→true transitions).</summary>
        public int SafetyLockEpisodes { get; init; }

        /// <summary>Number of distinct comfort-zone exits (in→out transitions).</summary>
        public int ComfortBreachEpisodes { get; init; }

        /// <summary>Ticks where the health supervisor flagged fatigue.</summary>
        public int FatigueIndicators { get; init; }

        /// <summary>Ticks where the health supervisor flagged strain.</summary>
        public int StrainDetections { get; init; }

        /// <summary>Inline coach messages shown during the session.</summary>
        public int CoachingHintsTriggered { get; init; }

        /// <summary>Number of live-state snapshots that entered the aggregate.</summary>
        public int EvaluatedTicks { get; init; }
    }

    /// <summary>
    /// Records per-session clinical aggregates from the coordinator's live-state stream.
    ///
    /// Responsibilities:
    ///   • Subscribe to <see cref="ExerciseIntelligenceCoordinator.ExerciseUpdated"/> and
    ///     accumulate resonance/stability/comfort/hold/safety statistics while recording.
    ///   • Feed every snapshot to <see cref="VocalHealthSupervisor.Evaluate"/> so strain
    ///     and fatigue trends are detected in real time (activates the dormant health layer).
    ///   • Expose <see cref="CurrentHealthScore"/> (0–100) derived from the supervisor
    ///     state — replaces the previously hardcoded health=100 in the audio path.
    ///   • Persist the outcome via <see cref="SessionAnalyticsStore"/> on completion so
    ///     MasteryEvaluator and ProgressionSafetyGate have a durable history to gate on.
    ///
    /// Health mapping (clinical design decision, see analysis doc §7): only the
    /// supervisor's Lock state maps below the coordinator's &lt;70 safety threshold.
    /// Caution/Restrict warn without locking to avoid a self-reinforcing lock spiral.
    /// A lock is therefore "sticky" for the rest of the session (lock = stop and rest);
    /// <see cref="BeginSession"/> resets the supervisor for the next session.
    /// </summary>
    public sealed class ExerciseSessionRecorder : IDisposable
    {
        private readonly ExerciseIntelligenceCoordinator _coordinator;
        private readonly VocalHealthSupervisor _healthSupervisor;
        private readonly SessionAnalyticsStore _analyticsStore;
        private readonly HydrationAdvisor? _hydrationAdvisor;
        private readonly FeedbackPipeline? _feedbackPipeline;
        private readonly VocalHealthFeedbackMapper? _vocalHealthMapper;
        private readonly HydrationFeedbackMapper? _hydrationMapper;
        private readonly object _lock = new();

        private bool _recording;
        private bool _disposed;
        private int _exerciseId;
        private int _sessionId;
        private int _userId = 1;
        private DateTime _startedAt;

        private double _resonanceSum;
        private double _stabilitySum;
        private double _maxHoldProgress;
        private int _ticks;
        private int _ticksInComfortZone;
        private int _safetyLockEpisodes;
        private int _comfortBreachEpisodes;
        private int _fatigueIndicators;
        private int _strainDetections;
        private int _pauseRecommendations;
        private int _hydrationSuggestions;
        private int _coachHints;
        private bool _wasSafetyLocked;
        private bool _wasInComfortZone = true;
        private double _currentHealthScore = 100;

        public ExerciseSessionRecorder(
            ExerciseIntelligenceCoordinator coordinator,
            VocalHealthSupervisor healthSupervisor,
            SessionAnalyticsStore analyticsStore,
            HydrationAdvisor? hydrationAdvisor = null,
            FeedbackPipeline? feedbackPipeline = null,
            VocalHealthFeedbackMapper? vocalHealthMapper = null,
            HydrationFeedbackMapper? hydrationMapper = null)
        {
            _coordinator      = coordinator      ?? throw new ArgumentNullException(nameof(coordinator));
            _healthSupervisor = healthSupervisor ?? throw new ArgumentNullException(nameof(healthSupervisor));
            _analyticsStore   = analyticsStore   ?? throw new ArgumentNullException(nameof(analyticsStore));
            _hydrationAdvisor = hydrationAdvisor;
            _feedbackPipeline = feedbackPipeline;
            _vocalHealthMapper = vocalHealthMapper;
            _hydrationMapper  = hydrationMapper;

            _coordinator.ExerciseUpdated    += OnExerciseUpdated;
            _coordinator.InlineCoachUpdated += OnInlineCoachUpdated;
        }

        /// <summary>
        /// Health score (0–100) for the live metric path. 100 when idle or healthy;
        /// drops below the coordinator's 70-threshold only when the supervisor locks.
        /// </summary>
        public double CurrentHealthScore
        {
            get { lock (_lock) return _currentHealthScore; }
        }

        /// <summary>Whether a session is currently being recorded.</summary>
        public bool IsRecording
        {
            get { lock (_lock) return _recording; }
        }

        /// <summary>
        /// Starts aggregation for a new session. Resets the health supervisor so each
        /// session evaluates against a fresh trend window (locks do not carry over
        /// in-memory — they persist as analytics events instead).
        /// </summary>
        public void BeginSession(int exerciseId, int sessionId, int userId = 1)
        {
            lock (_lock)
            {
                _exerciseId = exerciseId;
                _sessionId  = sessionId;
                _userId     = userId;
                _startedAt  = DateTime.Now;

                _resonanceSum = _stabilitySum = _maxHoldProgress = 0;
                _ticks = _ticksInComfortZone = 0;
                _safetyLockEpisodes = _comfortBreachEpisodes = 0;
                _fatigueIndicators = _strainDetections = _coachHints = 0;
                _pauseRecommendations = _hydrationSuggestions = 0;
                _wasSafetyLocked = false;
                _wasInComfortZone = true;
                _currentHealthScore = 100;

                _healthSupervisor.Reset();
                _recording = true;
            }

            // Sesjonsnivå-journalføring: SessionAnalyticsSessions-tabellen ble aldri
            // skrevet før (integrasjonsauditen) — daglig/ukentlig trend var alltid tom.
            _ = PersistSessionStartedAsync(sessionId, userId, DateTime.Now);
        }

        /// <summary>
        /// Stops aggregation, returns the session outcome and persists it to the
        /// analytics store (fire-and-forget — persistence failure never blocks the UI).
        /// </summary>
        public ExerciseSessionOutcome CompleteSession()
        {
            ExerciseSessionOutcome outcome;
            int sessionId, exerciseId, userId, strain, locks, breaches, pauses, hydration;
            double healthScore;
            DateTime startedAt;

            lock (_lock)
            {
                _recording = false;
                outcome = new ExerciseSessionOutcome
                {
                    AverageResonance       = _ticks > 0 ? _resonanceSum / _ticks : 0,
                    AverageStability       = _ticks > 0 ? _stabilitySum / _ticks : 0,
                    ComfortCompliance      = _ticks > 0 ? (double)_ticksInComfortZone / _ticks : 0,
                    HoldCompletion         = _maxHoldProgress,
                    SafetyLockEpisodes     = _safetyLockEpisodes,
                    ComfortBreachEpisodes  = _comfortBreachEpisodes,
                    FatigueIndicators      = _fatigueIndicators,
                    StrainDetections       = _strainDetections,
                    CoachingHintsTriggered = _coachHints,
                    EvaluatedTicks         = _ticks
                };

                sessionId  = _sessionId;
                exerciseId = _exerciseId;
                userId     = _userId;
                strain     = _strainDetections;
                locks      = _safetyLockEpisodes;
                breaches   = _comfortBreachEpisodes;
                pauses     = _pauseRecommendations;
                hydration  = _hydrationSuggestions;
                healthScore = _currentHealthScore;
                startedAt  = _startedAt;
            }

            _ = PersistAsync(outcome, sessionId, exerciseId, userId, strain, locks, breaches,
                pauses, hydration, healthScore, startedAt);
            return outcome;
        }

        /// <summary>
        /// Persists the user's subjective self-report. Reports indicating a health
        /// concern are journaled as PauseRecommended events so MasteryEvaluator and
        /// ProgressionSafetyGate can gate on them. Previously the report was
        /// write-only (collected in ExerciseWindow, never read — integration audit).
        /// </summary>
        public void SubmitSubjectiveReport(SubjectiveReport report)
        {
            if (report == null) return;

            int sessionId, userId;
            lock (_lock)
            {
                sessionId = report.SessionId ?? _sessionId;
                userId    = report.UserId > 0 ? report.UserId : _userId;
            }

            if (!report.IndicatesHealthConcern)
                return;

            _ = PersistSubjectiveConcernAsync(report, sessionId, userId);
        }

        /// <summary>Discards the current session without persisting anything.</summary>
        public void AbortSession()
        {
            lock (_lock) _recording = false;
        }

        // ────────────────────────────────────────────────────────────────────────
        // Event handlers
        // ────────────────────────────────────────────────────────────────────────

        private void OnExerciseUpdated(ExerciseLiveState state)
        {
            lock (_lock)
            {
                if (!_recording) return;

                // Skip synthetic idle snapshots (SetExerciseContext default / StopExercise
                // final state) and pure-silence frames: an all-zero state carries no
                // clinical information and would otherwise count as a comfort breach.
                if (IsIdleState(state)) return;

                _ticks++;
                _resonanceSum += Clamp01(state.PrimaryMetricScore);
                _stabilitySum += Clamp01(state.StabilityScore);
                _maxHoldProgress = Math.Max(_maxHoldProgress, Clamp01(state.HoldProgress));

                if (state.IsInComfortZone) _ticksInComfortZone++;
                if (state.IsSafetyLocked && !_wasSafetyLocked) _safetyLockEpisodes++;
                if (!state.IsInComfortZone && _wasInComfortZone) _comfortBreachEpisodes++;
                _wasSafetyLocked  = state.IsSafetyLocked;
                _wasInComfortZone = state.IsInComfortZone;
            }

            // Supervisor has its own internal lock; never call it while holding ours.
            var decision = _healthSupervisor.Evaluate(state);
            var hydrationAdvice = _hydrationAdvisor?.Evaluate(state);

            lock (_lock)
            {
                if (!_recording) return;
                if (decision.FatigueDetected) _fatigueIndicators++;
                if (decision.StrainDetected)  _strainDetections++;
                if (decision.PauseRecommended) _pauseRecommendations++;
                if (decision.HydrationSuggested || hydrationAdvice?.Suggested == true) _hydrationSuggestions++;
                _currentHealthScore = decision.State switch
                {
                    HealthSafetyState.Lock     => 40,   // under 70 → koordinatoren låser
                    HealthSafetyState.Restrict => 72,   // advarsel — bevisst over lås-terskelen
                    HealthSafetyState.Caution  => 85,
                    _                          => 100
                };
            }

            // Brukerrettet helse-/hydrerings-coaching: rut beslutningene gjennom
            // mapperne og FeedbackPipeline (guarden rate-limiter/prioriterer).
            // Var dormant før — helse påvirket kun lås/score i det stille (audit-funn).
            SubmitHealthFeedback(decision, hydrationAdvice, state);
        }

        private void SubmitHealthFeedback(
            VocalHealthDecision decision,
            HydrationAdvice? hydrationAdvice,
            ExerciseLiveState state)
        {
            if (_feedbackPipeline == null) return;

            try
            {
                var healthCandidate = _vocalHealthMapper?.Map(decision);
                if (healthCandidate != null)
                {
                    _feedbackPipeline.Submit(
                        healthCandidate,
                        _vocalHealthMapper!.BuildContext(decision));
                }

                if (hydrationAdvice != null)
                {
                    var hydrationCandidate = _hydrationMapper?.Map(hydrationAdvice);
                    if (hydrationCandidate != null)
                    {
                        _feedbackPipeline.Submit(
                            hydrationCandidate,
                            _hydrationMapper!.BuildContext(hydrationAdvice, state));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExerciseSessionRecorder] Feedback submit failed: {ex.Message}");
            }
        }

        private void OnInlineCoachUpdated(InlineCoachMessage message)
        {
            lock (_lock)
            {
                if (_recording) _coachHints++;
            }
        }

        private static bool IsIdleState(ExerciseLiveState state)
            => state.PrimaryMetricScore <= 0
               && state.SecondaryMetricScore <= 0
               && state.StabilityScore <= 0
               && state.HoldProgress <= 0
               && !state.IsHoldingCorrectly
               && !state.IsSafetyLocked;

        private static double Clamp01(double value)
            => double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Clamp(value, 0, 1);

        // ────────────────────────────────────────────────────────────────────────
        // Persistence
        // ────────────────────────────────────────────────────────────────────────

        private async Task PersistSessionStartedAsync(int sessionId, int userId, DateTime startedAt)
        {
            try
            {
                await _analyticsStore.RecordSessionStartedAsync(new SessionAnalyticsRecord
                {
                    SessionId = sessionId,
                    UserId    = userId,
                    StartedAt = startedAt
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExerciseSessionRecorder] Session-start persist failed: {ex.Message}");
            }
        }

        private async Task PersistSubjectiveConcernAsync(SubjectiveReport report, int sessionId, int userId)
        {
            try
            {
                // Severitet avledes av brukerens egen gradering: opplevd strain veier
                // tyngst, deretter fatigue (1-5) og lav komfort (1-5, invertert).
                var severity = report.ExperiencedStrain ? 1.0
                    : Math.Clamp(Math.Max(report.FatigueFeeling, 6 - report.ComfortLevel) / 5.0, 0.2, 1.0);

                await _analyticsStore.RecordHealthEventAsync(new HealthAnalyticsEvent
                {
                    SessionId  = sessionId,
                    UserId     = userId,
                    EventType  = HealthAnalyticsEventType.PauseRecommended,
                    OccurredAt = DateTime.Now,
                    Severity   = severity,
                    ReasonCode = "SUBJECTIVE_HEALTH_CONCERN"
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExerciseSessionRecorder] Subjective persist failed: {ex.Message}");
            }
        }

        private async Task PersistAsync(
            ExerciseSessionOutcome outcome,
            int sessionId,
            int exerciseId,
            int userId,
            int strainDetections,
            int lockEpisodes,
            int breachEpisodes,
            int pauseRecommendations,
            int hydrationSuggestions,
            double healthScore,
            DateTime startedAt)
        {
            try
            {
                // Sesjonsnivå-raden (driver GetDailySummaryAsync/GetWeeklyTrendAsync —
                // aggregatene var tomme før fordi denne aldri ble skrevet).
                await _analyticsStore.RecordSessionCompletedAsync(new SessionAnalyticsRecord
                {
                    SessionId                 = sessionId,
                    UserId                    = userId,
                    StartedAt                 = startedAt,
                    EndedAt                   = DateTime.Now,
                    ExerciseCount             = 1,
                    AverageResonance          = outcome.AverageResonance,
                    AverageStability          = outcome.AverageStability,
                    AveragePitchComfort       = outcome.ComfortCompliance,
                    AverageHealthScore        = Math.Clamp(healthScore / 100.0, 0, 1),
                    SafetyEventsCount         = lockEpisodes,
                    PauseRecommendationsCount = pauseRecommendations,
                    HydrationSuggestionsCount = hydrationSuggestions,
                    FatigueIndicatorCount     = outcome.FatigueIndicators
                }).ConfigureAwait(false);

                await _analyticsStore.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
                {
                    SessionId             = sessionId,
                    UserId                = userId,
                    ExerciseId            = exerciseId,
                    StartedAt             = startedAt,
                    EndedAt               = DateTime.Now,
                    HoldCompletionRate    = outcome.HoldCompletion,
                    ResonanceQualityIndex = outcome.AverageResonance,
                    StabilityConsistency  = outcome.AverageStability,
                    SafetyEventsCount     = lockEpisodes,
                    FatigueIndicators     = outcome.FatigueIndicators,
                    CoachingHintsTriggered = outcome.CoachingHintsTriggered
                }).ConfigureAwait(false);

                for (var i = 0; i < lockEpisodes; i++)
                {
                    await _analyticsStore.RecordHealthEventAsync(new HealthAnalyticsEvent
                    {
                        SessionId  = sessionId,
                        UserId     = userId,
                        EventType  = HealthAnalyticsEventType.SafetyFreeze,
                        OccurredAt = DateTime.Now,
                        Severity   = 1.0,
                        ReasonCode = "EXERCISE_SAFETY_LOCK"
                    }).ConfigureAwait(false);
                }

                if (strainDetections > 0)
                {
                    await _analyticsStore.RecordHealthEventAsync(new HealthAnalyticsEvent
                    {
                        SessionId  = sessionId,
                        UserId     = userId,
                        EventType  = HealthAnalyticsEventType.StrainPeriod,
                        OccurredAt = DateTime.Now,
                        Severity   = Math.Clamp(strainDetections / 10.0, 0, 1),
                        ReasonCode = "STRAIN_DETECTED"
                    }).ConfigureAwait(false);
                }

                // En økt med gjentatte komfortbrudd journalføres som én hendelse —
                // ProgressionSafetyGate og MasteryEvaluator teller brudd-økter, ikke enkeltbrudd.
                if (breachEpisodes >= 3)
                {
                    await _analyticsStore.RecordHealthEventAsync(new HealthAnalyticsEvent
                    {
                        SessionId  = sessionId,
                        UserId     = userId,
                        EventType  = HealthAnalyticsEventType.ComfortZoneBreach,
                        OccurredAt = DateTime.Now,
                        Severity   = Math.Clamp(breachEpisodes / 10.0, 0, 1),
                        ReasonCode = "REPEATED_COMFORT_BREACH"
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExerciseSessionRecorder] Persist failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _coordinator.ExerciseUpdated    -= OnExerciseUpdated;
            _coordinator.InlineCoachUpdated -= OnInlineCoachUpdated;
        }
    }
}
