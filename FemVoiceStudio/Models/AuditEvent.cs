using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Discriminates which kind of entity an <see cref="AuditEvent"/> records a change for.
    /// </summary>
    public enum AuditEntityType
    {
        /// <summary>A SmartCoach or clinical recommendation was acted upon.</summary>
        Recommendation,

        /// <summary>A user goal was created, modified, or closed.</summary>
        GoalChange,

        /// <summary>A manual clinician/coach override was applied.</summary>
        Override,

        /// <summary>A recovery event (rest prescription, ACWR alert, etc.) was triggered.</summary>
        RecoveryEvent,

        /// <summary>A case review action was recorded (e.g. review opened, approved).</summary>
        ReviewAction
    }

    /// <summary>
    /// A single immutable audit-trail entry.
    ///
    /// Owned by W0-A11 (Audit Trail). Persisted in the AuditTrail table via
    /// <see cref="FemVoiceStudio.Services.AuditTrailStore"/>.
    ///
    /// <para>
    /// Audit events are <b>strictly append-only</b>: once written, no row is ever
    /// mutated. The persistence layer enforces this by using plain INSERT with no
    /// ON CONFLICT clause and no UNIQUE constraint on AuditId, so a duplicate write
    /// produces two rows rather than an update.
    /// </para>
    ///
    /// <para>
    /// This is an AUDIT/TRACEABILITY record only — it never overrides any safety,
    /// health, or recovery gate. Clinical priority hierarchy
    /// (Safety &gt; Health &gt; Recovery &gt; Comfort &gt; Voice Development &gt; Reporting)
    /// is enforced at the engine level, not here.
    /// </para>
    /// </summary>
    public sealed record AuditEvent
    {
        /// <summary>Unique identifier for this audit entry.</summary>
        public Guid AuditId { get; init; } = Guid.NewGuid();

        /// <summary>The user this event relates to.</summary>
        public int UserId { get; init; }

        /// <summary>When the audited action occurred (UTC).</summary>
        public DateTime OccurredAt { get; init; }

        /// <summary>Which entity category this event describes.</summary>
        public AuditEntityType EntityType { get; init; }

        /// <summary>
        /// Opaque stable identifier of the audited entity (e.g. a Guid or integer
        /// serialised as a string). Combined with <see cref="EntityType"/> this
        /// uniquely locates the affected record.
        /// </summary>
        public string EntityId { get; init; } = string.Empty;

        /// <summary>
        /// Role of the actor who triggered the event (e.g. "Coach", "Clinician",
        /// "System"). Never a raw username — always a role label.
        /// </summary>
        public string ActorRole { get; init; } = string.Empty;

        /// <summary>
        /// Stable machine-readable code explaining why the action occurred
        /// (e.g. "GOAL_ACHIEVED", "SAFETY_OVERRIDE_APPLIED"). For logs and reports;
        /// never shown raw to the user.
        /// </summary>
        public string ReasonCode { get; init; } = string.Empty;

        /// <summary>
        /// JSON snapshot of the entity state <em>before</em> the change, or
        /// <see langword="null"/> if this is a creation event or before-state is
        /// not applicable.
        /// </summary>
        public string? BeforeJson { get; init; }

        /// <summary>
        /// JSON snapshot of the entity state <em>after</em> the change, or
        /// <see langword="null"/> if this is a deletion event or after-state is
        /// not applicable.
        /// </summary>
        public string? AfterJson { get; init; }
    }
}
