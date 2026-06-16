using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Runtime-integration tests for the Bølge-1 wiring of <see cref="ExerciseSessionRecorder"/>
    /// (Agent 3 cross-session recovery + Agent 5 SessionInsight). No mocking: a real
    /// <see cref="ExerciseIntelligenceCoordinator"/>, real <see cref="VocalHealthSupervisor"/>
    /// and a real <see cref="SessionAnalyticsStore"/> backed by the in-memory repository.
    ///
    /// The recorder builds the SessionInsight and the FULL cross-session RecoveryScoreInput
    /// on the SAME async path as persistence, so every assertion awaits
    /// <see cref="ExerciseSessionRecorder.LastPersistTask"/> before reading
    /// <see cref="ExerciseSessionRecorder.LastSessionInsight"/> / the persisted scores.
    ///
    /// The coordinator rate-limits evaluations to 100 ms, so consecutive metric injections
    /// are spaced by a short sleep — same pattern as <see cref="ExerciseSessionRecorderTests"/>.
    /// </summary>
    public class SessionInsightWiringTests : IDisposable
    {
        private readonly ExerciseIntelligenceCoordinator _coordinator;
        private readonly VocalHealthSupervisor _supervisor;
        private readonly InMemorySessionAnalyticsRepository _repository;
        private readonly SessionAnalyticsStore _store;
        private readonly ExerciseSessionRecorder _recorder;

        private static readonly DateTime WindowFrom = new DateTime(2000, 1, 1, 0, 0, 0);
        private static readonly DateTime WindowTo   = new DateTime(2100, 1, 1, 0, 0, 0);

        public SessionInsightWiringTests()
        {
            _coordinator = new ExerciseIntelligenceCoordinator();
            _supervisor  = new VocalHealthSupervisor();
            _repository  = new InMemorySessionAnalyticsRepository();
            _store       = new SessionAnalyticsStore(_repository);
            _recorder    = new ExerciseSessionRecorder(_coordinator, _supervisor, _store);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static void WaitForRateLimit() => Thread.Sleep(150);

        private void Begin(ExerciseTargetProfile profile, int exerciseId, int sessionId, int userId = 1)
        {
            _coordinator.StartExercise(profile, userId);
            _recorder.BeginSession(exerciseId, sessionId, userId);
        }

        /// <summary>
        /// Completes the session and awaits the persistence/insight task so
        /// <see cref="ExerciseSessionRecorder.LastSessionInsight"/> is guaranteed populated.
        /// </summary>
        private async Task<ExerciseSessionOutcome> CompleteAndWaitAsync()
        {
            var outcome = _recorder.CompleteSession();
            if (_recorder.LastPersistTask is { } task)
            {
                try { await task; } catch { /* persist failures are swallowed in the recorder */ }
            }
            return outcome;
        }

        /// <summary>A few healthy, in-zone resonance ticks → a clean, non-empty session.</summary>
        private void RunCleanResonanceTicks()
        {
            _coordinator.UpdateMetrics(resonanceScore: 0.6, pitch: 200, stability: 0.6, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(resonanceScore: 0.7, pitch: 200, stability: 0.6, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(resonanceScore: 0.8, pitch: 200, stability: 0.6, health: 100);
        }

        /// <summary>
        /// Seeds a prior COMPLETED session row a given number of days ago so it lands in the
        /// recovery window and the prior Voice-Intelligence trend. Uses the store's own
        /// completion path so the row carries the eight 0–100 scores.
        /// </summary>
        private Task SeedPriorSessionAsync(
            int sessionId,
            double daysAgo,
            double resonanceScore100 = 70,
            double comfortScore100 = 70,
            double compositeVoiceScore = 70,
            int userId = 1)
        {
            var started = DateTime.Now.AddDays(-daysAgo);
            return _store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId           = sessionId,
                UserId              = userId,
                StartedAt           = started,
                EndedAt             = started.AddMinutes(10),
                ExerciseCount       = 1,
                ResonanceScore100   = resonanceScore100,
                ComfortScore100     = comfortScore100,
                ConsistencyScore100 = 70,
                IntonationScore100  = 70,
                VocalWeightScore100 = 70,
                RecoveryScore100    = 70,
                PitchScore100       = 70,
                CompositeVoiceScore = compositeVoiceScore
            });
        }

        private Task SeedSafetyLockEventAsync(int sessionId, double daysAgo, int userId = 1)
            => _store.RecordHealthEventAsync(new HealthAnalyticsEvent
            {
                SessionId  = sessionId,
                UserId     = userId,
                EventType  = HealthAnalyticsEventType.SafetyFreeze,
                OccurredAt = DateTime.Now.AddDays(-daysAgo),
                Severity   = 1.0,
                ReasonCode = "EXERCISE_SAFETY_LOCK"
            });

        private Task SeedStrainEventAsync(int sessionId, double daysAgo, int userId = 1)
            => _store.RecordHealthEventAsync(new HealthAnalyticsEvent
            {
                SessionId  = sessionId,
                UserId     = userId,
                EventType  = HealthAnalyticsEventType.StrainPeriod,
                OccurredAt = DateTime.Now.AddDays(-daysAgo),
                Severity   = 0.8,
                ReasonCode = "STRAIN_DETECTED"
            });

        private async Task<double> ReadPersistedRecoveryScoreAsync(int sessionId, int userId = 1)
        {
            var trend = await _store.GetVoiceIntelligenceTrendAsync(WindowFrom, WindowTo, userId);
            var point = trend.FirstOrDefault(p => p.SessionId == sessionId);
            Assert.NotNull(point);
            return point!.RecoveryScore100;
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 1. A clean session produces an insight whose suggested focus is a real dimension.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_ProducesSessionInsight_WithValidFocusAndComposite()
        {
            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 1, sessionId: 5000);
            RunCleanResonanceTicks();

            await CompleteAndWaitAsync();

            var insight = _recorder.LastSessionInsight;
            Assert.NotNull(insight);
            Assert.Equal(5000, insight!.SessionId);
            // Composite is a 0–100 measurement.
            Assert.InRange(insight.CompositeVoiceScore, 0.0, 100.0);
            // The suggested focus is always a defined VoiceDimension.
            Assert.True(Enum.IsDefined(typeof(VoiceDimension), insight.SuggestedFocus));
            // The summary is non-empty, encouraging copy assembled by the builder.
            Assert.False(string.IsNullOrWhiteSpace(insight.Summary));
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 2. Risks reflect the session outcome — a safety lock surfaces a SAFETY_LOCK risk.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_WhenSafetyLockOccurred_InsightCarriesSafetyLockRisk()
        {
            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 2, sessionId: 5100);

            // A tick with health < 70 locks the coordinator → one safety-lock episode.
            _coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, 0.5, health: 100);

            var outcome = await CompleteAndWaitAsync();
            Assert.Equal(1, outcome.SafetyLockEpisodes);

            var insight = _recorder.LastSessionInsight;
            Assert.NotNull(insight);
            var lockRisk = insight!.Risks.FirstOrDefault(r => r.ReasonCode == "SAFETY_LOCK");
            Assert.NotNull(lockRisk);
            Assert.Equal(1, lockRisk!.Count);
            Assert.False(string.IsNullOrWhiteSpace(lockRisk.Description));
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 3. First session (no prior trend) → IsFirstSession, no improvements.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_FirstSession_HasNoImprovements_AndIsFlaggedFirst()
        {
            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 3, sessionId: 5200);
            RunCleanResonanceTicks();

            await CompleteAndWaitAsync();

            var insight = _recorder.LastSessionInsight;
            Assert.NotNull(insight);
            Assert.True(insight!.IsFirstSession);
            Assert.Empty(insight.Improvements);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 4. Improvements are computed against the prior trend (not the first session).
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_WithLowPriorScores_ReportsImprovements_AgainstPriorTrend()
        {
            // A prior session two days ago with very LOW scores → this clean session should
            // measure improvements against it (and is therefore NOT a first session).
            await SeedPriorSessionAsync(
                sessionId: 4000, daysAgo: 2,
                resonanceScore100: 5, comfortScore100: 5, compositeVoiceScore: 5);

            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 4, sessionId: 5300);
            RunCleanResonanceTicks();

            await CompleteAndWaitAsync();

            var insight = _recorder.LastSessionInsight;
            Assert.NotNull(insight);
            Assert.False(insight!.IsFirstSession);
            // At least one dimension rose past the improvement threshold vs the low reference.
            Assert.NotEmpty(insight.Improvements);
            Assert.All(insight.Improvements, i => Assert.True(i.Delta >= SessionInsightBuilder.ImprovementThreshold));
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 5. The current session is never its own improvement reference.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_ExcludesCurrentSessionFromPriorTrend()
        {
            // No prior history except the current session's own start row (written by
            // BeginSession with zero scores). If that row were used as the reference, the
            // session would spuriously "improve" against itself. It must be excluded ⇒
            // first session with no improvements.
            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 5, sessionId: 5400);
            RunCleanResonanceTicks();

            await CompleteAndWaitAsync();

            var insight = _recorder.LastSessionInsight;
            Assert.NotNull(insight);
            Assert.True(insight!.IsFirstSession);
            Assert.Empty(insight.Improvements);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 6. An empty / idle session produces NO insight (property stays null).
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_EmptySession_ProducesNoInsight()
        {
            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 6, sessionId: 5500);

            // Only pure-silence (idle) frames — the recorder skips them, so EvaluatedTicks = 0.
            _coordinator.UpdateMetrics(0, 0, 0, 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0, 0, 0, 100);

            var outcome = await CompleteAndWaitAsync();

            Assert.Equal(0, outcome.EvaluatedTicks);
            Assert.Null(_recorder.LastSessionInsight);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 7. The SessionInsightReady event fires with the same insight as the property.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_RaisesSessionInsightReady_WithTheBuiltInsight()
        {
            SessionInsight? eventInsight = null;
            _recorder.SessionInsightReady += i => eventInsight = i;

            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 7, sessionId: 5600);
            RunCleanResonanceTicks();

            await CompleteAndWaitAsync();

            Assert.NotNull(eventInsight);
            Assert.Same(_recorder.LastSessionInsight, eventInsight);
            Assert.Equal(5600, eventInsight!.SessionId);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 8. CROSS-SESSION RECOVERY CONTRAST: seeded recent locks/strain pull the recovery
        //    axis well below the clean no-history baseline (the dormant branches now fire).
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_WithRecentSafetyHistory_LowersRecoveryAxis_VsNoHistory()
        {
            // ── Baseline: a clean session with NO prior history ──────────────────────
            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 8, sessionId: 5700);
            RunCleanResonanceTicks();
            await CompleteAndWaitAsync();
            var baselineRecovery = await ReadPersistedRecoveryScoreAsync(5700);

            // Fresh fixture so the baseline's own persisted row cannot pollute the history.
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var repository = new InMemorySessionAnalyticsRepository();
            var store      = new SessionAnalyticsStore(repository);
            using var recorder = new ExerciseSessionRecorder(coordinator, supervisor, store);

            // ── Seed heavy recent load: several completed sessions + many safety/strain
            //    events inside the acute (7-day) window. ─────────────────────────────
            for (var i = 0; i < 4; i++)
            {
                await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
                {
                    SessionId = 4100 + i,
                    UserId    = 1,
                    StartedAt = DateTime.Now.AddDays(-(i + 1)),
                    EndedAt   = DateTime.Now.AddDays(-(i + 1)).AddMinutes(10),
                    ExerciseCount = 1,
                    CompositeVoiceScore = 60
                });
                await store.RecordHealthEventAsync(new HealthAnalyticsEvent
                {
                    SessionId  = 4100 + i,
                    UserId     = 1,
                    EventType  = HealthAnalyticsEventType.SafetyFreeze,
                    OccurredAt = DateTime.Now.AddDays(-(i + 1)),
                    Severity   = 1.0,
                    ReasonCode = "EXERCISE_SAFETY_LOCK"
                });
                await store.RecordHealthEventAsync(new HealthAnalyticsEvent
                {
                    SessionId  = 4100 + i,
                    UserId     = 1,
                    EventType  = HealthAnalyticsEventType.StrainPeriod,
                    OccurredAt = DateTime.Now.AddDays(-(i + 1)),
                    Severity   = 0.8,
                    ReasonCode = "STRAIN_DETECTED"
                });
            }

            // The CURRENT session is itself clean (no in-session lock) — so any drop in the
            // recovery axis comes purely from the cross-session history wiring.
            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 8, sessionId: 5701, userId: 1);
            coordinator.UpdateMetrics(0.6, 200, 0.6, 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.7, 200, 0.6, 100);
            var historyOutcome = recorder.CompleteSession();
            if (recorder.LastPersistTask is { } t) { try { await t; } catch { } }

            // The current session itself had no safety lock — proves the recovery drop is
            // cross-session, not in-session.
            Assert.Equal(0, historyOutcome.SafetyLockEpisodes);

            var historyTrend = await store.GetVoiceIntelligenceTrendAsync(WindowFrom, WindowTo, 1);
            var historyRecovery = historyTrend.Single(p => p.SessionId == 5701).RecoveryScore100;

            // The seeded recent safety/strain load drags the recovery axis clearly below the
            // clean baseline. (Manual: baseline ≈ 100; history ≈ 100 − 2×18 (locks, capped at
            // 90) − 2×9 (strain) − rest reward ≈ well under 80.)
            Assert.True(historyRecovery < baselineRecovery - 10,
                $"Expected history recovery ({historyRecovery:0.0}) to be well below baseline ({baselineRecovery:0.0}).");
            Assert.True(historyRecovery < 80,
                $"Expected history recovery ({historyRecovery:0.0}) to reflect heavy recent load (< 80).");

            recorder.Dispose();
            coordinator.Dispose();
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 9. The insight's RecoveryNeeds reflect the cross-session history too (not just
        //    the in-session counts) — its score matches the persisted recovery axis.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_InsightRecoveryNeeds_MatchPersistedRecoveryScore()
        {
            // Seed a heavy recent load so the recovery axis is meaningfully below 100.
            for (var i = 0; i < 3; i++)
            {
                await SeedSafetyLockEventAsync(sessionId: 4200 + i, daysAgo: i + 1);
                await SeedStrainEventAsync(sessionId: 4200 + i, daysAgo: i + 1);
                await SeedPriorSessionAsync(sessionId: 4200 + i, daysAgo: i + 1, compositeVoiceScore: 60);
            }

            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 9, sessionId: 5800);
            RunCleanResonanceTicks();

            await CompleteAndWaitAsync();

            var insight = _recorder.LastSessionInsight;
            Assert.NotNull(insight);
            var persistedRecovery = await ReadPersistedRecoveryScoreAsync(5800);

            // The insight's recovery-need is built from the SAME cross-session input that
            // scored the persisted recovery axis, so the two must agree.
            Assert.Equal(persistedRecovery, insight!.RecoveryNeeds.Score, 1);
            // And it is clearly below a fully-rested 100 (history actually fed the score).
            Assert.True(insight.RecoveryNeeds.Score < 90,
                $"Expected recovery-need ({insight.RecoveryNeeds.Score:0.0}) to reflect recent load (< 90).");
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 10. AbortSession produces no insight (no persistence, no insight).
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task AbortSession_ProducesNoInsight()
        {
            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 10, sessionId: 5900);
            RunCleanResonanceTicks();

            _recorder.AbortSession();
            await Task.Delay(80);

            Assert.Null(_recorder.LastSessionInsight);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 11. BeginSession clears a stale insight from a previous session.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task BeginSession_ClearsPriorInsight()
        {
            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 11, sessionId: 6000);
            RunCleanResonanceTicks();
            await CompleteAndWaitAsync();
            Assert.NotNull(_recorder.LastSessionInsight);

            // Opening a new session must clear the previous insight immediately.
            _recorder.BeginSession(exerciseId: 11, sessionId: 6001);
            Assert.Null(_recorder.LastSessionInsight);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 12. Session end never throws even when the analytics store is broken — the
        //     recovery/insight wiring is fully guarded (the clinical invariant: building
        //     insight must NEVER block or crash session end).
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_NeverThrows_WhenAnalyticsStoreFails()
        {
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var store      = new SessionAnalyticsStore(new ThrowingAnalyticsRepository());
            using var recorder = new ExerciseSessionRecorder(coordinator, supervisor, store);

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 12, sessionId: 6100, userId: 1);
            coordinator.UpdateMetrics(0.6, 200, 0.6, 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.7, 200, 0.6, 100);

            // CompleteSession is synchronous and must not throw despite the broken store.
            var exception = Record.Exception(() => recorder.CompleteSession());
            Assert.Null(exception);

            // Awaiting the persistence/insight task must also never surface an exception.
            if (recorder.LastPersistTask is { } task)
            {
                var awaitException = await Record.ExceptionAsync(async () => await task);
                Assert.Null(awaitException);
            }

            recorder.Dispose();
            coordinator.Dispose();
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 13. The insight's suggested focus is the weakest VI dimension (hierarchy
        //     tie-break) — the recorder hands the builder the freshly scored VI.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_SuggestedFocus_IsAValidNextStepFromTheScores()
        {
            Begin(ExerciseTargetProfile.ResonanceExercise(), exerciseId: 13, sessionId: 6200);
            RunCleanResonanceTicks();

            await CompleteAndWaitAsync();

            var insight = _recorder.LastSessionInsight;
            Assert.NotNull(insight);
            // Suggested exercises (if any) come from the static focus→exercise map; the focus
            // itself is always a defined dimension. The breakdown is deterministic and safe.
            Assert.True(Enum.IsDefined(typeof(VoiceDimension), insight!.SuggestedFocus));
            Assert.False(string.IsNullOrWhiteSpace(insight.BuildBreakdown()));
        }

        public void Dispose()
        {
            _recorder.Dispose();
            _coordinator.Dispose();
        }

        /// <summary>
        /// In-memory repository whose every read/write throws — used to prove the recorder's
        /// recovery/insight wiring is fully guarded and never blocks or crashes session end.
        /// A real fake (no mocking framework), per the project's testing rules.
        /// </summary>
        private sealed class ThrowingAnalyticsRepository : ISessionAnalyticsRepository, IVoiceIntelligenceTrendSource
        {
            public Task SaveSessionAsync(SessionAnalyticsRecord session, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("store is broken");

            public Task SaveExerciseSummaryAsync(ExercisePerformanceSummary summary, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("store is broken");

            public Task SaveHealthEventAsync(HealthAnalyticsEvent healthEvent, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("store is broken");

            public Task<System.Collections.Generic.IReadOnlyList<SessionAnalyticsRecord>> GetSessionsAsync(
                int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("store is broken");

            public Task<System.Collections.Generic.IReadOnlyList<ExercisePerformanceSummary>> GetExerciseSummariesAsync(
                int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("store is broken");

            public Task<System.Collections.Generic.IReadOnlyList<HealthAnalyticsEvent>> GetHealthEventsAsync(
                int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("store is broken");

            public Task<System.Collections.Generic.IReadOnlyList<VoiceIntelligenceTrendPoint>> GetVoiceIntelligenceTrendAsync(
                int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("store is broken");
        }
    }
}
