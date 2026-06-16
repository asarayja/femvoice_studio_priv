using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// The longitudinal voice development profile for one user: a structured, immutable
    /// aggregate of multi-window trend data, detected patterns and the current composite
    /// voice score. Assembled by the Longitudinal Profile engine (Agent A3).
    ///
    /// CLINICAL FRAMING: this record is DESCRIPTIVE / EXPLANATORY intelligence only. It
    /// must never override or substitute for the Safety &gt; Health &gt; Recovery hierarchy;
    /// those gates remain the only blocking authorities. Consumers must treat
    /// <see cref="HasEnoughData"/> == false as "insufficient evidence", never as
    /// "no progress".
    ///
    /// Dependencies (defined by co-agents in the same wave):
    /// <list type="bullet">
    ///   <item><description><see cref="TrendWindow"/> — A4</description></item>
    ///   <item><description><see cref="PlateauState"/>, <see cref="BreakthroughState"/>,
    ///   <see cref="RegressionState"/> — A56 (VoicePatternEvents.cs)</description></item>
    /// </list>
    /// </summary>
    public sealed record VoiceDevelopmentProfile
    {
        /// <summary>The user this profile belongs to.</summary>
        public int UserId { get; init; }

        /// <summary>When this profile snapshot was assembled.</summary>
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Short-horizon trend windows (7-day, with optional 30-day supplementary window).
        /// Each entry is a closed-open <c>[From, To)</c> interval covering the window's
        /// OLS slopes per <see cref="VoiceDimension"/> and composite statistics.
        /// Empty list when no weekly trend data is available.
        /// </summary>
        public IReadOnlyList<TrendWindow> WeeklyTrend { get; init; } =
            Array.Empty<TrendWindow>();

        /// <summary>
        /// Long-horizon trend windows (90-day and 180-day).
        /// Each entry is a closed-open <c>[From, To)</c> interval. Empty list when no
        /// monthly trend data is available.
        /// </summary>
        public IReadOnlyList<TrendWindow> MonthlyTrend { get; init; } =
            Array.Empty<TrendWindow>();

        /// <summary>
        /// Detected plateau state for the most salient stalled dimension, or <c>null</c>
        /// when no plateau is active. A plateau is DESCRIPTIVE; it must not block training.
        /// </summary>
        public PlateauState? Plateau { get; init; }

        /// <summary>
        /// Detected breakthrough state for the most salient accelerating dimension, or
        /// <c>null</c> when no breakthrough is active.
        /// </summary>
        public BreakthroughState? Breakthrough { get; init; }

        /// <summary>
        /// Detected regression state for the most salient declining dimension, or
        /// <c>null</c> when no regression is active. A regression is DESCRIPTIVE; it is
        /// not a safety or health gate.
        /// </summary>
        public RegressionState? Regression { get; init; }

        /// <summary>
        /// Hierarchy-weighted composite voice score at the time this profile was generated,
        /// 0–100. A measurement; never a gate. Defaults to 0 when insufficient data.
        /// </summary>
        public double CompositeVoiceScore { get; init; }

        /// <summary>
        /// True when enough session history exists across the trend windows for the slopes
        /// and patterns to be considered evidence rather than noise (i.e. at least one
        /// <see cref="TrendWindow"/> with <see cref="TrendWindow.HasEnoughData"/> == true).
        /// Consumers MUST treat false as "insufficient evidence", not "no development".
        /// </summary>
        public bool HasEnoughData { get; init; }

        /// <summary>
        /// Returns a minimal, empty-state profile for <paramref name="userId"/> with
        /// <see cref="HasEnoughData"/> == false. Useful as a safe default before any
        /// session history has accumulated.
        /// </summary>
        public static VoiceDevelopmentProfile Empty(int userId, DateTime generatedAt) =>
            new()
            {
                UserId = userId,
                GeneratedAt = generatedAt,
                WeeklyTrend = Array.Empty<TrendWindow>(),
                MonthlyTrend = Array.Empty<TrendWindow>(),
                Plateau = null,
                Breakthrough = null,
                Regression = null,
                CompositeVoiceScore = 0,
                HasEnoughData = false
            };
    }
}
