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

        // Exercise-id buckets (mirror ComplexityEngine): sounds/syllables 1..15,
        // words/phrases 16..35, sentences/conversational 36..50.
        private static readonly HashSet<int> LightPool = Enumerable.Range(1, 15).ToHashSet();
        private static readonly HashSet<int> SentencePool = Enumerable.Range(36, 15).ToHashSet();

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
            // Weak resonance ⇒ resonance focus. Sentence pool is 36..50; the deterministic
            // first pick would be 36, but it was just done, so it must be pushed down.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(resonance: 25),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences,
                RecentExerciseIds = new[] { 36, 37, 38 }
            };

            var result = Engine().RecommendNext(input);

            Assert.DoesNotContain(result.ExerciseId, new[] { 36, 37, 38 });
            Assert.Contains(result.ExerciseId, SentencePool);
        }

        // ── 11. Mastered exercise is deprioritised in favour of a fresh one ────────────
        [Fact]
        public void RecommendNext_DeprioritisesMasteredExercise()
        {
            // Mark the natural first pick (36) Mastered — engine should lift a non-mastered
            // exercise to the top instead of drilling the solved one.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(resonance: 25),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences,
                MasteryByExercise = new Dictionary<int, MasteryLevel> { [36] = MasteryLevel.Mastered }
            };

            var result = Engine().RecommendNext(input);

            Assert.NotEqual(36, result.ExerciseId);
            Assert.Contains(result.ExerciseId, SentencePool);
            // 36 is still a valid alternative, just not the primary.
            Assert.Contains(36, result.AlternativeExerciseIds);
        }

        // ── 12. Mastery penalty outweighs variation penalty (a deeper deprioritisation) ─
        [Fact]
        public void RecommendNext_MasteredOutranksMerelyRecent()
        {
            // 36 = Mastered (penalty 4), 37 = recently done (penalty 2). The fresh,
            // non-mastered 38 (penalty 0) should win; 37 should still outrank 36.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = Scores(resonance: 25),
                ComplexityLevel = SpeechComplexityLevel.StructuredSentences,
                RecentExerciseIds = new[] { 37 },
                MasteryByExercise = new Dictionary<int, MasteryLevel> { [36] = MasteryLevel.Mastered }
            };

            var result = Engine().RecommendNext(input);

            Assert.Equal(38, result.ExerciseId);
            var orderOf37 = result.AlternativeExerciseIds.ToList().IndexOf(37);
            var orderOf36 = result.AlternativeExerciseIds.ToList().IndexOf(36);
            Assert.True(orderOf37 >= 0 && orderOf36 >= 0 && orderOf37 < orderOf36);
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
