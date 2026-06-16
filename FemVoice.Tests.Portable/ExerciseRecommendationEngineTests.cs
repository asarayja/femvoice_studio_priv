using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ExerciseRecommendationEngine"/> — the unified, explainable
    /// next-exercise recommender that replaces the random training loop.
    ///
    /// The clinical ordering under test (ufravikelig):
    /// Safety &gt; Health &gt; Recovery &gt; Comfort &gt; Goals &gt; Progression &gt; Coaching.
    ///
    /// No mocking — the engine is a pure function over plain data; tests build real
    /// inputs (records/structs) and assert on the deterministic result.
    /// </summary>
    public class ExerciseRecommendationEngineTests
    {
        private static ExerciseRecommendationEngine Engine() => new();

        // Exercise-id buckets mirror ComplexityEngine over the REAL catalog (1..15).
        // The phantom 16..50 buckets are gone; IsolatedSounds = {1,2,3} gives the three
        // ids the variation/mastery-ranking mechanics below need.
        private static readonly HashSet<int> LightPool = Enumerable.Range(1, 15).ToHashSet();
        private static readonly HashSet<int> LevelPool = new() { 1, 2, 3 };

        private static RecoveryResult Recovery(double score, RecoveryStatus status)
            => new() { Score = score, Status = status, Explanation = "test" };

        private static VoiceIntelligenceTrendPoint Scores(
            double resonance = 70, double comfort = 70, double consistency = 70,
            double intonation = 70, double vocalWeight = 70, double recovery = 70,
            double pitch = 70, double composite = 70)
            => new()
            {
                SessionId = 1,
                ResonanceScore100 = resonance,
                ComfortScore100 = comfort,
                ConsistencyScore100 = consistency,
                IntonationScore100 = intonation,
                VocalWeightScore100 = vocalWeight,
                RecoveryScore100 = recovery,
                PitchScore100 = pitch,
                CompositeVoiceScore = composite
            };

        // ── 1. Low recovery (Overtrained) ⇒ recovery-oriented/light, EVEN with a goal ──
        [Fact]
        public void RecommendNext_WhenOvertrained_ReturnsRecoveryOrientedLightExercise()
        {
            // Resonance is rock-bottom (a strong "train resonance" pull), but recovery is
            // overtrained — Health > Goals must win and force a light recovery pick.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(10, RecoveryStatus.Overtrained),
                LatestVoiceScores = Scores(resonance: 5),
                GoalProfile = new VoiceGoalProfile { PrimaryFocus = "resonance" },
                ComplexityLevel = SpeechComplexityLevel.Conversational // would normally be hard
            };

            var result = Engine().RecommendNext(input);

            Assert.True(result.IsRecoveryOriented);
            Assert.Equal(RecommendationFocus.Recovery, result.Focus);
            Assert.Equal("recovery", result.FocusArea);
            // Forced into the lightest pool despite a Conversational level.
            Assert.Contains(result.ExerciseId, LightPool);
        }

        // ── 2. Strained recovery also forces recovery orientation (Health > Goals) ─────
        [Fact]
        public void RecommendNext_WhenStrained_IsRecoveryOriented()
        {
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(35, RecoveryStatus.Strained),
                LatestVoiceScores = Scores(resonance: 20),
                ComplexityLevel = SpeechComplexityLevel.Phrases
            };

            var result = Engine().RecommendNext(input);

            Assert.True(result.IsRecoveryOriented);
            Assert.Contains(result.ExerciseId, LightPool);
        }

        // ── 3. Low score but Adequate status still trips the score-based gate ──────────
        [Fact]
        public void RecommendNext_WhenRecoveryScoreAtThreshold_IsRecoveryOriented()
        {
            // Score == LowRecoveryThreshold (50) is "low" (inclusive) ⇒ recovery-oriented.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(ExerciseRecommendationEngine.LowRecoveryThreshold, RecoveryStatus.Adequate),
                LatestVoiceScores = Scores(),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences
            };

            var result = Engine().RecommendNext(input);

            Assert.True(result.IsRecoveryOriented);
            Assert.Equal(RecommendationFocus.Recovery, result.Focus);
        }

        // ── 4. Good recovery + weakest dimension drives the focus ──────────────────────
        [Fact]
        public void RecommendNext_WhenWellRecovered_FocusesOnWeakestDimension()
        {
            // Everything strong except consistency (very weak) ⇒ focus = consistency.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(consistency: 18),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences
            };

            var result = Engine().RecommendNext(input);

            Assert.False(result.IsRecoveryOriented);
            Assert.Equal(RecommendationFocus.Consistency, result.Focus);
            Assert.Equal("consistency", result.FocusArea);
        }

        // ── 5. Strictly-lower score wins over a higher-priority but stronger axis ───────
        [Fact]
        public void RecommendNext_PicksTheLowestScore_NotMerelyHighestPriority()
        {
            // Comfort (priority 1) is 52 (weak) but Intonation is 12 (much weaker).
            // Strictly-lower score wins ⇒ Intonation, even though Comfort outranks it.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(comfort: 52, intonation: 12),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences
            };

            var result = Engine().RecommendNext(input);

            Assert.Equal(RecommendationFocus.Intonation, result.Focus);
        }

        // ── 6. Equal weak scores ⇒ clinically higher-priority axis wins the tie ────────
        [Fact]
        public void RecommendNext_OnTie_HigherPriorityAxisWins()
        {
            // Comfort and Pitch both at 30. Comfort (priority 1) must beat Pitch (priority 5).
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(comfort: 30, pitch: 30),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences
            };

            var result = Engine().RecommendNext(input);

            Assert.Equal(RecommendationFocus.Comfort, result.Focus);
        }

        // ── 7. Unmeasured dimensions (neutral 50.0) never capture focus ────────────────
        [Fact]
        public void RecommendNext_IgnoresUnmeasuredNeutralDimensions()
        {
            // Pitch sits exactly on the neutral 50 fallback (unmeasured) — it must be
            // skipped. Resonance at 40 is the only real weak signal ⇒ focus = resonance.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(resonance: 40, pitch: 50.0),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences
            };

            var result = Engine().RecommendNext(input);

            Assert.Equal(RecommendationFocus.Resonance, result.Focus);
            Assert.NotEqual(RecommendationFocus.Pitch, result.Focus);
        }

        // ── 8. Empty history ⇒ sensible Foundation starter ─────────────────────────────
        [Fact]
        public void RecommendNext_WithNoHistory_ReturnsFoundationStarter()
        {
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(100, RecoveryStatus.WellRecovered), // new user, well rested
                LatestVoiceScores = null,
                ComplexityLevel = SpeechComplexityLevel.IsolatedSounds
            };

            var result = Engine().RecommendNext(input);

            Assert.Equal(RecommendationFocus.Foundation, result.Focus);
            Assert.False(result.IsRecoveryOriented);
            Assert.Contains(result.ExerciseId, LightPool);
            Assert.False(string.IsNullOrWhiteSpace(result.Rationale));
        }

        // ── 9. All-neutral scores are treated as no-history ⇒ Foundation ───────────────
        [Fact]
        public void RecommendNext_WithAllNeutralScores_ReturnsFoundation()
        {
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(
                    resonance: 50, comfort: 50, consistency: 50,
                    intonation: 50, vocalWeight: 50, pitch: 50),
                ComplexityLevel = SpeechComplexityLevel.Words
            };

            var result = Engine().RecommendNext(input);

            Assert.Equal(RecommendationFocus.Foundation, result.Focus);
        }

        // ── 10. Variation: the most recent exercise is not recommended again ───────────
        [Fact]
        public void RecommendNext_AvoidsRecentlyDoneExercise()
        {
            // Weak resonance ⇒ resonance focus. IsolatedSounds pool is {1,2,3}; ids 1 and 2
            // were just done, so the fresh 3 must be picked.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(resonance: 25),
                ComplexityLevel = SpeechComplexityLevel.IsolatedSounds,
                RecentExerciseIds = new[] { 1, 2 }
            };

            var result = Engine().RecommendNext(input);

            Assert.DoesNotContain(result.ExerciseId, new[] { 1, 2 });
            Assert.Contains(result.ExerciseId, LevelPool);
        }

        // ── 11. Mastered exercise is deprioritised in favour of a fresh one ────────────
        [Fact]
        public void RecommendNext_DeprioritisesMasteredExercise()
        {
            // Mark the natural first pick (1) Mastered — engine should lift a non-mastered
            // exercise to the top instead of drilling the solved one.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(resonance: 25),
                ComplexityLevel = SpeechComplexityLevel.IsolatedSounds,
                MasteryByExercise = new Dictionary<int, MasteryLevel> { [1] = MasteryLevel.Mastered }
            };

            var result = Engine().RecommendNext(input);

            Assert.NotEqual(1, result.ExerciseId);
            Assert.Contains(result.ExerciseId, LevelPool);
            // 1 is still a valid alternative, just not the primary.
            Assert.Contains(1, result.AlternativeExerciseIds);
        }

        // ── 12. Mastery penalty outweighs variation penalty (a deeper deprioritisation) ─
        [Fact]
        public void RecommendNext_MasteredOutranksMerelyRecent()
        {
            // 1 = Mastered (penalty 4), 2 = recently done (penalty 2). The fresh,
            // non-mastered 3 (penalty 0) should win; 2 should still outrank 1.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(resonance: 25),
                ComplexityLevel = SpeechComplexityLevel.IsolatedSounds,
                RecentExerciseIds = new[] { 2 },
                MasteryByExercise = new Dictionary<int, MasteryLevel> { [1] = MasteryLevel.Mastered }
            };

            var result = Engine().RecommendNext(input);

            Assert.Equal(3, result.ExerciseId);
            var orderOf2 = result.AlternativeExerciseIds.ToList().IndexOf(2);
            var orderOf1 = result.AlternativeExerciseIds.ToList().IndexOf(1);
            Assert.True(orderOf2 >= 0 && orderOf1 >= 0 && orderOf2 < orderOf1);
        }

        // ── 13. Style colours focus: DarkFeminine is NOT pushed toward bright resonance ─
        [Fact]
        public void RecommendNext_DarkFeminineGoal_DoesNotChaseResonance()
        {
            // Resonance is the weakest raw axis (20), but for a DarkFeminine goal low
            // resonance is intentional — the engine must NOT pick resonance focus.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                UserProfile = new UserVoiceProfile { PreferredVoiceStyle = VoiceStyleGoal.DarkFeminine },
                LatestVoiceScores = Scores(resonance: 20, vocalWeight: 35),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences
            };

            var result = Engine().RecommendNext(input);

            Assert.NotEqual(RecommendationFocus.Resonance, result.Focus);
            // VocalWeight (35) is the next genuine weakness ⇒ on-target for a darker goal.
            Assert.Equal(RecommendationFocus.VocalWeight, result.Focus);
        }

        // ── 14. A Feminine goal DOES train resonance when it is weakest ────────────────
        [Fact]
        public void RecommendNext_FeminineGoal_TrainsResonanceWhenWeak()
        {
            // Same weak resonance, but a Feminine goal — here resonance IS the right focus.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                UserProfile = new UserVoiceProfile { PreferredVoiceStyle = VoiceStyleGoal.Feminine },
                LatestVoiceScores = Scores(resonance: 20, vocalWeight: 35),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences
            };

            var result = Engine().RecommendNext(input);

            Assert.Equal(RecommendationFocus.Resonance, result.Focus);
        }

        // ── 15. No weak dimension ⇒ goal PrimaryFocus seeds the focus ──────────────────
        [Fact]
        public void RecommendNext_WhenNothingWeak_UsesGoalPrimaryFocus()
        {
            // All dimensions strong (no weakness), goal says "intonation" ⇒ focus = intonation.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                GoalProfile = new VoiceGoalProfile { PrimaryFocus = "intonation / melody" },
                LatestVoiceScores = Scores(
                    resonance: 80, comfort: 80, consistency: 80,
                    intonation: 80, vocalWeight: 80, pitch: 80),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences
            };

            var result = Engine().RecommendNext(input);

            Assert.Equal(RecommendationFocus.Intonation, result.Focus);
        }

        // ── 16. All-default input is total: a valid, light, recovery-safe recommendation ─
        [Fact]
        public void RecommendNext_WithDefaultInput_ReturnsValidRecommendation()
        {
            // default(struct): Recovery score 0 ⇒ low ⇒ recovery-oriented light pick.
            var result = Engine().RecommendNext(default);

            Assert.True(result.IsRecoveryOriented);
            Assert.Contains(result.ExerciseId, LightPool);
            Assert.False(string.IsNullOrWhiteSpace(result.Rationale));
            Assert.False(string.IsNullOrWhiteSpace(result.FocusArea));
        }

        // ── 17. Rationale is always populated and human-readable (explainability) ──────
        [Fact]
        public void RecommendNext_AlwaysProducesNonEmptyRationale()
        {
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(comfort: 30),
                ComplexityLevel = SpeechComplexityLevel.Words
            };

            var result = Engine().RecommendNext(input);

            Assert.False(string.IsNullOrWhiteSpace(result.Rationale));
            Assert.Contains("comfort", result.Rationale);
        }

        // ── A5-04. Flag penalty is PRIMARY; effectiveness is only a WITHIN-bucket tie-break;
        //          HasEnoughData=false is NEUTRAL (neither lift nor penalty). ──────────────
        [Fact]
        public void RankCandidates_FlagPenaltyAndEffectivenessTieBreak()
        {
            // Weak resonance + WellRecovered ⇒ focus = resonance, pool = IsolatedSounds {1,2,3}.
            // We craft the three ids so the ordering proves the two contractual properties:
            //
            //   • PENALTY-PRIMARY over effectiveness: id 2 is safety-FLAGGED (+FlagPenalty)
            //     yet carries the HIGHEST CompositeEffectiveness (95). An otherwise-equal but
            //     UNFLAGGED id 3 with a much LOWER effectiveness (10) must STILL outrank it —
            //     a flag can never be out-weighed by a better effectiveness number, because
            //     penalty is the primary sort key and effectiveness only breaks ties WITHIN a
            //     penalty bucket.
            //
            //   • HasEnoughData=false is NEUTRAL: id 1 has a low-data profile. It must be
            //     treated as the neutral midpoint (≈50) — neither lifted nor penalised. So it
            //     sorts ABOVE the evidenced-but-low id 3 (10 < 50) and BELOW where a genuine
            //     high score would put it — i.e. "insufficient evidence", never "ineffective".
            //
            // Expected final ranking (penalty asc, then effectiveness desc, then id):
            //   id 1 (penalty 0, eff≈50 neutral) → id 3 (penalty 0, eff 10) → id 2 (penalty 1).
            var effectiveness = new Dictionary<int, ExerciseEffectivenessProfile>
            {
                // Low-data ⇒ NEUTRAL. CompositeEffectiveness is deliberately set to an extreme
                // value to PROVE it is ignored while HasEnoughData is false (no lift/penalty).
                [1] = new ExerciseEffectivenessProfile
                {
                    ExerciseId = 1, HasEnoughData = false, CompositeEffectiveness = 99
                },
                // Flagged id carries the HIGHEST effectiveness — must still lose to the flag.
                [2] = new ExerciseEffectivenessProfile
                {
                    ExerciseId = 2, HasEnoughData = true, CompositeEffectiveness = 95
                },
                // Unflagged, evidenced, but LOW effectiveness — still beats the flagged id 2.
                [3] = new ExerciseEffectivenessProfile
                {
                    ExerciseId = 3, HasEnoughData = true, CompositeEffectiveness = 10
                },
            };

            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(resonance: 25),
                ComplexityLevel = SpeechComplexityLevel.IsolatedSounds,
                EffectivenessByExercise = effectiveness,
                FlaggedExerciseIds = new[] { 2 }
            };

            var result = Engine().RecommendNext(input);

            var ranking = new List<int> { result.ExerciseId };
            ranking.AddRange(result.AlternativeExerciseIds);

            // Full, deterministic order.
            Assert.Equal(new[] { 1, 3, 2 }, ranking);

            var posFlagged = ranking.IndexOf(2);   // flagged, eff 95
            var posUnflagged = ranking.IndexOf(3); // unflagged, eff 10
            var posNeutral = ranking.IndexOf(1);   // low-data ⇒ neutral

            // PENALTY PRIMARY: the flagged id sorts AFTER the otherwise-equal unflagged id
            // despite a strictly HIGHER CompositeEffectiveness.
            Assert.True(posUnflagged < posFlagged,
                "Unflagged id 3 must outrank flagged id 2 even though id 2 has higher effectiveness.");

            // HasEnoughData=false is NEUTRAL: the low-data id is NOT penalised to the bottom
            // (it beats the evidenced-but-low id 3 at the neutral midpoint) and is NOT lifted
            // by its inflated raw number (it does not jump the flagged id on effectiveness —
            // it only ranks ahead because the flagged id carries a penalty).
            Assert.True(posNeutral < posUnflagged,
                "Low-data (neutral ≈50) id 1 must outrank evidenced-but-low id 3.");
            Assert.True(posNeutral < posFlagged,
                "Low-data id 1 outranks flagged id 2 — but via the flag penalty, not an effectiveness lift.");

            // The flagged id is the single most de-prioritised candidate here.
            Assert.Equal(2, ranking[^1]);
        }

        // ── 18. Recovery pool never exceeds the lightest bucket, regardless of level ───
        [Fact]
        public void RecommendNext_RecoveryPool_NeverHarderThanLightBucket()
        {
            foreach (var level in new[]
                     {
                         SpeechComplexityLevel.Words, SpeechComplexityLevel.Phrases,
                         SpeechComplexityLevel.StructuredSentences, SpeechComplexityLevel.Conversational
                     })
            {
                var input = new ExerciseRecommendationInput
                {
                    Recovery = Recovery(5, RecoveryStatus.Overtrained),
                    LatestVoiceScores = Scores(resonance: 10),
                    ComplexityLevel = level
                };

                var result = Engine().RecommendNext(input);

                Assert.True(result.IsRecoveryOriented);
                Assert.Contains(result.ExerciseId, LightPool); // always ids 1..15
            }
        }
    }
}
