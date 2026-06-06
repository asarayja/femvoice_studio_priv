using System;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    public sealed class FeedbackPipeline
    {
        private readonly FeedbackConsistencyGuard _guard;

        public FeedbackPipeline(FeedbackConsistencyGuard guard)
        {
            _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        }

        public event EventHandler<FeedbackDecision>? FeedbackApproved;
        public event EventHandler<FeedbackDecision>? FeedbackSuppressed;
        public event EventHandler<FeedbackDecision>? FeedbackEscalated;

        public FeedbackDecision Submit(FeedbackCandidate candidate, FeedbackGuardContext? context = null)
        {
            var decision = _guard.Submit(candidate, context);
            Publish(decision);
            return decision;
        }

        private void Publish(FeedbackDecision decision)
        {
            switch (decision.Kind)
            {
                case FeedbackDecisionKind.Approved:
                    FeedbackApproved?.Invoke(this, decision);
                    break;
                case FeedbackDecisionKind.Escalated:
                    FeedbackEscalated?.Invoke(this, decision);
                    break;
                default:
                    FeedbackSuppressed?.Invoke(this, decision);
                    break;
            }
        }
    }

    public sealed class SmartCoachFeedbackMapper
    {
        public FeedbackCandidate? Map(SmartCoachMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrWhiteSpace(message.Message))
                return null;

            var priority = GetPriority(message.MessageType);
            var reasonCode = GetReasonCode(message);

            return new FeedbackCandidate(
                message.Message,
                reasonCode,
                priority,
                GetSeverity(priority),
                "SmartCoachEngine",
                GetConflictKey(message.MessageType, priority));
        }

        public FeedbackGuardContext BuildContext(SmartCoachMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var isHealthWarning = string.Equals(
                message.MessageType,
                "health_warning",
                StringComparison.OrdinalIgnoreCase);

            return new FeedbackGuardContext(
                IsHealthRiskActive: isHealthWarning,
                IsPauseRecommended: isHealthWarning,
                IsHoldStable: !isHealthWarning);
        }

        private static FeedbackPriority GetPriority(string messageType)
            => messageType.ToLowerInvariant() switch
            {
                "health_warning" => FeedbackPriority.HealthWarning,
                "achievement" => FeedbackPriority.PerformancePraise,
                "motivation" => FeedbackPriority.PerformancePraise,
                "tip" => FeedbackPriority.TechniqueCorrection,
                _ => FeedbackPriority.TechniqueCorrection
            };

        private static Models.MessageSeverity GetSeverity(FeedbackPriority priority)
            => priority >= FeedbackPriority.HealthWarning
                ? Models.MessageSeverity.Warning
                : priority == FeedbackPriority.TechniqueCorrection
                    ? Models.MessageSeverity.Suggestion
                    : Models.MessageSeverity.Info;

        private static string GetReasonCode(SmartCoachMessage message)
        {
            var type = string.IsNullOrWhiteSpace(message.MessageType)
                ? "message"
                : message.MessageType;
            return $"SMARTCOACH_{type.ToUpperInvariant()}";
        }

        private static string GetConflictKey(string messageType, FeedbackPriority priority)
            => priority >= FeedbackPriority.HealthWarning
                ? "SMARTCOACH_HEALTH"
                : $"SMARTCOACH_{messageType.ToUpperInvariant()}";
    }

    public sealed class InlineCoachFeedbackMapper
    {
        public FeedbackCandidate? Map(InlineCoachMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrWhiteSpace(message.CoachingReason))
                return null;

            var priority = GetPriority(message);
            var key = GetLocalizationKey(message.CoachingReason);

            return new FeedbackCandidate(
                key,
                message.CoachingReason,
                priority,
                message.Severity,
                "ExerciseIntelligenceCoordinator",
                GetConflictKey(message.CoachingReason, priority));
        }

        public FeedbackGuardContext BuildContext(InlineCoachMessage message, ExerciseLiveState? liveState)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var isSafetyReason = string.Equals(
                message.CoachingReason,
                "HEALTH_SAFETY_LOCK",
                StringComparison.OrdinalIgnoreCase);
            var isHealthReason = message.CoachingReason.StartsWith("HEALTH_", StringComparison.OrdinalIgnoreCase);

            return new FeedbackGuardContext(
                IsSafetyFreezeActive: liveState?.IsSafetyLocked == true || isSafetyReason,
                IsHealthRiskActive: liveState?.IsSafetyLocked == true || isHealthReason,
                IsPauseRecommended: isSafetyReason,
                IsHoldStable: liveState?.IsHoldingCorrectly ?? true);
        }

        private static FeedbackPriority GetPriority(InlineCoachMessage message)
        {
            if (string.Equals(message.CoachingReason, "HEALTH_SAFETY_LOCK", StringComparison.OrdinalIgnoreCase))
                return FeedbackPriority.SafetyFreeze;

            if (message.CoachingReason.StartsWith("HEALTH_", StringComparison.OrdinalIgnoreCase))
                return FeedbackPriority.HealthWarning;

            if (string.Equals(message.CoachingReason, "HOLD_COMPLETE", StringComparison.OrdinalIgnoreCase))
                return FeedbackPriority.PerformancePraise;

            return message.Severity == MessageSeverity.Warning
                ? FeedbackPriority.HealthWarning
                : FeedbackPriority.TechniqueCorrection;
        }

        private static string GetLocalizationKey(string reasonCode)
            => reasonCode.ToUpperInvariant() switch
            {
                "HEALTH_SAFETY_LOCK" => "InlineCoachFeedback_HealthSafetyLock",
                "RESONANCE_TOO_LOW" => "InlineCoachFeedback_ResonanceTooLow",
                "RESONANCE_TOO_HIGH" => "InlineCoachFeedback_ResonanceTooHigh",
                "STABILITY_LOW" => "InlineCoachFeedback_StabilityLow",
                "PITCH_OUT_OF_ZONE" => "InlineCoachFeedback_PitchOutOfZone",
                "HOLD_COMPLETE" => "InlineCoachFeedback_HoldComplete",
                _ => "InlineCoachFeedback_Generic"
            };

        private static string GetConflictKey(string reasonCode, FeedbackPriority priority)
            => priority >= FeedbackPriority.HealthWarning
                ? "INLINE_HEALTH"
                : $"INLINE_{reasonCode.ToUpperInvariant()}";
    }

    public sealed class ProgressionFeedbackMapper
    {
        public FeedbackCandidate? Map(ProgressionOrchestratorDecision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            if (decision.Dimension == ProgressionAdjustmentDimension.ExerciseVariation)
            {
                return new FeedbackCandidate(
                    "ProgressionFeedback_Variation",
                    decision.ReasonCode,
                    FeedbackPriority.ProgressionUpdate,
                    Models.MessageSeverity.Info,
                    "ProgressionOrchestrator",
                    "PROGRESSION_VARIATION");
            }

            return decision.Kind switch
            {
                ProgressionOrchestratorDecisionKind.ProgressionPaused => new FeedbackCandidate(
                    "ProgressionFeedback_Paused",
                    decision.ReasonCode,
                    FeedbackPriority.PauseRecommendation,
                    Models.MessageSeverity.Warning,
                    "ProgressionOrchestrator",
                    "PROGRESSION_HEALTH"),

                ProgressionOrchestratorDecisionKind.RegressionTriggered => new FeedbackCandidate(
                    "ProgressionFeedback_Regression",
                    decision.ReasonCode,
                    FeedbackPriority.HealthWarning,
                    Models.MessageSeverity.Warning,
                    "ProgressionOrchestrator",
                    "PROGRESSION_HEALTH"),

                ProgressionOrchestratorDecisionKind.PlateauDetected => new FeedbackCandidate(
                    "ProgressionFeedback_Plateau",
                    decision.ReasonCode,
                    FeedbackPriority.TechniqueCorrection,
                    Models.MessageSeverity.Suggestion,
                    "ProgressionOrchestrator",
                    "PROGRESSION_TECHNIQUE"),

                ProgressionOrchestratorDecisionKind.DifficultyAdjustmentSuggested or
                ProgressionOrchestratorDecisionKind.ExerciseProfileUpdated => new FeedbackCandidate(
                    "ProgressionFeedback_Update",
                    decision.ReasonCode,
                    FeedbackPriority.ProgressionUpdate,
                    Models.MessageSeverity.Info,
                    "ProgressionOrchestrator",
                    "PROGRESSION_UPDATE"),

                _ => null
            };
        }

        public FeedbackGuardContext BuildContext(ProgressionOrchestratorDecision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            return new FeedbackGuardContext(
                IsSafetyFreezeActive: decision.Kind == ProgressionOrchestratorDecisionKind.RegressionTriggered
                    && string.Equals(decision.ReasonCode, "SAFETY_EVENTS", StringComparison.OrdinalIgnoreCase),
                IsHealthRiskActive: decision.Kind == ProgressionOrchestratorDecisionKind.RegressionTriggered,
                IsPauseRecommended: decision.Kind == ProgressionOrchestratorDecisionKind.ProgressionPaused,
                IsFatigueActive: string.Equals(decision.ReasonCode, "FATIGUE_RISING", StringComparison.OrdinalIgnoreCase),
                IsHoldStable: !string.Equals(decision.ReasonCode, "HOLD_CONSOLIDATION", StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class HydrationFeedbackMapper
    {
        public FeedbackCandidate? Map(HydrationAdvice advice)
        {
            if (advice == null) throw new ArgumentNullException(nameof(advice));
            if (!advice.Suggested)
                return null;

            return new FeedbackCandidate(
                "VoiceHealthFeedback_Hydration",
                advice.ReasonCode,
                FeedbackPriority.HydrationSuggestion,
                Models.MessageSeverity.Suggestion,
                "HydrationAdvisor",
                "HEALTH_HYDRATION");
        }

        public FeedbackGuardContext BuildContext(HydrationAdvice advice, ExerciseLiveState? liveState)
        {
            if (advice == null) throw new ArgumentNullException(nameof(advice));

            return new FeedbackGuardContext(
                IsSafetyFreezeActive: liveState?.IsSafetyLocked == true,
                IsHealthRiskActive: liveState?.IsSafetyLocked == true,
                IsHoldStable: liveState?.IsHoldingCorrectly ?? true);
        }
    }

    public sealed class VocalHealthFeedbackMapper
    {
        public FeedbackCandidate? Map(VocalHealthDecision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            if (decision.State == HealthSafetyState.Lock)
            {
                return Candidate(
                    "VoiceHealthFeedback_Lock",
                    decision.ReasonCode,
                    FeedbackPriority.SafetyFreeze,
                    Models.MessageSeverity.Warning,
                    "HEALTH_LOCK");
            }

            if (decision.State == HealthSafetyState.Restrict)
            {
                return Candidate(
                    "VoiceHealthFeedback_Restrict",
                    decision.ReasonCode,
                    FeedbackPriority.HealthWarning,
                    Models.MessageSeverity.Warning,
                    "HEALTH_RESTRICT");
            }

            if (decision.PauseRecommended)
            {
                return Candidate(
                    "VoiceHealthFeedback_Pause",
                    decision.ReasonCode,
                    FeedbackPriority.PauseRecommendation,
                    Models.MessageSeverity.Warning,
                    "HEALTH_PAUSE");
            }

            if (decision.StrainDetected)
            {
                return Candidate(
                    "VoiceHealthFeedback_Strain",
                    decision.ReasonCode,
                    FeedbackPriority.ActiveStrainAlert,
                    Models.MessageSeverity.Warning,
                    "HEALTH_STRAIN");
            }

            if (decision.FatigueDetected)
            {
                return Candidate(
                    "VoiceHealthFeedback_Fatigue",
                    decision.ReasonCode,
                    FeedbackPriority.HealthWarning,
                    Models.MessageSeverity.Warning,
                    "HEALTH_FATIGUE");
            }

            if (decision.HydrationSuggested)
            {
                return Candidate(
                    "VoiceHealthFeedback_Hydration",
                    decision.ReasonCode,
                    FeedbackPriority.HydrationSuggestion,
                    Models.MessageSeverity.Suggestion,
                    "HEALTH_HYDRATION");
            }

            return null;
        }

        public FeedbackGuardContext BuildContext(VocalHealthDecision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            return new FeedbackGuardContext(
                IsSafetyFreezeActive: decision.State == HealthSafetyState.Lock,
                IsHealthRiskActive: decision.State is HealthSafetyState.Restrict or HealthSafetyState.Lock,
                IsActiveStrainAlert: decision.StrainDetected,
                IsPauseRecommended: decision.PauseRecommended,
                IsFatigueActive: decision.FatigueDetected,
                IsHoldStable: !decision.StrainDetected);
        }

        private static FeedbackCandidate Candidate(
            string localizationKey,
            string reasonCode,
            FeedbackPriority priority,
            Models.MessageSeverity severity,
            string conflictKey)
            => new(
                localizationKey,
                reasonCode,
                priority,
                severity,
                "VocalHealthSupervisor",
                conflictKey);
    }
}
