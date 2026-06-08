using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ExerciseSessionRecorder"/>.
    ///
    /// Strategy: drive the recorder through the real
    /// <see cref="ExerciseIntelligenceCoordinator"/> (parameterless test constructor),
    /// the real <see cref="VocalHealthSupervisor"/> (default options) and a real
    /// <see cref="SessionAnalyticsStore"/> backed by
    /// <see cref="InMemorySessionAnalyticsRepository"/>. No mocking frameworks.
    ///
    /// The coordinator rate-limits evaluations to 100 ms, so every metric injection
    /// after the first is preceded by a short sleep — same pattern as
    /// <see cref="ExerciseIntelligenceCoordinatorTests"/>.
    /// </summary>
    public class ExerciseSessionRecorderTests : IDisposable
    {
        private readonly ExerciseIntelligenceCoordinator _coordinator;
        private readonly VocalHealthSupervisor _supervisor;
        private readonly InMemorySessionAnalyticsRepository _repository;
        private readonly SessionAnalyticsStore _store;
        private readonly ExerciseSessionRecorder _recorder;

        // Fixed, wide query window — the recorder persists records with DateTime.Now
        // timestamps that the test cannot control, so we capture them with a range that
        // is guaranteed to contain "now" without referencing DateTime.Now in asserts.
        private static readonly DateTime WindowFrom = new DateTime(2000, 1, 1, 0, 0, 0);
        private static readonly DateTime WindowTo   = new DateTime(2100, 1, 1, 0, 0, 0);

        public ExerciseSessionRecorderTests()
        {
            _coordinator = new ExerciseIntelligenceCoordinator();
            _supervisor  = new VocalHealthSupervisor();
            _repository  = new InMemorySessionAnalyticsRepository();
            _store       = new SessionAnalyticsStore(_repository);
            _recorder    = new ExerciseSessionRecorder(_coordinator, _supervisor, _store);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        /// <summary>Advances past the coordinator's 100 ms evaluation rate-limit.</summary>
        private static void WaitForRateLimit() => Thread.Sleep(150);

        /// <summary>
        /// Starts the coordinator on the given profile and opens a recording session.
        /// The default zeroed state published by StartExercise is an idle snapshot and
        /// is therefore ignored by the recorder.
        /// </summary>
        private void Begin(ExerciseTargetProfile profile, int exerciseId, int sessionId, int userId = 1)
        {
            _coordinator.StartExercise(profile, userId);
            _recorder.BeginSession(exerciseId, sessionId, userId);
        }

        /// <summary>
        /// Polls the repository until a record satisfying <paramref name="predicate"/> is
        /// found or the timeout elapses. CompleteSession persists fire-and-forget, so the
        /// write may not be visible the instant CompleteSession returns.
        /// </summary>
        private static async Task<T> PollAsync<T>(Func<Task<T?>> fetch, Func<T?, bool> predicate)
            where T : class
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var value = await fetch();
                if (predicate(value)) return value!;
                await Task.Delay(20);
            }

            Assert.Fail("Expected persisted record was not visible within the timeout.");
            throw new InvalidOperationException("unreachable");
        }

        private Task<ExercisePerformanceSummary?> FetchSummaryAsync(int sessionId, int exerciseId)
            => _store.GetExerciseSummariesAsync(WindowFrom, WindowTo)
                .ContinueWith(t => t.Result.FirstOrDefault(s => s.SessionId == sessionId && s.ExerciseId == exerciseId));

        private Task<List<HealthAnalyticsEvent>?> FetchEventsAsync()
            => _store.GetHealthEventsAsync(WindowFrom, WindowTo)
                .ContinueWith(t => (List<HealthAnalyticsEvent>?)t.Result.ToList());

        // The SessionAnalyticsStore exposes session rows only via aggregates, so the raw
        // SessionAnalyticsSessions row (EndedAt, ExerciseCount, …) is read straight from the
        // in-memory repository. The recorder stamps StartedAt/EndedAt with DateTime.Now, so
        // the wide window is guaranteed to contain it.
        private Task<SessionAnalyticsRecord?> FetchSessionAsync(int sessionId, int userId = 1)
            => _repository.GetSessionsAsync(userId, WindowFrom, WindowTo)
                .ContinueWith(t => t.Result.FirstOrDefault(s => s.SessionId == sessionId));

        // ────────────────────────────────────────────────────────────────────────────
        // 1. Aggregation — averages and EvaluatedTicks
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void CompleteSession_AggregatesResonanceAverage_AndTickCount()
        {
            // Arrange — resonance profile maps PrimaryMetricScore directly to resonance.
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 7, sessionId: 100);

            // Act — three non-idle ticks with known resonance values.
            _coordinator.UpdateMetrics(resonanceScore: 0.6, pitch: 200, stability: 0.5, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(resonanceScore: 0.7, pitch: 200, stability: 0.5, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(resonanceScore: 0.8, pitch: 200, stability: 0.5, health: 100);

            var outcome = _recorder.CompleteSession();

            // Assert — mean of 0.6/0.7/0.8 = 0.7, three evaluated ticks.
            Assert.Equal(3, outcome.EvaluatedTicks);
            Assert.Equal(0.7, outcome.AverageResonance, 3);
            Assert.Equal(0.5, outcome.AverageStability, 3);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 2. ComfortCompliance + ComfortBreachEpisodes (edge counting)
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void CompleteSession_TracksComfortComplianceAndBreachEdges_ForPitchProfile()
        {
            // Arrange — pitch profile: comfort zone is 160–220 Hz, stability kept > 0 so
            // out-of-zone ticks (pitch normalising to 0) are still non-idle.
            var profile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 220);
            Begin(profile, exerciseId: 3, sessionId: 200);

            // Act — alternate in-zone / out-of-zone: in, out, in, out.
            _coordinator.UpdateMetrics(0.5, pitch: 200, stability: 0.5, health: 100); // in
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.5, pitch: 300, stability: 0.5, health: 100); // out
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.5, pitch: 200, stability: 0.5, health: 100); // in
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.5, pitch: 300, stability: 0.5, health: 100); // out

            var outcome = _recorder.CompleteSession();

            // Assert — 4 ticks, 2 in comfort zone → compliance 0.5.
            Assert.Equal(4, outcome.EvaluatedTicks);
            Assert.Equal(0.5, outcome.ComfortCompliance, 3);

            // Two distinct in→out transitions (edges), not the two out-of-zone levels.
            Assert.Equal(2, outcome.ComfortBreachEpisodes);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 3. SafetyLockEpisodes — edge counting on a sustained lock
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void CompleteSession_CountsSingleSafetyLockEpisode_EvenWhenLockPersists()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 4, sessionId: 300);

            // Act — three consecutive ticks with health < 70 keep the coordinator locked.
            _coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60);

            var outcome = _recorder.CompleteSession();

            // Assert — a sustained lock is one false→true transition only.
            Assert.Equal(1, outcome.SafetyLockEpisodes);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 4. Idle filter — all-zero snapshots do not count
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void CompleteSession_IgnoresIdleSnapshots()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 9, sessionId: 400);

            // Act — pure-silence frames: resonance/stability/hold all zero, health healthy.
            _coordinator.UpdateMetrics(0, 0, 0, 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0, 0, 0, 100);

            var outcome = _recorder.CompleteSession();

            // Assert — idle snapshots carry no clinical information and are skipped.
            Assert.Equal(0, outcome.EvaluatedTicks);
            Assert.Equal(0, outcome.AverageResonance);
            Assert.Equal(0, outcome.ComfortCompliance);
        }

        [Fact]
        public void CompleteSession_CountsNonIdleTicks_ButNotInterleavedIdleTicks()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 9, sessionId: 401);

            // Act — one real tick, one idle tick, one real tick.
            _coordinator.UpdateMetrics(0.6, 200, 0.5, 100); // real
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0, 0, 0, 100);       // idle → skipped
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.8, 200, 0.5, 100); // real

            var outcome = _recorder.CompleteSession();

            // Assert — only the two real ticks are aggregated; idle ignored.
            Assert.Equal(2, outcome.EvaluatedTicks);
            Assert.Equal(0.7, outcome.AverageResonance, 3);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 5. AbortSession — nothing persisted
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task AbortSession_PersistsNothing()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 5, sessionId: 500);

            _coordinator.UpdateMetrics(0.6, 200, 0.5, 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.7, 200, 0.5, 100);

            // Act
            _recorder.AbortSession();

            // Give any (incorrect) async persistence a chance to land before asserting.
            await Task.Delay(80);

            // Assert — no exercise summaries and no health events were written.
            var summaries = await _store.GetExerciseSummariesAsync(WindowFrom, WindowTo);
            var events    = await _store.GetHealthEventsAsync(WindowFrom, WindowTo);
            Assert.Empty(summaries);
            Assert.Empty(events);
            Assert.False(_recorder.IsRecording);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 6. CompleteSession — persists the performance summary with correct fields
        //    and a SafetyFreeze event when a lock occurred.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_PersistsPerformanceSummary_WithCorrectFields()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 11, sessionId: 600, userId: 1);

            _coordinator.UpdateMetrics(0.6, 200, 0.4, 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.8, 200, 0.6, 100);

            // Act
            var outcome = _recorder.CompleteSession();

            // Assert — the summary lands with the recorder's aggregated values.
            var summary = await PollAsync(
                () => FetchSummaryAsync(600, 11),
                s => s != null);

            Assert.Equal(600, summary.SessionId);
            Assert.Equal(11, summary.ExerciseId);
            Assert.Equal(1, summary.UserId);
            Assert.Equal(outcome.AverageResonance, summary.ResonanceQualityIndex, 3);
            Assert.Equal(outcome.AverageStability, summary.StabilityConsistency, 3);
            Assert.Equal(outcome.HoldCompletion, summary.HoldCompletionRate, 3);
            // No lock occurred → zero safety events recorded on the summary.
            Assert.Equal(0, summary.SafetyEventsCount);
        }

        [Fact]
        public async Task CompleteSession_PersistsSafetyFreezeEvent_WhenLockOccurred()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 12, sessionId: 700);

            // Act — at least one tick locks the coordinator (health < 70).
            _coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, 0.5, health: 100);

            var outcome = _recorder.CompleteSession();
            Assert.Equal(1, outcome.SafetyLockEpisodes);

            // Assert — one SafetyFreeze event with the recorder's reason code.
            var events = await PollAsync(
                FetchEventsAsync,
                e => e != null && e.Any(x => x.EventType == HealthAnalyticsEventType.SafetyFreeze));

            var freeze = events.Single(e => e.EventType == HealthAnalyticsEventType.SafetyFreeze);
            Assert.Equal(700, freeze.SessionId);
            Assert.Equal("EXERCISE_SAFETY_LOCK", freeze.ReasonCode);

            // The summary records the lock count as its safety-event count.
            var summary = await PollAsync(
                () => FetchSummaryAsync(700, 12),
                s => s != null);
            Assert.Equal(1, summary.SafetyEventsCount);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 7. Recording guard — events outside an open session are ignored
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Updates_BeforeBeginSession_AreIgnored()
        {
            // Arrange — coordinator is active but no recording session is open yet.
            var profile = ExerciseTargetProfile.ResonanceExercise();
            _coordinator.StartExercise(profile, 1);

            // Act — metrics arrive before BeginSession.
            _coordinator.UpdateMetrics(0.6, 200, 0.5, 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.7, 200, 0.5, 100);

            // Now open the session and add exactly one real tick.
            _recorder.BeginSession(exerciseId: 1, sessionId: 800);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.8, 200, 0.5, 100);

            var outcome = _recorder.CompleteSession();

            // Assert — only the post-BeginSession tick is aggregated.
            Assert.Equal(1, outcome.EvaluatedTicks);
            Assert.Equal(0.8, outcome.AverageResonance, 3);
        }

        [Fact]
        public void Updates_AfterCompleteSession_AreIgnored()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 1, sessionId: 801);

            _coordinator.UpdateMetrics(0.6, 200, 0.5, 100);
            var outcome1 = _recorder.CompleteSession();
            Assert.Equal(1, outcome1.EvaluatedTicks);

            // Act — further metric updates after completion must be ignored.
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.9, 200, 0.5, 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.9, 200, 0.5, 100);

            // A second CompleteSession returns the same aggregate — the post-completion
            // updates were ignored, so nothing new accumulated.
            var outcome2 = _recorder.CompleteSession();

            // Assert — tick count and average are unchanged by the ignored updates.
            Assert.Equal(outcome1.EvaluatedTicks, outcome2.EvaluatedTicks);
            Assert.Equal(0.6, outcome2.AverageResonance, 3);
            Assert.False(_recorder.IsRecording);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 8. CurrentHealthScore — 100 at construction / before any evaluation
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void CurrentHealthScore_Is100_Initially()
        {
            // Arrange — fresh recorder, no session started yet.
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            using var recorder = new ExerciseSessionRecorder(coordinator, supervisor, store);

            // Assert
            Assert.Equal(100, recorder.CurrentHealthScore);
        }

        [Fact]
        public void BeginSession_ResetsHealthScoreTo100()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 1, sessionId: 900);

            // A healthy tick keeps the supervisor in the Normal state → score 100.
            _coordinator.UpdateMetrics(0.7, 200, 0.6, 100);

            // Act — re-opening a session resets aggregates and health score.
            _recorder.BeginSession(exerciseId: 1, sessionId: 901);

            // Assert
            Assert.Equal(100, _recorder.CurrentHealthScore);
            Assert.True(_recorder.IsRecording);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 9. BeginSession — persists a session-start row (RecordSessionStartedAsync)
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task BeginSession_PersistsSessionStartRow_WithSessionIdAndUserId()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();

            // Act — BeginSession fires RecordSessionStartedAsync fire-and-forget.
            Begin(profile, exerciseId: 21, sessionId: 1000, userId: 7);

            // Assert — the session-start row is visible once the async write lands.
            var session = await PollAsync(
                () => FetchSessionAsync(1000, userId: 7),
                s => s != null);

            Assert.Equal(1000, session.SessionId);
            Assert.Equal(7, session.UserId);
            // A start row has no end and no aggregates yet (RecordSessionStartedAsync clears them).
            Assert.Null(session.EndedAt);
            Assert.Equal(0, session.ExerciseCount);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 10. CompleteSession — persists the session row with EndedAt + aggregates
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_PersistsSessionRow_WithEndedAtAndAggregates()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 22, sessionId: 1100, userId: 1);

            _coordinator.UpdateMetrics(0.6, 200, 0.4, 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.8, 200, 0.6, 100);

            // Act
            var outcome = _recorder.CompleteSession();

            // Assert — poll until the completed row (EndedAt set) is visible. BeginSession
            // wrote a start row first, so we wait specifically for the completion update.
            var session = await PollAsync(
                () => FetchSessionAsync(1100),
                s => s != null && s.EndedAt != null);

            Assert.Equal(1100, session.SessionId);
            Assert.NotNull(session.EndedAt);
            Assert.Equal(1, session.ExerciseCount);
            Assert.Equal(outcome.AverageResonance, session.AverageResonance, 3);
            // AveragePitchComfort is sourced from the outcome's ComfortCompliance.
            Assert.Equal(outcome.ComfortCompliance, session.AveragePitchComfort, 3);
            Assert.Equal(outcome.FatigueIndicators, session.FatigueIndicatorCount);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 11. SubmitSubjectiveReport — health concern → PauseRecommended event
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task SubmitSubjectiveReport_WithHealthConcern_PersistsPauseRecommendedEvent()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 23, sessionId: 1200, userId: 1);

            // ExperiencedStrain=true makes IndicatesHealthConcern true.
            var report = new SubjectiveReport
            {
                SessionId = 1200,
                UserId = 1,
                ComfortLevel = 4,
                FatigueFeeling = 2,
                ExperiencedStrain = true,
                WantsToContinue = false
            };

            // Act
            _recorder.SubmitSubjectiveReport(report);

            // Assert — exactly one PauseRecommended event with the subjective reason code.
            var events = await PollAsync(
                FetchEventsAsync,
                e => e != null && e.Any(x => x.EventType == HealthAnalyticsEventType.PauseRecommended));

            var pause = events.Single(e => e.EventType == HealthAnalyticsEventType.PauseRecommended);
            Assert.Equal(1200, pause.SessionId);
            Assert.Equal(1, pause.UserId);
            Assert.Equal("SUBJECTIVE_HEALTH_CONCERN", pause.ReasonCode);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 12. SubmitSubjectiveReport — no concern → nothing persisted
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task SubmitSubjectiveReport_WithoutHealthConcern_PersistsNothing()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 24, sessionId: 1300, userId: 1);

            // Good comfort (>2), low fatigue (<4), no strain, wants to continue →
            // IndicatesHealthConcern is false.
            var report = new SubjectiveReport
            {
                SessionId = 1300,
                UserId = 1,
                ComfortLevel = 5,
                FatigueFeeling = 1,
                ExperiencedStrain = false,
                WantsToContinue = true
            };

            // Act
            _recorder.SubmitSubjectiveReport(report);

            // Give any (incorrect) async persistence a chance to land before asserting.
            await Task.Delay(80);

            // Assert — no health events were written.
            var events = await _store.GetHealthEventsAsync(WindowFrom, WindowTo);
            Assert.Empty(events);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 13. SubmitSubjectiveReport(null) — no-op, no exception
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task SubmitSubjectiveReport_WithNull_IsNoOp()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise();
            Begin(profile, exerciseId: 25, sessionId: 1400, userId: 1);

            // Act + Assert — null report must not throw.
            var exception = Record.Exception(() => _recorder.SubmitSubjectiveReport(null!));
            Assert.Null(exception);

            // Give any (incorrect) async persistence a chance to land before asserting.
            await Task.Delay(80);

            // Assert — nothing was persisted as a health event.
            var events = await _store.GetHealthEventsAsync(WindowFrom, WindowTo);
            Assert.Empty(events);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 14. Feedback graph — strain routed through mappers → pipeline approval
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void OnExerciseUpdated_WithFullFeedbackGraph_RoutesVocalHealthDecisionToPipeline()
        {
            // Arrange — a recorder wired with the complete (real) feedback graph:
            // VocalHealthSupervisor + HydrationAdvisor + both mappers behind a
            // FeedbackPipeline guarded by a real FeedbackConsistencyGuard.
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var hydration  = new HydrationAdvisor();
            var store      = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var guard      = new FeedbackConsistencyGuard();
            var pipeline   = new FeedbackPipeline(guard);

            using var recorder = new ExerciseSessionRecorder(
                coordinator,
                supervisor,
                store,
                hydration,
                pipeline,
                new VocalHealthFeedbackMapper(),
                new HydrationFeedbackMapper());

            FeedbackCandidate? approved = null;
            pipeline.FeedbackApproved += (_, decision) => approved = decision.Candidate;

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), userId: 1);
            recorder.BeginSession(exerciseId: 30, sessionId: 1500, userId: 1);

            // Act — a tick with health < 70 locks the coordinator. The supervisor's
            // EvaluateStrain adds +0.80 (IsSafetyLocked) → StrainDetected on the first
            // locked tick → the VocalHealthFeedbackMapper produces a candidate.
            coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60);

            // Assert — a candidate sourced from the supervisor was approved by the guard.
            Assert.NotNull(approved);
            Assert.Equal("VocalHealthSupervisor", approved!.Source);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 15. Null-safe — same run without a feedback graph does not throw
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void OnExerciseUpdated_WithoutFeedbackGraph_IsNullSafe()
        {
            // Arrange — the legacy 3-arg recorder (no advisor / pipeline / mappers).
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var store      = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            using var recorder = new ExerciseSessionRecorder(coordinator, supervisor, store);

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), userId: 1);
            recorder.BeginSession(exerciseId: 31, sessionId: 1600, userId: 1);

            // Act + Assert — the same lock-inducing tick must not throw when the
            // optional feedback dependencies are null.
            var exception = Record.Exception(() =>
                coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60));
            Assert.Null(exception);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 16. Guard rate-limiting — two rapid strain ticks → one approved health candidate
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void OnExerciseUpdated_TwoRapidStrainTicks_GuardRateLimitsSameConflictKey()
        {
            // Arrange — a frozen clock so the guard's minimum-interval rate-limit is
            // deterministic: both ticks evaluate at the same instant, well within the
            // guard's default 2-second minimum interval for a repeated reason.
            var frozen = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var store      = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var guard      = new FeedbackConsistencyGuard(clock: () => frozen);
            var pipeline   = new FeedbackPipeline(guard);

            using var recorder = new ExerciseSessionRecorder(
                coordinator,
                supervisor,
                store,
                hydrationAdvisor: null,
                feedbackPipeline: pipeline,
                vocalHealthMapper: new VocalHealthFeedbackMapper(),
                hydrationMapper: new HydrationFeedbackMapper());

            var approvedHealthCandidates = new List<FeedbackCandidate>();
            pipeline.FeedbackApproved += (_, decision) =>
            {
                if (decision.Candidate.Source == "VocalHealthSupervisor")
                    approvedHealthCandidates.Add(decision.Candidate);
            };

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), userId: 1);
            recorder.BeginSession(exerciseId: 32, sessionId: 1700, userId: 1);

            // Act — two locked ticks in quick succession (frozen clock). Both produce a
            // supervisor candidate with the same conflict key, but the guard suppresses
            // the second because the same reason was approved within the min interval.
            coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60);
            WaitForRateLimit(); // only advances the coordinator's 100 ms gate, not the guard clock
            coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60);

            // Assert — exactly one approved health candidate; the rapid duplicate (same
            // conflict key, same frozen instant) was rate-limited away.
            Assert.Single(approvedHealthCandidates);
            var distinctConflictKeys = approvedHealthCandidates
                .Select(c => c.ConflictKey)
                .Distinct()
                .Count();
            Assert.Equal(1, distinctConflictKeys);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 17. ExerciseDetailViewModel — pipeline approval surfaces as coach message,
        //     filtered by source.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ExerciseDetailViewModel_ShowsApprovedFeedback_FromNonCoordinatorSourceOnly()
        {
            // Arrange — the VM subscribes to the pipeline's FeedbackApproved event.
            // Application.Current is null in the test host, so the handler runs
            // synchronously (no dispatcher marshalling).
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var localization = new TestLocalizationService();
            localization.AddString("VoiceHealthFeedback_Restrict", "Skjerm stemmen — ta det med ro.");
            var guard    = new FeedbackPipeline(new FeedbackConsistencyGuard());

            using var vm = new ExerciseDetailViewModel(
                coordinator,
                localization,
                profileFactory: null,
                feedbackPipeline: guard,
                inlineCoachFeedbackMapper: new InlineCoachFeedbackMapper());

            Assert.False(vm.IsCoachMessageVisible);

            // Act — submit a candidate sourced from the supervisor (NOT the coordinator).
            var healthCandidate = new FeedbackCandidate(
                "VoiceHealthFeedback_Restrict",
                "HEALTH_RESTRICT",
                FeedbackPriority.HealthWarning,
                MessageSeverity.Warning,
                Source: "VocalHealthSupervisor",
                ConflictKey: "HEALTH_RESTRICT");
            var healthDecision = guard.Submit(healthCandidate);
            Assert.Equal(FeedbackDecisionKind.Approved, healthDecision.Kind);

            // Assert — the VM resolves the localization key and shows it as the coach msg.
            Assert.True(vm.IsCoachMessageVisible);
            Assert.Equal("Skjerm stemmen — ta det med ro.", vm.CoachMessage);

            var healthMessage = vm.CoachMessage;

            // Act — a later candidate sourced from the coordinator must be filtered out
            // (it is surfaced via the inline-coach path, not this pipeline handler).
            var coordinatorCandidate = new FeedbackCandidate(
                "InlineCoachFeedback_ResonanceTooLow",
                "RESONANCE_TOO_LOW",
                FeedbackPriority.SafetyFreeze, // high priority → guard would otherwise approve
                MessageSeverity.Warning,
                Source: "ExerciseIntelligenceCoordinator",
                ConflictKey: "INLINE_RESONANCE_TOO_LOW");
            var coordinatorDecision = guard.Submit(coordinatorCandidate);
            Assert.Equal(FeedbackDecisionKind.Approved, coordinatorDecision.Kind);

            // Assert — the coach message is unchanged: the coordinator-sourced candidate
            // was filtered by OnPipelineFeedbackApproved.
            Assert.Equal(healthMessage, vm.CoachMessage);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 18. RES-01 — a pitch-only session must NOT report its normalised-pitch proxy as
        //     resonance. The Resonance dimension stays neutral 50, not the pitch value.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_PitchOnlyProfile_DoesNotEchoPitchProxyAsResonance()
        {
            // Arrange — a PITCH-ONLY profile (UsesResonance=false, UsesPitch=true). For this
            // profile the coordinator sets PrimaryMetricScore = NormalizePitch(...), i.e. a
            // pitch PROXY, never a resonance value. Comfort zone 160–220 Hz; pitch 200 Hz is
            // in-zone and normalises to (200−160)/(220−160) ≈ 0.67 ⇒ a clearly non-50 proxy.
            var profile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 220);
            Assert.False(profile.UsesResonance);
            Assert.True(profile.UsesPitch);
            Begin(profile, exerciseId: 40, sessionId: 1800, userId: 1);

            // Act — several high in-zone pitch ticks, NO real resonance signal at all.
            _coordinator.UpdateMetrics(0.0, pitch: 200, stability: 0.6, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.0, pitch: 205, stability: 0.6, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.0, pitch: 195, stability: 0.6, health: 100);

            var outcome = _recorder.CompleteSession();
            await (_recorder.LastPersistTask ?? Task.CompletedTask);

            // The session never carried a real resonance signal, so the outcome reports it
            // as such — AverageResonance stays 0 and UsedResonanceSignal is false. Crucially
            // the pitch proxy (~0.67) was NOT folded into the resonance mean.
            Assert.False(outcome.UsedResonanceSignal);
            Assert.Equal(0.0, outcome.AverageResonance, 3);

            // End-to-end: the persisted Resonance dimension is the neutral 50, NOT ~67 (the
            // pitch proxy ×100). A regression that echoes the proxy would land ~67 here.
            var session = await PollAsync(
                () => FetchSessionAsync(1800, userId: 1),
                s => s != null && s.EndedAt != null);
            Assert.Equal(50.0, session.ResonanceScore100, 3);

            // And the dimension's explanation says there was no resonance signal — proving
            // ScoreResonance took the neutral branch, not the "from average resonance" branch.
            var scores = new VoiceIntelligenceScorer().Compute(VoiceIntelligenceInput.Empty());
            Assert.Equal(50.0, scores.Resonance.Score, 3);
            Assert.Contains("no resonance signal", scores.Resonance.Explanation,
                StringComparison.OrdinalIgnoreCase);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 19. CONS-1/2 — Consistency must measure REPRODUCIBILITY (in-session spread),
        //     not voicing clarity. A steady 0.7 trace ≫ a 0.4/1.0-alternating mean-0.7 trace.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CompleteSession_Consistency_RewardsSteadyTrace_OverOscillatingSameMean()
        {
            // Two sessions with the SAME mean stability (0.7) but very different SPREAD.
            // Old behaviour (Consistency = average stability) would score them identically.
            // The fix re-derives Consistency from the in-session std-dev, so the steady
            // trace must score substantially higher than the oscillating one.
            var profile = ExerciseTargetProfile.ResonanceExercise();

            // ── Session A: perfectly steady stability 0.7 across four ticks ──────────
            Begin(profile, exerciseId: 41, sessionId: 1900, userId: 1);
            _coordinator.UpdateMetrics(0.6, 200, stability: 0.7, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, stability: 0.7, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, stability: 0.7, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, stability: 0.7, health: 100);
            var steady = _recorder.CompleteSession();
            await (_recorder.LastPersistTask ?? Task.CompletedTask);

            // ── Session B: 0.4 / 1.0 alternating — identical mean 0.7, large spread ──
            Begin(profile, exerciseId: 41, sessionId: 1901, userId: 1);
            _coordinator.UpdateMetrics(0.6, 200, stability: 0.4, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, stability: 1.0, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, stability: 0.4, health: 100);
            WaitForRateLimit();
            _coordinator.UpdateMetrics(0.6, 200, stability: 1.0, health: 100);
            var oscillating = _recorder.CompleteSession();
            await (_recorder.LastPersistTask ?? Task.CompletedTask);

            // Same mean stability (the live-UI / MasteryEvaluator signal is unchanged) …
            Assert.Equal(0.7, steady.AverageStability, 3);
            Assert.Equal(0.7, oscillating.AverageStability, 3);

            // … but very different STEADINESS — flat ⇒ ~1.0, oscillating ⇒ much lower.
            Assert.True(steady.StabilitySteadiness01 > 0.95,
                $"steady steadiness should be near 1.0 but was {steady.StabilitySteadiness01:0.000}");
            Assert.True(oscillating.StabilitySteadiness01 < 0.25,
                $"oscillating steadiness should be low but was {oscillating.StabilitySteadiness01:0.000}");

            // The persisted ConsistencyScore100 (which the scorer derives from steadiness)
            // must be substantially higher for the steady trace than the oscillating one.
            var steadyRow = await PollAsync(
                () => FetchSessionAsync(1900, userId: 1),
                s => s != null && s.EndedAt != null);
            var oscillatingRow = await PollAsync(
                () => FetchSessionAsync(1901, userId: 1),
                s => s != null && s.EndedAt != null);

            Assert.True(steadyRow.ConsistencyScore100 - oscillatingRow.ConsistencyScore100 > 40,
                $"steady Consistency ({steadyRow.ConsistencyScore100:0}) should far exceed " +
                $"oscillating ({oscillatingRow.ConsistencyScore100:0}).");
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 20. VH-02 (safety) — repeated SUPERVISOR pause recommendations across sessions
        //     feed the progression block via a distinct SUPERVISOR_PAUSE channel, WITHOUT
        //     any subjective report. They must never collide with SUBJECTIVE_HEALTH_CONCERN.
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task RepeatedSupervisorPauses_AcrossSessions_BlockProgression_WithoutSubjectiveReport()
        {
            // Induce an OBJECTIVE supervisor pause via FATIGUE, not a safety lock, so the
            // test isolates the SUPERVISOR_PAUSE channel: the first non-idle tick seeds the
            // supervisor's meso EMA directly, so resonance/stability of 0.30 (well under the
            // 0.70 baseline) drives both fatigue drifts past threshold ⇒ fatigueDetected ⇒
            // pauseRecommended on that very tick — with health 100 (no lock) and in the
            // comfort zone (no breach). This avoids the gate's earlier REPEATED_SAFETY_LOCKS
            // rule firing first, proving the pause channel specifically reaches the gate.
            var profile = ExerciseTargetProfile.ResonanceExercise();

            async Task RunPauseInducingSessionAsync(int sessionId)
            {
                Begin(profile, exerciseId: 42, sessionId: sessionId, userId: 1);
                // Two low-but-non-idle ticks (health 100 ⇒ no lock). Fatigue → pause fires.
                _coordinator.UpdateMetrics(0.30, 200, stability: 0.30, health: 100);
                WaitForRateLimit();
                _coordinator.UpdateMetrics(0.30, 200, stability: 0.30, health: 100);
                _recorder.CompleteSession();
                await (_recorder.LastPersistTask ?? Task.CompletedTask);
            }

            // Act — two separate pause-inducing sessions; the supervisor resets per session.
            await RunPauseInducingSessionAsync(2000);
            await RunPauseInducingSessionAsync(2001);

            // Assert — two PauseRecommended events exist, BOTH from the objective supervisor
            // channel (SUPERVISOR_PAUSE), and NONE from the subjective channel.
            var events = await PollAsync(
                FetchEventsAsync,
                e => e != null
                     && e.Count(x => x.EventType == HealthAnalyticsEventType.PauseRecommended
                                     && x.ReasonCode == "SUPERVISOR_PAUSE") >= 2);

            var supervisorPauses = events
                .Where(x => x.EventType == HealthAnalyticsEventType.PauseRecommended)
                .ToList();
            Assert.True(supervisorPauses.All(x => x.ReasonCode == "SUPERVISOR_PAUSE"),
                "all pause events must carry the distinct SUPERVISOR_PAUSE reason code.");
            Assert.DoesNotContain(supervisorPauses, x => x.ReasonCode == "SUBJECTIVE_HEALTH_CONCERN");

            // And the gate blocks progression on REPEATED_PAUSE_RECOMMENDATIONS — proving the
            // supervisor channel reaches the gate with no subjective report whatsoever.
            var gate = new ProgressionSafetyGate(_store);
            var result = await gate.EvaluateAsync(DateTime.Now, userId: 1);
            Assert.True(result.IsBlocked);
            Assert.Equal("REPEATED_PAUSE_RECOMMENDATIONS", result.ReasonCode);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Teardown
        // ────────────────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            _recorder.Dispose();
            _coordinator.Dispose();
        }
    }
}
