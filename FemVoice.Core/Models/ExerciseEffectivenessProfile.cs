using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Per-exercise EFFECTIVENESS intelligence (Sprint C.2, Agent EFF) derived from the
    /// persisted analytics history of one catalog exercise (ExerciseId 1–15).
    ///
    /// ── HONEST PROVENANCE ────────────────────────────────────────────────────────────
    /// FemVoice does NOT capture a true clinical before/after measurement for an exercise.
    /// Every "Gain" here is therefore an EXPLAINABLE PROXY: the ordinary-least-squares
    /// slope of a per-session metric over the exercise's own chronological trend (units =
    /// metric-points per session step). A positive slope means the metric improved while
    /// the user practised this exercise; it is NOT a causal claim that the exercise caused
    /// the change. <see cref="HasEnoughData"/> is false when too few sessions exist to
    /// trust the slope — consumers MUST treat a low-data profile as "insufficient evidence"
    /// rather than "ineffective", so the UI never misleads.
    ///
    /// This record is DATA/INSIGHT for the Health/Coaching layers. A high
    /// <see cref="RecoveryCost"/> or a safety flag means the exercise should be
    /// DE-PRIORITISED in recommendations — it is never itself a safety BLOCK. The
    /// ProgressionSafetyGate and the health-gates remain the only authorities that stop
    /// training.
    /// </summary>
    public sealed record ExerciseEffectivenessProfile
    {
        /// <summary>The catalog exercise this profile describes (1–15).</summary>
        public int ExerciseId { get; init; }

        /// <summary>
        /// OLS slope of ResonanceQualityIndex×100 across the exercise's chronological
        /// session trend (points per session). Positive ⇒ resonance improved across the
        /// practised sessions. 0 when flat or with too little data.
        /// </summary>
        public double ResonanceGain { get; init; }

        /// <summary>
        /// OLS slope of the ComfortScore100 of the sessions that ran this exercise
        /// (joined per SessionId from the Voice Intelligence trend; one session = one
        /// exercise). 0 with an explanation of "comfort data unavailable" when no comfort
        /// points could be joined.
        /// </summary>
        public double ComfortGain { get; init; }

        /// <summary>True when at least one ComfortScore100 point could be joined per
        /// SessionId, so <see cref="ComfortGain"/> reflects real comfort history rather
        /// than a neutral fallback.</summary>
        public bool HasComfortData { get; init; }

        /// <summary>
        /// OLS slope of StabilityConsistency×100 across the exercise's chronological
        /// session trend (points per session). Positive ⇒ steadier production over time.
        /// </summary>
        public double ConsistencyGain { get; init; }

        /// <summary>
        /// Recovery/strain COST of practising this exercise, 0–100 (higher = more
        /// taxing). Blends the average per-session fatigue indicators and safety events
        /// from the exercise summaries with the per-session strain/pause health-event
        /// density. This is a Health-layer insight: a high cost means de-prioritise, not
        /// a safety block.
        /// </summary>
        public double RecoveryCost { get; init; }

        /// <summary>
        /// Share of sessions that cleared the per-exercise success gate, 0–100. A session
        /// succeeds when its HoldCompletionRate AND ResonanceQualityIndex both meet the
        /// success thresholds (the same shape the MasteryEvaluator gates use). 0 with too
        /// little data.
        /// </summary>
        public double UserSuccessRate { get; init; }

        /// <summary>Number of sessions of this exercise contributing to the profile.</summary>
        public int SessionCount { get; init; }

        /// <summary>False when <see cref="SessionCount"/> is below the minimum needed to
        /// trust the slopes — the profile must then be read as "not enough evidence",
        /// never as "ineffective".</summary>
        public bool HasEnoughData { get; init; }

        /// <summary>
        /// A single composite EFFECTIVENESS index, 0–100, used only for RANKING (not a
        /// clinical score): it rewards resonance/comfort/consistency gains and penalises
        /// recovery cost, anchored at a neutral midpoint. Profiles without enough data sit
        /// at the neutral midpoint so they never out- or under-rank evidenced exercises.
        /// </summary>
        public double CompositeEffectiveness { get; init; }

        /// <summary>Plain, explainable summary of how the numbers were derived (for
        /// logs/tests and traceable UI copy). Never a raw Hz value.</summary>
        public string Explanation { get; init; } = string.Empty;
    }

    /// <summary>
    /// A single effectiveness SAFETY/HEALTH flag (Agent 6). DATA-driven insight only: it
    /// marks an exercise that recent history shows is taxing or comfort-eroding, so a
    /// recommender should de-prioritise it. It NEVER blocks training — the
    /// ProgressionSafetyGate and health-gates are the only authorities for that.
    /// <see cref="ReasonCode"/> is for logs/tests; <see cref="Explanation"/> is the
    /// human-readable rationale.
    /// </summary>
    public sealed record ExerciseEffectivenessFlag
    {
        public int ExerciseId { get; init; }

        /// <summary>Stable machine code (e.g. HIGH_RECOVERY_COST) — never shown raw.</summary>
        public string ReasonCode { get; init; } = string.Empty;

        /// <summary>Explainable rationale for the flag.</summary>
        public string Explanation { get; init; } = string.Empty;

        /// <summary>The driving metric value (recovery cost, fatigue, or comfort slope)
        /// that crossed its threshold, for traceability.</summary>
        public double Magnitude { get; init; }
    }
}
