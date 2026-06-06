using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ClinicalSessionScore"/>.
    ///
    /// Clinical design rules under test (see the production summary):
    ///   • Weights mirror the profile flags — exercises score only on the dimensions
    ///     they actually train, and the active weights are re-normalised.
    ///   • Pitch contributes ONLY through comfort-zone compliance, never pitch height.
    ///   • Safety gates and the time floor are hard caps, not subtractions.
    ///   • A silent session (EvaluatedTicks == 0) earns nothing.
    /// </summary>
    public class ClinicalSessionScoreTests
    {
        // Fixed, deterministic durations used across tests. elapsed >= targetSeconds / 2
        // keeps the time-floor cap from firing unless a test deliberately sets it low.
        private const int TargetSeconds = 60;
        private const int FullElapsed = 60;

        // ──────────────────────────────────────────────────────────────────────
        // 1. Profile-weighted score — resonance profile scores on resonance,
        //    stability and hold, and the values are reflected in the result.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Calculate_ResonanceProfile_ReflectsResonanceStabilityAndHold()
        {
            // Arrange — CreateResonanceHumming: UsesResonance + UsesStability,
            // RequiredHoldSeconds = 3 > 0. UsesPitch = false.
            // Active weights: resonance 0.45, stability 0.25, hold 0.10 → total 0.90.
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var outcome = new ExerciseSessionOutcome
            {
                AverageResonance = 0.80,
                AverageStability = 0.60,
                HoldCompletion = 0.90,
                ComfortCompliance = 0.0, // ignored: UsesPitch = false on this profile
                EvaluatedTicks = 100
            };

            // Act
            var score = ClinicalSessionScore.Calculate(outcome, profile, FullElapsed, TargetSeconds);

            // Assert — weightedTotal = 0.80*0.45 + 0.60*0.25 + 0.90*0.10 = 0.60
            // score = (0.60 / 0.90) * 100 = 66.666…
            Assert.Equal(66.6667, score, 3);
        }

        [Fact]
        public void Calculate_ResonanceProfile_HigherResonanceRaisesScore()
        {
            // Arrange
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var weak = new ExerciseSessionOutcome
            {
                AverageResonance = 0.40,
                AverageStability = 0.60,
                HoldCompletion = 0.70,
                EvaluatedTicks = 100
            };
            var strong = weak with { AverageResonance = 0.90 };

            // Act
            var weakScore = ClinicalSessionScore.Calculate(weak, profile, FullElapsed, TargetSeconds);
            var strongScore = ClinicalSessionScore.Calculate(strong, profile, FullElapsed, TargetSeconds);

            // Assert — resonance carries the largest weight (0.45); raising it must lift the score.
            Assert.True(strongScore > weakScore);
        }

        [Fact]
        public void Calculate_ResonanceProfile_AllPerfectMetrics_Returns100()
        {
            // Arrange — every active dimension at 1.0 → weightedTotal == weight → 100.
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var outcome = new ExerciseSessionOutcome
            {
                AverageResonance = 1.0,
                AverageStability = 1.0,
                HoldCompletion = 1.0,
                EvaluatedTicks = 100
            };

            // Act
            var score = ClinicalSessionScore.Calculate(outcome, profile, FullElapsed, TargetSeconds);

            // Assert
            Assert.Equal(100, score, 6);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 2. Pitch neutrality (clinical core requirement).
        //    The outcome has NO pitch-height field at all — pitch enters the score
        //    ONLY via ComfortCompliance. Two outcomes with the same compliance are
        //    therefore necessarily scored identically; higher compliance scores higher.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Calculate_PitchProfile_SameComplianceGivesIdenticalScore_RegardlessOfUnderlyingPitch()
        {
            // Arrange — PitchExercise: UsesPitch + UsesStability, RequiredHoldSeconds = 3.
            // Active weights: stability 0.25, pitch 0.20, hold 0.10 → total 0.55.
            var profile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 220);

            // Two sessions that achieved the SAME comfort compliance. One was sung low
            // in the zone, the other high — but the outcome record carries no pitch height,
            // so only compliance can ever influence the score. They must match exactly.
            var lowInZone = new ExerciseSessionOutcome
            {
                ComfortCompliance = 0.80,
                AverageStability = 0.65,
                HoldCompletion = 0.50,
                EvaluatedTicks = 100
            };
            var highInZone = new ExerciseSessionOutcome
            {
                ComfortCompliance = 0.80,
                AverageStability = 0.65,
                HoldCompletion = 0.50,
                EvaluatedTicks = 100
            };

            // Act
            var lowScore = ClinicalSessionScore.Calculate(lowInZone, profile, FullElapsed, TargetSeconds);
            var highScore = ClinicalSessionScore.Calculate(highInZone, profile, FullElapsed, TargetSeconds);

            // Assert — pitch height can never move the score: identical, to the bit.
            Assert.Equal(lowScore, highScore);
        }

        [Fact]
        public void Calculate_PitchProfile_HigherComfortComplianceGivesHigherScore()
        {
            // Arrange — only ComfortCompliance differs between the two outcomes.
            var profile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 220);
            var lessCompliant = new ExerciseSessionOutcome
            {
                ComfortCompliance = 0.55, // above the 0.5 hard-cap so the cap does not mask the trend
                AverageStability = 0.65,
                HoldCompletion = 0.50,
                EvaluatedTicks = 100
            };
            var moreCompliant = lessCompliant with { ComfortCompliance = 0.95 };

            // Act
            var lessScore = ClinicalSessionScore.Calculate(lessCompliant, profile, FullElapsed, TargetSeconds);
            var moreScore = ClinicalSessionScore.Calculate(moreCompliant, profile, FullElapsed, TargetSeconds);

            // Assert — being in the zone more of the time is the only pitch reward.
            Assert.True(moreScore > lessScore);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 3. Hard caps.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Calculate_SafetyLockEpisode_CapsAt40_EvenWithPerfectMetrics()
        {
            // Arrange — every metric perfect, but one safety lock fired.
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var outcome = new ExerciseSessionOutcome
            {
                AverageResonance = 1.0,
                AverageStability = 1.0,
                HoldCompletion = 1.0,
                SafetyLockEpisodes = 1,
                EvaluatedTicks = 100
            };

            // Act
            var score = ClinicalSessionScore.Calculate(outcome, profile, FullElapsed, TargetSeconds);

            // Assert — a locked session can never look "good".
            Assert.True(score <= 40);
            Assert.Equal(40, score, 6);
        }

        [Fact]
        public void Calculate_PitchProfile_LowComfortCompliance_CapsAt55()
        {
            // Arrange — pitch profile, ComfortCompliance < 0.5. Stability/hold maxed so the
            // raw weighted score would otherwise exceed 55, proving the cap engages.
            var profile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 220);
            var outcome = new ExerciseSessionOutcome
            {
                ComfortCompliance = 0.40,
                AverageStability = 1.0,
                HoldCompletion = 1.0,
                EvaluatedTicks = 100
            };

            // Act
            var score = ClinicalSessionScore.Calculate(outcome, profile, FullElapsed, TargetSeconds);

            // Assert
            Assert.True(score <= 55);
        }

        [Fact]
        public void Calculate_SessionUnderHalfTargetDuration_CapsAt30()
        {
            // Arrange — perfect metrics, but elapsed (20s) < targetSeconds/2 (30s).
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var outcome = new ExerciseSessionOutcome
            {
                AverageResonance = 1.0,
                AverageStability = 1.0,
                HoldCompletion = 1.0,
                EvaluatedTicks = 100
            };

            // Act — 20 < 60 / 2 = 30 → time floor engages.
            var score = ClinicalSessionScore.Calculate(outcome, profile, elapsedSeconds: 20, targetSeconds: 60);

            // Assert — time is a floor requirement, not a point source.
            Assert.True(score <= 30);
            Assert.Equal(30, score, 6);
        }

        [Fact]
        public void Calculate_SessionAtExactlyHalfTargetDuration_DoesNotEngageTimeFloor()
        {
            // Arrange — elapsed == targetSeconds/2 is NOT under half, so the cap must not fire.
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var outcome = new ExerciseSessionOutcome
            {
                AverageResonance = 1.0,
                AverageStability = 1.0,
                HoldCompletion = 1.0,
                EvaluatedTicks = 100
            };

            // Act — 30 is not < 60 / 2 = 30.
            var score = ClinicalSessionScore.Calculate(outcome, profile, elapsedSeconds: 30, targetSeconds: 60);

            // Assert
            Assert.Equal(100, score, 6);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 4. Silent session — EvaluatedTicks == 0 → 0.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Calculate_NoEvaluatedTicks_ReturnsZero()
        {
            // Arrange — would-be-perfect metrics but no evaluated voice data.
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var outcome = new ExerciseSessionOutcome
            {
                AverageResonance = 1.0,
                AverageStability = 1.0,
                HoldCompletion = 1.0,
                EvaluatedTicks = 0
            };

            // Act
            var score = ClinicalSessionScore.Calculate(outcome, profile, FullElapsed, TargetSeconds);

            // Assert — a silent session earns nothing.
            Assert.Equal(0, score);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 5. Weight redistribution — a profile with no hold (RequiredHoldSeconds = 0,
        //    e.g. IntonationExercise) ignores HoldCompletion entirely.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Calculate_ProfileWithoutHold_IgnoresHoldCompletion()
        {
            // Arrange — IntonationExercise: UsesPitch + UsesStability, RequiredHoldSeconds = 0.
            // Active weights: pitch 0.20, stability 0.25 → total 0.45 (no hold term).
            var profile = ExerciseTargetProfile.IntonationExercise();
            var noHold = new ExerciseSessionOutcome
            {
                ComfortCompliance = 0.70,
                AverageStability = 0.60,
                HoldCompletion = 0.0,
                EvaluatedTicks = 100
            };
            var fullHold = noHold with { HoldCompletion = 1.0 };

            // Act
            var noHoldScore = ClinicalSessionScore.Calculate(noHold, profile, FullElapsed, TargetSeconds);
            var fullHoldScore = ClinicalSessionScore.Calculate(fullHold, profile, FullElapsed, TargetSeconds);

            // Assert — hold is not part of the weight set, so it cannot move the score.
            Assert.Equal(noHoldScore, fullHoldScore);
        }

        [Fact]
        public void Calculate_IntonationProfile_UsesRedistributedPitchAndStabilityWeights()
        {
            // Arrange — verify the exact re-normalised math for the holdless profile.
            // Active weights: pitch 0.20, stability 0.25 → total 0.45.
            var profile = ExerciseTargetProfile.IntonationExercise();
            var outcome = new ExerciseSessionOutcome
            {
                ComfortCompliance = 0.80,
                AverageStability = 0.60,
                HoldCompletion = 0.99, // deliberately high — must be ignored
                EvaluatedTicks = 100
            };

            // Act
            var score = ClinicalSessionScore.Calculate(outcome, profile, FullElapsed, TargetSeconds);

            // Assert — weightedTotal = 0.80*0.20 + 0.60*0.25 = 0.16 + 0.15 = 0.31
            // score = (0.31 / 0.45) * 100 = 68.888…
            Assert.Equal(68.8889, score, 3);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 6. Argument validation.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Calculate_NullOutcome_ThrowsArgumentNullException()
        {
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            Assert.Throws<ArgumentNullException>(
                () => ClinicalSessionScore.Calculate(null!, profile, FullElapsed, TargetSeconds));
        }

        [Fact]
        public void Calculate_NullProfile_ThrowsArgumentNullException()
        {
            var outcome = new ExerciseSessionOutcome { EvaluatedTicks = 100 };
            Assert.Throws<ArgumentNullException>(
                () => ClinicalSessionScore.Calculate(outcome, null!, FullElapsed, TargetSeconds));
        }

        // ──────────────────────────────────────────────────────────────────────
        // 7. Clamping — never below 0, never above 100.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Calculate_OutOfRangeMetrics_ClampedToValidScoreRange()
        {
            // Arrange — metrics above their 0–1 domain. Per-metric Math.Clamp keeps the
            // weighted score at 100; the final Math.Clamp(score, 0, 100) is the safety net.
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var outcome = new ExerciseSessionOutcome
            {
                AverageResonance = 5.0,
                AverageStability = 5.0,
                HoldCompletion = 5.0,
                EvaluatedTicks = 100
            };

            // Act
            var score = ClinicalSessionScore.Calculate(outcome, profile, FullElapsed, TargetSeconds);

            // Assert
            Assert.InRange(score, 0, 100);
            Assert.Equal(100, score, 6);
        }

        [Fact]
        public void Calculate_NegativeMetrics_NeverGoBelowZero()
        {
            // Arrange — negative metrics clamp to 0 per dimension → weightedTotal 0 → score 0.
            var profile = ExerciseTargetProfile.CreateResonanceHumming();
            var outcome = new ExerciseSessionOutcome
            {
                AverageResonance = -3.0,
                AverageStability = -3.0,
                HoldCompletion = -3.0,
                EvaluatedTicks = 100
            };

            // Act
            var score = ClinicalSessionScore.Calculate(outcome, profile, FullElapsed, TargetSeconds);

            // Assert
            Assert.InRange(score, 0, 100);
            Assert.Equal(0, score, 6);
        }
    }
}
