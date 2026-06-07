using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Systematic documentation of the clinical feedback suppression matrix.
    /// Axes: (clinical context flag) x (candidate priority).
    /// Invariant: when Fatigue, ActiveStrain, PauseRecommended or any health/safety
    /// context is active, no progression cheer and no "keep going" praise gets through;
    /// a SafetyFreeze candidate always survives because safety wins over everything.
    /// </summary>
    public class FeedbackPriorityMatrixTests
    {
        // Deterministic clock far past the rate-limit window so only the clinical
        // suppression matrix is exercised, never the rate limiter.
        private static FeedbackConsistencyGuard NewGuard()
            => new FeedbackConsistencyGuard(
                clock: () => new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                minimumInterval: TimeSpan.Zero,
                escalationThreshold: 3);

        private static FeedbackCandidate Candidate(FeedbackPriority priority)
            => new("message", priority.ToString(), priority, MessageSeverity.Info, "MatrixSource", priority.ToString());

        private static FeedbackGuardContext Context(string contextName)
            => contextName switch
            {
                "SafetyFreeze" => new FeedbackGuardContext(IsSafetyFreezeActive: true),
                "HealthRisk" => new FeedbackGuardContext(IsHealthRiskActive: true),
                "StrainAlert" => new FeedbackGuardContext(IsActiveStrainAlert: true),
                "PauseRecommended" => new FeedbackGuardContext(IsPauseRecommended: true),
                "Fatigue" => new FeedbackGuardContext(IsFatigueActive: true),
                _ => throw new ArgumentOutOfRangeException(nameof(contextName), contextName, "Unknown context")
            };

        // =====================================================================
        // FULL SUPPRESSION MATRIX
        // expectSuppressed = true  -> candidate is blocked by the clinical context
        // expectSuppressed = false -> candidate passes the clinical gate (Approved)
        // =====================================================================
        [Theory]
        // --- SafetyFreeze: suppresses EVERYTHING below SafetyFreeze ---------
        [InlineData("SafetyFreeze", FeedbackPriority.ProgressionUpdate, true)]
        [InlineData("SafetyFreeze", FeedbackPriority.PerformancePraise, true)]
        [InlineData("SafetyFreeze", FeedbackPriority.TechniqueCorrection, true)]
        [InlineData("SafetyFreeze", FeedbackPriority.HydrationSuggestion, true)]
        [InlineData("SafetyFreeze", FeedbackPriority.PauseRecommendation, true)]
        [InlineData("SafetyFreeze", FeedbackPriority.ActiveStrainAlert, true)]
        [InlineData("SafetyFreeze", FeedbackPriority.HealthWarning, true)]
        // --- HealthRisk: suppresses progression cheer + praise --------------
        [InlineData("HealthRisk", FeedbackPriority.ProgressionUpdate, true)]
        [InlineData("HealthRisk", FeedbackPriority.PerformancePraise, true)]
        [InlineData("HealthRisk", FeedbackPriority.TechniqueCorrection, false)]
        [InlineData("HealthRisk", FeedbackPriority.HydrationSuggestion, false)]
        // --- StrainAlert: suppresses progression cheer + praise -------------
        [InlineData("StrainAlert", FeedbackPriority.ProgressionUpdate, true)]
        [InlineData("StrainAlert", FeedbackPriority.PerformancePraise, true)]
        [InlineData("StrainAlert", FeedbackPriority.TechniqueCorrection, false)]
        [InlineData("StrainAlert", FeedbackPriority.HydrationSuggestion, false)]
        // --- PauseRecommended: NO cheer, NO praise, NO technique "keep going"
        [InlineData("PauseRecommended", FeedbackPriority.ProgressionUpdate, true)]
        [InlineData("PauseRecommended", FeedbackPriority.PerformancePraise, true)]
        [InlineData("PauseRecommended", FeedbackPriority.TechniqueCorrection, true)]
        [InlineData("PauseRecommended", FeedbackPriority.HydrationSuggestion, false)]
        // --- Fatigue: NO progression cheer, NO praise ----------------------
        [InlineData("Fatigue", FeedbackPriority.ProgressionUpdate, true)]
        [InlineData("Fatigue", FeedbackPriority.PerformancePraise, true)]
        [InlineData("Fatigue", FeedbackPriority.TechniqueCorrection, false)]
        [InlineData("Fatigue", FeedbackPriority.HydrationSuggestion, false)]
        public void SuppressionMatrix_BlocksCheersWhenClinicalContextActive(
            string contextName,
            FeedbackPriority priority,
            bool expectSuppressed)
        {
            var guard = NewGuard();

            var decision = guard.Submit(Candidate(priority), Context(contextName));

            if (expectSuppressed)
            {
                Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            }
            else
            {
                Assert.Equal(FeedbackDecisionKind.Approved, decision.Kind);
            }
        }

        // =====================================================================
        // CORE INVARIANT: under Fatigue/Strain/Pause/Health there is NEVER a
        // progression cheer ("ProgressionUpdate") and NEVER a "keep going"
        // praise ("PerformancePraise"). This is the watertight clinical rule.
        // =====================================================================
        [Theory]
        [InlineData("HealthRisk")]
        [InlineData("StrainAlert")]
        [InlineData("PauseRecommended")]
        [InlineData("Fatigue")]
        public void NoProgressionCheer_NorPraise_WhenStrainOrFatigueOrPauseActive(string contextName)
        {
            var context = Context(contextName);

            var progression = NewGuard().Submit(Candidate(FeedbackPriority.ProgressionUpdate), context);
            var praise = NewGuard().Submit(Candidate(FeedbackPriority.PerformancePraise), context);

            Assert.Equal(FeedbackDecisionKind.Suppressed, progression.Kind);
            Assert.Equal(FeedbackDecisionKind.Suppressed, praise.Kind);
        }

        // =====================================================================
        // SAFETY WINS: a SafetyFreeze candidate is approved under EVERY context.
        // =====================================================================
        [Theory]
        [InlineData("SafetyFreeze")]
        [InlineData("HealthRisk")]
        [InlineData("StrainAlert")]
        [InlineData("PauseRecommended")]
        [InlineData("Fatigue")]
        public void SafetyFreezeCandidate_AlwaysPasses_RegardlessOfContext(string contextName)
        {
            var guard = NewGuard();

            var decision = guard.Submit(Candidate(FeedbackPriority.SafetyFreeze), Context(contextName));

            Assert.Equal(FeedbackDecisionKind.Approved, decision.Kind);
        }

        // =====================================================================
        // ESCALATION is unchanged: a repeatedly-suppressed candidate escalates
        // once the suppression count reaches the threshold.
        // =====================================================================
        [Fact]
        public void RepeatedClinicalSuppression_EscalatesAtThreshold()
        {
            var guard = NewGuard();
            var context = Context("Fatigue");
            var candidate = Candidate(FeedbackPriority.PerformancePraise);

            var first = guard.Submit(candidate, context);
            var second = guard.Submit(candidate, context);
            var third = guard.Submit(candidate, context);

            Assert.Equal(FeedbackDecisionKind.Suppressed, first.Kind);
            Assert.Equal(FeedbackDecisionKind.Suppressed, second.Kind);
            Assert.Equal(FeedbackDecisionKind.Escalated, third.Kind);
        }

        [Fact]
        public void PauseRecommendation_SuppressesPraiseWithDedicatedReason()
        {
            var guard = NewGuard();

            var decision = guard.Submit(
                Candidate(FeedbackPriority.PerformancePraise),
                Context("PauseRecommended"));

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Pause recommendation", decision.Reason);
        }

        [Fact]
        public void Fatigue_SuppressesPraiseWithDedicatedReason()
        {
            var guard = NewGuard();

            var decision = guard.Submit(
                Candidate(FeedbackPriority.PerformancePraise),
                Context("Fatigue"));

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
            Assert.Contains("Fatigue", decision.Reason);
        }
    }
}
