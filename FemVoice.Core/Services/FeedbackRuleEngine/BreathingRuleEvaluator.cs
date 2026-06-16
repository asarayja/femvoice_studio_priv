using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services.FeedbackRuleEngine
{
    /// <summary>
    /// Evaluates breathing/intensity parameters.
    /// Checks RMS intensity variance and detects air starvation.
    /// </summary>
    public class BreathingRuleEvaluator : IRuleEvaluator
    {
        #region Constants

        // Intensity thresholds (normalized 0-1)
        private const double IntensityTooWeak = 0.01;     // Too quiet
        private const double IntensityTooLoud = 0.8;       // Too loud
        private const double IntensityOptimalMin = 0.1;    // Minimum for good volume
        private const double IntensityOptimalMax = 0.6;    // Maximum for comfortable speaking
        
        // Variance thresholds
        private const double VarianceGood = 0.05;         // Good breath control
        private const double VarianceModerate = 0.15;      // Moderate control
        private const double VariancePoor = 0.25;         // Poor control
        
        // Air starvation
        private const double AirStarvationThreshold = 0.30; // 30% decline

        #endregion

        // Track intensity over time for air starvation detection
        private double _peakIntensity;
        private int _samplesInPhrase;

        public EvaluationStatus Evaluate(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel)
        {
            // Check if this is a breathing exercise
            if (definition.GoalCategory != GoalCategory.Breathing)
                return EvaluationStatus.NotApplicable;

            // Need valid intensity
            if (metrics.Intensity <= 0)
                return EvaluationStatus.NotApplicable;

            // Track peak intensity
            if (metrics.Intensity > _peakIntensity)
            {
                _peakIntensity = metrics.Intensity;
            }
            _samplesInPhrase++;

            // Check for air starvation (declining intensity toward end of phrase)
            bool isAirStarved = CheckAirStarvation(metrics);
            if (isAirStarved)
            {
                return EvaluationStatus.Adjust;
            }

            // Check intensity range
            if (metrics.Intensity < IntensityTooWeak)
            {
                return EvaluationStatus.Adjust;
            }

            if (metrics.Intensity > IntensityTooLoud)
            {
                return EvaluationStatus.Adjust;
            }

            // Check variance (breath control)
            if (metrics.IntensityVariance > VariancePoor)
            {
                return EvaluationStatus.Adjust;
            }

            // Good control
            if (metrics.IntensityVariance <= VarianceGood &&
                metrics.Intensity >= IntensityOptimalMin &&
                metrics.Intensity <= IntensityOptimalMax)
            {
                return EvaluationStatus.Correct;
            }

            return EvaluationStatus.Adjust;
        }

        public string GetHintKey(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel)
        {
            if (definition.GoalCategory != GoalCategory.Breathing)
                return "";

            // Check air starvation first
            if (CheckAirStarvation(metrics))
            {
                return "CoachHint_Breathing_DeepBreath";
            }

            // Check intensity
            if (metrics.Intensity < IntensityTooWeak)
            {
                return "CoachHint_Breathing_SpeakLouder";
            }

            if (metrics.Intensity > IntensityTooLoud)
            {
                return "CoachHint_Breathing_SpeakSofter";
            }

            // Check variance/control
            if (metrics.IntensityVariance > VariancePoor)
            {
                return "CoachHint_Breathing_Smoother";
            }

            if (metrics.IntensityVariance <= VarianceGood)
            {
                return "CoachHint_Breathing_Good";
            }

            return "CoachHint_Breathing_Adjust";
        }

        /// <summary>
        /// Check if intensity is declining (air starvation)
        /// </summary>
        private bool CheckAirStarvation(VoiceMetrics metrics)
        {
            if (_peakIntensity <= 0 || _samplesInPhrase < 10)
                return false;

            // Calculate current decline from peak
            double declineRatio = (_peakIntensity - metrics.Intensity) / _peakIntensity;
            return declineRatio > AirStarvationThreshold;
        }

        /// <summary>
        /// Reset for new phrase/session
        /// </summary>
        public void ResetPhrase()
        {
            _peakIntensity = 0;
            _samplesInPhrase = 0;
        }

        /// <summary>
        /// Get detailed breathing feedback
        /// </summary>
        public string GetDetailedFeedback(VoiceMetrics metrics, UserLevel userLevel)
        {
            var loc = LocalizationService.Instance;

            if (CheckAirStarvation(metrics))
            {
                return loc["CoachHint_Breathing_DeepBreath"];
            }

            if (metrics.Intensity < IntensityOptimalMin)
            {
                return loc["CoachHint_Breathing_ProjectMore"];
            }

            if (metrics.Intensity > IntensityOptimalMax)
            {
                return loc["CoachHint_Breathing_Relax"];
            }

            if (metrics.IntensityVariance > VarianceGood)
            {
                return loc["CoachHint_Breathing_Smoother"];
            }

            return loc["CoachHint_Breathing_Good"];
        }
    }
}
