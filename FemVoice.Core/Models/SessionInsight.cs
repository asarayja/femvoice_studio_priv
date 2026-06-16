using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// One explainable, positive-framed improvement: a dimension whose score rose since
    /// the previous session(s). Carries the structured delta so the UI can localise its
    /// own copy, plus a ready-made, clinically safe <see cref="Explanation"/> string.
    /// </summary>
    public sealed record DimensionImprovement
    {
        /// <summary>Which dimension improved.</summary>
        public required VoiceDimension Dimension { get; init; }

        /// <summary>This session's score for the dimension (0–100).</summary>
        public required double CurrentScore { get; init; }

        /// <summary>The prior reference score this is measured against (0–100).</summary>
        public required double PreviousScore { get; init; }

        /// <summary>Positive delta (current − previous), always &gt; 0 for an improvement.</summary>
        public double Delta => CurrentScore - PreviousScore;

        /// <summary>
        /// Short, encouraging, exploration-framed explanation, e.g.
        /// "Resonance +6 since last session". Safe for clinical surfaces; when promoted to
        /// RESX it must pass <see cref="ClinicalLanguagePolicy"/>.
        /// </summary>
        public required string Explanation { get; init; }
    }

    /// <summary>
    /// One detected risk flag for the session: a health/strain/comfort signal that a
    /// caller may want to surface gently. <see cref="ReasonCode"/> is a stable, machine
    /// key (never user-facing); <see cref="Description"/> is the calm, non-shaming copy.
    /// </summary>
    public sealed record SessionRisk
    {
        /// <summary>Stable machine code, e.g. "SAFETY_LOCK", "STRAIN_DETECTED",
        /// "COMFORT_BREACH". Never shown to the user.</summary>
        public required string ReasonCode { get; init; }

        /// <summary>Number of occurrences that drove the flag (episodes / detections).</summary>
        public required int Count { get; init; }

        /// <summary>Calm, non-shaming, clinically safe description of the signal.</summary>
        public required string Description { get; init; }
    }

    /// <summary>
    /// The recovery snapshot carried on a <see cref="SessionInsight"/>: a thin, immutable
    /// projection of <see cref="RecoveryResult"/> so the insight does not leak the scorer
    /// type and stays trivially serialisable / testable.
    /// </summary>
    public sealed record RecoveryNeed
    {
        /// <summary>Recovery score, 0 (overtrained) … 100 (well rested).</summary>
        public required double Score { get; init; }

        /// <summary>Coarse recovery status bucket.</summary>
        public required RecoveryStatus Status { get; init; }

        /// <summary>Explainable summary of the dominant recovery drivers.</summary>
        public required string Explanation { get; init; }

        /// <summary>
        /// True when recovery is below the "Adequate" band (Strained / Overtrained) — a
        /// hint to the UI that recovery should take visible priority over training goals.
        /// Health/recovery gating itself lives elsewhere; this is a presentation hint only.
        /// </summary>
        public bool NeedsAttention => Status is RecoveryStatus.Strained or RecoveryStatus.Overtrained;

        /// <summary>Projects a <see cref="RecoveryResult"/> into the insight DTO.</summary>
        public static RecoveryNeed FromResult(RecoveryResult result) => new()
        {
            Score = result.Score,
            Status = result.Status,
            Explanation = result.Explanation ?? string.Empty
        };
    }

    /// <summary>
    /// The end-of-session insight aggregate: a single, explainable summary assembled at
    /// session end from the per-session outcome, the freshly computed Voice Intelligence
    /// scores, the recovery snapshot and the prior trend history.
    ///
    /// CLINICAL FRAMING (non-negotiable): every user-visible string here is calm,
    /// exploratory and mastery-framed — never shaming, never pressuring. The aggregate is
    /// a MEASUREMENT/REFLECTION surface, not a gate: it must never override the
    /// Safety &gt; Health &gt; Recovery hierarchy. <see cref="RecoveryNeeds"/> is reported so
    /// the UI can give recovery visible priority, but the gating decision lives upstream.
    ///
    /// This type is pure data. <see cref="SessionInsightBuilder"/> assembles it; Bølge 2
    /// wires the builder into the recorder / ExerciseWindow.
    /// </summary>
    public sealed record SessionInsight
    {
        /// <summary>The session this insight describes (0 when not associated with one).</summary>
        public int SessionId { get; init; }

        /// <summary>When the insight was assembled.</summary>
        public DateTime GeneratedAt { get; init; } = DateTime.Now;

        /// <summary>This session's Voice Intelligence composite (0–100). A measurement.</summary>
        public double CompositeVoiceScore { get; init; }

        /// <summary>True when there was no prior history to compare against (first session):
        /// <see cref="Improvements"/> is then empty by definition, but the insight is still
        /// valid and encouraging.</summary>
        public bool IsFirstSession { get; init; }

        /// <summary>Dimensions that rose since the previous session(s), strongest delta
        /// first. Empty on a first session or when nothing improved beyond the threshold.</summary>
        public IReadOnlyList<DimensionImprovement> Improvements { get; init; } =
            Array.Empty<DimensionImprovement>();

        /// <summary>Health/strain/comfort flags detected this session, most severe first.
        /// Empty when the session was clean.</summary>
        public IReadOnlyList<SessionRisk> Risks { get; init; } = Array.Empty<SessionRisk>();

        /// <summary>Recovery snapshot — always present so the UI can surface restitution.</summary>
        public required RecoveryNeed RecoveryNeeds { get; init; }

        /// <summary>The single dimension to gently explore next: the weakest Voice
        /// Intelligence dimension (hierarchy tie-break). Always set.</summary>
        public required VoiceDimension SuggestedFocus { get; init; }

        /// <summary>Exercise ids matched to <see cref="SuggestedFocus"/>. A light static
        /// mapping today; Bølge 2 replaces it with the Agent-2 recommender.</summary>
        public IReadOnlyList<int> SuggestedExercises { get; init; } = Array.Empty<int>();

        /// <summary>
        /// Short, clinically safe, encouraging summary line for the user. Assembled from the
        /// structured fields above (mastery/exploration angle, no shame/pressure). When this
        /// is promoted to RESX it must pass <see cref="ClinicalLanguagePolicy"/>.
        /// </summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable, deterministic breakdown of the whole insight — focus, improvements,
        /// risks and recovery — one line each. Used by explainable UI surfaces and by tests.
        /// </summary>
        public string BuildBreakdown()
        {
            var lines = new List<string>
            {
                string.Create(CultureInfo.InvariantCulture,
                    $"Composite: {CompositeVoiceScore:0.0}/100 (focus: {SuggestedFocus})")
            };

            if (Improvements.Count == 0)
            {
                lines.Add(IsFirstSession
                    ? "Improvements: none yet — first session, nothing to compare against."
                    : "Improvements: none above threshold this session.");
            }
            else
            {
                foreach (var i in Improvements)
                    lines.Add(string.Create(CultureInfo.InvariantCulture,
                        $"Improvement: {i.Dimension} +{i.Delta:0.0} ({i.PreviousScore:0.0} → {i.CurrentScore:0.0})"));
            }

            if (Risks.Count == 0)
                lines.Add("Risks: none detected.");
            else
                foreach (var r in Risks)
                    lines.Add($"Risk: {r.ReasonCode} ×{r.Count} — {r.Description}");

            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"Recovery: {RecoveryNeeds.Score:0}/100 ({RecoveryNeeds.Status})"));

            if (SuggestedExercises.Count > 0)
                lines.Add("Suggested exercises: " +
                    string.Join(", ", SuggestedExercises.Select(e => e.ToString(CultureInfo.InvariantCulture))));

            return string.Join(Environment.NewLine, lines);
        }
    }
}
