using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// AGENT 8 — HEALTH FEEDBACK INTEGRATION.
    ///
    /// Proves the clinical-health decision engines (<see cref="VocalHealthSupervisor"/>,
    /// <see cref="HydrationAdvisor"/>) actually drive the single feedback authority
    /// (FeedbackPipeline → FeedbackConsistencyGuard) — never the UI directly — and that
    /// the resulting candidates carry the priorities mandated by the safety hierarchy:
    ///
    ///   Lock      ⇒ SafetyFreeze       (80)
    ///   Restrict  ⇒ HealthWarning      (70)
    ///   Strain    ⇒ ActiveStrainAlert  (60)
    ///   Pause     ⇒ PauseRecommendation(50)
    ///   Hydration ⇒ HydrationSuggestion(40)
    ///
    /// Strategy (no mocking frameworks):
    ///   • Real <see cref="VocalHealthSupervisor"/> / <see cref="HydrationAdvisor"/> driven
    ///     with synthetic <see cref="ExerciseLiveState"/> sequences that deterministically
    ///     trigger each decision kind (same seed sequences as the supervisor/advisor unit
    ///     tests, which are themselves green).
    ///   • Real mappers (<see cref="VocalHealthFeedbackMapper"/> /
    ///     <see cref="HydrationFeedbackMapper"/>) produce the candidate + guard context.
    ///   • A real <see cref="FeedbackPipeline"/> over a real
    ///     <see cref="FeedbackConsistencyGuard"/> (frozen clock where rate-limiting matters)
    ///     applies the recently-tightened suppression matrix.
    ///
    /// One end-to-end test additionally drives the production wiring through the real
    /// <see cref="ExerciseSessionRecorder"/> + <see cref="ExerciseIntelligenceCoordinator"/>
    /// to prove the same path fires in the assembled graph (ExerciseSessionRecorder
    /// pattern, with the coordinator's 100 ms evaluation gate respected).
    /// </summary>
    public class HealthFeedbackIntegrationTests
    {
        // A frozen clock keeps the guard's 2 s rate-limiter / active-warning window
        // deterministic: a single submit lands; rapid duplicates are rate-limited.
        private static readonly DateTime FrozenNow = new(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);

        // ── Harness ─────────────────────────────────────────────────────────────────

        private sealed class PipelineHarness
        {
            public FeedbackConsistencyGuard Guard { get; }
            public FeedbackPipeline Pipeline { get; }
            public List<FeedbackDecision> Approved { get; } = new();
            public List<FeedbackDecision> Suppressed { get; } = new();

            public PipelineHarness(Func<DateTime>? clock = null)
            {
                Guard = new FeedbackConsistencyGuard(clock: clock);
                Pipeline = new FeedbackPipeline(Guard);
                Pipeline.FeedbackApproved += (_, d) => Approved.Add(d);
                Pipeline.FeedbackSuppressed += (_, d) => Suppressed.Add(d);
                Pipeline.FeedbackEscalated += (_, d) => Suppressed.Add(d);
            }
        }

        private static ExerciseLiveState State(
            double resonance,
            double stability,
            double hold = 0.5,
            bool inComfortZone = true,
            bool safetyLocked = false,
            DateTime? timestamp = null)
            => new()
            {
                PrimaryMetricScore = resonance,
                SecondaryMetricScore = stability,
                StabilityScore = stability,
                HoldProgress = hold,
                IsInComfortZone = inComfortZone,
                IsHoldingCorrectly = hold >= 0.5,
                IsSafetyLocked = safetyLocked,
                Timestamp = timestamp ?? FrozenNow
            };

        private static VocalHealthSupervisor CreateSupervisor()
            => new(new VocalHealthSupervisorOptions
            {
                SpikeLimit = 0.25,
                StableSamplesForRecovery = 5,
                StrainSamplesForRestrict = 3,
                RestrictCyclesForLock = 3,
                BaselineResonance = 0.70,
                BaselineStability = 0.70
            });

        /// <summary>
        /// Drives the supervisor until a decision satisfies <paramref name="until"/>,
        /// returning that decision. Fails loudly if the seed sequence never reaches it,
        /// so a future change to the supervisor surfaces as a test failure here rather
        /// than a silently-wrong priority assertion.
        /// </summary>
        private static VocalHealthDecision DriveUntil(
            VocalHealthSupervisor supervisor,
            IEnumerable<ExerciseLiveState> states,
            Func<VocalHealthDecision, bool> until)
        {
            foreach (var state in states)
            {
                var decision = supervisor.Evaluate(state);
                if (until(decision))
                    return decision;
            }

            Assert.Fail("Supervisor seed sequence never produced the expected decision.");
            throw new InvalidOperationException("unreachable");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 1. Lock ⇒ SafetyFreeze(80) reaches the pipeline and is approved.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Lock_MapsToSafetyFreeze_AndIsApprovedByPipeline()
        {
            var supervisor = CreateSupervisor();
            var mapper = new VocalHealthFeedbackMapper();
            var harness = new PipelineHarness(() => FrozenNow);

            // Three consecutive safety-locked ticks escalate Restrict → Lock (proven in
            // VocalHealthSupervisorTests.Supervisor_EscalatesRestrictAndLockFromRepeatedSafetyBreaches).
            var lockDecision = DriveUntil(
                supervisor,
                new[]
                {
                    State(0.50, 0.30, safetyLocked: true),
                    State(0.50, 0.30, safetyLocked: true),
                    State(0.50, 0.30, safetyLocked: true)
                },
                d => d.State == HealthSafetyState.Lock);

            var candidate = mapper.Map(lockDecision);
            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.SafetyFreeze, candidate!.Priority);
            Assert.Equal("VocalHealthSupervisor", candidate.Source);

            var decision = harness.Pipeline.Submit(candidate, mapper.BuildContext(lockDecision));

            Assert.Equal(FeedbackDecisionKind.Approved, decision.Kind);
            Assert.Single(harness.Approved);
            Assert.Equal(FeedbackPriority.SafetyFreeze, harness.Approved[0].Candidate.Priority);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 2. Acute strain ⇒ ActiveStrainAlert(60) reaches the pipeline and is approved.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void AcuteStrain_MapsToActiveStrainAlert_AndIsApprovedByPipeline()
        {
            var supervisor = CreateSupervisor();
            var mapper = new VocalHealthFeedbackMapper();
            var harness = new PipelineHarness(() => FrozenNow);

            // Baseline, then a sudden hold collapse + stability drop → acute strain while
            // the state is still Caution and pause is not yet recommended (mirrors
            // VocalHealthSupervisorTests.Supervisor_DetectsAcuteStrainBeforeFatigue).
            supervisor.Evaluate(State(0.70, 0.70, hold: 0.80));
            var strainDecision = supervisor.Evaluate(
                State(0.65, 0.35, hold: 0.20, inComfortZone: false));

            Assert.True(strainDecision.StrainDetected);
            Assert.False(strainDecision.PauseRecommended);
            Assert.NotEqual(HealthSafetyState.Lock, strainDecision.State);
            Assert.NotEqual(HealthSafetyState.Restrict, strainDecision.State);

            var candidate = mapper.Map(strainDecision);
            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.ActiveStrainAlert, candidate!.Priority);
            Assert.Equal("VoiceHealthFeedback_Strain", candidate.Message);

            var decision = harness.Pipeline.Submit(candidate, mapper.BuildContext(strainDecision));

            Assert.Equal(FeedbackDecisionKind.Approved, decision.Kind);
            Assert.Equal(FeedbackPriority.ActiveStrainAlert, harness.Approved.Single().Candidate.Priority);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 3. Slow fatigue ⇒ Pause(50) reaches the pipeline and is approved.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void FatiguePause_MapsToPauseRecommendation_AndIsApprovedByPipeline()
        {
            var supervisor = CreateSupervisor();
            var mapper = new VocalHealthFeedbackMapper();
            var harness = new PipelineHarness(() => FrozenNow);

            // Slow meso drift produces FatigueDetected + PauseRecommended without a lock
            // (mirrors VocalHealthSupervisorTests.Supervisor_DetectsSlowFatigueFromMesoDrift).
            var states = Enumerable.Range(0, 16)
                .Select(i => State(0.70 - i * 0.020, 0.70 - i * 0.020, hold: 0.75));

            var pauseDecision = DriveUntil(
                supervisor,
                states,
                d => d.PauseRecommended
                     && d.State != HealthSafetyState.Lock
                     && d.State != HealthSafetyState.Restrict
                     && !d.StrainDetected);

            // The mapper resolves Lock → Restrict → Pause → Strain in that order; with no
            // lock/restrict/strain active, Pause is the produced candidate.
            var candidate = mapper.Map(pauseDecision);
            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.PauseRecommendation, candidate!.Priority);
            Assert.Equal("VoiceHealthFeedback_Pause", candidate.Message);

            var decision = harness.Pipeline.Submit(candidate, mapper.BuildContext(pauseDecision));

            Assert.Equal(FeedbackDecisionKind.Approved, decision.Kind);
            Assert.Equal(FeedbackPriority.PauseRecommendation, harness.Approved.Single().Candidate.Priority);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 4. Hydration ⇒ HydrationSuggestion(40) reaches the pipeline and is approved.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void HydrationFromAdvisor_MapsToHydrationSuggestion_AndIsApprovedByPipeline()
        {
            var advisor = new HydrationAdvisor(new HydrationAdvisorOptions
            {
                SpikeLimit = 0.25,
                BaselineResonance = 0.70,
                BaselineStability = 0.70,
                ResonanceDriftThreshold = 0.04,
                StabilityVarianceThreshold = 0.025,
                AccumulatedLoadThreshold = 0.35,
                SuggestionThreshold = 0.60
            });
            var mapper = new HydrationFeedbackMapper();
            var harness = new PipelineHarness(() => FrozenNow);

            // Resonance drift + stability variance drives a hydration suggestion (mirrors
            // HydrationAdvisorTests.Advisor_SuggestsHydrationFromResonanceDriftAndVariance).
            HydrationAdvice? suggested = null;
            for (var i = 0; i < 18 && suggested == null; i++)
            {
                var advice = advisor.Evaluate(new ExerciseLiveState
                {
                    PrimaryMetricScore = 0.68 - i * 0.012,
                    SecondaryMetricScore = i % 2 == 0 ? 0.72 : 0.56,
                    StabilityScore = i % 2 == 0 ? 0.72 : 0.56,
                    IsInComfortZone = true,
                    IsHoldingCorrectly = true,
                    HoldProgress = 0.7,
                    Timestamp = FrozenNow
                });
                if (advice.Suggested) suggested = advice;
            }

            Assert.NotNull(suggested);

            var candidate = mapper.Map(suggested!);
            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, candidate!.Priority);
            Assert.Equal("HydrationAdvisor", candidate.Source);

            // No safety lock in context → hydration passes the guard.
            var decision = harness.Pipeline.Submit(
                candidate,
                mapper.BuildContext(suggested!, State(0.60, 0.60)));

            Assert.Equal(FeedbackDecisionKind.Approved, decision.Kind);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, harness.Approved.Single().Candidate.Priority);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 5. Hydration is SUPPRESSED under a safety freeze (explicit guard rule), but
        //    passes otherwise — proves the guard's freeze-vs-hydration interaction.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Hydration_IsSuppressedUnderSafetyFreeze_ButPassesOtherwise()
        {
            var mapper = new HydrationFeedbackMapper();

            // A real suggestion (mapper requires Suggested == true to emit a candidate).
            var advice = new HydrationAdvice
            {
                Suggested = true,
                ReasonCode = "HYDRATION_RESONANCE_DRIFT",
                Score = 0.80,
                Timestamp = FrozenNow
            };
            var candidate = mapper.Map(advice);
            Assert.NotNull(candidate);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, candidate!.Priority);

            // ── Under a safety lock: the mapper's BuildContext sets IsSafetyFreezeActive,
            //    and the guard's explicit rule suppresses the hydration hint. ──
            var lockedState = State(0.45, 0.45, safetyLocked: true);
            var frozenContext = mapper.BuildContext(advice, lockedState);
            Assert.True(frozenContext.IsSafetyFreezeActive);

            var frozenHarness = new PipelineHarness(() => FrozenNow);
            var frozenDecision = frozenHarness.Pipeline.Submit(candidate, frozenContext);

            Assert.NotEqual(FeedbackDecisionKind.Approved, frozenDecision.Kind);
            Assert.Empty(frozenHarness.Approved);
            Assert.Single(frozenHarness.Suppressed);

            // ── Without a lock: same candidate passes. Fresh harness so no rate-limit
            //    state leaks between the two submits. ──
            var openState = State(0.60, 0.60);
            var openContext = mapper.BuildContext(advice, openState);
            Assert.False(openContext.IsSafetyFreezeActive);

            var openHarness = new PipelineHarness(() => FrozenNow);
            var openDecision = openHarness.Pipeline.Submit(candidate, openContext);

            Assert.Equal(FeedbackDecisionKind.Approved, openDecision.Kind);
            Assert.Single(openHarness.Approved);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 6. Suppression matrix at the priority boundaries: an active strain alert
        //    suppresses concurrently-submitted praise/progression (≤ PerformancePraise)
        //    while the strain warning itself survives — the recently-tightened matrix.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ActiveStrainContext_SuppressesPraiseAndProgression_ButNotTheStrainAlert()
        {
            var supervisor = CreateSupervisor();
            var mapper = new VocalHealthFeedbackMapper();

            supervisor.Evaluate(State(0.70, 0.70, hold: 0.80));
            var strainDecision = supervisor.Evaluate(
                State(0.65, 0.35, hold: 0.20, inComfortZone: false));
            Assert.True(strainDecision.StrainDetected);

            var strainCandidate = mapper.Map(strainDecision)!;
            var strainContext = mapper.BuildContext(strainDecision);
            Assert.True(strainContext.IsActiveStrainAlert);

            // Praise (20) and progression (10) candidates submitted under the same active
            // strain context must be suppressed; the strain alert (60) must pass.
            var praise = new FeedbackCandidate(
                "PerformancePraise_Generic", "PRAISE", FeedbackPriority.PerformancePraise,
                MessageSeverity.Info, "ProgressionOrchestrator", "PRAISE");
            var progression = new FeedbackCandidate(
                "ProgressionFeedback_Update", "PROGRESSION_UPDATE", FeedbackPriority.ProgressionUpdate,
                MessageSeverity.Info, "ProgressionOrchestrator", "PROGRESSION_UPDATE");

            var harness = new PipelineHarness(() => FrozenNow);

            var strainResult = harness.Pipeline.Submit(strainCandidate, strainContext);
            var praiseResult = harness.Pipeline.Submit(praise, strainContext);
            var progressionResult = harness.Pipeline.Submit(progression, strainContext);

            Assert.Equal(FeedbackDecisionKind.Approved, strainResult.Kind);
            Assert.NotEqual(FeedbackDecisionKind.Approved, praiseResult.Kind);
            Assert.NotEqual(FeedbackDecisionKind.Approved, progressionResult.Kind);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 7. Pause context suppresses TechniqueCorrection (30) — the boundary above
        //    praise that the tightened matrix added (Pause ≤ Technique is suppressed).
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void PauseContext_SuppressesTechniqueCorrection_ButNotThePauseRecommendation()
        {
            var supervisor = CreateSupervisor();
            var mapper = new VocalHealthFeedbackMapper();

            var states = Enumerable.Range(0, 16)
                .Select(i => State(0.70 - i * 0.020, 0.70 - i * 0.020, hold: 0.75));
            var pauseDecision = DriveUntil(
                supervisor,
                states,
                d => d.PauseRecommended
                     && d.State != HealthSafetyState.Lock
                     && d.State != HealthSafetyState.Restrict
                     && !d.StrainDetected);

            var pauseCandidate = mapper.Map(pauseDecision)!;
            var pauseContext = mapper.BuildContext(pauseDecision);
            Assert.True(pauseContext.IsPauseRecommended);

            var technique = new FeedbackCandidate(
                "InlineCoachFeedback_PitchOutOfZone", "PITCH_OUT_OF_ZONE",
                FeedbackPriority.TechniqueCorrection, MessageSeverity.Suggestion,
                "ExerciseIntelligenceCoordinator", "INLINE_PITCH_OUT_OF_ZONE");

            var harness = new PipelineHarness(() => FrozenNow);

            var pauseResult = harness.Pipeline.Submit(pauseCandidate, pauseContext);
            var techniqueResult = harness.Pipeline.Submit(technique, pauseContext);

            Assert.Equal(FeedbackDecisionKind.Approved, pauseResult.Kind);
            Assert.NotEqual(FeedbackDecisionKind.Approved, techniqueResult.Kind);
            Assert.Contains(harness.Suppressed,
                d => d.Candidate.Priority == FeedbackPriority.TechniqueCorrection);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 8. SubmitMany resolves competing health candidates by priority: only the
        //    highest-priority candidate is approved; the rest are suppressed — proving
        //    the pipeline arbitrates between simultaneous health signals correctly.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void SubmitMany_WithCompetingHealthCandidates_ApprovesOnlyHighestPriority()
        {
            var mapper = new VocalHealthFeedbackMapper();

            var lockCandidate = mapper.Map(new VocalHealthDecision
            {
                State = HealthSafetyState.Lock,
                ReasonCode = "HEALTH_LOCK",
                StrainDetected = true,
                Timestamp = FrozenNow
            })!;
            var hydrationCandidate = new HydrationFeedbackMapper().Map(new HydrationAdvice
            {
                Suggested = true,
                ReasonCode = "HYDRATION_RESONANCE_DRIFT",
                Score = 0.80,
                Timestamp = FrozenNow
            })!;

            Assert.Equal(FeedbackPriority.SafetyFreeze, lockCandidate.Priority);
            Assert.Equal(FeedbackPriority.HydrationSuggestion, hydrationCandidate.Priority);

            var guard = new FeedbackConsistencyGuard(clock: () => FrozenNow);

            // Submit lowest first to prove ordering is by priority, not submit order.
            var decisions = guard.SubmitMany(
                new[] { hydrationCandidate, lockCandidate },
                new FeedbackGuardContext(IsSafetyFreezeActive: true));

            var approved = decisions.Where(d => d.Kind == FeedbackDecisionKind.Approved).ToList();
            Assert.Single(approved);
            Assert.Equal(FeedbackPriority.SafetyFreeze, approved[0].Candidate.Priority);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 9. End-to-end through the production wiring: a real ExerciseSessionRecorder
        //    over a real coordinator routes a locked tick's supervisor decision through
        //    the mapper → pipeline and approves it — proving the assembled graph fires,
        //    not just isolated components. (ExerciseSessionRecorder test pattern.)
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void EndToEnd_RecorderRoutesSupervisorDecisionThroughPipeline_OnLockedTick()
        {
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var hydration = new HydrationAdvisor();
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var guard = new FeedbackConsistencyGuard();
            var pipeline = new FeedbackPipeline(guard);

            using var recorder = new ExerciseSessionRecorder(
                coordinator,
                supervisor,
                store,
                hydration,
                pipeline,
                new VocalHealthFeedbackMapper(),
                new HydrationFeedbackMapper());

            var approvedFromHealth = new List<FeedbackCandidate>();
            pipeline.FeedbackApproved += (_, d) =>
            {
                if (d.Candidate.Source == "VocalHealthSupervisor")
                    approvedFromHealth.Add(d.Candidate);
            };

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), userId: 1);
            recorder.BeginSession(exerciseId: 40, sessionId: 4000, userId: 1);

            // health < 70 locks the coordinator. The supervisor's EvaluateStrain adds
            // +0.80 for IsSafetyLocked → StrainDetected on the first locked tick, and the
            // state becomes Restrict (HealthWarning) — either way a high-priority
            // health candidate is produced and must survive the guard.
            coordinator.UpdateMetrics(0.6, 200, 0.5, health: 60);

            Assert.NotEmpty(approvedFromHealth);
            var first = approvedFromHealth[0];
            Assert.Equal("VocalHealthSupervisor", first.Source);
            // The produced candidate carries a clinically high priority (>= ActiveStrainAlert):
            // Lock → SafetyFreeze, Restrict → HealthWarning, or Strain → ActiveStrainAlert.
            Assert.True(first.Priority >= FeedbackPriority.ActiveStrainAlert,
                $"Expected a high-priority health candidate, got {first.Priority}.");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 10. Subjective-report path: a health-concern report is journaled as a
        //     PauseRecommended health event (the pipeline/analytics context), NOT written
        //     as direct UI text — proving SubmitSubjectiveReport ends in the clinical
        //     decision/analytics layer.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async System.Threading.Tasks.Task
            SubjectiveReport_WithHealthConcern_IsJournaledAsPauseRecommendedEvent_NotDirectUiText()
        {
            using var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            using var recorder = new ExerciseSessionRecorder(coordinator, supervisor, store);

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), userId: 1);
            recorder.BeginSession(exerciseId: 41, sessionId: 4100, userId: 1);

            // ExperiencedStrain == true ⇒ IndicatesHealthConcern ⇒ PauseRecommended event.
            recorder.SubmitSubjectiveReport(new SubjectiveReport
            {
                SessionId = 4100,
                UserId = 1,
                ComfortLevel = 4,
                FatigueFeeling = 2,
                ExperiencedStrain = true,
                WantsToContinue = false
            });

            // Poll the analytics store: the report lands as a PauseRecommended clinical
            // event with the subjective reason code (fire-and-forget persistence).
            HealthAnalyticsEvent? pause = null;
            for (var attempt = 0; attempt < 50 && pause == null; attempt++)
            {
                var events = await store
                    .GetHealthEventsAsync(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1));
                pause = events.FirstOrDefault(
                    e => e.EventType == HealthAnalyticsEventType.PauseRecommended);
                if (pause == null) await System.Threading.Tasks.Task.Delay(20);
            }

            Assert.NotNull(pause);
            Assert.Equal(4100, pause!.SessionId);
            Assert.Equal("SUBJECTIVE_HEALTH_CONCERN", pause.ReasonCode);
            Assert.Equal(HealthAnalyticsEventType.PauseRecommended, pause.EventType);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 11. A health-concern subjective report submitted at the supervisor level maps
        //     to a PauseRecommendation candidate that the pipeline approves — the
        //     decision-engine half of the subjective path (no direct UI write).
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void FatiguePauseEvent_FromSupervisorEvent_DrivesPipeline_NotUiDirectly()
        {
            var supervisor = CreateSupervisor();
            var mapper = new VocalHealthFeedbackMapper();
            var harness = new PipelineHarness(() => FrozenNow);

            // Subscribe to the supervisor's PauseRecommended event and route it through
            // the mapper → pipeline — exactly the "engine → mapper → pipeline" contract
            // (the supervisor never touches UI; it only raises a decision event).
            VocalHealthDecision? captured = null;
            supervisor.PauseRecommended += (_, decision) => captured = decision;

            foreach (var i in Enumerable.Range(0, 16))
            {
                supervisor.Evaluate(State(0.70 - i * 0.020, 0.70 - i * 0.020, hold: 0.75));
                if (captured != null) break;
            }

            Assert.NotNull(captured);

            var candidate = mapper.Map(captured!);
            Assert.NotNull(candidate);
            // Pause or higher (the drift can also surface fatigue→HealthWarning); in all
            // cases it is a clinically prioritised candidate, never UI text.
            Assert.True(candidate!.Priority >= FeedbackPriority.PauseRecommendation);

            var result = harness.Pipeline.Submit(candidate, mapper.BuildContext(captured!));
            Assert.Equal(FeedbackDecisionKind.Approved, result.Kind);
            Assert.Single(harness.Approved);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 12. Normal health ⇒ no candidate at all: the mapper returns null for a benign
        //     decision, so the pipeline is never spammed with "everything is fine"
        //     messages (the health layer only speaks when clinically warranted).
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void NormalHealthDecision_ProducesNoCandidate_PipelineStaysSilent()
        {
            var supervisor = CreateSupervisor();
            var mapper = new VocalHealthFeedbackMapper();
            var harness = new PipelineHarness(() => FrozenNow);

            // A single healthy tick: Normal state, no strain/fatigue/pause/hydration.
            var decision = supervisor.Evaluate(State(0.75, 0.75, hold: 0.80));
            Assert.Equal(HealthSafetyState.Normal, decision.State);
            Assert.False(decision.StrainDetected);
            Assert.False(decision.FatigueDetected);
            Assert.False(decision.PauseRecommended);

            var candidate = mapper.Map(decision);
            Assert.Null(candidate);

            // Nothing to submit → pipeline emits nothing.
            Assert.Empty(harness.Approved);
            Assert.Empty(harness.Suppressed);
        }
    }
}
