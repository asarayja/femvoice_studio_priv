using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Coarse-grained learning stage for the adaptive learning path (Sprint C, Agent LP).
    ///
    /// A readable, five-step mapping derived from the (7-level) clinical
    /// <see cref="SpeechComplexityLevel"/> ladder AND per-exercise mastery maturity.
    /// It is a PRESENTATION/PLANNING abstraction over the complexity ladder — it never
    /// gates progression itself (the <see cref="FemVoiceStudio.Services.Progression.ComplexityEngine"/>
    /// remains the single source of truth for what unlocks). Stage answers the softer
    /// question "where, broadly, is this learner on their journey?".
    ///
    /// Mapping (documented, deterministic):
    ///   IsolatedSounds, Syllables                 ⇒ Foundation
    ///   Words, Phrases                            ⇒ Building
    ///   StructuredSentences                       ⇒ Refining
    ///   SpontaneousSpeech                         ⇒ Integrating
    ///   Conversational                            ⇒ Maintaining
    ///
    /// Mastery nuance (never lowers a complexity-derived stage; can only confirm it):
    ///   When the complexity level sits at the top of a stage band AND mastery is
    ///   <see cref="MasteryLevel.Mastered"/>, the stage is allowed to advance by one
    ///   step (capped at Maintaining) to reflect a learner who has consolidated the
    ///   current band and is functionally operating in the next. This is additive and
    ///   bounded — it can never push the learner past Maintaining and never demotes.
    /// </summary>
    public enum LearningStage
    {
        /// <summary>Earliest work: isolated sounds / syllables. Establishing the basics.</summary>
        Foundation = 0,

        /// <summary>Words and short phrases. Building blocks of connected speech.</summary>
        Building = 1,

        /// <summary>Structured sentences. Refining control over longer productions.</summary>
        Refining = 2,

        /// <summary>Spontaneous speech. Integrating skills into less scripted output.</summary>
        Integrating = 3,

        /// <summary>Natural conversation. Maintaining and generalising the voice.</summary>
        Maintaining = 4
    }

    /// <summary>
    /// Confidence the learning-path picture is well-founded. Derived from how much
    /// CONSISTENT, POSITIVE history backs it (see <see cref="LearningPathProfile.ConfidenceScore"/>).
    /// Low/empty history ⇒ a modest starting confidence, never zero — a new learner's
    /// path is reasonable, just not yet evidenced.
    /// </summary>
    public enum LearningConfidenceLevel
    {
        /// <summary>Little or no consistent history yet — picture is provisional.</summary>
        Emerging = 0,

        /// <summary>Some consistent history — picture is reasonably grounded.</summary>
        Moderate = 1,

        /// <summary>Substantial consistent, improving history — picture is well grounded.</summary>
        Established = 2
    }

    /// <summary>
    /// One Voice Intelligence dimension paired with its latest 0–100 score and a short
    /// explanation. Used for both <see cref="LearningPathProfile.Strengths"/> and
    /// <see cref="LearningPathProfile.Weaknesses"/>. Carries the canonical dimension
    /// identity (<see cref="VoiceDimension"/>) so consumers can resolve display text via
    /// RESX rather than relying on this raw string.
    /// </summary>
    public sealed record DimensionAssessment
    {
        /// <summary>Which Voice Intelligence dimension this assessment is for.</summary>
        public required VoiceDimension Dimension { get; init; }

        /// <summary>Latest 0–100 score for the dimension (from the most recent trend point).</summary>
        public required double Score { get; init; }

        /// <summary>Short, neutral, non-pressuring explanation (explainable contract).</summary>
        public string Explanation { get; init; } = string.Empty;
    }

    /// <summary>
    /// The seven Voice Intelligence dimensions, in the clinical FOCUS hierarchy used by
    /// <see cref="LearningPathProfile.ActiveFocusAreas"/>:
    /// Recovery &gt; Comfort &gt; Resonance &gt; Consistency &gt; Intonation &gt; VocalWeight &gt; Pitch.
    /// (Recovery/Comfort = Health, which always leads; Pitch always trails.) The integer
    /// values encode that priority order — lower value = higher focus priority — so the
    /// hierarchy can be sorted on directly.
    /// </summary>
    public enum VoiceDimension
    {
        /// <summary>Recovery / restitution (Health). Highest focus priority.</summary>
        Recovery = 0,

        /// <summary>Comfort / ease (Health).</summary>
        Comfort = 1,

        /// <summary>Resonance — primary feminisation training dimension.</summary>
        Resonance = 2,

        /// <summary>Consistency — frame-to-frame stability.</summary>
        Consistency = 3,

        /// <summary>Intonation — natural pitch-variation.</summary>
        Intonation = 4,

        /// <summary>VocalWeight — spectral weight / tilt.</summary>
        VocalWeight = 5,

        /// <summary>Pitch — fundamental frequency. Lowest focus priority.</summary>
        Pitch = 6
    }

    /// <summary>
    /// A recommended exercise plus the reason it was surfaced. A LIGHT DTO so Sprint C
    /// Bølge 2's recommender (Agent 2) can later replace the provisional id mapping
    /// without changing this public shape.
    /// </summary>
    public sealed record RecommendedExercise
    {
        /// <summary>The exercise id (matches <c>ComplexityEngine.GetExerciseIdsForComplexity</c>).</summary>
        public required int ExerciseId { get; init; }

        /// <summary>The focus dimension this recommendation targets, if any.</summary>
        public VoiceDimension? TargetDimension { get; init; }

        /// <summary>Short, neutral reason this exercise was recommended.</summary>
        public string Reason { get; init; } = string.Empty;
    }

    /// <summary>
    /// A light, IO-free snapshot of the recovery picture, copied out of
    /// <see cref="FemVoiceStudio.Services.RecoveryResult"/> so the profile model does not
    /// depend on the Services layer. Recovery sits at the very top of the global priority
    /// order (Safety &gt; Health &gt; Recovery &gt; …) — these requirements can never be
    /// overridden by goals or coaching.
    /// </summary>
    public sealed record RecoveryRequirement
    {
        /// <summary>Recovery score, 0 (overtrained) … 100 (fully rested).</summary>
        public required double Score { get; init; }

        /// <summary>Coarse recovery status bucket (Overtrained/Strained/Adequate/WellRecovered).</summary>
        public required string Status { get; init; }

        /// <summary>True when recovery is below the "ready to train normally" line —
        /// i.e. status is Strained or Overtrained. When true, recovery takes precedence
        /// over any progression/goal/coaching intent.</summary>
        public required bool RestRecommended { get; init; }

        /// <summary>Short, neutral explanation of the recovery picture (explainable).</summary>
        public string Explanation { get; init; } = string.Empty;
    }

    /// <summary>
    /// The LearningPathProfile aggregate (Sprint C, Agent LP): ONE explainable model that
    /// gathers the previously-scattered learning signals — stage, strengths/weaknesses,
    /// active focus areas, recommended exercises, recovery requirements and confidence —
    /// into a single shape the coaching/UI layers can reason over.
    ///
    /// It is a pure VALUE: every part is precomputed by
    /// <see cref="FemVoiceStudio.Services.LearningPathProfileBuilder"/> from already-fetched
    /// inputs, and every part carries a short neutral explanation (the "explainable
    /// learning path" contract). It contains no raw Hz and no user-facing prose that
    /// bypasses RESX — explanations are diagnostic/traceability text the coaching layer
    /// routes through localisation + clinical-language policy before display.
    ///
    /// PRIORITY: nothing in this model overrides the global order
    /// (Safety &gt; Health &gt; Recovery &gt; Comfort &gt; Goals &gt; Progression &gt; Coaching).
    /// <see cref="RecoveryRequirements"/> is authoritative over goals/coaching.
    /// </summary>
    public sealed record LearningPathProfile
    {
        /// <summary>Broad learning stage (see <see cref="LearningStage"/> for the mapping).</summary>
        public required LearningStage CurrentStage { get; init; }

        /// <summary>Short, neutral explanation of why this stage was chosen.</summary>
        public string StageExplanation { get; init; } = string.Empty;

        /// <summary>
        /// Strongest Voice Intelligence dimensions (highest latest score first). Empty
        /// when there is no history yet.
        /// </summary>
        public IReadOnlyList<DimensionAssessment> Strengths { get; init; } =
            Array.Empty<DimensionAssessment>();

        /// <summary>
        /// Weakest Voice Intelligence dimensions (lowest latest score first). Empty when
        /// there is no history yet.
        /// </summary>
        public IReadOnlyList<DimensionAssessment> Weaknesses { get; init; } =
            Array.Empty<DimensionAssessment>();

        /// <summary>
        /// The dimensions to actively work on now, ordered by the clinical FOCUS
        /// hierarchy (Recovery &gt; Comfort &gt; Resonance &gt; Consistency &gt; Intonation
        /// &gt; VocalWeight &gt; Pitch). ONLY dimensions that are genuinely weak (latest
        /// score below the weakness threshold) appear — a learner with no weak dimension
        /// gets an empty list, never a fabricated focus.
        /// </summary>
        public IReadOnlyList<VoiceDimension> ActiveFocusAreas { get; init; } =
            Array.Empty<VoiceDimension>();

        /// <summary>Provisional exercise recommendations derived from the focus areas
        /// (Bølge 2's recommender replaces this without changing the shape).</summary>
        public IReadOnlyList<RecommendedExercise> RecommendedExercises { get; init; } =
            Array.Empty<RecommendedExercise>();

        /// <summary>The recovery picture — authoritative over goals/coaching.</summary>
        public required RecoveryRequirement RecoveryRequirements { get; init; }

        /// <summary>Coarse confidence bucket for the whole picture.</summary>
        public required LearningConfidenceLevel ConfidenceLevel { get; init; }

        /// <summary>Continuous 0–100 confidence backing <see cref="ConfidenceLevel"/>.</summary>
        public required double ConfidenceScore { get; init; }

        /// <summary>Short, neutral explanation of the confidence value.</summary>
        public string ConfidenceExplanation { get; init; } = string.Empty;

        /// <summary>When the profile was built.</summary>
        public DateTime ComputedAt { get; init; } = DateTime.Now;
    }
}
