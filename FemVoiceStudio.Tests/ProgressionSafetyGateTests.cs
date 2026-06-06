using System;
using System.Threading.Tasks;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ProgressionSafetyGate"/>.
    /// Verifies the clinical blocking rules evaluated before any difficulty promotion:
    /// repeated safety locks, rising strain trend, repeated comfort-zone breaches,
    /// rising fatigue trend, the 14-day ignore window, and the check priority order.
    ///
    /// The gate reads from a persisted <see cref="SessionAnalyticsStore"/>; tests use the
    /// in-memory repository and seed fixed timestamps relative to a frozen "now".
    /// </summary>
    public class ProgressionSafetyGateTests
    {
        // Frozen evaluation time so the rolling 7-/14-day windows are deterministic.
        private static readonly DateTime Now = new DateTime(2026, 6, 6, 12, 0, 0);

        [Fact]
        public async Task Evaluate_WithCleanHistory_ReturnsClear()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            var result = await gate.EvaluateAsync(Now);

            Assert.False(result.IsBlocked);
            Assert.Equal("CLEAR", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_WithTwoSafetyFreezesInLastWeek_BlocksWithRepeatedSafetyLocks()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            await AddEvent(store, HealthAnalyticsEventType.SafetyFreeze, Now.AddDays(-1), sessionId: 1);
            await AddEvent(store, HealthAnalyticsEventType.SafetyFreeze, Now.AddDays(-3), sessionId: 2);

            var result = await gate.EvaluateAsync(Now);

            Assert.True(result.IsBlocked);
            Assert.Equal("REPEATED_SAFETY_LOCKS", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_WithSingleSafetyFreeze_DoesNotBlock()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            await AddEvent(store, HealthAnalyticsEventType.SafetyFreeze, Now.AddDays(-1), sessionId: 1);

            var result = await gate.EvaluateAsync(Now);

            Assert.False(result.IsBlocked);
            Assert.Equal("CLEAR", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_WithRisingStrainTrend_BlocksWithStrainTrendRising()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            // 2 strain events in the recent week, 1 in the week before -> rising.
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-1), sessionId: 1);
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-2), sessionId: 2);
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-9), sessionId: 3);

            var result = await gate.EvaluateAsync(Now);

            Assert.True(result.IsBlocked);
            Assert.Equal("STRAIN_TREND_RISING", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_WithFallingStrainTrend_DoesNotBlock()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            // 2 strain events in the recent week, 3 in the week before -> falling, not rising.
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-1), sessionId: 1);
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-2), sessionId: 2);
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-9), sessionId: 3);
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-10), sessionId: 4);
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-11), sessionId: 5);

            var result = await gate.EvaluateAsync(Now);

            Assert.False(result.IsBlocked);
            Assert.Equal("CLEAR", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_WithThreeComfortBreachesOnDistinctSessions_BlocksWithRepeatedComfortBreaches()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            await AddEvent(store, HealthAnalyticsEventType.ComfortZoneBreach, Now.AddDays(-1), sessionId: 11);
            await AddEvent(store, HealthAnalyticsEventType.ComfortZoneBreach, Now.AddDays(-2), sessionId: 12);
            await AddEvent(store, HealthAnalyticsEventType.ComfortZoneBreach, Now.AddDays(-3), sessionId: 13);

            var result = await gate.EvaluateAsync(Now);

            Assert.True(result.IsBlocked);
            Assert.Equal("REPEATED_COMFORT_BREACHES", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_WithThreeComfortBreachesOnSameSession_DoesNotBlock()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            // 3 breach events but all on the same session -> only 1 distinct session.
            await AddEvent(store, HealthAnalyticsEventType.ComfortZoneBreach, Now.AddDays(-1), sessionId: 11);
            await AddEvent(store, HealthAnalyticsEventType.ComfortZoneBreach, Now.AddDays(-2), sessionId: 11);
            await AddEvent(store, HealthAnalyticsEventType.ComfortZoneBreach, Now.AddDays(-3), sessionId: 11);

            var result = await gate.EvaluateAsync(Now);

            Assert.False(result.IsBlocked);
            Assert.Equal("CLEAR", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_WithRisingFatigueTrend_BlocksWithFatigueTrendRising()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            // Recent-week fatigue sums to 4 (>= 3), prior week to 1 -> rising.
            await AddExerciseSummary(store, sessionId: 1, exerciseId: 10, startedAt: Now.AddDays(-1), fatigueIndicators: 2);
            await AddExerciseSummary(store, sessionId: 2, exerciseId: 10, startedAt: Now.AddDays(-3), fatigueIndicators: 2);
            await AddExerciseSummary(store, sessionId: 3, exerciseId: 10, startedAt: Now.AddDays(-9), fatigueIndicators: 1);

            var result = await gate.EvaluateAsync(Now);

            Assert.True(result.IsBlocked);
            Assert.Equal("FATIGUE_TREND_RISING", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_WithHighButFallingFatigue_DoesNotBlock()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            // Recent-week fatigue sums to 3 (>= 3) but prior week to 5 -> falling, not rising.
            await AddExerciseSummary(store, sessionId: 1, exerciseId: 10, startedAt: Now.AddDays(-1), fatigueIndicators: 3);
            await AddExerciseSummary(store, sessionId: 2, exerciseId: 10, startedAt: Now.AddDays(-9), fatigueIndicators: 5);

            var result = await gate.EvaluateAsync(Now);

            Assert.False(result.IsBlocked);
            Assert.Equal("CLEAR", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_IgnoresEventsOlderThanFourteenDays()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            // Two safety freezes that would otherwise block, but both are outside the 14-day window.
            await AddEvent(store, HealthAnalyticsEventType.SafetyFreeze, Now.AddDays(-15), sessionId: 1);
            await AddEvent(store, HealthAnalyticsEventType.SafetyFreeze, Now.AddDays(-20), sessionId: 2);

            var result = await gate.EvaluateAsync(Now);

            Assert.False(result.IsBlocked);
            Assert.Equal("CLEAR", result.ReasonCode);
        }

        [Fact]
        public async Task Evaluate_WhenLocksAndStrainBothTrigger_PrioritizesRepeatedSafetyLocks()
        {
            var store = CreateStore();
            var gate = new ProgressionSafetyGate(store);

            // Locks rule satisfied (2 freezes this week).
            await AddEvent(store, HealthAnalyticsEventType.SafetyFreeze, Now.AddDays(-1), sessionId: 1);
            await AddEvent(store, HealthAnalyticsEventType.SafetyFreeze, Now.AddDays(-2), sessionId: 2);

            // Strain rising rule also satisfied (2 recent, 0 prior).
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-1), sessionId: 3);
            await AddEvent(store, HealthAnalyticsEventType.StrainPeriod, Now.AddDays(-3), sessionId: 4);

            var result = await gate.EvaluateAsync(Now);

            Assert.True(result.IsBlocked);
            Assert.Equal("REPEATED_SAFETY_LOCKS", result.ReasonCode);
        }

        private static SessionAnalyticsStore CreateStore()
            => new(new InMemorySessionAnalyticsRepository());

        private static Task AddEvent(
            SessionAnalyticsStore store,
            HealthAnalyticsEventType eventType,
            DateTime occurredAt,
            int sessionId)
            => store.RecordHealthEventAsync(new HealthAnalyticsEvent
            {
                SessionId = sessionId,
                EventType = eventType,
                OccurredAt = occurredAt,
                Severity = 0.5,
                ReasonCode = eventType.ToString()
            });

        private static Task AddExerciseSummary(
            SessionAnalyticsStore store,
            int sessionId,
            int exerciseId,
            DateTime startedAt,
            int fatigueIndicators)
            => store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = sessionId,
                ExerciseId = exerciseId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(5),
                ResonanceQualityIndex = 0.7,
                StabilityConsistency = 0.7,
                HoldCompletionRate = 0.8,
                FatigueIndicators = fatigueIndicators
            });
    }
}
