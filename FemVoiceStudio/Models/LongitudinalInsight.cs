using System.Collections.Generic;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// One longitudinal coaching insight derived from the full voice-development history
    /// (trend windows + detected patterns). Answers the "why" question: which dimension is
    /// trending significantly and why does it matter now?
    ///
    /// CONTRACT (Sprint C.3/C.4 Bølge 1, Agent A2 — FROZEN):
    ///   <see cref="ReasonCode"/> is the stable machine key; never user-facing raw.
    ///   <see cref="What"/> and <see cref="Why"/> are localised strings composed by the
    ///   engine via <see cref="FemVoiceStudio.Services.LocalizationService"/> using the
    ///   frozen RESX keys (Insight_Improvement_Template, Insight_Decline_Template,
    ///   Insight_Stable_Template, Insight_InsufficientData).
    ///   <see cref="Evidence"/> carries short machine-fact strings (window days, slope,
    ///   session count) for tests and explainable UI surfaces — never raw RESX copy.
    ///
    /// Global priority: this is a DESCRIPTIVE/EXPLANATORY insight only. It must never
    /// override a Safety &gt; Health &gt; Recovery gate; that wiring lives upstream.
    /// </summary>
    public sealed record LongitudinalInsight
    {
        /// <summary>
        /// Stable machine reason code — the single token that unit tests assert on.
        /// Examples: "IMPROVEMENT", "DECLINE", "PLATEAU", "BREAKTHROUGH", "REGRESSION",
        /// "STABLE", "INSUFFICIENT_DATA".
        /// Never shown to the user in raw form.
        /// </summary>
        public string ReasonCode { get; init; } = string.Empty;

        /// <summary>
        /// The Voice Intelligence dimension this insight concerns. Uses the real
        /// <see cref="VoiceDimension"/> enum (Recovery=0 … Pitch=6, priority order).
        /// </summary>
        public VoiceDimension Dimension { get; init; }

        /// <summary>
        /// Confidence that the insight reflects a real signal, 0–100. Derived from
        /// the BuildConfidence formula (base 35 + volume + trend + stability).
        /// Lower confidence ⇒ fewer sessions or unstable signal.
        /// </summary>
        public double Confidence { get; init; }

        /// <summary>
        /// Localised "what happened" string, composed by the engine via
        /// LocalizationService + String.Format({0}=dimension, {1}=delta).
        /// Owners of the RESX copy (RES-Strings) decide the exact Norwegian wording.
        /// </summary>
        public string What { get; init; } = string.Empty;

        /// <summary>
        /// Localised "why it matters" string, composed the same way as <see cref="What"/>.
        /// </summary>
        public string Why { get; init; } = string.Empty;

        /// <summary>
        /// Short machine-fact evidence strings for tests and explainable UI.
        /// Each entry carries one numeric fact, e.g. "window=30d", "slope=+2.4", "sessions=8".
        /// Never localised RESX copy — that is the domain of <see cref="What"/>/<see cref="Why"/>.
        /// </summary>
        public IReadOnlyList<string> Evidence { get; init; } = System.Array.Empty<string>();
    }
}
