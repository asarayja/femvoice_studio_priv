using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint C.2, Agent 7 — RECOMMENDATION DATA PROVIDER tests.
    ///
    /// Verifies that the new effectiveness intelligence reaches its three consumers
    /// ADDITIVELY (null ⇒ EXACTLY today's behaviour), and that it can NEVER override the
    /// clinical ordering (Safety &gt; Health &gt; Recovery &gt; Comfort &gt; Goals &gt;
    /// Progression &gt; Coaching). Effectiveness is a WEAK tie-break / data-driven
    /// de-prioritisation only.
    ///
    ///   1) ExerciseRecommendationEngine: effectiveness as a weak tie-break within an equal
    ///      penalty bucket; HasEnoughData=false ⇒ neutral (no penalty); null ⇒ byte-identical
    ///      ranking; a safety-flagged id is de-prioritised; the recovery gate still wins.
    ///   2) LearningPathProfileBuilder.Build: RecommendedExercises ordered most-effective-first
    ///      when data exists; null ⇒ today's provisional band order unchanged.
    ///   3) SmartCoachEngine: a null effectiveness engine ⇒ unchanged behaviour; a real
    ///      engine never breaks the health gate.
    ///
    /// No mocking: real engines + real in-memory fakes (InMemorySessionAnalyticsRepository,
    /// TestDatabaseService) and hand-built records.
    /// </summary>
    public class ExerciseEffectivenessProviderTests
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        private static ExerciseRecommendationEngine RecEngine() => new();

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

        private static ExerciseEffectivenessProfile Profile(
            int exerciseId, double composite, bool hasEnoughData = true, double recoveryCost = 10)
            => new()
            {
                ExerciseId = exerciseId,
                CompositeEffectiveness = composite,
                HasEnoughData = hasEnoughData,
                SessionCount = hasEnoughData ? 6 : 1,
                RecoveryCost = recoveryCost,
                Explanation = "test"
            };

        // A foundation-pool input (no recovery gate, no measured history) so ranking is
        // driven purely by penalty + the effectiveness tie-break over ids {1,2,3}.
        private static ExerciseRecommendationInput FoundationInput(
            IReadOnlyDictionary<int, ExerciseEffectivenessProfile>? effectiveness = null,
            IReadOnlyCollection<int>? flagged = null)
            => new()
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = null, // ⇒ Foundation pool {1,2,3}
                ComplexityLevel = SpeechComplexityLevel.IsolatedSounds,
                EffectivenessByExercise = effectiveness,
                FlaggedExerciseIds = flagged
            };

        // ─────────────────────────────────────────────────────────────────────────
        // 1. RECOMMENDATION ENGINE
        // ─────────────────────────────────────────────────────────────────────────

        // ── 1a. More effective candidate ranks higher when penalty is equal ──────────
        [Fact]
        public void Recommend_MoreEffectiveCandidate_RanksHigherOnEqualFooting()
        {
            // Pool {1,2,3}, no recent/mastery ⇒ equal penalty. Without effectiveness the
            // primary would be id 1 (ascending id). Make id 3 clearly most effective.
            var eff = new Dictionary<int, ExerciseEffectivenessProfile>
            {
                [1] = Profile(1, composite: 40),
                [2] = Profile(2, composite: 55),
                [3] = Profile(3, composite: 90),
            };

            var result = RecEngine().RecommendNext(FoundationInput(eff));

            Assert.Equal(3, result.ExerciseId);
            // Full ranking is 3 (90) > 2 (55) > 1 (40).
            Assert.Equal(new[] { 2, 1 }, result.AlternativeExerciseIds.ToArray());
        }

        // ── 1b. HasEnoughData=false ⇒ neutral: no lift, no penalty ───────────────────
        [Fact]
        public void Recommend_LowDataProfile_IsNeutral_NotPenalised()
        {
            // id 1 has a LOW composite but NOT enough data ⇒ must be treated as neutral
            // (midpoint 50), never as "ineffective". id 2/3 have no profile ⇒ also neutral.
            // With every candidate neutral, the result falls back to ascending id ⇒ id 1.
            var eff = new Dictionary<int, ExerciseEffectivenessProfile>
            {
                [1] = Profile(1, composite: 5, hasEnoughData: false),
            };

            var result = RecEngine().RecommendNext(FoundationInput(eff));

            // Low-data id 1 is NOT pushed down — it keeps the natural id-1 lead.
            Assert.Equal(1, result.ExerciseId);
        }

        // ── 1c. Low-data id is NOT out-ranked below an absent (also-neutral) id, but a
        //        genuinely more effective evidenced id still leads it ──────────────────
        [Fact]
        public void Recommend_LowDataNeutral_DoesNotBeatEvidencedEffective()
        {
            var eff = new Dictionary<int, ExerciseEffectivenessProfile>
            {
                [1] = Profile(1, composite: 99, hasEnoughData: false), // neutral despite "99"
                [2] = Profile(2, composite: 80, hasEnoughData: true),  // evidenced, above neutral
            };

            var result = RecEngine().RecommendNext(FoundationInput(eff));

            // id 2 (80, evidenced) beats id 1 (neutral 50) and id 3 (neutral 50).
            Assert.Equal(2, result.ExerciseId);
        }

        // ── 1d. null effectiveness ⇒ ranking is byte-identical to today ───────────────
        [Fact]
        public void Recommend_NullEffectiveness_IsByteIdenticalToBaseline()
        {
            var withNull = RecEngine().RecommendNext(FoundationInput(effectiveness: null, flagged: null));
            var baseline = RecEngine().RecommendNext(new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = null,
                ComplexityLevel = SpeechComplexityLevel.IsolatedSounds
            });

            Assert.Equal(baseline.ExerciseId, withNull.ExerciseId);
            Assert.Equal(baseline.AlternativeExerciseIds.ToArray(), withNull.AlternativeExerciseIds.ToArray());
            Assert.Equal(1, withNull.ExerciseId); // ascending-id default
        }

        // ── 1e. A safety-flagged id is de-prioritised when a safer alternative exists ──
        [Fact]
        public void Recommend_FlaggedExercise_IsDeprioritised()
        {
            // id 1 would normally lead (ascending id), but it is safety-flagged ⇒ +penalty,
            // so unflagged ids 2 and 3 (lower penalty) come first.
            var result = RecEngine().RecommendNext(
                FoundationInput(effectiveness: null, flagged: new[] { 1 }));

            Assert.NotEqual(1, result.ExerciseId);
            Assert.Contains(2, new[] { result.ExerciseId });
            // Flagged id 1 sinks to the tail.
            Assert.Equal(1, result.AlternativeExerciseIds.Last());
        }

        // ── 1f. Flag penalty is WEAKER than variation/mastery (mild, not a block) ─────
        [Fact]
        public void Recommend_FlagPenalty_IsMilderThanVariation()
        {
            // id 2 is flagged (+1); id 1 is recent (+2). Variation outweighs the flag, so
            // the flagged-but-not-recent id 2 still ranks ahead of the recent id 1.
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = null,
                ComplexityLevel = SpeechComplexityLevel.IsolatedSounds,
                RecentExerciseIds = new[] { 1 },
                FlaggedExerciseIds = new[] { 2 }
            };

            var result = RecEngine().RecommendNext(input);

            // Penalties: id1=+2 (recent), id2=+1 (flag), id3=0 ⇒ order 3, 2, 1.
            Assert.Equal(3, result.ExerciseId);
            Assert.Equal(new[] { 2, 1 }, result.AlternativeExerciseIds.ToArray());
        }

        // ── 1g. RECOVERY GATE still wins over effectiveness (Health > Coaching) ───────
        [Fact]
        public void Recommend_LowRecovery_StaysLight_EvenIfEffectivenessFavoursHardExercise()
        {
            // A hard, Conversational-level exercise (id 15) is marked maximally effective,
            // but recovery is Overtrained ⇒ the recovery gate forces a LIGHT pick and the
            // effectiveness data can never pull the depleted voice toward id 15.
            var eff = new Dictionary<int, ExerciseEffectivenessProfile>
            {
                [15] = Profile(15, composite: 100),
                [1] = Profile(1, composite: 10),
            };
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(10, RecoveryStatus.Overtrained),
                LatestVoiceScores = Scores(resonance: 5),
                ComplexityLevel = SpeechComplexityLevel.Conversational,
                EffectivenessByExercise = eff
            };

            var result = RecEngine().RecommendNext(input);

            Assert.True(result.IsRecoveryOriented);
            Assert.Equal(RecommendationFocus.Recovery, result.Focus);
            // Lightest pool (isolated sounds 1..3), NEVER the "most effective" hard id 15.
            Assert.Contains(result.ExerciseId, new[] { 1, 2, 3 });
            Assert.DoesNotContain(15, result.AlternativeExerciseIds.Append(result.ExerciseId));
        }

        // ── 1h. Effectiveness never beats MASTERY deprioritisation (Progression) ──────
        [Fact]
        public void Recommend_EffectivenessNeverOverridesMastery()
        {
            // id 1 is the MOST effective but already Mastered (+4). Mastery deprioritisation
            // is stronger than the effectiveness tie-break, so an un-mastered id leads.
            var eff = new Dictionary<int, ExerciseEffectivenessProfile>
            {
                [1] = Profile(1, composite: 100),
                [2] = Profile(2, composite: 30),
                [3] = Profile(3, composite: 30),
            };
            var input = new ExerciseRecommendationInput
            {
                Recovery = Recovery(90, RecoveryStatus.WellRecovered),
                LatestVoiceScores = null,
                ComplexityLevel = SpeechComplexityLevel.IsolatedSounds,
                MasteryByExercise = new Dictionary<int, MasteryLevel> { [1] = MasteryLevel.Mastered },
                EffectivenessByExercise = eff
            };

            var result = RecEngine().RecommendNext(input);

            // id 1 is Mastered (+4) so it sinks to the tail despite composite 100.
            Assert.NotEqual(1, result.ExerciseId);
            Assert.Equal(1, result.AlternativeExerciseIds.Last());
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 2. LEARNING PATH
        // ─────────────────────────────────────────────────────────────────────────

        private static readonly LearningPathProfileBuilder PathBuilder = new();
        private static readonly RecoveryScorer Scorer = new();

        private static ComplexityEvaluation Complexity(SpeechComplexityLevel level)
            => new() { CurrentLevel = level };

        private static VoiceIntelligenceTrendPoint PathPoint(
            double resonance = 50, double comfort = 50, double consistency = 50,
            double intonation = 50, double vocalWeight = 50, double recovery = 50,
            double pitch = 50, double composite = 50, int dayOffset = 0)
            => new()
            {
                SessionId = 1 + dayOffset,
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

        // ── 2a. RecommendedExercises ordered most-effective-first when data exists ────
        [Fact]
        public void LearningPath_RecommendedExercises_OrderedByEffectiveness()
        {
            // IsolatedSounds ⇒ level ids {1,2,3}. No weak dimension (all 80) ⇒ the "continue
            // at current level" branch lists the level ids in order. With effectiveness,
            // they should be most-effective-first: 3 (90) > 2 (70) > 1 (40).
            var trend = new[] { PathPoint(resonance: 80, comfort: 80, consistency: 80,
                                          recovery: 80, vocalWeight: 80, intonation: 80, pitch: 80) };
            var recovery = Scorer.Score(new RecoveryScoreInput());
            var effectiveness = new List<ExerciseEffectivenessProfile>
            {
                Profile(1, composite: 40),
                Profile(2, composite: 70),
                Profile(3, composite: 90),
            };

            var profile = PathBuilder.Build(
                trend, recovery, Complexity(SpeechComplexityLevel.IsolatedSounds),
                mastery: null, effectiveness);

            var ids = profile.RecommendedExercises.Select(r => r.ExerciseId).ToArray();
            Assert.Equal(new[] { 3, 2, 1 }, ids);
        }

        // ── 2b. null effectiveness ⇒ today's provisional band order unchanged ─────────
        [Fact]
        public void LearningPath_NullEffectiveness_KeepsBandOrder()
        {
            var trend = new[] { PathPoint(resonance: 80, comfort: 80, consistency: 80,
                                          recovery: 80, vocalWeight: 80, intonation: 80, pitch: 80) };
            var recovery = Scorer.Score(new RecoveryScoreInput());

            var withNull = PathBuilder.Build(
                trend, recovery, Complexity(SpeechComplexityLevel.IsolatedSounds));
            var baseline = PathBuilder.Build(
                trend, recovery, Complexity(SpeechComplexityLevel.IsolatedSounds), mastery: null, null);

            var nullIds = withNull.RecommendedExercises.Select(r => r.ExerciseId).ToArray();
            var baseIds = baseline.RecommendedExercises.Select(r => r.ExerciseId).ToArray();
            Assert.Equal(baseIds, nullIds);
            // Provisional band order is ascending id over {1,2,3}.
            Assert.Equal(new[] { 1, 2, 3 }, nullIds);
        }

        // ── 2c. Low-data effectiveness profiles ⇒ neutral, band order preserved ───────
        [Fact]
        public void LearningPath_LowDataEffectiveness_FallsBackToBandOrder()
        {
            var trend = new[] { PathPoint(resonance: 80, comfort: 80, consistency: 80,
                                          recovery: 80, vocalWeight: 80, intonation: 80, pitch: 80) };
            var recovery = Scorer.Score(new RecoveryScoreInput());
            // Every profile is low-data ⇒ all neutral ⇒ ordering falls back to ascending id.
            var effectiveness = new List<ExerciseEffectivenessProfile>
            {
                Profile(1, composite: 5, hasEnoughData: false),
                Profile(2, composite: 99, hasEnoughData: false),
                Profile(3, composite: 50, hasEnoughData: false),
            };

            var profile = PathBuilder.Build(
                trend, recovery, Complexity(SpeechComplexityLevel.IsolatedSounds),
                mastery: null, effectiveness);

            var ids = profile.RecommendedExercises.Select(r => r.ExerciseId).ToArray();
            Assert.Equal(new[] { 1, 2, 3 }, ids);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 3. SMARTCOACH
        // ─────────────────────────────────────────────────────────────────────────

        private static SessionAnalyticsStore EmptyStore()
            => new(new InMemorySessionAnalyticsRepository());

        private static SmartCoachEngine SmartCoach(
            TestDatabaseService db,
            SessionAnalyticsStore? store,
            ExerciseEffectivenessEngine? effectiveness)
            => new(
                db,
                localization: null,
                feedbackPipeline: null,
                feedbackMapper: null,
                voiceGoalProfiles: null,
                voiceIntelligence: store,
                recoveryIntelligence: null,
                learningPathBuilder: new LearningPathProfileBuilder(),
                complexityEngine: null,
                masteryLevelProvider: null,
                effectivenessEngine: effectiveness);

        // ── 3a. Null effectiveness engine ⇒ unchanged daily recommendation ───────────
        [Fact]
        public void SmartCoach_NullEffectivenessEngine_UnchangedBehaviour()
        {
            var db = new TestDatabaseService();
            db.SetSmartCoachBaseline(new FemVoiceStudio.Data.SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 175,
                BaselineResonanceScore = 80,
                BaselineIntonation = 80,
                ConfidenceLevel = "high"
            });
            var store = EmptyStore();

            var withNull = SmartCoach(db, store, effectiveness: null).GenerateDailyRecommendation(1);

            Assert.NotNull(withNull);
            Assert.False(withNull.HealthWarning);
            Assert.False(string.IsNullOrWhiteSpace(withNull.FocusArea));
        }

        // ── 3b. Real effectiveness engine never breaks the HEALTH gate ───────────────
        [Fact]
        public void SmartCoach_WithEffectivenessEngine_HealthGateStillWins()
        {
            var db = new TestDatabaseService();
            db.SetSmartCoachBaseline(new FemVoiceStudio.Data.SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 175,
                BaselineResonanceScore = 80,
                ConfidenceLevel = "high"
            });
            // Active strain ⇒ health gate must force a recovery focus regardless of any
            // effectiveness data the provider could surface (Safety > Coaching).
            db.AddHealthMonitoring(new FemVoiceStudio.Data.SmartCoachHealthMonitoring
            {
                UserId = 1,
                Date = DateTime.Today,
                StrainDetected = true,
                StrainType = "fatigue",
                StrainLevel = 50,
                Recommendation = "rest"
            });
            var store = EmptyStore();
            var effEngine = new ExerciseEffectivenessEngine(store);

            var rec = SmartCoach(db, store, effEngine).GenerateDailyRecommendation(1);

            Assert.True(rec.HealthWarning);
            Assert.Equal("recovery", rec.FocusArea);
            // The recovery branch caps duration short — effectiveness never lengthens it.
            Assert.True(rec.RecommendedDurationMinutes <= 5);
        }

        // ── 3c. Real effectiveness engine with empty history ⇒ no throw, sane output ──
        [Fact]
        public void SmartCoach_WithEffectivenessEngine_EmptyHistory_Degrades_Gracefully()
        {
            var db = new TestDatabaseService();
            db.SetSmartCoachBaseline(new FemVoiceStudio.Data.SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 175,
                BaselineResonanceScore = 80,
                BaselineIntonation = 80,
                ConfidenceLevel = "high"
            });
            var store = EmptyStore();
            var effEngine = new ExerciseEffectivenessEngine(store);

            var rec = SmartCoach(db, store, effEngine).GenerateDailyRecommendation(1);

            // No persisted exercises ⇒ EvaluateAllAsync returns all-neutral profiles; the
            // recommendation is still well-formed and not a health warning.
            Assert.NotNull(rec);
            Assert.False(rec.HealthWarning);
            Assert.False(string.IsNullOrWhiteSpace(rec.FocusArea));
        }
    }
}
