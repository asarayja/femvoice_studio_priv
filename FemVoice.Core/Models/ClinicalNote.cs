using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Discriminates the purpose of a <see cref="ClinicalNote"/> entry.
    /// Coach and Clinical notes support upsert (editable).
    /// Review and GoalReview are append-only/immutable once written.
    /// </summary>
    public enum ClinicalNoteType
    {
        /// <summary>General coaching observation written by the AI or a human coach.</summary>
        Coach,

        /// <summary>Clinical observation (e.g. voice health, SLP remark).</summary>
        Clinical,

        /// <summary>Periodic review snapshot — append-only, never overwritten.</summary>
        Review,

        /// <summary>Goal-lifecycle review entry — append-only, never overwritten.</summary>
        GoalReview
    }

    /// <summary>
    /// A single clinical or coaching note attached to a user.
    ///
    /// Owned by W0-A6 (Clinical Notes). Persisted in the ClinicalNotes table via
    /// <see cref="FemVoiceStudio.Services.ClinicalNotesStore"/>.
    ///
    /// <para>
    /// This is a DOCUMENTATION/AUDIT record only — it never overrides any safety,
    /// health, or recovery gate. Clinical priority hierarchy (Safety &gt; Health &gt;
    /// Recovery &gt; Comfort &gt; Voice Development &gt; Reporting) is enforced at the
    /// engine level, not here.
    /// </para>
    /// </summary>
    public sealed record ClinicalNote
    {
        /// <summary>Unique identifier for this note.</summary>
        public Guid NoteId { get; init; } = Guid.NewGuid();

        /// <summary>The user this note is about.</summary>
        public int UserId { get; init; }

        /// <summary>Discriminates how this note was created and its mutability policy.</summary>
        public ClinicalNoteType NoteType { get; init; }

        /// <summary>
        /// Role of the author (e.g. "Coach", "Clinician", "System").
        /// Never a raw username — always a role label.
        /// </summary>
        public string AuthorRole { get; init; } = string.Empty;

        /// <summary>When the note was created.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>
        /// Free-text body of the note.
        ///
        /// <b>PII-BEARING</b>: this field may contain personally identifiable or
        /// clinically sensitive information. The research anonymizer MUST strip or
        /// replace this field before any note is included in a research export or
        /// shared dataset. Do NOT log or surface raw BodyText outside the clinical UI.
        /// </summary>
        public string BodyText { get; init; } = string.Empty;

        /// <summary>
        /// Optional type tag of the entity this note is linked to
        /// (e.g. "Session", "Goal", "Exercise"). Null if not linked.
        /// </summary>
        public string? LinkedEntityType { get; init; }

        /// <summary>
        /// Optional opaque identifier of the linked entity (e.g. a session ID or goal ID
        /// serialised as a string). Null if not linked.
        /// </summary>
        public string? LinkedEntityId { get; init; }
    }
}
