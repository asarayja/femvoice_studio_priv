using System;

namespace FemVoiceStudio.Models
{
    // OutcomeProfile and its supporting records (GoalProgress, GoalProgressEntry,
    // RecoveryProgress, ExerciseEffectivenessSummary, LongTermDevelopment) are defined in
    // OutcomeProfile.cs (W0-A4).

    // ── CaseReview domain types ───────────────────────────────────────────────────

    /// <summary>
    /// Discriminates the clinical context of a case review session.
    /// </summary>
    public enum ReviewType
    {
        /// <summary>End-of-month outcome review.</summary>
        Monthly,

        /// <summary>Review triggered by a reached or missed voice goal.</summary>
        Goal,

        /// <summary>Ad-hoc progress check between scheduled reviews.</summary>
        Progress,

        /// <summary>Recovery-focused review (overtraining, voice rest, return-to-train).</summary>
        Recovery,
    }

    /// <summary>
    /// Lifecycle state of a <see cref="CaseReview"/>.
    /// </summary>
    public enum ReviewStatus
    {
        /// <summary>Review has been created but not yet signed-off by a reviewer.</summary>
        Draft,

        /// <summary>Review has been completed and locked.</summary>
        Completed,
    }

    /// <summary>
    /// An immutable case review record: a structured, time-bounded clinical review of one
    /// user's voice outcome, carrying a JSON snapshot of the <see cref="OutcomeProfile"/>
    /// at the time of review creation.
    ///
    /// CLINICAL FRAMING: a case review is DESCRIPTIVE / EXPLANATORY only. It never
    /// overrides Safety &gt; Health &gt; Recovery gates; those remain the only blocking
    /// authorities. The JSON snapshot (<see cref="OutcomeSnapshotJson"/>) is opaque
    /// storage — consumers must round-trip via <c>JsonSerializer</c> when they need
    /// structured data.
    ///
    /// Lifecycle: created as <see cref="ReviewStatus.Draft"/>; transitioned to
    /// <see cref="ReviewStatus.Completed"/> via <see cref="CaseReviewsStore"/>.
    /// </summary>
    public sealed record CaseReview
    {
        /// <summary>Stable unique identifier for this review (UUID v4).</summary>
        public Guid ReviewId { get; init; }

        /// <summary>The user whose outcome this review covers.</summary>
        public int UserId { get; init; }

        /// <summary>Clinical context of the review.</summary>
        public ReviewType ReviewType { get; init; }

        /// <summary>
        /// Inclusive start of the review period (UTC).
        /// All outcome data inside [<see cref="PeriodStart"/>, <see cref="PeriodEnd"/>]
        /// was used when the snapshot was assembled.
        /// </summary>
        public DateTime PeriodStart { get; init; }

        /// <summary>
        /// Inclusive end of the review period (UTC).
        /// </summary>
        public DateTime PeriodEnd { get; init; }

        /// <summary>
        /// JSON serialisation of the <see cref="OutcomeProfile"/> at creation time.
        /// Stored opaquely — never parsed inside this layer. Use
        /// <c>JsonSerializer.Deserialize&lt;OutcomeProfile&gt;(OutcomeSnapshotJson)</c>
        /// when structured access is needed.
        /// </summary>
        public string OutcomeSnapshotJson { get; init; } = string.Empty;

        /// <summary>Lifecycle state: Draft on creation; Completed after sign-off.</summary>
        public ReviewStatus Status { get; init; }

        /// <summary>When this record was first persisted (UTC).</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>When this review was completed (UTC). Null while still in Draft.</summary>
        public DateTime? CompletedAt { get; init; }
    }
}
