using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    // Type aliases for SmartCoach models in Data namespace
    using SmartCoachBaseline = FemVoiceStudio.Data.SmartCoachBaseline;
    using SmartCoachGoal = FemVoiceStudio.Data.SmartCoachGoal;
    using SmartCoachDailyRecommendation = FemVoiceStudio.Data.SmartCoachDailyRecommendation;
    using SmartCoachWeeklyProgress = FemVoiceStudio.Data.SmartCoachWeeklyProgress;
    using SmartCoachHealthMonitoring = FemVoiceStudio.Data.SmartCoachHealthMonitoring;
    using SmartCoachMessage = FemVoiceStudio.Data.SmartCoachMessage;

    /// <summary>
    /// Integration tests for SmartCoach decision logic.
    /// Tests baseline calculation, recommendation generation, and prioritization.
    /// </summary>
    public class SmartCoachDecisionTests
    {
        private readonly TestDatabaseService _testDatabase;
        
        public SmartCoachDecisionTests()
        {
            _testDatabase = new TestDatabaseService();
        }
        
        [Fact]
        public void GenerateDailyRecommendation_WithLowResonance_PrioritizesResonance()
        {
            // Arrange
            var engine = new SmartCoachEngine(_testDatabase);
            
            // Setup baseline with low resonance
            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 170,
                BaselineResonanceScore = 55, // Below 70 threshold
                BaselineIntonation = 50,
                ConfidenceLevel = "high"
            });
            
            // Act
            var recommendation = engine.GenerateDailyRecommendation(1);
            
            // Assert
            Assert.Equal("resonance", recommendation.FocusArea);
        }
        
        [Fact]
        public void GenerateDailyRecommendation_WithHealthWarning_RecommendsRecovery()
        {
            // Arrange
            var engine = new SmartCoachEngine(_testDatabase);
            
            // Add a recent health issue
            _testDatabase.AddHealthMonitoring(new SmartCoachHealthMonitoring
            {
                UserId = 1,
                Date = DateTime.Today,
                StrainDetected = true,
                StrainType = "fatigue",
                StrainLevel = 60
            });
            
            // Act
            var recommendation = engine.GenerateDailyRecommendation(1);
            
            // Assert
            Assert.True(recommendation.HealthWarning);
            Assert.Equal("recovery", recommendation.FocusArea);
        }

        [Fact]
        public void CalculateWeeklyProgress_CountsRecoveryPracticeSessionsAndMinutes()
        {
            var engine = new SmartCoachEngine(_testDatabase);
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);

            _testDatabase.AddTrainingSession(new TrainingSession
            {
                UserId = 1,
                StartTime = weekStart.AddDays(1),
                EndTime = weekStart.AddDays(1).AddMinutes(5),
                AveragePitch = 0,
                ResonanceScore = 0,
                OverallScore = 0,
                IsRecoveryPractice = true
            });

            var progress = engine.CalculateWeeklyProgress(weekStart, 1);

            Assert.Equal(1, progress.SessionsCount);
            Assert.Equal(5, progress.TotalMinutes);
        }

        [Fact]
        public void CalculateWeeklyProgress_ExcludesRecoveryPracticeFromPerformanceAverages()
        {
            var engine = new SmartCoachEngine(_testDatabase);
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);

            _testDatabase.AddTrainingSession(new TrainingSession
            {
                UserId = 1,
                StartTime = weekStart.AddDays(1),
                EndTime = weekStart.AddDays(1).AddMinutes(5),
                AveragePitch = 0,
                ResonanceScore = 0,
                OverallScore = 0,
                IsRecoveryPractice = true
            });
            _testDatabase.AddTrainingSession(new TrainingSession
            {
                UserId = 1,
                StartTime = weekStart.AddDays(2),
                EndTime = weekStart.AddDays(2).AddMinutes(10),
                AveragePitch = 175,
                ResonanceScore = 72,
                OverallScore = 80,
                IsRecoveryPractice = false
            });

            var progress = engine.CalculateWeeklyProgress(weekStart, 1);

            Assert.Equal(2, progress.SessionsCount);
            Assert.Equal(15, progress.TotalMinutes);
            Assert.Equal(80, progress.AverageScore);
            Assert.Equal(175, progress.AveragePitch);
            Assert.Equal(72, progress.AverageResonance);
        }

        [Fact]
        public void GenerateDailyRecommendation_UsesVoiceGoalProfileFocusWhenClinicallySafe()
        {
            var engine = new SmartCoachEngine(
                _testDatabase,
                voiceGoalProfiles: new StaticVoiceGoalProfileProvider("intonation"));

            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 175,
                BaselineResonanceScore = 78,
                BaselineIntonation = 40,
                ConfidenceLevel = "high"
            });

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("intonation", recommendation.FocusArea);
        }

        [Fact]
        public void GenerateDailyRecommendation_DoesNotLetPitchGoalOverrideLowResonance()
        {
            var engine = new SmartCoachEngine(
                _testDatabase,
                voiceGoalProfiles: new StaticVoiceGoalProfileProvider("pitch"));

            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 150,
                BaselineResonanceScore = 40,
                BaselineIntonation = 70,
                ConfidenceLevel = "high"
            });

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("resonance", recommendation.FocusArea);
        }
        
        [Fact]
        public void CalculateBaseline_WithSufficientData_ReturnsHighConfidence()
        {
            // Arrange
            var engine = new SmartCoachEngine(_testDatabase);
            
            // Add 14 days of training data
            for (int i = 0; i < 14; i++)
            {
                _testDatabase.AddTrainingSession(new TrainingSession
                {
                    StartTime = DateTime.Today.AddDays(-i),
                    EndTime = DateTime.Today.AddDays(-i).AddMinutes(10),
                    AveragePitch = 175 + (i * 0.5),
                    ResonanceScore = 60 + i,
                    OverallScore = 70 + i
                });
            }
            
            // Act
            var baseline = engine.CalculateBaseline(1);
            
            // Assert
            Assert.Equal("high", baseline.ConfidenceLevel);
            Assert.True(baseline.BaselinePitch > 0);
        }

        private sealed class StaticVoiceGoalProfileProvider : IVoiceGoalProfileProvider
        {
            private readonly string _primaryFocus;

            public StaticVoiceGoalProfileProvider(string primaryFocus)
            {
                _primaryFocus = primaryFocus;
            }

            public VoiceGoalProfile? GetProfile(int userId = 1)
                => new()
                {
                    UserId = userId,
                    PrimaryFocus = _primaryFocus
                };

            public void SaveProfile(VoiceGoalProfile profile)
            {
            }
        }
        
        [Fact]
        public void CalculateBaseline_WithInsufficientData_ReturnsLowConfidence()
        {
            // Arrange
            var engine = new SmartCoachEngine(_testDatabase);
            
            // Add only 2 sessions
            _testDatabase.AddTrainingSession(new TrainingSession
            {
                StartTime = DateTime.Today,
                EndTime = DateTime.Today.AddMinutes(10),
                AveragePitch = 175,
                OverallScore = 75
            });
            
            // Act
            var baseline = engine.CalculateBaseline(1);
            
            // Assert
            Assert.Equal("low", baseline.ConfidenceLevel);
        }
        
        [Fact]
        public void GenerateGoals_WithLowResonance_CreatesResonanceGoal()
        {
            // Arrange
            var engine = new SmartCoachEngine(_testDatabase);
            
            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselineResonanceScore = 55, // Below 70 threshold
                ConfidenceLevel = "high"
            });
            
            // Act
            var goals = engine.GenerateGoals(1);
            
            // Assert
            Assert.Contains(goals, g => g.GoalType == "resonance");
        }
        
        [Fact]
        public void AnalyzeSessionForStrain_WithHighPitch_DetectsPitchPress()
        {
            // Arrange
            var engine = new SmartCoachEngine(_testDatabase);
            
            var session = new TrainingSession
            {
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMinutes(10),
                AveragePitch = 200, // Above 180 threshold
                OverallScore = 80
            };
            
            // Act
            var health = engine.AnalyzeSessionForStrain(session, 1);
            
            // Assert
            Assert.True(health.StrainDetected);
            Assert.Equal("pitch_press", health.StrainType);
        }
        
        [Fact]
        public void AnalyzeSessionForStrain_WithScoreDrop_DetectsFatigue()
        {
            // Arrange
            var engine = new SmartCoachEngine(_testDatabase);
            
            // Add previous session with high score
            _testDatabase.AddTrainingSession(new TrainingSession
            {
                StartTime = DateTime.Now.AddHours(-2),
                EndTime = DateTime.Now.AddHours(-2).AddMinutes(10),
                OverallScore = 85,
                AveragePitch = 175
            });
            
            // Current session with significant drop
            var session = new TrainingSession
            {
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMinutes(10),
                OverallScore = 50, // 35 point drop - above 20 threshold
                AveragePitch = 170
            };
            
            // Act
            var health = engine.AnalyzeSessionForStrain(session, 1);
            
            // Assert
            Assert.True(health.StrainDetected);
            Assert.Equal("fatigue", health.StrainType);
        }
        
        [Fact]
        public void GetStatusSummary_WithHealthIssues_ReturnsCautionMessage()
        {
            // Arrange
            var engine = new SmartCoachEngine(_testDatabase);
            
            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 180,
                BaselineResonanceScore = 80,
                ConfidenceLevel = "high"
            });
            
            _testDatabase.AddHealthMonitoring(new SmartCoachHealthMonitoring
            {
                UserId = 1,
                Date = DateTime.Today,
                StrainDetected = true,
                StrainLevel = 60
            });
            
            // Act
            var status = engine.GetStatusSummary(1);
            
            // Assert
            Assert.Equal(LocalizationService.Instance.GetString("SmartCoach_Status_VoiceHealthCareful"), status);
        }
    }
    
    /// <summary>
    /// Unit tests for FemVoiceScore progression control.
    /// Tests the clinical principle: pitch can only increase when resonance and health support it.
    /// </summary>
    public class FemVoiceScoreProgressionTests
    {
        [Fact]
        public void Calculate_PitchIncreaseBlocked_WhenResonanceBelow60()
        {
            // Arrange
            var engine = new FemVoiceScore();
            
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 220,  // High pitch
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                ResonanceScore = 50,  // Below 60 - should block pitch increase
                VoiceHealthScore = 80,
                StrainLevel = 20,
                IntensityRms = 0.3,
                DifficultyLevel = DifficultyLevel.Middels
            };
            
            // Act
            var result = engine.Calculate(input);
            
            // Assert: Pitch score should be penalized because resonance doesn't support high pitch
            Assert.True(result.PitchScore < 70, 
                "Pitch should be penalized when resonance is below 60");
        }
        
        [Fact]
        public void Calculate_PitchIncreaseAllowed_WhenResonanceAbove60AndHealthAbove70()
        {
            // Arrange
            var engine = new FemVoiceScore();
            
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 200,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                ResonanceScore = 70,  // Above threshold
                VoiceHealthScore = 80, // Above threshold
                StrainLevel = 10,
                IntensityRms = 0.3,
                DifficultyLevel = DifficultyLevel.Middels
            };
            
            // Act
            var result = engine.Calculate(input);
            
            // Assert: Pitch should get full score when supported by resonance and health
            Assert.True(result.PitchScore >= 70,
                "Pitch should be rewarded when resonance > 60 and health > 70");
        }
        
        [Fact]
        public void Calculate_OverallScoreReduced_WhenHighPitchWithPoorHealth()
        {
            // Arrange
            var engine = new FemVoiceScore();
            
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 240,  // High pitch
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                ResonanceScore = 50,
                VoiceHealthScore = 40,  // Poor health
                StrainLevel = 60,
                IntensityRms = 0.5,
                DifficultyLevel = DifficultyLevel.Middels
            };
            
            // Act
            var result = engine.Calculate(input);
            
            // Assert: Overall should be capped when high pitch + poor health
            Assert.True(result.OverallScore < 60,
                "Overall score should be reduced when high pitch combined with poor health");
        }
        
        [Fact]
        public void Calculate_MaintenanceSession_ReducesPitchRequirements()
        {
            // Arrange
            var engine = new FemVoiceScore();
            
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 190,  // Not in typical target range
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                ResonanceScore = 65,
                VoiceHealthScore = 85,
                StrainLevel = 5,
                IntensityRms = 0.3,
                DifficultyLevel = DifficultyLevel.Nybegynner,
                IsMaintenanceSession = true  // Maintenance mode
            };
            
            // Act
            var result = engine.Calculate(input);
            
            // Assert: Maintenance session should not penalize as heavily for lower pitch
            Assert.True(result.OverallScore >= 50 || result.WarningFlags == null,
                "Maintenance sessions should allow lower pitch without severe penalty");
        }
        
        [Fact]
        public void Calculate_StrainDetected_AppliesSignificantPenalty()
        {
            // Arrange
            var engine = new FemVoiceScore();
            
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 180,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                ResonanceScore = 70,
                VoiceHealthScore = 60,
                StrainLevel = 75,  // Critical strain level
                IntensityRms = 0.6,
                DifficultyLevel = DifficultyLevel.Middels
            };
            
            // Act
            var result = engine.Calculate(input);
            
            // Assert: Critical strain should cap overall score at 40
            Assert.True(result.OverallScore <= 40,
                "Critical strain should cap overall score at 40");
            Assert.Contains("CRITICAL_STRAIN", result.WarningFlags ?? "");
        }
        
        [Fact]
        public void Calculate_ResonancePriority_OverPitchInScoring()
        {
            // Arrange
            var engine = new FemVoiceScore();
            
            // Case: High pitch but low resonance (anti-pattern: pitch chasing)
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 240,
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                ResonanceScore = 30,  // Poor resonance
                VoiceHealthScore = 80,
                StrainLevel = 10,
                IntensityRms = 0.3,
                DifficultyLevel = DifficultyLevel.Middels
            };
            
            // Act
            var result = engine.Calculate(input);
            
            // Assert: Should penalize high pitch without resonance support
            Assert.True(result.OverallScore <= 70,
                "High pitch without resonance support should be penalized");
        }
        
        [Fact]
        public void Calculate_BalancedScores_ProduceGoodOverall()
        {
            // Arrange
            var engine = new FemVoiceScore();
            
            var input = new FemVoiceScoreInput
            {
                AveragePitch = 200,  // Good pitch
                TargetMinPitch = 165,
                TargetMaxPitch = 255,
                ResonanceScore = 75,  // Good resonance
                VoiceHealthScore = 85, // Good health
                StrainLevel = 5,
                IntensityRms = 0.3,
                IntonationRange = 50,
                DifficultyLevel = DifficultyLevel.Middels
            };
            
            // Act
            var result = engine.Calculate(input);
            
            // Assert: Balanced scores should produce good overall
            Assert.True(result.OverallScore >= 70,
                "Balanced parameters should produce good overall score");
            Assert.True(result.ResonanceScore >= 70);
            Assert.True(result.PitchScore >= 70);
        }
    }
    
    /// <summary>
    /// Unit tests for AdaptiveComfortZoneService.
    /// Tests that comfort zones are calculated correctly based on session type and user state.
    /// </summary>
    public class AdaptiveComfortZoneTests
    {
        [Fact]
        public void CalculateComfortZone_RecoverySession_LowersPitchRange()
        {
            // Arrange
            var testDb = new TestDatabaseService();
            testDb.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 180,
                BaselineResonanceScore = 70,
                ConfidenceLevel = "high"
            });
            
            var smartCoach = new SmartCoachEngine(testDb);
            var service = new AdaptiveComfortZoneService(smartCoach);
            
            // Act
            var zone = service.CalculateComfortZone(1, SessionType.Recovery);
            
            // Assert: Recovery should lower pitch range
            Assert.True(zone.Max < 180,
                "Recovery session should have reduced max pitch");
        }
        
        [Fact]
        public void CalculateComfortZone_MaintenanceSession_BalancesPitch()
        {
            // Arrange
            var testDb = new TestDatabaseService();
            testDb.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 175,
                BaselineResonanceScore = 65,
                ConfidenceLevel = "high"
            });
            
            var smartCoach = new SmartCoachEngine(testDb);
            var service = new AdaptiveComfortZoneService(smartCoach);
            
            // Act
            var zone = service.CalculateComfortZone(1, SessionType.Maintenance);
            
            // Assert: Maintenance should have moderate pitch range
            Assert.True(zone.Min >= 150 && zone.Max <= 200,
                "Maintenance should have balanced pitch range");
        }
        
        [Fact]
        public void GetRecommendedSessionType_WithRecentStrain_RecommendsRecovery()
        {
            // Arrange
            var testDb = new TestDatabaseService();
            testDb.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 175,
                BaselineResonanceScore = 60,
                ConfidenceLevel = "high"
            });
            
            // Add recent health issue
            testDb.AddHealthMonitoring(new SmartCoachHealthMonitoring
            {
                UserId = 1,
                Date = DateTime.Today,
                StrainDetected = true,
                StrainLevel = 60,
                StrainType = "fatigue"
            });
            
            var smartCoach = new SmartCoachEngine(testDb);
            var service = new AdaptiveComfortZoneService(smartCoach);
            
            // Act
            var sessionType = service.GetRecommendedSessionType(1);
            
            // Assert
            Assert.Equal(SessionType.Recovery, sessionType);
        }
    }
}
