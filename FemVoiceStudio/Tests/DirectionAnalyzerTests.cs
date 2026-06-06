using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for DirectionAnalyzer
    /// Tests parameter direction logic (Increase/Decrease/Stabilize)
    /// </summary>
    public class DirectionAnalyzerTests
    {
        private readonly DirectionAnalyzer _sut;

        public DirectionAnalyzerTests()
        {
            _sut = new DirectionAnalyzer();
        }

        #region Pitch Direction Tests

        [Fact]
        public void Analyze_PitchBelowTarget_Increases()
        {
            // Arrange
            double currentPitch = 140;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Nybegynner);

            // Assert
            Assert.Equal(Direction.Increase, result.Pitch.Direction);
            Assert.True(result.Pitch.ChangeAmount <= 5, 
                "Max pitch increase should be 5 Hz per session");
        }

        [Fact]
        public void Analyze_PitchAboveTarget_Decreases()
        {
            // Arrange
            double currentPitch = 270;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Avansert);

            // Assert
            Assert.Equal(Direction.Decrease, result.Pitch.Direction);
        }

        [Fact]
        public void Analyze_PitchWithinTarget_Stabilizes()
        {
            // Arrange
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Middels);

            // Assert
            Assert.Equal(Direction.Stabilize, result.Pitch.Direction);
            Assert.Equal(LocalizationService.Instance["Direction_PitchInRange"], result.Pitch.Reason);
        }

        [Fact]
        public void Analyze_HighStrain_DecreasesPitch()
        {
            // Arrange - high strain level should trigger decrease
            double currentPitch = 200;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50;
            double strainLevel = 60; // Above threshold

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Middels);

            // Assert
            Assert.Equal(Direction.Decrease, result.Pitch.Direction);
            Assert.Contains("Press", result.Pitch.Reason);
        }

        [Fact]
        public void Analyze_VeryHighPitch_DecreasesWithSafety()
        {
            // Arrange
            double currentPitch = 290;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Avansert);

            // Assert
            Assert.Equal(Direction.Decrease, result.Pitch.Direction);
            Assert.True(result.HasSafetyConcern);
        }

        #endregion

        #region Resonance Direction Tests

        [Fact]
        public void Analyze_LowF2_IncreasesResonance()
        {
            // Arrange - F2 below 1400 Hz threshold
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 300;
            double currentF2 = 1200; // Below 1400
            double intonationRange = 50;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Nybegynner);

            // Assert
            Assert.Equal(Direction.Increase, result.Resonance.Direction);
            Assert.Contains("F2", result.Resonance.Reason);
        }

        [Fact]
        public void Analyze_HighF2_MaintainsResonance()
        {
            // Arrange - F2 above target
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1800; // Above 1400
            double intonationRange = 50;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Avansert);

            // Assert
            Assert.Equal(Direction.Maintain, result.Resonance.Direction);
        }

        [Fact]
        public void Analyze_HighPitchVariation_StabilizesResonance()
        {
            // Arrange - high pitch variation indicates unstable resonance
            double currentPitch = 180;
            double pitchVariation = 40; // Above 30 threshold
            double currentF1 = 500;
            double currentF2 = 1600;
            double intonationRange = 50;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Middels);

            // Assert
            Assert.Equal(Direction.Stabilize, result.Resonance.Direction);
        }

        #endregion

        #region Intonation Direction Tests

        [Fact]
        public void Analyze_FlatIntonation_Increases()
        {
            // Arrange - intonation below 30 Hz
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 20; // Below 30
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Nybegynner);

            // Assert
            Assert.Equal(Direction.Increase, result.Intonation.Direction);
            Assert.Equal(LocalizationService.Instance["Direction_IntonationFlat"], result.Intonation.Reason);
        }

        [Fact]
        public void Analyze_GoodIntonation_Maintains()
        {
            // Arrange
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50; // Good range
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Middels);

            // Assert
            Assert.Equal(Direction.Maintain, result.Intonation.Direction);
        }

        #endregion

        #region Voice Health Direction Tests

        [Fact]
        public void Analyze_HighStrain_DecreasesHealthFocus()
        {
            // Arrange
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50;
            double strainLevel = 60; // Above 50

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Middels);

            // Assert
            Assert.Equal(Direction.Decrease, result.VoiceHealth.Direction);
            Assert.Equal(LocalizationService.Instance["Direction_HealthPressure"], result.VoiceHealth.Reason);
        }

        [Fact]
        public void Analyze_ModerateStrain_StabilizesHealthFocus()
        {
            // Arrange
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50;
            double strainLevel = 35; // Between 25 and 50

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Middels);

            // Assert
            Assert.Equal(Direction.Stabilize, result.VoiceHealth.Direction);
        }

        [Fact]
        public void Analyze_LowStrain_MaintainsHealthFocus()
        {
            // Arrange
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50;
            double strainLevel = 15; // Below 25

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Middels);

            // Assert
            Assert.Equal(Direction.Maintain, result.VoiceHealth.Direction);
        }

        #endregion

        #region Primary Focus Tests

        [Fact]
        public void Analyze_ResonancePriority_AlwaysFirst()
        {
            // Arrange - When resonance needs to increase, it should be primary
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 300;
            double currentF2 = 1200; // Below target - needs increase
            double intonationRange = 50;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Middels);

            // Assert - Resonance should be primary when it's increasing
            Assert.Equal(LocalizationService.Instance["Dashboard_Resonance"], result.PrimaryFocus);
        }

        [Fact]
        public void Analyze_HealthConcern_TakesPriorityOverPitch()
        {
            // Arrange - high strain should prioritize health
            double currentPitch = 180;
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1800; // Good resonance
            double intonationRange = 50;
            double strainLevel = 60; // High strain

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Middels);

            // Assert
            Assert.Equal(LocalizationService.Instance["Dashboard_VoiceHealth"], result.PrimaryFocus);
        }

        [Fact]
        public void Analyze_HasSafetyConcern_FlagSet()
        {
            // Arrange
            double currentPitch = 290; // Very high
            double pitchVariation = 20;
            double currentF1 = 500;
            double currentF2 = 1500;
            double intonationRange = 50;
            double strainLevel = 60;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Avansert);

            // Assert
            Assert.True(result.HasSafetyConcern);
        }

        #endregion

        #region Difficulty Level Tests

        [Fact]
        public void Analyze_BeginnerLevel_HigherPitchTarget()
        {
            // Arrange
            double currentPitch = 150;
            double pitchVariation = 20;
            double currentF1 = 400;
            double currentF2 = 1200;
            double intonationRange = 30;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Nybegynner);

            // Assert - Target should be lower for beginner
            Assert.Equal(150, result.Pitch.TargetValue);
        }

        [Fact]
        public void Analyze_AdvancedLevel_HigherPitchTarget()
        {
            // Arrange
            double currentPitch = 150;
            double pitchVariation = 20;
            double currentF1 = 400;
            double currentF2 = 1200;
            double intonationRange = 30;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Avansert);

            // Assert - Target should be higher for advanced
            Assert.Equal(165, result.Pitch.TargetValue);
        }

        #endregion

        #region Summary Generation Tests

        [Fact]
        public void Analyze_GeneratesSummary_ContainsAllParameters()
        {
            // Arrange
            double currentPitch = 160;
            double pitchVariation = 20;
            double currentF1 = 400;
            double currentF2 = 1200;
            double intonationRange = 25;
            double strainLevel = 10;

            // Act
            var result = _sut.Analyze(
                currentPitch, pitchVariation, currentF1, currentF2,
                intonationRange, strainLevel, DifficultyLevel.Nybegynner);

            // Assert
            Assert.NotNull(result.Summary);
            Assert.Contains(LocalizationService.Instance["Dashboard_Resonance"], result.Summary);
            Assert.Contains(LocalizationService.Instance["Dashboard_Pitch"], result.Summary);
        }

        #endregion
    }
}
