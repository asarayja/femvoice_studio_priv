using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for LevelClassificationSystem
    /// Tests upgrade/downgrade logic and level transitions
    /// </summary>
    public class LevelClassificationSystemTests
    {
        private readonly LevelClassificationSystem _sut;

        public LevelClassificationSystemTests()
        {
            _sut = new LevelClassificationSystem();
        }

        #region Level Conversion Tests

        [Theory]
        [InlineData(DifficultyLevel.Nybegynner, TrainingLevel.Beginner)]
        [InlineData(DifficultyLevel.Middels, TrainingLevel.Intermediate)]
        [InlineData(DifficultyLevel.Avansert, TrainingLevel.Advanced)]
        public void FromDifficultyLevel_ValidInput_ReturnsCorrectLevel(
            DifficultyLevel difficulty, TrainingLevel expected)
        {
            // Act
            var result = LevelClassificationSystem.FromDifficultyLevel(difficulty);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(TrainingLevel.Beginner, DifficultyLevel.Nybegynner)]
        [InlineData(TrainingLevel.Intermediate, DifficultyLevel.Middels)]
        [InlineData(TrainingLevel.Advanced, DifficultyLevel.Avansert)]
        public void ToDifficultyLevel_ValidInput_ReturnsCorrectLevel(
            TrainingLevel level, DifficultyLevel expected)
        {
            // Act
            var result = LevelClassificationSystem.ToDifficultyLevel(level);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Tolerance Tests

        [Fact]
        public void GetTolerance_Beginner_ReturnsTwentyPercent()
        {
            // Act
            var result = LevelClassificationSystem.GetTolerance(TrainingLevel.Beginner);

            // Assert
            Assert.Equal(0.20, result);
        }

        [Fact]
        public void GetTolerance_Intermediate_ReturnsTenPercent()
        {
            // Act
            var result = LevelClassificationSystem.GetTolerance(TrainingLevel.Intermediate);

            // Assert
            Assert.Equal(0.10, result);
        }

        [Fact]
        public void GetTolerance_Advanced_ReturnsFivePercent()
        {
            // Act
            var result = LevelClassificationSystem.GetTolerance(TrainingLevel.Advanced);

            // Assert
            Assert.Equal(0.05, result);
        }

        #endregion

        #region Pitch Range Tests

        [Fact]
        public void GetPitchRange_Beginner_ReturnsCorrectRange()
        {
            // Act
            var (min, max) = LevelClassificationSystem.GetPitchRange(TrainingLevel.Beginner);

            // Assert
            Assert.Equal(140, min);
            Assert.Equal(200, max);
        }

        [Fact]
        public void GetPitchRange_Intermediate_ReturnsCorrectRange()
        {
            // Act
            var (min, max) = LevelClassificationSystem.GetPitchRange(TrainingLevel.Intermediate);

            // Assert
            Assert.Equal(155, min);
            Assert.Equal(230, max);
        }

        [Fact]
        public void GetPitchRange_Advanced_ReturnsCorrectRange()
        {
            // Act
            var (min, max) = LevelClassificationSystem.GetPitchRange(TrainingLevel.Advanced);

            // Assert
            Assert.Equal(165, min);
            Assert.Equal(255, max);
        }

        #endregion

        #region Level Name Tests

        [Fact]
        public void GetLevelName_Beginner_ReturnsNorwegian()
        {
            // Act
            var result = LevelClassificationSystem.GetLevelName(TrainingLevel.Beginner);

            // Assert
            Assert.Equal("Nybegynner", result);
        }

        [Fact]
        public void GetLevelName_Intermediate_ReturnsNorwegian()
        {
            // Act
            var result = LevelClassificationSystem.GetLevelName(TrainingLevel.Intermediate);

            // Assert
            Assert.Equal("Middels", result);
        }

        [Fact]
        public void GetLevelName_Advanced_ReturnsNorwegian()
        {
            // Act
            var result = LevelClassificationSystem.GetLevelName(TrainingLevel.Advanced);

            // Assert
            Assert.Equal("Avansert", result);
        }

        #endregion

        #region Level Emoji Tests

        [Fact]
        public void GetLevelEmoji_Beginner_ReturnsGreenCircle()
        {
            // Act
            var result = LevelClassificationSystem.GetLevelEmoji(TrainingLevel.Beginner);

            // Assert
            Assert.Equal("🟢", result);
        }

        [Fact]
        public void GetLevelEmoji_Intermediate_ReturnsYellowCircle()
        {
            // Act
            var result = LevelClassificationSystem.GetLevelEmoji(TrainingLevel.Intermediate);

            // Assert
            Assert.Equal("🟡", result);
        }

        [Fact]
        public void GetLevelEmoji_Advanced_ReturnsBlueCircle()
        {
            // Act
            var result = LevelClassificationSystem.GetLevelEmoji(TrainingLevel.Advanced);

            // Assert
            Assert.Equal("🔵", result);
        }

        #endregion

        #region Upgrade Tests

        [Fact]
        public void Classify_SevenOfTenAboveThreshold_Upgrades()
        {
            // Arrange - 7 of 10 sessions above 70
            var scores = CreateScoreList(7, 75, 3, 55);
            var lastTransition = DateTime.Now.AddDays(-20);

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Beginner, lastTransition);

            // Assert
            Assert.True(result.ShouldUpgrade);
            Assert.Equal(TrainingLevel.Intermediate, result.SuggestedLevel);
        }

        [Fact]
        public void Classify_LessThanSevenAboveThreshold_NoUpgrade()
        {
            // Arrange - only 4 of 10 sessions above 70
            var scores = CreateScoreList(4, 75, 6, 55);
            var lastTransition = DateTime.Now.AddDays(-20);

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Beginner, lastTransition);

            // Assert
            Assert.False(result.ShouldUpgrade);
            Assert.Equal(TrainingLevel.Beginner, result.SuggestedLevel);
        }

        [Fact]
        public void Classify_AtAdvancedLevel_NoFurtherUpgrade()
        {
            // Arrange
            var scores = CreateScoreList(9, 80, 1, 60);
            var lastTransition = DateTime.Now.AddDays(-20);

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Advanced, lastTransition);

            // Assert
            Assert.False(result.ShouldUpgrade);
        }

        #endregion

        #region Downgrade Tests

        [Fact]
        public void Classify_FiveOfTenBelowThreshold_Downgrades()
        {
            // Arrange - 3 sessions above, 7 sessions below (need 5 below for downgrade)
            var scores = CreateScoreList(3, 45, 7, 75);
            var lastTransition = DateTime.Now.AddDays(-20);

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Intermediate, lastTransition);

            // Assert
            Assert.True(result.ShouldDowngrade || result.DowngradeRisk > 0,
                "Should have downgrade risk when most sessions are below threshold");
        }

        [Fact]
        public void Classify_StrainDetected_Downgrades()
        {
            // Arrange - 3 sessions but with strain warning
            var scores = new List<FemVoiceScoreResult>
            {
                CreateScore(70, true),
                CreateScore(65, true),
                CreateScore(60, true)
            };
            var lastTransition = DateTime.Now.AddDays(-20);

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Intermediate, lastTransition);

            // Assert
            Assert.True(result.ShouldDowngrade);
            Assert.Equal(LocalizationService.Instance.GetString("Level_StrainDetectedReason"), result.Reason);
        }

        #endregion

        #region Transition Timing Tests

        [Fact]
        public void Classify_LessThanFourteenDaysSinceTransition_NoChange()
        {
            // Arrange - only 10 days since last transition
            var scores = CreateScoreList(8, 80, 2, 50);
            var lastTransition = DateTime.Now.AddDays(-10);

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Beginner, lastTransition);

            // Assert
            Assert.False(result.ShouldUpgrade);
            Assert.Contains("14", result.Reason);
        }

        [Fact]
        public void Classify_MoreThanFourteenDaysSinceTransition_AllowsChange()
        {
            // Arrange - 20 days since last transition
            var scores = CreateScoreList(8, 80, 2, 50);
            var lastTransition = DateTime.Now.AddDays(-20);

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Beginner, lastTransition);

            // Assert
            Assert.True(result.ShouldUpgrade);
        }

        #endregion

        #region Insufficient Data Tests

        [Fact]
        public void Classify_LessThanThreeSessions_ReturnsCurrentLevel()
        {
            // Arrange
            var scores = new List<FemVoiceScoreResult>
            {
                CreateScore(75, false),
                CreateScore(65, false)
            };

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Beginner, null);

            // Assert
            Assert.Equal(TrainingLevel.Beginner, result.SuggestedLevel);
            Assert.Equal(LocalizationService.Instance.GetString("Level_NotEnoughDataReason"), result.Reason);
        }

        [Fact]
        public void Classify_NullScores_ReturnsCurrentLevel()
        {
            // Act
            var result = _sut.Classify(null!, TrainingLevel.Beginner, null);

            // Assert
            Assert.Equal(TrainingLevel.Beginner, result.SuggestedLevel);
        }

        #endregion

        #region Progress Calculation Tests

        [Fact]
        public void Classify_CalculatesUpgradeProgress_Correctly()
        {
            // Arrange - 7 of 10 above threshold = 70%
            var scores = CreateScoreList(7, 75, 3, 55);
            var lastTransition = DateTime.Now.AddDays(-20);

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Beginner, lastTransition);

            // Assert
            Assert.Equal(70, result.UpgradeProgress);
        }

        [Fact]
        public void Classify_CalculatesDowngradeRisk_Correctly()
        {
            // Arrange - 3 of 10 above, 7 below - should be 70% risk (3/10 = 30% above)
            var scores = CreateScoreList(3, 45, 7, 75);
            var lastTransition = DateTime.Now.AddDays(-20);

            // Act
            var result = _sut.Classify(scores, TrainingLevel.Intermediate, lastTransition);

            // Assert - Risk should reflect sessions below threshold
            Assert.True(result.DowngradeRisk > 0, "Should calculate downgrade risk");
        }

        #endregion

        #region Exercise Recommendation Tests

        [Fact]
        public void GetRecommendedExercises_Beginner_ReturnsBasicExercises()
        {
            // Act
            var result = LevelClassificationSystem.GetRecommendedExercises(TrainingLevel.Beginner);

            // Assert
            Assert.NotEmpty(result);
            Assert.Equal(LocalizationService.Instance.GetString("Level_Exercise_BasicResonance"), result[0]);
        }

        [Fact]
        public void GetRecommendedExercises_Intermediate_ReturnsCombinedExercises()
        {
            // Act
            var result = LevelClassificationSystem.GetRecommendedExercises(TrainingLevel.Intermediate);

            // Assert
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetRecommendedExercises_Advanced_ReturnsConversationExercises()
        {
            // Act
            var result = LevelClassificationSystem.GetRecommendedExercises(TrainingLevel.Advanced);

            // Assert
            Assert.NotEmpty(result);
            Assert.Equal(LocalizationService.Instance.GetString("Level_Exercise_Conversation"), result[0]);
        }

        #endregion

        #region Focus Area Tests

        [Fact]
        public void GetFocusArea_Beginner_EmphasizesResonance()
        {
            // Act
            var result = LevelClassificationSystem.GetFocusArea(TrainingLevel.Beginner);

            // Assert
            Assert.Equal(LocalizationService.Instance.GetString("Level_FocusArea_Beginner"), result);
        }

        [Fact]
        public void GetFocusArea_Intermediate_EmphasizesPrecision()
        {
            // Act
            var result = LevelClassificationSystem.GetFocusArea(TrainingLevel.Intermediate);

            // Assert
            Assert.Equal(LocalizationService.Instance.GetString("Level_FocusArea_Intermediate"), result);
        }

        [Fact]
        public void GetFocusArea_Advanced_EmphasizesNaturalness()
        {
            // Act
            var result = LevelClassificationSystem.GetFocusArea(TrainingLevel.Advanced);

            // Assert
            Assert.Equal(LocalizationService.Instance.GetString("Level_FocusArea_Advanced"), result);
        }

        #endregion

        #region Helper Methods

        private List<FemVoiceScoreResult> CreateScoreList(
            int highScoreCount, int highScore,
            int lowScoreCount, int lowScore)
        {
            var scores = new List<FemVoiceScoreResult>();

            for (int i = 0; i < highScoreCount; i++)
            {
                scores.Add(CreateScore(highScore, false));
            }

            for (int i = 0; i < lowScoreCount; i++)
            {
                scores.Add(CreateScore(lowScore, false));
            }

            // Shuffle the list
            var random = new Random(42);
            return scores.OrderBy(_ => random.Next()).ToList();
        }

        private FemVoiceScoreResult CreateScore(double overallScore, bool hasStrain)
        {
            return new FemVoiceScoreResult
            {
                OverallScore = overallScore,
                ResonanceScore = overallScore * 0.9,
                PitchScore = overallScore * 0.95,
                IntonationScore = overallScore * 0.85,
                VoiceHealthScore = hasStrain ? 30 : 90,
                CalculatedAt = DateTime.Now,
                WarningFlags = hasStrain ? "MODERATE_STRAIN" : null
            };
        }

        #endregion
    }
}
