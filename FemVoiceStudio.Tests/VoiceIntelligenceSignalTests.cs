using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint B — Agent SIG (signal-wiring). Verifies the RAW per-tick acoustic signals
    /// the <see cref="VoiceIntelligenceScorer"/> needs for the Intonation, VocalWeight and
    /// Pitch dimensions are now actually carried end-to-end:
    ///
    ///   ExerciseLiveState (new raw fields) → ExerciseIntelligenceCoordinator (fills them
    ///   from measured pitch + the resonance engine's FormantSnapshot) → ExerciseSessionRecorder
    ///   (session-aggregates them) → VoiceIntelligenceInput (real values, not the neutral-50
    ///   fallback).
    ///
    /// House style: real classes + in-memory fakes only, NO mocking frameworks. The
    /// coordinator rate-limits evaluations to 100 ms, so metric injections after the first
    /// are preceded by a short sleep — same pattern as the existing coordinator/recorder tests.
    ///
    /// SIGNAL TRUTH (documented, asserted below):
    ///   • Pitch       — REAL: measured F0 (Hz) flows via UpdateMetrics' `pitch` argument
    ///                   (the production audio path passes DetectPitch().Pitch there).
    ///   • Intonation  — REAL: max−min of the measured F0 over voiced ticks (session range).
    ///   • VocalWeight — REAL when the resonance engine's FormantSnapshot fires (F1 /
    ///                   spectral centroid / RMS intensity). HNR has NO source on the
    ///                   exercise path, so it stays missing and VocalWeight scores on the
    ///                   other three signals. Falls back to neutral 50 only with no formants.
    /// </summary>
    public class VoiceIntelligenceSignalTests
    {
        private static readonly DateTime WindowFrom = new(2000, 1, 1, 0, 0, 0);
        private static readonly DateTime WindowTo = new(2100, 1, 1, 0, 0, 0);

        /// <summary>Advances past the coordinator's 100 ms evaluation rate-limit.</summary>
        private static void WaitForRateLimit() => Thread.Sleep(150);

        // ──────────────────────────────────────────────────────────────────────────────
        // 1. Model — ExerciseLiveState carries the new raw fields
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ExerciseLiveState_CarriesNewRawSignalFields()
        {
            var state = new ExerciseLiveState
            {
                PrimaryMetricScore = 0.7,
                F1Hz = 420,
                SpectralCentroidHz = 2100,
                Intensity = 0.3,
                PitchHz = 195,
                HnrDb = 18.5
            };

            Assert.Equal(420, state.F1Hz, 3);
            Assert.Equal(2100, state.SpectralCentroidHz, 3);
            Assert.Equal(0.3, state.Intensity, 3);
            Assert.Equal(195, state.PitchHz, 3);
            Assert.Equal(18.5, state.HnrDb, 3);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 2. Backward-compat — an old-style construction (no new fields) still compiles
        //    and the new fields default to their "missing" sentinels (0 / NaN).
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ExerciseLiveState_LegacyConstruction_DefaultsRawFieldsToMissingSentinels()
        {
            // Exactly the shape used before Bølge 2 — must still compile unchanged.
            var legacy = new ExerciseLiveState
            {
                PrimaryMetricScore = 0.5,
                SecondaryMetricScore = 0.4,
                StabilityScore = 0.6,
                IsInComfortZone = true,
                IsHoldingCorrectly = false,
                HoldProgress = 0.2,
                IsSafetyLocked = false,
                Quality = PerformanceQuality.Good,
                Timestamp = DateTime.Now,
                SessionElapsedSeconds = 3
            };

            Assert.Equal(0, legacy.F1Hz);
            Assert.Equal(0, legacy.SpectralCentroidHz);
            Assert.Equal(0, legacy.Intensity);
            Assert.Equal(0, legacy.PitchHz);
            Assert.True(double.IsNaN(legacy.HnrDb), "HNR has no source on the exercise path → defaults to NaN (missing).");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 3. Coordinator — UpdateMetrics' measured pitch lands on ExerciseLiveState.PitchHz
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Coordinator_ForwardsMeasuredPitch_OntoLiveStatePitchHz()
        {
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var states = new List<ExerciseLiveState>();
            coordinator.ExerciseUpdated += s => states.Add(s);

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            states.Clear();

            // `pitch` here is the MEASURED F0 (Hz) — the production path passes it through.
            coordinator.UpdateMetrics(resonanceScore: 0.7, pitch: 188, stability: 0.6, health: 100);

            Assert.NotEmpty(states);
            Assert.Equal(188, states[^1].PitchHz, 3);
            // HNR never has a source on this path.
            Assert.True(double.IsNaN(states[^1].HnrDb));
        }

        [Fact]
        public void Coordinator_UnvoicedTick_LeavesPitchHzAtZeroSentinel()
        {
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var states = new List<ExerciseLiveState>();
            coordinator.ExerciseUpdated += s => states.Add(s);

            // Pitch profile so an out-of-range pitch still produces a non-idle tick via stability.
            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            states.Clear();

            // pitch = 0 ⇒ unvoiced ⇒ PitchHz stays at the missing sentinel (0).
            coordinator.UpdateMetrics(resonanceScore: 0.7, pitch: 0, stability: 0.6, health: 100);

            Assert.NotEmpty(states);
            Assert.Equal(0, states[^1].PitchHz);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 4. Recorder — measured pitch aggregates into a REAL (≠50) Pitch score
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Recorder_AggregatesMeasuredPitch_IntoRealPitchScore_NotNeutralFifty()
        {
            var (coordinator, recorder, store, repository) = BuildRecorderStack();
            using var _ = coordinator;
            using var __ = recorder;

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 1, sessionId: 5001, userId: 1);

            // High resonance supports the pitch gate; pitch ~200 Hz is a strong feminine F0.
            coordinator.UpdateMetrics(0.9, pitch: 198, stability: 0.8, health: 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.9, pitch: 205, stability: 0.8, health: 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.9, pitch: 202, stability: 0.8, health: 100);

            recorder.CompleteSession();
            if (recorder.LastPersistTask is { } p) await p;

            var s = (await repository.GetSessionsAsync(1, WindowFrom, WindowTo))
                .Single(x => x.SessionId == 5001);

            // Pitch is now a REAL measurement (around 200 Hz, in the ideal band) — not the
            // old neutral-50 fallback.
            Assert.NotEqual(50, s.PitchScore100, 0);
            Assert.True(s.PitchScore100 > 50,
                $"A well-placed ~200 Hz pitch should score above neutral; was {s.PitchScore100}.");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 5. Recorder — pitch spread aggregates into a REAL (≠50) Intonation score
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Recorder_AggregatesPitchSpread_IntoRealIntonationScore_NotNeutralFifty()
        {
            var (coordinator, recorder, store, repository) = BuildRecorderStack();
            using var _ = coordinator;
            using var __ = recorder;

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 1, sessionId: 5002, userId: 1);

            // A varied, natural pitch contour: min 180, max 250 ⇒ intonation range 70 Hz,
            // squarely inside the ideal 30–120 Hz band ⇒ a strong (non-neutral) score.
            coordinator.UpdateMetrics(0.8, pitch: 180, stability: 0.7, health: 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.8, pitch: 250, stability: 0.7, health: 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.8, pitch: 215, stability: 0.7, health: 100);

            recorder.CompleteSession();
            if (recorder.LastPersistTask is { } p) await p;

            var s = (await repository.GetSessionsAsync(1, WindowFrom, WindowTo))
                .Single(x => x.SessionId == 5002);

            Assert.NotEqual(50, s.IntonationScore100, 0);
            Assert.True(s.IntonationScore100 > 50,
                $"A 70 Hz intonation range sits in the ideal band → above neutral; was {s.IntonationScore100}.");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 6. Recorder — a flat (constant-pitch) session has NO intonation range, so
        //    Intonation correctly falls back to neutral 50, while Pitch stays REAL.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Recorder_FlatPitch_IntonationFallsBackToFifty_PitchStaysReal()
        {
            var (coordinator, recorder, store, repository) = BuildRecorderStack();
            using var _ = coordinator;
            using var __ = recorder;

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 1, sessionId: 5003, userId: 1);

            // Constant pitch ⇒ range = max − min = 0 ⇒ Intonation has no usable signal (50),
            // but AveragePitchHz is a real 200 Hz ⇒ Pitch is REAL.
            coordinator.UpdateMetrics(0.9, pitch: 200, stability: 0.8, health: 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.9, pitch: 200, stability: 0.8, health: 100);

            recorder.CompleteSession();
            if (recorder.LastPersistTask is { } p) await p;

            var s = (await repository.GetSessionsAsync(1, WindowFrom, WindowTo))
                .Single(x => x.SessionId == 5003);

            Assert.Equal(50, s.IntonationScore100, 0);     // no range ⇒ neutral fallback
            Assert.NotEqual(50, s.PitchScore100, 0);       // measured pitch ⇒ real
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 7. Recorder — no voiced tick at all ⇒ BOTH Pitch and Intonation fall back to 50.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Recorder_NoVoicedPitch_PitchAndIntonationBothFallBackToFifty()
        {
            var (coordinator, recorder, store, repository) = BuildRecorderStack();
            using var _ = coordinator;
            using var __ = recorder;

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 1, sessionId: 5004, userId: 1);

            // Resonance + stability keep the tick non-idle, but pitch = 0 ⇒ unvoiced ⇒ no
            // pitch/intonation signal at all ⇒ both axes neutral 50.
            coordinator.UpdateMetrics(0.9, pitch: 0, stability: 0.8, health: 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.9, pitch: 0, stability: 0.8, health: 100);

            recorder.CompleteSession();
            if (recorder.LastPersistTask is { } p) await p;

            var s = (await repository.GetSessionsAsync(1, WindowFrom, WindowTo))
                .Single(x => x.SessionId == 5004);

            Assert.Equal(50, s.PitchScore100, 0);
            Assert.Equal(50, s.IntonationScore100, 0);
            // VocalWeight has no formant source on the parameterless coordinator ⇒ 50 too.
            Assert.Equal(50, s.VocalWeightScore100, 0);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 8. Recorder — pitch variation reduces, but does not zero, a placed pitch score.
        //    (Sanity that PitchVariationHz is wired through, not silently dropped.)
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Recorder_WildPitchVariation_LowersPitchScore_BelowSteadyEquivalent()
        {
            // Steady ~200 Hz session.
            var steady = await RunPitchSession(6001, new double[] { 200, 200, 200, 200 });
            // Same mean (~200 Hz) but a large spread ⇒ higher PitchVariation ⇒ the
            // FemVoiceScore variation penalty pulls the pitch score down.
            var wild = await RunPitchSession(6002, new double[] { 140, 260, 150, 250 });

            Assert.True(wild < steady,
                $"Wild pitch variation should score below the steady equivalent (wild={wild}, steady={steady}).");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 9. Coordinator (production path) — the resonance engine's FormantSnapshot fills
        //    F1 / spectral centroid / intensity on ExerciseLiveState (REAL VocalWeight signal).
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Coordinator_ProductionPath_FormantSnapshotFillsVocalWeightSignals()
        {
            using var resonanceEngine = NewInlineResonanceEngine();
            using var scoreEngine = new FemVoiceScoreEngine(new SigScoreRepository(), new SigUserRepository());
            using var comfortZone = new ComfortZoneController(new SigUserRepository(), new SigScoreRepository());
            var smartCoach = new SmartCoachEngine(new TestDatabaseService());

            using var coordinator = new ExerciseIntelligenceCoordinator(
                resonanceEngine, scoreEngine, comfortZone, smartCoach);

            var states = new List<ExerciseLiveState>();
            coordinator.ExerciseUpdated += s => states.Add(s);

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            states.Clear();

            // Drive the REAL resonance engine with a loud synthetic signal. ExtractFormants
            // has a deterministic fallback (F1=350/F2=2000/F3=2800) when no clean peaks are
            // found, so a valid FormantSnapshot — and thus FormantsUpdated — is guaranteed
            // once enough above-threshold frames are pumped.
            resonanceEngine.Start();

            // Confirm the REAL engine actually emits a valid formant snapshot before we
            // assert on the wiring — pump in a bounded loop until FormantsUpdated fires
            // (robust to per-machine frame-count/timing variance), then drive a non-idle
            // UpdateMetrics evaluation that forwards the cached signal.
            var formantFired = false;
            resonanceEngine.FormantsUpdated += f => { if (f.F1 > 0) formantFired = true; };
            for (var attempt = 0; attempt < 20 && !formantFired; attempt++)
                PumpSyntheticVoice(resonanceEngine);
            Assert.True(formantFired, "Resonansmotoren skal emittere et gyldig formant-snapshot for det syntetiske signalet.");

            WaitForRateLimit();
            coordinator.UpdateMetrics(0.7, pitch: 200, stability: 0.6, health: 100);

            Assert.NotEmpty(states);
            var withSignal = states.LastOrDefault(s => s.F1Hz > 0 || s.SpectralCentroidHz > 0);
            Assert.NotNull(withSignal);
            Assert.True(withSignal!.F1Hz > 0, $"F1 should be measured from the formant snapshot; was {withSignal.F1Hz}.");
            Assert.True(withSignal.SpectralCentroidHz > 0,
                $"Spectral centroid should be measured; was {withSignal.SpectralCentroidHz}.");
            Assert.True(withSignal.Intensity > 0, $"RMS intensity should be measured; was {withSignal.Intensity}.");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 10. Recorder (production path) — formant-fed ticks aggregate into a REAL (≠50)
        //     VocalWeight score.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Recorder_ProductionPath_FormantSignal_ProducesRealVocalWeightScore()
        {
            using var resonanceEngine = NewInlineResonanceEngine();
            using var scoreEngine = new FemVoiceScoreEngine(new SigScoreRepository(), new SigUserRepository());
            using var comfortZone = new ComfortZoneController(new SigUserRepository(), new SigScoreRepository());
            var smartCoach = new SmartCoachEngine(new TestDatabaseService());

            using var coordinator = new ExerciseIntelligenceCoordinator(
                resonanceEngine, scoreEngine, comfortZone, smartCoach);

            var supervisor = new VocalHealthSupervisor();
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            using var recorder = new ExerciseSessionRecorder(coordinator, supervisor, store);

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 2, sessionId: 7001, userId: 1);

            resonanceEngine.Start();

            // Several formant-bearing ticks. Order matters: pump audio first (this refills
            // the formant cache; its own evaluation produces an idle state the recorder
            // skips because resonance is still 0), THEN wait past the 100 ms rate-limit, THEN
            // drive a non-idle UpdateMetrics evaluation that carries the cached formant signal
            // into the recorder. Sleeping before UpdateMetrics guarantees its evaluation is
            // not swallowed by the rate-limit consumed by the pump's evaluation.
            for (var i = 0; i < 4; i++)
            {
                PumpSyntheticVoice(resonanceEngine);
                WaitForRateLimit();
                coordinator.UpdateMetrics(0.7, pitch: 200, stability: 0.6, health: 100);
            }

            recorder.CompleteSession();
            if (recorder.LastPersistTask is { } p) await p;

            var s = (await repository.GetSessionsAsync(1, WindowFrom, WindowTo))
                .Single(x => x.SessionId == 7001);

            // VocalWeight scored on real F1/centroid/intensity ⇒ not the neutral-50 fallback.
            Assert.NotEqual(50, s.VocalWeightScore100, 0);
            Assert.InRange(s.VocalWeightScore100, 1, 100);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 11. Coordinator — StopExercise clears the raw-signal cache so it does not leak
        //     into the next session.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Coordinator_StopExercise_ClearsRawSignalCache_NoLeakIntoNextSession()
        {
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var states = new List<ExerciseLiveState>();
            coordinator.ExerciseUpdated += s => states.Add(s);

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            coordinator.UpdateMetrics(0.9, pitch: 210, stability: 0.8, health: 100);
            coordinator.StopExercise();

            // New session, then an UNVOICED tick: if the previous 210 Hz leaked, PitchHz
            // would be non-zero. It must be the missing sentinel (0).
            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            states.Clear();
            coordinator.UpdateMetrics(0.9, pitch: 0, stability: 0.8, health: 100);

            Assert.NotEmpty(states);
            Assert.Equal(0, states[^1].PitchHz);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 12. Recorder — raw signal does NOT leak across BeginSession boundaries.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Recorder_RawSignalAccumulators_ResetBetweenSessions()
        {
            var (coordinator, recorder, store, repository) = BuildRecorderStack();
            using var _ = coordinator;
            using var __ = recorder;

            // Session A: a clearly varied, voiced pitch contour.
            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 1, sessionId: 8001, userId: 1);
            coordinator.UpdateMetrics(0.9, pitch: 180, stability: 0.8, health: 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.9, pitch: 245, stability: 0.8, health: 100);
            recorder.CompleteSession();
            if (recorder.LastPersistTask is { } pa) await pa;

            // Session B: entirely UNVOICED. If accumulators leaked, Pitch/Intonation would
            // be non-neutral. They must both reset to the neutral-50 fallback.
            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 1, sessionId: 8002, userId: 1);
            coordinator.UpdateMetrics(0.9, pitch: 0, stability: 0.8, health: 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.9, pitch: 0, stability: 0.8, health: 100);
            recorder.CompleteSession();
            if (recorder.LastPersistTask is { } pb) await pb;

            var b = (await repository.GetSessionsAsync(1, WindowFrom, WindowTo))
                .Single(x => x.SessionId == 8002);

            Assert.Equal(50, b.PitchScore100, 0);
            Assert.Equal(50, b.IntonationScore100, 0);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs a single voiced session with the given per-tick pitches and returns the
        /// persisted PitchScore100. Uses the parameterless coordinator (measured-pitch path).
        /// </summary>
        private async Task<double> RunPitchSession(int sessionId, double[] pitches)
        {
            var (coordinator, recorder, store, repository) = BuildRecorderStack();
            using var _ = coordinator;
            using var __ = recorder;

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 1, sessionId: sessionId, userId: 1);

            for (var i = 0; i < pitches.Length; i++)
            {
                coordinator.UpdateMetrics(0.9, pitch: pitches[i], stability: 0.8, health: 100);
                if (i < pitches.Length - 1) WaitForRateLimit();
            }

            recorder.CompleteSession();
            if (recorder.LastPersistTask is { } p) await p;

            return (await repository.GetSessionsAsync(1, WindowFrom, WindowTo))
                .Single(x => x.SessionId == sessionId)
                .PitchScore100;
        }

        /// <summary>
        /// Builds the parameterless-coordinator recorder stack (real classes, in-memory
        /// analytics repository). The measured-pitch path is driven via UpdateMetrics.
        /// </summary>
        private static (
            ExerciseIntelligenceCoordinator coordinator,
            ExerciseSessionRecorder recorder,
            SessionAnalyticsStore store,
            InMemorySessionAnalyticsRepository repository) BuildRecorderStack()
        {
            var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            var recorder = new ExerciseSessionRecorder(coordinator, supervisor, store);
            return (coordinator, recorder, store, repository);
        }

        /// <summary>
        /// Constructs a <see cref="ResonanceProxyEngine"/> with NO captured synchronisation
        /// context, so its FormantsUpdated event fires INLINE (synchronously) rather than
        /// being posted to xunit's async test context. That makes the production-path
        /// formant signal deterministic: the snapshot is cached before the next evaluation.
        /// </summary>
        private static ResonanceProxyEngine NewInlineResonanceEngine()
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                return new ResonanceProxyEngine();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        /// <summary>
        /// Pumps a loud synthetic vowel-like signal (two formant-ish sinusoids over a 200 Hz
        /// fundamental) through the resonance engine — enough samples to cross several FFT
        /// frames so FormantsUpdated fires. Amplitude is well above the 0.01 RMS threshold.
        /// </summary>
        private static void PumpSyntheticVoice(ResonanceProxyEngine engine)
        {
            const int sampleRate = 48000;
            const int count = 48000 / 4; // ~0.25 s ⇒ many 2048-sample frames
            var buffer = new float[count];
            for (var i = 0; i < count; i++)
            {
                double t = (double)i / sampleRate;
                // FormantSnapshot.IsValid krever F1>0 && F2>0 && F3>0 — så vi trenger TRE
                // distinkte spektraltopper i 200–4000 Hz-søkeområdet, ellers blir F3=0,
                // snapshot ugyldig og FormantsUpdated fyrer aldri (rotårsak til at
                // VocalWeight-produksjonsstien testet falt tilbake til nøytral 50).
                // Grunntonen legges under 200 Hz-søkegulvet så den ikke plukkes som formant.
                double sample =
                    0.40 * Math.Sin(2 * Math.PI * 150 * t) +   // grunntone (under formant-søkegulvet)
                    0.35 * Math.Sin(2 * Math.PI * 700 * t) +   // ~F1
                    0.30 * Math.Sin(2 * Math.PI * 1800 * t) +  // ~F2
                    0.25 * Math.Sin(2 * Math.PI * 2900 * t);   // ~F3 (sikrer gyldig snapshot)
                buffer[i] = (float)sample;
            }
            engine.ProcessSamples(buffer);
        }

        // ── Minimal in-memory repositories (real classes, no mocks) ─────────────────────

        private sealed class SigScoreRepository : IScoreRepository
        {
            private readonly List<FemVoiceScoreSnapshot> _scores = new();
            private UserScoreBaseline? _baseline;

            public Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetScoreHistoryAsync(int userId, DateTime from, DateTime to, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<FemVoiceScoreSnapshot>>(
                    _scores.Where(s => s.UserId == userId && s.Timestamp >= from && s.Timestamp <= to).ToList());

            public Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetRecentScoresAsync(int userId, int count, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<FemVoiceScoreSnapshot>>(
                    _scores.Where(s => s.UserId == userId).OrderByDescending(s => s.Timestamp).Take(count).ToList());

            public Task SaveScoreAsync(FemVoiceScoreSnapshot snapshot, CancellationToken ct = default)
            {
                _scores.Add(snapshot);
                return Task.CompletedTask;
            }

            public Task<UserScoreBaseline?> GetBaselineAsync(int userId, CancellationToken ct = default)
                => Task.FromResult(_baseline);

            public Task SaveBaselineAsync(UserScoreBaseline baseline, CancellationToken ct = default)
            {
                _baseline = baseline;
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetRollingHistoryAsync(int userId, int days, CancellationToken ct = default)
            {
                var cutoff = DateTime.Now.AddDays(-days);
                return Task.FromResult<IReadOnlyList<FemVoiceScoreSnapshot>>(
                    _scores.Where(s => s.UserId == userId && s.Timestamp >= cutoff).OrderBy(s => s.Timestamp).ToList());
            }
        }

        private sealed class SigUserRepository : IUserRepository
        {
            public Task<ScoringConfiguration?> GetScoringConfigurationAsync(int userId) => Task.FromResult<ScoringConfiguration?>(null);
            public Task SaveScoringConfigurationAsync(int userId, ScoringConfiguration config) => Task.CompletedTask;
            public Task<UserComfortZoneSettings?> GetComfortZoneSettingsAsync(int userId) => Task.FromResult<UserComfortZoneSettings?>(null);
            public Task SaveComfortZoneSettingsAsync(int userId, UserComfortZoneSettings settings) => Task.CompletedTask;
            public Task<UserHealthData?> GetUserHealthDataAsync(int userId) => Task.FromResult<UserHealthData?>(new UserHealthData { UserId = userId, HealthScore = 100 });
            public Task RecordStrainIncidentAsync(int userId, StrainIncident incident) => Task.CompletedTask;
            public Task<IReadOnlyList<StrainIncident>> GetRecentStrainIncidentsAsync(int userId, int days) => Task.FromResult<IReadOnlyList<StrainIncident>>(new List<StrainIncident>());
            public Task<UserToleranceData?> GetToleranceDataAsync(int userId) => Task.FromResult<UserToleranceData?>(null);
            public Task UpdateToleranceDataAsync(int userId, UserToleranceData data) => Task.CompletedTask;
        }
    }
}
