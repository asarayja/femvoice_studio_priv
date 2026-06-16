using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// A single SmartCoach advice recommendation entry, tracking whether the user
    /// followed the advice and what outcome was observed.
    ///
    /// Owned by A8 (SmartCoach Memory). Persisted in the SmartCoachAdviceLog table
    /// via <see cref="FemVoiceStudio.Services.SmartCoachMemoryStore"/>.
    ///
    /// This is a DESCRIPTIVE/historical record only — it never overrides any
    /// safety, health, or recovery gate. Clinical priority hierarchy is enforced
    /// at the engine level, not here.
    /// </summary>
    public sealed record SmartCoachAdviceEntry
    {
        /// <summary>Unique identifier for this advice recommendation.</summary>
        public Guid AdviceId { get; init; } = Guid.NewGuid();

        /// <summary>The user this advice was generated for.</summary>
        public int UserId { get; init; }

        /// <summary>When the advice was generated and presented.</summary>
        public DateTime RecommendedAt { get; init; }

        /// <summary>Short label for the advised focus area (e.g. "Resonance", "Comfort").</summary>
        public string FocusArea { get; init; } = string.Empty;

        /// <summary>Optional specific exercise recommended. Null if the advice was general.</summary>
        public int? RecommendedExerciseId { get; init; }

        /// <summary>When the user started acting on this advice. Null if not started.</summary>
        public DateTime? StartedAt { get; init; }

        /// <summary>When the user completed the advised action. Null if not completed.</summary>
        public DateTime? CompletedAt { get; init; }

        /// <summary>True if the user chose to follow this advice.</summary>
        public bool UserFollowedAdvice { get; init; }

        /// <summary>
        /// Measured improvement in the focus dimension after acting on the advice (0–100 scale delta).
        /// Null if the outcome has not yet been measured or was not determinable.
        /// </summary>
        public double? OutcomeGain { get; init; }

        /// <summary>True if the advice led to a measurably positive outcome.</summary>
        public bool Success { get; init; }
    }
}
