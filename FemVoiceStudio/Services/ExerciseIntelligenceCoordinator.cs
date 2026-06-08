using System;
using System.Collections.Generic;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Coordinates real-time biofeedback evaluation for exercises.
    /// Integrates ResonanceProxyEngine, FemVoiceScoreEngine, ComfortZoneController,
    /// legacy acoustic health events and SmartCoachEngine into a single adaptive feedback loop.
    ///
    /// Design note: because the engines expose no polling properties, this coordinator
    /// caches the most recent value delivered by each engine event and re-evaluates on
    /// every incoming update. All evaluation logic is UI-agnostic.
    /// </summary>
    public class ExerciseIntelligenceCoordinator : IDisposable
    {
        // ── Engine dependencies (nullable — only set by the production constructor) ─
        // VoiceHealthMonitor/VocalHealthLegacyBridge ble fjernet i integrasjons-
        // oppryddingen: Analyze() ble aldri kalt i produksjon, så helse-eventene
        // fyrte aldri. Helse kommer nå utelukkende via health-parameteren i
        // UpdateMetrics (drevet av VocalHealthSupervisor gjennom ExerciseSessionRecorder).
        private readonly ResonanceProxyEngine? _resonanceEngine;
        private readonly FemVoiceScoreEngine? _scoreEngine;
        private readonly ComfortZoneController? _comfortZoneController;
        private readonly SmartCoachEngine? _smartCoachEngine;
        private readonly ILocalizationService _localization;

        // ── Cached engine values (guarded by _lock) ──────────────────────────────────
        // These are populated exclusively by engine events; never polled from engines.
        private double _cachedResonanceScore = 0;
        private double _cachedStabilityScore = 0;
        private double _cachedPitch = 0;          // OptimalPitch from ComfortZoneState
        private double _cachedPitchMin = 0;       // MinPitch from ComfortZoneState
        private double _cachedPitchMax = 0;       // MaxPitch from ComfortZoneState
        private bool   _comfortZoneSafetyLocked = false;

        // ── Cached RAW acoustic signals for Voice Intelligence (guarded by _lock) ─────
        // Populated only when the producing source actually delivers them; otherwise they
        // stay at their "missing" sentinel and the per-tick ExerciseLiveState carries the
        // sentinel forward. Never fabricated.
        //   _cachedMeasuredPitch ← measured F0 (Hz) from UpdateMetrics (production audio
        //     path), kept STRICTLY separate from _cachedPitch (the comfort-zone optimum).
        //   F1 / centroid / intensity ← the resonance engine's FormantSnapshot.
        private double _cachedMeasuredPitch = 0;     // measured F0 (Hz); 0 ⇒ unvoiced/unset
        private double _cachedFormantF1 = 0;         // 0 ⇒ not measured
        private double _cachedSpectralCentroid = 0;  // 0 ⇒ not measured
        private double _cachedFormantIntensity = 0;  // RmsValue 0–1; 0 ⇒ not measured

        // ── Exercise context (guarded by _lock) ──────────────────────────────────────
        private ExerciseTargetProfile _currentProfile;
        private int _currentUserId = 1;
        // Brukerens stilmål. Default = Feminine ⇒ historisk lys/fremre resonans-scoring
        // (ingen atferdsendring). Propageres til ResonanceProxyEngine slik at scoringen
        // peker mot brukerens faktiske klangmål, ikke en universell feminin klang.
        private VoiceStyleGoal _voiceStyle = VoiceStyleGoal.Feminine;

        // ── Hold-detection state (guarded by _lock) ──────────────────────────────────
        private double _currentHoldProgress = 0;
        private DateTime? _holdStartTime = null;

        // ── Safety state (guarded by _lock) ──────────────────────────────────────────
        // _isSafetyLocked is true when the ComfortZone OR the health monitor locks out.
        private bool   _isSafetyLocked = false;
        private double _currentHealthScore = 100;   // healthy by default

        // ── Rate-limiting (guarded by _lock) ─────────────────────────────────────────
        private DateTime _lastEvaluationTime = DateTime.MinValue;
        private readonly Dictionary<string, DateTime> _lastCoachMessages = new();
        private readonly TimeSpan _evaluationInterval = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan _coachMessageInterval = TimeSpan.FromSeconds(5);

        // ── Synchronisation ──────────────────────────────────────────────────────────
        private readonly object _lock = new();

        // ── Backing event fields ─────────────────────────────────────────────────────
        private event Action<ExerciseLiveState>? _exerciseUpdated;
        private event Action<InlineCoachMessage>? _inlineCoachUpdated;

        // ── Disposal ─────────────────────────────────────────────────────────────────
        private bool _disposed = false;

        // ── Exercise lifecycle (guarded by _lock) ─────────────────────────────────────
        private bool _isActive = false;
        private DateTime? _sessionStartTimestamp = null;

        // ────────────────────────────────────────────────────────────────────────────
        // Constructors
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Production constructor — all dependencies resolved via DI.
        /// </summary>
        public ExerciseIntelligenceCoordinator(
            ResonanceProxyEngine resonanceEngine,
            FemVoiceScoreEngine scoreEngine,
            ComfortZoneController comfortZoneController,
            SmartCoachEngine smartCoachEngine)
        {
            _resonanceEngine      = resonanceEngine      ?? throw new ArgumentNullException(nameof(resonanceEngine));
            _scoreEngine          = scoreEngine          ?? throw new ArgumentNullException(nameof(scoreEngine));
            _comfortZoneController = comfortZoneController ?? throw new ArgumentNullException(nameof(comfortZoneController));
            _smartCoachEngine     = smartCoachEngine     ?? throw new ArgumentNullException(nameof(smartCoachEngine));
            _localization         = LocalizationService.Instance;

            _currentProfile = ExerciseTargetProfile.ResonanceExercise();

            // Wire engine events — values are cached in handlers, never polled.
            _resonanceEngine.ResonanceScoreUpdated += OnResonanceScoreUpdated;
            _resonanceEngine.FormantsUpdated       += OnFormantsUpdated;
            _scoreEngine.ScoreUpdated              += OnScoreUpdated;
            _comfortZoneController.ZoneUpdated     += OnComfortZoneUpdated;
        }

        /// <summary>
        /// Parameterless constructor for unit-testing with manual metric injection.
        /// No engine events are wired; use <see cref="UpdateMetrics"/> and
        /// <see cref="UpdateHealthScore"/> to drive state.
        /// </summary>
        public ExerciseIntelligenceCoordinator()
        {
            _currentProfile = ExerciseTargetProfile.ResonanceExercise();
            _localization = LocalizationService.Instance;
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Public Events
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>Fired on every evaluation cycle with the latest exercise state.</summary>
        public event Action<ExerciseLiveState> ExerciseUpdated
        {
            add    { lock (_lock) { _exerciseUpdated += value; } }
            remove { lock (_lock) { _exerciseUpdated -= value; } }
        }

        /// <summary>Fired when the inline coach has a new message for the user.</summary>
        public event Action<InlineCoachMessage> InlineCoachUpdated
        {
            add    { lock (_lock) { _inlineCoachUpdated += value; } }
            remove { lock (_lock) { _inlineCoachUpdated -= value; } }
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the active voice-style goal and propagates it to the resonance engine so
        /// scoring aims at the user's actual timbre target (darker for DarkFeminine/
        /// Androgynous) rather than a universal feminine resonance. Default behaviour
        /// (Feminine) is unchanged. Safe to call before or after
        /// <see cref="SetExerciseContext(ExerciseTargetProfile,int)"/>; the style is
        /// re-applied on every context switch.
        /// </summary>
        public void SetVoiceStyle(VoiceStyleGoal style)
        {
            lock (_lock) { _voiceStyle = style; }
            _resonanceEngine?.SetVoiceStyle(style);
        }

        /// <summary>Returns the active voice-style goal driving resonance scoring.</summary>
        public VoiceStyleGoal GetVoiceStyle()
        {
            lock (_lock) { return _voiceStyle; }
        }

        /// <summary>
        /// Switches the active exercise context. Resets all hold and rate-limit state.
        /// </summary>
        public void SetExerciseContext(ExerciseTargetProfile profile, int userId)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (userId <= 0)     throw new ArgumentException("User ID must be positive.", nameof(userId));

            VoiceStyleGoal style;
            lock (_lock)
            {
                _currentProfile = profile;
                _currentUserId  = userId;
                ResetHoldState();
                _isSafetyLocked       = false;
                _comfortZoneSafetyLocked = false;
                _lastEvaluationTime   = DateTime.MinValue;
                _lastCoachMessages.Clear();
                style = _voiceStyle;
            }

            // Re-apply the style to the engine on every context switch so a freshly
            // resolved engine (or one shared across exercises) always scores against the
            // current user's timbre goal. Null-safe: no engine ⇒ no-op (test path).
            _resonanceEngine?.SetVoiceStyle(style);

            _exerciseUpdated?.Invoke(BuildDefaultState());
        }

        /// <summary>
        /// Style-aware overload: sets the voice-style goal, then switches the exercise
        /// context. Equivalent to calling <see cref="SetVoiceStyle"/> followed by
        /// <see cref="SetExerciseContext(ExerciseTargetProfile,int)"/>.
        /// </summary>
        public void SetExerciseContext(ExerciseTargetProfile profile, int userId, VoiceStyleGoal style)
        {
            SetVoiceStyle(style);
            SetExerciseContext(profile, userId);
        }

        /// <summary>Returns a snapshot of the active exercise profile.</summary>
        public ExerciseTargetProfile GetCurrentProfile()
        {
            lock (_lock) { return _currentProfile; }
        }

        /// <summary>Returns the current hold-progress value (0–1).</summary>
        public double GetHoldProgress()
        {
            lock (_lock) { return _currentHoldProgress; }
        }

        /// <summary>
        /// Whether an exercise is currently active and accepting evaluation input.
        /// <c>false</c> before <see cref="StartExercise"/> is called, and after
        /// <see cref="StopExercise"/> completes.
        /// </summary>
        public bool IsExerciseActive
        {
            get { lock (_lock) { return _isActive; } }
        }

        /// <summary>
        /// Starts a new exercise session. If an exercise is already active it is stopped
        /// cleanly first so no hold-state leaks between sessions.
        /// </summary>
        /// <param name="profile">Target profile for the exercise.</param>
        /// <param name="userId">Identifier of the current user.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is not positive.</exception>
        public void StartExercise(ExerciseTargetProfile profile, int userId)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (userId <= 0)     throw new ArgumentException("User ID must be positive.", nameof(userId));

            // Stop any running exercise cleanly; safe no-op when nothing is active.
            StopExercise();

            // Resets all hold-state, rate-limit state and safety flags.
            SetExerciseContext(profile, userId);

            lock (_lock)
            {
                _isActive = true;
                _sessionStartTimestamp = DateTime.Now;
            }
        }

        /// <summary>
        /// Style-aware overload: applies the voice-style goal before starting the
        /// exercise so resonance scoring aims at the user's timbre target from the first
        /// frame. Equivalent to <see cref="SetVoiceStyle"/> + <see cref="StartExercise(ExerciseTargetProfile,int)"/>.
        /// </summary>
        public void StartExercise(ExerciseTargetProfile profile, int userId, VoiceStyleGoal style)
        {
            SetVoiceStyle(style);
            StartExercise(profile, userId);
        }

        /// <summary>
        /// Stops the active exercise, resets all cached metric fields to literal zeros,
        /// and publishes a final zeroed <see cref="ExerciseLiveState"/> so the UI returns
        /// to its idle appearance.
        /// Safe to call when no exercise is active (no-op in that case).
        /// </summary>
        public void StopExercise()
        {
            bool wasActive;
            Action<ExerciseLiveState>? snapshot;

            lock (_lock)
            {
                wasActive = _isActive;
                if (!wasActive) return;  // guard: already stopped — no stale publish

                _isActive              = false;
                _sessionStartTimestamp = null;

                // Reset hold-detection state.
                _holdStartTime       = null;
                _currentHoldProgress = 0;

                // Reset ALL cached metric fields with explicit literal zeros so no
                // floating-point residue survives into the next session.
                _cachedResonanceScore = 0d;
                _cachedStabilityScore = 0d;
                _cachedPitch          = 0d;
                _isSafetyLocked       = false;
                _currentHealthScore   = 0d;

                // Reset the raw Voice-Intelligence signal cache too, so no measured F1 /
                // centroid / intensity / pitch residue survives into the next session.
                _cachedMeasuredPitch    = 0d;
                _cachedFormantF1        = 0d;
                _cachedSpectralCentroid = 0d;
                _cachedFormantIntensity = 0d;

                // Capture the delegate while the lock is held so subscribers added
                // concurrently on another thread do not race the publish below.
                snapshot = _exerciseUpdated;
            }

            // Publish the final zeroed state OUTSIDE the lock to prevent deadlocks.
            // Every field uses a literal zero — never a computed or accumulated value.
            var finalState = new ExerciseLiveState
            {
                PrimaryMetricScore    = 0,
                SecondaryMetricScore  = 0,
                StabilityScore        = 0,
                IsInComfortZone       = false,
                IsHoldingCorrectly    = false,
                HoldProgress          = 0,
                IsSafetyLocked        = false,
                Quality               = PerformanceQuality.Poor,
                Timestamp             = DateTime.Now,
                SessionElapsedSeconds = 0
            };

            snapshot?.Invoke(finalState);
        }

        /// <summary>
        /// Injects all metric values directly — intended for unit tests or manual input
        /// scenarios where engine events are not wired up.
        /// </summary>
        /// <param name="resonanceScore">Resonance score (0–1).</param>
        /// <param name="pitch">Current pitch in Hz (used for comfort-zone check).</param>
        /// <param name="stability">Stability score (0–1).</param>
        /// <param name="health">Health score (0–100; below 70 triggers safety lock).</param>
        public void UpdateMetrics(double resonanceScore, double pitch, double stability, double health)
        {
            lock (_lock)
            {
                _cachedResonanceScore = resonanceScore;
                _cachedStabilityScore = stability;
                _cachedPitch          = pitch;
                // The `pitch` argument is the MEASURED F0 (Hz) from the production audio
                // path (ExerciseWindow → DetectPitch). Cache it separately from the
                // comfort-zone optimum so the Voice-Intelligence Pitch/Intonation axes see
                // the user's real pitch, not the zone centre. 0 ⇒ unvoiced ⇒ left unset.
                _cachedMeasuredPitch  = pitch;
                _currentHealthScore   = health;

                // Derive pitch min/max from the profile if not yet seeded from zone events.
                if (_cachedPitchMin == 0 && _currentProfile.MinPitch.HasValue)
                    _cachedPitchMin = _currentProfile.MinPitch.Value;
                if (_cachedPitchMax == 0 && _currentProfile.MaxPitch.HasValue)
                    _cachedPitchMax = _currentProfile.MaxPitch.Value;
            }

            EvaluateExerciseStateFromCache();
        }

        /// <summary>
        /// Updates only the health score and triggers a re-evaluation.
        /// Intended for unit tests or direct health-monitor injection.
        /// </summary>
        public void UpdateHealthScore(double healthScore)
        {
            lock (_lock)
            {
                _currentHealthScore = healthScore;

                if (healthScore < 70)
                    _isSafetyLocked = true;
                else if (!_comfortZoneSafetyLocked)
                    _isSafetyLocked = false; // only clear if comfort zone is also unlocked
            }

            EvaluateExerciseStateFromCache();
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Engine Event Handlers — cache values, then evaluate
        // ────────────────────────────────────────────────────────────────────────────

        private void OnResonanceScoreUpdated(double resonanceScore)
        {
            lock (_lock) { _cachedResonanceScore = resonanceScore; }
            EvaluateExerciseStateFromCache();
        }

        private void OnFormantsUpdated(FormantSnapshot formants)
        {
            // Formant data arrives alongside the resonance score. Cache the RAW spectral
            // signals the VocalWeight dimension needs (F1, spectral centroid, RMS intensity)
            // ONLY when the snapshot is valid — an invalid/empty snapshot leaves the prior
            // cache untouched so a single silent frame does not zero a measured average.
            // HNR is not part of FormantSnapshot, so it is never sourced here (stays missing).
            if (formants.IsValid)
            {
                lock (_lock)
                {
                    if (formants.F1 > 0)               _cachedFormantF1        = formants.F1;
                    if (formants.SpectralCentroid > 0) _cachedSpectralCentroid = formants.SpectralCentroid;
                    if (formants.RmsValue > 0)         _cachedFormantIntensity = formants.RmsValue;
                }
            }

            EvaluateExerciseStateFromCache();
        }

        private void OnScoreUpdated(FemVoiceScoreSnapshot scoreSnapshot)
        {
            lock (_lock) { _cachedStabilityScore = scoreSnapshot.StabilityScore; }
            EvaluateExerciseStateFromCache();
        }

        private void OnComfortZoneUpdated(ComfortZoneState zoneState)
        {
            bool wasLocked;
            lock (_lock)
            {
                wasLocked                = _isSafetyLocked;
                _comfortZoneSafetyLocked = zoneState.IsSafetyLocked;
                _isSafetyLocked          = zoneState.IsSafetyLocked || _currentHealthScore < 70;

                // Cache pitch boundaries delivered by the zone update.
                _cachedPitchMin = zoneState.MinPitch;
                _cachedPitchMax = zoneState.MaxPitch;
                _cachedPitch    = zoneState.OptimalPitch;
            }

            EvaluateExerciseStateFromCache();

            if (zoneState.IsSafetyLocked && !wasLocked)
            {
                TryPublishCoachMessage(
                    _localization.GetString("ExerciseCoach_ComfortZoneLock"),
                    "COMFORT_ZONE_LOCK",
                    MessageSeverity.Warning,
                    15);
            }
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Core Evaluation Pipeline
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads cached values and runs the shared evaluation core.
        /// Used by all engine-event handlers.
        /// </summary>
        private void EvaluateExerciseStateFromCache()
        {
            // Fast-path rate-limit outside the lock.
            if (DateTime.Now - _lastEvaluationTime < _evaluationInterval)
                return;

            double resonanceScore, stabilityScore, pitch, pitchMin, pitchMax, healthScore;
            double measuredPitch, formantF1, spectralCentroid, formantIntensity;
            bool safetyLocked;

            lock (_lock)
            {
                resonanceScore   = _cachedResonanceScore;
                stabilityScore   = _cachedStabilityScore;
                pitch            = _cachedPitch;
                pitchMin         = _cachedPitchMin;
                pitchMax         = _cachedPitchMax;
                healthScore      = _currentHealthScore;
                safetyLocked     = _isSafetyLocked;
                measuredPitch    = _cachedMeasuredPitch;
                formantF1        = _cachedFormantF1;
                spectralCentroid = _cachedSpectralCentroid;
                formantIntensity = _cachedFormantIntensity;
            }

            EvaluateExerciseStateCore(
                resonanceScore, pitch, pitchMin, pitchMax, stabilityScore, healthScore,
                measuredPitch, formantF1, spectralCentroid, formantIntensity);
        }

        /// <summary>
        /// Shared evaluation core — called by both the cache-read path and
        /// <see cref="UpdateMetrics"/> (test path).
        /// </summary>
        private void EvaluateExerciseStateCore(
            double resonanceScore,
            double pitch,
            double pitchMin,
            double pitchMax,
            double stabilityScore,
            double healthScore,
            double measuredPitch = 0,
            double formantF1 = 0,
            double spectralCentroid = 0,
            double formantIntensity = 0)
        {
            ExerciseLiveState liveState;
            var pendingMessages = new List<(string msg, string reason, MessageSeverity sev, int dismiss)>();

            lock (_lock)
            {
                // Double-check rate limit inside lock.
                var now = DateTime.Now;
                if (now - _lastEvaluationTime < _evaluationInterval)
                    return;
                _lastEvaluationTime = now;

                // Guard: do not evaluate when no exercise is active.
                if (!_isActive)
                    return;

                bool healthSafetyLock        = healthScore < 70;
                bool effectivelySafetyLocked = _isSafetyLocked || healthSafetyLock;

                // ── Metric calculation ───────────────────────────────────────────────
                double primaryMetric   = 0;
                double secondaryMetric = 0;
                bool   isInComfortZone = true;

                if (_currentProfile.UsesResonance)
                    primaryMetric = resonanceScore;

                if (_currentProfile.UsesPitch)
                {
                    if (!_currentProfile.UsesResonance)
                        primaryMetric = NormalizePitch(pitch, pitchMin, pitchMax);

                    isInComfortZone = !effectivelySafetyLocked
                                      && pitchMin > 0
                                      && pitchMax > pitchMin
                                      && pitch >= pitchMin
                                      && pitch <= pitchMax;
                }

                if (_currentProfile.UsesStability)
                    secondaryMetric = stabilityScore;

                // ── Hold logic ───────────────────────────────────────────────────────
                bool isHoldingCorrectly = _currentProfile.RequiredHoldSeconds > 0
                    && EvaluateHoldCondition(primaryMetric, secondaryMetric, isInComfortZone);

                double holdProgress = CalculateHoldProgressLocked(isHoldingCorrectly, effectivelySafetyLocked);

                // ── Quality ──────────────────────────────────────────────────────────
                var quality = effectivelySafetyLocked
                    ? PerformanceQuality.Poor
                    : DeterminePerformanceQuality(primaryMetric, secondaryMetric, isInComfortZone);

                // ── Session elapsed time ─────────────────────────────────────────────
                // Derived from _sessionStartTimestamp (set in StartExercise) using the
                // same `now` already captured for rate-limiting — no second clock read.
                int sessionElapsedSeconds = _sessionStartTimestamp.HasValue
    ? (int)(now - _sessionStartTimestamp.Value).TotalSeconds
    : 0;

                // ── State snapshot ───────────────────────────────────────────────────
                liveState = new ExerciseLiveState
                {
                    PrimaryMetricScore    = primaryMetric,
                    SecondaryMetricScore  = secondaryMetric,
                    StabilityScore        = stabilityScore,
                    IsInComfortZone       = isInComfortZone,
                    IsHoldingCorrectly    = isHoldingCorrectly,
                    HoldProgress          = holdProgress,
                    IsSafetyLocked        = effectivelySafetyLocked,
                    Quality               = quality,
                    Timestamp             = now,
                    SessionElapsedSeconds = sessionElapsedSeconds,

                    // Raw acoustic signals for Voice Intelligence — forwarded ONLY when the
                    // source actually delivered a positive measurement; otherwise the field
                    // keeps its "missing" sentinel (0, or NaN for HNR which has no source on
                    // this path) and the session aggregate falls back to neutral 50.
                    F1Hz               = formantF1 > 0 ? formantF1 : 0,
                    SpectralCentroidHz = spectralCentroid > 0 ? spectralCentroid : 0,
                    Intensity          = formantIntensity > 0 ? formantIntensity : 0,
                    PitchHz            = measuredPitch > 0 ? measuredPitch : 0,
                    HnrDb              = double.NaN  // no HNR source on the exercise path
                };

                // ── Coaching triggers (collected inside lock, published outside) ─────
                if (effectivelySafetyLocked && healthSafetyLock)
                    pendingMessages.Add((_localization.GetString("ExerciseCoach_HealthSafetyLock"),
                        "HEALTH_SAFETY_LOCK", MessageSeverity.Warning, 12));

                if (_currentProfile.UsesResonance && !effectivelySafetyLocked)
                {
                    if (primaryMetric < _currentProfile.TargetResonanceMin)
                        pendingMessages.Add((_localization.GetString("ExerciseCoach_ResonanceTooLow"),
                            "RESONANCE_TOO_LOW", MessageSeverity.Suggestion, 6));
                    else if (primaryMetric > _currentProfile.TargetResonanceMax)
                        pendingMessages.Add((_localization.GetString("ExerciseCoach_ResonanceTooHigh"),
                            "RESONANCE_TOO_HIGH", MessageSeverity.Suggestion, 6));
                }

                if (_currentProfile.UsesStability && !effectivelySafetyLocked
                    && secondaryMetric < _currentProfile.StabilityThreshold)
                    pendingMessages.Add((_localization.GetString("ExerciseCoach_StabilityLow"),
                        "STABILITY_LOW", MessageSeverity.Info, 5));

                if (_currentProfile.UsesPitch && !isInComfortZone && !effectivelySafetyLocked)
                    pendingMessages.Add((_localization.GetString("ExerciseCoach_PitchOutOfZone"),
                        "PITCH_OUT_OF_ZONE", MessageSeverity.Suggestion, 6));

                if (holdProgress >= 1.0 && !effectivelySafetyLocked)
                    pendingMessages.Add((_localization.GetString("ExerciseCoach_HoldComplete"),
                        "HOLD_COMPLETE", MessageSeverity.Info, 4));

            } // ← lock released

            // ── Publish outside the lock to prevent deadlocks ─────────────────────
            _exerciseUpdated?.Invoke(liveState);

            foreach (var (msg, reason, sev, dismiss) in pendingMessages)
                TryPublishCoachMessage(msg, reason, sev, dismiss);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Hold Detection Helpers  (must be called while _lock is held)
        // ────────────────────────────────────────────────────────────────────────────

        private bool EvaluateHoldCondition(double primaryMetric, double secondaryMetric, bool isInComfortZone)
        {
            bool ok = true;
            if (_currentProfile.UsesResonance)
                ok = ok && primaryMetric >= _currentProfile.TargetResonanceMin
                        && primaryMetric <= _currentProfile.TargetResonanceMax;
            if (_currentProfile.UsesPitch)
                ok = ok && isInComfortZone;
            if (_currentProfile.UsesStability)
                ok = ok && secondaryMetric >= _currentProfile.StabilityThreshold;
            return ok;
        }

        private double CalculateHoldProgressLocked(bool isHoldingCorrectly, bool isSafetyLocked)
        {
            if (isSafetyLocked)
                return _currentHoldProgress; // frozen — resume when safety clears

            if (isHoldingCorrectly)
            {
                _holdStartTime ??= DateTime.Now;
                var elapsed = DateTime.Now - _holdStartTime.Value;
                _currentHoldProgress = Math.Min(1.0, elapsed.TotalSeconds / _currentProfile.RequiredHoldSeconds);
            }
            else
            {
                ResetHoldState();
            }

            return _currentHoldProgress;
        }

        private void ResetHoldState()
        {
            _holdStartTime       = null;
            _currentHoldProgress = 0;
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Quality Determination  (must be called while _lock is held)
        // ────────────────────────────────────────────────────────────────────────────

        private PerformanceQuality DeterminePerformanceQuality(
            double primaryMetric, double secondaryMetric, bool isInComfortZone)
        {
            double score = 0;

            if (_currentProfile.UsesResonance)
            {
                if (primaryMetric >= _currentProfile.TargetResonanceMin &&
                    primaryMetric <= _currentProfile.TargetResonanceMax)
                    score += 0.5;
                else if (primaryMetric >= _currentProfile.TargetResonanceMin * 0.8 &&
                         primaryMetric <= _currentProfile.TargetResonanceMax * 1.2)
                    score += 0.3;
            }

            if (_currentProfile.UsesStability)
            {
                if (secondaryMetric >= _currentProfile.StabilityThreshold)       score += 0.3;
                else if (secondaryMetric >= _currentProfile.StabilityThreshold * 0.8) score += 0.2;
            }

            if (_currentProfile.UsesPitch && isInComfortZone)
                score += 0.2;

            if (score >= 0.8) return PerformanceQuality.Excellent;
            if (score >= 0.6) return PerformanceQuality.Good;
            if (score >= 0.4) return PerformanceQuality.Improving;
            return PerformanceQuality.Poor;
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Coach Messaging  (must be called OUTSIDE _lock)
        // ────────────────────────────────────────────────────────────────────────────

        private void TryPublishCoachMessage(
            string message, string reason, MessageSeverity severity, int autoDismissSeconds)
        {
            InlineCoachMessage? coachMessage = null;

            lock (_lock)
            {
                if (_lastCoachMessages.TryGetValue(reason, out var lastTime) &&
                    DateTime.Now - lastTime < _coachMessageInterval)
                    return;

                _lastCoachMessages[reason] = DateTime.Now;
                coachMessage = new InlineCoachMessage
                {
                    ShortMessage       = message,
                    CoachingReason     = reason,
                    Severity           = severity,
                    AutoDismissSeconds = autoDismissSeconds
                };
            }

            _inlineCoachUpdated?.Invoke(coachMessage); // outside lock
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Utilities
        // ────────────────────────────────────────────────────────────────────────────

        private ExerciseLiveState BuildDefaultState()
        {
            bool locked;
            lock (_lock) { locked = _isSafetyLocked; }

            return new ExerciseLiveState
            {
                PrimaryMetricScore   = 0,
                SecondaryMetricScore = 0,
                StabilityScore       = 0,
                IsInComfortZone      = false,
                IsHoldingCorrectly   = false,
                HoldProgress         = 0,
                IsSafetyLocked       = locked,
                Quality              = PerformanceQuality.Poor,
                Timestamp            = DateTime.Now
            };
        }

        /// <summary>
        /// Normalises a raw pitch value to 0–1 using the provided min/max range.
        /// Returns 0 if the range is degenerate.
        /// </summary>
        private static double NormalizePitch(double pitch, double min, double max)
        {
            if (max <= min) return 0;
            return Math.Clamp((pitch - min) / (max - min), 0, 1);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // IDisposable
        // ────────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Releases managed resources and unsubscribes from engine events.</summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (_resonanceEngine != null)
                {
                    _resonanceEngine.ResonanceScoreUpdated -= OnResonanceScoreUpdated;
                    _resonanceEngine.FormantsUpdated       -= OnFormantsUpdated;
                }

                if (_scoreEngine != null)
                    _scoreEngine.ScoreUpdated -= OnScoreUpdated;

                if (_comfortZoneController != null)
                    _comfortZoneController.ZoneUpdated -= OnComfortZoneUpdated;

                lock (_lock)
                {
                    _exerciseUpdated    = null;
                    _inlineCoachUpdated = null;
                }
            }

            _disposed = true;
        }
    }
}
