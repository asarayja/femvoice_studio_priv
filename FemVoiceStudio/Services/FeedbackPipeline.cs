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

            // SmartCoach health_warning-meldinger oppstår KUN fra
            // SmartCoachEngine.AnalyzeSessionForStrain når strainDetected == true
            // (pitch_press eller fatigue). Strain-deteksjonen selv er derfor en aktiv
            // strain-tilstand: vi flagger IsActiveStrainAlert slik at guardens
            // suppresjonsmatrise undertrykker samtidig ros/progresjon (≤ PerformancePraise)
            // mens selve helse-advarselen (HealthWarning 70) slipper gjennom.
            return new FeedbackGuardContext(
                IsHealthRiskActive: isHealthWarning,
                IsActiveStrainAlert: isHealthWarning,
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

    /// <summary>
    /// The clinical intent behind a feedback message originating on the home screen
    /// (MainViewModel). Drives priority/severity/conflict-key selection in
    /// <see cref="MainScreenFeedbackMapper"/>. The home screen carries already-resolved
    /// display text (not localization keys), so candidates produced from these intents
    /// hold the final text directly and MainViewModel routes them back to the bound
    /// property by <see cref="MainScreenFeedbackBinding"/> — mirroring how
    /// <see cref="SmartCoachFeedbackMapper"/> emits raw text candidates.
    /// </summary>
    public enum MainScreenFeedbackKind
    {
        /// <summary>Live pitch-zone coaching ("under/in/above zone"). TechniqueCorrection.</summary>
        PitchZoneCoaching,

        /// <summary>SmartCoach explanation; HealthWarning when the context names a health issue, else technique.</summary>
        CoachExplanation,

        /// <summary>End-of-session summary. Praise vs. progression update by content.</summary>
        SessionSummary,

        /// <summary>Difficulty promotion / complexity advance celebration. ProgressionUpdate.</summary>
        ProgressionCelebration,

        /// <summary>Safety-lock engaged/released notice. SafetyFreeze (always survives the guard).</summary>
        SafetyLockNotice
    }

    /// <summary>
    /// Which bound MainViewModel property the approved home-screen message should be
    /// routed to. Lets MainViewModel demux <see cref="FeedbackPipeline.FeedbackApproved"/>
    /// without re-parsing the reason code.
    /// </summary>
    public enum MainScreenFeedbackBinding
    {
        RealtimeFeedback,
        CoachExplanation,
        SessionFeedback,
        StatusText
    }

    /// <summary>
    /// A home-screen feedback message ready to be routed through the pipeline.
    /// <paramref name="ResolvedText"/> is already localized/formatted (the home screen
    /// never deals in localization keys here). <paramref name="IsHealthContext"/> lets the
    /// coach-explanation source escalate to <see cref="FeedbackPriority.HealthWarning"/>
    /// when the explanation describes a strain/health issue. <paramref name="IsPraise"/>
    /// distinguishes a session-summary praise from a neutral progression update.
    /// Pure value type — testable without WPF.
    /// </summary>
    public sealed record MainScreenFeedbackIntent(
        MainScreenFeedbackKind Kind,
        string ResolvedText,
        bool IsHealthContext = false,
        bool IsPraise = false,
        string? ReasonDiscriminator = null);

    /// <summary>
    /// Snapshot of the home screen's clinical runtime state, used to build the
    /// <see cref="FeedbackGuardContext"/> so the suppression matrix applies to the
    /// front page exactly as it does to the exercise screen. All flags are READ from
    /// existing MainViewModel state (HealthIndicator, FemVoiceScore WarningFlags,
    /// the cached ProgressionSafetyGate recovery flag). Pure value type.
    /// </summary>
    public sealed record MainScreenClinicalState(
        bool IsHealthRiskActive = false,
        bool IsActiveStrainAlert = false,
        bool IsPauseRecommended = false,
        bool IsFatigueActive = false);

    /// <summary>
    /// Maps home-screen feedback intents to pipeline candidates and builds the guard
    /// context from the home screen's clinical runtime state. Lives next to the other
    /// mappers (no new silo). All candidates use Source "MainScreen" so MainViewModel
    /// can recognise its own approved messages and treat the candidate text as already
    /// resolved (rather than a localization key).
    /// </summary>
    public sealed class MainScreenFeedbackMapper
    {
        public const string SourceName = "MainScreen";

        public FeedbackCandidate? Map(MainScreenFeedbackIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (string.IsNullOrWhiteSpace(intent.ResolvedText))
                return null;

            var (priority, severity, reasonCode, conflictKey) = Classify(intent);

            // En valgfri diskriminator gir to logisk distinkte hendelser samme
            // klassifisering, men ULIKE ReasonCode/ConflictKey, slik at de ikke
            // rate-limiter eller konflikt-undertrykker hverandre i guarden (f.eks.
            // safety-lås ENGASJERT vs. FRIGJORT — begge må gjennom selv om de fyrer
            // tett). ReasonCode-PREFIKSET bevares slik at ruteren (BindingFor/receiveren)
            // fortsatt kjenner igjen kategorien.
            if (!string.IsNullOrEmpty(intent.ReasonDiscriminator))
            {
                reasonCode = $"{reasonCode}_{intent.ReasonDiscriminator}";
                conflictKey = $"{conflictKey}_{intent.ReasonDiscriminator}";
            }

            return new FeedbackCandidate(
                intent.ResolvedText,
                reasonCode,
                priority,
                severity,
                SourceName,
                conflictKey);
        }

        /// <summary>
        /// Where the approved message should be shown. Independent of the guard so it
        /// can be read on the approval event without re-deriving the intent.
        /// </summary>
        public static MainScreenFeedbackBinding BindingFor(MainScreenFeedbackKind kind)
            => kind switch
            {
                MainScreenFeedbackKind.PitchZoneCoaching => MainScreenFeedbackBinding.RealtimeFeedback,
                MainScreenFeedbackKind.CoachExplanation => MainScreenFeedbackBinding.CoachExplanation,
                MainScreenFeedbackKind.SessionSummary => MainScreenFeedbackBinding.SessionFeedback,
                MainScreenFeedbackKind.ProgressionCelebration => MainScreenFeedbackBinding.StatusText,
                MainScreenFeedbackKind.SafetyLockNotice => MainScreenFeedbackBinding.StatusText,
                _ => MainScreenFeedbackBinding.StatusText
            };

        /// <summary>
        /// Routes an approved candidate's ReasonCode back to its bound property. Uses
        /// prefix matching so an optional discriminator suffix (e.g. safety ENGAGED vs
        /// RELEASED) still routes correctly. Used by MainViewModel's approval receiver;
        /// exposed (and pure) so it is unit-testable without WPF.
        /// </summary>
        public static MainScreenFeedbackBinding BindingForReasonCode(string reasonCode)
        {
            if (string.IsNullOrEmpty(reasonCode))
                return MainScreenFeedbackBinding.StatusText;

            if (reasonCode.StartsWith("MAINSCREEN_PITCH_ZONE", StringComparison.Ordinal))
                return MainScreenFeedbackBinding.RealtimeFeedback;
            if (reasonCode.StartsWith("MAINSCREEN_COACH", StringComparison.Ordinal))
                return MainScreenFeedbackBinding.CoachExplanation;
            if (reasonCode.StartsWith("MAINSCREEN_SESSION", StringComparison.Ordinal))
                return MainScreenFeedbackBinding.SessionFeedback;

            // MAINSCREEN_PROGRESSION / MAINSCREEN_SAFETY_LOCK → status line.
            return MainScreenFeedbackBinding.StatusText;
        }

        public FeedbackGuardContext BuildContext(MainScreenClinicalState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            return new FeedbackGuardContext(
                IsHealthRiskActive: state.IsHealthRiskActive,
                IsActiveStrainAlert: state.IsActiveStrainAlert,
                IsPauseRecommended: state.IsPauseRecommended,
                IsFatigueActive: state.IsFatigueActive,
                // Home screen has no "hold" concept; keep it stable so the unstable-hold
                // praise suppression never fires spuriously here.
                IsHoldStable: true);
        }

        private static (FeedbackPriority Priority, Models.MessageSeverity Severity, string ReasonCode, string ConflictKey)
            Classify(MainScreenFeedbackIntent intent)
            => intent.Kind switch
            {
                MainScreenFeedbackKind.SafetyLockNotice => (
                    FeedbackPriority.SafetyFreeze,
                    Models.MessageSeverity.Warning,
                    "MAINSCREEN_SAFETY_LOCK",
                    "MAINSCREEN_SAFETY"),

                MainScreenFeedbackKind.CoachExplanation when intent.IsHealthContext => (
                    FeedbackPriority.HealthWarning,
                    Models.MessageSeverity.Warning,
                    "MAINSCREEN_COACH_HEALTH",
                    "MAINSCREEN_COACH"),

                MainScreenFeedbackKind.CoachExplanation => (
                    FeedbackPriority.TechniqueCorrection,
                    Models.MessageSeverity.Suggestion,
                    "MAINSCREEN_COACH_TECHNIQUE",
                    "MAINSCREEN_COACH"),

                MainScreenFeedbackKind.PitchZoneCoaching => (
                    FeedbackPriority.TechniqueCorrection,
                    Models.MessageSeverity.Suggestion,
                    "MAINSCREEN_PITCH_ZONE",
                    "MAINSCREEN_PITCH_ZONE"),

                MainScreenFeedbackKind.ProgressionCelebration => (
                    FeedbackPriority.ProgressionUpdate,
                    Models.MessageSeverity.Info,
                    "MAINSCREEN_PROGRESSION",
                    "MAINSCREEN_PROGRESSION"),

                // SessionSummary: praise vs. neutral progression update by content.
                _ when intent.IsPraise => (
                    FeedbackPriority.PerformancePraise,
                    Models.MessageSeverity.Info,
                    "MAINSCREEN_SESSION_PRAISE",
                    "MAINSCREEN_SESSION"),

                _ => (
                    FeedbackPriority.ProgressionUpdate,
                    Models.MessageSeverity.Info,
                    "MAINSCREEN_SESSION_SUMMARY",
                    "MAINSCREEN_SESSION")
            };
    }

    /// <summary>
    /// Debounce helper for the home screen's high-frequency live feedback sources
    /// (pitch-zone coaching ~30 Hz, coach explanation ~1 Hz). Submits to the pipeline
    /// only when the message KEY changes, or when a minimum interval has elapsed since
    /// the last submit for that binding — so the guard's per-reason rate-limiter is
    /// never spammed at 30 Hz. Pure, deterministic (injectable clock), no WPF — fully
    /// unit-testable. Not thread-safe; the home screen calls it from the UI thread only.
    /// </summary>
    public sealed class MainScreenFeedbackDebouncer
    {
        private readonly Func<DateTime> _clock;
        private readonly TimeSpan _minimumInterval;
        private readonly System.Collections.Generic.Dictionary<MainScreenFeedbackBinding, (string Key, DateTime At)> _last = new();

        public MainScreenFeedbackDebouncer(Func<DateTime>? clock = null, TimeSpan? minimumInterval = null)
        {
            _clock = clock ?? (() => DateTime.UtcNow);
            _minimumInterval = minimumInterval ?? TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Returns true when a message with <paramref name="messageKey"/> for
        /// <paramref name="binding"/> should be submitted to the pipeline now. A repeated
        /// identical key inside the window is swallowed (false); a changed key passes
        /// immediately; an unchanged key passes again only after the window elapses.
        /// </summary>
        public bool ShouldSubmit(MainScreenFeedbackBinding binding, string messageKey)
        {
            if (string.IsNullOrEmpty(messageKey))
                return false;

            var now = _clock();
            if (_last.TryGetValue(binding, out var prev))
            {
                if (prev.Key == messageKey && now - prev.At < _minimumInterval)
                    return false;
            }

            _last[binding] = (messageKey, now);
            return true;
        }

        /// <summary>Clears debounce memory — call on session start/stop so a new session re-emits.</summary>
        public void Reset() => _last.Clear();
    }
}
