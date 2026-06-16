using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// One explainable, 0–100 score for a single voice dimension.
    /// Carries a short human-readable <see cref="Explanation"/> (the "explainable"
    /// contract) so any consumer can show <em>why</em> the number is what it is.
    ///
    /// NB: this is intentionally distinct from <c>Models.VoiceMetrics</c> (the raw
    /// per-frame DTO). This type is part of the higher-level
    /// <see cref="VoiceIntelligenceScores"/> aggregate.
    /// </summary>
    public sealed record DimensionScore
    {
        /// <summary>Dimension score, clamped to 0–100.</summary>
        public double Score { get; }

        /// <summary>Short, clinician-friendly explanation of the score.</summary>
        public string Explanation { get; }

        public DimensionScore(double score, string explanation)
        {
            Score = Math.Clamp(double.IsNaN(score) ? 0.0 : score, 0.0, 100.0);
            Explanation = explanation ?? string.Empty;
        }

        /// <summary>Neutral fallback (50) used when a signal is missing.</summary>
        public static DimensionScore Neutral(string explanation) =>
            new DimensionScore(50.0, explanation);
    }

    /// <summary>
    /// The central voice-intelligence aggregate: seven explainable, traceable
    /// 0–100 dimension scores plus one hierarchy-weighted
    /// <see cref="CompositeVoiceScore"/> (also 0–100).
    ///
    /// HIERARCHY (clinical): Health &gt; Comfort &gt; Resonance &gt; Consistency &gt;
    /// Intonation &gt; Pitch. Pitch is never the dominant weight; Comfort + Recovery
    /// (= Health) jointly outweigh Resonance, the single strongest <em>training</em>
    /// dimension. The composite is a MEASUREMENT, not a safety gate — health/safety
    /// gates live elsewhere and must never be overridden by this number.
    ///
    /// Traceability: <see cref="RawInputs"/> carries the raw aggregates each score was
    /// derived from, so a score can always be traced back to its inputs.
    /// </summary>
    public sealed record VoiceIntelligenceScores
    {
        /// <summary>Resonance — primary feminisation dimension. Highest training weight.</summary>
        public required DimensionScore Resonance { get; init; }

        /// <summary>Comfort — perceived ease / absence of comfort-zone breaches (Health).</summary>
        public required DimensionScore Comfort { get; init; }

        /// <summary>Consistency — frame-to-frame stability of production.</summary>
        public required DimensionScore Consistency { get; init; }

        /// <summary>Intonation — natural pitch-variation patterns.</summary>
        public required DimensionScore Intonation { get; init; }

        /// <summary>VocalWeight — perceived spectral "weight"/tilt (lighter = stronger feminisation).</summary>
        public required DimensionScore VocalWeight { get; init; }

        /// <summary>Recovery — restitution / how unloaded the voice is (Health).</summary>
        public required DimensionScore Recovery { get; init; }

        /// <summary>Pitch — fundamental frequency. LOWEST composite weight; never dominant.</summary>
        public required DimensionScore Pitch { get; init; }

        /// <summary>Hierarchy-weighted composite, 0–100. A measurement, not a gate.</summary>
        public required double CompositeVoiceScore { get; init; }

        /// <summary>When the aggregate was computed.</summary>
        public DateTime ComputedAt { get; init; } = DateTime.Now;

        /// <summary>
        /// Traceability map: the raw session aggregates the scores were derived from
        /// (e.g. "averageResonance01", "comfortCompliance01", "comfortBreaches",
        /// "averageStability01", "intonationRange", "pitchHz", plus the vocal-weight /
        /// recovery raw inputs). Lets any consumer trace a score back to its inputs.
        /// </summary>
        public IReadOnlyDictionary<string, double> RawInputs { get; init; } =
            new Dictionary<string, double>();

        /// <summary>
        /// Human-readable breakdown of all seven dimensions plus the composite,
        /// one line each, in hierarchy order (Health-first, Pitch last). Used by the
        /// "explainable" UI surfaces and by tests.
        /// </summary>
        public string BuildBreakdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"CompositeVoiceScore: {CompositeVoiceScore:0.0}/100"));
            Append(sb, "Comfort", Comfort);
            Append(sb, "Recovery", Recovery);
            Append(sb, "Resonance", Resonance);
            Append(sb, "Consistency", Consistency);
            Append(sb, "Intonation", Intonation);
            Append(sb, "VocalWeight", VocalWeight);
            Append(sb, "Pitch", Pitch);
            return sb.ToString().TrimEnd();
        }

        private static void Append(StringBuilder sb, string name, DimensionScore d) =>
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{name}: {d.Score:0.0}/100 — {d.Explanation}"));
    }
}
