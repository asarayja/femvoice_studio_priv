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
    /// RECOVERY-FØRST + HEALTH &gt; PROGRESSION (regresjon).
    ///
    /// Sikkerhetshierarki: Safety &gt; Health &gt; Recovery &gt; Progression &gt; Coaching &gt; UI.
    ///
    /// To deler, begge mot EKTE produksjonsklasser (eneste fake er den in-memory
    /// SessionAnalyticsStore-repoen, à la TestDatabaseService-mønsteret):
    ///
    ///   (a) En BLOKKERT <see cref="ProgressionSafetyGate"/> (REPEATED_SAFETY_LOCKS /
    ///       STRAIN_TREND_RISING) ⇒ <see cref="RecoveryActivationPolicy"/> sier
    ///       recoveryActive=true, og <see cref="TargetProfileAdapter"/>.Personalize med
    ///       recoveryActive:true KRYMPER sonen — aldri utvider den. Følger
    ///       RecoveryAwareTargetZoneTests / ProgressionAuthorityTests-mønstrene.
    ///
    ///   (b) En samtidig progresjon-/coaching-kandidat UNDERTRYKKES mens en recovery-/
    ///       helse-melding passerer, gjennom den ekte FeedbackConsistencyGuard-arbitreringen.
    /// </summary>
    public class RecoveryFirstOrderingTests
    {
        private static readonly DateTime Clock = new DateTime(2026, 6, 6, 12, 0, 0, DateTimeKind.Utc);

        private static SessionAnalyticsStore NewStore() => new(new InMemorySessionAnalyticsRepository());

        private static FeedbackConsistencyGuard NewGuard()
            => new(() => Clock, TimeSpan.Zero);

        private static HealthAnalyticsEvent SafetyFreeze(int sessionId, DateTime occurredAt)
            => new()
            {
                SessionId = sessionId,
                EventType = HealthAnalyticsEventType.SafetyFreeze,
                OccurredAt = occurredAt,
                Severity = 1,
                ReasonCode = "SAFETY_FREEZE"
            };

        private static HealthAnalyticsEvent StrainPeriod(int sessionId, DateTime occurredAt)
            => new()
            {
                SessionId = sessionId,
                EventType = HealthAnalyticsEventType.StrainPeriod,
                OccurredAt = occurredAt,
                Severity = 1,
                ReasonCode = "STRAIN_PERIOD"
            };

        // ── Gate-seeding ─────────────────────────────────────────────────────────

        /// <summary>To safety-freeze siste uke ⇒ REPEATED_SAFETY_LOCKS-blokk.</summary>
        private static async Task<ProgressionSafetyGate> RepeatedSafetyLocksGate(SessionAnalyticsStore store)
        {
            await store.RecordHealthEventAsync(SafetyFreeze(1, Clock.AddDays(-1)));
            await store.RecordHealthEventAsync(SafetyFreeze(2, Clock.AddDays(-3)));
            return new ProgressionSafetyGate(store);
        }

        /// <summary>Stigende strain (≥2 nylig, flere enn forrige uke) ⇒ STRAIN_TREND_RISING.</summary>
        private static async Task<ProgressionSafetyGate> RisingStrainGate(SessionAnalyticsStore store)
        {
            // Forrige uke: 1 strain (8 dager siden, innenfor 14-dagers vinduet).
            await store.RecordHealthEventAsync(StrainPeriod(1, Clock.AddDays(-8)));
            // Denne uken: 2 strain ⇒ recent(2) > prior(1) og recent >= 2.
            await store.RecordHealthEventAsync(StrainPeriod(2, Clock.AddDays(-2)));
            await store.RecordHealthEventAsync(StrainPeriod(3, Clock.AddDays(-1)));
            return new ProgressionSafetyGate(store);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  (a) Blokkert gate ⇒ recoveryActive ⇒ adapter KRYMPER sonen, aldri utvider
        // ═══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task RepeatedSafetyLocks_BlocksGate_RecoveryActive_ShrinksExerciseZone()
        {
            var store = NewStore();
            var gate = await RepeatedSafetyLocksGate(store);

            var gateResult = await gate.EvaluateAsync(Clock);
            Assert.True(gateResult.IsBlocked);
            Assert.Equal("REPEATED_SAFETY_LOCKS", gateResult.ReasonCode);

            // Ingen override anvendt ⇒ recovery må aktiveres her (ellers krympes aldri).
            var recoveryActive = RecoveryActivationPolicy.ForExerciseProfile(
                gateBlocked: gateResult.IsBlocked, overrideApplied: false);
            Assert.True(recoveryActive);

            var adapter = new TargetProfileAdapter();
            var user = new UserVoiceProfile
            {
                ComfortZoneMinPitch = 165,
                ComfortZoneMaxPitch = 225,
                PreferredVoiceStyle = VoiceStyleGoal.Custom   // stil-nøytral ⇒ isoler recovery
            };
            var baseProfile = ExerciseTargetProfile.CreateCoordinatedGlideUp();

            var withoutRecovery = adapter.Personalize(baseProfile, user, recoveryActive: false);
            var withRecovery = adapter.Personalize(baseProfile, user, recoveryActive);

            // Recovery KRYMPER alle krav (aldri utvider).
            Assert.True(withRecovery.StabilityThreshold <= withoutRecovery.StabilityThreshold);
            Assert.True(withRecovery.TargetResonanceMin <= withoutRecovery.TargetResonanceMin);
            Assert.True(withRecovery.RequiredHoldSeconds <= withoutRecovery.RequiredHoldSeconds);
            Assert.True(withRecovery.TargetResonanceMax <= withoutRecovery.TargetResonanceMax);

            // Pitch-sonen krymper strengt mot midten.
            Assert.True(withRecovery.MinPitch!.Value >= withoutRecovery.MinPitch!.Value);
            Assert.True(withRecovery.MaxPitch!.Value <= withoutRecovery.MaxPitch!.Value);
            Assert.True(
                withRecovery.MaxPitch.Value - withRecovery.MinPitch.Value
                < withoutRecovery.MaxPitch.Value - withoutRecovery.MinPitch.Value);
        }

        [Fact]
        public async Task RisingStrainTrend_BlocksGate_RecoveryActive_ShrinksHomePitchZone()
        {
            var store = NewStore();
            var gate = await RisingStrainGate(store);

            var gateResult = await gate.EvaluateAsync(Clock);
            Assert.True(gateResult.IsBlocked);
            Assert.Equal("STRAIN_TREND_RISING", gateResult.ReasonCode);

            // Forsiden følger gaten direkte.
            var recoveryActive = RecoveryActivationPolicy.ForHomeZone(gateResult.IsBlocked);
            Assert.True(recoveryActive);

            var baseZone = (min: 160.0, max: 230.0);
            var shrunk = TargetProfileAdapter.PersonalizePitchZone(baseZone, user: null, recoveryActive);

            // KRYMPER mot midten — bredden minker, aldri øker.
            Assert.True(shrunk.max - shrunk.min < baseZone.max - baseZone.min);
            Assert.True(shrunk.min > baseZone.min);
            Assert.True(shrunk.max < baseZone.max);
        }

        [Fact]
        public async Task BlockedGate_RecoveryShrink_NeverExceedsBaseProfile_OnAnyExercise()
        {
            // Invariant på tvers av ALLE øvelser: recovery kan aldri ende HØYERE enn
            // base-profilen på noen dimensjon (krymper eller holder, aldri utvider).
            var store = NewStore();
            var gate = await RepeatedSafetyLocksGate(store);
            var gateResult = await gate.EvaluateAsync(Clock);
            var recoveryActive = RecoveryActivationPolicy.ForExerciseProfile(
                gateResult.IsBlocked, overrideApplied: false);
            Assert.True(recoveryActive);

            var adapter = new TargetProfileAdapter();
            var user = new UserVoiceProfile { PreferredVoiceStyle = VoiceStyleGoal.Custom };

            ExerciseTargetProfile[] profiles =
            {
                ExerciseTargetProfile.ResonanceExercise(),
                ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 220),
                ExerciseTargetProfile.IntonationExercise(minPitch: 150, maxPitch: 210),
                ExerciseTargetProfile.StrawPhonation(),
                ExerciseTargetProfile.CreateResonanceHumming(),
                ExerciseTargetProfile.CreateResonanceVowels(),
                ExerciseTargetProfile.CreateCoordinatedGlideUp(),
                ExerciseTargetProfile.CreateStabilityTraining(),
            };

            foreach (var baseProfile in profiles)
            {
                var recovered = adapter.Personalize(baseProfile, user, recoveryActive);

                Assert.True(recovered.StabilityThreshold <= baseProfile.StabilityThreshold,
                    "stability must not grow under recovery");
                Assert.True(recovered.RequiredHoldSeconds <= baseProfile.RequiredHoldSeconds,
                    "hold seconds must not grow under recovery");
                Assert.True(recovered.TargetResonanceMin <= baseProfile.TargetResonanceMin,
                    "resonance floor must not grow under recovery");
                Assert.True(recovered.TargetResonanceMax <= baseProfile.TargetResonanceMax,
                    "resonance ceiling must not grow under recovery");

                if (baseProfile.MinPitch.HasValue && baseProfile.MaxPitch.HasValue
                    && recovered.MinPitch.HasValue && recovered.MaxPitch.HasValue)
                {
                    Assert.True(
                        recovered.MaxPitch.Value - recovered.MinPitch.Value
                        <= baseProfile.MaxPitch.Value - baseProfile.MinPitch.Value,
                        "pitch zone width must not grow under recovery");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  (b) Samtidig progresjon/coaching undertrykkes mens recovery/helse passerer
        // ═══════════════════════════════════════════════════════════════════════

        // Ekte recovery-/helse-kandidat fra produksjonsmapperen (VocalHealthFeedbackMapper).
        // En strain-detektert beslutning (ikke Lock/Restrict/Pause) ⇒ ActiveStrainAlert(60).
        private static readonly VocalHealthFeedbackMapper Health = new();
        private static readonly ProgressionFeedbackMapper Progression = new();

        private static VocalHealthDecision StrainDecision()
            => new()
            {
                State = HealthSafetyState.Normal,
                ReasonCode = "STRAIN_RISING",
                StrainDetected = true,
                Timestamp = Clock
            };

        private static VocalHealthDecision PauseDecision()
            => new()
            {
                State = HealthSafetyState.Normal,
                ReasonCode = "PAUSE_RECOMMENDED",
                PauseRecommended = true,
                Timestamp = Clock
            };

        private static FeedbackCandidate ProgressionPromotion()
            => Progression.Map(new ProgressionOrchestratorDecision
            {
                Kind = ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated,
                Dimension = ProgressionAdjustmentDimension.Resonance,
                ReasonCode = "RESONANCE_PROGRESSION",
                Reason = "RESONANCE_PROGRESSION",
                Confidence = 0.9,
                SuggestedProfile = ExerciseTargetProfile.CreateResonanceHumming()
            })!;

        [Fact]
        public void StrainAlert_Passes_WhileProgressionPromotion_IsSuppressed_SameBatch()
        {
            var guard = NewGuard();

            var strain = Health.Map(StrainDecision())!;
            Assert.Equal(FeedbackPriority.ActiveStrainAlert, strain.Priority);

            var promotion = ProgressionPromotion();
            Assert.Equal(FeedbackPriority.ProgressionUpdate, promotion.Priority);

            // Recovery-konteksten fra samme beslutning: strain aktiv.
            var context = Health.BuildContext(StrainDecision());
            Assert.True(context.IsActiveStrainAlert);

            // Input-rekkefølgen favoriserer progresjon ⇒ beviser at SubmitMany re-ordner.
            var decisions = guard.SubmitMany(new[] { promotion, strain }, context);

            var approved = decisions.Where(d => d.Kind == FeedbackDecisionKind.Approved).ToList();
            Assert.Single(approved);
            Assert.Equal(FeedbackPriority.ActiveStrainAlert, approved[0].Candidate.Priority);

            var promotionDecision = decisions.Single(d => d.Candidate.ReasonCode == "RESONANCE_PROGRESSION");
            Assert.NotEqual(FeedbackDecisionKind.Approved, promotionDecision.Kind);
            // Ingen progresjon/coaching (<= PerformancePraise) slapp gjennom.
            Assert.DoesNotContain(approved, d => d.Candidate.Priority <= FeedbackPriority.PerformancePraise);
        }

        [Fact]
        public void PauseRecommendation_Passes_WhileProgressionPromotion_IsSuppressed()
        {
            var guard = NewGuard();

            var pause = Health.Map(PauseDecision())!;
            Assert.Equal(FeedbackPriority.PauseRecommendation, pause.Priority);

            var promotion = ProgressionPromotion();
            var context = Health.BuildContext(PauseDecision());
            Assert.True(context.IsPauseRecommended);

            var decisions = guard.SubmitMany(new[] { promotion, pause }, context);

            var pauseDecision = decisions.Single(d => d.Candidate.Priority == FeedbackPriority.PauseRecommendation);
            var promotionDecision = decisions.Single(d => d.Candidate.ReasonCode == "RESONANCE_PROGRESSION");

            // Recovery (PauseRecommendation 50) passerer; progresjon (10) undertrykkes.
            Assert.Equal(FeedbackDecisionKind.Approved, pauseDecision.Kind);
            Assert.NotEqual(FeedbackDecisionKind.Approved, promotionDecision.Kind);
        }

        [Fact]
        public void RecoveryFlavouredRegression_Passes_WhileStrainBlocksTheCoachingHint()
        {
            // HEALTH > PROGRESSION end-to-end: en recovery-flavoured regression (HealthWarning)
            // overlever en hostile strain-kontekst, mens et coaching-/teknikk-hint (lavere
            // rang) i samme batch viker. Progresjon kan aldri slå helse.
            var guard = NewGuard();

            var regression = Progression.Map(new ProgressionOrchestratorDecision
            {
                Kind = ProgressionOrchestratorDecisionKind.RegressionTriggered,
                Dimension = ProgressionAdjustmentDimension.Recovery,
                ReasonCode = "PERFORMANCE_REGRESSION",
                Reason = "PERFORMANCE_REGRESSION",
                Confidence = 0.9,
                SuggestedProfile = ExerciseTargetProfile.CreateResonanceHumming()
            })!;
            Assert.Equal(FeedbackPriority.HealthWarning, regression.Priority);

            var promotion = ProgressionPromotion();

            var hostile = new FeedbackGuardContext(
                IsActiveStrainAlert: true,
                IsHoldStable: false);

            var decisions = guard.SubmitMany(new[] { promotion, regression }, hostile);

            var regressionDecision = decisions.Single(d => d.Candidate.ReasonCode == "PERFORMANCE_REGRESSION");
            var promotionDecision = decisions.Single(d => d.Candidate.ReasonCode == "RESONANCE_PROGRESSION");

            Assert.Equal(FeedbackDecisionKind.Approved, regressionDecision.Kind);
            Assert.NotEqual(FeedbackDecisionKind.Approved, promotionDecision.Kind);
        }
    }
}
