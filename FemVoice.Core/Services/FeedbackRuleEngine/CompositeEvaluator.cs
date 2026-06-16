using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services.FeedbackRuleEngine
{
    /// <summary>
    /// Composite evaluator that aggregates results from all parameter evaluators.
    /// Priority order: Stop > Adjust > Correct
    /// </summary>
    public class CompositeEvaluator
    {
        private readonly PitchRuleEvaluator _pitchEvaluator;
        private readonly ResonanceRuleEvaluator _resonanceEvaluator;
        private readonly IntonationRuleEvaluator _intonationEvaluator;
        private readonly BreathingRuleEvaluator _breathingEvaluator;

        public CompositeEvaluator()
        {
            _pitchEvaluator = new PitchRuleEvaluator();
            _resonanceEvaluator = new ResonanceRuleEvaluator();
            _intonationEvaluator = new IntonationRuleEvaluator();
            _breathingEvaluator = new BreathingRuleEvaluator();
        }

        /// <summary>
        /// Evaluate all parameters and return combined result.
        /// Priority: Health > Pitch > Resonance > Stability > Intonation
        /// </summary>
        public ExerciseEvaluationResult Evaluate(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel)
        {
            var result = new ExerciseEvaluationResult
            {
                Timestamp = DateTime.Now,
                Details = new Dictionary<string, object>()
            };

            // PRIORITY 1: Health check first - always override with health status
            var healthStatus = EvaluateHealth(metrics);
            if (healthStatus != null)
            {
                result.Status = healthStatus.Value.Status;
                result.HealthIndicator = healthStatus.Value.Health;
                result.CoachHintKey = healthStatus.Value.HintKey;
                result.PitchStatus = EvaluationStatus.Stop;
                result.ResonanceStatus = EvaluationStatus.Stop;
                result.StabilityStatus = EvaluationStatus.Stop;
                result.IntonationStatus = EvaluationStatus.NotApplicable;
                return result;
            }

            // PRIORITY 2: Evaluate pitch
            result.PitchStatus = _pitchEvaluator.Evaluate(metrics, definition, userLevel);
            result.Details["Pitch"] = metrics.Pitch;
            result.Details["PitchStatus"] = result.PitchStatus.ToString();

            // PRIORITY 3: Evaluate resonance (resonance-first hierarchy)
            result.ResonanceStatus = _resonanceEvaluator.Evaluate(metrics, definition, userLevel);
            result.Details["F1"] = metrics.F1;
            result.Details["F2"] = metrics.F2;
            result.Details["ResonanceStatus"] = result.ResonanceStatus.ToString();

            // PRIORITY 4: Evaluate stability (jitter/shimmer)
            result.StabilityStatus = EvaluateStability(metrics);
            result.Details["Jitter"] = metrics.Jitter;
            result.Details["Shimmer"] = metrics.Shimmer;
            result.Details["StabilityStatus"] = result.StabilityStatus.ToString();

            // PRIORITY 5: Evaluate intonation
            if (definition.RequiresIntonation)
            {
                result.IntonationStatus = _intonationEvaluator.Evaluate(metrics, definition, userLevel);
                result.Details["IntonationRange"] = metrics.IntonationRange;
                result.Details["IntonationStatus"] = result.IntonationStatus.ToString();
            }

            // PRIORITY 6: Evaluate breathing (if applicable)
            if (definition.GoalCategory == GoalCategory.Breathing)
            {
                var breathingStatus = _breathingEvaluator.Evaluate(metrics, definition, userLevel);
                result.Details["BreathingStatus"] = breathingStatus.ToString();
            }

            // Combine results with priority
            // Overall status is the worst of all statuses (Stop > Adjust > Correct)
            var allStatuses = new[]
            {
                result.PitchStatus,
                result.ResonanceStatus,
                result.StabilityStatus,
                result.IntonationStatus
            };

            result.Status = GetWorstStatus(allStatuses);
            result.HealthIndicator = metrics.HealthStatus;

            // Generate hint key
            result.CoachHintKey = GenerateHintKey(result, metrics, definition, userLevel);

            return result;
        }

        /// <summary>
        /// Evaluate health indicators and return stop/warning if needed
        /// </summary>
        private (EvaluationStatus Status, HealthIndicator Health, string HintKey)? EvaluateHealth(VoiceMetrics metrics)
        {
            // Critical strain: immediate stop
            if (metrics.Jitter > 2.5 || metrics.Shimmer > 5.0 || metrics.StrainLevel > 0.75)
            {
                return (EvaluationStatus.Stop, HealthIndicator.Critical, "CoachHint_Health_Critical");
            }

            // Warning indicators
            if (metrics.HasStrainWarning || metrics.IsPitchPress || metrics.ConsecutiveStrainCycles >= 3)
            {
                // Determine specific warning
                if (metrics.IsPitchPress)
                {
                    return (EvaluationStatus.Stop, HealthIndicator.Warning, "CoachHint_PitchPress_Rest");
                }
                if (metrics.Jitter > 1.5)
                {
                    return (EvaluationStatus.Adjust, HealthIndicator.Warning, "CoachHint_Health_JitterWarning");
                }
                if (metrics.Shimmer > 3.5)
                {
                    return (EvaluationStatus.Adjust, HealthIndicator.Warning, "CoachHint_Health_ShimmerWarning");
                }
                if (metrics.HNR < 15 && metrics.HNR > 0)
                {
                    return (EvaluationStatus.Adjust, HealthIndicator.Warning, "CoachHint_Health_Hoarseness");
                }
                
                return (EvaluationStatus.Adjust, HealthIndicator.Warning, "CoachHint_Health_TakeItEasy");
            }

            return null;
        }

        /// <summary>
        /// Evaluate stability based on jitter and shimmer
        /// </summary>
        private EvaluationStatus EvaluateStability(VoiceMetrics metrics)
        {
            if (metrics.Jitter <= 0 && metrics.Shimmer <= 0)
                return EvaluationStatus.NotApplicable;

            // Critical: very high jitter/shimmer
            if (metrics.Jitter > 2.5 || metrics.Shimmer > 5.0)
            {
                return EvaluationStatus.Stop;
            }

            // Warning: elevated jitter/shimmer
            if (metrics.Jitter > 1.5 || metrics.Shimmer > 3.5)
            {
                return EvaluationStatus.Adjust;
            }

            // Good stability
            return EvaluationStatus.Correct;
        }

        /// <summary>
        /// Get worst status from array
        /// </summary>
        private EvaluationStatus GetWorstStatus(EvaluationStatus[] statuses)
        {
            foreach (var status in statuses)
            {
                if (status == EvaluationStatus.Stop)
                    return EvaluationStatus.Stop;
            }

            foreach (var status in statuses)
            {
                if (status == EvaluationStatus.Adjust)
                    return EvaluationStatus.Adjust;
            }

            return EvaluationStatus.Correct;
        }

        /// <summary>
        /// Generate appropriate hint key based on evaluation results
        /// </summary>
        private string GenerateHintKey(ExerciseEvaluationResult result, VoiceMetrics metrics, 
            ExerciseDefinition definition, UserLevel userLevel)
        {
            // Health warning takes priority
            if (result.HealthIndicator == HealthIndicator.Critical)
            {
                return "CoachHint_Health_Critical";
            }
            if (result.HealthIndicator == HealthIndicator.Warning)
            {
                if (metrics.IsPitchPress)
                    return "CoachHint_PitchPress_Rest";
                if (metrics.Jitter > 1.5)
                    return "CoachHint_Health_JitterWarning";
                return "CoachHint_Health_TakeItEasy";
            }

            // Resonance-first hierarchy: if F2 is back, prioritize resonance feedback
            if (result.ResonanceStatus == EvaluationStatus.Stop || 
                (result.ResonanceStatus == EvaluationStatus.Adjust && metrics.F2 < 1400))
            {
                return _resonanceEvaluator.GetHintKey(metrics, definition, userLevel);
            }

            // Then pitch
            if (result.PitchStatus == EvaluationStatus.Adjust)
            {
                return _pitchEvaluator.GetHintKey(metrics, definition, userLevel);
            }

            // Then stability
            if (result.StabilityStatus == EvaluationStatus.Adjust)
            {
                return "CoachHint_Stability_Adjust";
            }

            // Then intonation
            if (result.IntonationStatus == EvaluationStatus.Adjust)
            {
                return _intonationEvaluator.GetHintKey(metrics, definition, userLevel);
            }

            // All correct
            return "CoachHint_Correct";
        }
    }
}
