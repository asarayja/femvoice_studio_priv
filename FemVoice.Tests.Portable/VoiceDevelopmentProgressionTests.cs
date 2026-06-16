using System;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Services.Progression;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// AGENT 10 — VOICE DEVELOPMENT PROGRESSION.
    ///
    /// Proves the redesign that replaces single-axis "pitch progression" with a
    /// multidimensional "Voice Development Progression" driven by the seven
    /// <see cref="VoiceIntelligenceScores"/> dimensions, ADDITIVELY and backward-
    /// compatibly:
    ///
    ///   • <see cref="ProgressionAdjustmentDimension"/> gains Comfort/Consistency/
    ///     Intonation/VocalWeight (appended LAST so existing enum values are stable).
    ///   • <see cref="ProgressionOrchestratorContext.Voice"/> is OPTIONAL. When null,
    ///     the orchestrator's resonance/stability/hold gate-chain is byte-identical
    ///     to the pre-redesign behaviour. When set, the weakest dimension (broken in
    ///     clinical hierarchy order) selects the next development focus. Pitch is
    ///     never the sole/primary driver — it only wins when strictly lowest.
    ///   • <see cref="ComplexityEngine.CanAdvanceToNextLevel"/> gains Comfort/
    ///     Consistency/Recovery thresholds as EXTRA AND-conditions — advancement can
    ///     only become STRICTER, never easier. Recovery gate ⇒ an overloaded voice is
    ///     never advanced even when resonance/pitch look strong (Health > Progression).
    ///   • The <see cref="ProgressionSafetyGate"/> is untouched and a safety block can
    ///     NEVER be lifted by any VoiceMetrics score (Safety > everything).
    ///
    /// No mocking: the real orchestrator + real engines run over the in-memory
    /// <see cref="InMemorySessionAnalyticsRepository"/> (the only fake), mirroring
    /// the ProgressionOrchestratorStyleTests / ProgressionAuthorityTests patterns.
    /// </summary>
    public class VoiceDevelopmentProgressionTests
    {
        private static readonly DateTime Now = new DateTime(2026, 5, 28, 12, 0, 0);
        private const int ExerciseId = 10;

        // ──────────────────────────────────────────────────────────────────────────
        // 1. The four new ProgressionAdjustmentDimension members exist and were
        //    APPENDED last (existing values keep their ordinal — backward compat).
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Enum_HasNewVoiceDevelopmentDimensions()
        {
            // The four new dimensions are defined.
            Assert.True(Enum.IsDefined(typeof(ProgressionAdjustmentDimension), ProgressionAdjustmentDimension.Comfort));
            Assert.True(Enum.IsDefined(typeof(ProgressionAdjustmentDimension), ProgressionAdjustmentDimension.Consistency));
            Assert.True(Enum.IsDefined(typeof(ProgressionAdjustmentDimension), ProgressionAdjustmentDimension.Intonation));
            Assert.True(Enum.IsDefined(typeof(ProgressionAdjustmentDimension), ProgressionAdjustmentDimension.VocalWeight));
        }

        [Fact]
        public void Enum_ExistingValuesKeepTheirOrdinals_BackwardCompatible()
        {
            // The pre-existing members MUST keep their original integer values so any
            // persisted/serialized (int)-cast keeps its meaning.
            Assert.Equal(0, (int)ProgressionAdjustmentDimension.None);
            Assert.Equal(1, (int)ProgressionAdjustmentDimension.Resonance);
            Assert.Equal(2, (int)ProgressionAdjustmentDimension.Stability);
            Assert.Equal(3, (int)ProgressionAdjustmentDimension.HoldLength);
            Assert.Equal(4, (int)ProgressionAdjustmentDimension.PitchComfort);
            Assert.Equal(5, (int)ProgressionAdjustmentDimension.Recovery);
            Assert.Equal(6, (int)ProgressionAdjustmentDimension.ExerciseVariation);
            // The new ones come strictly after the highest pre-existing ordinal.
            Assert.True((int)ProgressionAdjustmentDimension.Comfort > 6);
            Assert.True((int)ProgressionAdjustmentDimension.Consistency > 6);
            Assert.True((int)ProgressionAdjustmentDimension.Intonation > 6);
            Assert.True((int)ProgressionAdjustmentDimension.VocalWeight > 6);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 2. null Voice ⇒ pre-redesign behaviour is byte-identical. We drive the
        //    all-gates-passed branch and assert the decision matches the legacy
        //    profile-primary-dimension outcome exactly.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task NullVoice_AllGatesPassed_ProducesLegacyResonanceProgression()
        {
            var (orchestrator, store) = NewStack();
            await SeedAllGatesPassedSeries(store);

            var decision = await orchestrator.EvaluateAsync(
                Context(ExerciseTargetProfile.CreateResonanceHumming(), voice: null));

            // Pure resonance profile, all gates passed, no Voice ⇒ legacy RESONANCE_PROGRESSION.
            Assert.Equal(ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated, decision.Kind);
            Assert.Equal(ProgressionAdjustmentDimension.Resonance, decision.Dimension);
            Assert.Equal("RESONANCE_PROGRESSION", decision.ReasonCode);
        }

        [Fact]
        public async Task NullVoice_DecisionIsByteIdenticalToOmittingVoiceEntirely()
        {
            // Two independent runs over identical seeds: one with an explicit null Voice,
            // one with the field never set. They must be indistinguishable.
            var (o1, s1) = NewStack();
            await SeedAllGatesPassedSeries(s1);
            var withExplicitNull = await o1.EvaluateAsync(new ProgressionOrchestratorContext
            {
                ExerciseId = ExerciseId,
                CurrentProfile = ExerciseTargetProfile.CreateResonanceHumming(),
                EvaluationTime = Now,
                Voice = null
            });

            var (o2, s2) = NewStack();
            await SeedAllGatesPassedSeries(s2);
            var withDefault = await o2.EvaluateAsync(new ProgressionOrchestratorContext
            {
                ExerciseId = ExerciseId,
                CurrentProfile = ExerciseTargetProfile.CreateResonanceHumming(),
                EvaluationTime = Now
            });

            Assert.Equal(withDefault.Kind, withExplicitNull.Kind);
            Assert.Equal(withDefault.Dimension, withExplicitNull.Dimension);
            Assert.Equal(withDefault.ReasonCode, withExplicitNull.ReasonCode);
            Assert.Equal(
                withDefault.SuggestedProfile!.TargetResonanceMin,
                withExplicitNull.SuggestedProfile!.TargetResonanceMin, 6);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 3. The weakest Voice dimension drives the development focus, in clinical
        //    hierarchy order. One test per dimension being uniquely lowest.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task WeakestComfort_DrivesComfortFocus()
        {
            var decision = await EvaluateWithVoice(VoiceWith(
                comfort: 20, recovery: 80, resonance: 80, consistency: 80,
                intonation: 80, vocalWeight: 80, pitch: 80));

            Assert.Equal(ProgressionAdjustmentDimension.Comfort, decision.Dimension);
            Assert.Equal("VOICE_DEV_COMFORT", decision.ReasonCode);
        }

        [Fact]
        public async Task WeakestRecovery_DrivesRecoveryFocus_AndScalesDown()
        {
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var decision = await EvaluateWithVoice(VoiceWith(
                comfort: 80, recovery: 20, resonance: 80, consistency: 80,
                intonation: 80, vocalWeight: 80, pitch: 80), profile);

            Assert.Equal(ProgressionAdjustmentDimension.Recovery, decision.Dimension);
            Assert.Equal("VOICE_DEV_RECOVERY", decision.ReasonCode);
            // Recovery focus de-loads: the suggested resonance floor is <= the base
            // (ScaleForRecovery shrinks; never grows requirements).
            Assert.True(decision.SuggestedProfile!.TargetResonanceMin <= profile.TargetResonanceMin);
        }

        [Fact]
        public async Task WeakestResonance_DrivesResonanceFocus()
        {
            var decision = await EvaluateWithVoice(VoiceWith(
                comfort: 80, recovery: 80, resonance: 20, consistency: 80,
                intonation: 80, vocalWeight: 80, pitch: 80));

            Assert.Equal(ProgressionAdjustmentDimension.Resonance, decision.Dimension);
            Assert.Equal("VOICE_DEV_RESONANCE", decision.ReasonCode);
        }

        [Fact]
        public async Task WeakestConsistency_DrivesConsistencyFocus()
        {
            var decision = await EvaluateWithVoice(VoiceWith(
                comfort: 80, recovery: 80, resonance: 80, consistency: 20,
                intonation: 80, vocalWeight: 80, pitch: 80));

            Assert.Equal(ProgressionAdjustmentDimension.Consistency, decision.Dimension);
            Assert.Equal("VOICE_DEV_CONSISTENCY", decision.ReasonCode);
        }

        [Fact]
        public async Task WeakestIntonation_DrivesIntonationFocus()
        {
            var decision = await EvaluateWithVoice(VoiceWith(
                comfort: 80, recovery: 80, resonance: 80, consistency: 80,
                intonation: 20, vocalWeight: 80, pitch: 80));

            Assert.Equal(ProgressionAdjustmentDimension.Intonation, decision.Dimension);
            Assert.Equal("VOICE_DEV_INTONATION", decision.ReasonCode);
        }

        [Fact]
        public async Task WeakestVocalWeight_DrivesVocalWeightFocus()
        {
            var decision = await EvaluateWithVoice(VoiceWith(
                comfort: 80, recovery: 80, resonance: 80, consistency: 80,
                intonation: 80, vocalWeight: 20, pitch: 80));

            Assert.Equal(ProgressionAdjustmentDimension.VocalWeight, decision.Dimension);
            Assert.Equal("VOICE_DEV_VOCAL_WEIGHT", decision.ReasonCode);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 4. Pitch is NEVER the sole/primary driver: it only takes focus when it is
        //    STRICTLY the lowest, and even a tie at the bottom hands focus to a
        //    higher-hierarchy dimension instead.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task PitchOnlyDrivesFocus_WhenStrictlyLowest()
        {
            var decision = await EvaluateWithVoice(VoiceWith(
                comfort: 80, recovery: 80, resonance: 80, consistency: 80,
                intonation: 80, vocalWeight: 80, pitch: 20));

            // Pitch wins focus ONLY because every other dimension is stronger.
            Assert.Equal(ProgressionAdjustmentDimension.PitchComfort, decision.Dimension);
            Assert.Equal("VOICE_DEV_PITCH", decision.ReasonCode);
        }

        [Fact]
        public async Task PitchTiedAtBottom_YieldsFocusToHigherHierarchyDimension()
        {
            // Comfort and Pitch are both lowest (20). Hierarchy breaks the tie: the
            // clinically-higher Comfort takes focus, never Pitch.
            var decision = await EvaluateWithVoice(VoiceWith(
                comfort: 20, recovery: 80, resonance: 80, consistency: 80,
                intonation: 80, vocalWeight: 80, pitch: 20));

            Assert.Equal(ProgressionAdjustmentDimension.Comfort, decision.Dimension);
            Assert.NotEqual(ProgressionAdjustmentDimension.PitchComfort, decision.Dimension);
        }

        [Fact]
        public async Task AllDimensionsTied_HierarchyTopWins_ComfortNotPitch()
        {
            // Everything equal ⇒ first-in-hierarchy (Comfort) wins; Pitch never does.
            var decision = await EvaluateWithVoice(VoiceWith(
                comfort: 70, recovery: 70, resonance: 70, consistency: 70,
                intonation: 70, vocalWeight: 70, pitch: 70));

            Assert.Equal(ProgressionAdjustmentDimension.Comfort, decision.Dimension);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 5. Voice can re-route FOCUS but can NEVER lift a recovery/safety branch.
        //    A safety-event regression fires before the Voice branch is even reached,
        //    regardless of how strong the Voice snapshot is (Safety > everything).
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task PerfectVoice_CannotOverrideSafetyEventRegression()
        {
            var (orchestrator, store) = NewStack();
            await SeedAllGatesPassedSeries(store);
            // Two safety freezes this week ⇒ RegressionTriggered("SAFETY_EVENTS").
            await store.RecordHealthEventAsync(SafetyFreeze(101, Now.AddDays(-2)));
            await store.RecordHealthEventAsync(SafetyFreeze(102, Now.AddDays(-1)));

            // An all-100 Voice snapshot — as strong as possible.
            var perfect = VoiceWith(100, 100, 100, 100, 100, 100, 100);
            var decision = await orchestrator.EvaluateAsync(
                Context(ExerciseTargetProfile.CreateResonanceHumming(), voice: perfect));

            // Safety wins: regression, not a Voice-development profile update.
            Assert.Equal(ProgressionOrchestratorDecisionKind.RegressionTriggered, decision.Kind);
            Assert.Equal("SAFETY_EVENTS", decision.ReasonCode);
            Assert.DoesNotContain("VOICE_DEV", decision.ReasonCode);
        }

        [Fact]
        public async Task PerfectVoice_CannotOverrideFatiguePause()
        {
            var (orchestrator, store) = NewStack();
            await SeedAllGatesPassedSeries(store);
            // A fatigue-heavy recent session ⇒ ProgressionPaused("FATIGUE_RISING").
            await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = 500,
                StartedAt = Now.AddDays(-1),
                EndedAt = Now.AddDays(-1).AddMinutes(5),
                ExerciseCount = 1,
                AverageResonance = 0.8,
                AverageStability = 0.8,
                AveragePitchComfort = 0.8,
                AverageHealthScore = 0.8,
                FatigueIndicatorCount = 2
            });

            var perfect = VoiceWith(100, 100, 100, 100, 100, 100, 100);
            var decision = await orchestrator.EvaluateAsync(
                Context(ExerciseTargetProfile.CreateResonanceHumming(), voice: perfect));

            Assert.Equal(ProgressionOrchestratorDecisionKind.ProgressionPaused, decision.Kind);
            Assert.Equal("FATIGUE_RISING", decision.ReasonCode);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 6. ComplexityEngine.CanAdvanceToNextLevel — the new Voice gates are ADDITIVE
        //    AND-conditions: they can only block, never unblock. A voice that clears
        //    every legacy gate is still blocked when comfort/consistency/recovery are
        //    low (Health/Comfort > Progression), even with high resonance/pitch.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void CanAdvance_NullVoice_IsByteIdenticalToLegacy()
        {
            var engine = NewComplexityEngine();
            var eval = LegacyAdvanceableEvaluation();

            // Legacy gate-set fully satisfied ⇒ advanceable with no Voice supplied.
            Assert.True(engine.CanAdvanceToNextLevel(eval));
            Assert.True(engine.CanAdvanceToNextLevel(eval, voice: null));
        }

        [Fact]
        public void CanAdvance_LowComfort_BlocksDespiteHighResonanceAndPitch()
        {
            var engine = NewComplexityEngine();
            var eval = LegacyAdvanceableEvaluation();

            // High resonance + pitch, but comfort is low ⇒ blocked (Comfort > Progression).
            var voice = VoiceWith(comfort: 30, recovery: 90, resonance: 95, consistency: 90,
                intonation: 90, vocalWeight: 90, pitch: 95);

            Assert.True(engine.CanAdvanceToNextLevel(eval));            // legacy view: ready
            Assert.False(engine.CanAdvanceToNextLevel(eval, voice));    // voice view: blocked
        }

        [Fact]
        public void CanAdvance_LowConsistency_BlocksDespiteHighResonanceAndPitch()
        {
            var engine = NewComplexityEngine();
            var eval = LegacyAdvanceableEvaluation();

            var voice = VoiceWith(comfort: 90, recovery: 90, resonance: 95, consistency: 30,
                intonation: 90, vocalWeight: 90, pitch: 95);

            Assert.True(engine.CanAdvanceToNextLevel(eval));
            Assert.False(engine.CanAdvanceToNextLevel(eval, voice));
        }

        [Fact]
        public void CanAdvance_LowRecovery_BlocksDespiteHighResonanceAndPitch_HealthOverProgression()
        {
            var engine = NewComplexityEngine();
            var eval = LegacyAdvanceableEvaluation();

            // The overloaded-voice case: resonance/pitch are excellent but recovery is
            // poor. Health > Progression ⇒ no advancement.
            var voice = VoiceWith(comfort: 90, recovery: 25, resonance: 98, consistency: 90,
                intonation: 90, vocalWeight: 90, pitch: 98);

            Assert.True(engine.CanAdvanceToNextLevel(eval));
            Assert.False(engine.CanAdvanceToNextLevel(eval, voice));
        }

        [Fact]
        public void CanAdvance_AllVoiceDimensionsStrong_StaysAdvanceable_NeverLooserThanLegacy()
        {
            var engine = NewComplexityEngine();
            var eval = LegacyAdvanceableEvaluation();

            // A strong voice does not unblock anything the legacy gates already pass,
            // and it does not block either ⇒ still advanceable.
            var voice = VoiceWith(comfort: 90, recovery: 90, resonance: 90, consistency: 90,
                intonation: 90, vocalWeight: 90, pitch: 90);

            Assert.True(engine.CanAdvanceToNextLevel(eval, voice));
        }

        [Fact]
        public void CanAdvance_StrongVoice_CannotUnblockAFailedLegacyGate()
        {
            // The additive contract in the other direction: a perfect voice can NEVER
            // make a legacy-blocked evaluation advanceable. Voice only tightens.
            var engine = NewComplexityEngine();
            var eval = LegacyAdvanceableEvaluation();
            eval.VoiceHealthScore = 10;   // fail a legacy health gate

            var perfect = VoiceWith(100, 100, 100, 100, 100, 100, 100);

            Assert.False(engine.CanAdvanceToNextLevel(eval));
            Assert.False(engine.CanAdvanceToNextLevel(eval, perfect));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Fixtures
        // ══════════════════════════════════════════════════════════════════════════

        private static (ProgressionOrchestrator, SessionAnalyticsStore) NewStack()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var orchestrator = new ProgressionOrchestrator(store, new ProgressionOrchestratorOptions
            {
                MinimumSessionsForDecision = 3,
                PlateauSessionThreshold = 6,
                MaxSafetyEventsBeforeRegression = 2,
                MaxFatigueIndicatorsBeforePause = 2
            });
            return (orchestrator, store);
        }

        // ComplexityEngine requires a concrete DatabaseService. CanAdvanceToNextLevel is a
        // PURE function of the supplied ComplexityEvaluation (and the optional Voice
        // snapshot) — the temp-file DB only constructs the engine; no row is read from it.
        // Same fixture pattern as SafetyPriorityEngineTests.BuildComplexityEngine.
        private static ComplexityEngine NewComplexityEngine()
        {
            var databasePath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "FemVoiceStudio.Tests",
                $"{Guid.NewGuid():N}.db");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(databasePath)!);
            return new ComplexityEngine(new FemVoiceStudio.Data.DatabaseService(databasePath));
        }

        private static ProgressionOrchestratorContext Context(
            ExerciseTargetProfile profile, VoiceIntelligenceScores? voice)
            => new()
            {
                ExerciseId = ExerciseId,
                CurrentProfile = profile,
                EvaluationTime = Now,
                Voice = voice
            };

        private static async Task<ProgressionOrchestratorDecision> EvaluateWithVoice(
            VoiceIntelligenceScores voice, ExerciseTargetProfile? profile = null)
        {
            var (orchestrator, store) = NewStack();
            await SeedAllGatesPassedSeries(store);
            return await orchestrator.EvaluateAsync(
                Context(profile ?? ExerciseTargetProfile.CreateResonanceHumming(), voice));
        }

        // Resonance comfortably > 0.65, stability/hold above their gates, with
        // consistent improvement over baseline ⇒ all consolidation gates pass and the
        // orchestrator reaches the Voice-development (or legacy primary-dimension) leaf.
        private static async Task SeedAllGatesPassedSeries(SessionAnalyticsStore store)
        {
            await AddExercise(store, 1, Now.AddDays(-6), 0.70, 0.70, 0.85);
            await AddExercise(store, 2, Now.AddDays(-5), 0.71, 0.71, 0.86);
            await AddExercise(store, 3, Now.AddDays(-3), 0.80, 0.78, 0.92);
            await AddExercise(store, 4, Now.AddDays(-2), 0.81, 0.79, 0.93);
            await AddExercise(store, 5, Now.AddDays(-1), 0.82, 0.80, 0.94);
        }

        private static async Task AddExercise(
            SessionAnalyticsStore store, int sessionId, DateTime startedAt,
            double resonance, double stability, double hold)
        {
            await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = sessionId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(5),
                ExerciseCount = 1,
                AverageResonance = resonance,
                AverageStability = stability,
                AveragePitchComfort = 0.8,
                AverageHealthScore = 1.0
            });
            await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = sessionId,
                ExerciseId = ExerciseId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(5),
                ResonanceQualityIndex = resonance,
                StabilityConsistency = stability,
                HoldCompletionRate = hold
            });
        }

        private static HealthAnalyticsEvent SafetyFreeze(int sessionId, DateTime occurredAt)
            => new()
            {
                SessionId = sessionId,
                EventType = HealthAnalyticsEventType.SafetyFreeze,
                OccurredAt = occurredAt,
                Severity = 1,
                ReasonCode = "SAFETY_FREEZE"
            };

        // A ComplexityEvaluation that clears every LEGACY advancement gate, so any
        // block must come from the new additive Voice gates.
        private static ComplexityEvaluation LegacyAdvanceableEvaluation()
            => new()
            {
                CurrentLevel = SpeechComplexityLevel.IsolatedSounds,
                SessionsAtCurrentLevel = 5,
                SuccessRate = 90,
                AverageResonance = 80,
                PitchStability = 80,
                IntonationScore = 70,
                VoiceHealthScore = 90,
                StrainLevel = 10,
                SessionsPerWeek = 4,
                HealthAllowsProgression = true
            };

        // Build a VoiceIntelligenceScores with explicit per-dimension scores; the
        // composite is irrelevant to these tests (only the dimensions drive focus/gates).
        private static VoiceIntelligenceScores VoiceWith(
            double comfort, double recovery, double resonance, double consistency,
            double intonation, double vocalWeight, double pitch)
            => new()
            {
                Comfort = new DimensionScore(comfort, "test"),
                Recovery = new DimensionScore(recovery, "test"),
                Resonance = new DimensionScore(resonance, "test"),
                Consistency = new DimensionScore(consistency, "test"),
                Intonation = new DimensionScore(intonation, "test"),
                VocalWeight = new DimensionScore(vocalWeight, "test"),
                Pitch = new DimensionScore(pitch, "test"),
                CompositeVoiceScore = 0
            };
    }
}
