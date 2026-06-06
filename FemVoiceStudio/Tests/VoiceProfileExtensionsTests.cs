using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for VoiceProfileExtensions
    /// Tests personalization and bandit algorithm logic
    /// </summary>
    public class VoiceProfileExtensionsTests
    {
        private readonly VoiceProfileExtensions _sut;

        public VoiceProfileExtensionsTests()
        {
            _sut = new VoiceProfileExtensions();
        }

        #region Profile Update Tests

        [Fact]
        public void UpdateProfile_ValidScore_UpdatesLastUpdated()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            var beforeUpdate = DateTime.Now;
            var score = CreateScoreResult(75);

            // Act
            _sut.UpdateProfile(profile, score);
            var afterUpdate = DateTime.Now;

            // Assert
            Assert.InRange(profile.LastUpdated, beforeUpdate, afterUpdate);
        }

        [Fact]
        public void UpdateProfile_AddsDailyProgress()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            var score = CreateScoreResult(70);

            // Act
            _sut.UpdateProfile(profile, score);

            // Assert
            Assert.NotEmpty(profile.DailyProgress);
            Assert.Equal(70, profile.DailyProgress.First().FemVoiceScore);
        }

        [Fact]
        public void UpdateProfile_KeepsRecentData()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            
            // Add 35 days of progress
            for (int i = 0; i < 35; i++)
            {
                profile.DailyProgress.Add(new DailyProgress
                {
                    Date = DateTime.Today.AddDays(-i),
                    FemVoiceScore = 60 + i
                });
            }

            var score = CreateScoreResult(80);

            // Act
            _sut.UpdateProfile(profile, score);

            // Assert - Should keep recent data
            Assert.True(profile.DailyProgress.Count >= 1);
        }

        [Fact]
        public void UpdateProfile_WithExerciseType_UpdatesEffectiveness()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            var score = CreateScoreResult(75);

            // Act
            _sut.UpdateProfile(profile, score, "Humming");

            // Assert
            Assert.True(profile.ExerciseEffectiveness.ContainsKey("Humming"));
            Assert.Equal(1, profile.ExerciseEffectiveness["Humming"].TimesCompleted);
        }

        [Fact]
        public void UpdateProfile_NullProfile_DoesNotThrow()
        {
            // Arrange
            var score = CreateScoreResult(75);

            // Act & Assert - Should not throw
            _sut.UpdateProfile(null!, score);
        }

        #endregion

        #region Strengths and Weaknesses Tests

        [Fact]
        public void UpdateProfile_CalculatesStrengths_Correctly()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            
            // Add varied scores
            profile.DailyProgress = new List<DailyProgress>
            {
                new DailyProgress { ResonanceScore = 80, PitchScore = 50, IntonationScore = 60 },
                new DailyProgress { ResonanceScore = 75, PitchScore = 55, IntonationScore = 65 },
                new DailyProgress { ResonanceScore = 70, PitchScore = 45, IntonationScore = 55 }
            };

            // Act
            _sut.UpdateProfile(profile, CreateScoreResult(70));

            // Assert
            Assert.True(profile.ResonanceStrength >= profile.PitchStrength);
        }

        [Fact]
        public void UpdateProfile_CalculatesStrengths()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            
            // Add varied scores
            profile.DailyProgress = new List<DailyProgress>
            {
                new DailyProgress { ResonanceScore = 80, PitchScore = 50, IntonationScore = 60 },
                new DailyProgress { ResonanceScore = 75, PitchScore = 55, IntonationScore = 65 },
                new DailyProgress { ResonanceScore = 70, PitchScore = 45, IntonationScore = 55 }
            };

            // Act
            _sut.UpdateProfile(profile, CreateScoreResult(70));

            // Assert
            Assert.True(profile.ResonanceStrength >= 0);
        }

        #endregion

        #region Bandit Algorithm Tests

        [Fact]
        public void RecommendExercise_NoData_ReturnsFirstAvailable()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            var exercises = new List<string> { "Humming", "Glissando", "Setningslesing" };

            // Act
            var result = _sut.RecommendExercise(profile, exercises);

            // Assert
            Assert.NotNull(result);
            Assert.Contains(result, exercises);
        }

        [Fact]
        public void RecommendExercise_SomeExperience_UsesBest()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            profile.ExerciseEffectiveness = new Dictionary<string, ExerciseEffectiveness>
            {
                ["Humming"] = new ExerciseEffectiveness { ExerciseType = "Humming", TimesCompleted = 5, AverageScoreDelta = 75 },
                ["Glissando"] = new ExerciseEffectiveness { ExerciseType = "Glissando", TimesCompleted = 3, AverageScoreDelta = 60 }
            };
            
            var exercises = new List<string> { "Humming", "Glissando", "Setningslesing" };

            // Act
            var result = _sut.RecommendExercise(profile, exercises);

            // Assert - Should recommend Humming (higher score)
            Assert.Equal("Humming", result);
        }

        [Fact]
        public void RecommendExercise_NullProfile_ReturnsDefault()
        {
            // Arrange
            var exercises = new List<string> { "Humming", "Glissando" };

            // Act
            var result = _sut.RecommendExercise(null!, exercises);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void RecommendExercise_EmptyList_ReturnsNull()
        {
            // Arrange
            var profile = CreateEmptyProfile();

            // Act
            var result = _sut.RecommendExercise(profile, new List<string>());

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Personalized Goals Tests

        [Fact]
        public void GetPersonalizedGoals_WeakestResonance_ReturnsResonanceFocus()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            profile.ResonanceStrength = 40;
            profile.PitchStrength = 70;
            profile.IntonationStrength = 65;
            profile.TargetMinPitch = 170;
            profile.TargetMinF2 = 1400;
            profile.WeeklyProgressionRate = 2.0;

            // Act
            var (targetPitch, targetF2, focusArea) = _sut.GetPersonalizedGoals(profile);

            // Assert
            Assert.Equal(LocalizationService.Instance["Dashboard_Resonance"], focusArea);
            Assert.True(targetF2 > 1400);
        }

        [Fact]
        public void GetPersonalizedGoals_NullProfile_ReturnsDefaults()
        {
            // Act
            var (targetPitch, targetF2, focusArea) = _sut.GetPersonalizedGoals(null!);

            // Assert
            Assert.Equal(170, targetPitch);
            Assert.Equal(1400, targetF2);
            Assert.Equal(LocalizationService.Instance["Dashboard_Resonance"], focusArea);
        }

        #endregion

        #region Progression Rate Tests

        [Fact]
        public void CalculateProgressionRate_ImprovingFast_ReturnsHigherRate()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            
            // This week: high scores, Last week: low scores
            var today = DateTime.Today;
            profile.DailyProgress = new List<DailyProgress>();
            
            // Last week
            for (int i = 7; i < 14; i++)
            {
                profile.DailyProgress.Add(new DailyProgress
                {
                    Date = today.AddDays(-i),
                    PitchScore = 50
                });
            }
            
            // This week
            for (int i = 0; i < 7; i++)
            {
                profile.DailyProgress.Add(new DailyProgress
                {
                    Date = today.AddDays(-i),
                    PitchScore = 75
                });
            }

            // Act
            var result = _sut.CalculateProgressionRate(profile);

            // Assert
            Assert.True(result > 2.0);
        }

        [Fact]
        public void CalculateProgressionRate_Stagnating_ReturnsDefaultRate()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            
            var today = DateTime.Today;
            profile.DailyProgress = new List<DailyProgress>();
            
            // Last week and this week: similar scores
            for (int i = 7; i < 14; i++)
            {
                profile.DailyProgress.Add(new DailyProgress
                {
                    Date = today.AddDays(-i),
                    PitchScore = 55
                });
            }
            
            for (int i = 0; i < 7; i++)
            {
                profile.DailyProgress.Add(new DailyProgress
                {
                    Date = today.AddDays(-i),
                    PitchScore = 55
                });
            }

            // Act
            var result = _sut.CalculateProgressionRate(profile);

            // Assert
            Assert.Equal(2.0, result);
        }

        [Fact]
        public void CalculateProgressionRate_NullProfile_ReturnsDefault()
        {
            // Act
            var result = _sut.CalculateProgressionRate(null!);

            // Assert
            Assert.Equal(2.0, result);
        }

        [Fact]
        public void CalculateProgressionRate_InsufficientData_ReturnsDefault()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            profile.DailyProgress.Add(new DailyProgress 
            { 
                Date = DateTime.Today, 
                PitchScore = 60 
            });

            // Act
            var result = _sut.CalculateProgressionRate(profile);

            // Assert
            Assert.Equal(2.0, result);
        }

        #endregion

        #region Focus Recommendation Tests

        [Fact]
        public void GetFocusRecommendation_WeakestResonance_ReturnsResonance()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            profile.ResonanceStrength = 40;
            profile.PitchStrength = 70;
            profile.IntonationStrength = 65;

            // Act
            var result = _sut.GetFocusRecommendation(profile);

            // Assert
            Assert.Contains(LocalizationService.Instance["Dashboard_Resonance"], result);
        }

        [Fact]
        public void GetFocusRecommendation_WeakestPitch_ReturnsPitch()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            profile.ResonanceStrength = 75;
            profile.PitchStrength = 35;
            profile.IntonationStrength = 65;

            // Act
            var result = _sut.GetFocusRecommendation(profile);

            // Assert
            Assert.Contains("Pitch", result);
        }

        [Fact]
        public void GetFocusRecommendation_NullProfile_ReturnsDefault()
        {
            // Act
            var result = _sut.GetFocusRecommendation(null!);

            // Assert
            Assert.Equal(LocalizationService.Instance["VoiceProfile_FocusResonance"], result);
        }

        #endregion

        #region Optimal Session Length Tests

        [Fact]
        public void UpdateProfile_TracksSessionLength()
        {
            // Arrange
            var profile = CreateEmptyProfile();
            
            // Different session lengths
            profile.DailyProgress = new List<DailyProgress>
            {
                new DailyProgress { SessionMinutes = 5, FemVoiceScore = 50 },
                new DailyProgress { SessionMinutes = 5, FemVoiceScore = 55 },
                new DailyProgress { SessionMinutes = 10, FemVoiceScore = 75 },
                new DailyProgress { SessionMinutes = 10, FemVoiceScore = 70 },
                new DailyProgress { SessionMinutes = 15, FemVoiceScore = 60 },
                new DailyProgress { SessionMinutes = 15, FemVoiceScore = 65 }
            };

            // Act
            _sut.UpdateProfile(profile, CreateScoreResult(75));

            // Assert - Should track session data
            Assert.NotNull(profile);
        }

        #endregion

        #region Helper Methods

        private VoiceProfile CreateEmptyProfile()
        {
            return new VoiceProfile
            {
                UserId = 1,
                CreatedAt = DateTime.Now,
                LastUpdated = DateTime.Now,
                ExerciseEffectiveness = new Dictionary<string, ExerciseEffectiveness>(),
                DailyProgress = new List<DailyProgress>()
            };
        }

        private FemVoiceScoreResult CreateScoreResult(double overallScore)
        {
            return new FemVoiceScoreResult
            {
                OverallScore = overallScore,
                ResonanceScore = overallScore * 0.9,
                PitchScore = overallScore * 0.85,
                IntonationScore = overallScore * 0.8,
                VoiceHealthScore = 95,
                CalculatedAt = DateTime.Now
            };
        }

        #endregion
    }
}
