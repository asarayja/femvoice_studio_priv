using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services.FeedbackRuleEngine
{
    /// <summary>
    /// Interface for rule-based parameter evaluators
    /// </summary>
    public interface IRuleEvaluator
    {
        /// <summary>
        /// Evaluate metrics against exercise definition
        /// </summary>
        EvaluationStatus Evaluate(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel);
        
        /// <summary>
        /// Get localized hint key based on evaluation result
        /// </summary>
        string GetHintKey(VoiceMetrics metrics, ExerciseDefinition definition, UserLevel userLevel);
    }
}
