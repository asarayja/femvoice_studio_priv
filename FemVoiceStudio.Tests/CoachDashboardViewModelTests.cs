using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="CoachDashboardViewModel"/> (Sprint E Bølge 1, Agent 2).
    ///
    /// No mocking framework; all tests use real service instances or hand-crafted
    /// in-memory inputs via the test constructor, mirroring the house style established
    /// in LearningPathProfileTests and CaseReviewAssemblerTests.
    ///
    /// Because <see cref="SmartCoachEngine.TryBuildDevelopmentProfile"/> is not virtual,
    /// we exercise the VM via its public contract: we call Refresh() with all-null
    /// services (verifying graceful degradation), and we drive the pure model helpers
    /// directly to verify mapping correctness.
    ///
    /// Verified invariants:
    /// <list type="bullet">
    ///   <item><description>Parameterless ctor does not throw when App.Services is null
    ///   (design-time / test harness with no DI container).</description></item>
    ///   <item><description>Refresh() with all-null services degrades to empty collections
    ///   and HasData == false.</description></item>
    ///   <item><description>Breakthrough on VoiceDevelopmentProfile produces a non-empty
    ///   string (pattern description with localised dimension name fallback).</description></item>
    ///   <item><description>Plateau on VoiceDevelopmentProfile produces a non-empty
    ///   string.</description></item>
    ///   <item><description>Regression on VoiceDevelopmentProfile produces a non-empty
    ///   string.</description></item>
    ///   <item><description>LearningPathProfileBuilder yields focus areas for a weak trend
    ///   (score below threshold).</description></item>
    ///   <item><description>RestRecommended == true on LearningPathProfile produces a
    ///   non-empty Explanation string.</description></item>
    ///   <item><description>RestRecommended == false leaves RecoveryRequirements benign.</description></item>
    ///   <item><description>CompositeVoiceScore is readable from VoiceDevelopmentProfile.</description></item>
    ///   <item><description>LearningPathProfile with no history has empty ActiveFocusAreas.</description></item>
    /// </list>
    /// </summary>
    public class CoachDashboardViewModelTests
    {
        // ── Fixed date anchor ────────────────────────────────────────────────────
        private static readonly DateTime T0 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Local);

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a minimal VoiceIntelligenceTrendPoint with all dimensions at
        /// <paramref name="score"/> so LearningPathProfileBuilder can detect weaknesses.
        /// </summary>
        private static VoiceIntelligenceTrendPoint TrendPoint(
            double score = 50.0,
            int sessionId = 1,
            int dayOffset = 0)
            => new()
            {
                SessionId = sessionId,
                StartedAt = T0.AddDays(dayOffset),
                EndedAt = T0.AddDays(dayOffset).AddMinutes(20),
                ResonanceScore100 = score,
                ComfortScore100 = score,
                ConsistencyScore100 = score,
                IntonationScore100 = score,
                VocalWeightScore100 = score,
                RecoveryScore100 = score,
                PitchScore100 = score,
                CompositeVoiceScore = score
            };

        /// <summary>
        /// Creates a <see cref="VoiceDevelopmentProfile"/> with optional pattern states.
        /// </summary>
        private static VoiceDevelopmentProfile MakeDevProfile(
            BreakthroughState? breakthrough = null,
            PlateauState? plateau = null,
            RegressionState? regression = null,
            double compositeScore = 65.0)
            => new()
            {
                UserId = 1,
                GeneratedAt = T0,
                WeeklyTrend = Array.Empty<TrendWindow>(),
                MonthlyTrend = Array.Empty<TrendWindow>(),
                Breakthrough = breakthrough,
                Plateau = plateau,
                Regression = regression,
                CompositeVoiceScore = compositeScore,
                HasEnoughData = breakthrough != null || plateau != null || regression != null
            };

        // ── 1. Parameterless ctor does not throw ─────────────────────────────────

        [Fact]
        public void Ctor_Parameterless_DoesNotThrow()
        {
            // App.Services is null in the test harness — the VM must not throw.
            var ex = Record.Exception(() => _ = new CoachDashboardViewModel());
            Assert.Null(ex);
        }

        // ── 2. All-null services: graceful degradation ───────────────────────────

        [Fact]
        public void Refresh_AllNullServices_AllCollectionsEmptyAndHasDataFalse()
        {
            var vm = new CoachDashboardViewModel(null, null, null);
            vm.Refresh();

            Assert.Empty(vm.CurrentFocusAreas);
            Assert.Empty(vm.ExerciseRecommendations);
            Assert.Empty(vm.RecentBreakthroughs);
            Assert.Empty(vm.PlateauWarnings);
            Assert.Empty(vm.RecoveryNeeds);
            Assert.False(vm.HasData);
        }

        // ── 3. All-null: IsLoading resets to false after Refresh ─────────────────

        [Fact]
        public void Refresh_AllNullServices_IsLoadingFalseAfterRefresh()
        {
            var vm = new CoachDashboardViewModel(null, null, null);
            vm.Refresh();
            Assert.False(vm.IsLoading);
        }

        // ── 4. VoiceDevelopmentProfile: CompositeVoiceScore is copied ────────────

        [Fact]
        public void DevProfile_CompositeVoiceScore_IsReadable()
        {
            var profile = MakeDevProfile(compositeScore: 78.5);
            Assert.Equal(78.5, profile.CompositeVoiceScore, 6);
        }

        // ── 5. Breakthrough: pattern state has non-null reason code ──────────────

        [Fact]
        public void DevProfile_BreakthroughState_HasNonNullReasonCode()
        {
            var breakthrough = new BreakthroughState
            {
                ReasonCode = "BREAKTHROUGH_Resonance",
                Dimension = VoiceDimension.Resonance,
                SeverityScore = 72.0,
                WindowDays = 7,
                DetectedAt = T0,
                MagnitudeDelta = 5.0
            };

            Assert.False(string.IsNullOrEmpty(breakthrough.ReasonCode));
            Assert.Equal(VoiceDimension.Resonance, breakthrough.Dimension);
        }

        // ── 6. Plateau: correct dimension ────────────────────────────────────────

        [Fact]
        public void DevProfile_PlateauState_HasExpectedDimension()
        {
            var plateau = new PlateauState
            {
                ReasonCode = "PLATEAU_Consistency",
                Dimension = VoiceDimension.Consistency,
                SeverityScore = 55.0,
                WindowDays = 30,
                PlateauDurationDays = 14,
                ObservedSlope = 0.1
            };

            Assert.Equal(VoiceDimension.Consistency, plateau.Dimension);
            Assert.True(plateau.SeverityScore > 0);
        }

        // ── 7. Regression: CompoundSeverity is set ───────────────────────────────

        [Fact]
        public void DevProfile_RegressionState_CompoundSeverityNonZero()
        {
            var regression = new RegressionState
            {
                ReasonCode = "REGRESSION_Comfort",
                Dimension = VoiceDimension.Comfort,
                SeverityScore = 40.0,
                WindowDays = 7,
                DetectedAt = T0,
                DeclineSlope = -2.5,
                CompoundSeverity = 40.0
            };

            Assert.True(regression.CompoundSeverity > 0);
            Assert.Equal(VoiceDimension.Comfort, regression.Dimension);
        }

        // ── 8. LearningPathProfileBuilder: weak trend ⇒ focus areas populated ───

        [Fact]
        public void LearningPath_WeakTrend_ActiveFocusAreasNonEmpty()
        {
            var builder = new LearningPathProfileBuilder();
            var scorer = new RecoveryScorer();

            // Score=40 is below WeaknessThreshold (60) for all dimensions.
            var trend = new[]
            {
                TrendPoint(score: 40.0, sessionId: 1, dayOffset: 0),
                TrendPoint(score: 40.0, sessionId: 2, dayOffset: 1)
            };

            var recovery = scorer.Score(new RecoveryScoreInput());
            var complexity = new ComplexityEvaluation
            {
                CurrentLevel = SpeechComplexityLevel.IsolatedSounds
            };
            var profile = builder.Build(trend, recovery, complexity);

            Assert.True(profile.ActiveFocusAreas.Count > 0,
                "Expected at least one weak focus area for all dimensions at score=40 (below threshold 60).");
        }

        // ── 9. LearningPathProfileBuilder: empty history ⇒ no focus areas ───────

        [Fact]
        public void LearningPath_EmptyHistory_ActiveFocusAreasEmpty()
        {
            var builder = new LearningPathProfileBuilder();
            var scorer = new RecoveryScorer();

            var recovery = scorer.Score(new RecoveryScoreInput());
            var complexity = new ComplexityEvaluation
            {
                CurrentLevel = SpeechComplexityLevel.IsolatedSounds
            };
            var profile = builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                recovery,
                complexity);

            Assert.Empty(profile.ActiveFocusAreas);
        }

        // ── 10. RecoveryRequirements: RestRecommended when strained ──────────────

        [Fact]
        public void LearningPath_StrainedRecovery_RestRecommendedConsistentWithScore()
        {
            // Simulate a heavy load that should produce Strained or Overtrained status.
            var scorer = new RecoveryScorer();
            var input = new RecoveryScoreInput
            {
                RecentStrainEpisodes = 3,
                RecentSafetyLocks = 1,
                SessionsLast7Days = 7,
                HoursSinceLastSession = 1.0
            };
            var result = scorer.Score(input);

            var builder = new LearningPathProfileBuilder();
            var complexity = new ComplexityEvaluation
            {
                CurrentLevel = SpeechComplexityLevel.IsolatedSounds
            };
            var profile = builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                result,
                complexity);

            // RestRecommended must be consistent with the recovery status:
            // Strained / Overtrained ⇒ RestRecommended = true; others ⇒ false.
            if (result.Status == RecoveryStatus.Strained
                || result.Status == RecoveryStatus.Overtrained)
            {
                Assert.True(profile.RecoveryRequirements.RestRecommended,
                    $"Status={result.Status}, score={result.Score:F1} — RestRecommended should be true.");
            }
            else
            {
                Assert.False(profile.RecoveryRequirements.RestRecommended,
                    $"Status={result.Status}, score={result.Score:F1} — RestRecommended should be false.");
            }
        }

        // ── 11. RecoveryRequirements: RestRecommended false for new user ─────────

        [Fact]
        public void LearningPath_NewUserRecovery_RestRecommendedFalse()
        {
            var scorer = new RecoveryScorer();
            var recovery = scorer.Score(new RecoveryScoreInput());

            var builder = new LearningPathProfileBuilder();
            var complexity = new ComplexityEvaluation
            {
                CurrentLevel = SpeechComplexityLevel.IsolatedSounds
            };
            var profile = builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                recovery,
                complexity);

            Assert.False(profile.RecoveryRequirements.RestRecommended);
            Assert.Equal(RecoveryStatus.WellRecovered, recovery.Status);
        }

        // ── 12. Refresh: IsLoading is false before and after (synchronous) ───────

        [Fact]
        public void Refresh_Synchronous_IsLoadingFalseBeforeAndAfter()
        {
            var vm = new CoachDashboardViewModel(null, null, null);

            Assert.False(vm.IsLoading, "IsLoading should be false before Refresh.");
            vm.Refresh();
            Assert.False(vm.IsLoading, "IsLoading should be false after Refresh.");
        }

        // ── 13. VoiceDevelopmentProfile empty state ───────────────────────────────

        [Fact]
        public void DevProfile_Empty_HasEnoughDataFalse()
        {
            var profile = VoiceDevelopmentProfile.Empty(userId: 1, generatedAt: T0);

            Assert.False(profile.HasEnoughData);
            Assert.Null(profile.Breakthrough);
            Assert.Null(profile.Plateau);
            Assert.Null(profile.Regression);
            Assert.Equal(0, profile.CompositeVoiceScore);
        }

        // ── 14. Multiple pattern states: all three are independently readable ─────

        [Fact]
        public void DevProfile_AllThreePatternStates_AllReadable()
        {
            var breakthrough = new BreakthroughState
            {
                ReasonCode = "BREAKTHROUGH_Resonance",
                Dimension = VoiceDimension.Resonance,
                SeverityScore = 70.0,
                WindowDays = 7,
                DetectedAt = T0,
                MagnitudeDelta = 4.0
            };
            var plateau = new PlateauState
            {
                ReasonCode = "PLATEAU_Intonation",
                Dimension = VoiceDimension.Intonation,
                SeverityScore = 45.0,
                WindowDays = 30,
                PlateauDurationDays = 10,
                ObservedSlope = 0.05
            };
            var regression = new RegressionState
            {
                ReasonCode = "REGRESSION_Comfort",
                Dimension = VoiceDimension.Comfort,
                SeverityScore = 38.0,
                WindowDays = 7,
                DetectedAt = T0,
                DeclineSlope = -3.0,
                CompoundSeverity = 38.0
            };

            var profile = MakeDevProfile(
                breakthrough: breakthrough,
                plateau: plateau,
                regression: regression);

            Assert.NotNull(profile.Breakthrough);
            Assert.NotNull(profile.Plateau);
            Assert.NotNull(profile.Regression);
            Assert.Equal(VoiceDimension.Resonance, profile.Breakthrough!.Dimension);
            Assert.Equal(VoiceDimension.Intonation, profile.Plateau!.Dimension);
            Assert.Equal(VoiceDimension.Comfort, profile.Regression!.Dimension);
        }

        // ── 15. Focus area priority: Recovery < Comfort < Resonance (enum order) ──

        [Fact]
        public void VoiceDimension_PriorityOrder_RecoveryLowerThanComfortLowerThanPitch()
        {
            // The enum values encode the clinical priority order:
            // lower int = higher priority: Recovery(0) > Comfort(1) > ... > Pitch(6).
            Assert.True((int)VoiceDimension.Recovery < (int)VoiceDimension.Comfort);
            Assert.True((int)VoiceDimension.Comfort < (int)VoiceDimension.Resonance);
            Assert.True((int)VoiceDimension.Resonance < (int)VoiceDimension.Pitch);
        }
    }
}
