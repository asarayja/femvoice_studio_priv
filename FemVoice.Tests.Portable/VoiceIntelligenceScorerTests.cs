using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="VoiceIntelligenceScorer"/> + <see cref="VoiceIntelligenceScores"/> —
    /// the central voice-intelligence aggregate (seven explainable 0–100 dimensions +
    /// one hierarchy-weighted composite).
    ///
    /// No mocking: the scorer composes real engines (FemVoiceScore, VocalWeightAnalyzer,
    /// RecoveryScorer) over a plain input struct, so tests exercise the real classes
    /// with hand-checked expectations.
    ///
    /// Composite rules under test (a–d):
    ///   (a) Pitch has the LOWEST weight.
    ///   (b) Resonance is the highest SINGLE training weight.
    ///   (c) Comfort + Recovery (= Health) jointly exceed Resonance.
    ///   (d) All seven dimensions represented; weights sum to 1.0.
    /// </summary>
    public class VoiceIntelligenceScorerTests
    {
        private static VoiceIntelligenceScorer NewScorer() => new VoiceIntelligenceScorer();

        // A fully-populated, all-high input.
        private static VoiceIntelligenceInput HighInput() => VoiceIntelligenceInput.Empty() with
        {
            AverageResonance01 = 0.90,
            ComfortCompliance01 = 0.95,
            ComfortBreaches = 0,
            AverageStability01 = 0.92,
            IntonationRangeHz = 75,           // inside ideal band
            AverageF1Hz = 700,
            AverageSpectralCentroidHz = 2400,  // light/bright
            AverageHnrDb = 22,
            AverageIntensity = 0.15,
            AveragePitchHz = 200,             // inside target band
            PitchVariation = 5,
            Recovery = new RecoveryScoreInput(), // empty ⇒ well rested (100)
        };

        // A fully-populated, all-low input.
        private static VoiceIntelligenceInput LowInput() => VoiceIntelligenceInput.Empty() with
        {
            AverageResonance01 = 0.05,
            ComfortCompliance01 = 0.10,
            ComfortBreaches = 6,
            AverageStability01 = 0.08,
            IntonationRangeHz = 4,            // far below ideal min
            AverageF1Hz = 360,
            AverageSpectralCentroidHz = 1050, // heavy/dark
            AverageHnrDb = 6,
            AverageIntensity = 0.78,
            AveragePitchHz = 110,             // well below target
            PitchVariation = 40,
            Recovery = new RecoveryScoreInput
            {
                RecentSafetyLocks = 3,
                RecentStrainEpisodes = 4,
                RecentFatigueIndicators = 8,
            },
        };

        // ──────────────────────────────────────────────────────────────────────────
        // 1–4. Per-dimension 0..1 → 0..100 mappings.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Resonance_Maps01To0100()
        {
            var input = VoiceIntelligenceInput.Empty() with { AverageResonance01 = 0.73 };
            var r = NewScorer().Compute(input);
            Assert.Equal(73.0, r.Resonance.Score, 6);
        }

        [Fact]
        public void Comfort_Maps01To0100_NoBreaches()
        {
            var input = VoiceIntelligenceInput.Empty() with
            {
                ComfortCompliance01 = 0.80,
                ComfortBreaches = 0,
            };
            var r = NewScorer().Compute(input);
            Assert.Equal(80.0, r.Comfort.Score, 6);
        }

        [Fact]
        public void Comfort_BreachesLowerScore()
        {
            // 0.90×100 = 90 base; 4 breaches × 6 = 24 penalty ⇒ 66.
            var input = VoiceIntelligenceInput.Empty() with
            {
                ComfortCompliance01 = 0.90,
                ComfortBreaches = 4,
            };
            var r = NewScorer().Compute(input);
            Assert.Equal(66.0, r.Comfort.Score, 6);
            Assert.Contains("breach", r.Comfort.Explanation);
        }

        [Fact]
        public void Consistency_Maps01To0100()
        {
            var input = VoiceIntelligenceInput.Empty() with { AverageStability01 = 0.61 };
            var r = NewScorer().Compute(input);
            Assert.Equal(61.0, r.Consistency.Score, 6);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 5–7. Reused engines: Intonation, VocalWeight, Recovery.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Intonation_InIdealBand_ScoresHigh()
        {
            // FemVoiceScore: range 75 inside [30,120], near center 75 ⇒ ~100.
            var input = VoiceIntelligenceInput.Empty() with { IntonationRangeHz = 75 };
            var r = NewScorer().Compute(input);
            Assert.True(r.Intonation.Score >= 90,
                $"expected high intonation, got {r.Intonation.Score}");
        }

        [Fact]
        public void VocalWeight_LightBright_ScoresHigherThanHeavyDark()
        {
            var light = VoiceIntelligenceInput.Empty() with
            {
                AverageF1Hz = 700,
                AverageSpectralCentroidHz = 2400,
                AverageHnrDb = 22,
                AverageIntensity = 0.15,
            };
            var heavy = VoiceIntelligenceInput.Empty() with
            {
                AverageF1Hz = 360,
                AverageSpectralCentroidHz = 1050,
                AverageHnrDb = 6,
                AverageIntensity = 0.78,
            };
            var scorer = NewScorer();
            Assert.True(scorer.Compute(light).VocalWeight.Score
                      > scorer.Compute(heavy).VocalWeight.Score);
        }

        [Fact]
        public void Recovery_SafetyLocks_LowerScoreThanRested()
        {
            var rested = VoiceIntelligenceInput.Empty() with { Recovery = new RecoveryScoreInput() };
            var loaded = VoiceIntelligenceInput.Empty() with
            {
                Recovery = new RecoveryScoreInput { RecentSafetyLocks = 3 },
            };
            var scorer = NewScorer();
            Assert.Equal(100.0, scorer.Compute(rested).Recovery.Score, 6);
            Assert.True(scorer.Compute(loaded).Recovery.Score < 60);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 8–11. Composite weighting contract (rules a–d).
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void CompositeWeights_SumToOne()
        {
            double sum =
                VoiceIntelligenceScorer.ResonanceWeight +
                VoiceIntelligenceScorer.ComfortWeight +
                VoiceIntelligenceScorer.ConsistencyWeight +
                VoiceIntelligenceScorer.RecoveryWeight +
                VoiceIntelligenceScorer.IntonationWeight +
                VoiceIntelligenceScorer.VocalWeightWeight +
                VoiceIntelligenceScorer.PitchWeight;
            Assert.Equal(1.0, sum, 9);
        }

        [Fact]
        public void Pitch_HasLowestWeight()
        {
            double pitch = VoiceIntelligenceScorer.PitchWeight;
            Assert.True(pitch < VoiceIntelligenceScorer.ResonanceWeight);
            Assert.True(pitch < VoiceIntelligenceScorer.ComfortWeight);
            Assert.True(pitch < VoiceIntelligenceScorer.ConsistencyWeight);
            Assert.True(pitch < VoiceIntelligenceScorer.RecoveryWeight);
            Assert.True(pitch < VoiceIntelligenceScorer.IntonationWeight);
            Assert.True(pitch < VoiceIntelligenceScorer.VocalWeightWeight);
        }

        [Fact]
        public void Resonance_IsHighestSingleTrainingWeight()
        {
            double res = VoiceIntelligenceScorer.ResonanceWeight;
            // Higher than every other single weight.
            Assert.True(res > VoiceIntelligenceScorer.ComfortWeight);
            Assert.True(res > VoiceIntelligenceScorer.ConsistencyWeight);
            Assert.True(res > VoiceIntelligenceScorer.RecoveryWeight);
            Assert.True(res > VoiceIntelligenceScorer.IntonationWeight);
            Assert.True(res > VoiceIntelligenceScorer.VocalWeightWeight);
            Assert.True(res > VoiceIntelligenceScorer.PitchWeight);
        }

        [Fact]
        public void Health_ComfortPlusRecovery_ExceedsResonance()
        {
            double health =
                VoiceIntelligenceScorer.ComfortWeight + VoiceIntelligenceScorer.RecoveryWeight;
            Assert.True(health > VoiceIntelligenceScorer.ResonanceWeight,
                $"Health {health} must exceed Resonance {VoiceIntelligenceScorer.ResonanceWeight}");
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 12–13. All-high vs all-low end-to-end composite.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void AllHigh_ProducesHighComposite()
        {
            var r = NewScorer().Compute(HighInput());
            Assert.True(r.CompositeVoiceScore >= 80,
                $"expected high composite, got {r.CompositeVoiceScore}");
            Assert.InRange(r.CompositeVoiceScore, 0, 100);
        }

        [Fact]
        public void AllLow_ProducesLowComposite()
        {
            var r = NewScorer().Compute(LowInput());
            Assert.True(r.CompositeVoiceScore <= 35,
                $"expected low composite, got {r.CompositeVoiceScore}");
            Assert.InRange(r.CompositeVoiceScore, 0, 100);
        }

        [Fact]
        public void AllHigh_ExceedsAllLow()
        {
            var scorer = NewScorer();
            Assert.True(scorer.Compute(HighInput()).CompositeVoiceScore
                      > scorer.Compute(LowInput()).CompositeVoiceScore);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 14–16. Robustness: missing signals ⇒ neutral fallback, no throw.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void MissingVocalWeight_FallsBackToNeutral_NoThrow()
        {
            // Empty() leaves F1/centroid as missing sentinels.
            var input = VoiceIntelligenceInput.Empty() with { AverageResonance01 = 0.5 };
            var r = NewScorer().Compute(input);
            Assert.Equal(50.0, r.VocalWeight.Score, 6);
            Assert.Contains("neutral", r.VocalWeight.Explanation, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MissingRecovery_FallsBackToNeutral_NoThrow()
        {
            var input = VoiceIntelligenceInput.Empty(); // Recovery == null
            var r = NewScorer().Compute(input);
            Assert.Equal(50.0, r.Recovery.Score, 6);
            Assert.Contains("neutral", r.Recovery.Explanation, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EmptyInput_AllDimensionsNeutral_CompositeIsFifty()
        {
            // Every signal missing ⇒ all seven dimensions neutral 50 ⇒ composite 50.
            var r = NewScorer().Compute(VoiceIntelligenceInput.Empty());
            Assert.Equal(50.0, r.Resonance.Score, 6);
            Assert.Equal(50.0, r.Comfort.Score, 6);
            Assert.Equal(50.0, r.Consistency.Score, 6);
            Assert.Equal(50.0, r.Intonation.Score, 6);
            Assert.Equal(50.0, r.VocalWeight.Score, 6);
            Assert.Equal(50.0, r.Recovery.Score, 6);
            Assert.Equal(50.0, r.Pitch.Score, 6);
            Assert.Equal(50.0, r.CompositeVoiceScore, 6);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 17–18. BuildBreakdown + traceability.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void BuildBreakdown_ContainsAllSevenDimensionsAndComposite()
        {
            var r = NewScorer().Compute(HighInput());
            var text = r.BuildBreakdown();
            Assert.Contains("CompositeVoiceScore", text);
            Assert.Contains("Resonance", text);
            Assert.Contains("Comfort", text);
            Assert.Contains("Consistency", text);
            Assert.Contains("Intonation", text);
            Assert.Contains("VocalWeight", text);
            Assert.Contains("Recovery", text);
            Assert.Contains("Pitch", text);
        }

        [Fact]
        public void RawInputs_CarryTraceability()
        {
            var r = NewScorer().Compute(HighInput());
            Assert.True(r.RawInputs.ContainsKey("averageResonance01"));
            Assert.True(r.RawInputs.ContainsKey("comfortCompliance01"));
            Assert.True(r.RawInputs.ContainsKey("comfortBreaches"));
            Assert.True(r.RawInputs.ContainsKey("averageStability01"));
            Assert.True(r.RawInputs.ContainsKey("averagePitchHz"));
            Assert.Equal(0.90, r.RawInputs["averageResonance01"], 6);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 19. Clamping: out-of-range inputs stay within 0..100.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void OutOfRangeInputs_AreClamped()
        {
            var input = VoiceIntelligenceInput.Empty() with
            {
                AverageResonance01 = 1.8,   // >1 ⇒ clamps to 100
                ComfortCompliance01 = 1.5,
                AverageStability01 = 1.3,
            };
            var r = NewScorer().Compute(input);
            Assert.Equal(100.0, r.Resonance.Score, 6);
            Assert.Equal(100.0, r.Comfort.Score, 6);
            Assert.Equal(100.0, r.Consistency.Score, 6);
            Assert.InRange(r.CompositeVoiceScore, 0, 100);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // 20. Precomputed 0..100 resonance score takes precedence.
        // ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void PrecomputedResonanceScore_TakesPrecedence()
        {
            var input = VoiceIntelligenceInput.Empty() with
            {
                AverageResonance01 = 0.20,    // would map to 20
                ResonanceScore0100 = 88,      // explicit ⇒ wins
            };
            var r = NewScorer().Compute(input);
            Assert.Equal(88.0, r.Resonance.Score, 6);
        }
    }
}
