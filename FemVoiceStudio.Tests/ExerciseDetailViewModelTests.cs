using System.Threading;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class ExerciseDetailViewModelTests
    {
        private static ExerciseDetailViewModel CreateViewModel()
            => new(
                new ExerciseIntelligenceCoordinator(),
                LocalizationService.Instance,
                new ExerciseProfileFactory());

        [Fact]
        public void ApplyProfile_HidesPitchDirection_WhenPitchIsNotPrimaryFeedback()
        {
            using var viewModel = CreateViewModel();

            viewModel.ApplyProfile(ExerciseTargetProfile.CreateResonanceHumming());

            Assert.True(viewModel.ShowResonanceBar);
            Assert.False(viewModel.ShowPitchDirection);
            Assert.Equal(0.0, viewModel.PitchDirectionValue);
        }

        [Fact]
        public void ApplyProfile_ShowsPitchDirection_ForPitchPrimaryProfiles()
        {
            using var viewModel = CreateViewModel();

            viewModel.ApplyProfile(ExerciseTargetProfile.CreateCoordinatedGlideUp());

            Assert.True(viewModel.ShowPitchDirection);
        }

        [Fact]
        public void ResonanceStatus_UsesAdaptiveProfileRange()
        {
            using var viewModel = CreateViewModel();
            viewModel.ApplyProfile(ExerciseTargetProfile.CreateResonanceHumming(
                targetResonanceMin: 0.70,
                targetResonanceMax: 0.85,
                stabilityThreshold: 0.45));
            viewModel.StartExerciseCommand.Execute(null);

            viewModel.UpdateLiveMetrics(resonanceScore: 0.60, pitch: 0, stability: 0.60, health: 100);
            Assert.Equal("LiveFeedback_OnTrack", viewModel.ResonanceStatusKey);

            Thread.Sleep(120);
            viewModel.UpdateLiveMetrics(resonanceScore: 0.75, pitch: 0, stability: 0.60, health: 100);
            Assert.Equal("LiveFeedback_Good", viewModel.ResonanceStatusKey);

            Thread.Sleep(120);
            viewModel.UpdateLiveMetrics(resonanceScore: 0.95, pitch: 0, stability: 0.60, health: 100);
            Assert.Equal("LiveFeedback_Smoother", viewModel.ResonanceStatusKey);
        }

        [Fact]
        public void PitchStatus_ReportsComfortZoneWithoutRawHz()
        {
            using var viewModel = CreateViewModel();
            viewModel.ApplyProfile(ExerciseTargetProfile.PitchExercise(minPitch: 120, maxPitch: 220));
            viewModel.StartExerciseCommand.Execute(null);

            viewModel.UpdateLiveMetrics(resonanceScore: 0, pitch: 170, stability: 0.70, health: 100);

            Assert.True(viewModel.ShowPitchDirection);
            Assert.Equal(1.0, viewModel.PitchDirectionValue);
            Assert.Equal("Shield_ComfortZoneOk", viewModel.PitchStatusKey);
        }

        [Fact]
        public void LiveCompositeScore_CombinesResonanceStabilityAndHold()
        {
            using var viewModel = CreateViewModel();
            viewModel.ApplyProfile(ExerciseTargetProfile.CreateResonanceHumming(
                targetResonanceMin: 0.50,
                targetResonanceMax: 0.85,
                stabilityThreshold: 0.45,
                requiredHoldSeconds: 3));
            viewModel.StartExerciseCommand.Execute(null);

            viewModel.UpdateLiveMetrics(resonanceScore: 0.80, pitch: 0, stability: 0.70, health: 100);

            Assert.InRange(viewModel.LiveCompositeScorePercent, 60, 100);
            Assert.Equal("LiveFeedback_OnTrack", viewModel.ClinicalLoopStatusKey);
        }

        [Fact]
        public void ClinicalLoopStatus_PrioritizesSafetyBeforeProgression()
        {
            using var viewModel = CreateViewModel();
            viewModel.ApplyProfile(ExerciseTargetProfile.CreateResonanceHumming());
            viewModel.StartExerciseCommand.Execute(null);

            viewModel.UpdateLiveMetrics(resonanceScore: 0.85, pitch: 0, stability: 0.90, health: 50);

            Assert.Equal("Shield_SafetyLocked", viewModel.ClinicalLoopStatusKey);
            Assert.Equal("ProgressionFeedback_Paused", viewModel.ProgressExplanationKey);
            Assert.InRange(viewModel.LiveCompositeScorePercent, 0, 35);
        }
    }
}
