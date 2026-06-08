using System;
using System.Collections.Generic;
using FemVoiceStudio.Audio;

namespace FemVoiceStudio.Models
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  RESEARCH MODE DATA CONTRACT (Sprint E — Agent 5 Research Mode + Agent 9
    //  Research Analytics).
    //
    //  HARD PRIVACY INVARIANT: nothing in the OUTPUT side of this file may carry a
    //  personally identifiable value. The integer UserId, any device name, any free
    //  text, and the time-of-day component of any timestamp are all re-identification
    //  vectors and are stripped/replaced by the ResearchAnonymizer before a row ever
    //  becomes a <see cref="ResearchParticipantRow"/>. The only stable identifier that
    //  survives is the opaque <see cref="ResearchParticipantRow.ParticipantToken"/>
    //  issued by <see cref="FemVoiceStudio.Services.ParticipantTokenProvider"/>.
    //
    //  This is a REPORTING/RESEARCH layer only. It is the lowest tier of the clinical
    //  hierarchy (Safety > Health > Recovery > Comfort > Voice Development > Reporting)
    //  and never gates, blocks, or influences training.
    // ──────────────────────────────────────────────────────────────────────────────

    // ── RAW (PII-bearing) INPUT shapes ────────────────────────────────────────────

    /// <summary>
    /// A single raw, PII-BEARING per-session measurement row for one local user, BEFORE
    /// anonymization. This is the input the <see cref="FemVoiceStudio.Services.ResearchAnonymizer"/>
    /// consumes; it must NEVER be exported or shared as-is.
    ///
    /// <para><b>Re-identification vectors carried here:</b> the integer
    /// <see cref="UserId"/> and the full <see cref="Timestamp"/> (time-of-day is a
    /// fingerprint). Both are removed by anonymization.</para>
    /// </summary>
    public sealed record RawResearchRow
    {
        /// <summary>Local integer user id. PII — replaced by an opaque token on anonymization.</summary>
        public int UserId { get; init; }

        /// <summary>Full timestamp of the measurement. The time-of-day is a re-identification
        /// vector and is truncated to day granularity on anonymization.</summary>
        public DateTime Timestamp { get; init; }

        /// <summary>Composite voice score for the session (0–100).</summary>
        public double CompositeVoiceScore { get; init; }

        /// <summary>Recovery score for the session (0–100).</summary>
        public double RecoveryScore0to100 { get; init; }

        /// <summary>Catalog exercise practised in this session (1–15), if any.</summary>
        public int? ExerciseId { get; init; }

        /// <summary>Effectiveness score for the practised exercise (0–100), if measured.</summary>
        public double? ExerciseEffectiveness { get; init; }

        /// <summary>True when this session's longitudinal window was flagged as a plateau.</summary>
        public bool PlateauActive { get; init; }

        /// <summary>
        /// Optional raw microphone calibration profile for this session. Its
        /// <see cref="MicrophoneCalibrationProfile.DeviceName"/> is PII and is dropped on
        /// anonymization; only the non-identifying acoustic numbers may survive. Null when
        /// no calibration is attached.
        /// </summary>
        public MicrophoneCalibrationProfile? Calibration { get; init; }

        /// <summary>
        /// Optional free-text subjective note for the session (PII). This field is dropped
        /// ENTIRELY on anonymization — free text can never be guaranteed PII-free.
        /// </summary>
        public string? SubjectiveNote { get; init; }

        /// <summary>
        /// Optional free-text clinical note body for the session (PII). Dropped ENTIRELY on
        /// anonymization for the same reason as <see cref="SubjectiveNote"/>.
        /// </summary>
        public string? ClinicalNoteBody { get; init; }
    }

    // ── ANONYMIZED (PII-free) OUTPUT shapes ───────────────────────────────────────

    /// <summary>
    /// One anonymized, PII-FREE measurement row. This is the only per-session shape that
    /// may be exported or shared.
    ///
    /// <para>By construction it carries NO integer user id, NO device name, NO free text,
    /// and NO time-of-day: the timestamp has been truncated to a calendar
    /// <see cref="DayBucket"/> (midnight UTC) so the hour-of-day fingerprint is gone, and
    /// identity is reduced to the opaque <see cref="ParticipantToken"/>.</para>
    /// </summary>
    public sealed record ResearchParticipantRow
    {
        /// <summary>Opaque, non-reversible participant identifier (a random UUID). Never the
        /// integer UserId.</summary>
        public string ParticipantToken { get; init; } = string.Empty;

        /// <summary>The measurement day, truncated to midnight UTC (date granularity). The
        /// original time-of-day has been discarded to block time-of-day re-identification.</summary>
        public DateTime DayBucket { get; init; }

        /// <summary>Composite voice score for the session (0–100).</summary>
        public double CompositeVoiceScore { get; init; }

        /// <summary>Recovery score for the session (0–100).</summary>
        public double RecoveryScore0to100 { get; init; }

        /// <summary>Catalog exercise id (1–15), if any. A catalog id is not PII.</summary>
        public int? ExerciseId { get; init; }

        /// <summary>Effectiveness score for the practised exercise (0–100), if measured.</summary>
        public double? ExerciseEffectiveness { get; init; }

        /// <summary>True when this session's longitudinal window was flagged as a plateau.</summary>
        public bool PlateauActive { get; init; }

        /// <summary>True when a calibration profile was present and its non-identifying
        /// acoustic numbers were retained (its device name was always dropped).</summary>
        public bool HasCalibration { get; init; }

        /// <summary>Signal-to-noise ratio (dB) from the calibration profile, if present.
        /// A non-identifying acoustic measurement. 0 when no calibration was attached.</summary>
        public double CalibrationSignalToNoiseDb { get; init; }
    }

    // ── GROUP-LEVEL aggregate shapes ──────────────────────────────────────────────

    /// <summary>
    /// Cohort-level mean effectiveness for one catalog exercise across all participants.
    /// Multi-participant by SHAPE even when only one participant contributed (N=1) — the
    /// cohort caveat then lives on the enclosing <see cref="ResearchAggregateMetrics"/>.
    /// </summary>
    public sealed record GroupExerciseEffectiveness
    {
        /// <summary>The catalog exercise id (1–15).</summary>
        public int ExerciseId { get; init; }

        /// <summary>Mean exercise-effectiveness score (0–100) across all contributing
        /// participants and sessions for this exercise.</summary>
        public double MeanEffectiveness { get; init; }

        /// <summary>Number of distinct participants that contributed at least one measured
        /// effectiveness value for this exercise.</summary>
        public int ContributingParticipantCount { get; init; }

        /// <summary>Total number of measured effectiveness sessions for this exercise across
        /// the cohort.</summary>
        public int SessionCount { get; init; }
    }

    /// <summary>
    /// Cohort plateau frequency: the share of participants for whom a plateau was observed
    /// in at least one session. Defined at group level; with N=1 it degenerates to 0 or 1.
    /// </summary>
    public sealed record CohortPlateauFrequency
    {
        /// <summary>Number of participants with at least one plateau-flagged session.</summary>
        public int ParticipantsWithPlateau { get; init; }

        /// <summary>Total number of participants in the cohort.</summary>
        public int ParticipantCount { get; init; }

        /// <summary>Share of participants with a plateau, 0–100. 0 when the cohort is empty.</summary>
        public double PlateauFrequencyPercent { get; init; }
    }

    /// <summary>
    /// Cohort recovery distribution: simple distribution statistics of the per-participant
    /// MEAN recovery score across the cohort. Each participant contributes one mean value,
    /// so an individual's session-level recovery values are never exposed at group level.
    /// </summary>
    public sealed record CohortRecoveryDistribution
    {
        /// <summary>Mean of the per-participant mean recovery scores (0–100).</summary>
        public double Mean { get; init; }

        /// <summary>Minimum per-participant mean recovery score (0–100). 0 when empty.</summary>
        public double Min { get; init; }

        /// <summary>Maximum per-participant mean recovery score (0–100). 0 when empty.</summary>
        public double Max { get; init; }

        /// <summary>Number of participants contributing a recovery mean.</summary>
        public int ParticipantCount { get; init; }
    }

    /// <summary>
    /// The full bundle of group-level aggregate metrics for a research export.
    /// </summary>
    public sealed record ResearchAggregateMetrics
    {
        /// <summary>Per-exercise cohort effectiveness, ranked by mean effectiveness
        /// descending then exercise id ascending.</summary>
        public IReadOnlyList<GroupExerciseEffectiveness> ExerciseEffectiveness { get; init; } =
            Array.Empty<GroupExerciseEffectiveness>();

        /// <summary>Cohort plateau frequency.</summary>
        public CohortPlateauFrequency PlateauFrequency { get; init; } = new();

        /// <summary>Cohort recovery distribution.</summary>
        public CohortRecoveryDistribution RecoveryDistribution { get; init; } = new();
    }

    // ── TOP-LEVEL dataset ─────────────────────────────────────────────────────────

    /// <summary>
    /// A complete, PII-FREE research dataset ready for export or sharing.
    ///
    /// <para><b>N=1 VOLUME CAVEAT.</b> Research analytics is multi-participant by shape,
    /// but a single local install usually yields exactly ONE participant. When
    /// <see cref="ParticipantCount"/> is below the documented cohort threshold
    /// (<see cref="FemVoiceStudio.Services.ResearchAggregator.MinimumCohortSize"/>),
    /// <see cref="HasSufficientCohort"/> is false and <see cref="VolumeCaveatNote"/>
    /// carries a human-readable warning that group statistics must be read as a single
    /// case, not as a generalisable cohort finding. The valid single-participant
    /// longitudinal series in <see cref="DayBucketedRows"/> is STILL computed and emitted
    /// at N=1 — only the cohort-generalisation claim is withheld.</para>
    ///
    /// <para>Every field here is non-identifying by construction. There is deliberately no
    /// integer user id, device name, or free-text field anywhere in this record graph.</para>
    /// </summary>
    public sealed record ResearchDataset
    {
        /// <summary>The opaque participant token this dataset was built for. For a multi-
        /// participant cohort this is the token of the install that produced the export;
        /// individual rows each carry their own token in <see cref="DayBucketedRows"/>.</summary>
        public string ParticipantToken { get; init; } = string.Empty;

        /// <summary>All anonymized, day-bucketed measurement rows. Always populated when any
        /// input existed — emitted even at N=1.</summary>
        public IReadOnlyList<ResearchParticipantRow> DayBucketedRows { get; init; } =
            Array.Empty<ResearchParticipantRow>();

        /// <summary>Group-level aggregate metrics. Computed for any participant count; read
        /// the caveat when <see cref="HasSufficientCohort"/> is false.</summary>
        public ResearchAggregateMetrics AggregateMetrics { get; init; } = new();

        /// <summary>True only when <see cref="ParticipantCount"/> meets the documented
        /// minimum cohort size. False at N=1 (and below threshold generally).</summary>
        public bool HasSufficientCohort { get; init; }

        /// <summary>Number of distinct participants contributing to this dataset.</summary>
        public int ParticipantCount { get; init; }

        /// <summary>Human-readable caveat when the cohort is too small to generalise.
        /// Empty when <see cref="HasSufficientCohort"/> is true.</summary>
        public string VolumeCaveatNote { get; init; } = string.Empty;
    }
}
