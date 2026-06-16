namespace FemVoiceStudio.Models
{
    /// <summary>
    /// A brief, user-facing coaching hint produced by
    /// <see cref="FemVoiceStudio.Services.ExerciseIntelligenceCoordinator"/> and displayed
    /// inline within the exercise UI without opening a separate SmartCoach window.
    /// </summary>
    public class InlineCoachMessage
    {
        /// <summary>
        /// Single-sentence, user-friendly guidance.
        /// Must never contain raw numeric values — phrasing is always qualitative
        /// (e.g. "lift the resonance a little higher" rather than "raise F2 by 200 Hz").
        /// </summary>
        public string ShortMessage { get; init; } = string.Empty;

        /// <summary>
        /// Internal reason code used for rate-limiting, analytics, and unit-test assertions.
        /// Examples: "RESONANCE_TOO_LOW", "HOLD_COMPLETE", "HEALTH_SAFETY_LOCK".
        /// </summary>
        public string CoachingReason { get; init; } = string.Empty;

        /// <summary>
        /// Visual and behavioural severity of the message.
        /// Drives colour, icon, and auto-dismiss timing in the UI layer.
        /// </summary>
        public MessageSeverity Severity { get; init; }

        /// <summary>
        /// Number of seconds before the UI automatically dismisses this message.
        /// A value of 0 means the message persists until manually dismissed.
        /// </summary>
        public int AutoDismissSeconds { get; init; }
    }

    /// <summary>
    /// Indicates the urgency and visual treatment of an <see cref="InlineCoachMessage"/>.
    /// </summary>
    public enum MessageSeverity
    {
        /// <summary>
        /// Neutral, positive, or progress-confirming message.
        /// Rendered with a subtle accent colour; short auto-dismiss.
        /// </summary>
        Info,

        /// <summary>
        /// Actionable hint to improve technique.
        /// Rendered with a highlight colour; medium auto-dismiss.
        /// </summary>
        Suggestion,

        /// <summary>
        /// Safety or health alert requiring the user's attention.
        /// Rendered prominently; longer auto-dismiss or manual dismissal required.
        /// </summary>
        Warning
    }
}
