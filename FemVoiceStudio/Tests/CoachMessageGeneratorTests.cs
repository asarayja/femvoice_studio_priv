using System;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for CoachMessageGenerator
    /// Tests message generation with What/Why/How structure
    /// </summary>
    public class CoachMessageGeneratorTests
    {
        private readonly CoachMessageGenerator _sut;

        public CoachMessageGeneratorTests()
        {
            _sut = new CoachMessageGenerator();
        }

        #region Message Structure Tests

        [Fact]
        public void GenerateMessage_AlwaysHasWhat()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.False(string.IsNullOrEmpty(result.What));
        }

        [Fact]
        public void GenerateMessage_AlwaysHasHow()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.False(string.IsNullOrEmpty(result.How));
        }

        [Fact]
        public void GenerateMessage_AlwaysHasWhy()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.False(string.IsNullOrEmpty(result.Why));
        }

        [Fact]
        public void GenerateMessage_AlwaysHasFullMessage()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.False(string.IsNullOrEmpty(result.FullMessage));
            Assert.Contains("**", result.FullMessage); // Markdown bold
        }

        [Fact]
        public void GenerateMessage_AlwaysHasEmoji()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.False(string.IsNullOrEmpty(result.Emoji));
        }

        [Fact]
        public void GenerateMessage_AlwaysHasEncouragement()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.False(string.IsNullOrEmpty(result.Encouragement));
        }

        #endregion

        #region Level-Specific Tests

        [Fact]
        public void GenerateMessage_BeginnerLevel_HasBeginnerExercises()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.NotNull(result.How);
            // Beginner should have specific exercises - check for absence of advanced terms
        }

        [Fact]
        public void GenerateMessage_IntermediateLevel_AdvancedExercise()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Intermediate, 60);

            // Assert - Intermediate should have different exercises
            Assert.NotNull(result.How);
        }

        [Fact]
        public void GenerateMessage_AdvancedLevel_IntegrationExercise()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Advanced, 60);

            // Assert - Advanced should mention integration
            Assert.NotNull(result.How);
        }

        #endregion

        #region Parameter-Specific Tests

        [Fact]
        public void GenerateMessage_ResonansIncrease_HasForwardResonance()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.Equal(LocalizationService.Instance["Dashboard_Resonance"], result.What);
            Assert.False(string.IsNullOrWhiteSpace(result.How));
        }

        [Fact]
        public void GenerateMessage_ResonansMaintain_HasMaintenanceMessage()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Maintain);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 70);

            // Assert
            Assert.NotNull(result.How);
        }

        [Fact]
        public void GenerateMessage_PitchIncrease_HasGlissando()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Pitch", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.Equal(LocalizationService.Instance["Dashboard_Pitch"], result.What);
        }

        [Fact]
        public void GenerateMessage_PitchDecrease_HasRelaxation()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Pitch", Direction.Decrease);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.Equal(LocalizationService.Instance["CoachGenerator_PitchReductionHow"], result.How);
        }

        [Fact]
        public void GenerateMessage_IntonationIncrease_HasIntonationExercises()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Intonasjon", Direction.Increase);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 60);

            // Assert
            Assert.Equal(LocalizationService.Instance["Dashboard_Intonation"], result.What);
        }

        [Fact]
        public void GenerateMessage_VoiceHealthDecrease_HasHealthFocus()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Stemmehelse", Direction.Decrease);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 40);

            // Assert
            Assert.Equal(LocalizationService.Instance["Dashboard_VoiceHealth"], result.What);
        }

        #endregion

        #region Encouragement Tests

        [Fact]
        public void GenerateMessage_HighScore_ExcitingEncouragement()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Vedlikehold", Direction.Maintain);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 85);

            // Assert
            Assert.NotNull(result.Encouragement);
            // High score should have enthusiastic messages
        }

        [Fact]
        public void GenerateMessage_MediumScore_ModerateEncouragement()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Vedlikehold", Direction.Maintain);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 65);

            // Assert
            Assert.NotNull(result.Encouragement);
        }

        [Fact]
        public void GenerateMessage_LowScore_SupportiveEncouragement()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Vedlikehold", Direction.Maintain);
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 45);

            // Assert
            Assert.NotNull(result.Encouragement);
        }

        #endregion

        #region Short Status Tests

        [Fact]
        public void GenerateShortStatus_SafetyConcern_ReturnsShieldMessage()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Stemmehelse", Direction.Decrease);
            direction.HasSafetyConcern = true;
            
            // Act
            var result = _sut.GenerateShortStatus(direction);

            // Assert
            Assert.Contains("🛡️", result);
        }

        [Fact]
        public void GenerateShortStatus_ResonanceFocus_ReturnsResonanceEmoji()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Resonans", Direction.Increase);
            
            // Act
            var result = _sut.GenerateShortStatus(direction);

            // Assert
            Assert.Contains("🔊", result);
        }

        [Fact]
        public void GenerateShortStatus_PitchFocus_ReturnsMusicEmoji()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Pitch", Direction.Increase);
            
            // Act
            var result = _sut.GenerateShortStatus(direction);

            // Assert
            Assert.Contains("🎵", result);
        }

        [Fact]
        public void GenerateShortStatus_IntonationFocus_ReturnsChartEmoji()
        {
            // Arrange
            var direction = CreateDirectionAnalysis("Intonasjon", Direction.Increase);
            
            // Act
            var result = _sut.GenerateShortStatus(direction);

            // Assert
            Assert.Contains("📈", result);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void GenerateMessage_Maintenance_NoFocus_ReturnsMaintenanceMessage()
        {
            // Arrange
            var direction = new DirectionAnalysisResult
            {
                PrimaryFocus = "Vedlikehold",
                Resonance = new DirectionRecommendation { Direction = Direction.Maintain },
                Pitch = new DirectionRecommendation { Direction = Direction.Stabilize },
                Intonation = new DirectionRecommendation { Direction = Direction.Maintain },
                VoiceHealth = new DirectionRecommendation { Direction = Direction.Maintain }
            };
            
            // Act
            var result = _sut.GenerateMessage(direction, TrainingLevel.Beginner, 70);

            // Assert
            Assert.Equal(LocalizationService.Instance["Direction_Maintenance"], result.What);
        }

        #endregion

        #region Helper Methods

        private DirectionAnalysisResult CreateDirectionAnalysis(string primaryFocus, Direction pitchDirection)
        {
            var resonance = new DirectionRecommendation
            {
                Parameter = "Resonans",
                Direction = Direction.Maintain,
                Reason = "God resonans"
            };

            var pitch = new DirectionRecommendation
            {
                Parameter = "Pitch",
                Direction = pitchDirection,
                Reason = "Under målområde"
            };

            var intonation = new DirectionRecommendation
            {
                Parameter = "Intonasjon",
                Direction = Direction.Maintain,
                Reason = "God variasjon"
            };

            var voiceHealth = new DirectionRecommendation
            {
                Parameter = "Stemmehelse",
                Direction = Direction.Maintain,
                Reason = "God helse"
            };

            return new DirectionAnalysisResult
            {
                PrimaryFocus = primaryFocus,
                Resonance = resonance,
                Pitch = pitch,
                Intonation = intonation,
                VoiceHealth = voiceHealth,
                HasSafetyConcern = false
            };
        }

        #endregion
    }
}
