using System;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class FeedbackConsistencyGuardTests
    {
        private static FeedbackCandidate Candidate(
            FeedbackPriority priority,
            string reason,
            MessageSeverity severity = MessageSeverity.Info)
            => new("message", reason, priority, severity);

        [Fact]
        public void HealthWarning_OverridesPerformancePraise()
        {
            var guard = new FeedbackConsistencyGuard();
            var praise = Candidate(FeedbackPriority.PerformancePraise, "PRAISE");

            var decision = guard.Submit(praise, new FeedbackGuardContext(IsHealthRiskActive: true));

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Health risk", decision.Reason);
        }

        [Fact]
        public void PauseRecommendation_OverridesTechniqueHint()
        {
            var guard = new FeedbackConsistencyGuard();
            var technique = Candidate(FeedbackPriority.TechniqueCorrection, "TECHNIQUE");

            var decision = guard.Submit(technique, new FeedbackGuardContext(IsPauseRecommended: true));

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Pause", decision.Reason);
        }

        [Fact]
        public void SafetyFreeze_OverridesProgression()
        {
            var guard = new FeedbackConsistencyGuard();
            var progression = Candidate(FeedbackPriority.ProgressionUpdate, "PROGRESSION");

            var decision = guard.Submit(progression, new FeedbackGuardContext(IsSafetyFreezeActive: true));

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Safety freeze", decision.Reason);
        }

        [Fact]
        public void SubmitMany_ApprovesOnlyHighestPriorityMessage()
        {
            var guard = new FeedbackConsistencyGuard(minimumInterval: TimeSpan.Zero);
            var decisions = guard.SubmitMany(new[]
            {
                Candidate(FeedbackPriority.PerformancePraise, "PRAISE"),
                Candidate(FeedbackPriority.ActiveStrainAlert, "STRAIN", MessageSeverity.Warning),
                Candidate(FeedbackPriority.TechniqueCorrection, "TECHNIQUE", MessageSeverity.Suggestion)
            });

            Assert.Single(decisions.Where(d => d.Kind == FeedbackDecisionKind.Approved));
            Assert.Equal("STRAIN", decisions.Single(d => d.Kind == FeedbackDecisionKind.Approved).Candidate.ReasonCode);
        }

        [Fact]
        public void RateLimit_SuppressesRepeatedReason()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var guard = new FeedbackConsistencyGuard(() => now, TimeSpan.FromSeconds(5));
            var first = Candidate(FeedbackPriority.TechniqueCorrection, "SAME", MessageSeverity.Suggestion);
            var second = Candidate(FeedbackPriority.TechniqueCorrection, "SAME", MessageSeverity.Suggestion);

            var firstDecision = guard.Submit(first);
            var secondDecision = guard.Submit(second);

            Assert.Equal(FeedbackDecisionKind.Approved, firstDecision.Kind);
            Assert.Equal(FeedbackDecisionKind.Suppressed, secondDecision.Kind);
            Assert.Contains("Rate limit", secondDecision.Reason);
        }

        [Fact]
        public void RepeatedSuppression_EscalatesConflict()
        {
            var guard = new FeedbackConsistencyGuard(minimumInterval: TimeSpan.Zero, escalationThreshold: 3);
            var candidate = Candidate(FeedbackPriority.PerformancePraise, "PRAISE");
            var context = new FeedbackGuardContext(IsHoldStable: false);
            var escalated = false;
            guard.FeedbackEscalated += (_, _) => escalated = true;

            guard.Submit(candidate, context);
            guard.Submit(candidate, context);
            var third = guard.Submit(candidate, context);

            Assert.True(escalated);
            Assert.Equal(FeedbackDecisionKind.Escalated, third.Kind);
        }

        [Fact]
        public void UnstableHold_SuppressesPraise()
        {
            var guard = new FeedbackConsistencyGuard();
            var praise = Candidate(FeedbackPriority.PerformancePraise, "HOLD_COMPLETE");

            var decision = guard.Submit(praise, new FeedbackGuardContext(IsHoldStable: false));

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Unstable hold", decision.Reason);
        }

        [Fact]
        public void ActiveStrain_SuppressesPraise()
        {
            var guard = new FeedbackConsistencyGuard();
            var praise = Candidate(FeedbackPriority.PerformancePraise, "PRAISE");

            var decision = guard.Submit(praise, new FeedbackGuardContext(IsActiveStrainAlert: true));

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Active strain", decision.Reason);
        }

        [Fact]
        public void FeedbackPipeline_PublishesOnlyApprovedToApprovedEvent()
        {
            var guard = new FeedbackConsistencyGuard(minimumInterval: TimeSpan.FromSeconds(5));
            var pipeline = new FeedbackPipeline(guard);
            var approvedCount = 0;
            var suppressedCount = 0;
            pipeline.FeedbackApproved += (_, _) => approvedCount++;
            pipeline.FeedbackSuppressed += (_, _) => suppressedCount++;
            var candidate = Candidate(FeedbackPriority.TechniqueCorrection, "TECHNIQUE");

            pipeline.Submit(candidate);
            pipeline.Submit(candidate);

            Assert.Equal(1, approvedCount);
            Assert.Equal(1, suppressedCount);
        }

        [Fact]
        public void SmartCoachFeedbackMapper_MapsHealthWarningToHealthWarning()
        {
            var mapper = new SmartCoachFeedbackMapper();
            var message = new SmartCoachMessage
            {
                MessageType = "health_warning",
                Message = "Rest your voice."
            };

            var candidate = mapper.Map(message);
            var context = mapper.BuildContext(message);

            Assert.NotNull(candidate);
            Assert.Equal("Rest your voice.", candidate!.Message);
            Assert.Equal("SMARTCOACH_HEALTH_WARNING", candidate.ReasonCode);
            Assert.Equal(FeedbackPriority.HealthWarning, candidate.Priority);
            Assert.True(context.IsHealthRiskActive);
            Assert.True(context.IsActiveStrainAlert);
            Assert.True(context.IsPauseRecommended);
        }

        [Fact]
        public void SmartCoachFeedbackMapper_MapsTipToTechniqueCorrection()
        {
            var mapper = new SmartCoachFeedbackMapper();
            var message = new SmartCoachMessage
            {
                MessageType = "tip",
                Message = "Focus on resonance."
            };

            var candidate = mapper.Map(message);
            var context = mapper.BuildContext(message);

            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.TechniqueCorrection, candidate!.Priority);
            Assert.Equal(MessageSeverity.Suggestion, candidate.Severity);
            Assert.False(context.IsHealthRiskActive);
        }

        [Fact]
        public void SmartCoachPipeline_SuppressesRepeatedMessageReason()
        {
            var guard = new FeedbackConsistencyGuard(minimumInterval: TimeSpan.FromSeconds(5));
            var pipeline = new FeedbackPipeline(guard);
            var mapper = new SmartCoachFeedbackMapper();
            var message = new SmartCoachMessage
            {
                MessageType = "tip",
                Message = "Focus on resonance."
            };
            var candidate = mapper.Map(message)!;
            var context = mapper.BuildContext(message);

            var first = pipeline.Submit(candidate, context);
            var second = pipeline.Submit(candidate, context);

            Assert.Equal(FeedbackDecisionKind.Approved, first.Kind);
            Assert.Equal(FeedbackDecisionKind.Suppressed, second.Kind);
            Assert.Contains("Rate limit", second.Reason);
        }

        [Fact]
        public void FreshSession_SuppressesGenericHydrationFeedback()
        {
            var now = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
            var guard = new FeedbackConsistencyGuard(() => now, minimumInterval: TimeSpan.Zero);
            guard.BeginSession(now);
            var hydration = new FeedbackCandidate(
                "VoiceHealthFeedback_Hydration",
                HydrationReasonCodes.Basic,
                FeedbackPriority.HydrationSuggestion,
                MessageSeverity.Suggestion,
                "HydrationAdvisor",
                "HEALTH_HYDRATION");

            var decision = guard.Submit(
                hydration,
                new FeedbackGuardContext(SessionElapsedSeconds: 30));

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Fresh session", decision.Reason);
        }

        [Fact]
        public void CriticalWarnings_BypassFreshSessionSuppression()
        {
            var now = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
            var guard = new FeedbackConsistencyGuard(() => now, minimumInterval: TimeSpan.Zero);
            guard.BeginSession(now);
            var warning = new FeedbackCandidate(
                "VoiceHealthFeedback_Strain",
                "STRAIN_DETECTED",
                FeedbackPriority.ActiveStrainAlert,
                MessageSeverity.Warning,
                "VocalHealthSupervisor",
                "HEALTH_STRAIN");

            var decision = guard.Submit(
                warning,
                new FeedbackGuardContext(IsActiveStrainAlert: true, SessionElapsedSeconds: 30));

            Assert.Equal(FeedbackDecisionKind.Approved, decision.Kind);
        }

        [Fact]
        public void StalePreviousSession_LowPriorityMessageIsRemoved()
        {
            var now = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
            var guard = new FeedbackConsistencyGuard(() => now, minimumInterval: TimeSpan.Zero);
            guard.BeginSession(now);
            var stale = new FeedbackCandidate(
                "ProgressionFeedback_Update",
                "PROFILE_UPDATED",
                FeedbackPriority.ProgressionUpdate,
                MessageSeverity.Info,
                "ProgressionOrchestrator",
                "PROGRESSION_UPDATE",
                CreatedAt: now.AddMinutes(-61));

            var decision = guard.Submit(
                stale,
                new FeedbackGuardContext(SessionElapsedSeconds: 300));

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Stale", decision.Reason);
        }

        [Fact]
        public void DuplicateReasonOrConflict_SuppressesSecondHydrationCandidate()
        {
            var now = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
            var guard = new FeedbackConsistencyGuard(() => now, minimumInterval: TimeSpan.Zero);
            guard.BeginSession(now);
            var context = new FeedbackGuardContext(SessionElapsedSeconds: 300);
            var first = new FeedbackCandidate(
                "VoiceHealthFeedback_Hydration",
                HydrationReasonCodes.Basic,
                FeedbackPriority.HydrationSuggestion,
                MessageSeverity.Suggestion,
                "HydrationAdvisor",
                "HEALTH_HYDRATION");
            var second = first with { ReasonCode = "HYDRATION_SUSTAINED" };

            var firstDecision = guard.Submit(first, context);
            var secondDecision = guard.Submit(second, context);

            Assert.Equal(FeedbackDecisionKind.Approved, firstDecision.Kind);
            Assert.Equal(FeedbackDecisionKind.Suppressed, secondDecision.Kind);
            Assert.Contains("conflict key", secondDecision.Reason);
        }

        [Fact]
        public void LowPriorityMessage_PerSessionLimitSuppressesFurtherRepeats()
        {
            var now = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
            var guard = new FeedbackConsistencyGuard(() => now, minimumInterval: TimeSpan.Zero);
            guard.BeginSession(now);
            var context = new FeedbackGuardContext(SessionElapsedSeconds: 300);
            var generic = new FeedbackCandidate(
                "InlineCoachFeedback_Generic",
                "GENERIC_HINT",
                FeedbackPriority.TechniqueCorrection,
                MessageSeverity.Suggestion,
                "ExerciseIntelligenceCoordinator",
                "INLINE_GENERIC");

            var first = guard.Submit(generic, context);
            now = now.AddMinutes(6);
            var second = guard.Submit(generic with { CreatedAt = now }, context);

            Assert.Equal(FeedbackDecisionKind.Approved, first.Kind);
            Assert.Equal(FeedbackDecisionKind.Suppressed, second.Kind);
            Assert.Contains("Per-session display limit", second.Reason);
        }

        [Fact]
        public void SubmitMany_FatigueOrRestOutranksHydration()
        {
            var guard = new FeedbackConsistencyGuard(minimumInterval: TimeSpan.Zero);
            guard.BeginSession(new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc));
            var hydration = new FeedbackCandidate(
                "VoiceHealthFeedback_Hydration",
                HydrationReasonCodes.Basic,
                FeedbackPriority.HydrationSuggestion,
                MessageSeverity.Suggestion,
                "HydrationAdvisor",
                "HEALTH_HYDRATION");
            var fatigue = new FeedbackCandidate(
                "VoiceHealthFeedback_Fatigue",
                "FATIGUE_DETECTED",
                FeedbackPriority.HealthWarning,
                MessageSeverity.Warning,
                "VocalHealthSupervisor",
                "HEALTH_FATIGUE");

            var decisions = guard.SubmitMany(
                new[] { hydration, fatigue },
                new FeedbackGuardContext(IsFatigueActive: true, SessionElapsedSeconds: 300));

            var approved = Assert.Single(decisions.Where(d => d.Kind == FeedbackDecisionKind.Approved));
            Assert.Equal("FATIGUE_DETECTED", approved.Candidate.ReasonCode);
        }

        [Fact]
        public void DynamicRecheck_SuppressesCandidateWhenContextChangesBeforeDisplay()
        {
            var now = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
            var guard = new FeedbackConsistencyGuard(() => now, minimumInterval: TimeSpan.Zero);
            guard.BeginSession(now);
            var candidate = new FeedbackCandidate(
                "VoiceHealthFeedback_Hydration",
                HydrationReasonCodes.Basic,
                FeedbackPriority.HydrationSuggestion,
                MessageSeverity.Suggestion,
                "HydrationAdvisor",
                "HEALTH_HYDRATION");

            var earlyContext = new FeedbackGuardContext(SessionElapsedSeconds: 30);
            var decision = guard.Submit(candidate, earlyContext);

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Fresh session", decision.Reason);
        }

        [Fact]
        public void InlineCoachFeedbackMapper_MapsSafetyLockToSafetyFreeze()
        {
            var mapper = new InlineCoachFeedbackMapper();
            var message = new InlineCoachMessage
            {
                ShortMessage = "raw",
                CoachingReason = "HEALTH_SAFETY_LOCK",
                Severity = MessageSeverity.Warning,
                AutoDismissSeconds = 12
            };
            var state = new ExerciseLiveState { IsSafetyLocked = true };

            var candidate = mapper.Map(message);
            var context = mapper.BuildContext(message, state);

            Assert.NotNull(candidate);
            // Updated: pipeline now delegates to ExerciseCoach_* keys (same set as the coordinator)
            // so identical wording fires regardless of which path resolves the trigger.
            Assert.Equal("ExerciseCoach_HealthSafetyLock", candidate!.Message);
            Assert.Equal(FeedbackPriority.SafetyFreeze, candidate.Priority);
            Assert.True(context.IsSafetyFreezeActive);
            Assert.True(context.IsHealthRiskActive);
            Assert.True(context.IsPauseRecommended);
        }

        [Fact]
        public void InlineCoachFeedbackMapper_MapsHoldCompleteToPraise()
        {
            var mapper = new InlineCoachFeedbackMapper();
            var message = new InlineCoachMessage
            {
                ShortMessage = "raw",
                CoachingReason = "HOLD_COMPLETE",
                Severity = MessageSeverity.Info,
                AutoDismissSeconds = 4
            };

            var candidate = mapper.Map(message);

            Assert.NotNull(candidate);
            // Updated: pipeline now delegates to ExerciseCoach_* keys (same set as the coordinator)
            // so identical wording fires regardless of which path resolves the trigger.
            Assert.Equal("ExerciseCoach_HoldComplete", candidate!.Message);
            Assert.Equal(FeedbackPriority.PerformancePraise, candidate.Priority);
            Assert.Equal("ExerciseIntelligenceCoordinator", candidate.Source);
        }

        [Fact]
        public void InlineCoachPraise_IsSuppressedWhenHoldIsNotStable()
        {
            var guard = new FeedbackConsistencyGuard(minimumInterval: TimeSpan.Zero);
            var pipeline = new FeedbackPipeline(guard);
            var mapper = new InlineCoachFeedbackMapper();
            var message = new InlineCoachMessage
            {
                ShortMessage = "raw",
                CoachingReason = "HOLD_COMPLETE",
                Severity = MessageSeverity.Info
            };
            var candidate = mapper.Map(message)!;
            var context = mapper.BuildContext(message, new ExerciseLiveState { IsHoldingCorrectly = false });

            var decision = pipeline.Submit(candidate, context);

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Unstable hold", decision.Reason);
        }

        [Fact]
        public void ProgressionFeedbackMapper_MapsRegressionToHealthWarning()
        {
            var mapper = new ProgressionFeedbackMapper();
            var decision = new ProgressionOrchestratorDecision
            {
                Kind = ProgressionOrchestratorDecisionKind.RegressionTriggered,
                ReasonCode = "SAFETY_EVENTS"
            };

            var candidate = mapper.Map(decision);
            var context = mapper.BuildContext(decision);

            Assert.NotNull(candidate);
            Assert.Equal("ProgressionFeedback_Regression", candidate!.Message);
            Assert.Equal(FeedbackPriority.HealthWarning, candidate!.Priority);
            Assert.True(context.IsSafetyFreezeActive);
            Assert.True(context.IsHealthRiskActive);
        }

        [Fact]
        public void ProgressionFeedbackMapper_MapsProgressionToProgressionUpdate()
        {
            var mapper = new ProgressionFeedbackMapper();
            var decision = new ProgressionOrchestratorDecision
            {
                Kind = ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                ReasonCode = "RESONANCE_FIRST"
            };

            var candidate = mapper.Map(decision);

            Assert.NotNull(candidate);
            Assert.Equal("ProgressionFeedback_Update", candidate!.Message);
            Assert.Equal(FeedbackPriority.ProgressionUpdate, candidate!.Priority);
            Assert.Equal("ProgressionOrchestrator", candidate.Source);
        }

        [Fact]
        public void ProgressionFeedbackMapper_MapsExerciseVariationToVariationMessage()
        {
            var mapper = new ProgressionFeedbackMapper();
            var decision = new ProgressionOrchestratorDecision
            {
                Kind = ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                Dimension = ProgressionAdjustmentDimension.ExerciseVariation,
                ReasonCode = "EXERCISE_VARIATION_RECOMMENDED"
            };

            var candidate = mapper.Map(decision);

            Assert.NotNull(candidate);
            Assert.Equal("ProgressionFeedback_Variation", candidate!.Message);
            Assert.Equal(FeedbackPriority.ProgressionUpdate, candidate.Priority);
        }

        [Fact]
        public void VocalHealthFeedbackMapper_MapsLockToSafetyFreeze()
        {
            var mapper = new VocalHealthFeedbackMapper();
            var decision = new VocalHealthDecision
            {
                State = HealthSafetyState.Lock,
                ReasonCode = "HEALTH_LOCK",
                StrainDetected = true,
                PauseRecommended = true
            };

            var candidate = mapper.Map(decision);
            var context = mapper.BuildContext(decision);

            Assert.NotNull(candidate);
            Assert.Equal("VoiceHealthFeedback_Lock", candidate!.Message);
            Assert.Equal(FeedbackPriority.SafetyFreeze, candidate.Priority);
            Assert.True(context.IsSafetyFreezeActive);
            Assert.True(context.IsHealthRiskActive);
        }

        [Fact]
        public void VocalHealthFeedbackMapper_MapsHydrationAsSupportiveSuggestion()
        {
            var mapper = new VocalHealthFeedbackMapper();
            var decision = new VocalHealthDecision
            {
                State = HealthSafetyState.Normal,
                ReasonCode = "HYDRATION_SUGGESTED",
                HydrationSuggested = true
            };

            var candidate = mapper.Map(decision);
            var context = mapper.BuildContext(decision);

            Assert.NotNull(candidate);
            Assert.Equal("VoiceHealthFeedback_Hydration", candidate!.Message);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, candidate.Priority);
            Assert.False(context.IsSafetyFreezeActive);
            Assert.False(context.IsPauseRecommended);
        }

        [Fact]
        public void Submit_IsThreadSafeForSimultaneousEvents()
        {
            var guard = new FeedbackConsistencyGuard(minimumInterval: TimeSpan.Zero);
            var approved = 0;
            guard.FeedbackApproved += _ => System.Threading.Interlocked.Increment(ref approved);

            System.Threading.Tasks.Parallel.For(0, 25, i =>
            {
                guard.Submit(Candidate(FeedbackPriority.TechniqueCorrection, $"TECHNIQUE_{i}"));
            });

            Assert.Equal(25, approved);
        }
    }
}
