using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Detected plateau state: composite and per-dimension slope is effectively flat across
    /// two or more consecutive <see cref="TrendWindow"/>s with sufficient data. Descriptive
    /// intelligence only — never a training block. Consumers use <see cref="ReasonCode"/>
    /// for routing; human copy is composed from that code via LocalizationService.
    /// </summary>
    public sealed record PlateauState
    {
        /// <summary>
        /// Stable machine code, e.g. "PLATEAU_Resonance". Never shown raw; copy composed
        /// via LocalizationService at presentation time.
        /// </summary>
        public string ReasonCode { get; init; } = string.Empty;

        /// <summary>The weakest or most relevant dimension driving the plateau detection.</summary>
        public VoiceDimension Dimension { get; init; }

        /// <summary>
        /// 0–100. Severity grows with plateau duration (days flat) and how close to zero
        /// the observed slope is. At least 1 when a plateau is detected.
        /// </summary>
        public double SeverityScore { get; init; }

        /// <summary>Window span in days (e.g. 7 or 30) in which the plateau was observed.</summary>
        public int WindowDays { get; init; }

        /// <summary>Start of the earliest window contributing to the plateau (inclusive).</summary>
        public DateTime? StartedAt { get; init; }

        /// <summary>
        /// Number of consecutive days the plateau has been active (derived from window
        /// boundaries). Used to scale <see cref="SeverityScore"/>.
        /// </summary>
        public int PlateauDurationDays { get; init; }

        /// <summary>OLS composite slope observed across the plateau windows (points/session).</summary>
        public double ObservedSlope { get; init; }
    }

    /// <summary>
    /// Detected breakthrough state: a shorter window shows clearly stronger positive
    /// slope than the preceding longer window — an inflection point from flat/declining
    /// to meaningfully improving. Descriptive intelligence only — never a gate.
    /// </summary>
    public sealed record BreakthroughState
    {
        /// <summary>
        /// Stable machine code, e.g. "BREAKTHROUGH_Resonance". Copy composed via
        /// LocalizationService at presentation time.
        /// </summary>
        public string ReasonCode { get; init; } = string.Empty;

        /// <summary>The dimension with the largest positive inflection.</summary>
        public VoiceDimension Dimension { get; init; }

        /// <summary>0–100. Scales with the magnitude of the slope inflection.</summary>
        public double SeverityScore { get; init; }

        /// <summary>Shorter window span (days) in which the breakthrough was detected.</summary>
        public int WindowDays { get; init; }

        /// <summary>When the breakthrough inflection was detected (start of the shorter window).</summary>
        public DateTime? DetectedAt { get; init; }

        /// <summary>
        /// Difference: short-window composite slope minus long-window composite slope
        /// (points/session). Always positive when a breakthrough is reported.
        /// </summary>
        public double MagnitudeDelta { get; init; }
    }

    /// <summary>
    /// Detected regression state: one or more dimensions show a clearly declining slope
    /// (≤ −1.5 points/session, matching the ComputeComfortSlope clinical threshold).
    /// CompoundSeverity rises when multiple dimensions fall simultaneously. Descriptive
    /// intelligence only — health/recovery gates are the sole training authorities.
    /// </summary>
    public sealed record RegressionState
    {
        /// <summary>
        /// Stable machine code, e.g. "REGRESSION_Comfort". Copy composed via
        /// LocalizationService at presentation time.
        /// </summary>
        public string ReasonCode { get; init; } = string.Empty;

        /// <summary>The most severely declining dimension.</summary>
        public VoiceDimension Dimension { get; init; }

        /// <summary>0–100. Scales with the steepness of the decline slope.</summary>
        public double SeverityScore { get; init; }

        /// <summary>Window span in days over which the regression was measured.</summary>
        public int WindowDays { get; init; }

        /// <summary>When the regression was detected (start of the relevant window).</summary>
        public DateTime? DetectedAt { get; init; }

        /// <summary>
        /// OLS slope of the declining dimension (points/session). Always ≤ −1.5 when a
        /// regression is reported (matching the clinical ComputeComfortSlope threshold).
        /// </summary>
        public double DeclineSlope { get; init; }

        /// <summary>
        /// Compound severity: base severity multiplied by a co-decline factor that grows
        /// when multiple dimensions are falling simultaneously. Equal to
        /// <see cref="SeverityScore"/> when only one dimension declines.
        /// </summary>
        public double CompoundSeverity { get; init; }
    }
}
