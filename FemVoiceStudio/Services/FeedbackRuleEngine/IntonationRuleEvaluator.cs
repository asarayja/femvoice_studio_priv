using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services.FeedbackRuleEngine
{
    /// <summary>
    /// Evaluates intonation/prosody parameters.
    /// Checks pitch range (30-120 Hz ideal) and intonation patterns (rising/falling).
    /// </summary>
    public class IntonationRuleEvaluator : IRuleEvaluator
    {
        #region Constants

        // Clinical thresholds for intonation
        private const double FlatIntonationThreshold = 30.0;  // Below = monoton
        private const double MinIdealRange = 30.0;             // Minimum for natural variation
        private const double MaxIdealRange = 120.0;            // Maximum for natural variation
        private const double RisingThreshold = 0.2;            // Intonation rise score for questions
        
        // Pattern classification thresholds
        private const double RisingSlopeThreshold = 0.05;      // 5% rise = rising pattern
        private const double FallingSlopeThreshold = -0.05;    // 5% fall = falling pattern

        #endregion

        public EvaluationStatus Evaluate(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel)
        {
            // Check if exercise requires intonation evaluation
            if (!definition.RequiresIntonation)
                return EvaluationStatus.NotApplicable;

            // Need valid pitch data
            if (metrics.Pitch <= 0 || metrics.IntonationRange <= 0)
                return EvaluationStatus.NotApplicable;

            // Check for flat/monoton intonation
            if (metrics.IntonationRange < FlatIntonationThreshold)
            {
                return EvaluationStatus.Adjust;
            }

            // Check for natural range
            bool isInNaturalRange = metrics.IntonationRange >= MinIdealRange && 
                                   metrics.IntonationRange <= MaxIdealRange;

            // For question exercises, check rising intonation
            if (definition.GoalCategory == GoalCategory.Intonation)
            {
                // Need at least some variation
                if (metrics.IntonationRange < FlatIntonationThreshold)
                {
                    return EvaluationStatus.Adjust;
                }
                
                // Natural range with appropriate pattern
                if (isInNaturalRange)
                {
                    return EvaluationStatus.Correct;
                }
                
                return EvaluationStatus.Adjust;
            }

            // General intonation check
            if (isInNaturalRange)
            {
                return EvaluationStatus.Correct;
            }
            
            if (metrics.IntonationRange > MaxIdealRange)
            {
                // Too much variation - might be exaggerated
                return EvaluationStatus.Adjust;
            }

            return EvaluationStatus.Adjust;
        }

        public string GetHintKey(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel)
        {
            if (!definition.RequiresIntonation)
                return "";

            // Flat/monoton
            if (metrics.IntonationRange < FlatIntonationThreshold)
            {
                return "CoachHint_Intonation_AddVariation";
            }

            // Check pattern for question exercises
            if (definition.GoalCategory == GoalCategory.Intonation)
            {
                // For question exercises, need rising intonation
                if (metrics.IntonationPattern == IntonationPattern.Rising ||
                    metrics.IntonationRiseScore > RisingThreshold)
                {
                    return "CoachHint_Intonation_Rising";
                }
                
                if (metrics.IntonationRange >= MinIdealRange && metrics.IntonationRange <= MaxIdealRange)
                {
                    return "CoachHint_Intonation_Natural";
                }
            }

            // General natural intonation
            if (metrics.IntonationRange >= MinIdealRange && metrics.IntonationRange <= MaxIdealRange)
            {
                return "CoachHint_Intonation_Natural";
            }

            if (metrics.IntonationRange > MaxIdealRange)
            {
                return "CoachHint_Intonation_ReduceExaggeration";
            }

            return "CoachHint_Intonation_AddVariation";
        }

        /// <summary>
        /// Get intonation pattern description
        /// </summary>
        public string GetPatternDescription(VoiceMetrics metrics)
        {
            return metrics.IntonationPattern switch
            {
                IntonationPattern.Flat => "Monoton / flat",
                IntonationPattern.Rising => "Rising (question-like)",
                IntonationPattern.Falling => "Falling (statement-like)",
                IntonationPattern.Natural => "Natural variation",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Get feedback for improving intonation
        /// </summary>
        public string GetImprovementHint(VoiceMetrics metrics, ExerciseDefinition definition)
        {
            var loc = LocalizationService.Instance;

            if (metrics.IntonationRange < FlatIntonationThreshold)
            {
                return loc["CoachHint_Intonation_AddVariation"];
            }

            if (definition.GoalCategory == GoalCategory.Intonation)
            {
                // Question exercise
                if (metrics.IntonationPattern != IntonationPattern.Rising &&
                    metrics.IntonationRiseScore < RisingThreshold)
                {
                    return loc["CoachHint_Intonation_RiseForQuestion"];
                }
            }

            return loc["CoachHint_Intonation_Natural"];
        }
    }
}
