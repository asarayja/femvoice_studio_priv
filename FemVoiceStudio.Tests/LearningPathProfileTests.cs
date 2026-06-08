using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="LearningPathProfileBuilder"/> (Sprint C, Agent LP).
    ///
    /// No mocking: the builder is a PURE function over pre-fetched inputs, so every test
    /// exercises the real <see cref="LearningPathProfileBuilder"/>, the real
    /// <see cref="RecoveryScorer"/> and real <see cref="ComplexityEvaluation"/> /
    /// <see cref="VoiceIntelligenceTrendPoint"/> / <see cref="MasteryEvaluation"/> values
    /// with hand-computed expectations.
    ///
    /// Confidence formula under test (mirrors the production XML docs):
    ///   empty history          ⇒ 35 (Emerging);
    ///   otherwise raw = 35×0.5 + volume + trend + stability, clamped 0–100, where
    ///     volume    = min(points,12)/12 × 25,
    ///     trend     = ((clamp(slope,−1,1)+1)/2) × 25  (count≥3, else 0.5×25 neutral),
    ///     stability = (1 − clamp(stdDev/25,0,1)) × 32.5;
    ///   buckets ≥70 Established / ≥45 Moderate / else Emerging.
    /// Weakness threshold = 60; focus hierarchy = Recovery&gt;Comfort&gt;Resonance&gt;
    /// Consistency&gt;Intonation&gt;VocalWeight&gt;Pitch.
    /// </summary>
    public class LearningPathProfileTests
    {
        private static readonly LearningPathProfileBuilder Builder = new();
        private static readonly RecoveryScorer Scorer = new();

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private static ComplexityEvaluation Complexity(SpeechComplexityLevel level) =>
            new() { CurrentLevel = level };

        private static VoiceIntelligenceTrendPoint Point(
            double resonance = 50, double comfort = 50, double consistency = 50,
            double intonation = 50, double vocalWeight = 50, double recovery = 50,
            double pitch = 50, double composite = 50, int sessionId = 1, int dayOffset = 0)
            => new()
            {
                SessionId = sessionId,
                StartedAt = new DateTime(2026, 1, 1).AddDays(dayOffset),
                EndedAt = new DateTime(2026, 1, 1).AddDays(dayOffset).AddMinutes(20),
                ResonanceScore100 = resonance,
                ComfortScore100 = comfort,
                ConsistencyScore100 = consistency,
                IntonationScore100 = intonation,
                VocalWeightScore100 = vocalWeight,
                RecoveryScore100 = recovery,
                PitchScore100 = pitch,
                CompositeVoiceScore = composite
            };

        // ── 1. Empty history ⇒ Foundation, modest confidence, WellRecovered ───────────

        [Fact]
        public void Build_EmptyHistory_FoundationStageAndModerateLowConfidence()
        {
            var recovery = Scorer.Score(new RecoveryScoreInput()); // new user ⇒ WellRecovered
            var profile = Builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                recovery,
                Complexity(SpeechComplexityLevel.IsolatedSounds));

            Assert.Equal(LearningStage.Foundation, profile.CurrentStage);
            Assert.Equal(LearningConfidenceLevel.Emerging, profile.ConfidenceLevel);
            Assert.Equal(35.0, profile.ConfidenceScore, 6);
            Assert.Empty(profile.Strengths);
            Assert.Empty(profile.Weaknesses);
            Assert.Empty(profile.ActiveFocusAreas);
        }

        [Fact]
        public void Build_EmptyHistory_RecoveryIsWellRecoveredAndRestNotRecommended()
        {
            var recovery = Scorer.Score(new RecoveryScoreInput());
            var profile = Builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                recovery,
                Complexity(SpeechComplexityLevel.IsolatedSounds));

            Assert.Equal(nameof(RecoveryStatus.WellRecovered), profile.RecoveryRequirements.Status);
            Assert.False(profile.RecoveryRequirements.RestRecommended);
            Assert.Equal(100.0, profile.RecoveryRequirements.Score, 6);
        }

        // ── 2. Stage mapping across the 7-level ladder ────────────────────────────────

        [Theory]
        [InlineData(SpeechComplexityLevel.IsolatedSounds, LearningStage.Foundation)]
        [InlineData(SpeechComplexityLevel.Syllables, LearningStage.Foundation)]
        [InlineData(SpeechComplexityLevel.Words, LearningStage.Building)]
        [InlineData(SpeechComplexityLevel.Phrases, LearningStage.Building)]
        [InlineData(SpeechComplexityLevel.StructuredSentences, LearningStage.Refining)]
        [InlineData(SpeechComplexityLevel.SpontaneousSpeech, LearningStage.Integrating)]
        [InlineData(SpeechComplexityLevel.Conversational, LearningStage.Maintaining)]
        public void Build_StageMapping_FollowsDocumentedTable(
            SpeechComplexityLevel level, LearningStage expected)
        {
            var profile = Builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(level));

            Assert.Equal(expected, profile.CurrentStage);
        }

        // ── 3. Mastery nuance advances stage by one, only at top-of-band, never demotes ─

        [Fact]
        public void Build_MasteredAtTopOfBand_AdvancesStageByOne()
        {
            // Syllables = top of Foundation band; Mastered ⇒ Building.
            var profile = Builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Syllables),
                new MasteryEvaluation { Level = MasteryLevel.Mastered });

            Assert.Equal(LearningStage.Building, profile.CurrentStage);
        }

        [Fact]
        public void Build_MasteredButNotTopOfBand_DoesNotAdvanceStage()
        {
            // IsolatedSounds is NOT the top of the Foundation band ⇒ stays Foundation.
            var profile = Builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.IsolatedSounds),
                new MasteryEvaluation { Level = MasteryLevel.Mastered });

            Assert.Equal(LearningStage.Foundation, profile.CurrentStage);
        }

        [Fact]
        public void Build_DevelopingMasteryAtTopOfBand_DoesNotAdvanceStage()
        {
            // Top of band but NOT Mastered ⇒ no nudge.
            var profile = Builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Phrases),
                new MasteryEvaluation { Level = MasteryLevel.Developing });

            Assert.Equal(LearningStage.Building, profile.CurrentStage);
        }

        [Fact]
        public void Build_MasteredAtConversational_NeverExceedsMaintaining()
        {
            var profile = Builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Conversational),
                new MasteryEvaluation { Level = MasteryLevel.Mastered });

            Assert.Equal(LearningStage.Maintaining, profile.CurrentStage);
        }

        // ── 4. Strengths / weaknesses ranking ─────────────────────────────────────────

        [Fact]
        public void Build_RanksStrengthsHighestFirstAndWeaknessesLowestFirst()
        {
            // Distinct scores so the ordering is unambiguous.
            var point = Point(
                resonance: 90, comfort: 80, consistency: 70,
                intonation: 30, vocalWeight: 20, recovery: 65, pitch: 10);

            var profile = Builder.Build(
                new[] { point },
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Words));

            // Strongest three, descending: Resonance(90) > Comfort(80) > Consistency(70).
            Assert.Equal(
                new[] { VoiceDimension.Resonance, VoiceDimension.Comfort, VoiceDimension.Consistency },
                profile.Strengths.Select(s => s.Dimension).ToArray());

            // Weakest three, ascending: Pitch(10) < VocalWeight(20) < Intonation(30).
            Assert.Equal(
                new[] { VoiceDimension.Pitch, VoiceDimension.VocalWeight, VoiceDimension.Intonation },
                profile.Weaknesses.Select(w => w.Dimension).ToArray());

            // Scores are carried through faithfully.
            Assert.Equal(90.0, profile.Strengths[0].Score, 6);
            Assert.Equal(10.0, profile.Weaknesses[0].Score, 6);
        }

        // ── 5. Active focus areas follow the hierarchy and include only the genuinely weak ─

        [Fact]
        public void Build_ActiveFocus_OnlyWeakDimensions_OrderedByHierarchy()
        {
            // Weak (<60): Recovery(40), Resonance(55), Pitch(30). Strong: the rest.
            var point = Point(
                resonance: 55, comfort: 80, consistency: 75,
                intonation: 70, vocalWeight: 90, recovery: 40, pitch: 30);

            var profile = Builder.Build(
                new[] { point },
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Words));

            // Hierarchy order: Recovery > Resonance > Pitch (Comfort/etc. are not weak).
            Assert.Equal(
                new[] { VoiceDimension.Recovery, VoiceDimension.Resonance, VoiceDimension.Pitch },
                profile.ActiveFocusAreas.ToArray());
        }

        [Fact]
        public void Build_ActiveFocus_PitchExcludedWhenStrongEvenThoughLowestPriority()
        {
            // Only Comfort is weak; Pitch is high ⇒ Pitch must NOT appear despite being
            // the lowest-priority dimension.
            var point = Point(
                resonance: 80, comfort: 45, consistency: 80,
                intonation: 80, vocalWeight: 80, recovery: 80, pitch: 95);

            var profile = Builder.Build(
                new[] { point },
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Words));

            Assert.Equal(new[] { VoiceDimension.Comfort }, profile.ActiveFocusAreas.ToArray());
            Assert.DoesNotContain(VoiceDimension.Pitch, profile.ActiveFocusAreas);
        }

        [Fact]
        public void Build_NoWeakDimension_EmptyFocusAndContinueRecommendations()
        {
            // Everything ≥60 ⇒ no fabricated focus; recommendations have no target dim.
            var point = Point(
                resonance: 70, comfort: 70, consistency: 70,
                intonation: 70, vocalWeight: 70, recovery: 70, pitch: 70);

            var profile = Builder.Build(
                new[] { point },
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Words));

            Assert.Empty(profile.ActiveFocusAreas);
            Assert.NotEmpty(profile.RecommendedExercises);
            Assert.All(profile.RecommendedExercises, r => Assert.Null(r.TargetDimension));
        }

        [Fact]
        public void Build_RecommendationsTargetFocusDimensions_FromCurrentLevelBand()
        {
            var point = Point(recovery: 30, comfort: 35); // two Health weaknesses
            var profile = Builder.Build(
                new[] { point },
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Words)); // Words/Phrases ⇒ ids 16..35

            Assert.NotEmpty(profile.RecommendedExercises);
            // First focus is Recovery (highest priority), so first recommendation targets it.
            Assert.Equal(VoiceDimension.Recovery, profile.RecommendedExercises[0].TargetDimension);
            Assert.All(profile.RecommendedExercises, r => Assert.InRange(r.ExerciseId, 16, 35));
        }

        // ── 6. Confidence: longer consistent positive trend ⇒ higher confidence ───────

        [Fact]
        public void Build_LongConsistentPositiveTrend_HigherConfidenceThanShortFlat()
        {
            // Long, steadily improving composite (low volatility, positive slope).
            var rising = new List<VoiceIntelligenceTrendPoint>();
            for (var i = 0; i < 12; i++)
                rising.Add(Point(composite: 50 + i * 0.5, sessionId: i + 1, dayOffset: i));

            // Short, flat history.
            var shortFlat = new[]
            {
                Point(composite: 50, sessionId: 1, dayOffset: 0),
                Point(composite: 50, sessionId: 2, dayOffset: 1)
            };

            var rec = Scorer.Score(new RecoveryScoreInput());
            var longProfile = Builder.Build(rising, rec, Complexity(SpeechComplexityLevel.Words));
            var shortProfile = Builder.Build(shortFlat, rec, Complexity(SpeechComplexityLevel.Words));

            Assert.True(longProfile.ConfidenceScore > shortProfile.ConfidenceScore,
                $"long={longProfile.ConfidenceScore} short={shortProfile.ConfidenceScore}");
            Assert.Equal(LearningConfidenceLevel.Established, longProfile.ConfidenceLevel);
        }

        [Fact]
        public void Build_DecliningTrend_LowerConfidenceThanImprovingSameLength()
        {
            var improving = new List<VoiceIntelligenceTrendPoint>();
            var declining = new List<VoiceIntelligenceTrendPoint>();
            for (var i = 0; i < 6; i++)
            {
                improving.Add(Point(composite: 50 + i * 0.6, sessionId: i + 1, dayOffset: i));
                declining.Add(Point(composite: 50 - i * 0.6, sessionId: i + 1, dayOffset: i));
            }

            var rec = Scorer.Score(new RecoveryScoreInput());
            var up = Builder.Build(improving, rec, Complexity(SpeechComplexityLevel.Words));
            var down = Builder.Build(declining, rec, Complexity(SpeechComplexityLevel.Words));

            Assert.True(up.ConfidenceScore > down.ConfidenceScore,
                $"up={up.ConfidenceScore} down={down.ConfidenceScore}");
        }

        [Fact]
        public void Build_SinglePoint_ConfidenceMatchesHandComputedValue()
        {
            // count=1 ⇒ volume = 1/12×25 = 2.0833…; trend neutral = 0.5×25 = 12.5;
            // stability = (1−0)×32.5 = 32.5; base = 35×0.5 = 17.5 ⇒ 64.5833… ⇒ Moderate.
            var profile = Builder.Build(
                new[] { Point(composite: 55) },
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Words));

            Assert.Equal(64.5833333, profile.ConfidenceScore, 4);
            Assert.Equal(LearningConfidenceLevel.Moderate, profile.ConfidenceLevel);
        }

        [Fact]
        public void Build_VolatileHistory_LowerConfidenceThanSteadyHistory()
        {
            var steady = new List<VoiceIntelligenceTrendPoint>();
            var volatile_ = new List<VoiceIntelligenceTrendPoint>();
            for (var i = 0; i < 8; i++)
            {
                steady.Add(Point(composite: 60, sessionId: i + 1, dayOffset: i));
                volatile_.Add(Point(composite: i % 2 == 0 ? 20 : 90, sessionId: i + 1, dayOffset: i));
            }

            var rec = Scorer.Score(new RecoveryScoreInput());
            var steadyProfile = Builder.Build(steady, rec, Complexity(SpeechComplexityLevel.Words));
            var volatileProfile = Builder.Build(volatile_, rec, Complexity(SpeechComplexityLevel.Words));

            Assert.True(steadyProfile.ConfidenceScore > volatileProfile.ConfidenceScore,
                $"steady={steadyProfile.ConfidenceScore} volatile={volatileProfile.ConfidenceScore}");
        }

        // ── 7. Recovery requirement reflects the RecoveryResult ───────────────────────

        [Fact]
        public void Build_StrainedRecovery_RestRecommendedAndStatusPropagated()
        {
            // Heavy load ⇒ Strained or worse ⇒ RestRecommended = true.
            var recovery = Scorer.Score(new RecoveryScoreInput
            {
                RecentSafetyLocks = 2,
                RecentStrainEpisodes = 3,
                RecentFatigueIndicators = 4
            });

            var profile = Builder.Build(
                new[] { Point() },
                recovery,
                Complexity(SpeechComplexityLevel.Words));

            Assert.True(profile.RecoveryRequirements.RestRecommended);
            Assert.Equal(recovery.Status.ToString(), profile.RecoveryRequirements.Status);
            Assert.Equal(recovery.Score, profile.RecoveryRequirements.Score, 6);
            Assert.True(
                profile.RecoveryRequirements.Status == nameof(RecoveryStatus.Strained) ||
                profile.RecoveryRequirements.Status == nameof(RecoveryStatus.Overtrained));
        }

        [Fact]
        public void Build_AdequateRecovery_RestNotRecommended()
        {
            // A mild single fatigue indicator keeps the score in the Adequate/WellRecovered
            // band ⇒ rest not recommended.
            var recovery = Scorer.Score(new RecoveryScoreInput { RecentFatigueIndicators = 1 });
            Assert.True(recovery.Score >= 50.0); // sanity: still Adequate or better

            var profile = Builder.Build(
                new[] { Point() },
                recovery,
                Complexity(SpeechComplexityLevel.Words));

            Assert.False(profile.RecoveryRequirements.RestRecommended);
        }

        // ── 8. Robustness: NaN / out-of-range scores are clamped, never thrown ────────

        [Fact]
        public void Build_NonFiniteAndOutOfRangeScores_AreClampedNotThrown()
        {
            var point = Point(
                resonance: double.NaN, comfort: 150, consistency: -20,
                intonation: 50, vocalWeight: 50, recovery: 50, pitch: 50);

            var profile = Builder.Build(
                new[] { point },
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.Words));

            Assert.All(profile.Strengths, s => Assert.InRange(s.Score, 0.0, 100.0));
            Assert.All(profile.Weaknesses, w => Assert.InRange(w.Score, 0.0, 100.0));
            // Comfort(150)→100 is the top strength; Resonance(NaN)→0 and Consistency(−20)→0
            // are weakest.
            Assert.Equal(100.0, profile.Strengths[0].Score, 6);
            Assert.Equal(0.0, profile.Weaknesses[0].Score, 6);
        }

        [Fact]
        public void Build_NullComplexity_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Builder.Build(
                Array.Empty<VoiceIntelligenceTrendPoint>(),
                Scorer.Score(new RecoveryScoreInput()),
                complexity: null!));
        }

        [Fact]
        public void Build_NullTrend_TreatedAsEmptyHistory()
        {
            var profile = Builder.Build(
                trend: null!,
                Scorer.Score(new RecoveryScoreInput()),
                Complexity(SpeechComplexityLevel.IsolatedSounds));

            Assert.Equal(LearningStage.Foundation, profile.CurrentStage);
            Assert.Equal(35.0, profile.ConfidenceScore, 6);
            Assert.Empty(profile.ActiveFocusAreas);
        }
    }
}
