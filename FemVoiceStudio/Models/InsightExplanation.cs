using FemVoiceStudio.Models;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// A structured explanation of why a particular voice dimension has been chosen as the
    /// focus for a recommendation, and what practising the associated exercise is expected
    /// to improve. Produced by <see cref="FemVoiceStudio.Services.RecommendationExplanationEngine"/>.
    ///
    /// CONTRACT (Sprint C.3/C.4 Bølge 1, Agent A7 — FROZEN):
    ///   <see cref="ReasonCode"/> is the stable machine key used by tests and routing logic.
    ///   All human-facing strings (<see cref="WhyThisFocus"/>, <see cref="WhatItImproves"/>,
    ///   <see cref="ExpectedOutcome"/>, <see cref="ConfidenceLabel"/>) are resolved via
    ///   LocalizationService using the frozen RESX keys
    ///   (Explanation_WhyFocus_Template, Explanation_WhatItImproves_Template,
    ///   Explanation_ExpectedOutcome_Template, Explanation_Confidence_High/Medium/Low).
    ///
    /// CLINICAL FRAMING: this is DESCRIPTIVE / EXPLANATORY intelligence only. It must never
    /// override or substitute for the Safety &gt; Health &gt; Recovery hierarchy. A low-data
    /// or unknown-effectiveness path emits <c>ReasonCode = "INSUFFICIENT_EVIDENCE"</c> and
    /// explains the lack of evidence rather than fabricating a claim.
    /// </summary>
    public sealed record InsightExplanation
    {
        /// <summary>
        /// Stable machine reason code — the single token tests assert on.
        /// Examples: "WEAKEST_DIMENSION", "MOST_GAIN_POTENTIAL", "RECOVERY_FOCUS",
        /// "COMFORT_FOCUS", "INSUFFICIENT_EVIDENCE".
        /// Never shown to the user in raw form.
        /// </summary>
        public string ReasonCode { get; init; } = string.Empty;

        /// <summary>
        /// The voice dimension this explanation centres on. Uses the real
        /// <see cref="VoiceDimension"/> enum (Recovery=0 … Pitch=6, priority order).
        /// </summary>
        public VoiceDimension Focus { get; init; }

        /// <summary>
        /// Localised "why this dimension was chosen" string, composed by the engine via
        /// LocalizationService + Explanation_WhyFocus_Template. Explains whether the
        /// dimension is the weakest, most improvable, or most clinically relevant.
        /// </summary>
        public string WhyThisFocus { get; init; } = string.Empty;

        /// <summary>
        /// Localised "what the exercise improves" string, composed via
        /// Explanation_WhatItImproves_Template. Session-level framing when gain is
        /// ambiguous between per-session and per-exercise.
        /// </summary>
        public string WhatItImproves { get; init; } = string.Empty;

        /// <summary>
        /// Localised expected-outcome string, composed via Explanation_ExpectedOutcome_Template.
        /// Combines effectiveness evidence (slope, success rate) with trend direction.
        /// Falls back to a neutral "insufficient evidence" phrasing when
        /// <see cref="ReasonCode"/> is "INSUFFICIENT_EVIDENCE".
        /// </summary>
        public string ExpectedOutcome { get; init; } = string.Empty;

        /// <summary>
        /// Localised confidence label resolved from one of the three frozen keys:
        /// Explanation_Confidence_High (≥70), Explanation_Confidence_Medium (≥40),
        /// Explanation_Confidence_Low (&lt;40).
        /// </summary>
        public string ConfidenceLabel { get; init; } = string.Empty;
    }
}
