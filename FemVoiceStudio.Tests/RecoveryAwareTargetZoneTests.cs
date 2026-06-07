using System;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Range = FemVoiceStudio.Models.Range;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tester koblingen mellom den persisterte kliniske gaten
    /// (<see cref="ProgressionSafetyGate"/>) og recovery-grenen i
    /// <see cref="TargetProfileAdapter"/>, via <see cref="RecoveryActivationPolicy"/>.
    ///
    /// WPF-laget (MainViewModel/ExerciseWindow) kan ikke instansieres her, så vi tester
    /// nøyaktig den rene beslutnings- og koblingslogikken disse to kallstedene bruker:
    ///   • forsiden:  recoveryActive = gate.IsBlocked
    ///   • øvelsen:   recoveryActive = gate.IsBlocked && !overrideApplied
    /// og at flagget — matet inn i den EKTE adapteren — KRYMPER soner men aldri utvider.
    ///
    /// KLINISK INVARIANT (verifiseres): recovery-flagget kan kun føre til at soner
    /// krymper, aldri at krav utvides; override-stien dobbel-krympes ALDRI.
    /// Ekte klasser + in-memory-fake (SessionAnalyticsStore), ingen mocks.
    /// </summary>
    public class RecoveryAwareTargetZoneTests
    {
        private static readonly DateTime Now = new DateTime(2026, 6, 6, 12, 0, 0);

        private static SessionAnalyticsStore CreateStore()
            => new(new InMemorySessionAnalyticsRepository());

        private static Task AddPauseRecommendation(SessionAnalyticsStore store, DateTime occurredAt, int sessionId)
            => store.RecordHealthEventAsync(new HealthAnalyticsEvent
            {
                SessionId = sessionId,
                EventType = HealthAnalyticsEventType.PauseRecommended,
                OccurredAt = occurredAt,
                Severity = 0.5,
                ReasonCode = "PauseRecommended"
            });

        /// <summary>Driver gaten til IsBlocked via to pause-anbefalinger siste uke.</summary>
        private static async Task<ProgressionSafetyGate> BlockedGate()
        {
            var store = CreateStore();
            await AddPauseRecommendation(store, Now.AddDays(-1), sessionId: 1);
            await AddPauseRecommendation(store, Now.AddDays(-3), sessionId: 2);
            return new ProgressionSafetyGate(store);
        }

        private static ProgressionSafetyGate ClearGate() => new ProgressionSafetyGate(CreateStore());

        // ── RecoveryActivationPolicy: rene beslutninger ──────────────────────────

        [Fact]
        public void HomeZone_RecoveryActive_EqualsGateBlocked()
        {
            // Forsiden bygger alltid fra uskalert policy-sone ⇒ recovery følger gaten direkte.
            Assert.True(RecoveryActivationPolicy.ForHomeZone(gateBlocked: true));
            Assert.False(RecoveryActivationPolicy.ForHomeZone(gateBlocked: false));
        }

        [Fact]
        public void ExerciseProfile_RecoveryActive_OnlyWhenBlockedAndNoOverride()
        {
            // gateBlocked && !overrideApplied — sannhetstabell.
            Assert.True(RecoveryActivationPolicy.ForExerciseProfile(gateBlocked: true, overrideApplied: false));
            Assert.False(RecoveryActivationPolicy.ForExerciseProfile(gateBlocked: true, overrideApplied: true));
            Assert.False(RecoveryActivationPolicy.ForExerciseProfile(gateBlocked: false, overrideApplied: false));
            Assert.False(RecoveryActivationPolicy.ForExerciseProfile(gateBlocked: false, overrideApplied: true));
        }

        // ── Gate → forsidens pitch-målsone (PersonalizePitchZone) ────────────────

        [Fact]
        public async Task HomeZone_WhenGateBlocked_ShrinksPitchZone()
        {
            var gate = await BlockedGate();
            var result = await gate.EvaluateAsync(Now);
            var recoveryActive = RecoveryActivationPolicy.ForHomeZone(result.IsBlocked);

            var baseZone = (min: 160.0, max: 230.0);
            var shrunk = TargetProfileAdapter.PersonalizePitchZone(baseZone, user: null, recoveryActive);

            Assert.True(recoveryActive);
            // KRYMPER mot midten — bredden blir mindre, aldri større.
            Assert.True(shrunk.max - shrunk.min < baseZone.max - baseZone.min);
            Assert.True(shrunk.min > baseZone.min);
            Assert.True(shrunk.max < baseZone.max);
        }

        [Fact]
        public async Task HomeZone_WhenGateClear_LeavesPitchZoneUnchanged()
        {
            var gate = ClearGate();
            var result = await gate.EvaluateAsync(Now);
            var recoveryActive = RecoveryActivationPolicy.ForHomeZone(result.IsBlocked);

            var baseZone = (min: 160.0, max: 230.0);
            var same = TargetProfileAdapter.PersonalizePitchZone(baseZone, user: null, recoveryActive);

            Assert.False(recoveryActive);
            Assert.Equal(baseZone.min, same.min, 6);
            Assert.Equal(baseZone.max, same.max, 6);
        }

        [Fact]
        public void HomeZone_ReapplyFromBaseZone_DoesNotDoubleShrink()
        {
            // Forsidens re-apply skjer ALLTID fra BASE-sonen (RepersonalizePitchTargetZone),
            // aldri fra den allerede krympede sonen — to påfølgende recovery-applikasjoner
            // fra samme base gir identisk resultat (idempotent), ikke en stadig mindre sone.
            var baseZone = (min: 160.0, max: 230.0);

            var first = TargetProfileAdapter.PersonalizePitchZone(baseZone, user: null, recoveryActive: true);
            var second = TargetProfileAdapter.PersonalizePitchZone(baseZone, user: null, recoveryActive: true);

            Assert.Equal(first.min, second.min, 6);
            Assert.Equal(first.max, second.max, 6);
        }

        // ── Gate → øvelsesprofil (Personalize) med override-nyansen ──────────────

        private static ExerciseTargetProfile PitchProfile()
            => ExerciseTargetProfile.CreateCoordinatedGlideUp();

        private static UserVoiceProfile CalibratedPitchUser()
            => new UserVoiceProfile
            {
                ComfortZoneMinPitch = 170,
                ComfortZoneMaxPitch = 220,
                PreferredVoiceStyle = VoiceStyleGoal.Custom   // stil-nøytral ⇒ isoler recovery
            };

        [Fact]
        public async Task ExerciseProfile_BlockedAndNoOverride_ShrinksRequirements()
        {
            var gate = await BlockedGate();
            var result = await gate.EvaluateAsync(Now);
            var recoveryActive = RecoveryActivationPolicy.ForExerciseProfile(
                result.IsBlocked, overrideApplied: false);

            var adapter = new TargetProfileAdapter();
            var user = CalibratedPitchUser();
            var baseProfile = PitchProfile();

            // Referanse uten recovery (samme stil/bruker) for å isolere recovery-effekten.
            var withoutRecovery = adapter.Personalize(baseProfile, user, recoveryActive: false);
            var withRecovery = adapter.Personalize(baseProfile, user, recoveryActive);

            Assert.True(recoveryActive);
            // Stabilitetskravet og resonans-minimum KRYMPER (aldri øker) under recovery.
            Assert.True(withRecovery.StabilityThreshold <= withoutRecovery.StabilityThreshold);
            Assert.True(withRecovery.TargetResonanceMin <= withoutRecovery.TargetResonanceMin);
            // Pitch-sonen krymper mot midten.
            Assert.True(withRecovery.MaxPitch!.Value - withRecovery.MinPitch!.Value
                        < withoutRecovery.MaxPitch!.Value - withoutRecovery.MinPitch!.Value);
        }

        [Fact]
        public async Task ExerciseProfile_BlockedButOverrideApplied_DoesNotDoubleShrink()
        {
            // Override er ALLEREDE recovery-skalert (ClampToRecoveryFloor) ⇒ recoveryActive=false
            // her, ellers ville kravene krympes to ganger.
            var gate = await BlockedGate();
            var result = await gate.EvaluateAsync(Now);
            var recoveryActive = RecoveryActivationPolicy.ForExerciseProfile(
                result.IsBlocked, overrideApplied: true);

            var adapter = new TargetProfileAdapter();
            var user = CalibratedPitchUser();
            var overrideProfile = PitchProfile();   // representerer den allerede skalerte override-profilen

            var personalized = adapter.Personalize(overrideProfile, user, recoveryActive);
            var withoutRecovery = adapter.Personalize(overrideProfile, user, recoveryActive: false);

            Assert.True(result.IsBlocked);
            Assert.False(recoveryActive);   // gaten blokkerer, men override hindrer dobbel-krymping
            // Ingen ekstra recovery-krymping: identisk med ren stil/komfort-personalisering.
            Assert.Equal(withoutRecovery.StabilityThreshold, personalized.StabilityThreshold, 6);
            Assert.Equal(withoutRecovery.TargetResonanceMin, personalized.TargetResonanceMin, 6);
            Assert.Equal(withoutRecovery.MinPitch!.Value, personalized.MinPitch!.Value, 6);
            Assert.Equal(withoutRecovery.MaxPitch!.Value, personalized.MaxPitch!.Value, 6);
        }

        [Fact]
        public async Task ExerciseProfile_GateClear_NoRecoveryRegardlessOfOverride()
        {
            var gate = ClearGate();
            var result = await gate.EvaluateAsync(Now);

            Assert.False(RecoveryActivationPolicy.ForExerciseProfile(result.IsBlocked, overrideApplied: false));
            Assert.False(RecoveryActivationPolicy.ForExerciseProfile(result.IsBlocked, overrideApplied: true));
        }
    }
}
