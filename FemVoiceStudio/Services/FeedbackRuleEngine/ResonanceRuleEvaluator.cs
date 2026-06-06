using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services.FeedbackRuleEngine
{
    /// <summary>
    /// Evaluates resonance/formant parameters against exercise targets.
    /// Implements clinical principle: Resonance-first hierarchy.
    /// F2 is primary indicator - if F2 &lt; 1400 Hz, resonance improvement comes before pitch adjustment.
    /// </summary>
    public class ResonanceRuleEvaluator : IRuleEvaluator
    {
        #region Constants

        // Clinical thresholds for resonance
        private const double F2BackResonance = 1400.0;    // Below this = back resonance (needs improvement)
        private const double F2Neutral = 1800.0;          // Above this = neutral/good
        private const double F2Optimal = 1800.0;          // Optimal target
        private const double F2AdvancedTarget = 2000.0;   // Advanced target
        
        private const double F1OptimalMin = 400.0;
        private const double F1OptimalMax = 700.0;
        
        private const double SpectralCentroidThreshold = 2000.0; // Above = bright tone
        
        // Tolerance by user level (Hz)
        private const double BeginnerTolerance = 200.0;
        private const double IntermediateTolerance = 100.0;
        private const double AdvancedTolerance = 50.0;

        #endregion

        public EvaluationStatus Evaluate(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel)
        {
            // Need valid F2 to evaluate
            if (metrics.F2 <= 0)
                return EvaluationStatus.NotApplicable;

            double tolerance = GetTolerance(userLevel);
            double effectiveF2Min = definition.TargetF2Range.Min - tolerance;
            double effectiveF2Max = definition.TargetF2Range.Max + tolerance;

            // Primary: Check F2 in range
            bool isF2InRange = metrics.F2 >= effectiveF2Min && metrics.F2 <= effectiveF2Max;
            
            // Secondary: Check F1 in optimal range (if available)
            bool isF1InRange = metrics.F1 > 0 && 
                              metrics.F1 >= F1OptimalMin && 
                              metrics.F1 <= F1OptimalMax;
            
            // Check spectral centroid (brightness)
            bool isBrightTone = metrics.SpectralCentroid >= SpectralCentroidThreshold;

            // Clinical logic: Resonance-first hierarchy
            // If F2 is back (&lt;1400), this is the priority issue
            
            if (metrics.F2 < F2BackResonance)
            {
                // Critical: Back resonance - STOP any pitch increases
                return EvaluationStatus.Stop;
            }
            
            if (metrics.F2 < effectiveF2Min)
            {
                // Need improvement toward target
                return EvaluationStatus.Adjust;
            }
            
            if (isF2InRange)
            {
                // In target range - check if optimal
                if (metrics.F2 >= F2Optimal && isF1InRange)
                {
                    return EvaluationStatus.Correct;
                }
                return EvaluationStatus.Adjust;
            }

            return EvaluationStatus.Adjust;
        }

        public string GetHintKey(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel)
        {
            double tolerance = GetTolerance(userLevel);
            double effectiveF2Min = definition.TargetF2Range.Min - tolerance;

            // Clinical: Resonance-first hierarchy
            
            // Back resonance - most critical
            if (metrics.F2 < F2BackResonance)
            {
                return "CoachHint_Resonance_MoveForward";
            }
            
            // Near target range - encouraging
            if (metrics.F2 >= F2BackResonance && metrics.F2 < effectiveF2Min)
            {
                return "CoachHint_Resonance_AlmostThere";
            }
            
            // In range but not optimal
            if (metrics.F2 >= effectiveF2Min && metrics.F2 < F2Neutral)
            {
                // Check if improving
                return "CoachHint_Resonance_KeepGoing";
            }
            
            // Optimal resonance
            if (metrics.F2 >= F2Optimal)
            {
                // Check F1 and spectral centroid
                if (metrics.F1 >= F1OptimalMin && metrics.F1 <= F1OptimalMax &&
                    metrics.SpectralCentroid >= SpectralCentroidThreshold)
                {
                    return "CoachHint_Resonance_Optimal";
                }
                return "CoachHint_Resonance_Good";
            }

            return "CoachHint_Resonance_Adjust";
        }

        /// <summary>
        /// Get detailed feedback for resonance improvement
        /// </summary>
        public string GetDetailedFeedback(VoiceMetrics metrics, UserLevel userLevel)
        {
            var loc = LocalizationService.Instance;
            
            if (metrics.F2 < F2BackResonance)
            {
                // Very back - give detailed beginner-friendly instruction
                return userLevel == UserLevel.Nybegynner 
                    ? loc["CoachHint_Resonance_MoveForward_Beginner"]
                    : loc["CoachHint_Resonance_MoveForward"];
            }
            
            if (metrics.F2 < F2Neutral)
            {
                return loc["CoachHint_Resonance_AlmostThere"];
            }
            
            return loc["CoachHint_Resonance_Optimal"];
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
