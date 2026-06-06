using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services.FeedbackRuleEngine
{
    /// <summary>
    /// Evaluates pitch parameters against exercise targets.
    /// Includes pitch press detection and stability evaluation.
    /// </summary>
    public class PitchRuleEvaluator : IRuleEvaluator
    {
        #region Constants

        private const double PitchPressThreshold = 180.0;
        private const double StabilityVariationThreshold = 25.0; // Hz
        
        // Tolerance by user level (Hz)
        private const double BeginnerTolerance = 10.0;
        private const double IntermediateTolerance = 7.0;
        private const double AdvancedTolerance = 5.0;

        #endregion

        public EvaluationStatus Evaluate(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel)
        {
            if (metrics.Pitch <= 0)
                return EvaluationStatus.NotApplicable;

            // Get effective tolerance based on user level
            double tolerance = GetTolerance(userLevel);
            double targetMin = definition.TargetPitchRange.Min - tolerance;
            double targetMax = definition.TargetPitchRange.Max + tolerance;

            // Check if pitch is in range
            bool isInRange = metrics.Pitch >= targetMin && metrics.Pitch <= targetMax;
            
            // Check for pitch press (high pitch + instability)
            bool isPitchPress = metrics.Pitch > PitchPressThreshold && 
                               (metrics.Jitter > 1.5 || metrics.IsPitchPress);

            // Check for instability (high variation)
            bool isUnstable = metrics.PitchVariation > StabilityVariationThreshold;

            // Priority 1: Pitch press = Stop
            if (isPitchPress)
            {
                return EvaluationStatus.Stop;
            }

            // Priority 2: High instability = Adjust
            if (isUnstable)
            {
                return EvaluationStatus.Adjust;
            }

            // Otherwise check if in target range
            return isInRange ? EvaluationStatus.Correct : EvaluationStatus.Adjust;
        }

        public string GetHintKey(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel)
        {
            double tolerance = GetTolerance(userLevel);
            double targetMin = definition.TargetPitchRange.Min - tolerance;
            double targetMax = definition.TargetPitchRange.Max + tolerance;

            // Pitch press detection
            if (metrics.Pitch > PitchPressThreshold && (metrics.Jitter > 1.5 || metrics.IsPitchPress))
            {
                return "CoachHint_PitchPress_Rest";
            }

            // High instability
            if (metrics.PitchVariation > StabilityVariationThreshold)
            {
                return "CoachHint_Pitch_Stabilize";
            }

            // Below target
            if (metrics.Pitch < targetMin)
            {
                return "CoachHint_Pitch_TooLow";
            }

            // Above target
            if (metrics.Pitch > targetMax)
            {
                return "CoachHint_Pitch_TooHigh";
            }

            // In range - good
            return "CoachHint_Pitch_Good";
        }

        private double GetTolerance(UserLevel userLevel)
        {
            return userLevel switch
            {
                UserLevel.Nybegynner => BeginnerTolerance,
                UserLevel.Middels => IntermediateTolerance,
                UserLevel.Avansert => AdvancedTolerance,
                _ => IntermediateTolerance
            };
        }
    }
}
