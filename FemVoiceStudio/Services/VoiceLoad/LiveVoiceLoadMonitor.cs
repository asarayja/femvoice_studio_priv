using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models.VoiceLoad;

namespace FemVoiceStudio.Services.VoiceLoad
{
    /// <summary>The per-tick output of the monitor: the load state plus the gated recommendation.</summary>
    public sealed record VoiceLoadObservation
    {
        public VoiceLoadState State { get; init; } = new();
        public VoiceLoadRecommendation Recommendation { get; init; } = new();
    }

    /// <summary>
    /// Sprint F (Agent 1) — the stateful, per-session Live Voice Load Monitor.
    ///
    /// Tracks cumulative vocal load over a session from a stream of <see cref="VoiceLoadInputs"/>
    /// snapshots (one per evaluation tick), using rolling EMAs/counters — PREFERRING TREND over
    /// single-frame events. It performs NO signal processing, holds NO references to audio/engines,
    /// reads NO clock (it uses each snapshot's Timestamp), and NEVER mutates any input.
    ///
    /// Conservative by design: startup/tail silence is ignored, soft/low-pitch voicing is not
    /// punished, insufficient data is marked clearly, and the band only jumps straight to
    /// PauseRecommended when a safety/health rule has already fired. Not a medical system.
    /// </summary>
    public sealed class LiveVoiceLoadMonitor
    {
        // ── Tunables (conservative; verified against the Sprint F validation scenarios) ──
        private const double MaxTickDeltaSeconds = 0.5;     // cap inter-tick gaps (≈100ms ticks)
        private const double StabilityFastAlpha = 0.12;
        private const double StabilityBaselineAlpha = 0.04;
        private const double StrainAlpha = 0.10;
        private const double FatigueAlpha = 0.10;
        private const double ResonanceAlpha = 0.10;
        private const double RmsAlpha = 0.10;
        private const double ScoreAlpha = 0.15;
        private const int MinVoicedTicksForSufficiency = 20;
        private const double MessageMinIntervalSeconds = 45;
        private const double HydrationCooldownSeconds = 180;
        private const double TrendEpsilon = 0.03;

        private readonly object _sync = new();

        private bool _initialized;
        private DateTime _sessionStart;
        private DateTime _lastTimestamp;
        private DateTime _lastPauseTimestamp;
        private int _exerciseCountInSession = 1;   // one BeginSession == one exercise (recorder hardcodes 1)

        private long _activeTicks, _voicedTicks, _dropoutTicks, _resonanceTicks, _outOfComfortTicks;
        private double _activeVoicedSeconds;
        private double _stabilityFastEma, _stabilityBaselineEma;
        // Frozen early-session stability baseline (avg over the first N active ticks). Unlike an
        // EMA it does NOT catch up to a sustained decline, so trend = recent − early stays valid.
        private double _stabilityEarlySum;
        private int _stabilityEarlyCount;
        private double _stabilityEarly;
        private const int EarlyBaselineTicks = 50;
        private double _strainEma, _fatigueEma;
        private double _resonanceEma, _resonanceInstabilityEma;
        private bool _resonanceEverUsed;
        private double _rmsMeanEma, _rmsVolatilityEma;
        private bool _rmsEverMeasured;
        private double _smoothedScore;

        private int _pausesTaken;
        private bool _loadWasHighBeforeLastPause;
        private RecoveryReadinessEvaluator.Snapshot _beforePauseSnapshot;

        // Anti-spam state
        private DateTime? _lastMessageTime;
        private GentleCoachCategory _lastMessageCategory = GentleCoachCategory.None;
        private PauseRecommendationLevel _lastPauseLevel = PauseRecommendationLevel.None;
        private DateTime? _lastHydrationContextTime;
        private bool _hydrationRemindedThisSession;

        private VoiceLoadState _currentState = new();

        public VoiceLoadState CurrentState { get { lock (_sync) return _currentState; } }

        /// <summary>Resets all per-session state. Call at session start (BeginSession).</summary>
        public void Reset(DateTime sessionStart)
        {
            lock (_sync)
            {
                _initialized = true;
                _sessionStart = sessionStart;
                _lastTimestamp = sessionStart;
                _lastPauseTimestamp = sessionStart;
                _activeTicks = _voicedTicks = _dropoutTicks = _resonanceTicks = _outOfComfortTicks = 0;
                _activeVoicedSeconds = 0;
                _stabilityFastEma = _stabilityBaselineEma = 0;
                _stabilityEarlySum = 0;
                _stabilityEarlyCount = 0;
                _stabilityEarly = 0;
                _strainEma = _fatigueEma = 0;
                _resonanceEma = _resonanceInstabilityEma = 0;
                _resonanceEverUsed = false;
                _rmsMeanEma = _rmsVolatilityEma = 0;
                _rmsEverMeasured = false;
                _smoothedScore = 0;
                _pausesTaken = 0;
                _loadWasHighBeforeLastPause = false;
                _beforePauseSnapshot = default;
                _lastMessageTime = null;
                _lastMessageCategory = GentleCoachCategory.None;
                _lastPauseLevel = PauseRecommendationLevel.None;
                _lastHydrationContextTime = null;
                _hydrationRemindedThisSession = false;
                _currentState = new VoiceLoadState();
            }
        }

        /// <summary>Records that the user actually took a pause (resets time-since-pause).</summary>
        public void NotePauseTaken(DateTime at)
        {
            lock (_sync)
            {
                _pausesTaken++;
                _lastPauseTimestamp = at;
                _loadWasHighBeforeLastPause =
                    _currentState.VoiceLoadBand is VoiceLoadBand.High or VoiceLoadBand.PauseRecommended;
                _beforePauseSnapshot = SnapshotLocked();
            }
        }

        /// <summary>
        /// Evaluates practice readiness after a resume, comparing the post-resume rolling state
        /// to the snapshot captured at the last pause. PRACTICE readiness only — never medical.
        /// </summary>
        public RecoveryReadinessResult EvaluateRecoveryReadiness(DateTime resumeAt)
        {
            lock (_sync)
            {
                if (_pausesTaken == 0)
                    return new RecoveryReadinessResult { Readiness = RecoveryReadiness.InsufficientData, Reasons = new[] { "NO_PAUSE_TAKEN" } };
                var pausedSeconds = Math.Max(0, (resumeAt - _lastPauseTimestamp).TotalSeconds);
                return RecoveryReadinessEvaluator.Evaluate(_beforePauseSnapshot, SnapshotLocked(), pausedSeconds);
            }
        }

        /// <summary>Processes one tick and returns the current load state + gated recommendation.</summary>
        public VoiceLoadObservation Observe(VoiceLoadInputs input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            lock (_sync)
            {
                if (!_initialized) Reset(input.Timestamp);

                var dt = Math.Clamp((input.Timestamp - _lastTimestamp).TotalSeconds, 0, MaxTickDeltaSeconds);
                _lastTimestamp = input.Timestamp;

                // Health-derived signals are meaningful even across brief gaps → updated every tick.
                _strainEma = Ema(_strainEma, Clamp01(input.StrainScore), StrainAlpha);
                _fatigueEma = Ema(_fatigueEma, Clamp01(input.FatigueScore), FatigueAlpha);

                var voiced = input.PitchHz > 0;
                var active = voiced || input.IsHoldingCorrectly || input.Intensity > 0;
                if (active)
                {
                    _activeTicks++;
                    if (voiced) { _voicedTicks++; _activeVoicedSeconds += dt; }
                    else _dropoutTicks++;
                    if (!input.IsInComfortZone) _outOfComfortTicks++;

                    _stabilityFastEma = Ema(_stabilityFastEma, Clamp01(input.StabilityScore), StabilityFastAlpha);
                    _stabilityBaselineEma = Ema(_stabilityBaselineEma, Clamp01(input.StabilityScore), StabilityBaselineAlpha);
                    if (_stabilityEarlyCount < EarlyBaselineTicks)
                    {
                        _stabilityEarlySum += Clamp01(input.StabilityScore);
                        _stabilityEarlyCount++;
                        _stabilityEarly = _stabilityEarlySum / _stabilityEarlyCount;
                    }

                    if (input.UsesResonanceSignal)
                    {
                        _resonanceEverUsed = true;
                        _resonanceTicks++;
                        var prev = _resonanceEma;
                        _resonanceEma = Ema(_resonanceEma, Clamp01(input.ResonanceScore), ResonanceAlpha);
                        _resonanceInstabilityEma = Ema(_resonanceInstabilityEma, Math.Abs(input.ResonanceScore - prev), ResonanceAlpha);
                    }

                    if (input.Intensity > 0)
                    {
                        _rmsEverMeasured = true;
                        var prevMean = _rmsMeanEma;
                        _rmsMeanEma = Ema(_rmsMeanEma, input.Intensity, RmsAlpha);
                        _rmsVolatilityEma = Ema(_rmsVolatilityEma, Math.Abs(input.Intensity - prevMean), RmsAlpha);
                    }
                }

                var aggregates = BuildAggregatesLocked(input);
                var scoreResult = VoiceLoadScoreEngine.ComputeRawScore(aggregates);
                _smoothedScore = _voicedTicks <= 1 ? scoreResult.RawScore : Ema(_smoothedScore, scoreResult.RawScore, ScoreAlpha);

                var dataSufficient = _voicedTicks >= MinVoicedTicksForSufficiency;
                var band = VoiceLoadScoreEngine.ResolveBand(
                    _smoothedScore, input.IsSafetyLocked, input.PauseRecommendedByHealth, input.HealthStateRank, dataSufficient);
                var trend = ResolveTrendLocked(dataSufficient);

                _currentState = new VoiceLoadState
                {
                    VoiceLoadScore = (int)Math.Round(_smoothedScore),
                    VoiceLoadBand = band,
                    ActiveVoicedSeconds = _activeVoicedSeconds,
                    TimeSinceLastPauseSeconds = Math.Max(0, (input.Timestamp - _lastPauseTimestamp).TotalSeconds),
                    ExerciseCountInSession = _exerciseCountInSession,
                    TrendDirection = trend,
                    PrimaryLoadDrivers = scoreResult.Drivers,
                    Confidence = Clamp01((double)_voicedTicks / (MinVoicedTicksForSufficiency * 3)),
                    IsDataSufficient = dataSufficient
                };

                var recommendation = BuildRecommendationLocked(input, _currentState, dataSufficient);
                return new VoiceLoadObservation { State = _currentState, Recommendation = recommendation };
            }
        }

        /// <summary>Assembles the end-of-session trend summary from the accumulated rolling state.</summary>
        public SessionTrendSummary BuildSessionTrendSummary()
        {
            lock (_sync)
            {
                if (_voicedTicks < MinVoicedTicksForSufficiency)
                    return new SessionTrendSummary { TrendCategory = SessionTrendCategory.InsufficientData, TrendConfidence = 0 };

                var delta = _stabilityFastEma - _stabilityEarly;
                var dropoutRate = _activeTicks == 0 ? 0 : (double)_dropoutTicks / _activeTicks;
                var changes = new List<string>();
                SessionTrendCategory category;

                if (delta >= 0.06) { category = SessionTrendCategory.ImprovingStability; changes.Add("STABILITY_IMPROVED"); }
                else if (delta <= -0.12 || _strainEma >= 0.6) { category = SessionTrendCategory.ClearDecline; changes.Add("STABILITY_DECLINED"); }
                else if (delta <= -0.05) { category = SessionTrendCategory.MildDecline; changes.Add("STABILITY_SLIGHTLY_DECLINED"); }
                else if (dropoutRate >= 0.35) { category = SessionTrendCategory.Variable; changes.Add("PITCH_DROPOUTS_INCREASED"); }
                else { category = SessionTrendCategory.Stable; changes.Add("STABILITY_HELD"); }

                if (_strainEma >= 0.5) changes.Add("STRAIN_ACCUMULATED");

                var adjustmentKey = category switch
                {
                    SessionTrendCategory.ClearDecline => VoiceLoadMessageKeys.EndSession,
                    SessionTrendCategory.MildDecline => VoiceLoadMessageKeys.PauseSoon,
                    SessionTrendCategory.Variable => VoiceLoadMessageKeys.LowerIntensity,
                    _ => VoiceLoadMessageKeys.ContinueCalmly
                };

                return new SessionTrendSummary
                {
                    TrendCategory = category,
                    TrendConfidence = Clamp01((double)_voicedTicks / (MinVoicedTicksForSufficiency * 4)),
                    MainChanges = changes,
                    RecommendedAdjustmentKey = adjustmentKey
                };
            }
        }

        /// <summary>Builds the evidence record for the latest decision (for diagnostics/reports).</summary>
        public VoiceLoadEvidence BuildEvidence(VoiceLoadObservation observation, RecoveryReadiness recoveryReadiness, SessionTrendSummary trend)
        {
            if (observation is null) throw new ArgumentNullException(nameof(observation));
            return new VoiceLoadEvidence
            {
                VoiceLoadScore = observation.State.VoiceLoadScore,
                VoiceLoadBand = observation.State.VoiceLoadBand.ToString(),
                PauseRecommendationLevel = observation.Recommendation.Pause.ToString(),
                HydrationContextLevel = observation.Recommendation.Hydration.ToString(),
                RecoveryReadiness = recoveryReadiness.ToString(),
                SessionTrend = trend?.TrendCategory.ToString() ?? SessionTrendCategory.InsufficientData.ToString(),
                VoiceLoadDrivers = observation.State.PrimaryLoadDrivers,
                MessageShownCategory = observation.Recommendation.Message?.Category.ToString(),
                MessageSuppressed = observation.Recommendation.Message is null,
                SuppressionReason = observation.Recommendation.SuppressionReason,
                TimeSinceLastPauseSeconds = observation.State.TimeSinceLastPauseSeconds,
                ActiveVoicedSeconds = observation.State.ActiveVoicedSeconds,
                ExerciseCountInSession = observation.State.ExerciseCountInSession,
                TrendConfidence = trend?.TrendConfidence ?? 0
            };
        }

        // ── internals (all called under _sync) ──────────────────────────────────────

        private VoiceLoadScoreEngine.Aggregates BuildAggregatesLocked(VoiceLoadInputs input)
            => new(
                ActiveVoicedSeconds: _activeVoicedSeconds,
                TimeSinceLastPauseSeconds: Math.Max(0, (input.Timestamp - _lastPauseTimestamp).TotalSeconds),
                StabilityEma: _stabilityFastEma,
                DropoutRate: _activeTicks == 0 ? 0 : (double)_dropoutTicks / _activeTicks,
                RmsVolatility: _rmsEverMeasured ? _rmsVolatilityEma : -1,
                ResonanceInstability: _resonanceEverUsed ? Math.Clamp(_resonanceInstabilityEma * 3.0, 0, 1) : -1,
                StrainEma: _strainEma,
                FatigueEma: _fatigueEma,
                OutOfComfortRate: _activeTicks == 0 ? 0 : (double)_outOfComfortTicks / _activeTicks,
                HealthPauseRecommended: input.PauseRecommendedByHealth,
                IsSafetyLocked: input.IsSafetyLocked,
                HealthStateRank: input.HealthStateRank);

        private VoiceLoadTrendDirection ResolveTrendLocked(bool dataSufficient)
        {
            if (!dataSufficient || _stabilityEarlyCount == 0) return VoiceLoadTrendDirection.Unknown;
            var delta = _stabilityFastEma - _stabilityEarly;   // recent vs frozen early baseline
            if (delta > TrendEpsilon) return VoiceLoadTrendDirection.Improving;
            if (delta < -TrendEpsilon) return VoiceLoadTrendDirection.Worsening;
            return VoiceLoadTrendDirection.Stable;
        }

        private RecoveryReadinessEvaluator.Snapshot SnapshotLocked()
            => new(_stabilityFastEma, _strainEma, _activeTicks == 0 ? 0 : (double)_dropoutTicks / _activeTicks, _voicedTicks >= MinVoicedTicksForSufficiency);

        private VoiceLoadRecommendation BuildRecommendationLocked(VoiceLoadInputs input, VoiceLoadState state, bool dataSufficient)
        {
            var loadPersistedAfterPause = _pausesTaken >= 1 && _loadWasHighBeforeLastPause
                && state.VoiceLoadBand == VoiceLoadBand.PauseRecommended;

            var (pause, pauseReasons) = PauseIntelligenceEngine.Decide(
                state.VoiceLoadBand, state.TrendDirection, input.PauseRecommendedByHealth,
                input.IsSafetyLocked, loadPersistedAfterPause, dataSufficient);

            var hydration = ComputeHydrationContextLocked(input, state, dataSufficient);
            var candidate = GentleCoachComposer.Compose(pause, hydration, state.VoiceLoadBand, state.TrendDirection, dataSufficient);

            var (message, suppression) = ApplyAntiSpamLocked(candidate, pause, input.Timestamp);
            if (message is not null)
            {
                _lastMessageTime = input.Timestamp;
                _lastMessageCategory = message.Category;
            }
            _lastPauseLevel = pause;

            return new VoiceLoadRecommendation
            {
                Pause = pause,
                Hydration = hydration,
                Message = message,
                SuppressionReason = suppression,
                Reasons = pauseReasons
            };
        }

        private HydrationContextLevel ComputeHydrationContextLocked(VoiceLoadInputs input, VoiceLoadState state, bool dataSufficient)
        {
            if (!dataSufficient) return HydrationContextLevel.None;

            // Defer to the existing hydration signals; add load-context only conservatively.
            var triggered = input.HydrationSuggestedByHealth || input.HydrationSuggestedByAdvisor
                || state.VoiceLoadBand is VoiceLoadBand.High or VoiceLoadBand.PauseRecommended;
            if (!triggered) return HydrationContextLevel.None;

            // Anti-spam: respect a cooldown so hydration context is never shown every exercise.
            var cooledDown = _lastHydrationContextTime is null
                || (input.Timestamp - _lastHydrationContextTime.Value).TotalSeconds >= HydrationCooldownSeconds;
            if (!cooledDown) return HydrationContextLevel.None;

            _lastHydrationContextTime = input.Timestamp;
            var level = _hydrationRemindedThisSession && state.VoiceLoadBand == VoiceLoadBand.PauseRecommended
                ? HydrationContextLevel.RepeatedLoadContext
                : HydrationContextLevel.GentleReminder;
            _hydrationRemindedThisSession = true;
            return level;
        }

        private (GentleCoachMessage? Message, string? Suppression) ApplyAntiSpamLocked(
            GentleCoachMessage candidate, PauseRecommendationLevel pause, DateTime now)
        {
            if (candidate.Category is GentleCoachCategory.None or GentleCoachCategory.InsufficientData
                || string.IsNullOrEmpty(candidate.LocalizationKey))
                return (null, "NO_GUIDANCE");

            var isEscalation = pause > _lastPauseLevel;   // rising urgency always allowed through

            // Min interval between messages (unless escalating).
            if (!isEscalation && _lastMessageTime is { } last
                && (now - last).TotalSeconds < MessageMinIntervalSeconds)
                return (null, "RATE_LIMIT");

            // Do not repeat the same category back-to-back unless escalating.
            if (!isEscalation && candidate.Category == _lastMessageCategory)
                return (null, "DUPLICATE_CATEGORY");

            return (candidate, null);
        }

        private static double Ema(double previous, double current, double alpha)
            => previous + (Clamp01OrRaw(current) - previous) * Math.Clamp(alpha, 0, 1);

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1);

        // RMS/abs-dev values can exceed 1 only transiently; keep them non-negative without clamping signal.
        private static double Clamp01OrRaw(double v) => double.IsNaN(v) ? 0 : Math.Max(0, v);
    }
}
