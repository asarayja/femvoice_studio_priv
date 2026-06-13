using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="RecoveryScorer"/> — the Recovery (restitution)
    /// dimension, 0–100, high in the Health hierarchy. No mocking: the scorer is a
    /// pure function over a plain input struct, so tests exercise the real class with
    /// hand-computed expected values.
    ///
    /// Weights/thresholds under test (mirror the production XML docs):
    ///   safety −18/ea (cap 90, dominant), strain −9/ea (cap 45), pause −6/ea (cap 30),
    ///   fatigue −3/ea (cap 36), hydration −1.5/ea (cap 12);
    ///   trend −2.5 per (recent−prior) unit (cap 20);
    ///   overtraining (sessions>5 AND hours<12): (sessions−5)×4×restShortfall (cap 30);
    ///   rest reward +4 per full 12h (cap 12); status ≥75 / ≥50 / ≥25 / else.
    /// </summary>
    public class RecoveryScorerTests
    {
        private static readonly RecoveryScorer Scorer = new();

        // ──────────────────────────────────────────────────────────────────────
        // 1. New user / empty history ⇒ neutral-high default (well rested).
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_EmptyInput_ReturnsFullyRested()
        {
            // Arrange — default struct: every signal 0, no history.
            var input = new RecoveryScoreInput();

            // Act
            var result = Scorer.Score(input);

            // Assert — nothing to recover from ⇒ 100 / WellRecovered.
            Assert.Equal(100.0, result.Score, 6);
            Assert.Equal(RecoveryStatus.WellRecovered, result.Status);
            Assert.True(
                result.Explanation.Contains("well rested", StringComparison.OrdinalIgnoreCase)
                || result.Explanation.Contains("godt uthvilt", StringComparison.OrdinalIgnoreCase),
                result.Explanation);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 2. Heavy fatigue + strain ⇒ low score / strained-or-worse.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_HeavyFatigueAndStrain_ReturnsLowScore()
        {
            // Arrange — 5 fatigue, 4 strain. No safety/pause, no trend, no rest.
            // fatiguePenalty = min(5*3, 36) = 15; strainPenalty = min(4*9, 45) = 36.
            // raw = 100 - 15 - 36 = 49 ⇒ Strained (49 < 50).
            var input = new RecoveryScoreInput
            {
                RecentFatigueIndicators = 5,
                PriorFatigueIndicators = 5, // flat trend so trend does not add
                RecentStrainEpisodes = 4
            };

            // Act
            var result = Scorer.Score(input);

            // Assert
            Assert.Equal(49.0, result.Score, 6);
            Assert.Equal(RecoveryStatus.Strained, result.Status);
            Assert.True(result.Score < 50);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 3. Fatigue TREND — rising fatigue scores lower than a flat history with
        //    the same recent count.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_RisingFatigueTrend_ScoresLowerThanFlatTrend()
        {
            // Both have RecentFatigueIndicators = 6 ⇒ fatiguePenalty = min(18, 36) = 18.
            // Rising: prior = 2 ⇒ delta 4 ⇒ trendPenalty = min(4*2.5, 20) = 10 ⇒ raw 100-18-10 = 72.
            // Flat:   prior = 6 ⇒ delta 0 ⇒ trendPenalty 0                      ⇒ raw 100-18    = 82.
            var rising = new RecoveryScoreInput
            {
                RecentFatigueIndicators = 6,
                PriorFatigueIndicators = 2
            };
            var flat = new RecoveryScoreInput
            {
                RecentFatigueIndicators = 6,
                PriorFatigueIndicators = 6
            };

            var risingResult = Scorer.Score(rising);
            var flatResult = Scorer.Score(flat);

            Assert.Equal(72.0, risingResult.Score, 6);
            Assert.Equal(82.0, flatResult.Score, 6);
            Assert.True(risingResult.Score < flatResult.Score);
        }

        [Fact]
        public void Score_ImprovingFatigueTrend_IsNotPenalisedForTrend()
        {
            // Recent < prior (getting better). Trend must add nothing — only the recent
            // fatigue count itself penalises. recent 3 ⇒ fatiguePenalty 9 ⇒ raw 91.
            var input = new RecoveryScoreInput
            {
                RecentFatigueIndicators = 3,
                PriorFatigueIndicators = 8
            };

            var result = Scorer.Score(input);

            Assert.Equal(91.0, result.Score, 6);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 4. Overtraining — high density with little rest is penalised; the SAME
        //    density with ample rest is not (and even earns rest reward).
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_OvertrainingDenseAndUnrested_IsPenalised()
        {
            // sessions 8 (>5), hours 2 (<12). restShortfall = (12-2)/12 = 0.83333.
            // overtrain = min((8-5)*4*0.83333, 30) = min(10, 30) = 10. No rest reward (2<12).
            // raw = 100 - 10 = 90.
            var input = new RecoveryScoreInput
            {
                SessionsLast7Days = 8,
                HoursSinceLastSession = 2.0
            };

            var result = Scorer.Score(input);

            Assert.Equal(90.0, result.Score, 5);
            Assert.True(
                result.Explanation.Contains("training density", StringComparison.OrdinalIgnoreCase)
                || result.Explanation.Contains("treningsmengde", StringComparison.OrdinalIgnoreCase),
                result.Explanation);
        }

        [Fact]
        public void Score_SameDensityButWellRested_NotOvertrainedAndScoresHigher()
        {
            // Same 8 sessions, but 36h rest ⇒ hours >= 12 ⇒ NOT overtraining.
            // restReward = min(floor(36/12)*4, 12) = min(12, 12) = 12 ⇒ raw 112 ⇒ clamp 100.
            var dense = new RecoveryScoreInput { SessionsLast7Days = 8, HoursSinceLastSession = 2.0 };
            var rested = new RecoveryScoreInput { SessionsLast7Days = 8, HoursSinceLastSession = 36.0 };

            var denseResult = Scorer.Score(dense);
            var restedResult = Scorer.Score(rested);

            Assert.Equal(100.0, restedResult.Score, 6);
            Assert.True(restedResult.Score > denseResult.Score);
        }

        [Fact]
        public void Score_DensityAtThreshold_DoesNotTriggerOvertraining()
        {
            // Exactly 5 sessions is NOT > 5 ⇒ no overtraining penalty, even with 0h rest.
            var input = new RecoveryScoreInput
            {
                SessionsLast7Days = 5,
                HoursSinceLastSession = 0.0
            };

            var result = Scorer.Score(input);

            Assert.Equal(100.0, result.Score, 6);
            Assert.DoesNotContain("training density", result.Explanation, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("treningsmengde", result.Explanation, StringComparison.OrdinalIgnoreCase);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 5. Safety locks dominate — a single lock outweighs everything else and
        //    pushes the score deep into the strained/overtrained range.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_SingleSafetyLock_DominatesAndLowersScoreHard()
        {
            // One safety lock = −18, far heavier than any other single signal.
            // raw = 100 - 18 = 82. Explanation flags it as dominant.
            var input = new RecoveryScoreInput { RecentSafetyLocks = 1 };

            var result = Scorer.Score(input);

            Assert.Equal(82.0, result.Score, 6);
            Assert.True(
                result.Explanation.Contains("safety lock", StringComparison.OrdinalIgnoreCase)
                || result.Explanation.Contains("safety-pause", StringComparison.OrdinalIgnoreCase),
                result.Explanation);
            Assert.True(
                result.Explanation.Contains("dominant", StringComparison.OrdinalIgnoreCase)
                || result.Explanation.Contains("dominerende", StringComparison.OrdinalIgnoreCase),
                result.Explanation);
        }

        [Fact]
        public void Score_SafetyLocksOutweighEqualCountOfFatigue()
        {
            // 3 safety locks (−54) vs 3 fatigue indicators (−9): same count, safety far worse.
            // Flat prior fatigue on the fatigue case so the rising-trend term stays out of it.
            var locks = new RecoveryScoreInput { RecentSafetyLocks = 3 };
            var fatigue = new RecoveryScoreInput { RecentFatigueIndicators = 3, PriorFatigueIndicators = 3 };

            var locksResult = Scorer.Score(locks);
            var fatigueResult = Scorer.Score(fatigue);

            Assert.Equal(46.0, locksResult.Score, 6);   // 100 - 54
            Assert.Equal(91.0, fatigueResult.Score, 6);  // 100 - 9
            Assert.True(locksResult.Score < fatigueResult.Score);
        }

        [Fact]
        public void Score_ManySafetyLocks_DriveTowardOvertrained()
        {
            // 6 safety locks: 6*18 = 108, capped at 90. raw = 100 - 90 = 10 ⇒ Overtrained.
            var input = new RecoveryScoreInput { RecentSafetyLocks = 6 };

            var result = Scorer.Score(input);

            Assert.Equal(10.0, result.Score, 6);
            Assert.Equal(RecoveryStatus.Overtrained, result.Status);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 6. Clamping — score never leaves [0,100] no matter how extreme the input.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_CatastrophicLoad_ClampsToZero()
        {
            // Everything maxed. Sum of capped penalties (90+45+30+36+12) + trend 20 = 233 ⇒
            // raw deeply negative ⇒ clamp 0.
            var input = new RecoveryScoreInput
            {
                RecentSafetyLocks = 10,
                RecentStrainEpisodes = 10,
                RecentPauseRecommendations = 10,
                RecentFatigueIndicators = 20,
                PriorFatigueIndicators = 0,
                HydrationSuggestionsRecent = 20,
                SessionsLast7Days = 14,
                HoursSinceLastSession = 0.0
            };

            var result = Scorer.Score(input);

            Assert.Equal(0.0, result.Score, 6);
            Assert.Equal(RecoveryStatus.Overtrained, result.Status);
            Assert.InRange(result.Score, 0.0, 100.0);
        }

        [Fact]
        public void Score_RestRewardNeverExceeds100()
        {
            // Huge rest, no load. Reward is capped (+12) and the final clamp keeps it ≤100.
            var input = new RecoveryScoreInput { HoursSinceLastSession = 1000.0 };

            var result = Scorer.Score(input);

            Assert.Equal(100.0, result.Score, 6);
            Assert.InRange(result.Score, 0.0, 100.0);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 7. Robustness — negative counts and non-finite hours are sanitised to 0,
        //    yielding a sane (high) score rather than NaN/garbage.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_NegativeAndNonFiniteInputs_AreSanitised()
        {
            var input = new RecoveryScoreInput
            {
                RecentFatigueIndicators = -5,
                PriorFatigueIndicators = -3,
                RecentStrainEpisodes = -2,
                RecentSafetyLocks = -1,
                RecentPauseRecommendations = -4,
                HydrationSuggestionsRecent = -7,
                SessionsLast7Days = -9,
                HoursSinceLastSession = double.NaN
            };

            var result = Scorer.Score(input);

            // All negatives → 0, NaN hours → 0 ⇒ no penalties, no reward ⇒ 100.
            Assert.Equal(100.0, result.Score, 6);
            Assert.InRange(result.Score, 0.0, 100.0);
            Assert.Equal(RecoveryStatus.WellRecovered, result.Status);
        }

        [Fact]
        public void Score_InfiniteHours_DoesNotProduceInfiniteScore()
        {
            var input = new RecoveryScoreInput
            {
                RecentFatigueIndicators = 4,
                PriorFatigueIndicators = 4, // flat trend so only the fatigue count penalises
                HoursSinceLastSession = double.PositiveInfinity
            };

            var result = Scorer.Score(input);

            // Infinity sanitised to 0 ⇒ no rest reward ⇒ raw 100 - 12 = 88.
            Assert.Equal(88.0, result.Score, 6);
            Assert.InRange(result.Score, 0.0, 100.0);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 8. Status boundaries — exact thresholds map to the right bucket.
        // ──────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(100.0, RecoveryStatus.WellRecovered)]
        [InlineData(75.0, RecoveryStatus.WellRecovered)]
        [InlineData(74.999, RecoveryStatus.Adequate)]
        [InlineData(50.0, RecoveryStatus.Adequate)]
        [InlineData(49.999, RecoveryStatus.Strained)]
        [InlineData(25.0, RecoveryStatus.Strained)]
        [InlineData(24.999, RecoveryStatus.Overtrained)]
        [InlineData(0.0, RecoveryStatus.Overtrained)]
        public void ClassifyStatus_AtThresholdBoundaries_MapsToExpectedBucket(
            double score, RecoveryStatus expected)
        {
            Assert.Equal(expected, RecoveryScorer.ClassifyStatus(score));
        }

        // ──────────────────────────────────────────────────────────────────────
        // 9. Explanation is always present and traceable to the dominant driver.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_Explanation_NamesTheActiveDrivers()
        {
            var input = new RecoveryScoreInput
            {
                RecentStrainEpisodes = 2,
                RecentPauseRecommendations = 1,
                HydrationSuggestionsRecent = 3
            };

            var result = Scorer.Score(input);

            Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
            Assert.True(
                result.Explanation.Contains("strain", StringComparison.OrdinalIgnoreCase)
                || result.Explanation.Contains("belastning", StringComparison.OrdinalIgnoreCase),
                result.Explanation);
            Assert.True(
                result.Explanation.Contains("pause", StringComparison.OrdinalIgnoreCase),
                result.Explanation);
            Assert.True(
                result.Explanation.Contains("hydration", StringComparison.OrdinalIgnoreCase)
                || result.Explanation.Contains("hydrering", StringComparison.OrdinalIgnoreCase),
                result.Explanation);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 10. Monotonicity — adding load never raises the recovery score.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_AddingFatigue_NeverRaisesScore()
        {
            var baseline = new RecoveryScoreInput { RecentFatigueIndicators = 2, PriorFatigueIndicators = 2 };
            var worse = baseline with { RecentFatigueIndicators = 3 };

            var baselineResult = Scorer.Score(baseline);
            var worseResult = Scorer.Score(worse);

            Assert.True(worseResult.Score <= baselineResult.Score);
        }

        [Fact]
        public void Score_PauseRecommendations_LowerScoreByExpectedAmount()
        {
            // 2 pause recommendations = −12. raw = 100 - 12 = 88.
            var input = new RecoveryScoreInput { RecentPauseRecommendations = 2 };

            var result = Scorer.Score(input);

            Assert.Equal(88.0, result.Score, 6);
        }
    }
}
