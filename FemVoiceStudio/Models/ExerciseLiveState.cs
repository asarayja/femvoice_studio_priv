using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Represents the real-time state of an exercise evaluation cycle.
    /// Immutable snapshot published by <see cref="FemVoiceStudio.Services.ExerciseIntelligenceCoordinator"/>
    /// on every evaluation tick.
    /// </summary>
    public class ExerciseLiveState
    {
        /// <summary>
        /// Primary performance metric for the current exercise type (0–1).
        /// For resonance exercises: resonance score.
        /// For pitch exercises: normalised pitch score.
        /// For straw phonation: intensity + stability composite.
        /// </summary>
        public double PrimaryMetricScore { get; init; }

        /// <summary>
        /// Secondary performance metric for the current exercise type (0–1).
        /// Typically stability, or range variability for intonation exercises.
        /// </summary>
        public double SecondaryMetricScore { get; init; }

        /// <summary>
        /// Raw stability score (0–1) from the adaptive scoring engine.
        /// Exposed separately so the UI can always render a stability meter
        /// regardless of which metric is primary/secondary.
        /// </summary>
        public double StabilityScore { get; init; }

        /// <summary>
        /// Whether the current pitch is within the user-adapted comfort zone.
        /// Always <c>true</c> for exercise types that do not use pitch.
        /// </summary>
        public bool IsInComfortZone { get; init; }

        /// <summary>
        /// Whether all hold conditions for the current exercise profile are satisfied.
        /// </summary>
        public bool IsHoldingCorrectly { get; init; }

        /// <summary>
        /// Fractional progress toward the required hold duration (0–1).
        /// Frozen (not reset) when <see cref="IsSafetyLocked"/> is <c>true</c>.
        /// </summary>
        public double HoldProgress { get; init; }

        /// <summary>
        /// <c>true</c> when either a health-score threshold or the
        /// <see cref="FemVoiceStudio.Services.ComfortZoneController"/> has engaged a safety lock.
        /// Score accumulation and hold-timer progression are disabled while locked.
        /// </summary>
        public bool IsSafetyLocked { get; init; }

        /// <summary>
        /// Overall qualitative assessment of the current performance.
        /// </summary>
        public PerformanceQuality Quality { get; init; }

        /// <summary>
        /// UTC timestamp of when this snapshot was produced.
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// Number of whole seconds elapsed since the exercise session started.
        /// Coordinator computes this based on processed audio frames (no polling).
        /// </summary>
        public int SessionElapsedSeconds { get; init; }

        // ── Raw per-tick acoustic signals (Voice Intelligence, Bølge 2) ──────────────
        // Additive, init-only, default = "missing" sentinel (0 / NaN). These carry the
        // RAW per-tick aggregates the VoiceIntelligenceScorer needs for the Intonation,
        // VocalWeight and Pitch dimensions — signals the older PrimaryMetricScore /
        // StabilityScore pair did not preserve. They are filled ONLY when the producing
        // engine/audio path actually delivers them; otherwise they stay at their sentinel
        // and the session aggregate falls back to the scorer's neutral 50. Never fabricated.

        /// <summary>
        /// First formant (F1) in Hz for this tick (from the resonance engine's formant
        /// snapshot). 0 ⇒ not measured this tick. Feeds the VocalWeight dimension.
        /// </summary>
        public double F1Hz { get; init; }

        /// <summary>
        /// Spectral centroid in Hz for this tick (perceived "brightness"; the primary
        /// vocal-weight signal). 0 ⇒ not measured this tick. Feeds VocalWeight.
        /// </summary>
        public double SpectralCentroidHz { get; init; }

        /// <summary>
        /// Harmonics-to-noise ratio in dB for this tick. <see cref="double.NaN"/> ⇒ not
        /// measured (the exercise path does not currently surface HNR). Feeds VocalWeight.
        /// </summary>
        public double HnrDb { get; init; } = double.NaN;

        /// <summary>
        /// RMS intensity (0–1) for this tick. 0 ⇒ not measured this tick. Feeds VocalWeight.
        /// </summary>
        public double Intensity { get; init; }

        /// <summary>
        /// Measured fundamental frequency (F0) in Hz for this tick. 0 ⇒ unvoiced / not
        /// measured. Feeds the Pitch dimension and (aggregated as range/variation) the
        /// Intonation dimension. This is the user's ACTUAL pitch, not a comfort-zone centre.
        /// </summary>
        public double PitchHz { get; init; }
    }

    /// <summary>
    /// Qualitative performance assessment used for UI colour-coding and SmartCoach context.
    /// </summary>
    public enum PerformanceQuality
    {
        /// <summary>Insufficient performance — significant correction needed.</summary>
        Poor,

        /// <summary>Metrics moving toward target but not yet within range.</summary>
        Improving,

        /// <summary>Metrics within acceptable range of target.</summary>
        Good,

        /// <summary>Metrics consistently within optimal range with strong stability.</summary>
        Excellent
    }
}
