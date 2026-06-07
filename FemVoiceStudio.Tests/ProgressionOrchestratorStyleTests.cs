using System;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Style-aware progression tests (Agent 2 — Voice Goal Integration).
    ///
    /// Verifies that <see cref="ProgressionOrchestratorContext.PreferredVoiceStyle"/>
    /// dampens upward resonance scaling for styles where a brighter/forward resonance
    /// target is not the user's goal (DarkFeminine strongest, Androgynous mild), while:
    ///   - null style reproduces the pre-existing (style-neutral) behaviour exactly, and
    ///   - the safety/recovery branches are NEVER influenced by style (safety > style).
    /// </summary>
    public class ProgressionOrchestratorStyleTests
    {
        // ------------------------------------------------------------------
        // Null style == pre-change behaviour (regression guard for all
        // existing ProgressionOrchestratorTests, which pass no style).
        // ------------------------------------------------------------------

        [Fact]
        public async Task ResonanceFirst_NullStyle_MatchesStyleNeutralUpscale()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            await AddResonanceFirstImprovingSeries(store, exerciseId: 10, now);

            var decision = await orchestrator.EvaluateAsync(Context(10, profile, now, style: null));

            Assert.Equal(ProgressionAdjustmentDimension.Resonance, decision.Dimension);
            Assert.Equal("RESONANCE_FIRST", decision.ReasonCode);
            // Style-neutral delta is +0.02 on the lower resonance target.
            Assert.Equal(profile.TargetResonanceMin + 0.02, decision.SuggestedProfile!.TargetResonanceMin, 6);
        }

        [Fact]
        public async Task ResonanceFirst_FeminineStyle_IdenticalToNullStyle()
        {
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            var neutral = await EvaluateResonanceFirst(now, profile, style: null);
            var feminine = await EvaluateResonanceFirst(now, profile, style: VoiceStyleGoal.Feminine);

            // Feminine is the default feminization target — no damping.
            Assert.Equal(neutral.SuggestedProfile!.TargetResonanceMin, feminine.SuggestedProfile!.TargetResonanceMin, 6);
            Assert.Equal(neutral.SuggestedProfile.TargetResonanceMax, feminine.SuggestedProfile.TargetResonanceMax, 6);
        }

        [Theory]
        [InlineData(VoiceStyleGoal.Situational)]
        [InlineData(VoiceStyleGoal.Custom)]
        public async Task ResonanceFirst_UndampedStyles_IdenticalToNullStyle(VoiceStyleGoal style)
        {
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            var neutral = await EvaluateResonanceFirst(now, profile, style: null);
            var styled = await EvaluateResonanceFirst(now, profile, style: style);

            Assert.Equal(neutral.SuggestedProfile!.TargetResonanceMin, styled.SuggestedProfile!.TargetResonanceMin, 6);
        }

        // ------------------------------------------------------------------
        // DarkFeminine / Androgynous dampen upward resonance scaling.
        // ------------------------------------------------------------------

        [Fact]
        public async Task ResonanceFirst_DarkFeminine_HalvesUpwardResonanceDelta()
        {
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            var neutral = await EvaluateResonanceFirst(now, profile, style: null);
            var dark = await EvaluateResonanceFirst(now, profile, style: VoiceStyleGoal.DarkFeminine);

            // Delta halved: dark resonance min rises only half as far as the neutral one.
            Assert.Equal(profile.TargetResonanceMin + 0.01, dark.SuggestedProfile!.TargetResonanceMin, 6);

            // It still rises (resonance is consolidated, not abandoned) but less than neutral.
            Assert.True(dark.SuggestedProfile.TargetResonanceMin > profile.TargetResonanceMin);
            Assert.True(dark.SuggestedProfile.TargetResonanceMin < neutral.SuggestedProfile!.TargetResonanceMin);
        }

        [Fact]
        public async Task ResonanceFirst_Androgynous_MildlyDampensDeltaBetweenNeutralAndDark()
        {
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            var neutral = await EvaluateResonanceFirst(now, profile, style: null);
            var androgynous = await EvaluateResonanceFirst(now, profile, style: VoiceStyleGoal.Androgynous);
            var dark = await EvaluateResonanceFirst(now, profile, style: VoiceStyleGoal.DarkFeminine);

            // 0.75 factor → +0.015 on the lower resonance target.
            Assert.Equal(profile.TargetResonanceMin + 0.015, androgynous.SuggestedProfile!.TargetResonanceMin, 6);

            // Ordering: dark < androgynous < neutral.
            Assert.True(androgynous.SuggestedProfile.TargetResonanceMin < neutral.SuggestedProfile!.TargetResonanceMin);
            Assert.True(androgynous.SuggestedProfile.TargetResonanceMin > dark.SuggestedProfile!.TargetResonanceMin);
        }

        [Fact]
        public async Task ResonanceProgression_DarkFeminine_DampensVsNeutral()
        {
            // Drive the RESONANCE_PROGRESSION branch: resonance already stable (>0.65),
            // stability/hold gates passed, pure resonance profile → resonance advances.
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            var neutral = await EvaluateResonanceProgression(now, profile, style: null);
            var dark = await EvaluateResonanceProgression(now, profile, style: VoiceStyleGoal.DarkFeminine);

            Assert.Equal("RESONANCE_PROGRESSION", neutral.ReasonCode);
            Assert.Equal("RESONANCE_PROGRESSION", dark.ReasonCode);
            Assert.True(dark.SuggestedProfile!.TargetResonanceMin < neutral.SuggestedProfile!.TargetResonanceMin);
            Assert.True(dark.SuggestedProfile.TargetResonanceMin > profile.TargetResonanceMin);
        }

        [Fact]
        public async Task Plateau_DarkFeminine_DampensResonanceElevationVsNeutral()
        {
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            var neutral = await EvaluatePlateau(now, profile, style: null);
            var dark = await EvaluatePlateau(now, profile, style: VoiceStyleGoal.DarkFeminine);

            Assert.Equal(ProgressionOrchestratorDecisionKind.PlateauDetected, neutral.Kind);
            Assert.Equal(ProgressionOrchestratorDecisionKind.PlateauDetected, dark.Kind);
            // Plateau-break raises the resonance target; damping applies to that elevation too.
            Assert.True(dark.SuggestedProfile!.TargetResonanceMin < neutral.SuggestedProfile!.TargetResonanceMin);
        }

        // ------------------------------------------------------------------
        // Safety / recovery branches are STYLE-INVARIANT (safety > style).
        // ------------------------------------------------------------------

        [Fact]
        public async Task SafetyEvents_RegressionProfileIsIdenticalRegardlessOfStyle()
        {
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            var neutral = await EvaluateSafetyEvents(now, profile, style: null);
            var dark = await EvaluateSafetyEvents(now, profile, style: VoiceStyleGoal.DarkFeminine);

            Assert.Equal(ProgressionOrchestratorDecisionKind.RegressionTriggered, neutral.Kind);
            Assert.Equal("SAFETY_EVENTS", neutral.ReasonCode);
            Assert.Equal("SAFETY_EVENTS", dark.ReasonCode);
            // Recovery scaling lowers resonance — must be identical no matter the style.
            Assert.Equal(neutral.SuggestedProfile!.TargetResonanceMin, dark.SuggestedProfile!.TargetResonanceMin, 6);
            Assert.Equal(neutral.SuggestedProfile.StabilityThreshold, dark.SuggestedProfile.StabilityThreshold, 6);
            Assert.Equal(neutral.SuggestedProfile.RequiredHoldSeconds, dark.SuggestedProfile.RequiredHoldSeconds, 6);
        }

        [Fact]
        public async Task FatigueRising_PauseIsIdenticalRegardlessOfStyle()
        {
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            var neutral = await EvaluateFatigue(now, profile, style: null);
            var dark = await EvaluateFatigue(now, profile, style: VoiceStyleGoal.DarkFeminine);

            Assert.Equal(ProgressionOrchestratorDecisionKind.ProgressionPaused, neutral.Kind);
            Assert.Equal("FATIGUE_RISING", neutral.ReasonCode);
            Assert.Equal("FATIGUE_RISING", dark.ReasonCode);
            // Pause keeps the current profile untouched regardless of style.
            Assert.Equal(neutral.SuggestedProfile!.TargetResonanceMin, dark.SuggestedProfile!.TargetResonanceMin, 6);
        }

        [Fact]
        public async Task PerformanceRegression_IsIdenticalRegardlessOfStyle()
        {
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            var neutral = await EvaluatePerformanceRegression(now, profile, style: null);
            var dark = await EvaluatePerformanceRegression(now, profile, style: VoiceStyleGoal.DarkFeminine);

            Assert.Equal(ProgressionOrchestratorDecisionKind.RegressionTriggered, neutral.Kind);
            Assert.Equal("PERFORMANCE_REGRESSION", neutral.ReasonCode);
            Assert.Equal("PERFORMANCE_REGRESSION", dark.ReasonCode);
            Assert.Equal(neutral.SuggestedProfile!.TargetResonanceMin, dark.SuggestedProfile!.TargetResonanceMin, 6);
        }

        // ==================================================================
        // Scenario builders
        // ==================================================================

        private static async Task<ProgressionOrchestratorDecision> EvaluateResonanceFirst(
            DateTime now, ExerciseTargetProfile profile, VoiceStyleGoal? style)
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            await AddResonanceFirstImprovingSeries(store, exerciseId: 10, now);
            return await orchestrator.EvaluateAsync(Context(10, profile, now, style));
        }

        private static async Task<ProgressionOrchestratorDecision> EvaluateResonanceProgression(
            DateTime now, ExerciseTargetProfile profile, VoiceStyleGoal? style)
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            // Resonance comfortably above 0.65, stability/hold above their gates,
            // with consistent improvement over baseline → RESONANCE_PROGRESSION.
            await AddExercise(store, 1, 10, now.AddDays(-6), resonance: 0.70, stability: 0.70, hold: 0.85);
            await AddExercise(store, 2, 10, now.AddDays(-5), resonance: 0.71, stability: 0.71, hold: 0.86);
            await AddExercise(store, 3, 10, now.AddDays(-3), resonance: 0.80, stability: 0.78, hold: 0.92);
            await AddExercise(store, 4, 10, now.AddDays(-2), resonance: 0.81, stability: 0.79, hold: 0.93);
            await AddExercise(store, 5, 10, now.AddDays(-1), resonance: 0.82, stability: 0.80, hold: 0.94);
            return await orchestrator.EvaluateAsync(Context(10, profile, now, style));
        }

        private static async Task<ProgressionOrchestratorDecision> EvaluatePlateau(
            DateTime now, ExerciseTargetProfile profile, VoiceStyleGoal? style)
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            for (var i = 0; i < 6; i++)
            {
                await AddExercise(store, i + 1, 10, now.AddDays(-6 + i), resonance: 0.70, stability: 0.69, hold: 0.75);
            }
            return await orchestrator.EvaluateAsync(Context(10, profile, now, style));
        }

        private static async Task<ProgressionOrchestratorDecision> EvaluateSafetyEvents(
            DateTime now, ExerciseTargetProfile profile, VoiceStyleGoal? style)
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            await AddResonanceFirstImprovingSeries(store, exerciseId: 10, now);
            await store.RecordHealthEventAsync(HealthEvent(101, now.AddDays(-2)));
            await store.RecordHealthEventAsync(HealthEvent(102, now.AddDays(-1)));
            return await orchestrator.EvaluateAsync(Context(10, profile, now, style));
        }

        private static async Task<ProgressionOrchestratorDecision> EvaluateFatigue(
            DateTime now, ExerciseTargetProfile profile, VoiceStyleGoal? style)
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            await AddResonanceFirstImprovingSeries(store, exerciseId: 10, now);
            await store.RecordSessionCompletedAsync(FatigueSession(200, now.AddDays(-1), fatigue: 2));
            return await orchestrator.EvaluateAsync(Context(10, profile, now, style));
        }

        private static async Task<ProgressionOrchestratorDecision> EvaluatePerformanceRegression(
            DateTime now, ExerciseTargetProfile profile, VoiceStyleGoal? style)
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            await AddExercise(store, 1, 10, now.AddDays(-6), resonance: 0.78, stability: 0.76, hold: 0.85);
            await AddExercise(store, 2, 10, now.AddDays(-5), resonance: 0.77, stability: 0.75, hold: 0.84);
            await AddExercise(store, 3, 10, now.AddDays(-4), resonance: 0.76, stability: 0.74, hold: 0.83);
            await AddExercise(store, 4, 10, now.AddDays(-3), resonance: 0.55, stability: 0.54, hold: 0.60);
            await AddExercise(store, 5, 10, now.AddDays(-2), resonance: 0.54, stability: 0.53, hold: 0.59);
            await AddExercise(store, 6, 10, now.AddDays(-1), resonance: 0.53, stability: 0.52, hold: 0.58);
            return await orchestrator.EvaluateAsync(Context(10, profile, now, style));
        }

        // RESONANCE_FIRST: consistent improvement over baseline, but recent resonance
        // still under the 0.65 stability gate so resonance is consolidated first.
        private static async Task AddResonanceFirstImprovingSeries(
            SessionAnalyticsStore store, int exerciseId, DateTime now)
        {
            await AddExercise(store, 1, exerciseId, now.AddDays(-6), resonance: 0.52, stability: 0.62, hold: 0.75);
            await AddExercise(store, 2, exerciseId, now.AddDays(-5), resonance: 0.53, stability: 0.63, hold: 0.76);
            await AddExercise(store, 3, exerciseId, now.AddDays(-3), resonance: 0.60, stability: 0.72, hold: 0.85);
            await AddExercise(store, 4, exerciseId, now.AddDays(-2), resonance: 0.61, stability: 0.73, hold: 0.86);
            await AddExercise(store, 5, exerciseId, now.AddDays(-1), resonance: 0.62, stability: 0.74, hold: 0.87);
        }

        // ==================================================================
        // Shared fixtures (mirrors ProgressionOrchestratorTests)
        // ==================================================================

        private static SessionAnalyticsStore CreateStore()
            => new(new InMemorySessionAnalyticsRepository());

        private static ProgressionOrchestrator CreateOrchestrator(SessionAnalyticsStore store)
            => new(store, new ProgressionOrchestratorOptions
            {
                MinimumSessionsForDecision = 3,
                PlateauSessionThreshold = 6,
                MaxSafetyEventsBeforeRegression = 2,
                MaxFatigueIndicatorsBeforePause = 2
            });

        private static ProgressionOrchestratorContext Context(
            int exerciseId,
            ExerciseTargetProfile profile,
            DateTime evaluationTime,
            VoiceStyleGoal? style)
            => new()
            {
                ExerciseId = exerciseId,
                CurrentProfile = profile,
                EvaluationTime = evaluationTime,
                PreferredVoiceStyle = style
            };

        private static async Task AddExercise(
            SessionAnalyticsStore store,
            int sessionId,
            int exerciseId,
            DateTime startedAt,
            double resonance,
            double stability,
            double hold)
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
                ExerciseId = exerciseId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(5),
                ResonanceQualityIndex = resonance,
                StabilityConsistency = stability,
                HoldCompletionRate = hold
            });
        }

        private static SessionAnalyticsRecord FatigueSession(int sessionId, DateTime startedAt, int fatigue)
            => new()
            {
                SessionId = sessionId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(5),
                ExerciseCount = 1,
                AverageResonance = 0.7,
                AverageStability = 0.7,
                AveragePitchComfort = 0.8,
                AverageHealthScore = 0.8,
                FatigueIndicatorCount = fatigue
            };

        private static HealthAnalyticsEvent HealthEvent(int sessionId, DateTime occurredAt)
            => new()
            {
                SessionId = sessionId,
                EventType = HealthAnalyticsEventType.SafetyFreeze,
                OccurredAt = occurredAt,
                Severity = 1,
                ReasonCode = "SAFETY_FREEZE"
            };
    }
}
