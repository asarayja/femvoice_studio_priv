using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class SessionAnalyticsStoreTests
    {
        [Fact]
        public async Task RecordSessionStarted_StoresOpenSessionWithoutCompletionMetrics()
        {
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            var day = new DateOnly(2026, 5, 28);
            var startedAt = day.ToDateTime(new TimeOnly(9, 0));

            await store.RecordSessionStartedAsync(new SessionAnalyticsRecord
            {
                SessionId = 5,
                StartedAt = startedAt,
                AverageResonance = 0.9,
                ExerciseCount = 3
            });

            var sessions = await repository.GetSessionsAsync(1, startedAt.AddMinutes(-1), startedAt.AddMinutes(1));
            var summary = await store.GetDailySummaryAsync(day);

            Assert.Single(sessions);
            Assert.Null(sessions[0].EndedAt);
            Assert.Equal(0, sessions[0].ExerciseCount);
            Assert.Equal(0, sessions[0].AverageResonance);
            Assert.Equal(1, summary.SessionCount);
            Assert.Equal(TimeSpan.Zero, summary.TotalDuration);
        }

        [Fact]
        public async Task RecordSessionCompleted_UpdatesStartedSessionWithoutDuplicate()
        {
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            var day = new DateOnly(2026, 5, 28);
            var startedAt = day.ToDateTime(new TimeOnly(9, 0));

            await store.RecordSessionStartedAsync(new SessionAnalyticsRecord
            {
                SessionId = 5,
                StartedAt = startedAt
            });

            await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = 5,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(10),
                ExerciseCount = 1,
                AverageResonance = 0.7,
                AverageStability = 0.8
            });

            var sessions = await repository.GetSessionsAsync(1, startedAt.AddMinutes(-1), startedAt.AddMinutes(11));
            var summary = await store.GetDailySummaryAsync(day);

            Assert.Single(sessions);
            Assert.Equal(startedAt.AddMinutes(10), sessions[0].EndedAt);
            Assert.Equal(1, summary.SessionCount);
            Assert.Equal(1, summary.ExerciseCount);
            Assert.Equal(TimeSpan.FromMinutes(10), summary.TotalDuration);
            Assert.Equal(0.7, summary.AverageResonance);
        }

        [Fact]
        public async Task RecordSessionCompleted_StoresNormalizedSessionInDailySummary()
        {
            var store = CreateStore();
            var day = new DateOnly(2026, 5, 28);

            await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = 10,
                StartedAt = day.ToDateTime(new TimeOnly(10, 0)),
                EndedAt = day.ToDateTime(new TimeOnly(10, 20)),
                ExerciseCount = 2,
                AverageResonance = 1.4,
                AverageStability = 0.6,
                AveragePitchComfort = double.NaN,
                AverageHealthScore = 0.9
            });

            var summary = await store.GetDailySummaryAsync(day);

            Assert.Equal(1, summary.SessionCount);
            Assert.Equal(2, summary.ExerciseCount);
            Assert.Equal(TimeSpan.FromMinutes(20), summary.TotalDuration);
            Assert.Equal(1, summary.AverageResonance);
            Assert.Equal(0.6, summary.AverageStability);
            Assert.Equal(0, summary.AveragePitchComfort);
            Assert.Equal(0.9, summary.AverageHealthScore);
        }

        [Fact]
        public async Task RecordExercisePerformance_AddsHoldAndFatigueData()
        {
            var store = CreateStore();
            var day = new DateOnly(2026, 5, 28);

            await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = 20,
                ExerciseId = 3,
                StartedAt = day.ToDateTime(new TimeOnly(11, 0)),
                HoldCompletionRate = 0.75,
                ResonanceQualityIndex = 0.8,
                StabilityConsistency = 0.7,
                FatigueIndicators = 2,
                CoachingHintsTriggered = 4
            });

            var summary = await store.GetDailySummaryAsync(day);

            Assert.Equal(0.75, summary.HoldCompletionRate);
            Assert.Equal(2, summary.FatigueIndicatorCount);
        }

        [Fact]
        public async Task RecordHealthEvent_CountsSafetyPauseHydrationAndStrain()
        {
            var store = CreateStore();
            var day = new DateOnly(2026, 5, 28);
            var at = day.ToDateTime(new TimeOnly(12, 0));

            await store.RecordHealthEventAsync(Event(HealthAnalyticsEventType.SafetyFreeze, at));
            await store.RecordHealthEventAsync(Event(HealthAnalyticsEventType.PauseRecommended, at.AddMinutes(1)));
            await store.RecordHealthEventAsync(Event(HealthAnalyticsEventType.HydrationSuggested, at.AddMinutes(2)));
            await store.RecordHealthEventAsync(Event(HealthAnalyticsEventType.StrainPeriod, at.AddMinutes(3)));

            var summary = await store.GetDailySummaryAsync(day);

            Assert.Equal(1, summary.SafetyEventsCount);
            Assert.Equal(1, summary.PauseRecommendationsCount);
            Assert.Equal(1, summary.HydrationSuggestionsCount);
            Assert.Equal(1, summary.StrainPeriodsCount);
        }

        [Fact]
        public async Task RecordHealthEvent_StoresHealthTrendWithoutInflatingInterventionCounts()
        {
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            var day = new DateOnly(2026, 5, 28);
            var at = day.ToDateTime(new TimeOnly(12, 0));

            await store.RecordHealthEventAsync(Event(HealthAnalyticsEventType.HealthTrendUpdated, at) with
            {
                Severity = 0.42,
                ReasonCode = "FATIGUE_RISING"
            });

            var events = await repository.GetHealthEventsAsync(1, at.AddMinutes(-1), at.AddMinutes(1));
            var summary = await store.GetDailySummaryAsync(day);

            Assert.Single(events);
            Assert.Equal(HealthAnalyticsEventType.HealthTrendUpdated, events[0].EventType);
            Assert.Equal(0.42, events[0].Severity);
            Assert.Equal("FATIGUE_RISING", events[0].ReasonCode);
            Assert.Equal(0, summary.SafetyEventsCount);
            Assert.Equal(0, summary.PauseRecommendationsCount);
            Assert.Equal(0, summary.HydrationSuggestionsCount);
            Assert.Equal(0, summary.StrainPeriodsCount);
        }

        [Fact]
        public async Task DailySummary_AveragesMultipleSessions()
        {
            var store = CreateStore();
            var day = new DateOnly(2026, 5, 28);

            await store.RecordSessionCompletedAsync(Session(1, day, resonance: 0.6, stability: 0.8));
            await store.RecordSessionCompletedAsync(Session(2, day, resonance: 0.8, stability: 0.4));

            var summary = await store.GetDailySummaryAsync(day);

            Assert.Equal(2, summary.SessionCount);
            Assert.Equal(0.7, summary.AverageResonance, 3);
            Assert.Equal(0.6, summary.AverageStability, 3);
        }

        [Fact]
        public async Task WeeklyTrend_ReturnsSevenOrderedDays()
        {
            var store = CreateStore();
            var monday = new DateOnly(2026, 5, 25);

            await store.RecordSessionCompletedAsync(Session(1, monday.AddDays(2), resonance: 0.8, stability: 0.6));

            var trend = await store.GetWeeklyTrendAsync(monday);

            Assert.Equal(7, trend.Days.Count);
            Assert.Equal(monday, trend.Days.First().Day);
            Assert.Equal(monday.AddDays(6), trend.Days.Last().Day);
            Assert.Equal(0.8, trend.AverageResonance);
        }

        [Fact]
        public async Task ExerciseTrend_ReturnsRequestedExerciseOnly()
        {
            var store = CreateStore();
            var day = new DateOnly(2026, 5, 28);
            var from = day.ToDateTime(TimeOnly.MinValue);
            var to = from.AddDays(1);

            await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = 1,
                ExerciseId = 7,
                StartedAt = from.AddHours(1),
                HoldCompletionRate = 0.7
            });
            await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = 2,
                ExerciseId = 8,
                StartedAt = from.AddHours(2),
                HoldCompletionRate = 0.9
            });

            var trend = await store.GetExerciseTrendAsync(7, from, to);

            Assert.Single(trend);
            Assert.Equal(7, trend[0].ExerciseId);
            Assert.Equal(0.7, trend[0].HoldCompletionRate);
        }

        [Fact]
        public async Task DuplicateSessionExerciseAndEvent_DoNotDoubleCount()
        {
            var store = CreateStore();
            var day = new DateOnly(2026, 5, 28);
            var at = day.ToDateTime(new TimeOnly(13, 0));
            var eventId = Guid.NewGuid();

            await store.RecordSessionCompletedAsync(Session(1, day, resonance: 0.5, stability: 0.5));
            await store.RecordSessionCompletedAsync(Session(1, day, resonance: 0.9, stability: 0.9));
            await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = 1,
                ExerciseId = 5,
                StartedAt = at,
                HoldCompletionRate = 0.4,
                FatigueIndicators = 1
            });
            await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = 1,
                ExerciseId = 5,
                StartedAt = at,
                HoldCompletionRate = 0.8,
                FatigueIndicators = 3
            });
            await store.RecordHealthEventAsync(Event(HealthAnalyticsEventType.SafetyFreeze, at) with { EventId = eventId });
            await store.RecordHealthEventAsync(Event(HealthAnalyticsEventType.SafetyFreeze, at) with { EventId = eventId });

            var summary = await store.GetDailySummaryAsync(day);

            Assert.Equal(1, summary.SessionCount);
            Assert.Equal(0.9, summary.AverageResonance);
            Assert.Equal(0.8, summary.HoldCompletionRate);
            Assert.Equal(3, summary.FatigueIndicatorCount);
            Assert.Equal(1, summary.SafetyEventsCount);
        }

        [Fact]
        public async Task DailySummary_DoesNotDoubleCountSafetyWhenSessionAndEventBothReportIt()
        {
            var store = CreateStore();
            var day = new DateOnly(2026, 5, 28);
            var at = day.ToDateTime(new TimeOnly(13, 0));

            await store.RecordSessionCompletedAsync(Session(1, day, resonance: 0.5, stability: 0.5) with
            {
                SafetyEventsCount = 1
            });
            await store.RecordHealthEventAsync(Event(HealthAnalyticsEventType.SafetyFreeze, at));

            var summary = await store.GetDailySummaryAsync(day);

            Assert.Equal(1, summary.SafetyEventsCount);
        }

        [Fact]
        public async Task MissingData_ReturnsEmptySummary()
        {
            var store = CreateStore();

            var summary = await store.GetDailySummaryAsync(new DateOnly(2026, 5, 28));

            Assert.Equal(0, summary.SessionCount);
            Assert.Equal(TimeSpan.Zero, summary.TotalDuration);
            Assert.Equal(0, summary.AverageResonance);
            Assert.Equal(0, summary.SafetyEventsCount);
        }

        [Fact]
        public async Task SqliteRepository_PersistsSessionAnalyticsAcrossStoreInstances()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"femvoice-analytics-{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={databasePath}";
            var day = new DateOnly(2026, 5, 28);

            try
            {
                var firstStore = new SessionAnalyticsStore(new SqliteSessionAnalyticsRepository(connectionString));
                await firstStore.RecordSessionCompletedAsync(Session(44, day, resonance: 0.7, stability: 0.6));
                await firstStore.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
                {
                    SessionId = 44,
                    ExerciseId = 12,
                    StartedAt = day.ToDateTime(new TimeOnly(15, 0)),
                    HoldCompletionRate = 0.8,
                    ResonanceQualityIndex = 0.7,
                    StabilityConsistency = 0.6
                });

                var secondStore = new SessionAnalyticsStore(new SqliteSessionAnalyticsRepository(connectionString));
                var summary = await secondStore.GetDailySummaryAsync(day);
                var trend = await secondStore.GetExerciseTrendAsync(
                    12,
                    day.ToDateTime(TimeOnly.MinValue),
                    day.ToDateTime(TimeOnly.MinValue).AddDays(1));

                Assert.Equal(1, summary.SessionCount);
                Assert.Equal(0.7, summary.AverageResonance, 3);
                Assert.Single(trend);
                Assert.Equal(0.8, trend[0].HoldCompletionRate);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }
            }
        }

        private static SessionAnalyticsStore CreateStore()
        {
            return new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
        }

        private static SessionAnalyticsRecord Session(int sessionId, DateOnly day, double resonance, double stability)
        {
            var start = day.ToDateTime(new TimeOnly(9, 0)).AddMinutes(sessionId);
            return new SessionAnalyticsRecord
            {
                SessionId = sessionId,
                StartedAt = start,
                EndedAt = start.AddMinutes(10),
                ExerciseCount = 1,
                AverageResonance = resonance,
                AverageStability = stability,
                AveragePitchComfort = 0.7,
                AverageHealthScore = 0.8
            };
        }

        private static HealthAnalyticsEvent Event(HealthAnalyticsEventType eventType, DateTime occurredAt)
        {
            return new HealthAnalyticsEvent
            {
                SessionId = 1,
                EventType = eventType,
                OccurredAt = occurredAt,
                Severity = 0.8,
                ReasonCode = eventType.ToString()
            };
        }
    }
}
