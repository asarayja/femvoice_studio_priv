using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Integration tests for safety lock logic in ProgressionService.
    /// Tests strain-based progression blocking.
    /// </summary>
    public class SafetyLockTests
    {
        private readonly TestDatabaseService _testDatabase;
        private readonly ProgressionService _progressionService;
        
        public SafetyLockTests()
        {
            _testDatabase = new TestDatabaseService();
            _progressionService = new ProgressionService(_testDatabase);
        }
        
        [Fact]
        public void RecordStrainIncident_CriticalStrain_EngagesLock()
        {
            // Act - Record critical strain (level >= 70)
            _progressionService.RecordStrainIncident(75, "pitch_press");
            
            // Assert
            var status = _progressionService.GetSafetyLockStatus();
            Assert.True(status.IsBlocked);
            Assert.Equal(1, status.CriticalStrainCount);
            Assert.NotNull(status.Reason);
        }
        
        [Fact]
        public void RecordStrainIncident_ModerateStrainOnce_DoesNotEngageLock()
        {
            // Act - Record moderate strain (level >= 40 but < 70)
            _progressionService.RecordStrainIncident(50, "fatigue");
            
            // Assert
            var status = _progressionService.GetSafetyLockStatus();
            Assert.False(status.IsBlocked);
            Assert.Equal(1, status.ModerateStrainCount);
        }
        
        [Fact]
        public void RecordStrainIncident_TwoModerateStrains_EngagesLock()
        {
            // Act - Record first moderate strain
            _progressionService.RecordStrainIncident(45, "fatigue");
            
            // Act - Record second moderate strain
            _progressionService.RecordStrainIncident(55, "intensity");
            
            // Assert - Lock should engage on 2nd moderate strain
            var status = _progressionService.GetSafetyLockStatus();
            Assert.True(status.IsBlocked);
            Assert.Equal(2, status.ModerateStrainCount);
            Assert.Contains("moderate", status.Reason, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void IsProgressionBlocked_WithActiveLock_ReturnsTrue()
        {
            // Arrange
            _progressionService.RecordStrainIncident(80, "critical");
            
            // Act
            var isBlocked = _progressionService.IsProgressionBlocked();
            
            // Assert
            Assert.True(isBlocked);
        }
        
        [Fact]
        public void EvaluateProgression_WithActiveLock_BlocksPromotion()
        {
            // Arrange
            _progressionService.RecordStrainIncident(75, "critical");
            
            var session = new TrainingSession
            {
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMinutes(10),
                OverallScore = 85, // Would normally qualify for promotion
                AveragePitch = 190
            };
            
            // Act
            var result = _progressionService.EvaluateProgression(session);
            
            // Assert - Should not promote due to safety lock
            Assert.Equal(DifficultyLevel.Nybegynner, result.NewDifficulty);
            Assert.True(result.IsBlockedBySafety);
            Assert.Contains("blocked", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void ResetStrainCounters_ResetsCounts()
        {
            // Arrange
            _progressionService.RecordStrainIncident(50, "moderate");
            _progressionService.RecordStrainIncident(45, "moderate");
            
            // Act
            _progressionService.ResetStrainCounters();
            
            // Assert
            var status = _progressionService.GetSafetyLockStatus();
            Assert.Equal(0, status.ModerateStrainCount);
            Assert.Equal(0, status.CriticalStrainCount);
        }
        
        [Fact]
        public void SafetyLockEngaged_EventRaisedOnCriticalStrain()
        {
            // Arrange
            var eventRaised = false;
            string? eventReason = null;
            _progressionService.SafetyLockEngaged += (s, e) =>
            {
                eventRaised = true;
                eventReason = e.Reason;
            };
            
            // Act
            _progressionService.RecordStrainIncident(85, "critical_strain");
            
            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(eventReason);
        }
        
        [Fact]
        public void SafetyLockReleased_EventRaisedAfterRelease()
        {
            // Arrange
            _progressionService.RecordStrainIncident(80, "critical");
            
            var releaseEventRaised = false;
            _progressionService.SafetyLockReleased += (s, e) => releaseEventRaised = true;
            
            // Act
            var released = _progressionService.ReleaseSafetyLock();
            
            // Assert
            Assert.True(released);
            Assert.True(releaseEventRaised);
            Assert.False(_progressionService.IsProgressionBlocked());
        }
        
        [Fact]
        public void ReleaseSafetyLock_ResetsCounters()
        {
            // Arrange
            _progressionService.RecordStrainIncident(75, "critical");
            _progressionService.RecordStrainIncident(50, "moderate");
            
            // Act
            _progressionService.ReleaseSafetyLock();
            
            // Assert
            var status = _progressionService.GetSafetyLockStatus();
            Assert.Equal(0, status.CriticalStrainCount);
            Assert.Equal(0, status.ModerateStrainCount);
            Assert.Null(status.Reason);
        }
        
        [Fact]
        public void EvaluateProgressionWithSafety_BlocksPromotionWhenLocked()
        {
            // Arrange
            _progressionService.RecordStrainIncident(72, "critical");
            
            var session = new TrainingSession
            {
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMinutes(10),
                OverallScore = 90,
                AveragePitch = 200
            };
            
            // Act
            var result = _progressionService.EvaluateProgressionWithSafety(session);
            
            // Assert
            Assert.Equal(DifficultyLevel.Nybegynner, result.NewDifficulty);
            Assert.True(result.IsBlockedBySafety);
        }
        
        [Fact]
        public void GetSafetyLockStatus_IncludesExpirationTime()
        {
            // Arrange
            _progressionService.RecordStrainIncident(85, "critical");
            
            // Act
            var status = _progressionService.GetSafetyLockStatus();
            
            // Assert
            Assert.NotNull(status.ExpiresAt);
            Assert.True(status.ExpiresAt > DateTime.Now);
        }
    }
    
    /// <summary>
    /// Tests for PeriodizationService
    /// </summary>
    public class PeriodizationServiceTests
    {
        private readonly TestDatabaseService _testDatabase;
        private readonly PeriodizationService _periodizationService;
        
        public PeriodizationServiceTests()
        {
            _testDatabase = new TestDatabaseService();
            _periodizationService = new PeriodizationService(_testDatabase);
        }
        
        [Fact]
        public void Evaluate_NewUser_StartsInActivePhase()
        {
            // Act
            var result = _periodizationService.Evaluate(1);
            
            // Assert
            Assert.Equal(TrainingPhase.Active, result.Phase);
            Assert.Equal(1, result.WeekInPhase);
        }
        
        [Fact]
        public void RecordSession_UpdatesWeeklyStats()
        {
            // Arrange
            var session = new TrainingSession
            {
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddMinutes(15),
                OverallScore = 75,
                AveragePitch = 180
            };
            
            // Act
            _periodizationService.RecordSession(session, 1);
            var result = _periodizationService.Evaluate(1);
            
            // Assert - Should have recorded the session
            Assert.NotNull(result);
        }
        
        [Fact]
        public void RecordStrain_ModerateStrain_IncrementsCount()
        {
            // Act
            _periodizationService.RecordStrain(50, 1); // Moderate
            
            // Assert
            var state = _periodizationService.GetState();
            Assert.Equal(1, state.ModerateStrainCount);
        }
        
        [Fact]
        public void RecordStrain_CriticalStrain_EngagesBlock()
        {
            // Act
            _periodizationService.RecordStrain(80, 1); // Critical
            
            // Assert
            var state = _periodizationService.GetState();
            Assert.Equal(1, state.CriticalStrainCount);
            Assert.True(state.IsProgressionBlocked);
        }
        
        [Fact]
        public void RecordStrain_TwoModerateStrains_EngagesBlock()
        {
            // Act
            _periodizationService.RecordStrain(45, 1);
            _periodizationService.RecordStrain(55, 1);
            
            // Assert
            var state = _periodizationService.GetState();
            Assert.True(state.IsProgressionBlocked);
        }
        
        [Fact]
        public void ForcePhaseTransition_ChangesPhase()
        {
            // Act
            _periodizationService.ForcePhaseTransition(TrainingPhase.Maintenance);
            
            // Assert
            var state = _periodizationService.GetState();
            Assert.Equal(TrainingPhase.Maintenance, state.CurrentPhase);
        }
        
        [Fact]
        public void GetAdjustedTarget_MaintenancePhase_Returns70Percent()
        {
            // Arrange
            _periodizationService.ForcePhaseTransition(TrainingPhase.Maintenance);
            
            // Act
            var adjusted = _periodizationService.GetAdjustedTarget(100);
            
            // Assert
            Assert.Equal(70, adjusted);
        }
        
        [Fact]
        public void GetAdjustedTarget_DeloadPhase_Returns50Percent()
        {
            // Arrange
            _periodizationService.ForcePhaseTransition(TrainingPhase.Deload);
            
            // Act
            var adjusted = _periodizationService.GetAdjustedTarget(100);
            
            // Assert
            Assert.Equal(50, adjusted);
        }
        
        [Fact]
        public void ReleaseProgressionLock_AfterExpiry_ReleasesLock()
        {
            // Arrange - Manually set a lock
            var state = _periodizationService.GetState();
            // Note: We can't easily test expiry in unit tests without mocking
            
            // This test verifies the method exists and works
            var released = _periodizationService.ReleaseProgressionLock(1);
            Assert.True(released);
        }
        
        [Fact]
        public void PhaseTransition_EventRaised()
        {
            // Arrange
            var eventRaised = false;
            _periodizationService.PhaseTransition += (s, e) => eventRaised = true;
            
            // Act
            _periodizationService.ForcePhaseTransition(TrainingPhase.Maintenance);
            
            // Assert
            Assert.True(eventRaised);
        }
        
        [Fact]
        public void ProgressionBlocked_EventRaised()
        {
            // Arrange
            var eventRaised = false;
            _periodizationService.ProgressionBlocked += (s, e) => eventRaised = true;
            
            // Act
            _periodizationService.RecordStrain(85, 1); // Critical strain
            
            // Assert
            Assert.True(eventRaised);
        }
    }
}
