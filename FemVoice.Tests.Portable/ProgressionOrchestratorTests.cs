using System;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class ProgressionOrchestratorTests
    {
        [Fact]
        public async Task Evaluate_IncreasesDifficultyAfterStableImprovement()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            await AddExercise(store, 1, 10, now.AddDays(-6), resonance: 0.55, stability: 0.55, hold: 0.60);
            await AddExercise(store, 2, 10, now.AddDays(-5), resonance: 0.56, stability: 0.56, hold: 0.62);
            await AddExercise(store, 3, 10, now.AddDays(-3), resonance: 0.72, stability: 0.70, hold: 0.78);
            await AddExercise(store, 4, 10, now.AddDays(-2), resonance: 0.73, stability: 0.71, hold: 0.79);
            await AddExercise(store, 5, 10, now.AddDays(-1), resonance: 0.74, stability: 0.72, hold: 0.80);

            var decision = await orchestrator.EvaluateAsync(Context(10, profile, now));

            Assert.Equal(ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated, decision.Kind);
            Assert.Equal(ProgressionAdjustmentDimension.Resonance, decision.Dimension);
            Assert.True(decision.SuggestedProfile!.TargetResonanceMin > profile.TargetResonanceMin);
        }

        [Fact]
        public async Task Evaluate_DoesNotIncreaseDifficultyFromSingleSpike()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            await AddExercise(store, 1, 10, now.AddDays(-5), resonance: 0.55, stability: 0.55, hold: 0.60);
            await AddExercise(store, 2, 10, now.AddDays(-4), resonance: 0.56, stability: 0.56, hold: 0.62);
            await AddExercise(store, 3, 10, now.AddDays(-3), resonance: 0.57, stability: 0.56, hold: 0.62);
            await AddExercise(store, 4, 10, now.AddDays(-2), resonance: 0.58, stability: 0.57, hold: 0.63);
            await AddExercise(store, 5, 10, now.AddDays(-1), resonance: 0.90, stability: 0.90, hold: 0.95);

            var decision = await orchestrator.EvaluateAsync(Context(10, profile, now));

            Assert.Equal(ProgressionOrchestratorDecisionKind.Maintain, decision.Kind);
            Assert.Equal("NO_CONSISTENT_IMPROVEMENT", decision.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_DoesNotIncreaseWhenFatigueRises()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);

            await AddImprovingSeries(store, exerciseId: 10, now);
            await store.RecordSessionCompletedAsync(Session(100, now.AddDays(-1), fatigue: 2));

            var decision = await orchestrator.EvaluateAsync(Context(10, ExerciseTargetProfile.CreateResonanceHumming(), now));

            Assert.Equal(ProgressionOrchestratorDecisionKind.ProgressionPaused, decision.Kind);
            Assert.Equal("FATIGUE_RISING", decision.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_TriggersRegressionWhenSafetyEventsAccumulate()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            await AddImprovingSeries(store, exerciseId: 10, now);
            await store.RecordHealthEventAsync(HealthEvent(1, now.AddDays(-2)));
            await store.RecordHealthEventAsync(HealthEvent(2, now.AddDays(-1)));

            var decision = await orchestrator.EvaluateAsync(Context(10, profile, now));

            Assert.Equal(ProgressionOrchestratorDecisionKind.RegressionTriggered, decision.Kind);
            Assert.Equal("SAFETY_EVENTS", decision.ReasonCode);
            Assert.True(decision.SuggestedProfile!.RequiredHoldSeconds < profile.RequiredHoldSeconds);
        }

        [Fact]
        public async Task Evaluate_DetectsPlateauAcrossStableWindow()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);

            for (var i = 0; i < 6; i++)
            {
                await AddExercise(store, i + 1, 10, now.AddDays(-6 + i), resonance: 0.70, stability: 0.69, hold: 0.75);
            }

            var decision = await orchestrator.EvaluateAsync(Context(10, ExerciseTargetProfile.CreateResonanceHumming(), now));

            Assert.Equal(ProgressionOrchestratorDecisionKind.PlateauDetected, decision.Kind);
            Assert.Equal("PLATEAU_DETECTED", decision.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_TriggersRegressionWhenRecentPerformanceDrops()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);

            await AddExercise(store, 1, 10, now.AddDays(-6), resonance: 0.78, stability: 0.76, hold: 0.85);
            await AddExercise(store, 2, 10, now.AddDays(-5), resonance: 0.77, stability: 0.75, hold: 0.84);
            await AddExercise(store, 3, 10, now.AddDays(-4), resonance: 0.76, stability: 0.74, hold: 0.83);
            await AddExercise(store, 4, 10, now.AddDays(-3), resonance: 0.55, stability: 0.54, hold: 0.60);
            await AddExercise(store, 5, 10, now.AddDays(-2), resonance: 0.54, stability: 0.53, hold: 0.59);
            await AddExercise(store, 6, 10, now.AddDays(-1), resonance: 0.53, stability: 0.52, hold: 0.58);

            var decision = await orchestrator.EvaluateAsync(Context(10, ExerciseTargetProfile.CreateResonanceHumming(), now));

            Assert.Equal(ProgressionOrchestratorDecisionKind.RegressionTriggered, decision.Kind);
            Assert.Equal("PERFORMANCE_REGRESSION", decision.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_PrioritizesResonanceBeforePitch()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var pitchProfile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 220);

            await AddExercise(store, 1, 10, now.AddDays(-6), resonance: 0.52, stability: 0.62, hold: 0.75);
            await AddExercise(store, 2, 10, now.AddDays(-5), resonance: 0.53, stability: 0.63, hold: 0.76);
            await AddExercise(store, 3, 10, now.AddDays(-3), resonance: 0.60, stability: 0.72, hold: 0.85);
            await AddExercise(store, 4, 10, now.AddDays(-2), resonance: 0.61, stability: 0.73, hold: 0.86);
            await AddExercise(store, 5, 10, now.AddDays(-1), resonance: 0.62, stability: 0.74, hold: 0.87);

            var decision = await orchestrator.EvaluateAsync(Context(10, pitchProfile, now));

            Assert.Equal(ProgressionAdjustmentDimension.Resonance, decision.Dimension);
            Assert.Equal(160, decision.SuggestedProfile!.MinPitch);
            Assert.Equal(220, decision.SuggestedProfile.MaxPitch);
            Assert.True(decision.SuggestedProfile.TargetResonanceMin > pitchProfile.TargetResonanceMin);
        }

        [Fact]
        public async Task Evaluate_SuggestsExerciseVariationWhenOneExerciseDominates()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            for (var i = 0; i < 8; i++)
            {
                await AddExercise(
                    store,
                    sessionId: i + 1,
                    exerciseId: 10,
                    startedAt: now.AddDays(-8 + i),
                    resonance: 0.78,
                    stability: 0.76,
                    hold: 0.84);
            }

            var decision = await orchestrator.EvaluateAsync(Context(10, profile, now));

            Assert.Equal(ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated, decision.Kind);
            Assert.Equal(ProgressionAdjustmentDimension.ExerciseVariation, decision.Dimension);
            Assert.Equal("EXERCISE_VARIATION_RECOMMENDED", decision.ReasonCode);
            Assert.True(decision.SuggestedProfile!.UsesIntensity);
        }

        [Fact]
        public async Task Evaluate_DoesNotSuggestVariationWhenRecentHistoryIsMixed()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            for (var i = 0; i < 8; i++)
            {
                await AddExercise(
                    store,
                    sessionId: i + 1,
                    exerciseId: i % 2 == 0 ? 10 : 11,
                    startedAt: now.AddDays(-8 + i),
                    resonance: 0.78,
                    stability: 0.76,
                    hold: 0.84);
            }

            var decision = await orchestrator.EvaluateAsync(Context(10, profile, now));

            Assert.NotEqual(ProgressionAdjustmentDimension.ExerciseVariation, decision.Dimension);
        }

        [Fact]
        public async Task Evaluate_PausesProgressionWhenSubjectiveReportIndicatesHealthConcern()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);

            var decision = await orchestrator.EvaluateAsync(Context(
                10,
                ExerciseTargetProfile.CreateResonanceHumming(),
                now,
                new SubjectiveReport
                {
                    ComfortLevel = 2,
                    FatigueFeeling = 4,
                    ExperiencedStrain = true,
                    WantsToContinue = true
                }));

            Assert.Equal(ProgressionOrchestratorDecisionKind.ProgressionPaused, decision.Kind);
            Assert.Equal("SUBJECTIVE_HEALTH_CONCERN", decision.ReasonCode);
            Assert.Equal(ProgressionAdjustmentDimension.Recovery, decision.Dimension);
        }

        [Fact]
        public async Task Evaluate_PausesProgressionWhenMotivationDrops()
        {
            var store = CreateStore();
            var orchestrator = CreateOrchestrator(store);
            var now = new DateTime(2026, 5, 28, 12, 0, 0);

            var decision = await orchestrator.EvaluateAsync(Context(
                10,
                ExerciseTargetProfile.CreateResonanceHumming(),
                now,
                new SubjectiveReport
                {
                    ComfortLevel = 5,
                    FatigueFeeling = 1,
                    ExperiencedStrain = false,
                    WantsToContinue = false
                }));

            Assert.Equal(ProgressionOrchestratorDecisionKind.ProgressionPaused, decision.Kind);
            Assert.Equal("MOTIVATION_DROPPING", decision.ReasonCode);
            Assert.Equal(ProgressionAdjustmentDimension.Recovery, decision.Dimension);
        }

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
            SubjectiveReport? subjectiveReport = null)
            => new()
            {
                ExerciseId = exerciseId,
                CurrentProfile = profile,
                EvaluationTime = evaluationTime,
                SubjectiveReport = subjectiveReport
            };

        private static async Task AddImprovingSeries(SessionAnalyticsStore store, int exerciseId, DateTime now)
        {
            await AddExercise(store, 1, exerciseId, now.AddDays(-6), resonance: 0.55, stability: 0.55, hold: 0.60);
            await AddExercise(store, 2, exerciseId, now.AddDays(-5), resonance: 0.56, stability: 0.56, hold: 0.62);
            await AddExercise(store, 3, exerciseId, now.AddDays(-3), resonance: 0.72, stability: 0.70, hold: 0.78);
            await AddExercise(store, 4, exerciseId, now.AddDays(-2), resonance: 0.73, stability: 0.71, hold: 0.79);
            await AddExercise(store, 5, exerciseId, now.AddDays(-1), resonance: 0.74, stability: 0.72, hold: 0.80);
        }

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

        private static SessionAnalyticsRecord Session(int sessionId, DateTime startedAt, int fatigue)
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
