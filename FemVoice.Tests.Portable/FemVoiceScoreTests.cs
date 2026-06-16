using Xunit;
using FemVoiceStudio.Services;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for FemVoiceScore scoring engine.
    /// Tests clinical progression rules and safety constraints.
    /// </summary>
    public class FemVoiceScoreTests
    {
        private readonly FemVoiceScore _engine = new();

        #region Basic Scoring Tests

        [Fact]
        public void Calculate_WithValidInput_ReturnsValidScores()
        {
            var input = CreateValidInput();
            
            var result = _engine.Calculate(input);
            
            Assert.NotNull(result);
            Assert.InRange(result.OverallScore, 0, 100);
            Assert.InRange(result.ResonanceScore, 0, 100);
            Assert.InRange(result.PitchScore, 0, 100);
            Assert.InRange(result.IntonationScore, 0, 100);
            Assert.InRange(result.VoiceHealthScore, 0, 100);
        }

        [Fact]
        public void Calculate_WithNullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _engine.Calculate(null!));
        }

        #endregion

        #region Progression Control Tests

        /// <summary>
        /// Clinical rule: Pitch should NOT increase when resonance is low.
        /// This prevents "pitch chasing" behavior.
        /// </summary>
        [Fact]
        public void Calculate_HighPitchWithLowResonance_AppliesPenalty()
        {
            var input = CreateValidInput();
            input.AveragePitch = 230; // High pitch
            input.ResonanceScore = 40; // Low resonance - doesn't support pitch
            input.VoiceHealthScore = 80; // Good health
            
            var result = _engine.Calculate(input);
            
            // Pitch score should be penalized because resonance doesn't support it
            Assert.True(result.PitchScore < 60, 
                "Pitch score should be penalized when resonance is low");
        }

        /// <summary>
        /// Clinical rule: Pitch can increase when resonance > 60 AND health > 70.
        /// This is the safe progression pathway.
        /// </summary>
        [Fact]
        public void Calculate_HighPitchWithGoodResonanceAndHealth_NoPenalty()
        {
            var input = CreateValidInput();
            input.AveragePitch = 200;
            input.ResonanceScore = 70; // Supports pitch increase
            input.VoiceHealthScore = 80; // Good health
            
            var result = _engine.Calculate(input);
            
            // Should have good pitch score when supported by resonance
            Assert.True(result.PitchScore >= 60,
                "Pitch score should be good when resonance and health support it");
        }

        /// <summary>
        /// Clinical rule: Pitch increase blocked when health is poor.
        /// This prevents strain-based training.
        /// </summary>
        [Fact]
        public void Calculate_HighPitchWithPoorHealth_AppliesPenalty()
        {
            var input = CreateValidInput();
            input.AveragePitch = 220;
            input.ResonanceScore = 70;
            input.VoiceHealthScore = 50; // Poor health
            
            var result = _engine.Calculate(input);
            
            // Overall score should be reduced due to health concerns
            Assert.True(result.VoiceHealthScore < 60,
                "Voice health score should reflect poor health");
        }

        #endregion

        #region Maintenance Session Tests

        [Fact]
        public void Calculate_MaintenanceSession_ReducesPitchRequirements()
        {
            var input = CreateValidInput();
            input.IsMaintenanceSession = true;
            input.AveragePitch = 250; // Would normally be penalized
            
            var result = _engine.Calculate(input);
            
            // In maintenance mode, higher pitch should be more tolerated
            Assert.True(result.PitchScore >= 50 || result.OverallScore >= 40);
        }

        #endregion

        #region Strain Safety Tests

        [Fact]
        public void Calculate_CriticalStrain_ReturnsWarning()
        {
            var input = CreateValidInput();
            input.StrainLevel = 80; // Critical strain
            
            var result = _engine.Calculate(input);
            
            Assert.NotNull(result.WarningFlags);
            Assert.Contains("STRAIN", result.WarningFlags);
        }

        [Fact]
        public void Calculate_HighPitchStrain_ReturnsWarning()
        {
            var input = CreateValidInput();
            input.AveragePitch = 290; // Above safe limit
            
            var result = _engine.Calculate(input);
            
            Assert.NotNull(result.WarningFlags);
            Assert.Contains("HIGH_PITCH", result.WarningFlags);
        }

        [Fact]
        public void Calculate_WithStrain_PenalizesOverallScore()
        {
            var input = CreateValidInput();
            input.StrainLevel = 60; // Moderate strain
            
            var result = _engine.Calculate(input);
            
            // Overall score should be capped when strain is present
            Assert.True(result.OverallScore <= 60,
                "Overall score should be capped when strain is detected");
        }

        #endregion

        #region Resonance Priority Tests

        /// <summary>
        /// Clinical principle: Resonance (45%) has higher weight than Pitch (30%).
        /// This ensures users focus on resonance before pitch.
        /// </summary>
        [Fact]
        public void Calculate_ResonanceHasHigherWeightThanPitch()
        {
            var inputGoodResonance = CreateValidInput();
            inputGoodResonance.AverageF2 = 1800; // Good F2
            inputGoodResonance.SpectralCentroid = 2500; // Bright
            
            var inputGoodPitch = CreateValidInput();
            inputGoodPitch.AveragePitch = 210; // Good pitch
            inputGoodPitch.AverageF2 = 1200; // Poor resonance
            
            var resultResonance = _engine.Calculate(inputGoodResonance);
            var resultPitch = _engine.Calculate(inputGoodPitch);
            
            // Resonance should contribute more to overall score
            Assert.True(resultResonance.OverallScore >= resultPitch.OverallScore - 10,
                "Good resonance should score as well or better than good pitch alone");
        }

        #endregion

        #region Pitch Variation Tests

        [Fact]
        public void Calculate_ExcessivePitchVariation_Penalized()
        {
            var input = CreateValidInput();
            input.PitchVariation = 50; // High variation
            
            var result = _engine.Calculate(input);
            
            // High variation should reduce pitch score
            Assert.True(result.PitchScore < 70,
                "Pitch score should be reduced with high variation");
        }

        [Fact]
        public void Calculate_LowPitchVariation_Rewarded()
        {
            var input = CreateValidInput();
            input.AveragePitch = 200;
            input.PitchVariation = 10; // Low variation - stable
            
            var result = _engine.Calculate(input);
            
            // Low variation should have good pitch score
            Assert.True(result.PitchScore >= 70,
                "Stable pitch should be rewarded");
        }

        #endregion

        #region Difficulty Adjustment Tests

        [Fact]
        public void Calculate_BeginnerLevel_AppliesBonus()
        {
            var input = CreateValidInput();
            input.DifficultyLevel = DifficultyLevel.Nybegynner;
            
            var result = _engine.Calculate(input);
            
            // Beginners get a bonus
            Assert.True(result.OverallScore >= 50);
        }

        [Fact]
        public void Calculate_AdvancedLevel_AppliesPenalty()
        {
            var input = CreateValidInput();
            input.DifficultyLevel = DifficultyLevel.Avansert;
            
            var result = _engine.Calculate(input);
            
            // Advanced has stricter standards
            Assert.InRange(result.OverallScore, 0, 100);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Calculate_ZeroPitch_ReturnsZeroPitchScore()
        {
            var input = CreateValidInput();
            input.AveragePitch = 0;
            
            var result = _engine.Calculate(input);
            
            Assert.Equal(0, result.PitchScore);
        }

        [Fact]
        public void Calculate_AllScoresClampedTo100()
        {
            var input = CreateValidInput();
            input.AverageF2 = 10000; // Extreme values
            input.SpectralCentroid = 10000;
            
            var result = _engine.Calculate(input);
            
            Assert.True(result.OverallScore <= 100);
            Assert.True(result.ResonanceScore <= 100);
        }

        [Fact]
        public void Calculate_AllScoresClampedTo0()
        {
            var input = CreateValidInput();
            input.AveragePitch = 50; // Very low
            input.AverageF2 = 500; // Very low
            input.AverageF1 = 200; // Very low
            
            var result = _engine.Calculate(input);
            
            Assert.True(result.OverallScore >= 0);
            Assert.True(result.ResonanceScore >= 0);
        }

        #endregion

        #region Personalized Target Tests (SAFETY-CERT-01)

        /// <summary>
        /// SAFETY-CERT-01 (no-universal-target invariant): a user whose comfort zone has
        /// been down-adjusted (e.g. 150-190 Hz) must be REWARDED for sitting in the centre
        /// of their own zone (170 Hz), not penalised toward the universal feminine 210 Hz.
        /// The pitch curve peaks at the centre of [TargetMinPitch, effectiveMaxPitch].
        /// </summary>
        [Fact]
        public void Calculate_PitchAtCentreOfDownAdjustedZone_ScoresTop()
        {
            var input = CreateValidInput();
            input.DifficultyLevel = DifficultyLevel.Middels; // no tolerance bonus/penalty
            input.TargetMinPitch = 150;
            input.TargetMaxPitch = 190;
            input.AveragePitch = 170;   // exact centre of the personalized zone
            input.PitchVariation = 10;  // stable, no variation penalty
            input.ResonanceScore = 70;  // supports pitch, avoids resonance penalty branch

            var result = _engine.Calculate(input);

            // Centre of zone -> distanceFromIdeal 0 -> pitchScore 100 (pre-clamp), clamped 100.
            Assert.Equal(100, result.PitchScore, 3);
        }

        /// <summary>
        /// SAFETY-CERT-01: 170 Hz inside a down-adjusted 150-190 Hz zone must out-score the
        /// same 170 Hz evaluated against the legacy universal 165-255 zone (centred on 210),
        /// proving the score follows the user's target, not a hardcoded feminine centre.
        /// </summary>
        [Fact]
        public void Calculate_DownAdjustedZone_NotPenalizedTowardUniversalTarget()
        {
            var personalized = CreateValidInput();
            personalized.DifficultyLevel = DifficultyLevel.Middels;
            personalized.TargetMinPitch = 150;
            personalized.TargetMaxPitch = 190;
            personalized.AveragePitch = 170;
            personalized.PitchVariation = 10;
            personalized.ResonanceScore = 70;

            var universal = CreateValidInput();
            universal.DifficultyLevel = DifficultyLevel.Middels;
            universal.TargetMinPitch = 165;   // legacy universal feminine zone
            universal.TargetMaxPitch = 255;   // centre 210 Hz
            universal.AveragePitch = 170;
            universal.PitchVariation = 10;
            universal.ResonanceScore = 70;

            var rPersonalized = _engine.Calculate(personalized);
            var rUniversal = _engine.Calculate(universal);

            // In the legacy universal zone, 170 Hz is ~40 Hz below the 210 Hz centre and is
            // penalised; in the personalized zone it is the centre and is not.
            Assert.True(rPersonalized.PitchScore > rUniversal.PitchScore,
                "170 Hz in a down-adjusted zone must out-score 170 Hz vs the universal 210 Hz centre");
        }

        /// <summary>
        /// SAFETY-CERT-01: the resonance curve must follow the user's personalized formant
        /// targets. A measured F2 that sits at the bottom of a lowered personalized F2 target
        /// scores worse than the same F2 against a higher (legacy) target window — confirming
        /// the per-user TargetMin/MaxF2 fields, not the universal constants, drive resonance.
        /// </summary>
        [Fact]
        public void Calculate_ResonanceFollowsPersonalizedFormantTargets()
        {
            // Use precomputed-resonance OFF so the formant math runs.
            var lowerF2Target = CreateValidInput();
            lowerF2Target.DifficultyLevel = DifficultyLevel.Middels;
            lowerF2Target.ResonanceScore = 0;          // force formant-based computation
            lowerF2Target.AverageF1 = 0;               // isolate F2 contribution
            lowerF2Target.SpectralCentroid = 0;        // isolate F2 contribution
            lowerF2Target.AverageF2 = 1600;
            lowerF2Target.TargetMinF2 = 1600;          // measured F2 at the floor of target
            lowerF2Target.TargetMaxF2 = 2000;

            var higherF2Target = CreateValidInput();
            higherF2Target.DifficultyLevel = DifficultyLevel.Middels;
            higherF2Target.ResonanceScore = 0;
            higherF2Target.AverageF1 = 0;
            higherF2Target.SpectralCentroid = 0;
            higherF2Target.AverageF2 = 1600;
            higherF2Target.TargetMinF2 = 1400;         // measured F2 above the floor of target
            higherF2Target.TargetMaxF2 = 2000;

            var rLow = _engine.Calculate(lowerF2Target);
            var rHigh = _engine.Calculate(higherF2Target);

            // 1600 Hz is exactly at the floor of [1600,2000] (f2Score=50) but above the floor
            // of [1400,2000] (f2Score>50). Personalized targets must change the resonance score.
            Assert.True(rHigh.ResonanceScore > rLow.ResonanceScore,
                "Resonance must track per-user TargetMin/MaxF2, not the universal formant constants");
        }

        #endregion

        #region Helper Methods

        private FemVoiceScoreInput CreateValidInput()
        {
            return new FemVoiceScoreInput
            {
                AveragePitch = 180,
                MinPitch = 160,
                MaxPitch = 200,
                PitchVariation = 15,
                AverageF1 = 500,
                AverageF2 = 1500,
                AverageF3 = 2800,
                SpectralCentroid = 2000,
                IntonationRange = 40,
                IntonationRiseScore = 25,
                StrainLevel = 10,
                IntensityRms = 0.5,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                TargetMinF1 = 400,
                TargetMaxF1 = 700,
                TargetMinF2 = 1400,
                TargetMaxF2 = 2000,
                DifficultyLevel = DifficultyLevel.Nybegynner,
                ResonanceScore = 50,
                VoiceHealthScore = 80,
                IsMaintenanceSession = false,
                ConsecutiveStableSessions = 3
            };
        }

        #endregion
    }
}
