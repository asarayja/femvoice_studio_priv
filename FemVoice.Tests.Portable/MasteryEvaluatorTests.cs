using System;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="MasteryEvaluator"/>.
    /// Verifies the clinical gates: session-count floor, recent safety-lock demotion,
    /// thin-history conservatism, the 14-day / comfort-breach / fatigue gates that keep
    /// an otherwise-eligible exercise at Stable, and the resonance threshold.
    /// All evaluations use a fixed <c>now</c> — never DateTime.Now.
    /// </summary>
    public class MasteryEvaluatorTests
    {
        private static readonly DateTime Now = new DateTime(2026, 6, 6, 12, 0, 0);
        private const int ExerciseId = 10;

        // ── 1. Beginner: fewer than 3 completed sessions ─────────────────────────
        [Fact]
        public async Task Evaluate_WithFewerThanThreeSessions_ReturnsBeginner()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);

            var result = await evaluator.EvaluateAsync(
                ExerciseId, totalSessions: 2, ExerciseTargetProfile.CreateResonanceHumming(), Now);

            Assert.Equal(MasteryLevel.Beginner, result.Level);
            Assert.Equal("SESSION_COUNT", result.ReasonCode);
        }

        // ── 2. Recent safety lock overrides everything, even with perfect history ──
        [Fact]
        public async Task Evaluate_WithRecentSafetyLock_DemotesToDeveloping()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            // 25 strong sessions, but the most recent one carries a safety event (3 days ago).
            await SeedStrongSummaries(store, count: 25, lastStart: Now.AddDays(-1));
            await AddSummary(store, sessionId: 999, startedAt: Now.AddDays(-3),
                resonance: 0.70, stability: 0.65, safetyEvents: 1);

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 25, profile, Now);

            Assert.Equal(MasteryLevel.Developing, result.Level);
            Assert.Equal("GATE_SAFETY_RECENT_LOCK", result.ReasonCode);
        }

        // ── 3. Thin analytics history can never reach Mastered ───────────────────
        [Fact]
        public async Task Evaluate_WithTooFewSummaries_ReturnsDevelopingInsufficientHistory()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            // totalSessions claims 25, but only 4 analytics summaries exist (< 5 required).
            await SeedStrongSummaries(store, count: 4, lastStart: Now.AddDays(-1));

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 25, profile, Now);

            Assert.Equal(MasteryLevel.Developing, result.Level);
            Assert.Equal("INSUFFICIENT_HISTORY", result.ReasonCode);
        }

        // ── 4. All gates passed → Mastered ───────────────────────────────────────
        [Fact]
        public async Task Evaluate_WithAllGatesPassed_ReturnsMastered()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            await SeedStrongSummaries(store, count: 22, lastStart: Now.AddDays(-1));

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 22, profile, Now);

            Assert.Equal(MasteryLevel.Mastered, result.Level);
            Assert.Equal("ALL_GATES_PASSED", result.ReasonCode);
        }

        // ── 5. Lock 10 days ago (inside 14d, outside 7d) → Stable, not Mastered ──
        [Fact]
        public async Task Evaluate_WithSafetyLockWithin14Days_StaysStable()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            // 22 strong sessions ending 8 days ago (so nothing is inside the last 7 days),
            // plus one strong session 10 days ago that carries a safety event.
            await SeedStrongSummaries(store, count: 22, lastStart: Now.AddDays(-8));
            await AddSummary(store, sessionId: 999, startedAt: Now.AddDays(-10),
                resonance: 0.70, stability: 0.65, safetyEvents: 1);

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 22, profile, Now);

            Assert.Equal(MasteryLevel.Stable, result.Level);
            Assert.Equal("GATE_SAFETY_14D", result.ReasonCode);
        }

        // ── 6. 3+ comfort-breach sessions among the recent window → Stable ───────
        [Fact]
        public async Task Evaluate_WithThreeComfortBreachSessions_StaysStable()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            // 22 strong sessions; the most recent ten use session ids 13..22.
            await SeedStrongSummaries(store, count: 22, lastStart: Now.AddDays(-1));

            // Three distinct recent sessions journaled with a comfort-zone breach (max allowed is 2).
            await AddBreachEvent(store, sessionId: 22, occurredAt: Now.AddDays(-1));
            await AddBreachEvent(store, sessionId: 21, occurredAt: Now.AddDays(-2));
            await AddBreachEvent(store, sessionId: 20, occurredAt: Now.AddDays(-3));

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 22, profile, Now);

            Assert.Equal(MasteryLevel.Stable, result.Level);
            Assert.Equal("GATE_COMFORT_BREACHES", result.ReasonCode);
        }

        // ── 7. Rising fatigue trend → Stable, not Mastered ───────────────────────
        [Fact]
        public async Task Evaluate_WithRisingFatigue_StaysStable()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            // 22 strong sessions. The recent window is the last 10 (session ids 13..22);
            // load the second half (18..22) with fatigue so secondHalf > firstHalf and >= 3.
            await SeedStrongSummaries(store, count: 22, lastStart: Now.AddDays(-1),
                fatigueFor: id => id >= 18 ? 2 : 0);

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 22, profile, Now);

            Assert.Equal(MasteryLevel.Stable, result.Level);
            Assert.Equal("GATE_FATIGUE_RISING", result.ReasonCode);
        }

        // ── 8. Resonance below the profile threshold → Developing even with 20+ ──
        [Fact]
        public async Task Evaluate_WithResonanceBelowThreshold_ReturnsDeveloping()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming(); // TargetResonanceMin 0.50

            // 22 sessions whose resonance (0.40) is below the 0.50 floor, stability fine.
            await SeedStrongSummaries(store, count: 22, lastStart: Now.AddDays(-1), resonance: 0.40);

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 22, profile, Now);

            Assert.Equal(MasteryLevel.Developing, result.Level);
            Assert.Equal("BUILDING", result.ReasonCode);
        }

        // ── 9. Resonance-free profile skips the resonance gate ───────────────────
        [Fact]
        public async Task Evaluate_WithProfileNotUsingResonance_SkipsResonanceGate()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);
            // Intonation does not use resonance (UsesResonance == false), StabilityThreshold 0.35.
            var profile = ExerciseTargetProfile.IntonationExercise();

            // Resonance index is poor (0.10) but irrelevant; stability (0.65) clears the 0.35 floor.
            await SeedStrongSummaries(store, count: 22, lastStart: Now.AddDays(-1),
                resonance: 0.10, stability: 0.65);

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 22, profile, Now);

            Assert.Equal(MasteryLevel.Mastered, result.Level);
            Assert.Equal("ALL_GATES_PASSED", result.ReasonCode);
        }

        // ── 10. >= 8 sessions, good averages, clean week → Stable ────────────────
        [Fact]
        public async Task Evaluate_WithEightCleanSessions_ReturnsStableConsistent()
        {
            var store = CreateStore();
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            // Exactly 8 strong sessions — enough for Stable but short of the 20 for Mastered.
            await SeedStrongSummaries(store, count: 8, lastStart: Now.AddDays(-1));

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 8, profile, Now);

            Assert.Equal(MasteryLevel.Stable, result.Level);
            Assert.Equal("CONSISTENT", result.ReasonCode);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static SessionAnalyticsStore CreateStore()
            => new(new InMemorySessionAnalyticsRepository());

        /// <summary>
        /// Seeds <paramref name="count"/> consecutive strong exercise summaries ending at
        /// <paramref name="lastStart"/> (one per day, working backwards). Session ids run
        /// 1..count so the most recent ten are ids (count-9)..count.
        /// </summary>
        private static async Task SeedStrongSummaries(
            SessionAnalyticsStore store,
            int count,
            DateTime lastStart,
            double resonance = 0.70,
            double stability = 0.65,
            Func<int, int>? fatigueFor = null)
        {
            for (var i = 0; i < count; i++)
            {
                var sessionId = i + 1;
                // Oldest session gets the earliest date; the newest (id == count) starts at lastStart.
                var startedAt = lastStart.AddDays(-(count - 1 - i));
                await AddSummary(
                    store,
                    sessionId,
                    startedAt,
                    resonance,
                    stability,
                    safetyEvents: 0,
                    fatigue: fatigueFor?.Invoke(sessionId) ?? 0);
            }
        }

        private static async Task AddSummary(
            SessionAnalyticsStore store,
            int sessionId,
            DateTime startedAt,
            double resonance,
            double stability,
            int safetyEvents = 0,
            int fatigue = 0)
        {
            await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = sessionId,
                ExerciseId = ExerciseId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(5),
                ResonanceQualityIndex = resonance,
                StabilityConsistency = stability,
                HoldCompletionRate = 0.80,
                SafetyEventsCount = safetyEvents,
                FatigueIndicators = fatigue
            });
        }

        private static async Task AddBreachEvent(
            SessionAnalyticsStore store,
            int sessionId,
            DateTime occurredAt)
        {
            await store.RecordHealthEventAsync(new HealthAnalyticsEvent
            {
                SessionId = sessionId,
                EventType = HealthAnalyticsEventType.ComfortZoneBreach,
                OccurredAt = occurredAt,
                Severity = 1,
                ReasonCode = "COMFORT_ZONE_BREACH"
            });
        }
    }
}
