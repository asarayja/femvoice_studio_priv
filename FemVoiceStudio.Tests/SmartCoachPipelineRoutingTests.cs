using System;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Integration tests verifying that SmartCoachEngine routes ALL clinically
    /// relevant coach messages through the FeedbackPipeline + FeedbackConsistencyGuard
    /// before they can reach the database (and therefore the dashboard read path
    /// GetUnreadMessages → SmartCoachViewModel.RecentMessages).
    ///
    /// Key invariant: AnalyzeSessionForStrain's health_warning message must NOT be
    /// persisted directly via _database.SaveCoachMessage; it must only be saved when
    /// the guard returns Approved. A guard that suppresses the message leaves the
    /// message store empty.
    /// </summary>
    public class SmartCoachPipelineRoutingTests
    {
        // Fixed clock so the guard's per-reason rate limiting is deterministic.
        private static readonly DateTime FixedNow =
            new(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);

        private static TrainingSession PitchPressSession()
            => new()
            {
                UserId = 1,
                StartTime = FixedNow,
                EndTime = FixedNow.AddMinutes(10),
                AveragePitch = 200, // Above the 180 Hz pitch-press threshold.
                OverallScore = 80
            };

        [Fact]
        public void AnalyzeSessionForStrain_WithApprovingPipeline_SavesHealthWarningThroughPipeline()
        {
            // Arrange: fresh guard (nothing primed) approves the first health warning.
            var db = new TestDatabaseService();
            var guard = new FeedbackConsistencyGuard(() => FixedNow, TimeSpan.FromSeconds(5));
            var pipeline = new FeedbackPipeline(guard);
            var mapper = new SmartCoachFeedbackMapper();
            var engine = new SmartCoachEngine(db, null, pipeline, mapper);

            // Act
            var health = engine.AnalyzeSessionForStrain(PitchPressSession(), 1);

            // Assert: strain detected AND exactly one approved message persisted.
            Assert.True(health.StrainDetected);
            Assert.Equal("pitch_press", health.StrainType);

            var saved = db.GetUnreadMessages(1);
            Assert.Single(saved);
            Assert.Equal("health_warning", saved[0].MessageType);
        }

        [Fact]
        public void AnalyzeSessionForStrain_WithSuppressingPipeline_DoesNotSaveMessage()
        {
            // Arrange: prime the guard with the SAME health_warning reason at the same
            // instant so the engine's subsequent submission is rate-limited → Suppressed.
            var db = new TestDatabaseService();
            var guard = new FeedbackConsistencyGuard(() => FixedNow, TimeSpan.FromSeconds(5));
            var pipeline = new FeedbackPipeline(guard);
            var mapper = new SmartCoachFeedbackMapper();

            var primingMessage = new SmartCoachMessage
            {
                UserId = 1,
                MessageType = "health_warning",
                Message = "Priming the guard with the same reason code."
            };
            var primingDecision = pipeline.Submit(
                mapper.Map(primingMessage)!,
                mapper.BuildContext(primingMessage));
            Assert.Equal(FeedbackDecisionKind.Approved, primingDecision.Kind);

            var engine = new SmartCoachEngine(db, null, pipeline, mapper);

            // Act: the strain health warning shares the reason SMARTCOACH_HEALTH_WARNING
            // and arrives within the rate-limit window → guard suppresses it.
            var health = engine.AnalyzeSessionForStrain(PitchPressSession(), 1);

            // Assert: strain still detected and health monitoring still recorded,
            // but NO coach message reached the database (suppressed ⇒ not saved).
            Assert.True(health.StrainDetected);
            Assert.Empty(db.GetUnreadMessages(1));
            Assert.Empty(db.GetMessages(1));
        }

        [Fact]
        public void DashboardReadPath_OnlyExposesPipelineApprovedStrainMessages()
        {
            // Arrange
            var db = new TestDatabaseService();
            var guard = new FeedbackConsistencyGuard(() => FixedNow, TimeSpan.FromSeconds(5));
            var pipeline = new FeedbackPipeline(guard);
            var mapper = new SmartCoachFeedbackMapper();
            var engine = new SmartCoachEngine(db, null, pipeline, mapper);

            // Act
            engine.AnalyzeSessionForStrain(PitchPressSession(), 1);

            // Assert: GetUnreadMessages is the exact source the SmartCoach dashboard
            // (SmartCoachViewModel.RecentMessages) binds to. Every message it returns
            // must have passed the guard, so unread == approved.
            var dashboardMessages = db.GetUnreadMessages(1);
            Assert.Single(dashboardMessages);
            Assert.False(dashboardMessages[0].IsRead);
            Assert.Equal("health_warning", dashboardMessages[0].MessageType);
            Assert.False(string.IsNullOrWhiteSpace(dashboardMessages[0].Message));
        }

        [Fact]
        public void StrainHealthWarningContext_FlagsActiveStrain_AndSuppressesConcurrentPraise()
        {
            // The strain health-warning context must declare IsActiveStrainAlert so that
            // any concurrent lower-priority praise/progression is suppressed by the guard.
            var mapper = new SmartCoachFeedbackMapper();
            var strainMessage = new SmartCoachMessage
            {
                UserId = 1,
                MessageType = "health_warning",
                Message = "Rest your voice."
            };

            var context = mapper.BuildContext(strainMessage);
            Assert.True(context.IsActiveStrainAlert);
            Assert.True(context.IsHealthRiskActive);

            var guard = new FeedbackConsistencyGuard(() => FixedNow, TimeSpan.Zero);
            var praise = new FeedbackCandidate(
                "Great consistency!",
                "SMARTCOACH_ACHIEVEMENT",
                FeedbackPriority.PerformancePraise,
                MessageSeverity.Info);

            var decision = guard.Submit(praise, context);

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Active strain", decision.Reason);
        }

        [Fact]
        public void AnalyzeSessionForStrain_WithoutPipeline_FallsBackToDirectSaveForBackwardCompat()
        {
            // When no pipeline/mapper is wired (legacy single-arg construction), the
            // engine must still surface the warning so coaching does not silently die.
            // This documents the SaveCoachMessageThroughPipeline fallback branch.
            var db = new TestDatabaseService();
            var engine = new SmartCoachEngine(db);

            var health = engine.AnalyzeSessionForStrain(PitchPressSession(), 1);

            Assert.True(health.StrainDetected);
            Assert.Single(db.GetUnreadMessages(1));
            Assert.Equal("health_warning", db.GetUnreadMessages(1)[0].MessageType);
        }
    }
}
