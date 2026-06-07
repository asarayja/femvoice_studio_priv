using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tester for <see cref="TargetProfileAdapter"/> — personlig måltilpasning av
    /// øvelsesprofiler og forsidens pitch-målsone.
    ///
    /// Kliniske invarianter som verifiseres:
    ///   • null-bruker ⇒ profilen returneres uendret (bakoverkompatibelt).
    ///   • Stilmål justerer RESONANS (primærdimensjonen), aldri faste Hz-mål.
    ///   • DarkFeminine SENKER resonansmålet (aldri øker) — mørk feminin stemme
    ///     skal ikke presses mot lys resonans.
    ///   • Feminine eleverer lett, men clampes mot taket.
    ///   • Personlig kalibrert komfortsone overstyrer profilens generiske grenser.
    ///   • Recovery > Progression: krymper ALLE krav og kan aldri øke noe.
    ///   • Den resulterende profilen er alltid Validate()-konsistent.
    /// </summary>
    public class TargetProfileAdapterTests
    {
        private static TargetProfileAdapter CreateAdapter() => new TargetProfileAdapter();

        private static UserVoiceProfile ProfileWithStyle(VoiceStyleGoal style) =>
            new UserVoiceProfile { PreferredVoiceStyle = style };

        // ── null-bruker ──────────────────────────────────────────────────────────

        [Fact]
        public void Personalize_NullUser_ReturnsBaseProfileUnchanged()
        {
            var adapter = CreateAdapter();
            var baseProfile = ExerciseTargetProfile.CreateResonanceVowels();

            var result = adapter.Personalize(baseProfile, user: null, recoveryActive: false);

            // Samme referanse — bakoverkompatibelt, ingen muterte verdier.
            Assert.Same(baseProfile, result);
        }

        // ── Stilbasert resonansjustering ──────────────────────────────────────────

        [Fact]
        public void Personalize_DarkFeminine_LowersResonanceTargetsNeverRaises()
        {
            var adapter = CreateAdapter();
            var baseProfile = ExerciseTargetProfile.CreateResonanceVowels(); // min 0.58, max 0.92

            var result = adapter.Personalize(
                baseProfile, ProfileWithStyle(VoiceStyleGoal.DarkFeminine), recoveryActive: false);

            // Mørk feminin: senk min (−0.08) og max (−0.05) — aldri øk.
            Assert.True(result.TargetResonanceMin < baseProfile.TargetResonanceMin);
            Assert.True(result.TargetResonanceMax < baseProfile.TargetResonanceMax);
            Assert.Equal(baseProfile.TargetResonanceMin - 0.08, result.TargetResonanceMin, 3);
            Assert.Equal(baseProfile.TargetResonanceMax - 0.05, result.TargetResonanceMax, 3);
        }

        [Fact]
        public void Personalize_DarkFeminine_ResonanceMinRespectsFloor()
        {
            var adapter = CreateAdapter();
            // Lav base-min slik at −0.08 ville gå under gulvet 0.20.
            var baseProfile = ExerciseTargetProfile.CreateCoordinatedGlideUp(targetResonanceMin: 0.22);

            var result = adapter.Personalize(
                baseProfile, ProfileWithStyle(VoiceStyleGoal.DarkFeminine), recoveryActive: false);

            Assert.True(result.TargetResonanceMin >= 0.20);
        }

        [Fact]
        public void Personalize_Feminine_RaisesResonanceMinLightly()
        {
            var adapter = CreateAdapter();
            var baseProfile = ExerciseTargetProfile.CreateResonanceHumming(); // min 0.50

            var result = adapter.Personalize(
                baseProfile, ProfileWithStyle(VoiceStyleGoal.Feminine), recoveryActive: false);

            Assert.True(result.TargetResonanceMin > baseProfile.TargetResonanceMin);
            Assert.Equal(baseProfile.TargetResonanceMin + 0.03, result.TargetResonanceMin, 3);
        }

        [Fact]
        public void Personalize_Feminine_ClampsResonanceMinAtCeiling()
        {
            var adapter = CreateAdapter();
            // Base-min nær taket slik at +0.03 ville bryte 0.85.
            var baseProfile = ExerciseTargetProfile.CreateResonanceVowels(
                targetResonanceMin: 0.84, targetResonanceMax: 0.95);

            var result = adapter.Personalize(
                baseProfile, ProfileWithStyle(VoiceStyleGoal.Feminine), recoveryActive: false);

            Assert.True(result.TargetResonanceMin <= 0.85);
        }

        [Fact]
        public void Personalize_Androgynous_LowersResonanceMinSlightly()
        {
            var adapter = CreateAdapter();
            var baseProfile = ExerciseTargetProfile.CreateResonanceVowels(); // min 0.58

            var result = adapter.Personalize(
                baseProfile, ProfileWithStyle(VoiceStyleGoal.Androgynous), recoveryActive: false);

            Assert.Equal(baseProfile.TargetResonanceMin - 0.04, result.TargetResonanceMin, 3);
        }

        [Theory]
        [InlineData(VoiceStyleGoal.Situational)]
        [InlineData(VoiceStyleGoal.Custom)]
        public void Personalize_SituationalOrCustom_LeavesResonanceUnchanged(VoiceStyleGoal style)
        {
            var adapter = CreateAdapter();
            var baseProfile = ExerciseTargetProfile.CreateResonanceVowels();

            var result = adapter.Personalize(baseProfile, ProfileWithStyle(style), recoveryActive: false);

            Assert.Equal(baseProfile.TargetResonanceMin, result.TargetResonanceMin, 6);
            Assert.Equal(baseProfile.TargetResonanceMax, result.TargetResonanceMax, 6);
        }

        // ── Personlig komfortsone overstyrer pitch-grenser ────────────────────────

        [Fact]
        public void Personalize_CalibratedComfortZone_OverridesPitchBoundsWhenUsesPitch()
        {
            var adapter = CreateAdapter();
            var baseProfile = ExerciseTargetProfile.PitchExercise(minPitch: 150, maxPitch: 250);
            var user = new UserVoiceProfile
            {
                PreferredVoiceStyle = VoiceStyleGoal.Custom,
                ComfortZoneMinPitch = 180,
                ComfortZoneMaxPitch = 215
            };

            var result = adapter.Personalize(baseProfile, user, recoveryActive: false);

            Assert.Equal(180, result.MinPitch);
            Assert.Equal(215, result.MaxPitch);
        }

        [Fact]
        public void Personalize_CalibratedComfortZone_IgnoredWhenProfileDoesNotUsePitch()
        {
            var adapter = CreateAdapter();
            // Resonansøvelse — UsesPitch == false; pitch-grensene skal ikke skrives.
            var baseProfile = ExerciseTargetProfile.CreateResonanceVowels();
            var user = new UserVoiceProfile
            {
                PreferredVoiceStyle = VoiceStyleGoal.Custom,
                ComfortZoneMinPitch = 180,
                ComfortZoneMaxPitch = 215
            };

            var result = adapter.Personalize(baseProfile, user, recoveryActive: false);

            Assert.Null(result.MinPitch);
            Assert.Null(result.MaxPitch);
        }

        // ── Recovery krymper alle krav ────────────────────────────────────────────

        [Fact]
        public void Personalize_Recovery_ShrinksAllRequirementsNeverRaises()
        {
            var adapter = CreateAdapter();
            var baseProfile = ExerciseTargetProfile.CreateStabilityTraining(); // stab 0.70, hold 6
            var user = ProfileWithStyle(VoiceStyleGoal.Custom);

            var result = adapter.Personalize(baseProfile, user, recoveryActive: true);

            Assert.True(result.TargetResonanceMin <= baseProfile.TargetResonanceMin);
            Assert.True(result.StabilityThreshold <= baseProfile.StabilityThreshold);
            Assert.True(result.RequiredHoldSeconds <= baseProfile.RequiredHoldSeconds);
            Assert.True(result.TargetResonanceMax <= baseProfile.TargetResonanceMax);

            Assert.Equal(baseProfile.StabilityThreshold - 0.08, result.StabilityThreshold, 3);
            Assert.Equal(baseProfile.RequiredHoldSeconds - 2, result.RequiredHoldSeconds, 3);
        }

        [Fact]
        public void Personalize_Recovery_RespectsFloorsAndNeverGoesNegative()
        {
            var adapter = CreateAdapter();
            // Lave krav slik at recovery-deltaene ville gå under gulvene.
            var baseProfile = ExerciseTargetProfile.CreateResonanceHumming(
                targetResonanceMin: 0.18, stabilityThreshold: 0.22, requiredHoldSeconds: 1.0);

            var result = adapter.Personalize(
                baseProfile, ProfileWithStyle(VoiceStyleGoal.Custom), recoveryActive: true);

            Assert.True(result.TargetResonanceMin >= 0.15);
            Assert.True(result.StabilityThreshold >= 0.20);
            Assert.True(result.RequiredHoldSeconds >= 0);
        }

        [Fact]
        public void Personalize_Recovery_ShrinksPitchZoneTowardCenter()
        {
            var adapter = CreateAdapter();
            var baseProfile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 220);

            var result = adapter.Personalize(
                baseProfile, ProfileWithStyle(VoiceStyleGoal.Custom), recoveryActive: true);

            // Sonen er smalere, men senteret er bevart.
            Assert.True(result.MinPitch > 160);
            Assert.True(result.MaxPitch < 220);
            Assert.Equal(190, (result.MinPitch!.Value + result.MaxPitch!.Value) / 2.0, 3);
        }

        [Fact]
        public void Personalize_RecoveryAndStyle_Combined_RecoveryNeverRaisesAfterStyle()
        {
            var adapter = CreateAdapter();
            var baseProfile = ExerciseTargetProfile.CreateResonanceVowels(); // min 0.58
            var user = ProfileWithStyle(VoiceStyleGoal.Feminine);

            // Stil alene (Feminine) eleverer min; recovery deretter senker den.
            var styleOnly = adapter.Personalize(baseProfile, user, recoveryActive: false);
            var combined  = adapter.Personalize(baseProfile, user, recoveryActive: true);

            // Recovery anvendes ETTER stil og kan aldri ende høyere enn stil-resultatet.
            Assert.True(combined.TargetResonanceMin <= styleOnly.TargetResonanceMin);
            // Og aldri høyere enn base-profilens utgangspunkt.
            Assert.True(combined.TargetResonanceMin <= baseProfile.TargetResonanceMin);
        }

        // ── Validate() holder for alle stiler ─────────────────────────────────────

        [Theory]
        [InlineData(VoiceStyleGoal.Feminine)]
        [InlineData(VoiceStyleGoal.Androgynous)]
        [InlineData(VoiceStyleGoal.DarkFeminine)]
        [InlineData(VoiceStyleGoal.Situational)]
        [InlineData(VoiceStyleGoal.Custom)]
        public void Personalize_AllStyles_ProduceValidProfiles(VoiceStyleGoal style)
        {
            var adapter = CreateAdapter();
            var user = ProfileWithStyle(style);

            // En representativ profil per dimensjon — pluss recovery på/av.
            var profiles = new[]
            {
                ExerciseTargetProfile.CreateResonanceHumming(),
                ExerciseTargetProfile.CreateResonanceVowels(),
                ExerciseTargetProfile.CreateStabilityTraining(),
                ExerciseTargetProfile.CreateCoordinatedGlideUp(),
                ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 220)
            };

            foreach (var p in profiles)
            {
                // Validate() kastes inni Personalize; et returnert objekt er beviset.
                var noRecovery = adapter.Personalize(p, user, recoveryActive: false);
                var recovery   = adapter.Personalize(p, user, recoveryActive: true);

                noRecovery.Validate();
                recovery.Validate();
                Assert.True(noRecovery.TargetResonanceMax >= noRecovery.TargetResonanceMin);
                Assert.True(recovery.TargetResonanceMax >= recovery.TargetResonanceMin);
            }
        }

        // ── PersonalizePitchZone (forsidens målsone) ──────────────────────────────

        [Fact]
        public void PersonalizePitchZone_NoUser_ReturnsBaseZone()
        {
            var result = TargetProfileAdapter.PersonalizePitchZone(
                (160, 230), user: null, recoveryActive: false);

            Assert.Equal(160, result.min);
            Assert.Equal(230, result.max);
        }

        [Fact]
        public void PersonalizePitchZone_CalibratedUser_OverridesBaseZone()
        {
            var user = new UserVoiceProfile
            {
                ComfortZoneMinPitch = 185,
                ComfortZoneMaxPitch = 210
            };

            var result = TargetProfileAdapter.PersonalizePitchZone(
                (160, 230), user, recoveryActive: false);

            Assert.Equal(185, result.min);
            Assert.Equal(210, result.max);
        }

        [Fact]
        public void PersonalizePitchZone_UncalibratedUser_FallsBackToBaseZone()
        {
            // Bruker finnes, men komfortsonen er ikke kalibrert (null grenser).
            var user = new UserVoiceProfile();

            var result = TargetProfileAdapter.PersonalizePitchZone(
                (165, 225), user, recoveryActive: false);

            Assert.Equal(165, result.min);
            Assert.Equal(225, result.max);
        }

        [Fact]
        public void PersonalizePitchZone_Recovery_ShrinksZoneTowardCenter()
        {
            var result = TargetProfileAdapter.PersonalizePitchZone(
                (160, 220), user: null, recoveryActive: true);

            Assert.True(result.min > 160);
            Assert.True(result.max < 220);
            // Senter bevart; bredden redusert med ~15 % på hver side.
            Assert.Equal(190, (result.min + result.max) / 2.0, 3);
            Assert.True((result.max - result.min) < 60);
        }
    }
}
