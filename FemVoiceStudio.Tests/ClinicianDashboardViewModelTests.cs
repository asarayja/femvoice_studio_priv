using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// ClinicianDashboardViewModel (Sprint E Wave 1) — unit tests via test ctor.
    /// Constructs the VM with explicit in-memory fakes (no App.Services, no WPF host,
    /// no DB). Tests verify:
    ///   1. Properties fill from a well-formed OutcomeProfile (via ApplyOutcomeProfile).
    ///   2. Dimension scores fill from a VI trend point (via ApplyLatestVoiceMetrics).
    ///   3. Trend collections fill from a VoiceDevelopmentProfile (via ApplyDevelopmentProfile).
    ///   4. Learning-path display fills (via ApplyLearningPathFromDevProfile).
    ///   5. Null/empty inputs degrade gracefully to 0/empty — no exception.
    ///   6. Test ctor does NOT call Load, so test-controlled Apply paths are isolated.
    /// </summary>
    public class ClinicianDashboardViewModelTests
    {
        private static readonly DateTime At = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a VM via the test ctor (no App.Services, no auto-load).
        /// </summary>
        private static ClinicianDashboardViewModel MakeVm()
            => new ClinicianDashboardViewModel(
                profileBuilder:      null,
                smartCoach:          null,
                effectivenessEngine: null,
                analyticsStore:      null);

        /// <summary>
        /// Builds a minimal OutcomeProfile with specified recovery fields.
        /// </summary>
        private static OutcomeProfile MakeOutcome(
            double recoveryScore       = 75.0,
            string recoveryStatus      = "Adequate",
            bool   overtraining        = false,
            double recoveryDebt        = 10.0,
            string recommendation      = "Keep going.",
            IReadOnlyList<ExerciseEffectivenessProfile>? ranked   = null,
            IReadOnlyList<ExerciseEffectivenessFlag>?    concerns = null,
            IReadOnlyList<GoalProgressEntry>?            goals    = null)
            => new OutcomeProfile
            {
                UserId      = 1,
                GeneratedAt = At,
                HasEnoughData = true,
                RecoveryProgress = new RecoveryProgress
                {
                    CurrentScore0to100       = recoveryScore,
                    Status                   = recoveryStatus,
                    OvertrainingPredicted    = overtraining,
                    RecoveryDebt             = recoveryDebt,
                    RecommendationText       = recommendation
                },
                ExerciseEffectiveness = new ExerciseEffectivenessSummary
                {
                    Ranked   = ranked   ?? Array.Empty<ExerciseEffectivenessProfile>(),
                    Concerns = concerns ?? Array.Empty<ExerciseEffectivenessFlag>()
                },
                GoalProgress = new GoalProgress
                {
                    Goals = goals ?? Array.Empty<GoalProgressEntry>()
                }
            };

        /// <summary>Builds a minimal VoiceIntelligenceTrendPoint with all scores set.</summary>
        private static VoiceIntelligenceTrendPoint MakeTrendPoint(
            double resonance    = 80.0,
            double comfort      = 70.0,
            double consistency  = 65.0,
            double recovery     = 55.0,
            double intonation   = 72.0,
            double vocalWeight  = 60.0,
            double pitch        = 45.0,
            double composite    = 68.0)
            => new VoiceIntelligenceTrendPoint
            {
                StartedAt           = At,
                ResonanceScore100   = resonance,
                ComfortScore100     = comfort,
                ConsistencyScore100 = consistency,
                RecoveryScore100    = recovery,
                IntonationScore100  = intonation,
                VocalWeightScore100 = vocalWeight,
                PitchScore100       = pitch,
                CompositeVoiceScore = composite
            };

        /// <summary>
        /// Builds a minimal VoiceDevelopmentProfile with one weekly TrendWindow.
        /// </summary>
        private static VoiceDevelopmentProfile MakeDevProfile(
            double compositeScore = 72.0,
            bool   hasEnoughData  = true,
            double comfortSlope   = 1.5,
            double resonanceSlope = 0.8,
            double consistencySlope = 0.4)
        {
            var slopes = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Comfort]     = comfortSlope,
                [VoiceDimension.Resonance]   = resonanceSlope,
                [VoiceDimension.Consistency] = consistencySlope
            };

            var window = new TrendWindow
            {
                WindowDays      = 7,
                From            = At.AddDays(-7),
                To              = At,
                SessionCount    = 4,
                HasEnoughData   = true,
                CompositeMean   = compositeScore,
                CompositeSlope  = 0.6,
                DimensionSlopes = slopes
            };

            return new VoiceDevelopmentProfile
            {
                UserId             = 1,
                GeneratedAt        = At,
                WeeklyTrend        = new[] { window },
                MonthlyTrend       = Array.Empty<TrendWindow>(),
                CompositeVoiceScore = compositeScore,
                HasEnoughData      = hasEnoughData
            };
        }

        // ═════════════════════════════════════════════════════════════════════════
        // 1. Test ctor does not throw and produces an initialised VM
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void TestCtor_DoesNotThrow_AndVmIsInitialised()
        {
            var vm = MakeVm();

            // Scalar properties default to 0 / empty (no auto-load).
            Assert.Equal(0.0, vm.CompositeScore, 6);
            Assert.False(vm.HasEnoughData);
            Assert.Empty(vm.RankedExercises);
            Assert.Empty(vm.Insights);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // 2. ApplyLatestVoiceMetrics fills per-dimension score properties
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ApplyLatestVoiceMetrics_FilledPoint_SetsAllDimensionScores()
        {
            var vm    = MakeVm();
            var point = MakeTrendPoint(
                resonance:   88.0,
                comfort:     76.0,
                consistency: 63.0,
                recovery:    51.0,
                intonation:  74.0,
                vocalWeight: 58.0,
                pitch:       42.0);

            vm.ApplyLatestVoiceMetrics(point);

            Assert.Equal(88.0, vm.ResonanceScore,        precision: 6);
            Assert.Equal(76.0, vm.ComfortScore,          precision: 6);
            Assert.Equal(63.0, vm.ConsistencyScore,      precision: 6);
            Assert.Equal(51.0, vm.RecoveryMetricScore,   precision: 6);
            Assert.Equal(74.0, vm.IntonationScore,       precision: 6);
            Assert.Equal(58.0, vm.VocalWeightScore,      precision: 6);
            Assert.Equal(42.0, vm.PitchScore,            precision: 6);
        }

        [Fact]
        public void ApplyLatestVoiceMetrics_NullPoint_ClearsAllDimensionScores()
        {
            var vm = MakeVm();
            // First set some values
            vm.ApplyLatestVoiceMetrics(MakeTrendPoint(resonance: 80));
            // Then clear
            vm.ApplyLatestVoiceMetrics(null);

            Assert.Equal(0.0, vm.ResonanceScore,       precision: 6);
            Assert.Equal(0.0, vm.ComfortScore,         precision: 6);
            Assert.Equal(0.0, vm.ConsistencyScore,     precision: 6);
            Assert.Equal(0.0, vm.RecoveryMetricScore,  precision: 6);
            Assert.Equal(0.0, vm.IntonationScore,      precision: 6);
            Assert.Equal(0.0, vm.VocalWeightScore,     precision: 6);
            Assert.Equal(0.0, vm.PitchScore,           precision: 6);
        }

        [Fact]
        public void ApplyLatestVoiceMetrics_ClampsAbove100()
        {
            var vm    = MakeVm();
            var point = MakeTrendPoint(resonance: 150.0);  // out-of-range

            vm.ApplyLatestVoiceMetrics(point);

            Assert.Equal(100.0, vm.ResonanceScore, precision: 6);
        }

        [Fact]
        public void ApplyLatestVoiceMetrics_ClampsBelow0()
        {
            var vm    = MakeVm();
            var point = MakeTrendPoint(comfort: -20.0);  // out-of-range

            vm.ApplyLatestVoiceMetrics(point);

            Assert.Equal(0.0, vm.ComfortScore, precision: 6);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // 3. ApplyDevelopmentProfile fills CompositeScore, HasEnoughData, trends
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ApplyDevelopmentProfile_FilledProfile_SetsCompositeAndHasEnoughData()
        {
            var vm      = MakeVm();
            var profile = MakeDevProfile(compositeScore: 72.5, hasEnoughData: true);

            vm.ApplyDevelopmentProfile(profile);

            Assert.Equal(72.5, vm.CompositeScore, precision: 6);
            Assert.True(vm.HasEnoughData);
        }

        [Fact]
        public void ApplyDevelopmentProfile_FilledProfile_FillsTrendCollections()
        {
            var vm      = MakeVm();
            var profile = MakeDevProfile(
                comfortSlope:     1.5,
                resonanceSlope:   0.8,
                consistencySlope: 0.4);

            vm.ApplyDevelopmentProfile(profile);

            // One window → one entry in each trend collection.
            Assert.Single(vm.ComfortTrend);
            Assert.Single(vm.ResonanceTrend);
            Assert.Single(vm.ConsistencyTrend);

            Assert.Equal(1.5, vm.ComfortTrend[0].Value,     precision: 6);
            Assert.Equal(0.8, vm.ResonanceTrend[0].Value,   precision: 6);
            Assert.Equal(0.4, vm.ConsistencyTrend[0].Value, precision: 6);
        }

        [Fact]
        public void ApplyDevelopmentProfile_NullProfile_ClearsCollectionsAndZeroesScores()
        {
            var vm = MakeVm();
            // Populate first
            vm.ApplyDevelopmentProfile(MakeDevProfile());
            // Then clear
            vm.ApplyDevelopmentProfile(null);

            Assert.Equal(0.0, vm.CompositeScore, precision: 6);
            Assert.False(vm.HasEnoughData);
            Assert.Empty(vm.ComfortTrend);
            Assert.Empty(vm.ResonanceTrend);
            Assert.Empty(vm.ConsistencyTrend);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // 4. ApplyOutcomeProfile fills recovery / exercise / goal / insight props
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ApplyOutcomeProfile_FilledOutcome_SetsRecoveryProperties()
        {
            var vm      = MakeVm();
            var outcome = MakeOutcome(
                recoveryScore:  82.0,
                recoveryStatus: "WellRecovered",
                overtraining:   false,
                recoveryDebt:   5.0,
                recommendation: "Godt restituert.");

            vm.ApplyOutcomeProfile(outcome);

            Assert.Equal(82.0,               vm.RecoveryScore0to100,    precision: 6);
            Assert.Equal("WellRecovered",    vm.RecoveryStatus);
            Assert.False(vm.OvertrainingPredicted);
            Assert.Equal(5.0,                vm.RecoveryDebt,           precision: 6);
            Assert.Equal("Godt restituert.", vm.RecoveryRecommendation);
        }

        [Fact]
        public void ApplyOutcomeProfile_OvertrainingTrue_SetsFlag()
        {
            var vm      = MakeVm();
            var outcome = MakeOutcome(overtraining: true);

            vm.ApplyOutcomeProfile(outcome);

            Assert.True(vm.OvertrainingPredicted);
        }

        [Fact]
        public void ApplyOutcomeProfile_RankedExercises_PopulatesCollection()
        {
            var vm = MakeVm();
            var ranked = new[]
            {
                new ExerciseEffectivenessProfile
                {
                    ExerciseId = 3, CompositeEffectiveness = 78.0,
                    ResonanceGain = 1.5, ComfortGain = 0.8,
                    ConsistencyGain = 0.4, RecoveryCost = 20.0,
                    UserSuccessRate = 85.0, SessionCount = 8,
                    HasEnoughData = true, Explanation = "test"
                },
                new ExerciseEffectivenessProfile
                {
                    ExerciseId = 7, CompositeEffectiveness = 62.0,
                    ResonanceGain = 0.5, ComfortGain = 0.2,
                    ConsistencyGain = 0.1, RecoveryCost = 40.0,
                    UserSuccessRate = 70.0, SessionCount = 5,
                    HasEnoughData = true, Explanation = "test2"
                },
            };
            var outcome = MakeOutcome(ranked: ranked);

            vm.ApplyOutcomeProfile(outcome);

            Assert.Equal(2, vm.RankedExercises.Count);
            Assert.Equal(3,    vm.RankedExercises[0].ExerciseId);
            Assert.Equal(78.0, vm.RankedExercises[0].CompositeEffectiveness, precision: 6);
            Assert.Equal(7,    vm.RankedExercises[1].ExerciseId);
        }

        [Fact]
        public void ApplyOutcomeProfile_WithConcerns_PopulatesConcernCollection()
        {
            var vm       = MakeVm();
            var concerns = new[]
            {
                new ExerciseEffectivenessFlag
                {
                    ExerciseId = 5, ReasonCode = "HIGH_RECOVERY_COST",
                    Explanation = "Krevende øvelse.", Magnitude = 82.0
                }
            };
            var outcome = MakeOutcome(concerns: concerns);

            vm.ApplyOutcomeProfile(outcome);

            Assert.Single(vm.ExerciseConcerns);
            Assert.Equal(5,                     vm.ExerciseConcerns[0].ExerciseId);
            Assert.Equal("HIGH_RECOVERY_COST",  vm.ExerciseConcerns[0].ReasonCode);
        }

        [Fact]
        public void ApplyOutcomeProfile_WithGoals_PopulatesGoalProgressItems()
        {
            var vm    = MakeVm();
            var goals = new[]
            {
                new GoalProgressEntry
                {
                    GoalType       = "resonance",
                    CurrentValue   = 60.0,
                    TargetValue    = 80.0,
                    PercentComplete = 75.0,
                    DeltaToGoal    = 20.0,
                    IsAchieved     = false,
                    PrimaryFocus   = VoiceDimension.Resonance
                }
            };
            var outcome = MakeOutcome(goals: goals);

            vm.ApplyOutcomeProfile(outcome);

            Assert.Single(vm.GoalProgressItems);
            Assert.Equal("resonance", vm.GoalProgressItems[0].GoalType);
            Assert.Equal(75.0,        vm.GoalProgressItems[0].PercentComplete, precision: 6);
        }

        [Fact]
        public void ApplyOutcomeProfile_WithLongitudinalInsights_PopulatesInsights()
        {
            var vm = MakeVm();

            // Build an OutcomeProfile that has an insight in LongTermDevelopment.
            var insight = new LongitudinalInsight
            {
                ReasonCode = "IMPROVEMENT",
                Dimension  = VoiceDimension.Resonance,
                Confidence = 80.0,
                What       = "Resonans har forbedret seg.",
                Why        = "Treningen gir resultater.",
                Evidence   = new[] { "slope=+1.5", "sessions=6" }
            };

            var outcome = new OutcomeProfile
            {
                UserId        = 1,
                GeneratedAt   = At,
                HasEnoughData = true,
                RecoveryProgress      = new RecoveryProgress(),
                ExerciseEffectiveness = new ExerciseEffectivenessSummary(),
                GoalProgress          = new GoalProgress(),
                LongTermDevelopment   = new LongTermDevelopment
                {
                    Insights = new[] { insight }
                }
            };

            vm.ApplyOutcomeProfile(outcome);

            Assert.Single(vm.Insights);
            Assert.Equal("Resonans har forbedret seg.", vm.Insights[0]);
        }

        [Fact]
        public void ApplyOutcomeProfile_Null_ClearsAllCollectionsAndZeroesRecovery()
        {
            var vm = MakeVm();
            // Populate
            vm.ApplyOutcomeProfile(MakeOutcome(recoveryScore: 90.0));
            // Clear
            vm.ApplyOutcomeProfile(null);

            Assert.Equal(0.0, vm.RecoveryScore0to100, precision: 6);
            Assert.Empty(vm.RecoveryStatus);
            Assert.Empty(vm.RankedExercises);
            Assert.Empty(vm.ExerciseConcerns);
            Assert.Empty(vm.GoalProgressItems);
            Assert.Empty(vm.Insights);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // 5. ApplyLearningPathFromDevProfile fills stage / strengths / weaknesses
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ApplyLearningPathFromDevProfile_HighComposite_ReturnsAdvancedStage()
        {
            var vm      = MakeVm();
            var profile = MakeDevProfile(compositeScore: 85.0);
            var point   = MakeTrendPoint(resonance: 90, comfort: 80, consistency: 75, pitch: 40);

            vm.ApplyLearningPathFromDevProfile(profile, point);

            Assert.Equal("Maintaining", vm.LearningStageLabel);
            Assert.Equal("Established",  vm.ConfidenceLabel);
        }

        [Fact]
        public void ApplyLearningPathFromDevProfile_LowComposite_ReturnsFoundation()
        {
            var vm      = MakeVm();
            var profile = MakeDevProfile(compositeScore: 20.0);
            var point   = MakeTrendPoint();

            vm.ApplyLearningPathFromDevProfile(profile, point);

            Assert.Equal("Foundation", vm.LearningStageLabel);
        }

        [Fact]
        public void ApplyLearningPathFromDevProfile_FillsStrengthsAndWeaknesses()
        {
            var vm      = MakeVm();
            var profile = MakeDevProfile(compositeScore: 55.0);
            // High resonance/comfort = strengths; low pitch = weakness
            var point = MakeTrendPoint(
                resonance:   88.0,
                comfort:     80.0,
                consistency: 75.0,
                recovery:    40.0,   // weak
                intonation:  70.0,
                vocalWeight: 65.0,
                pitch:       30.0);  // weak

            vm.ApplyLearningPathFromDevProfile(profile, point);

            // Should have some strengths (>0 score)
            Assert.NotEmpty(vm.Strengths);
            // Weaknesses: recovery (40) and pitch (30) are both < 60
            Assert.NotEmpty(vm.Weaknesses);
            // Focus areas map from weaknesses
            Assert.NotEmpty(vm.FocusAreas);
        }

        [Fact]
        public void ApplyLearningPathFromDevProfile_NullProfile_ClearsAll()
        {
            var vm = MakeVm();
            vm.ApplyLearningPathFromDevProfile(MakeDevProfile(), MakeTrendPoint());

            // Clear
            vm.ApplyLearningPathFromDevProfile(null, null);

            Assert.Empty(vm.LearningStageLabel);
            Assert.Empty(vm.FocusAreas);
            Assert.Empty(vm.Strengths);
            Assert.Empty(vm.Weaknesses);
        }

        [Fact]
        public void ApplyLearningPathFromDevProfile_NoEnoughData_ClearsAll()
        {
            var vm      = MakeVm();
            var profile = MakeDevProfile(hasEnoughData: false);

            vm.ApplyLearningPathFromDevProfile(profile, MakeTrendPoint());

            Assert.Empty(vm.LearningStageLabel);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // 6. Full Apply chain does not throw on empty / zero inputs
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void AllApplyMethods_EmptyInputs_DegradeGracefully_NoException()
        {
            var vm = MakeVm();

            // None of these must throw.
            vm.ApplyDevelopmentProfile(null);
            vm.ApplyLatestVoiceMetrics(null);
            vm.ApplyOutcomeProfile(null);
            vm.ApplyLearningPathFromDevProfile(null, null);

            // Collections remain empty, scalars remain 0.
            Assert.Equal(0.0, vm.CompositeScore,       precision: 6);
            Assert.Equal(0.0, vm.ResonanceScore,       precision: 6);
            Assert.Equal(0.0, vm.RecoveryScore0to100,  precision: 6);
            Assert.Empty(vm.RankedExercises);
            Assert.Empty(vm.ComfortTrend);
            Assert.Empty(vm.FocusAreas);
            Assert.Empty(vm.Insights);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // 7. Multiple trend windows produce matching collection lengths
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ApplyDevelopmentProfile_TwoWindows_TrendCollectionsHaveTwoEntries()
        {
            var vm = MakeVm();

            var slopes1 = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Comfort]     = 2.0,
                [VoiceDimension.Resonance]   = 1.0,
                [VoiceDimension.Consistency] = 0.5
            };
            var slopes2 = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Comfort]     = -0.5,
                [VoiceDimension.Resonance]   = 1.5,
                [VoiceDimension.Consistency] = 0.8
            };

            var w1 = new TrendWindow
            {
                WindowDays = 7,  From = At.AddDays(-14), To = At.AddDays(-7),
                SessionCount = 3, HasEnoughData = true, CompositeMean = 60,
                DimensionSlopes = slopes1
            };
            var w2 = new TrendWindow
            {
                WindowDays = 7,  From = At.AddDays(-7), To = At,
                SessionCount = 4, HasEnoughData = true, CompositeMean = 65,
                DimensionSlopes = slopes2
            };

            var profile = new VoiceDevelopmentProfile
            {
                UserId              = 1,
                GeneratedAt         = At,
                WeeklyTrend         = new[] { w1, w2 },
                MonthlyTrend        = Array.Empty<TrendWindow>(),
                CompositeVoiceScore = 62.5,
                HasEnoughData       = true
            };

            vm.ApplyDevelopmentProfile(profile);

            // Two windows → two entries each.
            Assert.Equal(2, vm.ComfortTrend.Count);
            Assert.Equal(2, vm.ResonanceTrend.Count);
            Assert.Equal(2, vm.ConsistencyTrend.Count);

            // Values match the slopes we injected.
            Assert.Equal( 2.0, vm.ComfortTrend[0].Value,    precision: 6);
            Assert.Equal(-0.5, vm.ComfortTrend[1].Value,    precision: 6);
            Assert.Equal( 1.5, vm.ResonanceTrend[1].Value,  precision: 6);
        }
    }
}
