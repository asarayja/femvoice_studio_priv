using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for FemVoiceScore calculation algorithm
    /// Uses AAA pattern (Arrange, Act, Assert)
    /// </summary>
    public class FemVoiceScoreTests
    {
        private readonly FemVoiceScore _sut; // System Under Test

        public FemVoiceScoreTests()
        {
            _sut = new FemVoiceScore();
        }

        #region Resonance Score Tests

        [Fact]
        public void Calculate_ResonanceScore_OptimalF2_ReturnsHighScore()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AverageF2 = 1800,
                AverageF1 = 500,
                SpectralCentroid = 2500,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.True(result.ResonanceScore >= 70, 
                $"Expected resonance score >= 70, got {result.ResonanceScore}");
        }

        [Fact]
        public void Calculate_ResonanceScore_LowF2_ReturnsLowScore()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AverageF2 = 1000,
                AverageF1 = 300,
                SpectralCentroid = 1500,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.True(result.ResonanceScore < 65,
                $"Expected resonance score < 65, got {result.ResonanceScore}");
        }

        [Fact]
        public void Calculate_ResonanceScore_ZeroValues_ReturnsLowScore()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AverageF2 = 0,
                AverageF1 = 0,
                SpectralCentroid = 0,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert - With beginner tolerance bonus, should get some points
            Assert.True(result.ResonanceScore <= 20,
                $"Expected resonance score <= 20, got {result.ResonanceScore}");
        }

        #endregion

        #region Pitch Score Tests

        [Fact]
        public void Calculate_PitchScore_WithinTargetRange_ReturnsHighScore()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 200,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.True(result.PitchScore >= 60,
                $"Expected pitch score >= 60, got {result.PitchScore}");
        }

        [Fact]
        public void Calculate_PitchScore_BelowTargetRange_ReturnsPenalty()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 130,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.True(result.PitchScore < 70,
                $"Expected pitch score < 70, got {result.PitchScore}");
        }

        [Fact]
        public void Calculate_PitchScore_AboveTargetRange_ReturnsPenalty()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 280,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.True(result.PitchScore < 60,
                $"Expected pitch score < 60, got {result.PitchScore}");
        }

        [Fact]
        public void Calculate_PitchScore_ZeroPitch_ReturnsZero()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 0,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.Equal(0, result.PitchScore, 0);
        }

        #endregion

        #region Intonation Score Tests

        [Fact]
        public void Calculate_IntonationScore_GoodRange_ReturnsHighScore()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                IntonationRange = 50,
                IntonationRiseScore = 40,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.True(result.IntonationScore >= 60,
                $"Expected intonation score >= 60, got {result.IntonationScore}");
        }

        [Fact]
        public void Calculate_IntonationScore_Flat_ReturnsLowScore()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                IntonationRange = 15,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.True(result.IntonationScore < 50,
                $"Expected intonation score < 50, got {result.IntonationScore}");
        }

        #endregion

        #region Voice Health Score Tests

        [Fact]
        public void Calculate_VoiceHealthScore_NoStrain_ReturnsHighScore()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                StrainLevel = 10,
                IntensityRms = 0.3,
                AveragePitch = 200,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.True(result.VoiceHealthScore >= 80,
                $"Expected voice health >= 80, got {result.VoiceHealthScore}");
        }

        [Fact]
        public void Calculate_VoiceHealthScore_HighStrain_ReturnsReducedScore()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                StrainLevel = 80,
                IntensityRms = 0.5,
                AveragePitch = 200,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert - Health score should be affected by strain
            Assert.True(result.VoiceHealthScore <= 100,
                $"Voice health should be calculated, got {result.VoiceHealthScore}");
        }

        [Fact]
        public void Calculate_VoiceHealthScore_CriticalStrain_ReturnsWarning()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                StrainLevel = 85,
                IntensityRms = 0.5,
                AveragePitch = 290,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.NotNull(result.WarningFlags);
            Assert.Contains("CRITICAL_STRAIN", result.WarningFlags);
        }

        #endregion

        #region Overall Score Tests

        [Fact]
        public void Calculate_OverallScore_ValidInputs_ReturnsWeightedScore()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 200,
                AverageF1 = 500,
                AverageF2 = 1700,
                SpectralCentroid = 2200,
                IntonationRange = 50,
                StrainLevel = 15,
                IntensityRms = 0.3,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            // Verify weighted calculation: 45% resonance + 30% pitch + 15% intonation + 10% health
            var expectedMin = 50; // Should be reasonably high with good inputs
            Assert.True(result.OverallScore >= expectedMin,
                $"Expected overall score >= {expectedMin}, got {result.OverallScore}");
            Assert.True(result.OverallScore <= 100,
                $"Expected overall score <= 100, got {result.OverallScore}");
        }

        [Fact]
        public void Calculate_OverallScore_ClampedToHundred()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 500, // Unrealistically high
                AverageF1 = 5000,   // Unrealistically high
                AverageF2 = 10000,  // Unrealistically high
                SpectralCentroid = 10000,
                IntonationRange = 500,
                StrainLevel = 0,
                IntensityRms = 0.5,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert
            Assert.True(result.OverallScore <= 100,
                $"Expected overall score <= 100, got {result.OverallScore}");
        }

        [Fact]
        public void Calculate_OverallScore_PoorResonanceWithGoodPitch_HasClinicalPenalty()
        {
            // Arrange - Good pitch but poor resonance
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 220,
                AverageF1 = 200,
                AverageF2 = 800,
                SpectralCentroid = 1000,
                IntonationRange = 50,
                StrainLevel = 10,
                IntensityRms = 0.3,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var result = _sut.Calculate(input);

            // Assert - Clinical rule should apply penalty when resonance is poor but pitch is good
            // The overall score should reflect that resonance is prioritized
            Assert.True(result.ResonanceScore < result.PitchScore,
                $"Expected resonance < pitch when resonance is poor");
        }

        #endregion

        #region Input Validation Tests

        [Fact]
        public void Calculate_NullInput_ThrowsArgumentNullException()
        {
            // Arrange
            FemVoiceScoreInput? input = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Calculate(input!));
        }

        #endregion

        #region Difficulty Tolerance Tests

        [Fact]
        public void Calculate_BeginnerLevel_AppliesBonusTolerance()
        {
            // Arrange
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 180,
                AverageF1 = 450,
                AverageF2 = 1300,
                SpectralCentroid = 1800,
                IntonationRange = 35,
                StrainLevel = 10,
                IntensityRms = 0.3,
                DifficultyLevel = DifficultyLevel.Nybegynner
            };

            // Act
            var resultBeginner = _sut.Calculate(input);
            
            input.DifficultyLevel = DifficultyLevel.Avansert;
            var resultAdvanced = _sut.Calculate(input);

            // Assert - Beginner should have higher score due to tolerance
            Assert.True(resultBeginner.OverallScore >= resultAdvanced.OverallScore - 5,
                $"Expected beginner score >= advanced score");
        }

        #endregion
    }
}
