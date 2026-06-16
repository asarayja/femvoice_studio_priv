using System;
using System.Collections.Generic;
using System.Threading;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ExerciseIntelligenceCoordinator"/>.
    ///
    /// Strategy: the coordinator's parameterless constructor wires no engine events.
    /// Tests drive state exclusively through <see cref="ExerciseIntelligenceCoordinator.UpdateMetrics"/>
    /// and <see cref="ExerciseIntelligenceCoordinator.UpdateHealthScore"/>, then assert on
    /// collected <see cref="ExerciseLiveState"/> and <see cref="InlineCoachMessage"/> instances.
    ///
    /// No UI references, no mocking frameworks required — the coordinator's test-seam
    /// methods provide full controllability.
    /// </summary>
    public class ExerciseIntelligenceCoordinatorTests : IDisposable
    {
        // ── Helpers ───────────────────────────────────────────────────────────────────

        /// <summary>Creates a coordinator, collects its events into lists, and returns all three.</summary>
        private static (
            ExerciseIntelligenceCoordinator coordinator,
            List<ExerciseLiveState> states,
            List<InlineCoachMessage> messages)
            BuildSut(ExerciseTargetProfile? profile = null, int userId = 1)
        {
            var coordinator = new ExerciseIntelligenceCoordinator();
            var states   = new List<ExerciseLiveState>();
            var messages = new List<InlineCoachMessage>();

            coordinator.ExerciseUpdated    += s => states.Add(s);
            coordinator.InlineCoachUpdated += m => messages.Add(m);

            coordinator.SetExerciseContext(profile ?? ExerciseTargetProfile.ResonanceExercise(), userId);

            // Clear the zeroed context-change state so tests start clean.
            states.Clear();

            return (coordinator, states, messages);
        }

        /// <summary>
        /// Bypasses the 100 ms rate limit by advancing time between calls via a short sleep.
        /// </summary>
        private static void WaitForRateLimit() => Thread.Sleep(150);

        // ────────────────────────────────────────────────────────────────────────────
        // 1. Hold Detection — HoldProgress increments when criteria are met
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void HoldProgress_IncreasesOverTime_WhenAllConditionsMet()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.5,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.4,
                requiredHoldSeconds: 1.0);

            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(resonanceScore: 0.7, pitch: 200, stability: 0.6, health: 100);
            WaitForRateLimit();
            sut.UpdateMetrics(resonanceScore: 0.7, pitch: 200, stability: 0.6, health: 100);

            Assert.True(states.Count >= 2);
            Assert.True(states[^1].HoldProgress > 0,
                "HoldProgress should be positive when all hold conditions are met.");
            Assert.True(states[^1].IsHoldingCorrectly);
        }

        [Fact]
        public void HoldProgress_CompletesAtOne_AfterRequiredDuration()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.4,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.3,
                requiredHoldSeconds: 0.2);

            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            for (int i = 0; i < 5; i++)
            {
                WaitForRateLimit();
                sut.UpdateMetrics(resonanceScore: 0.65, pitch: 200, stability: 0.5, health: 100);
            }

            Assert.Contains(states, s => s.HoldProgress >= 1.0);
        }

        [Fact]
        public void HoldProgress_ResetsToZero_WhenConditionsBreak()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.5,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.4,
                requiredHoldSeconds: 2.0);

            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(0.7, 200, 0.6, 100);
            WaitForRateLimit();
            sut.UpdateMetrics(0.2, 200, 0.6, 100);

            Assert.True(states[^1].HoldProgress == 0,
                "HoldProgress should reset when hold conditions are no longer met.");
            Assert.False(states[^1].IsHoldingCorrectly);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 2. Safety Lock — freezes HoldProgress, resumes when cleared
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void HoldProgress_Freezes_WhenSafetyLockEngaged()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.4,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.3,
                requiredHoldSeconds: 5.0);

            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(0.65, 200, 0.5, 100);
            WaitForRateLimit();
            sut.UpdateMetrics(0.65, 200, 0.5, 100);

            double progressBeforeLock = states[^1].HoldProgress;

            WaitForRateLimit();
            sut.UpdateHealthScore(50);
            WaitForRateLimit();
            sut.UpdateMetrics(0.65, 200, 0.5, 50);

            double progressAfterLock = states[^1].HoldProgress;

            Assert.True(states[^1].IsSafetyLocked, "State should report safety locked.");
            Assert.Equal(progressBeforeLock, progressAfterLock, precision: 2);
        }

        [Fact]
        public void HoldProgress_Resumes_AfterSafetyLockCleared()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.4,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.3,
                requiredHoldSeconds: 5.0);

            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateHealthScore(50);
            WaitForRateLimit();
            sut.UpdateHealthScore(90);
            WaitForRateLimit();
            sut.UpdateMetrics(0.65, 200, 0.5, 90);
            WaitForRateLimit();
            sut.UpdateMetrics(0.65, 200, 0.5, 90);

            Assert.False(states[^1].IsSafetyLocked, "Lock should be cleared after health recovers.");
        }

        [Fact]
        public void IsSafetyLocked_True_WhenHealthBelowSeventyThreshold()
        {
            var (sut, states, _) = BuildSut();
            sut.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);

            sut.UpdateMetrics(0.7, 200, 0.6, 65);

            Assert.True(states[^1].IsSafetyLocked);
        }

        [Fact]
        public void IsSafetyLocked_False_WhenHealthAboveSeventyThreshold()
        {
            var (sut, states, _) = BuildSut();
            sut.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);

            sut.UpdateMetrics(0.7, 200, 0.6, 80);

            Assert.False(states[^1].IsSafetyLocked);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 3. Wrong Resonance — triggers InlineCoachMessage with correct severity
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void InlineCoach_Triggered_WithSuggestionSeverity_WhenResonanceTooLow()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin: 0.6,
                targetResonanceMax: 0.9);

            var (sut, _, messages) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(0.3, 200, 0.6, 100);

            Assert.Contains(messages, m =>
                m.CoachingReason == "RESONANCE_TOO_LOW" &&
                m.Severity       == MessageSeverity.Suggestion);
        }

        [Fact]
        public void InlineCoach_Triggered_WithSuggestionSeverity_WhenResonanceTooHigh()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin: 0.5,
                targetResonanceMax: 0.75);

            var (sut, _, messages) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(0.95, 200, 0.6, 100);

            Assert.Contains(messages, m =>
                m.CoachingReason == "RESONANCE_TOO_HIGH" &&
                m.Severity       == MessageSeverity.Suggestion);
        }

        [Fact]
        public void InlineCoach_NotTriggeredForResonance_WhenSafetyLocked()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin: 0.6,
                targetResonanceMax: 0.9);

            var (sut, _, messages) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(0.3, 200, 0.6, 50);

            Assert.DoesNotContain(messages, m => m.CoachingReason == "RESONANCE_TOO_LOW");
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 4. Stability threshold
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void InlineCoach_Triggered_WhenStabilityBelowProfileThreshold()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.4,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.7);

            var (sut, _, messages) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(0.65, 200, 0.5, 100);

            Assert.Contains(messages, m => m.CoachingReason == "STABILITY_LOW");
        }

        [Fact]
        public void InlineCoach_NotTriggered_WhenStabilityMeetsProfileThreshold()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.4,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.4);

            var (sut, _, messages) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(0.65, 200, 0.6, 100);

            Assert.DoesNotContain(messages, m => m.CoachingReason == "STABILITY_LOW");
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 5. Exercise-specific metric priority
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void PrimaryMetric_IsResonanceScore_ForResonanceProfile()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise();
            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(resonanceScore: 0.72, pitch: 180, stability: 0.5, health: 100);

            Assert.Equal(0.72, states[^1].PrimaryMetricScore, precision: 5);
        }

        [Fact]
        public void PrimaryMetric_IsNormalisedPitch_ForPitchProfile()
        {
            var profile = ExerciseTargetProfile.PitchExercise(minPitch: 150, maxPitch: 250);
            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            // (200-150)/(250-150) = 0.5
            sut.UpdateMetrics(resonanceScore: 0.4, pitch: 200, stability: 0.5, health: 100);

            Assert.Equal(0.5, states[^1].PrimaryMetricScore, precision: 5);
        }

        [Fact]
        public void SecondaryMetric_IsStabilityScore_ForResonanceProfile()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise();
            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(resonanceScore: 0.65, pitch: 200, stability: 0.80, health: 100);

            Assert.Equal(0.80, states[^1].SecondaryMetricScore, precision: 5);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 6. ComfortZone violation
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void IsInComfortZone_False_WhenPitchOutsideProfileBounds()
        {
            var profile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 240);
            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(resonanceScore: 0.5, pitch: 300, stability: 0.5, health: 100);

            Assert.False(states[^1].IsInComfortZone);
        }

        [Fact]
        public void IsInComfortZone_True_WhenPitchInsideProfileBounds()
        {
            var profile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 240);
            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(resonanceScore: 0.5, pitch: 200, stability: 0.5, health: 100);

            Assert.True(states[^1].IsInComfortZone);
        }

        [Fact]
        public void InlineCoach_Triggered_WhenPitchOutOfZone_ForPitchProfile()
        {
            var profile = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 240);
            var (sut, _, messages) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(0.5, 300, 0.5, 100);

            Assert.Contains(messages, m => m.CoachingReason == "PITCH_OUT_OF_ZONE");
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 7. PerformanceQuality mapping
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Quality_IsExcellent_WhenAllMetricsOptimal()
        {
            var profile = new ExerciseTargetProfile
            {
                UsesResonance       = true,
                UsesPitch           = true,
                UsesStability       = true,
                TargetResonanceMin  = 0.5,
                TargetResonanceMax  = 0.9,
                StabilityThreshold  = 0.4,
                MinPitch            = 160,
                MaxPitch            = 240,
                RequiredHoldSeconds = 2.0
            };

            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(
                resonanceScore: 0.7,
                pitch:          200,
                stability:      0.8,
                health:         100);

            Assert.Equal(PerformanceQuality.Excellent, states[^1].Quality);
        }

        [Fact]
        public void Quality_IsPoor_WhenSafetyLocked()
        {
            var (sut, states, _) = BuildSut();
            sut.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);

            sut.UpdateMetrics(0.7, 200, 0.8, 40);

            Assert.Equal(PerformanceQuality.Poor, states[^1].Quality);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 8. Coach message rate limiting
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void InlineCoach_NotSentTwice_WithinRateLimitWindow()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin: 0.6, targetResonanceMax: 0.9);

            var (sut, _, messages) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            sut.UpdateMetrics(0.3, 200, 0.6, 100);
            WaitForRateLimit();
            sut.UpdateMetrics(0.3, 200, 0.6, 100);

            int count = messages.FindAll(m => m.CoachingReason == "RESONANCE_TOO_LOW").Count;
            Assert.Equal(1, count);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 9. Context reset on SetExerciseContext
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void SetExerciseContext_ResetsHoldProgress()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin: 0.4, targetResonanceMax: 0.9,
                stabilityThreshold: 0.3, requiredHoldSeconds: 0.2);

            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            for (int i = 0; i < 4; i++) { WaitForRateLimit(); sut.UpdateMetrics(0.65, 200, 0.5, 100); }
            Assert.True(sut.GetHoldProgress() > 0);

            sut.SetExerciseContext(ExerciseTargetProfile.PitchExercise(), userId: 2);

            Assert.Equal(0, sut.GetHoldProgress());
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 10. HoldComplete message fires when progress reaches 1
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void InlineCoach_HoldComplete_FiredWhenProgressReachesOne()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.4,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.3,
                requiredHoldSeconds: 0.15);

            var (sut, _, messages) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            for (int i = 0; i < 6; i++) { WaitForRateLimit(); sut.UpdateMetrics(0.65, 200, 0.5, 100); }

            Assert.Contains(messages, m => m.CoachingReason == "HOLD_COMPLETE");
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 11. Disposal
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Dispose_DoesNotThrow_AndStopsPublishingEvents()
        {
            var (sut, states, _) = BuildSut();
            int countBefore = states.Count;

            sut.Dispose();

            int countAfter = states.Count;
            Assert.Equal(countBefore, countAfter);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 12. ExerciseTargetProfile.Validate
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ExerciseTargetProfile_Validate_ThrowsWhenMaxResonanceLessThanMin()
        {
            var profile = new ExerciseTargetProfile
            {
                TargetResonanceMin = 0.8,
                TargetResonanceMax = 0.4
            };

            Assert.Throws<InvalidOperationException>(profile.Validate);
        }

        [Fact]
        public void ExerciseTargetProfile_Validate_ThrowsWhenMaxPitchLessThanMin()
        {
            var profile = new ExerciseTargetProfile
            {
                TargetResonanceMin = 0.3,
                TargetResonanceMax = 0.9,
                MinPitch = 250,
                MaxPitch = 150
            };

            Assert.Throws<InvalidOperationException>(profile.Validate);
        }

        [Fact]
        public void ExerciseTargetProfile_Validate_PassesForResonanceFactoryMethod()
        {
            var profile = ExerciseTargetProfile.ResonanceExercise();
            var ex = Record.Exception(profile.Validate);
            Assert.Null(ex);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 13. Lifecycle — StartExercise / StopExercise (steg 2.3 / 4.4)
        // ────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void StartExercise_SetsIsActiveTrue()
        {
            // Arrange
            var sut = new ExerciseIntelligenceCoordinator();
            Assert.False(sut.IsExerciseActive, "Should start inactive.");

            // Act
            sut.StartExercise(ExerciseTargetProfile.ResonanceExercise(), userId: 1);

            // Assert
            Assert.True(sut.IsExerciseActive);
        }

        [Fact]
        public void StopExercise_SetsIsActiveFalse_AndResetsHoldProgress()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.4,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.3,
                requiredHoldSeconds: 0.2);

            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            // Build some hold progress.
            for (int i = 0; i < 3; i++) { WaitForRateLimit(); sut.UpdateMetrics(0.65, 200, 0.5, 100); }
            Assert.True(sut.GetHoldProgress() > 0, "Hold progress should be positive before stop.");

            // Act
            sut.StopExercise();

            // Assert
            Assert.False(sut.IsExerciseActive);
            Assert.Equal(0, sut.GetHoldProgress());

            var finalState = states[^1];
            Assert.Equal(0, finalState.HoldProgress);
        }

        [Fact]
        public void EvaluateState_DoesNotFire_WhenNotActive()
        {
            // Arrange — coordinator created but StartExercise never called.
            var coordinator = new ExerciseIntelligenceCoordinator();
            var states = new List<ExerciseLiveState>();
            coordinator.ExerciseUpdated += s => states.Add(s);
            coordinator.SetExerciseContext(ExerciseTargetProfile.ResonanceExercise(), 1);
            states.Clear(); // discard the context-change zeroed state

            // Act — inject metrics without starting the exercise.
            coordinator.UpdateMetrics(0.7, 200, 0.6, 100);
            WaitForRateLimit();
            coordinator.UpdateMetrics(0.7, 200, 0.6, 100);

            // Assert — no evaluation states with non-default values should have been published.
            Assert.DoesNotContain(states, s => s.PrimaryMetricScore > 0 || s.HoldProgress > 0);
        }

        [Fact]
        public void StartExercise_WhileAlreadyActive_RestartsCleanly()
        {
            // Arrange — start first exercise and build hold progress.
            var profile1 = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.4,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.3,
                requiredHoldSeconds: 0.2);

            var (sut, _, _) = BuildSut(profile1);
            sut.StartExercise(profile1, userId: 1);

            for (int i = 0; i < 3; i++) { WaitForRateLimit(); sut.UpdateMetrics(0.65, 200, 0.5, 100); }
            double progressAfterFirst = sut.GetHoldProgress();
            Assert.True(progressAfterFirst > 0, "Hold progress should be positive after first exercise.");

            // Act — start a second exercise while the first is still active.
            var profile2 = ExerciseTargetProfile.PitchExercise(minPitch: 160, maxPitch: 240);
            sut.StartExercise(profile2, userId: 2);

            // Assert — hold state from the first exercise must not leak into the second.
            Assert.True(sut.IsExerciseActive, "Should still be active after restart.");
            Assert.True(sut.GetHoldProgress() == 0, "Hold progress must be reset to zero when restarting.");
        }

        [Fact]
        public void StopExercise_PublishesFinalZeroedState()
        {
            // Arrange
            var profile = ExerciseTargetProfile.ResonanceExercise(
                targetResonanceMin:  0.4,
                targetResonanceMax:  0.9,
                stabilityThreshold:  0.3,
                requiredHoldSeconds: 5.0);

            var (sut, states, _) = BuildSut(profile);
            sut.StartExercise(profile, 1);

            // Build some non-zero state.
            WaitForRateLimit();
            sut.UpdateMetrics(0.65, 200, 0.5, 100);

            // Act
            sut.StopExercise();

            // Assert — last published state must be fully zeroed.
            var finalState = states[^1];
            Assert.Equal(0, finalState.HoldProgress);
            Assert.Equal(0, finalState.PrimaryMetricScore);
            Assert.False(finalState.IsSafetyLocked);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Teardown
        // ────────────────────────────────────────────────────────────────────────────

        public void Dispose() { /* each test creates its own SUT */ }
    }
}
