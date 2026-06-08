using FemVoiceStudio.Data;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tester for adaptivt øktvolum + anbefaling-surfacing (Agent VOL — Sprint C).
    ///
    /// To rene, testbare enheter dekkes (ingen mocking, ekte klasser):
    ///   • <see cref="AdaptiveDifficultyService.RecommendVolume"/> — den rene volum-
    ///     beregningen (antall øvelser/sett ut fra Recovery/Comfort/Consistency/Health).
    ///   • <see cref="SmartCoachViewModel"/> / <see cref="MainViewModel"/> sine rene
    ///     surfacing-helpere som eksponerer SmartCoachs RecommendedExerciseId og volum.
    ///
    /// Klinisk invariant gjennom HELE suiten (ufravikelig): Health &gt; Progression.
    /// Lav recovery/helse strammer ALLTID inn volumet — god konsistens kan ALDRI
    /// overstyre et recovery-/helse-behov. Recovery-orienterte anbefalinger respekteres
    /// i surfacing (Health &gt; Goals).
    /// </summary>
    public class AdaptiveVolumeTests
    {
        private static AdaptiveDifficultyService Service() => new();

        private static RecoveryResult Recovery(double score)
            => new()
            {
                Score = score,
                Status = RecoveryScorer.ClassifyStatus(score),
                Explanation = "test"
            };

        private static SmartCoachDailyRecommendation Recommendation(
            string focusArea = "resonance",
            int? exerciseId = 12,
            bool healthWarning = false)
            => new()
            {
                FocusArea = focusArea,
                RecommendedExerciseId = exerciseId,
                HealthWarning = healthWarning
            };

        // ── 1. Lav recovery ⇒ lavere volum (færre øvelser, recovery-stramming) ───────
        [Fact]
        public void RecommendVolume_LowRecovery_ReducesExerciseCountAndFlagsRecovery()
        {
            var result = Service().RecommendVolume(
                recovery: Recovery(15),          // Overtrained
                comfortScore: 80,
                consistencyScore: 90,            // sterk konsistens — skal IKKE redde volumet
                healthScore: 90);

            Assert.True(result.IsReducedForRecovery);
            Assert.Equal(VolumeTier.Reduced, result.Tier);
            Assert.Equal(AdaptiveDifficultyService.MinExerciseCount, result.ExerciseCount);
            Assert.Equal(AdaptiveDifficultyService.MinSetCount, result.SetCount);
        }

        // ── 2. Lav helse ⇒ lavere volum selv med god recovery (Health > Progression) ─
        [Fact]
        public void RecommendVolume_LowHealth_ReducesEvenWithGoodRecovery()
        {
            var result = Service().RecommendVolume(
                recovery: Recovery(95),          // godt restituert
                comfortScore: 90,
                consistencyScore: 95,            // toppkonsistens
                healthScore: 30);                // men lav helse

            Assert.True(result.IsReducedForRecovery);
            Assert.Equal(VolumeTier.Reduced, result.Tier);
            Assert.True(result.ExerciseCount <= AdaptiveDifficultyService.BaselineExerciseCount);
        }

        // ── 3. God konsistens + god recovery/helse ⇒ moderat HØYERE volum ────────────
        [Fact]
        public void RecommendVolume_StrongConsistencyAndSafe_IncreasesModerately()
        {
            var result = Service().RecommendVolume(
                recovery: Recovery(90),
                comfortScore: 85,
                consistencyScore: 90,
                healthScore: 90);

            Assert.False(result.IsReducedForRecovery);
            Assert.Equal(VolumeTier.Increased, result.Tier);
            Assert.True(result.ExerciseCount > AdaptiveDifficultyService.BaselineExerciseCount);
        }

        // ── 4. Det moderate løftet er CLAMPET — aldri over taket ─────────────────────
        [Fact]
        public void RecommendVolume_MaximalSafeInput_NeverExceedsMaxCounts()
        {
            var result = Service().RecommendVolume(
                recovery: Recovery(100),
                comfortScore: 100,
                consistencyScore: 100,
                healthScore: 100);

            Assert.True(result.ExerciseCount <= AdaptiveDifficultyService.MaxExerciseCount);
            Assert.True(result.SetCount <= AdaptiveDifficultyService.MaxSetCount);
            // Moderat: et "kan øke litt" — ikke et hopp til taket på 8.
            Assert.True(result.ExerciseCount <= AdaptiveDifficultyService.BaselineExerciseCount + 2);
        }

        // ── 5. Trygg, men middels konsistens ⇒ baseline (verken opp eller ned) ───────
        [Fact]
        public void RecommendVolume_SafeButMediocreConsistency_StaysAtBaseline()
        {
            var result = Service().RecommendVolume(
                recovery: Recovery(80),
                comfortScore: 80,
                consistencyScore: 55,            // under StrongConsistencyThreshold
                healthScore: 80);

            Assert.False(result.IsReducedForRecovery);
            Assert.Equal(VolumeTier.Baseline, result.Tier);
            Assert.Equal(AdaptiveDifficultyService.BaselineExerciseCount, result.ExerciseCount);
        }

        // ── 6. MONOTONI i recovery: bedre recovery ⇒ aldri lavere volum ──────────────
        [Theory]
        [InlineData(10, 40)]
        [InlineData(40, 60)]
        [InlineData(60, 80)]
        [InlineData(80, 100)]
        public void RecommendVolume_IsMonotoneInRecovery(double lower, double higher)
        {
            var svc = Service();
            var low = svc.RecommendVolume(Recovery(lower), comfortScore: 80, consistencyScore: 90, healthScore: 90);
            var high = svc.RecommendVolume(Recovery(higher), comfortScore: 80, consistencyScore: 90, healthScore: 90);

            Assert.True(high.ExerciseCount >= low.ExerciseCount,
                $"recovery {higher} gav lavere volum ({high.ExerciseCount}) enn {lower} ({low.ExerciseCount})");
            Assert.True(high.SetCount >= low.SetCount);
        }

        // ── 7. MONOTONI i konsistens (i trygg sone): mer konsistens ⇒ aldri lavere ───
        [Theory]
        [InlineData(50, 75)]
        [InlineData(75, 90)]
        [InlineData(90, 100)]
        public void RecommendVolume_IsMonotoneInConsistency_WhenSafe(double lower, double higher)
        {
            var svc = Service();
            var low = svc.RecommendVolume(Recovery(90), comfortScore: 90, consistencyScore: lower, healthScore: 90);
            var high = svc.RecommendVolume(Recovery(90), comfortScore: 90, consistencyScore: higher, healthScore: 90);

            Assert.True(high.ExerciseCount >= low.ExerciseCount);
        }

        // ── 8. Alltid clampet til de definerte grensene, for et bredt input-spenn ────
        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(100, 100, 100, 100)]
        [InlineData(50, 50, 50, 50)]
        [InlineData(double.NaN, double.NaN, double.NaN, double.NaN)]
        [InlineData(-50, 200, -10, 999)]
        public void RecommendVolume_AlwaysWithinClampedBounds(
            double recoveryScore, double comfort, double consistency, double health)
        {
            var result = Service().RecommendVolume(
                Recovery(double.IsNaN(recoveryScore) ? 0 : recoveryScore),
                comfortScore: comfort,
                consistencyScore: consistency,
                healthScore: health);

            Assert.InRange(result.ExerciseCount,
                AdaptiveDifficultyService.MinExerciseCount, AdaptiveDifficultyService.MaxExerciseCount);
            Assert.InRange(result.SetCount,
                AdaptiveDifficultyService.MinSetCount, AdaptiveDifficultyService.MaxSetCount);
            Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        }

        // ── 9. Overtrained er strengere enn akkurat-under-grensen (hard < myk gate) ──
        [Fact]
        public void RecommendVolume_Overtrained_IsNotMoreVolumeThanBorderlineStrained()
        {
            var svc = Service();
            var overtrained = svc.RecommendVolume(Recovery(10), comfortScore: 70, consistencyScore: 70, healthScore: 70);
            var borderline = svc.RecommendVolume(Recovery(49), comfortScore: 70, consistencyScore: 70, healthScore: 70);

            // Begge er recovery-strammet; overtrained skal aldri gi MER volum enn borderline.
            Assert.True(overtrained.IsReducedForRecovery);
            Assert.True(borderline.IsReducedForRecovery);
            Assert.True(overtrained.ExerciseCount <= borderline.ExerciseCount);
        }

        // ── 10. Surfacing: anbefalt øvelse eksponeres som hint (SmartCoachViewModel) ──
        [Fact]
        public void SmartCoach_BuildRecommendedExerciseHint_SurfacesExerciseId()
        {
            var (hasExercise, exerciseId, hint, isRecovery) =
                SmartCoachViewModel.BuildRecommendedExerciseHint(Recommendation(exerciseId: 23));

            Assert.True(hasExercise);
            Assert.Equal(23, exerciseId);
            Assert.False(string.IsNullOrWhiteSpace(hint));
            Assert.False(isRecovery);
        }

        // ── 11. Surfacing: recovery-orientert anbefaling RESPEKTERES (Health > Goals) ─
        [Fact]
        public void SmartCoach_BuildRecommendedExerciseHint_RecoveryFocus_IsFlaggedRecoveryOriented()
        {
            var byFocus = SmartCoachViewModel.BuildRecommendedExerciseHint(
                Recommendation(focusArea: "recovery", exerciseId: 3));
            var byHealthWarning = SmartCoachViewModel.BuildRecommendedExerciseHint(
                Recommendation(focusArea: "resonance", exerciseId: 3, healthWarning: true));

            Assert.True(byFocus.IsRecoveryOriented);
            Assert.True(byHealthWarning.IsRecoveryOriented);
        }

        // ── 12. Surfacing: null-anbefaling / ingen ID ⇒ ingen krasj, intet hint ──────
        [Fact]
        public void SmartCoach_BuildRecommendedExerciseHint_NullOrNoId_NoCrashNoHint()
        {
            var nullRec = SmartCoachViewModel.BuildRecommendedExerciseHint(null);
            var noId = SmartCoachViewModel.BuildRecommendedExerciseHint(Recommendation(exerciseId: null));
            var zeroId = SmartCoachViewModel.BuildRecommendedExerciseHint(Recommendation(exerciseId: 0));

            Assert.False(nullRec.HasExercise);
            Assert.False(noId.HasExercise);
            Assert.False(zeroId.HasExercise);
            Assert.Equal("", nullRec.Hint);
        }

        // ── 13. VM-volum: recovery-orientert anbefaling tvinger inn-stramming ────────
        [Fact]
        public void SmartCoach_BuildVolumeSuggestion_RecoveryRecommendation_ForcesReduction()
        {
            // Selv med toppkonsistens OG høy helse skal et recovery-fokusert råd stramme inn.
            var volume = SmartCoachViewModel.BuildVolumeSuggestion(
                recommendation: Recommendation(focusArea: "recovery", exerciseId: 2),
                healthScore: 95,
                comfortScore: 95,
                consistencyScore: 95);

            Assert.True(volume.IsReducedForRecovery);
            Assert.Equal(VolumeTier.Reduced, volume.Tier);
        }

        // ── 14. VM-volum: trygt, ikke-recovery råd + sterk konsistens ⇒ løft ─────────
        [Fact]
        public void SmartCoach_BuildVolumeSuggestion_SafeNonRecovery_AllowsIncrease()
        {
            var volume = SmartCoachViewModel.BuildVolumeSuggestion(
                recommendation: Recommendation(focusArea: "resonance", exerciseId: 12),
                healthScore: 90,
                comfortScore: 90,
                consistencyScore: 90);

            Assert.False(volume.IsReducedForRecovery);
            Assert.Equal(VolumeTier.Increased, volume.Tier);
        }

        // ── 15. MainViewModel-hint: surfacing + recovery-respekt + null-safe ─────────
        [Fact]
        public void Main_BuildCoachRecommendationHint_SurfacesAndRespectsRecovery()
        {
            var normal = MainViewModel.BuildCoachRecommendationHint(
                Recommendation(focusArea: "pitch", exerciseId: 7));
            var recovery = MainViewModel.BuildCoachRecommendationHint(
                Recommendation(focusArea: "recovery", exerciseId: 1));
            var none = MainViewModel.BuildCoachRecommendationHint(null);

            Assert.True(normal.HasHint);
            Assert.False(normal.IsRecoveryOriented);
            Assert.False(string.IsNullOrWhiteSpace(normal.Hint));

            Assert.True(recovery.HasHint);
            Assert.True(recovery.IsRecoveryOriented);

            Assert.False(none.HasHint);
            Assert.Equal("", none.Hint);
        }
    }
}
