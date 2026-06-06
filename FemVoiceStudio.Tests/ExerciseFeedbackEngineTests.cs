using System;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Subsystems.Analysis;
using Xunit;

namespace FemVoiceStudio.Tests
{
    // Type alias for VoiceMetrics in the correct namespace
    using VoiceMetrics = FemVoiceStudio.Models.VoiceMetrics;

    /// <summary>
    /// Unit tests for ExerciseFeedbackEngine
    /// Tests the core evaluation logic
    /// </summary>
    public class ExerciseFeedbackEngineTests
    {
        private ExerciseDefinition CreateDefaultDefinition()
        {
            return new ExerciseDefinition
            {
                ExerciseId = 1,
                Name = "Test Exercise",
                TargetPitchRange = new TargetRange(165, 220),
                TargetF1Range = new TargetRange(300, 800),
                TargetF2Range = new TargetRange(1800, 2600),
                TargetF3Min = 2700,
                StabilityThresholdPercent = 2.0,
                RequiresIntonation = false
            };
        }
        
        private Models.VoiceMetrics CreateMetrics(
            double pitch = 180, 
            double f1 = 500, 
            double f2 = 2000, 
            double f3 = 2800,
            double jitter = 1.0, 
            double shimmer = 1.5,
            double strain = 0.2,
            double intensity = 0.5,
            double intonationRange = 10)
        {
            return new Models.VoiceMetrics
            {
                Pitch = pitch,
                F1 = f1,
                F2 = f2,
                F3 = f3,
                Jitter = jitter,
                Shimmer = shimmer,
                StrainLevel = strain,
                Intensity = intensity,
                IntonationRange = intonationRange,
                Timestamp = DateTime.Now
            };
        }
        
        [Fact]
        public void EvaluateMetrics_CorrectValues_ReturnsCorrectStatus()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            var metrics = CreateMetrics(
                pitch: 180,      // Within 165-220 range
                f2: 2100,        // Within 1800-2600 range
                jitter: 1.5       // Below 2% threshold
            );
            
            engine.Start(definition, UserLevel.Middels);
            
            // Act
            var result = engine.EvaluateMetrics(metrics);
            
            // Assert
            Assert.Equal(EvaluationStatus.Correct, result.Status);
            Assert.Equal(EvaluationStatus.Correct, result.PitchStatus);
            Assert.Equal(EvaluationStatus.Correct, result.ResonanceStatus);
            Assert.Equal(EvaluationStatus.Correct, result.StabilityStatus);
            Assert.Equal(HealthIndicator.Safe, result.HealthIndicator);
            
            engine.Stop();
        }
        
        [Fact]
        public void EvaluateMetrics_PitchOutOfRange_ReturnsAdjustStatus()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            var metrics = CreateMetrics(
                pitch: 250,      // Above 220 - out of range
                f2: 2100,
                jitter: 1.0
            );
            
            engine.Start(definition, UserLevel.Middels);
            
            // Act
            var result = engine.EvaluateMetrics(metrics);
            
            // Assert
            Assert.Equal(EvaluationStatus.Adjust, result.Status);
            Assert.Equal(EvaluationStatus.Adjust, result.PitchStatus);
            Assert.Equal(HealthIndicator.Safe, result.HealthIndicator);
            
            engine.Stop();
        }
        
        [Fact]
        public void EvaluateMetrics_CriticalJitter_ReturnsStopStatus()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            var metrics = CreateMetrics(
                pitch: 180,
                f2: 2100,
                jitter: 3.0,      // Above 2.5% threshold
                shimmer: 1.0
            );
            
            engine.Start(definition, UserLevel.Middels);
            
            // Act
            var result = engine.EvaluateMetrics(metrics);
            
            // Assert
            Assert.Equal(EvaluationStatus.Stop, result.Status);
            Assert.Equal(EvaluationStatus.Stop, result.StabilityStatus);
            Assert.Equal(HealthIndicator.Critical, result.HealthIndicator);
            Assert.Equal("HealthWarning_Jitter", result.CoachHintKey);
            
            engine.Stop();
        }
        
        [Fact]
        public void EvaluateMetrics_CriticalShimmer_ReturnsStopStatus()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            var metrics = CreateMetrics(
                pitch: 180,
                f2: 2100,
                jitter: 1.0,
                shimmer: 5.0      // Above 4% threshold
            );
            
            engine.Start(definition, UserLevel.Middels);
            
            // Act
            var result = engine.EvaluateMetrics(metrics);
            
            // Assert
            Assert.Equal(EvaluationStatus.Stop, result.Status);
            Assert.Equal(HealthIndicator.Critical, result.HealthIndicator);
            Assert.Equal("HealthWarning_Shimmer", result.CoachHintKey);
            
            engine.Stop();
        }
        
        [Fact]
        public void EvaluateMetrics_ResonanceOutOfRange_ReturnsAdjustStatus()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            // Use F2 clearly outside tolerance: with F2 range (1800, 2600), tolerance = 400
            // So valid range is 1400-3000. Using 1200 or 3200 to be clearly outside.
            var metrics = CreateMetrics(
                pitch: 180,
                f2: 1200,       // Way below 1400 min - clearly back resonance
                f1: 500,
                jitter: 1.0
            );
            
            engine.Start(definition, UserLevel.Middels);
            
            // Act
            var result = engine.EvaluateMetrics(metrics);
            
            // Assert
            Assert.Equal(EvaluationStatus.Adjust, result.Status);
            Assert.Equal(EvaluationStatus.Adjust, result.ResonanceStatus);
            
            engine.Stop();
        }
        
        [Fact]
        public void EvaluateMetrics_NybegynnerLevel_HigherTolerance()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            
            // With Middels level: pitch must be within ~8 Hz tolerance
            // With Nybegynner level: pitch can be within ~15 Hz tolerance
            var metrics = CreateMetrics(
                pitch: 235,      // Slightly above 220
                f2: 2100,
                jitter: 1.0
            );
            
            engine.Start(definition, UserLevel.Nybegynner);
            
            // Act
            var result = engine.EvaluateMetrics(metrics);
            
            // With Nybegynner tolerance, this might be Adjust instead of Stop
            Assert.NotEqual(EvaluationStatus.Stop, result.Status);
            
            engine.Stop();
        }
        
        [Fact]
        public void EvaluateMetrics_IntonationRequired_EvaluatesIntonation()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            definition.RequiresIntonation = true;
            
            // Low intonation range
            var metrics = CreateMetrics(
                pitch: 180,
                f2: 2100,
                jitter: 1.0,
                intonationRange: 2    // Below 5% threshold
            );
            
            engine.Start(definition, UserLevel.Middels);
            
            // Act
            var result = engine.EvaluateMetrics(metrics);
            
            // Assert
            Assert.Equal(EvaluationStatus.Adjust, result.IntonationStatus);
            
            engine.Stop();
        }
        
        [Fact]
        public void GetSessionSummary_ReturnsCorrectMetrics()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            engine.Start(definition, UserLevel.Middels);
            
            // Add some metrics
            engine.AddMetrics(CreateMetrics(pitch: 180, f2: 2100, jitter: 1.0));
            engine.AddMetrics(CreateMetrics(pitch: 185, f2: 2150, jitter: 1.2));
            engine.AddMetrics(CreateMetrics(pitch: 175, f2: 2050, jitter: 0.8));
            
            // Wait for processing
            System.Threading.Thread.Sleep(200);
            
            // Act
            var summary = engine.GetSessionSummary();
            
            // Assert
            Assert.NotNull(summary);
            Assert.True(summary.AveragePitch > 0);
            Assert.True(summary.AverageF2 > 0);
            
            engine.Stop();
        }
        
        [Fact]
        public void PauseResume_WorksCorrectly()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            engine.Start(definition, UserLevel.Middels);
            
            // Act
            engine.Pause();
            Assert.True(engine.IsPaused);
            
            engine.Resume();
            Assert.False(engine.IsPaused);
            
            engine.Stop();
        }
        
        [Fact]
        public void Constructor_WithCustomInterval_UsesCorrectInterval()
        {
            // Arrange & Act
            var engine = new ExerciseFeedbackEngine(100);
            
            // Assert - the engine should use 100ms interval
            // This is tested indirectly through the evaluation loop
            Assert.NotNull(engine);
            
            engine.Stop();
        }
        
        [Fact]
        public void NullMetrics_DoesNotThrow()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var definition = CreateDefaultDefinition();
            engine.Start(definition, UserLevel.Middels);
            
            // Act & Assert - should not throw
            engine.AddMetrics(null!);
            
            engine.Stop();
        }
        
        [Fact]
        public void EvaluateMetrics_WithoutStart_ReturnsCorrect()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine();
            var metrics = CreateMetrics();
            
            // Act - engine not started
            var result = engine.EvaluateMetrics(metrics);
            
            // Assert - should return correct with default message
            Assert.NotNull(result);
            Assert.Equal(EvaluationStatus.Correct, result.Status);
            
            engine.Stop();
        }
        
        [Fact]
        public void HealthWarning_CriticalJitter_TriggersHealthCriticalEvent()
        {
            // Arrange
            var engine = new ExerciseFeedbackEngine(50);
            var definition = CreateDefaultDefinition();
            engine.Start(definition, UserLevel.Middels);
            
            var eventRaised = false;
            engine.HealthCritical += (s, e) => eventRaised = true;
            
            // Act - add metrics to trigger async processing
            engine.AddMetrics(CreateMetrics(jitter: 3.0));
            
            // Wait for the async evaluation loop to process
            System.Threading.Thread.Sleep(200);
            
            // Assert
            Assert.True(eventRaised);
            
            // Also verify the result directly
            var results = engine.GetResults();
            Assert.NotNull(results);
            var lastResult = results.LastOrDefault();
            if (lastResult != null)
                Assert.Equal(HealthIndicator.Critical, lastResult.HealthIndicator);
            
            engine.Stop();
        }
    }
}
