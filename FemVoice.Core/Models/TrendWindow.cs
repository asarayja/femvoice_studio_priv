using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// An immutable snapshot of voice-dimension trend statistics for one time window.
    /// Produced by <see cref="FemVoiceStudio.Services.TrendEngineService"/> for each of the
    /// canonical window spans (7 / 30 / 90 / 180 days) and consumed by
    /// <see cref="VoiceDevelopmentProfile"/> (A3) and the Plateau/Breakthrough/Regression
    /// detector (A56).
    ///
    /// CLINICAL FRAMING: slopes and statistics are DESCRIPTIVE / EXPLANATORY. They must
    /// never override or substitute for the Safety &gt; Health &gt; Recovery hierarchy.
    /// Consumers must treat <see cref="HasEnoughData"/> == false as "insufficient evidence",
    /// never as "no development".
    ///
    /// Window convention: closed-open [<see cref="From"/>, <see cref="To"/>). A session at
    /// exactly <c>From</c> is included; a session at exactly <c>To</c> is excluded.
    ///
    /// Slope unit: OLS regression over x = 0, 1, 2, … (session index). The unit is
    /// score-points per session step, not per day. The same OLS formula is used by
    /// LearningPathProfileBuilder.LinearSlope and ExerciseEffectivenessEngine.LinearSlope.
    /// </summary>
    public sealed record TrendWindow
    {
        /// <summary>
        /// Duration of this window in calendar days. One of 7, 30, 90, or 180.
        /// </summary>
        public int WindowDays { get; init; }

        /// <summary>Inclusive start of the window (UTC). Sessions at exactly this time are included.</summary>
        public DateTime From { get; init; }

        /// <summary>Exclusive end of the window (UTC). Sessions at exactly this time are excluded.</summary>
        public DateTime To { get; init; }

        /// <summary>
        /// OLS slope per session step for each <see cref="VoiceDimension"/> (0–100 score
        /// points per session index increment). An empty dictionary means no session data
        /// was present in this window. Keys are the real enum values — never string names.
        /// </summary>
        public IReadOnlyDictionary<VoiceDimension, double> DimensionSlopes { get; init; } =
            new Dictionary<VoiceDimension, double>();

        /// <summary>
        /// OLS slope of the hierarchy-weighted composite voice score per session step
        /// (score-points per session index). 0 when fewer than 2 sessions are present.
        /// </summary>
        public double CompositeSlope { get; init; }

        /// <summary>
        /// Arithmetic mean of the composite voice score (0–100) across all sessions in
        /// this window. 0 when no sessions are present.
        /// </summary>
        public double CompositeMean { get; init; }

        /// <summary>
        /// Minimum composite voice score (0–100) observed in this window.
        /// 0 when no sessions are present.
        /// </summary>
        public double CompositeMin { get; init; }

        /// <summary>
        /// Maximum composite voice score (0–100) observed in this window.
        /// 0 when no sessions are present.
        /// </summary>
        public double CompositeMax { get; init; }

        /// <summary>
        /// Number of sessions whose <c>StartedAt</c> falls in <c>[From, To)</c>.
        /// </summary>
        public int SessionCount { get; init; }

        /// <summary>
        /// Confidence score (0–100) that the statistics in this window are trustworthy.
        /// Follows the same BuildConfidence formula as LearningPathProfileBuilder:
        /// base 35 × 0.5 + volume + trend + stability, clamped to [0, 100].
        /// Fewer sessions ⇒ lower confidence. Always 35 with zero sessions.
        /// </summary>
        public double Confidence { get; init; }

        /// <summary>
        /// True when <see cref="SessionCount"/> &gt;= 3, the minimum to compute a
        /// meaningful OLS slope. Consumers must treat false as "insufficient evidence".
        /// </summary>
        public bool HasEnoughData { get; init; }
    }
}
