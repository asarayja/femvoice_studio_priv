using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// In-session stability STEADINESS (0–1): <c>1 − clamp(stdDev/scale, 0, 1)</c> over
        /// the per-tick stability signal, where stdDev is the population standard deviation.
        /// Measures REPRODUCIBILITY (how steady the signal was) rather than how high/voiced
        /// it was — a flat 0.7 trace scores high, a 0.4/1.0-alternating mean-0.7 trace scores
        /// low (CONS-1/2). Neutral 0.5 fallback when fewer than two voiced ticks were seen.
        /// Feeds <c>ScoreConsistency</c>; <see cref="AverageStability"/> stays the live-UI /
        /// MasteryEvaluator signal.
        /// </summary>
        public double StabilitySteadiness01 { get; init; } = 0.5;

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

        /// <summary>
        /// Session-representative health score (0–100): the per-tick average,
        /// capped at 40 when a safety lock occurred. Used instead of the last-tick
        /// snapshot, which let a transient end-of-session strain misrepresent an
        /// otherwise healthy session (review finding).
        /// </summary>
        public double SessionHealthScore { get; init; } = 100;

        // ── Raw acoustic aggregates for Voice Intelligence (Bølge 2 SIGNAL-wiring) ────
        // Session averages of the raw per-tick signals the coordinator now forwards on
        // ExerciseLiveState. Each is averaged over ONLY the ticks where the signal was
        // actually measured (a positive value); when no tick carried it the aggregate is
        // the "missing" sentinel (<= 0 / NaN) and the scorer falls back to neutral 50.

        /// <summary>Session-average F1 in Hz over ticks that measured it. 0 ⇒ never measured.</summary>
        public double AverageF1Hz { get; init; }

        /// <summary>Session-average spectral centroid in Hz over ticks that measured it. 0 ⇒ never measured.</summary>
        public double AverageSpectralCentroidHz { get; init; }

        /// <summary>Session-average RMS intensity (0–1) over ticks that measured it. 0 ⇒ never measured.</summary>
        public double AverageIntensity { get; init; }

        /// <summary>Session-average measured pitch (F0) in Hz over voiced ticks. 0 ⇒ never voiced.</summary>
        public double AveragePitchHz { get; init; }

        /// <summary>Pitch variation (Hz): population std-dev of measured pitch over voiced ticks. 0 ⇒ &lt; 2 voiced ticks.</summary>
        public double PitchVariationHz { get; init; }

        /// <summary>Intonation range (Hz): max − min measured pitch over voiced ticks. 0 ⇒ never voiced.</summary>
        public double IntonationRangeHz { get; init; }

        /// <summary>Number of ticks that carried a measured (positive) pitch. Drives the pitch aggregates.</summary>
        public int VoicedTicks { get; init; }

        /// <summary>
        /// <c>true</c> when at least one evaluated tick carried a REAL resonance signal
        /// (the profile's <c>UsesResonance</c> was set). <c>false</c> for a pitch-only
        /// session, where <see cref="AverageResonance"/> is 0 because no resonance was
        /// measured — distinct from a genuine resonance session that averaged 0. The VI
        /// scorer uses this to keep the Resonance dimension's neutral-50 sentinel instead
        /// of scoring the normalised-pitch proxy as resonance (RES-01).
        /// </summary>
        public bool UsedResonanceSignal { get; init; }
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

        // Pure, stateless scorer (no DB/IO) — derives the seven explainable 0–100
        // dimension scores + composite at session end. Reused across sessions.
        private readonly VoiceIntelligenceScorer _voiceIntelligenceScorer = new();

        // Predictive cross-session recovery (Bølge 1, Agent 3). Builds a FULL
        // RecoveryScoreInput from the persisted analytics history so the recovery axis
        // and the SessionInsight recovery-need reflect real cross-session restitution —
        // not just this session's in-memory counts (the dormant scorer branches now fire).
        private readonly RecoveryIntelligenceService _recoveryIntelligence;

        // Pure end-of-session insight assembler (Bølge 1, Agent 5). Combines the freshly
        // computed Voice-Intelligence scores, the prior trend and the recovery snapshot
        // into one explainable, clinically safe SessionInsight.
        private readonly SessionInsightBuilder _sessionInsightBuilder;

        // Pure recovery scorer — converts the cross-session RecoveryScoreInput into the
        // RecoveryResult the SessionInsight carries (same scorer the VI recovery axis uses).
        private readonly RecoveryScorer _recoveryScorer = new();

        private readonly object _lock = new();

        private bool _recording;
        private bool _disposed;
        private int _exerciseId;
        private int _sessionId;
        private int _userId = 1;
        private DateTime _startedAt;

        private double _resonanceSum;
        // Ticks that carried a REAL resonance signal (state.UsesResonanceSignal). For a
        // pitch-only profile this stays 0 all session, so the resonance dimension keeps the
        // neutral-50 sentinel instead of echoing the normalised-pitch proxy (RES-01).
        private int    _resonanceTicks;
        private double _stabilitySum;
        // Sum-of-squares of the per-tick stability signal — drives the in-session
        // stability std-dev so Consistency measures reproducibility, not voicing
        // clarity (CONS-1/2). Reset per session alongside _stabilitySum.
        private double _stabilitySqSum;
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
        private double _healthScoreSum;

        // ── Raw acoustic signal accumulators (Voice Intelligence SIGNAL-wiring) ───────
        // Each spectral signal is summed over ONLY the ticks that measured it, with its
        // own counter, so an unmeasured tick never drags the average toward zero.
        private double _f1Sum;          private int _f1Ticks;
        private double _centroidSum;    private int _centroidTicks;
        private double _intensitySum;   private int _intensityTicks;
        // Pitch: sum + sum-of-squares (population variance) + running min/max over voiced ticks.
        private double _pitchSum;
        private double _pitchSqSum;
        private int    _pitchTicks;
        private double _pitchMin;
        private double _pitchMax;

        public ExerciseSessionRecorder(
            ExerciseIntelligenceCoordinator coordinator,
            VocalHealthSupervisor healthSupervisor,
            SessionAnalyticsStore analyticsStore,
            HydrationAdvisor? hydrationAdvisor = null,
            FeedbackPipeline? feedbackPipeline = null,
            VocalHealthFeedbackMapper? vocalHealthMapper = null,
            HydrationFeedbackMapper? hydrationMapper = null,
            RecoveryIntelligenceService? recoveryIntelligence = null,
            SessionInsightBuilder? sessionInsightBuilder = null)
        {
            _coordinator      = coordinator      ?? throw new ArgumentNullException(nameof(coordinator));
            _healthSupervisor = healthSupervisor ?? throw new ArgumentNullException(nameof(healthSupervisor));
            _analyticsStore   = analyticsStore   ?? throw new ArgumentNullException(nameof(analyticsStore));
            _hydrationAdvisor = hydrationAdvisor;
            _feedbackPipeline = feedbackPipeline;
            _vocalHealthMapper = vocalHealthMapper;
            _hydrationMapper  = hydrationMapper;

            // Default to the pure Bølge-1 services so existing 3-/7-arg call sites keep
            // working unchanged; tests can inject their own (no mocking — real classes).
            _recoveryIntelligence  = recoveryIntelligence  ?? new RecoveryIntelligenceService();
            _sessionInsightBuilder = sessionInsightBuilder ?? new SessionInsightBuilder();

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
        /// The persistence task from the most recent CompleteSession. Await this
        /// before reading analytics (f.eks. ProgressionOrchestrator) for å garantere
        /// at den nettopp fullførte økten er journalført (review-funn: lese-etter-
        /// skriv-race mot fire-and-forget-persisteringen). Feil svelges internt.
        /// </summary>
        public Task? LastPersistTask { get; private set; }

        /// <summary>
        /// The end-of-session <see cref="SessionInsight"/> assembled by the most recent
        /// <see cref="CompleteSession"/> (Bølge 1, Agent 5 wiring). Built on the same async
        /// path as persistence, so it is only guaranteed populated once
        /// <see cref="LastPersistTask"/> has completed. <c>null</c> when the session was
        /// empty/invalid (no evaluated ticks) or when insight building failed — a consumer
        /// should null-check and simply hide its insight surface in that case.
        /// </summary>
        public SessionInsight? LastSessionInsight { get; private set; }

        /// <summary>
        /// Raised on the thread that finishes the persistence/insight task once a fresh
        /// <see cref="SessionInsight"/> is available at session end. Never raised when the
        /// session produced no insight (empty session / build failure). Subscribers must
        /// marshal to the UI thread themselves (the recorder is UI-agnostic).
        /// </summary>
        public event Action<SessionInsight>? SessionInsightReady;

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

                _resonanceSum = _stabilitySum = _stabilitySqSum = _maxHoldProgress = 0;
                _resonanceTicks = 0;
                _ticks = _ticksInComfortZone = 0;
                _safetyLockEpisodes = _comfortBreachEpisodes = 0;
                _fatigueIndicators = _strainDetections = _coachHints = 0;
                _pauseRecommendations = _hydrationSuggestions = 0;
                _wasSafetyLocked = false;
                _wasInComfortZone = true;
                _currentHealthScore = 100;
                _healthScoreSum = 0;

                // Reset the raw acoustic accumulators so signal never leaks across sessions.
                _f1Sum = _centroidSum = _intensitySum = 0;
                _f1Ticks = _centroidTicks = _intensityTicks = 0;
                _pitchSum = _pitchSqSum = 0;
                _pitchTicks = 0;
                _pitchMin = 0;
                _pitchMax = 0;

                _healthSupervisor.Reset();
                // Hydreringsadvisoren er en DI-singleton; uten denne lekket _lastSuggestionAt
                // og _accumulatedLoad mellom økter (2-min cooldown ble effektivt per-app-
                // levetid). Reset gjør cooldown + akkumulert last per-økt.
                _hydrationAdvisor?.Reset();
                _recording = true;
            }

            // Clear any prior session's insight so a UI that polls the property between
            // sessions never shows a stale card while the new session is in progress.
            LastSessionInsight = null;

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
            DateTime startedAt;

            lock (_lock)
            {
                _recording = false;

                // Øktrepresentativ helse: per-tick-snitt, hardt cappet til 40 ved
                // safety-lock-episode. Siste-tick-verdien alene lot en transient
                // strain på slutten feilrepresentere en ellers frisk økt.
                var averageHealth = _ticks > 0 ? _healthScoreSum / _ticks : 100;
                var sessionHealth = _safetyLockEpisodes > 0
                    ? Math.Min(averageHealth, 40)
                    : averageHealth;

                // Raw acoustic aggregates — averaged over only the ticks that measured the
                // signal. Pitch variation is the population std-dev over voiced ticks
                // (Var = E[X²] − E[X]², floored at 0 for FP safety); intonation range is
                // max − min over voiced ticks. All collapse to 0 when no tick carried them.
                double avgF1        = _f1Ticks        > 0 ? _f1Sum        / _f1Ticks        : 0;
                double avgCentroid  = _centroidTicks  > 0 ? _centroidSum  / _centroidTicks  : 0;
                double avgIntensity = _intensityTicks > 0 ? _intensitySum / _intensityTicks : 0;

                double avgPitch = _pitchTicks > 0 ? _pitchSum / _pitchTicks : 0;
                double pitchVariation = 0;
                if (_pitchTicks > 1)
                {
                    double meanSq = _pitchSqSum / _pitchTicks;
                    double variance = meanSq - (avgPitch * avgPitch);
                    pitchVariation = variance > 0 ? Math.Sqrt(variance) : 0;
                }
                double intonationRange = _pitchTicks > 0 ? Math.Max(0, _pitchMax - _pitchMin) : 0;

                // Resonance mean over ONLY the ticks that carried a real resonance signal.
                // 0 / no-resonance-tick ⇒ pitch-only session ⇒ the VI input keeps the Empty()
                // sentinel below so ScoreResonance returns its neutral-50 branch (RES-01).
                double avgResonance = _resonanceTicks > 0 ? _resonanceSum / _resonanceTicks : 0;

                // In-session stability STEADINESS — population std-dev of the per-tick
                // stability signal, mapped to 1 − clamp(stdDev/scale, 0, 1). This measures
                // reproducibility (how STEADY the voice was), not how voiced/high it was, so
                // Consistency stops echoing voicing clarity (CONS-1/2). Mirrors the pitch
                // std-dev pattern above. Neutral 0.5 fallback with ≤ 1 evaluated tick, where
                // the spread is undefined (a single sample would falsely read as perfectly steady).
                const double StabilitySteadinessScale = 0.25; // stdDev ≥ 0.25 ⇒ steadiness 0
                double stabilitySteadiness = 0.5;
                if (_ticks > 1)
                {
                    double avgStability = _stabilitySum / _ticks;
                    double meanSqStab = _stabilitySqSum / _ticks;
                    double varStab = meanSqStab - (avgStability * avgStability);
                    double stdStab = varStab > 0 ? Math.Sqrt(varStab) : 0;
                    stabilitySteadiness = 1.0 - Math.Clamp(stdStab / StabilitySteadinessScale, 0.0, 1.0);
                }

                outcome = new ExerciseSessionOutcome
                {
                    AverageResonance       = avgResonance,
                    AverageStability       = _ticks > 0 ? _stabilitySum / _ticks : 0,
                    StabilitySteadiness01  = stabilitySteadiness,
                    ComfortCompliance      = _ticks > 0 ? (double)_ticksInComfortZone / _ticks : 0,
                    HoldCompletion         = _maxHoldProgress,
                    SafetyLockEpisodes     = _safetyLockEpisodes,
                    ComfortBreachEpisodes  = _comfortBreachEpisodes,
                    FatigueIndicators      = _fatigueIndicators,
                    StrainDetections       = _strainDetections,
                    CoachingHintsTriggered = _coachHints,
                    EvaluatedTicks         = _ticks,
                    SessionHealthScore     = sessionHealth,
                    UsedResonanceSignal    = _resonanceTicks > 0,

                    // Raw acoustic aggregates for the Voice-Intelligence Intonation /
                    // VocalWeight / Pitch dimensions (0 / sentinel ⇒ scorer's neutral 50).
                    AverageF1Hz               = avgF1,
                    AverageSpectralCentroidHz = avgCentroid,
                    AverageIntensity          = avgIntensity,
                    AveragePitchHz            = avgPitch,
                    PitchVariationHz          = pitchVariation,
                    IntonationRangeHz         = intonationRange,
                    VoicedTicks               = _pitchTicks
                };

                sessionId  = _sessionId;
                exerciseId = _exerciseId;
                userId     = _userId;
                strain     = _strainDetections;
                locks      = _safetyLockEpisodes;
                breaches   = _comfortBreachEpisodes;
                pauses     = _pauseRecommendations;
                hydration  = _hydrationSuggestions;
                startedAt  = _startedAt;
            }

            LastPersistTask = PersistAsync(outcome, sessionId, exerciseId, userId, strain, locks, breaches,
                pauses, hydration, outcome.SessionHealthScore, startedAt);
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
                // Accumulate resonance ONLY when this tick carried a real resonance signal.
                // For a pitch-only profile PrimaryMetricScore is a normalised-pitch proxy, so
                // folding it into _resonanceSum would make the Resonance dimension echo pitch
                // (RES-01). When no tick uses resonance the average stays the Empty() sentinel.
                if (state.UsesResonanceSignal)
                {
                    _resonanceSum += Clamp01(state.PrimaryMetricScore);
                    _resonanceTicks++;
                }

                double stability = Clamp01(state.StabilityScore);
                _stabilitySum   += stability;
                _stabilitySqSum += stability * stability;   // for in-session std-dev (CONS-1/2)
                _maxHoldProgress = Math.Max(_maxHoldProgress, Clamp01(state.HoldProgress));

                // Accumulate the raw acoustic signals the coordinator forwarded this tick.
                // Each is gated on a positive, finite measurement (the "missing" sentinel
                // is <= 0 / NaN) so unmeasured ticks never bias the average toward zero.
                AccumulateRawSignals(state);

                if (state.IsInComfortZone) _ticksInComfortZone++;
                if (state.IsSafetyLocked && !_wasSafetyLocked) _safetyLockEpisodes++;
                if (!state.IsInComfortZone && _wasInComfortZone) _comfortBreachEpisodes++;
                _wasSafetyLocked  = state.IsSafetyLocked;
                _wasInComfortZone = state.IsInComfortZone;
            }

            // Supervisor has its own internal lock; never call it while holding ours.
            var decision = _healthSupervisor.Evaluate(state);
            // Gi advisoren supervisorens fatigue/strain-kontekst (samme tick) — kun for
            // ReasonCode/meldingsvalg; hydrering forblir lavere prioritet enn pause/hvile.
            var hydrationAdvice = _hydrationAdvisor?.Evaluate(
                state, decision.FatigueScore, decision.FatigueDetected, decision.StrainDetected);

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
                _healthScoreSum += _currentHealthScore;
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

        /// <summary>
        /// Folds this tick's raw acoustic signals into the per-signal accumulators.
        /// MUST be called while <see cref="_lock"/> is held. Each axis is summed only when
        /// the tick actually measured it (positive + finite); the "missing" sentinels
        /// (0, or NaN for HNR) are skipped so an unmeasured tick cannot bias the mean.
        /// </summary>
        private void AccumulateRawSignals(ExerciseLiveState state)
        {
            if (IsPositiveFinite(state.F1Hz))               { _f1Sum        += state.F1Hz;               _f1Ticks++; }
            if (IsPositiveFinite(state.SpectralCentroidHz)) { _centroidSum  += state.SpectralCentroidHz; _centroidTicks++; }
            if (IsPositiveFinite(state.Intensity))          { _intensitySum += state.Intensity;          _intensityTicks++; }

            if (IsPositiveFinite(state.PitchHz))
            {
                double p = state.PitchHz;
                _pitchSum   += p;
                _pitchSqSum += p * p;
                if (_pitchTicks == 0)
                {
                    _pitchMin = _pitchMax = p;
                }
                else
                {
                    if (p < _pitchMin) _pitchMin = p;
                    if (p > _pitchMax) _pitchMax = p;
                }
                _pitchTicks++;
            }
        }

        private static bool IsPositiveFinite(double v)
            => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;

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
                // Debug.WriteLine kompileres bort i Release — RC0-loggen er eneste spor.
                System.Diagnostics.Debug.WriteLine($"[ExerciseSessionRecorder] Session-start persist failed: {ex.Message}");
                Rc0RuntimeLog.Write("Persistence", $"SessionStartPersist FAILED; SessionId={sessionId}; {ex.GetType().Name}: {ex.Message}");
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
                Rc0RuntimeLog.Write("Persistence", $"SubjectivePersist FAILED; SessionId={sessionId}; {ex.GetType().Name}: {ex.Message}");
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
                // ── Cross-session recovery (Bølge 1, Agent 3 wiring) ─────────────────
                // Build a FULL RecoveryScoreInput from the persisted analytics history BEFORE
                // scoring the Voice-Intelligence recovery axis, so SessionsLast7Days /
                // HoursSinceLastSession / PriorFatigueIndicators are real and the scorer's
                // dormant overtraining/trend branches actually fire. The snapshot is taken as
                // of "now" but EXCLUDES the session just completing (it is not journaled yet),
                // so recovery reflects the load leading INTO this session. Never blocks
                // session end: any failure falls back to the in-session-only input.
                var fullRecoveryInput = await BuildCrossSessionRecoveryInputAsync(userId)
                    .ConfigureAwait(false);

                // Prior Voice-Intelligence trend, used as the per-dimension improvement
                // reference for the SessionInsight. Read BEFORE this session is written and
                // defensively filtered to exclude the current sessionId. Failure ⇒ empty
                // (treated as a first session) — never blocks.
                var priorTrend = await GetPriorTrendAsync(userId, sessionId)
                    .ConfigureAwait(false);

                // Voice Intelligence: derive the seven explainable 0–100 dimension scores
                // plus the hierarchy-weighted composite from the session aggregates we have.
                // Robust — never throws (see ComputeVoiceIntelligenceScores); a failed or
                // partial computation falls back to all-zero so session-end never blocks.
                var vi = ComputeVoiceIntelligenceScores(
                    outcome, pauseRecommendations, hydrationSuggestions, fullRecoveryInput);

                // End-of-session insight (Bølge 1, Agent 5 wiring). Built from the freshly
                // scored VI, the prior trend and the cross-session recovery result. Total —
                // any failure leaves LastSessionInsight null so the UI just hides its card.
                BuildAndPublishInsight(vi, priorTrend, outcome, fullRecoveryInput, sessionId);

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
                    FatigueIndicatorCount     = outcome.FatigueIndicators,
                    // Eight Voice Intelligence scores (0–100). Default 0 if scoring failed.
                    ResonanceScore100         = vi.Resonance.Score,
                    ComfortScore100           = vi.Comfort.Score,
                    ConsistencyScore100       = vi.Consistency.Score,
                    IntonationScore100        = vi.Intonation.Score,
                    VocalWeightScore100       = vi.VocalWeight.Score,
                    RecoveryScore100          = vi.Recovery.Score,
                    PitchScore100             = vi.Pitch.Score,
                    CompositeVoiceScore       = vi.CompositeVoiceScore
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

                // VH-02 (safety, strictly additive): the health supervisor's OBJECTIVE
                // pause recommendations get their own journaled channel — distinct from the
                // subjective SUBJECTIVE_HEALTH_CONCERN write (PersistSubjectiveConcernAsync).
                // Without this, a supervisor that repeatedly recommended a pause never fed
                // ProgressionSafetyGate.REPEATED_PAUSE_RECOMMENDATIONS nor the RecoveryScorer
                // pause penalty (both count PauseRecommended events), so an objectively
                // strained voice could keep progressing with no subjective report at all.
                // One PauseRecommended event per pause-recommending session — this can only
                // ADD a brake, never remove one.
                if (pauseRecommendations > 0)
                {
                    await _analyticsStore.RecordHealthEventAsync(new HealthAnalyticsEvent
                    {
                        SessionId  = sessionId,
                        UserId     = userId,
                        EventType  = HealthAnalyticsEventType.PauseRecommended,
                        OccurredAt = DateTime.Now,
                        Severity   = Math.Clamp(pauseRecommendations / 10.0, 0, 1),
                        ReasonCode = "SUPERVISOR_PAUSE"
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
                Rc0RuntimeLog.Write("Persistence", $"SessionPersist FAILED; SessionId={sessionId}; {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Derives the Voice Intelligence scores (seven explainable 0–100 dimensions +
        /// hierarchy-weighted composite) from the session outcome. Total/robust: never
        /// throws — any failure or partial signal falls back to an all-zero result so
        /// session-end persistence is never blocked.
        ///
        /// Signal availability from the exercise path (Bølge 2 SIGNAL-wiring):
        ///   • Resonance   ← outcome.AverageResonance, ONLY when outcome.UsedResonanceSignal
        ///                   (pitch-only sessions keep the neutral-50 sentinel — RES-01).
        ///   • Comfort     ← outcome.ComfortCompliance + ComfortBreachEpisodes (present).
        ///   • Consistency ← outcome.StabilitySteadiness01 (in-session stability std-dev →
        ///                   reproducibility, NOT the raw voicing-clarity mean — CONS-1/2).
        ///   • Recovery    ← this session's own fatigue/strain/lock/pause/hydration counts
        ///                   (an in-session restitution snapshot; cross-session history
        ///                   — SessionsLast7Days / HoursSinceLastSession — remains the
        ///                   analytics layer's job and stays at 0 here).
        ///   • VocalWeight ← outcome.AverageF1Hz + AverageSpectralCentroidHz + AverageIntensity
        ///                   (REAL when the resonance engine's formant snapshots fired this
        ///                   session; HNR has no source on this path ⇒ left missing). Falls
        ///                   back to neutral 50 only when no tick carried a formant snapshot.
        ///   • Pitch       ← outcome.AveragePitchHz + PitchVariationHz (REAL: the measured
        ///                   F0 from the production audio path, averaged over voiced ticks).
        ///                   Neutral 50 only when the session had no voiced tick.
        ///   • Intonation  ← outcome.IntonationRangeHz (REAL: max−min of measured F0 over
        ///                   voiced ticks). Neutral 50 only when the session had no voiced tick.
        /// </summary>
        private VoiceIntelligenceScores ComputeVoiceIntelligenceScores(
            ExerciseSessionOutcome outcome,
            int pauseRecommendations,
            int hydrationSuggestions,
            RecoveryScoreInput? crossSessionRecovery = null)
        {
            try
            {
                // Recovery snapshot. Prefer the FULL cross-session input built from analytics
                // history (Agent 3 wiring) so SessionsLast7Days / HoursSinceLastSession /
                // PriorFatigueIndicators are real and the dormant scorer branches fire. When
                // that history read failed it is null ⇒ fall back to the in-session snapshot
                // (this session is the "recent window"; cross-session fields stay 0 — the
                // pre-wiring behaviour, preserved so session end never regresses).
                var recovery = crossSessionRecovery ?? new RecoveryScoreInput
                {
                    RecentFatigueIndicators    = Math.Max(0, outcome.FatigueIndicators),
                    RecentStrainEpisodes       = Math.Max(0, outcome.StrainDetections),
                    RecentSafetyLocks          = Math.Max(0, outcome.SafetyLockEpisodes),
                    RecentPauseRecommendations = Math.Max(0, pauseRecommendations),
                    HydrationSuggestionsRecent = Math.Max(0, hydrationSuggestions),
                };

                // Start from the "everything missing" sentinels, then fill every axis the
                // exercise path actually measures. Intonation / VocalWeight / Pitch now
                // carry REAL aggregates whenever the session produced the signal; an axis
                // with no measurement keeps Empty()'s sentinel ⇒ scorer's neutral 50.
                var input = VoiceIntelligenceInput.Empty() with
                {
                    ComfortCompliance01 = Clamp01(outcome.ComfortCompliance),
                    ComfortBreaches     = Math.Max(0, outcome.ComfortBreachEpisodes),
                    // Consistency now measures REPRODUCIBILITY (in-session stability std-dev),
                    // not voicing clarity — feed the steadiness, not the raw stability mean
                    // (CONS-1/2). A flat trace ⇒ high; an oscillating mean-equal trace ⇒ low.
                    AverageStability01  = Clamp01(outcome.StabilitySteadiness01),
                    Recovery            = recovery,
                };

                // Resonance axis — fill the REAL resonance mean ONLY when the session
                // actually measured resonance. For a pitch-only session PrimaryMetricScore
                // was a normalised-pitch proxy (never folded into the mean), so we leave the
                // Empty() sentinel (-1) ⇒ ScoreResonance returns neutral 50 instead of
                // echoing the pitch proxy as resonance (RES-01).
                if (outcome.UsedResonanceSignal)
                    input = input with { AverageResonance01 = Clamp01(outcome.AverageResonance) };

                // VocalWeight axis — fill only the spectral signals that were measured;
                // leave the rest at Empty()'s "missing" sentinel. HNR has no source here.
                if (outcome.AverageF1Hz > 0)
                    input = input with { AverageF1Hz = outcome.AverageF1Hz };
                if (outcome.AverageSpectralCentroidHz > 0)
                    input = input with { AverageSpectralCentroidHz = outcome.AverageSpectralCentroidHz };
                if (outcome.AverageIntensity > 0)
                    input = input with { AverageIntensity = outcome.AverageIntensity };

                // Pitch + Intonation axes — measured F0 from the production audio path,
                // averaged/ranged over voiced ticks. Only set when the session was voiced.
                if (outcome.AveragePitchHz > 0)
                    input = input with
                    {
                        AveragePitchHz = outcome.AveragePitchHz,
                        PitchVariation = Math.Max(0, outcome.PitchVariationHz),
                    };
                if (outcome.IntonationRangeHz > 0)
                    input = input with { IntonationRangeHz = outcome.IntonationRangeHz };

                return _voiceIntelligenceScorer.Compute(input);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ExerciseSessionRecorder] Voice Intelligence scoring failed: {ex.Message}");
                return ZeroVoiceIntelligenceScores();
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // Cross-session recovery + insight (Bølge 1 wiring — Agents 3 & 5)
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a FULL cross-session <see cref="RecoveryScoreInput"/> from the persisted
        /// analytics history via <see cref="RecoveryIntelligenceService"/> (Agent 3 wiring),
        /// so the recovery axis reflects real training density / rest / fatigue-trend rather
        /// than this session's in-memory counts alone. Total: NEVER throws — any read or
        /// build failure returns <c>null</c> so the caller falls back to the in-session input
        /// and session end is never blocked. The snapshot is taken as of <c>DateTime.Now</c>
        /// and reflects the history leading INTO the just-finished session (which has not been
        /// journaled yet), which is exactly the recovery context the score should capture.
        /// </summary>
        private async Task<RecoveryScoreInput?> BuildCrossSessionRecoveryInputAsync(int userId)
        {
            try
            {
                var snapshot = await _recoveryIntelligence
                    .BuildSnapshotAsync(_analyticsStore, DateTime.Now, userId)
                    .ConfigureAwait(false);
                return RecoveryIntelligenceService.BuildScoreInput(snapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ExerciseSessionRecorder] Cross-session recovery build failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads the prior Voice-Intelligence trend used as the SessionInsight improvement
        /// reference (Agent 5 wiring). Read BEFORE the current session is journaled and
        /// defensively filtered to EXCLUDE <paramref name="currentSessionId"/> so an early
        /// write (or a re-run) cannot make a session improve against itself. Total: any
        /// failure returns an empty list (treated as a first session) — never blocks.
        /// </summary>
        private async Task<IReadOnlyList<VoiceIntelligenceTrendPoint>> GetPriorTrendAsync(
            int userId, int currentSessionId)
        {
            try
            {
                // Wide-but-bounded historical window; the builder only uses the most recent
                // point as the improvement reference, so a generous lookback is safe.
                var to   = DateTime.Now;
                var from = to.AddDays(-RecoveryIntelligenceService.ChronicWindowDays);
                var trend = await _analyticsStore
                    .GetVoiceIntelligenceTrendAsync(from, to, userId)
                    .ConfigureAwait(false);

                if (trend.Count == 0) return Array.Empty<VoiceIntelligenceTrendPoint>();

                // Exclude the session currently completing — it must never be its own
                // improvement reference.
                return trend.Where(p => p.SessionId != currentSessionId).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ExerciseSessionRecorder] Prior-trend read failed: {ex.Message}");
                return Array.Empty<VoiceIntelligenceTrendPoint>();
            }
        }

        /// <summary>
        /// Assembles the end-of-session <see cref="SessionInsight"/> via
        /// <see cref="SessionInsightBuilder"/> (Agent 5 wiring), stores it on
        /// <see cref="LastSessionInsight"/> and raises <see cref="SessionInsightReady"/>.
        /// Skipped for an empty/invalid session (no evaluated ticks) — there is nothing
        /// meaningful to reflect on, so the property stays null and the UI hides its card.
        /// Total: any failure leaves the property null and never blocks session end.
        /// </summary>
        private void BuildAndPublishInsight(
            VoiceIntelligenceScores vi,
            IReadOnlyList<VoiceIntelligenceTrendPoint> priorTrend,
            ExerciseSessionOutcome outcome,
            RecoveryScoreInput? crossSessionRecovery,
            int sessionId)
        {
            try
            {
                // Empty session ⇒ no clinical content to reflect on. Leave the insight null.
                if (outcome.EvaluatedTicks <= 0)
                {
                    LastSessionInsight = null;
                    return;
                }

                // Use the SAME recovery snapshot the VI recovery axis was scored from so the
                // insight's recovery-need is consistent with the dimension score. Fall back to
                // the in-session input when the cross-session read failed (mirrors the scorer).
                var recoveryInput = crossSessionRecovery ?? new RecoveryScoreInput
                {
                    RecentFatigueIndicators    = Math.Max(0, outcome.FatigueIndicators),
                    RecentStrainEpisodes       = Math.Max(0, outcome.StrainDetections),
                    RecentSafetyLocks          = Math.Max(0, outcome.SafetyLockEpisodes),
                };
                var recovery = _recoveryScorer.Score(recoveryInput);

                var insight = _sessionInsightBuilder.Build(
                    vi, priorTrend, outcome, recovery, sessionId);

                LastSessionInsight = insight;

                // Notify subscribers separately so a throwing handler never wipes the
                // successfully-built LastSessionInsight (it stays available to pollers).
                try { SessionInsightReady?.Invoke(insight); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ExerciseSessionRecorder] SessionInsightReady subscriber threw: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ExerciseSessionRecorder] Session insight build failed: {ex.Message}");
                LastSessionInsight = null;
            }
        }

        /// <summary>All-zero fallback — used only when scoring throws. Carries an
        /// explanation so a downstream reader can tell a real 0 from a scoring failure.</summary>
        private static VoiceIntelligenceScores ZeroVoiceIntelligenceScores()
        {
            var zero = new DimensionScore(0, "No score — Voice Intelligence scoring failed at session end.");
            return new VoiceIntelligenceScores
            {
                Resonance = zero,
                Comfort = zero,
                Consistency = zero,
                Intonation = zero,
                VocalWeight = zero,
                Recovery = zero,
                Pitch = zero,
                CompositeVoiceScore = 0
            };
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
