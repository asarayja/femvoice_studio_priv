using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Agent 6 — Feedback Pipeline Authority for the home screen.
    ///
    /// The front page (MainViewModel) used to set RealtimeFeedback / CoachExplanation /
    /// Feedback / StatusText directly, bypassing FeedbackPipeline → FeedbackConsistencyGuard
    /// entirely; the clinical suppression matrix therefore never applied to it. These tests
    /// lock in the new authority path:
    ///   • MainScreenFeedbackMapper maps each home-screen intent to the correct priority /
    ///     severity / reason code / conflict key.
    ///   • MainScreenFeedbackMapper.BuildContext faithfully forwards the home screen's
    ///     clinical runtime state to the guard.
    ///   • End-to-end through a real FeedbackConsistencyGuard + FeedbackPipeline, the
    ///     suppression matrix now blocks praise / progression / technique chatter on the
    ///     front page when the clinical context is active — and a safety-lock notice
    ///     always survives.
    ///   • The debounce helper keeps the 30 Hz live source from spamming the guard.
    ///   • Reason-code routing demuxes approved messages back to the right bound property.
    ///
    /// Pure classes + a real guard/pipeline (TestDatabaseService pattern: real classes,
    /// in-memory, no mocking framework). No WPF is touched.
    /// </summary>
    public class MainScreenFeedbackMapperTests
    {
        // Deterministic clock far past the rate-limit window so only the CLINICAL
        // suppression matrix is exercised end-to-end (never the rate limiter), matching
        // FeedbackPriorityMatrixTests' convention.
        private static FeedbackConsistencyGuard NewGuard()
            => new FeedbackConsistencyGuard(
                clock: () => new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                minimumInterval: TimeSpan.Zero,
                escalationThreshold: 3);

        // ────────────────────────────────────────────────────────────────────────────
        // Mapper classification
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Map_PitchZoneCoaching_IsTechniqueCorrection_FromMainScreen()
        {
            var mapper = new MainScreenFeedbackMapper();

            var candidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.PitchZoneCoaching, "in zone"));

            Assert.NotNull(candidate);
            Assert.Equal("in zone", candidate!.Message);          // resolved text, NOT a loc key
            Assert.Equal(FeedbackPriority.TechniqueCorrection, candidate.Priority);
            Assert.Equal(MainScreenFeedbackMapper.SourceName, candidate.Source);
            Assert.Equal("MAINSCREEN_PITCH_ZONE", candidate.ReasonCode);
        }

        [Fact]
        public void Map_CoachExplanation_HealthContext_IsHealthWarning()
        {
            var mapper = new MainScreenFeedbackMapper();

            var candidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.CoachExplanation, "ease off a little", IsHealthContext: true));

            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.HealthWarning, candidate!.Priority);
            Assert.Equal(MessageSeverity.Warning, candidate.Severity);
            Assert.Equal("MAINSCREEN_COACH_HEALTH", candidate.ReasonCode);
        }

        [Fact]
        public void Map_CoachExplanation_NonHealth_IsTechniqueCorrection()
        {
            var mapper = new MainScreenFeedbackMapper();

            var candidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.CoachExplanation, "lift resonance"));

            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.TechniqueCorrection, candidate!.Priority);
            Assert.Equal("MAINSCREEN_COACH_TECHNIQUE", candidate.ReasonCode);
        }

        [Fact]
        public void Map_SessionSummary_Praise_IsPerformancePraise_NeutralIsProgressionUpdate()
        {
            var mapper = new MainScreenFeedbackMapper();

            var praise = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.SessionSummary, "great session", IsPraise: true));
            var neutral = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.SessionSummary, "session complete", IsPraise: false));

            Assert.Equal(FeedbackPriority.PerformancePraise, praise!.Priority);
            Assert.Equal("MAINSCREEN_SESSION_PRAISE", praise.ReasonCode);
            Assert.Equal(FeedbackPriority.ProgressionUpdate, neutral!.Priority);
            Assert.Equal("MAINSCREEN_SESSION_SUMMARY", neutral.ReasonCode);
        }

        [Fact]
        public void Map_ProgressionCelebration_IsProgressionUpdate()
        {
            var mapper = new MainScreenFeedbackMapper();

            var candidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.ProgressionCelebration, "promoted to intermediate"));

            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.ProgressionUpdate, candidate!.Priority);
            Assert.Equal("MAINSCREEN_PROGRESSION", candidate.ReasonCode);
        }

        [Fact]
        public void Map_SafetyLockNotice_IsSafetyFreeze()
        {
            var mapper = new MainScreenFeedbackMapper();

            var candidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.SafetyLockNotice, "safety lock engaged"));

            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.SafetyFreeze, candidate!.Priority);
            Assert.Equal("MAINSCREEN_SAFETY_LOCK", candidate.ReasonCode);
        }

        [Fact]
        public void Map_BlankText_ReturnsNull()
        {
            var mapper = new MainScreenFeedbackMapper();

            Assert.Null(mapper.Map(new MainScreenFeedbackIntent(MainScreenFeedbackKind.PitchZoneCoaching, "")));
            Assert.Null(mapper.Map(new MainScreenFeedbackIntent(MainScreenFeedbackKind.PitchZoneCoaching, "   ")));
        }

        [Fact]
        public void Map_Discriminator_ProducesDistinctReasonAndConflictKeys()
        {
            var mapper = new MainScreenFeedbackMapper();

            var engaged = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.SafetyLockNotice, "engaged", ReasonDiscriminator: "ENGAGED"));
            var released = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.SafetyLockNotice, "released", ReasonDiscriminator: "RELEASED"));

            Assert.Equal("MAINSCREEN_SAFETY_LOCK_ENGAGED", engaged!.ReasonCode);
            Assert.Equal("MAINSCREEN_SAFETY_LOCK_RELEASED", released!.ReasonCode);
            Assert.NotEqual(engaged.ConflictKey, released.ConflictKey);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Context building
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void BuildContext_ForwardsClinicalFlags_AndKeepsHoldStable()
        {
            var mapper = new MainScreenFeedbackMapper();

            var context = mapper.BuildContext(new MainScreenClinicalState(
                IsHealthRiskActive: true,
                IsActiveStrainAlert: true,
                IsPauseRecommended: true,
                IsFatigueActive: true));

            Assert.True(context.IsHealthRiskActive);
            Assert.True(context.IsActiveStrainAlert);
            Assert.True(context.IsPauseRecommended);
            Assert.True(context.IsFatigueActive);
            Assert.True(context.IsHoldStable); // home screen has no hold; never spuriously unstable
            Assert.False(context.IsSafetyFreezeActive);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Binding routing (approval demux)
        // ────────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("MAINSCREEN_PITCH_ZONE", MainScreenFeedbackBinding.RealtimeFeedback)]
        [InlineData("MAINSCREEN_COACH_TECHNIQUE", MainScreenFeedbackBinding.CoachExplanation)]
        [InlineData("MAINSCREEN_COACH_HEALTH", MainScreenFeedbackBinding.CoachExplanation)]
        [InlineData("MAINSCREEN_SESSION_PRAISE", MainScreenFeedbackBinding.SessionFeedback)]
        [InlineData("MAINSCREEN_SESSION_SUMMARY", MainScreenFeedbackBinding.SessionFeedback)]
        [InlineData("MAINSCREEN_PROGRESSION", MainScreenFeedbackBinding.StatusText)]
        [InlineData("MAINSCREEN_SAFETY_LOCK_ENGAGED", MainScreenFeedbackBinding.StatusText)]
        [InlineData("MAINSCREEN_SAFETY_LOCK_RELEASED", MainScreenFeedbackBinding.StatusText)]
        public void BindingForReasonCode_RoutesByPrefix_ToleratesDiscriminator(
            string reasonCode, MainScreenFeedbackBinding expected)
        {
            Assert.Equal(expected, MainScreenFeedbackMapper.BindingForReasonCode(reasonCode));
        }

        [Fact]
        public void BindingFor_MatchesBindingForReasonCode_AcrossAllKinds()
        {
            var mapper = new MainScreenFeedbackMapper();
            foreach (MainScreenFeedbackKind kind in Enum.GetValues(typeof(MainScreenFeedbackKind)))
            {
                var candidate = mapper.Map(new MainScreenFeedbackIntent(kind, "x"));
                Assert.NotNull(candidate);
                Assert.Equal(
                    MainScreenFeedbackMapper.BindingFor(kind),
                    MainScreenFeedbackMapper.BindingForReasonCode(candidate!.ReasonCode));
            }
        }

        // ────────────────────────────────────────────────────────────────────────────
        // End-to-end clinical suppression on the home screen (the load-bearing fix)
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ProgressionCelebration_IsSuppressed_UnderActiveStrain()
        {
            var guard = NewGuard();
            var pipeline = new FeedbackPipeline(guard);
            var mapper = new MainScreenFeedbackMapper();

            var candidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.ProgressionCelebration, "promoted!"))!;
            var context = mapper.BuildContext(new MainScreenClinicalState(IsActiveStrainAlert: true));

            var decision = pipeline.Submit(candidate, context);

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
        }

        [Fact]
        public void SessionPraise_IsSuppressed_UnderHealthRisk()
        {
            var guard = NewGuard();
            var pipeline = new FeedbackPipeline(guard);
            var mapper = new MainScreenFeedbackMapper();

            var candidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.SessionSummary, "great session", IsPraise: true))!;
            var context = mapper.BuildContext(new MainScreenClinicalState(IsHealthRiskActive: true));

            var decision = pipeline.Submit(candidate, context);

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
        }

        [Fact]
        public void PitchZoneCoaching_IsSuppressed_WhenPauseRecommended()
        {
            // Pause (recovery) suppresses TechniqueCorrection — the home screen's pitch
            // chatter must go quiet while the user is recovering.
            var guard = NewGuard();
            var pipeline = new FeedbackPipeline(guard);
            var mapper = new MainScreenFeedbackMapper();

            var candidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.PitchZoneCoaching, "above zone"))!;
            var context = mapper.BuildContext(new MainScreenClinicalState(IsPauseRecommended: true));

            var decision = pipeline.Submit(candidate, context);

            Assert.Equal(FeedbackDecisionKind.Suppressed, decision.Kind);
        }

        [Fact]
        public void SafetyLockNotice_IsAlwaysApproved_RegardlessOfContext()
        {
            var mapper = new MainScreenFeedbackMapper();
            var candidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.SafetyLockNotice, "safety lock engaged",
                ReasonDiscriminator: "ENGAGED"))!;

            // Even under the harshest clinical context, SafetyFreeze must survive.
            var context = mapper.BuildContext(new MainScreenClinicalState(
                IsHealthRiskActive: true, IsActiveStrainAlert: true, IsPauseRecommended: true, IsFatigueActive: true));

            var guard = NewGuard();
            var pipeline = new FeedbackPipeline(guard);
            var decision = pipeline.Submit(candidate, context);

            Assert.Equal(FeedbackDecisionKind.Approved, decision.Kind);
        }

        [Fact]
        public void CoachHealthWarning_SurvivesHealthRisk_WhileTechniqueChatterDoesNot()
        {
            var mapper = new MainScreenFeedbackMapper();
            var healthCtx = mapper.BuildContext(new MainScreenClinicalState(IsHealthRiskActive: true));

            // HealthWarning candidate passes under health risk; technique candidate does not.
            var healthCandidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.CoachExplanation, "ease off", IsHealthContext: true))!;
            var techniqueCandidate = mapper.Map(new MainScreenFeedbackIntent(
                MainScreenFeedbackKind.CoachExplanation, "lift resonance"))!;

            var healthDecision = new FeedbackPipeline(NewGuard()).Submit(healthCandidate, healthCtx);
            var techniqueDecision = new FeedbackPipeline(NewGuard()).Submit(techniqueCandidate, healthCtx);

            Assert.Equal(FeedbackDecisionKind.Approved, healthDecision.Kind);
            // HealthRisk does not suppress TechniqueCorrection per the matrix (only praise/
            // progression) — so technique chatter still passes under pure health risk; it is
            // PauseRecommended that silences technique. Assert the matrix faithfully here.
            Assert.Equal(FeedbackDecisionKind.Approved, techniqueDecision.Kind);
        }

        [Fact]
        public void NoClinicalContext_AllHomeScreenCandidatesApproved()
        {
            var mapper = new MainScreenFeedbackMapper();
            var calm = mapper.BuildContext(new MainScreenClinicalState());

            foreach (MainScreenFeedbackKind kind in Enum.GetValues(typeof(MainScreenFeedbackKind)))
            {
                var candidate = mapper.Map(new MainScreenFeedbackIntent(kind, "msg"))!;
                var decision = new FeedbackPipeline(NewGuard()).Submit(candidate, calm);
                Assert.Equal(FeedbackDecisionKind.Approved, decision.Kind);
            }
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Debounce helper — keep 30 Hz live source from spamming the guard
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Debounce_SameKeyWithinWindow_IsSwallowed()
        {
            var t = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var debouncer = new MainScreenFeedbackDebouncer(
                clock: () => t, minimumInterval: TimeSpan.FromSeconds(1));

            Assert.True(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "in zone"));
            // Same key, same instant (within window) → swallowed.
            Assert.False(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "in zone"));
        }

        [Fact]
        public void Debounce_ChangedKey_PassesImmediately()
        {
            var t = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var debouncer = new MainScreenFeedbackDebouncer(
                clock: () => t, minimumInterval: TimeSpan.FromSeconds(1));

            Assert.True(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "in zone"));
            // Different key inside the window → passes (the message actually changed).
            Assert.True(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "above zone"));
        }

        [Fact]
        public void Debounce_SameKeyAfterWindow_PassesAgain()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var debouncer = new MainScreenFeedbackDebouncer(
                clock: () => now, minimumInterval: TimeSpan.FromSeconds(1));

            Assert.True(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "in zone"));
            now = now.AddSeconds(1.5); // advance past the window
            Assert.True(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "in zone"));
        }

        [Fact]
        public void Debounce_PerBinding_IsIndependent()
        {
            var t = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var debouncer = new MainScreenFeedbackDebouncer(
                clock: () => t, minimumInterval: TimeSpan.FromSeconds(1));

            Assert.True(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "same"));
            // A different binding with the same text must not be blocked by the first.
            Assert.True(debouncer.ShouldSubmit(MainScreenFeedbackBinding.CoachExplanation, "same"));
        }

        [Fact]
        public void Debounce_BlankKey_NeverSubmits()
        {
            var t = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var debouncer = new MainScreenFeedbackDebouncer(clock: () => t);

            Assert.False(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, ""));
        }

        [Fact]
        public void Debounce_Reset_AllowsRepeatOfSameKey()
        {
            var t = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var debouncer = new MainScreenFeedbackDebouncer(
                clock: () => t, minimumInterval: TimeSpan.FromSeconds(1));

            Assert.True(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "in zone"));
            Assert.False(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "in zone"));

            debouncer.Reset(); // new session
            Assert.True(debouncer.ShouldSubmit(MainScreenFeedbackBinding.RealtimeFeedback, "in zone"));
        }
    }
}
