using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Services.FeedbackRuleEngine;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class FeedbackSignalPolicyTests
    {
        [Fact]
        public void RealtimeFeedback_NoVoiceUsesSignalCalibrationCue()
        {
            LocalizationService.Instance.SetLanguage("nb-NO");
            var service = new FeedbackService();

            var message = service.GetRealtimeFeedback(
                new PitchAnalysisResult
                {
                    IsVoiced = false,
                    RmsValue = 0.001,
                    Confidence = 0
                },
                targetMinPitch: 160,
                targetMaxPitch: 240);

            Assert.Contains("Signalet er svakt", message);
            Assert.Contains("mikrofonkalibrering", message);
            Assert.DoesNotContain("snakk høyere", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RealtimeFeedback_BelowZoneReferencesGraphTargetWithoutPressure()
        {
            LocalizationService.Instance.SetLanguage("nb-NO");
            var service = new FeedbackService();

            var message = service.GetRealtimeFeedback(
                new PitchAnalysisResult
                {
                    IsVoiced = true,
                    Pitch = 130,
                    RmsValue = 0.02,
                    Confidence = 1
                },
                targetMinPitch: 160,
                targetMaxPitch: 230);

            Assert.Contains("Under målsonen", message);
            Assert.Contains("lavere/mørkere", message);
            Assert.Contains("grønne sonen 160-230 Hz", message);
            Assert.Contains("komfortabelt", message);
            Assert.DoesNotContain("snakk høyere", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("du må høyere", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RealtimeFeedback_InZoneKeepsResonanceFocus()
        {
            LocalizationService.Instance.SetLanguage("nb-NO");
            var service = new FeedbackService();

            var message = service.GetRealtimeFeedback(
                new PitchAnalysisResult
                {
                    IsVoiced = true,
                    Pitch = 190,
                    RmsValue = 0.02,
                    Confidence = 1
                },
                targetMinPitch: 175,
                targetMaxPitch: 240);

            Assert.Contains("Innenfor målsonen", message);
            Assert.Contains("komfortabel", message);
            Assert.Contains("resonans", message);
        }

        [Fact]
        public void RealtimeFeedback_AboveZoneWarnsAgainstPressure()
        {
            LocalizationService.Instance.SetLanguage("nb-NO");
            var service = new FeedbackService();

            var message = service.GetRealtimeFeedback(
                new PitchAnalysisResult
                {
                    IsVoiced = true,
                    Pitch = 260,
                    RmsValue = 0.02,
                    Confidence = 1
                },
                targetMinPitch: 175,
                targetMaxPitch: 240);

            Assert.Contains("Over målsonen", message);
            Assert.Contains("høyere/lysere", message);
            Assert.Contains("unngå press", message);
            Assert.DoesNotContain("du må høyere", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void BreathingLowIntensityHint_UsesSignalComfortCue()
        {
            LocalizationService.Instance.SetLanguage("nb-NO");
            var evaluator = new BreathingRuleEvaluator();
            var definition = new ExerciseDefinition { GoalCategory = GoalCategory.Breathing };
            var metrics = new VoiceMetrics
            {
                Intensity = 0.005,
                IntensityVariance = 0.01
            };

            var key = evaluator.GetHintKey(metrics, definition, UserLevel.Nybegynner);
            var message = LocalizationService.Instance[key];

            Assert.Equal("CoachHint_Breathing_SpeakLouder", key);
            Assert.Contains("Signalet er lavt", message);
            Assert.Contains("komfortabel lyd", message);
            Assert.DoesNotContain("snakk høyere", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("prosjekter", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void BreathingDetailedLowIntensityFeedback_DoesNotAskForProjection()
        {
            LocalizationService.Instance.SetLanguage("nb-NO");
            var evaluator = new BreathingRuleEvaluator();
            var message = evaluator.GetDetailedFeedback(
                new VoiceMetrics
                {
                    Intensity = 0.05,
                    IntensityVariance = 0.01
                },
                UserLevel.Nybegynner);

            Assert.Contains("uten ekstra kraft", message);
            Assert.DoesNotContain("prosjekter", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("snakk høyere", message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
