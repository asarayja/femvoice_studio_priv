using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// The kind of clinical decision a professional is asking to override (Sprint E,
    /// Agent 7 — Manual Override). Each kind carries a different intended payload on
    /// <see cref="ManualOverrideRequest"/>.
    /// </summary>
    public enum ManualOverrideKind
    {
        /// <summary>Override the recommended exercise / its target profile.</summary>
        ExerciseReco,

        /// <summary>Override the recovery plan (rest/load) for the user.</summary>
        RecoveryPlan,

        /// <summary>Override the user's voice goal profile.</summary>
        VoiceGoals,

        /// <summary>Override how fast difficulty progression is paced.</summary>
        ProgressionPace
    }

    /// <summary>
    /// A SAFETY-CRITICAL request from a clinician/coach to manually override an
    /// engine-derived clinical decision (Sprint E, Agent 7).
    ///
    /// ── CLINICAL INVARIANT ───────────────────────────────────────────────────────────
    /// An override may only ever HOLD or make a target MORE conservative. The
    /// <see cref="FemVoiceStudio.Services.ManualOverrideEngine"/> applies a two-stage
    /// clamp that guarantees the persisted profile is never less conservative than the
    /// safety/recovery gate floor. A professional can lower a goal, never raise it past
    /// what Safety/Recovery permit. The hard gates (IsLowRecovery, VocalHealthSupervisor
    /// Lock) are never reachable by an override — it only ever writes a more-conservative
    /// profile.
    ///
    /// Priority hierarchy that this request is subordinate to:
    /// Safety &gt; Health &gt; Recovery &gt; Comfort &gt; Voice Development &gt; Reporting.
    /// </summary>
    public sealed record ManualOverrideRequest
    {
        /// <summary>Which clinical decision is being overridden.</summary>
        public ManualOverrideKind OverrideKind { get; init; }

        /// <summary>The user the override applies to.</summary>
        public int UserId { get; init; }

        /// <summary>The specific catalog exercise targeted (1–15). Null for non-exercise
        /// overrides (e.g. <see cref="ManualOverrideKind.VoiceGoals"/>).</summary>
        public int? ExerciseId { get; init; }

        /// <summary>The exercise target profile the professional intends to apply. Subject
        /// to the two-stage clamp before persistence. Null when the override is not an
        /// exercise-profile change.</summary>
        public ExerciseTargetProfile? IntendedProfile { get; init; }

        /// <summary>The voice goal profile the professional intends to apply. Null when the
        /// override is not a voice-goals change.</summary>
        public VoiceGoalProfile? IntendedGoalProfile { get; init; }

        /// <summary>Stable machine reason code for the override (for audit/logs, never shown
        /// raw). Defaults to empty.</summary>
        public string ReasonCode { get; init; } = string.Empty;

        /// <summary>Role of the actor requesting the override (e.g. "Clinician", "Coach").
        /// Defaults to empty.</summary>
        public string ActorRole { get; init; } = string.Empty;

        /// <summary>When the override was requested.</summary>
        public DateTime RequestedAt { get; init; }
    }

    /// <summary>
    /// The outcome of evaluating a <see cref="ManualOverrideRequest"/> through the
    /// <see cref="FemVoiceStudio.Services.ManualOverrideEngine"/>.
    ///
    /// <see cref="WasApplied"/> is true when an exercise-profile override produced a
    /// persisted profile; <see cref="WasClamped"/> is true when the two-stage clamp
    /// modified the professional's intended values (i.e. the intent was less conservative
    /// than the safety/recovery floor and was pulled back). <see cref="BlockedReasonCode"/>
    /// is set when the request could not be applied as-is (e.g. a non-exercise override
    /// kind that carries no profile to clamp).
    /// </summary>
    public sealed record ManualOverrideResult
    {
        /// <summary>True when the override produced a persisted, clamped profile.</summary>
        public bool WasApplied { get; init; }

        /// <summary>True when the two-stage clamp made the applied profile more
        /// conservative than the professional's intended profile.</summary>
        public bool WasClamped { get; init; }

        /// <summary>Stable reason code when the override was not applied as requested.
        /// Null when the override was applied.</summary>
        public string? BlockedReasonCode { get; init; }

        /// <summary>The actual profile persisted after clamping. Null when nothing was
        /// applied.</summary>
        public ExerciseTargetProfile? AppliedProfile { get; init; }

        /// <summary>Identifier of the audit event recorded for this override.</summary>
        public Guid AuditId { get; init; } = Guid.NewGuid();

        /// <summary>Identifier of the persisted manual-override log row.</summary>
        public Guid ManualOverrideId { get; init; } = Guid.NewGuid();
    }
}
