using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// AGENT 10 — PROGRESSION AUTHORITY.
    ///
    /// Proves that the progression subsystem (ProgressionOrchestrator, ProgressionFeedbackMapper,
    /// ProgressionSafetyGate, MasteryEvaluator) is wired through the SINGLE feedback authority
    /// (FeedbackPipeline → FeedbackConsistencyGuard) and that progression can NEVER override the
    /// Safety &gt; Health &gt; Recovery part of the clinical hierarchy.
    ///
    /// Method: the real production mapper + real guard are exercised together (no mocks; the
    /// in-memory SessionAnalyticsStore repository is the only fake, per the TestDatabaseService
    /// pattern). Each test asserts on the guard's actual decision, not on a re-implemented matrix.
    ///
    /// Mandate coverage:
    ///   1. Orchestrator decisions → mapper → pipeline: recovery messages (RegressionTriggered /
    ///      ProgressionPaused) PASS under strain; promotion (ProfileUpdated) is SUPPRESSED.
    ///   2. ProgressionSafetyGate block → RecoveryActivationPolicy → TargetProfileAdapter:
    ///      a blocked gate shrinks (never grows) the target zone.
    ///   3. MasteryEvaluator persisted-history gates (covered in MasteryEvaluatorTests; one
    ///      cross-check here that a fresh promotion-eligible exercise still can't bypass the
    ///      analytics-row requirement).
    ///   4. Ordering: when a health/recovery decision and a progression escalation are submitted
    ///      together (SubmitMany), health wins and progression is suppressed.
    /// </summary>
    public class ProgressionAuthorityTests
    {
        // Frozen clock so the guard's rate-limiter and the gate's rolling windows are deterministic.
        private static readonly DateTime Clock = new DateTime(2026, 6, 6, 12, 0, 0, DateTimeKind.Utc);
        private const int ExerciseId = 10;

        // ─────────────────────────────────────────────────────────────────────────
        // 1. Orchestrator decision → ProgressionFeedbackMapper → guard
        //    Recovery-flavoured progression messages survive strain; promotion does not.
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void RegressionTriggered_MapsToHealthWarning_AndSurvivesActiveStrain()
        {
            var mapper = new ProgressionFeedbackMapper();
            var guard = NewGuard();

            var decision = Decision(
                ProgressionOrchestratorDecisionKind.RegressionTriggered,
                ProgressionAdjustmentDimension.Recovery,
                "PERFORMANCE_REGRESSION");

            var candidate = mapper.Map(decision);
            Assert.NotNull(candidate);
            // Regression is a recovery/health message — it must be at HealthWarning priority.
            Assert.Equal(FeedbackPriority.HealthWarning, candidate!.Priority);

            // Submit under a hostile clinical context: strain + health risk + pause all active.
            var hostile = new FeedbackGuardContext(
                IsHealthRiskActive: true,
                IsActiveStrainAlert: true,
                IsPauseRecommended: true,
                IsHoldStable: false);

            var result = guard.Submit(candidate, hostile);

            // The recovery message MUST pass — health-level feedback is never self-suppressed.
            Assert.Equal(FeedbackDecisionKind.Approved, result.Kind);
        }

        [Fact]
        public void ProgressionPaused_MapsToPauseRecommendation_AndSurvivesActiveStrain()
        {
            var mapper = new ProgressionFeedbackMapper();
            var guard = NewGuard();

            var decision = Decision(
                ProgressionOrchestratorDecisionKind.ProgressionPaused,
                ProgressionAdjustmentDimension.Recovery,
                "FATIGUE_RISING");

            var candidate = mapper.Map(decision);
            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.PauseRecommendation, candidate!.Priority);

            // Strain + fatigue active: the suppression matrix only kills <= PerformancePraise,
            // so a PauseRecommendation(50) message survives.
            var hostile = new FeedbackGuardContext(
                IsActiveStrainAlert: true,
                IsFatigueActive: true,
                IsHoldStable: false);

            var result = guard.Submit(candidate, hostile);

            Assert.Equal(FeedbackDecisionKind.Approved, result.Kind);
        }

        [Fact]
        public void ProfileUpdate_MapsToProgressionUpdate_AndIsSuppressedUnderStrain()
        {
            var mapper = new ProgressionFeedbackMapper();
            var guard = NewGuard();

            var decision = Decision(
                ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                ProgressionAdjustmentDimension.Resonance,
                "RESONANCE_PROGRESSION");

            var candidate = mapper.Map(decision);
            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.ProgressionUpdate, candidate!.Priority);

            // Active strain must suppress a progression update (ProgressionUpdate <= PerformancePraise).
            var strain = new FeedbackGuardContext(IsActiveStrainAlert: true);
            var result = guard.Submit(candidate, strain);

            Assert.NotEqual(FeedbackDecisionKind.Approved, result.Kind);
        }

        [Theory]
        // Every clinical context that must veto a neutral progression update.
        [InlineData(true, false, false, false)]   // health risk
        [InlineData(false, true, false, false)]   // active strain
        [InlineData(false, false, true, false)]   // pause recommended
        [InlineData(false, false, false, true)]   // fatigue active
        public void ProgressionUpdate_IsSuppressed_UnderEveryRecoveryContext(
            bool healthRisk, bool strain, bool pause, bool fatigue)
        {
            var mapper = new ProgressionFeedbackMapper();
            var guard = NewGuard();

            var candidate = mapper.Map(Decision(
                ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                ProgressionAdjustmentDimension.Stability,
                "STABILITY_ENDURANCE"));
            Assert.NotNull(candidate);

            var context = new FeedbackGuardContext(
                IsHealthRiskActive: healthRisk,
                IsActiveStrainAlert: strain,
                IsPauseRecommended: pause,
                IsFatigueActive: fatigue);

            var result = guard.Submit(candidate!, context);

            Assert.NotEqual(FeedbackDecisionKind.Approved, result.Kind);
        }

        [Fact]
        public void ExerciseVariation_MapsToProgressionUpdate_AndIsSuppressedUnderPause()
        {
            // The ExerciseVariation branch is a separate code path in the mapper (it short-circuits
            // before the Kind switch); verify it too is a low-priority update that the guard vetoes.
            var mapper = new ProgressionFeedbackMapper();
            var guard = NewGuard();

            var candidate = mapper.Map(Decision(
                ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                ProgressionAdjustmentDimension.ExerciseVariation,
                "EXERCISE_VARIATION_RECOMMENDED"));
            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.ProgressionUpdate, candidate!.Priority);

            var result = guard.Submit(candidate, new FeedbackGuardContext(IsPauseRecommended: true));

            Assert.NotEqual(FeedbackDecisionKind.Approved, result.Kind);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 1b. End-to-end: a real orchestrator decision (not a hand-built record) routed
        //     through the real mapper + guard, proving the wiring holds for a genuine
        //     RegressionTriggered produced from persisted history.
        //
        // Two distinct regression sub-cases, both clinically correct:
        //   • SAFETY_EVENTS regression: BuildContext flags IsSafetyFreezeActive, and the
        //     HealthWarning(70) notice YIELDS to that safety-freeze context (Safety(80) is
        //     the authoritative message; progression's own notice must not compete with it).
        //     This is the strongest possible demonstration of Safety > Progression.
        //   • PERFORMANCE_REGRESSION (no safety freeze): the HealthWarning(70) recovery
        //     message SURVIVES even a hostile strain/fatigue context.
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task RealOrchestratorSafetyEventRegression_YieldsToItsOwnSafetyFreezeContext()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var orchestrator = new ProgressionOrchestrator(store, new ProgressionOrchestratorOptions
            {
                MinimumSessionsForDecision = 3,
                MaxSafetyEventsBeforeRegression = 2,
                MaxFatigueIndicatorsBeforePause = 2
            });

            await SeedImprovingSeries(store, ExerciseId, Clock);
            // Two safety freezes drive a real RegressionTriggered("SAFETY_EVENTS") decision.
            await store.RecordHealthEventAsync(SafetyFreeze(1, Clock.AddDays(-2)));
            await store.RecordHealthEventAsync(SafetyFreeze(2, Clock.AddDays(-1)));

            var decision = await orchestrator.EvaluateAsync(new ProgressionOrchestratorContext
            {
                ExerciseId = ExerciseId,
                CurrentProfile = ExerciseTargetProfile.CreateResonanceHumming(),
                EvaluationTime = Clock
            });

            Assert.Equal(ProgressionOrchestratorDecisionKind.RegressionTriggered, decision.Kind);
            Assert.Equal("SAFETY_EVENTS", decision.ReasonCode);

            var mapper = new ProgressionFeedbackMapper();
            var candidate = mapper.Map(decision)!;
            var context = mapper.BuildContext(decision);

            // The candidate is HealthWarning(70); its own context is a safety-freeze.
            Assert.Equal(FeedbackPriority.HealthWarning, candidate.Priority);
            Assert.True(context.IsSafetyFreezeActive);

            var guard = NewGuard();
            var result = guard.Submit(candidate, context);

            // Safety(80) is the only priority that survives a safety-freeze context: the
            // progression-sourced HealthWarning(70) notice correctly yields. The authoritative
            // SafetyFreeze Lock message comes from the VocalHealth path, not from progression.
            Assert.NotEqual(FeedbackDecisionKind.Approved, result.Kind);
        }

        [Fact]
        public async Task RealOrchestratorPerformanceRegression_RoutedThroughMapperAndGuard_SurvivesStrain()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var orchestrator = new ProgressionOrchestrator(store, new ProgressionOrchestratorOptions
            {
                MinimumSessionsForDecision = 3,
                RegressionThreshold = 0.08,
                MaxSafetyEventsBeforeRegression = 2,
                MaxFatigueIndicatorsBeforePause = 2
            });

            // A genuine performance drop (strong baseline, weak recent) — no safety events.
            await AddPerf(store, 1, Clock.AddDays(-6), 0.78, 0.76, 0.85);
            await AddPerf(store, 2, Clock.AddDays(-5), 0.77, 0.75, 0.84);
            await AddPerf(store, 3, Clock.AddDays(-4), 0.76, 0.74, 0.83);
            await AddPerf(store, 4, Clock.AddDays(-3), 0.55, 0.54, 0.60);
            await AddPerf(store, 5, Clock.AddDays(-2), 0.54, 0.53, 0.59);
            await AddPerf(store, 6, Clock.AddDays(-1), 0.53, 0.52, 0.58);

            var decision = await orchestrator.EvaluateAsync(new ProgressionOrchestratorContext
            {
                ExerciseId = ExerciseId,
                CurrentProfile = ExerciseTargetProfile.CreateResonanceHumming(),
                EvaluationTime = Clock
            });

            Assert.Equal(ProgressionOrchestratorDecisionKind.RegressionTriggered, decision.Kind);
            Assert.Equal("PERFORMANCE_REGRESSION", decision.ReasonCode);

            var mapper = new ProgressionFeedbackMapper();
            var candidate = mapper.Map(decision)!;
            var context = mapper.BuildContext(decision);

            // Health-risk context (not safety-freeze): a HealthWarning(70) survives it.
            Assert.Equal(FeedbackPriority.HealthWarning, candidate.Priority);
            Assert.True(context.IsHealthRiskActive);
            Assert.False(context.IsSafetyFreezeActive);

            // Add hostile strain on top — the recovery message must still pass.
            var hostile = context with { IsActiveStrainAlert = true, IsHoldStable = false };
            var guard = NewGuard();
            var result = guard.Submit(candidate, hostile);

            Assert.Equal(FeedbackDecisionKind.Approved, result.Kind);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 4. ORDERING — progression can never beat health when both arrive together.
        //    SubmitMany orders by priority DESC; the first approved wins, the rest are
        //    suppressed. A progression update submitted alongside a regression/health
        //    message must lose.
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void SubmitMany_HealthAndProgressionTogether_HealthWinsProgressionSuppressed()
        {
            var mapper = new ProgressionFeedbackMapper();
            var guard = NewGuard();

            var regression = mapper.Map(Decision(
                ProgressionOrchestratorDecisionKind.RegressionTriggered,
                ProgressionAdjustmentDimension.Recovery,
                "PERFORMANCE_REGRESSION"))!;
            var promotion = mapper.Map(Decision(
                ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                ProgressionAdjustmentDimension.PitchComfort,
                "PITCH_AFTER_RESONANCE_STABILITY"))!;

            // Order the inputs "wrong" (progression first) to prove SubmitMany re-orders by priority.
            var decisions = guard.SubmitMany(new[] { promotion, regression });

            var regressionDecision = decisions.Single(d => d.Candidate.ReasonCode == "PERFORMANCE_REGRESSION");
            var promotionDecision = decisions.Single(d => d.Candidate.ReasonCode == "PITCH_AFTER_RESONANCE_STABILITY");

            Assert.Equal(FeedbackDecisionKind.Approved, regressionDecision.Kind);
            Assert.NotEqual(FeedbackDecisionKind.Approved, promotionDecision.Kind);
        }

        [Fact]
        public void SubmitMany_PauseAndPromotionTogether_PauseWinsEvenInputOrderFavoursPromotion()
        {
            var mapper = new ProgressionFeedbackMapper();
            var guard = NewGuard();

            var pause = mapper.Map(Decision(
                ProgressionOrchestratorDecisionKind.ProgressionPaused,
                ProgressionAdjustmentDimension.Recovery,
                "FATIGUE_RISING"))!;
            var promotion = mapper.Map(Decision(
                ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                ProgressionAdjustmentDimension.Resonance,
                "RESONANCE_PROGRESSION"))!;

            var decisions = guard.SubmitMany(new[] { promotion, pause });

            var pauseDecision = decisions.Single(d => d.Candidate.ReasonCode == "FATIGUE_RISING");
            var promotionDecision = decisions.Single(d => d.Candidate.ReasonCode == "RESONANCE_PROGRESSION");

            Assert.Equal(FeedbackDecisionKind.Approved, pauseDecision.Kind);
            Assert.NotEqual(FeedbackDecisionKind.Approved, promotionDecision.Kind);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 2. ProgressionSafetyGate block → RecoveryActivationPolicy → TargetProfileAdapter.
        //    A blocked gate forces recovery, which can only SHRINK target requirements.
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task BlockedGate_ActivatesRecovery_WhichShrinksExerciseProfile()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var gate = new ProgressionSafetyGate(store);

            // Two safety freezes this week → REPEATED_SAFETY_LOCKS block.
            await store.RecordHealthEventAsync(SafetyFreeze(1, Clock.AddDays(-1)));
            await store.RecordHealthEventAsync(SafetyFreeze(2, Clock.AddDays(-3)));

            var gateResult = await gate.EvaluateAsync(Clock);
            Assert.True(gateResult.IsBlocked);
            Assert.Equal("REPEATED_SAFETY_LOCKS", gateResult.ReasonCode);

            // No persisted override applied → policy says recovery must be activated here.
            var recoveryActive = RecoveryActivationPolicy.ForExerciseProfile(
                gateBlocked: gateResult.IsBlocked, overrideApplied: false);
            Assert.True(recoveryActive);

            var adapter = new TargetProfileAdapter();
            var user = new UserVoiceProfile { PreferredVoiceStyle = VoiceStyleGoal.Feminine };
            var baseProfile = ExerciseTargetProfile.PitchExercise(
                minPitch: 160, maxPitch: 220,
                targetResonanceMin: 0.55, targetResonanceMax: 0.85,
                stabilityThreshold: 0.60, requiredHoldSeconds: 4);

            var recovered = adapter.Personalize(baseProfile, user, recoveryActive);

            // Recovery can only shrink: every requirement is <= the base, and the pitch zone narrows.
            Assert.True(recovered.StabilityThreshold <= baseProfile.StabilityThreshold);
            Assert.True(recovered.RequiredHoldSeconds <= baseProfile.RequiredHoldSeconds);
            Assert.True(recovered.TargetResonanceMin <= baseProfile.TargetResonanceMin);
            Assert.True(recovered.MinPitch!.Value >= baseProfile.MinPitch!.Value);
            Assert.True(recovered.MaxPitch!.Value <= baseProfile.MaxPitch!.Value);
            // Strictly narrower zone width.
            Assert.True(
                recovered.MaxPitch.Value - recovered.MinPitch.Value
                < baseProfile.MaxPitch.Value - baseProfile.MinPitch.Value);
        }

        [Fact]
        public async Task ClearGate_DoesNotActivateRecovery_ProfileUnshrunk()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var gate = new ProgressionSafetyGate(store);

            // No events → CLEAR.
            var gateResult = await gate.EvaluateAsync(Clock);
            Assert.False(gateResult.IsBlocked);

            var recoveryActive = RecoveryActivationPolicy.ForHomeZone(gateResult.IsBlocked);
            Assert.False(recoveryActive);

            // With recovery off, the home pitch zone is not shrunk.
            var baseZone = (min: 165.0, max: 255.0);
            var (min, max) = TargetProfileAdapter.PersonalizePitchZone(baseZone, user: null, recoveryActive);

            Assert.Equal(baseZone.min, min);
            Assert.Equal(baseZone.max, max);
        }

        [Fact]
        public void OverrideAlreadyApplied_DoesNotDoubleScaleRecovery_OnExerciseProfile()
        {
            // Guards the documented double-scaling nuance: when a persisted override is already
            // recovery-scaled, the exercise-profile policy must NOT re-activate recovery.
            Assert.False(RecoveryActivationPolicy.ForExerciseProfile(gateBlocked: true, overrideApplied: true));
            // But the home zone (built from the unscaled policy zone) must still shrink on block.
            Assert.True(RecoveryActivationPolicy.ForHomeZone(gateBlocked: true));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 3. MasteryEvaluator cross-check: a promotion-eligible session count can never
        //    bypass the persisted-analytics-row gate (LookbackDays 90, count-gates).
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Mastery_CannotPromoteWithoutVerifiedAnalyticsRows_EvenWith20LegacySessions()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            // Only 4 verified analytics rows exist, below MinAnalyticsSessionsRequired (5),
            // even though totalSessions (legacy attendance counter) claims 25.
            for (var i = 0; i < 4; i++)
            {
                await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
                {
                    SessionId = i + 1,
                    ExerciseId = ExerciseId,
                    StartedAt = Clock.AddDays(-(4 - i)),
                    EndedAt = Clock.AddDays(-(4 - i)).AddMinutes(5),
                    ResonanceQualityIndex = 0.80,
                    StabilityConsistency = 0.80,
                    HoldCompletionRate = 0.90
                });
            }

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 25, profile, Clock);

            // The conservative default holds: no Mastered (or even Stable) without verified history.
            Assert.Equal(MasteryLevel.Developing, result.Level);
            Assert.Equal("INSUFFICIENT_HISTORY", result.ReasonCode);
        }

        [Fact]
        public async Task Mastery_RecentSafetyLockGatesDownward_RegardlessOfStrongHistory()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var evaluator = new MasteryEvaluator(store);
            var profile = ExerciseTargetProfile.CreateResonanceHumming();

            // 22 strong verified sessions...
            for (var i = 0; i < 22; i++)
            {
                await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
                {
                    SessionId = i + 1,
                    ExerciseId = ExerciseId,
                    StartedAt = Clock.AddDays(-(22 - i)),
                    EndedAt = Clock.AddDays(-(22 - i)).AddMinutes(5),
                    ResonanceQualityIndex = 0.80,
                    StabilityConsistency = 0.80,
                    HoldCompletionRate = 0.90
                });
            }
            // ...but the most recent (1 day ago) carries a safety event.
            await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = 999,
                ExerciseId = ExerciseId,
                StartedAt = Clock.AddDays(-1),
                EndedAt = Clock.AddDays(-1).AddMinutes(5),
                ResonanceQualityIndex = 0.80,
                StabilityConsistency = 0.80,
                HoldCompletionRate = 0.90,
                SafetyEventsCount = 1
            });

            var result = await evaluator.EvaluateAsync(ExerciseId, totalSessions: 23, profile, Clock);

            // Health demotion overrides mastery — safety beats progression.
            Assert.Equal(MasteryLevel.Developing, result.Level);
            Assert.Equal("GATE_SAFETY_RECENT_LOCK", result.ReasonCode);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 5. NO AUTO-GOAL-ESCALATION — GetWeeklySessionTarget reflects ONLY the user's
        //    own chosen TrainingFrequencyPerWeek and never silently escalates above it.
        //    Progression must never inflate the user's self-set cadence (Progression is
        //    below Health/Recovery, and must never push the user harder than they chose).
        // ─────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(7)]
        public void GetWeeklySessionTarget_NeverExceedsUsersChosenTrainingFrequency(int frequency)
        {
            var db = new TestDatabaseService();
            db.SaveUserVoiceProfile(new UserVoiceProfile
            {
                UserId = 1,
                TrainingFrequencyPerWeek = frequency
            });
            var engine = new SmartCoachEngine(db);

            var target = engine.GetWeeklySessionTarget(1);

            // The target IS the user's own cadence — never auto-escalated above it.
            Assert.Equal(frequency, target);
            Assert.True(target <= frequency,
                "GetWeeklySessionTarget must never exceed the user's chosen TrainingFrequencyPerWeek.");
        }

        [Fact]
        public void GetWeeklySessionTarget_NoProfile_FallsBackToConservativeDefault_NoEscalation()
        {
            // Uten lagret profil degraderer vi til et konservativt standardmål (3), aldri
            // til en eskalert verdi. Beviser at fraværet av en profil ikke åpner for at
            // coachingen presser en høyere kadens enn brukeren noensinne har valgt.
            var db = new TestDatabaseService();   // ingen profil lagret
            var engine = new SmartCoachEngine(db);

            var target = engine.GetWeeklySessionTarget(1);

            Assert.Equal(3, target);
        }

        [Fact]
        public void GetWeeklySessionTarget_LoweredFrequency_IsHonoured_NotRatchetedUp()
        {
            // Senker brukeren sin egen kadens skal målet FØLGE nedover — progresjon kan
            // aldri «låse inn» et tidligere høyere mål (ingen oppover-ratchet).
            var db = new TestDatabaseService();
            db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, TrainingFrequencyPerWeek = 6 });
            var engine = new SmartCoachEngine(db);
            Assert.Equal(6, engine.GetWeeklySessionTarget(1));

            db.SaveUserVoiceProfile(new UserVoiceProfile { UserId = 1, TrainingFrequencyPerWeek = 2 });

            Assert.Equal(2, engine.GetWeeklySessionTarget(1));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        private static FeedbackConsistencyGuard NewGuard()
            // Fixed clock; the rate-limiter is irrelevant to the matrix tests because each guard
            // sees at most one approved candidate per ReasonCode.
            => new(() => Clock, TimeSpan.FromSeconds(2));

        private static ProgressionOrchestratorDecision Decision(
            ProgressionOrchestratorDecisionKind kind,
            ProgressionAdjustmentDimension dimension,
            string reasonCode)
            => new()
            {
                Kind = kind,
                Dimension = dimension,
                ReasonCode = reasonCode,
                Reason = reasonCode,
                Confidence = 0.9,
                SuggestedProfile = ExerciseTargetProfile.CreateResonanceHumming()
            };

        private static Task AddPerf(
            SessionAnalyticsStore store,
            int sessionId,
            DateTime startedAt,
            double resonance,
            double stability,
            double hold)
            => store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
            {
                SessionId = sessionId,
                ExerciseId = ExerciseId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(5),
                ResonanceQualityIndex = resonance,
                StabilityConsistency = stability,
                HoldCompletionRate = hold
            });

        private static HealthAnalyticsEvent SafetyFreeze(int sessionId, DateTime occurredAt)
            => new()
            {
                SessionId = sessionId,
                EventType = HealthAnalyticsEventType.SafetyFreeze,
                OccurredAt = occurredAt,
                Severity = 1,
                ReasonCode = "SAFETY_FREEZE"
            };

        private static async Task SeedImprovingSeries(SessionAnalyticsStore store, int exerciseId, DateTime now)
        {
            (double r, double s, double h)[] points =
            {
                (0.55, 0.55, 0.60),
                (0.56, 0.56, 0.62),
                (0.72, 0.70, 0.78),
                (0.73, 0.71, 0.79),
                (0.74, 0.72, 0.80)
            };
            var offsets = new[] { -6, -5, -3, -2, -1 };
            for (var i = 0; i < points.Length; i++)
            {
                var startedAt = now.AddDays(offsets[i]);
                await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
                {
                    SessionId = i + 1,
                    StartedAt = startedAt,
                    EndedAt = startedAt.AddMinutes(5),
                    ExerciseCount = 1,
                    AverageResonance = points[i].r,
                    AverageStability = points[i].s,
                    AveragePitchComfort = 0.8,
                    AverageHealthScore = 1.0
                });
                await store.RecordExercisePerformanceAsync(new ExercisePerformanceSummary
                {
                    SessionId = i + 1,
                    ExerciseId = exerciseId,
                    StartedAt = startedAt,
                    EndedAt = startedAt.AddMinutes(5),
                    ResonanceQualityIndex = points[i].r,
                    StabilityConsistency = points[i].s,
                    HoldCompletionRate = points[i].h
                });
            }
        }
    }
}
