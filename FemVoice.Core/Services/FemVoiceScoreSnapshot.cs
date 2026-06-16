using System;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Snapshot containing the calculated FemVoiceScore with adaptive intelligence metrics.
    /// Used for real-time scoring updates and trend analysis.
    /// </summary>
    public sealed class FemVoiceScoreSnapshot
    {
        /// <summary>
        /// The raw weighted composite score before adaptive normalization.
        /// Range: 0-100
        /// </summary>
        public double RawScore { get; init; }

        /// <summary>
        /// The adaptive score normalized against the user's personal baseline trend.
        /// Range: 0-100
        /// </summary>
        public double AdaptiveScore { get; init; }

        /// <summary>
        /// The direction and rate of change in the user's performance trend.
        /// Positive = improving, Negative = declining, Near zero = stable
        /// </summary>
        public double TrendSlope { get; init; }

        /// <summary>
        /// Indicates whether the user has been in a plateau (minimal improvement) for more than 14 days.
        /// </summary>
        public bool PlateauDetected { get; init; }

        /// <summary>
        /// Indicates whether the user has shown sustained regression (>10% drop from baseline).
        /// </summary>
        public bool RegressionDetected { get; init; }

        /// <summary>
        /// The timestamp when this snapshot was calculated.
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// The user ID this snapshot belongs to.
        /// </summary>
        public int UserId { get; init; }

        /// <summary>
        /// Component scores used in the calculation.
        /// </summary>
        public double ResonanceScore { get; init; }
        public double PitchScore { get; init; }
        public double StabilityScore { get; init; }
        public double HealthModifier { get; init; }

        /// <summary>
        /// Creates an empty snapshot for initialization or error states.
        /// </summary>
        public static FemVoiceScoreSnapshot Empty => new()
        {
            RawScore = 0,
            AdaptiveScore = 0,
            TrendSlope = 0,
            PlateauDetected = false,
            RegressionDetected = false,
            Timestamp = DateTime.MinValue,
            UserId = 0,
            ResonanceScore = 0,
            PitchScore = 0,
            StabilityScore = 0,
            HealthModifier = 0
        };
    }
}
