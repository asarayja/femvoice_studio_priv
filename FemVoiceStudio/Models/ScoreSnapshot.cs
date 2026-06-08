using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Pure, WPF-free mapping from the persisted Voice Intelligence trend
    /// (<see cref="VoiceIntelligenceTrendPoint"/>) to <see cref="ScoreSnapshot"/>
    /// view-model rows. Extracted so the mapping (0–100 preservation, chronology,
    /// empty-history handling) is unit-testable without any WPF/OxyPlot dependency.
    ///
    /// Hierarchy is preserved purely as data: every dimension is carried through; the
    /// composite is a measurement only and never gates anything here.
    /// </summary>
    public static class VoiceIntelligenceTrendMapper
    {
        private static double Clamp0To100(double value)
        {
            if (double.IsNaN(value)) return 0.0;
            return Math.Clamp(value, 0.0, 100.0);
        }

        /// <summary>
        /// Map one trend point to a <see cref="ScoreSnapshot"/>. The seven dimensions
        /// land on both the dimension-specific fields and (for the three legacy
        /// dimensions) the existing alias fields, so old chart bindings keep working.
        /// </summary>
        public static ScoreSnapshot ToSnapshot(VoiceIntelligenceTrendPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);

            var resonance = Clamp0To100(point.ResonanceScore100);
            var comfort = Clamp0To100(point.ComfortScore100);
            var consistency = Clamp0To100(point.ConsistencyScore100);
            var intonation = Clamp0To100(point.IntonationScore100);
            var vocalWeight = Clamp0To100(point.VocalWeightScore100);
            var recovery = Clamp0To100(point.RecoveryScore100);
            var pitch = Clamp0To100(point.PitchScore100);
            var composite = Clamp0To100(point.CompositeVoiceScore);

            return new ScoreSnapshot
            {
                Timestamp = point.StartedAt,
                OverallScore = composite,

                // Legacy alias fields (kept for existing bindings/charts).
                ResonanceScore = resonance,
                PitchScore = pitch,
                IntonationScore = intonation,
                // Voice health proxy = the Health pair (Comfort + Recovery), mean.
                VoiceHealthScore = Clamp0To100((comfort + recovery) / 2.0),

                // Voice Intelligence dimensions.
                ResonanceDimension = resonance,
                ComfortDimension = comfort,
                ConsistencyDimension = consistency,
                IntonationDimension = intonation,
                VocalWeightDimension = vocalWeight,
                RecoveryDimension = recovery,
                PitchDimension = pitch,
                CompositeVoiceScore = composite
            };
        }

        /// <summary>
        /// Map a whole trend (already chronological from the store) to snapshots,
        /// defensively re-sorted by <see cref="VoiceIntelligenceTrendPoint.StartedAt"/>.
        /// A null or empty trend yields an empty list (no crash, "ikke nok data").
        /// </summary>
        public static IReadOnlyList<ScoreSnapshot> ToSnapshots(
            IEnumerable<VoiceIntelligenceTrendPoint>? trend)
        {
            if (trend is null)
            {
                return Array.Empty<ScoreSnapshot>();
            }

            return trend
                .Where(p => p is not null)
                .OrderBy(p => p.StartedAt)
                .Select(ToSnapshot)
                .ToList();
        }
    }

    /// <summary>
    /// Time-series data point for FemVoiceScore visualization.
    ///
    /// Sprint B (Bølge 2): extended additively with the seven explainable Voice
    /// Intelligence dimensions (0–100) plus the hierarchy-weighted composite, so the
    /// analysis trend charts can plot ALL dimensions — not just pitch/resonance — from
    /// <c>SessionAnalyticsStore.GetVoiceIntelligenceTrendAsync</c>. Existing fields are
    /// untouched; legacy consumers (e.g. MainViewModel) keep compiling.
    /// </summary>
    public class ScoreSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double OverallScore { get; set; }
        public double ResonanceScore { get; set; }
        public double PitchScore { get; set; }
        public double IntonationScore { get; set; }
        public double VoiceHealthScore { get; set; }
        public double CurrentPitch { get; set; }
        public double CurrentResonance { get; set; }
        public double CurrentStability { get; set; }

        // ── Voice Intelligence dimensions (Sprint B, 0–100) ──────────────────────
        // Hierarchy order (clinical): Health (Comfort + Recovery) > Resonance >
        // Consistency > Intonation > VocalWeight > Pitch. Pitch is never dominant.
        // ResonanceScore / PitchScore / IntonationScore above remain the legacy
        // aliases; the dimension-specific fields below carry the Bølge 1 scores.
        public double ResonanceDimension { get; set; }
        public double ComfortDimension { get; set; }
        public double ConsistencyDimension { get; set; }
        public double IntonationDimension { get; set; }
        public double VocalWeightDimension { get; set; }
        public double RecoveryDimension { get; set; }
        public double PitchDimension { get; set; }

        /// <summary>Hierarchy-weighted composite ("Voice Development"), 0–100.</summary>
        public double CompositeVoiceScore { get; set; }
    }

    /// <summary>
    /// Time-series data point for health trend visualization
    /// </summary>
    public class HealthSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double StrainLevel { get; set; }
        public double FatigueLevel { get; set; }
        public double IntensityControl { get; set; }
        public bool StrainDetected { get; set; }
        public bool FatigueWarning { get; set; }
    }

    /// <summary>
    /// Real-time pitch sample for visualization
    /// </summary>
    public class PitchSample
    {
        public DateTime Timestamp { get; set; }
        public double Pitch { get; set; }
        public double SmoothedPitch { get; set; }
        public double Confidence { get; set; }
        public double Intensity { get; set; }
        public bool IsVoiced { get; set; }
        public bool IsInComfortZone { get; set; }
    }

    /// <summary>
    /// Formant sample for resonance analysis
    /// </summary>
    public class FormantSample
    {
        public DateTime Timestamp { get; set; }
        public double F1 { get; set; }
        public double F2 { get; set; }
        public double F3 { get; set; }
        public double SpectralCentroid { get; set; }
        public double ResonanceScore { get; set; }
        public bool IsForwardResonance { get; set; }
    }

    /// <summary>
    /// Stability state for real-time display
    /// </summary>
    public enum StabilityState
    {
        NoVoice,
        Unstable,
        Developing,
        Stable,
        VeryStable
    }

    /// <summary>
    /// Health indicator for real-time display
    /// </summary>
    public enum HealthState
    {
        NoVoice,
        Safe,
        Monitor,
        Warning,
        Danger
    }

    /// <summary>
    /// Range structure for comfort zone
    /// </summary>
    public class Range
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Optimal { get; set; }

        public Range(double min, double max, double optimal = 0)
        {
            Min = min;
            Max = max;
            Optimal = optimal > 0 ? optimal : (min + max) / 2;
        }

        public Range() { }

        public bool IsInRange(double value) => value >= Min && value <= Max;

        public double DistanceFromCenter(double value) => Math.Abs(value - Optimal);
    }

    /// <summary>
    /// Session type for adaptive targeting
    /// </summary>
    public enum SessionType
    {
        Progressive,     // Normal training - push limits slightly
        Maintenance,      // Consolidation - protect against overtraining
        Recovery          // Rest - focus on comfort, no pitch pressure
    }

    /// <summary>
    /// Coach explanation context for SmartCoach messages
    /// </summary>
    public class CoachExplanationContext
    {
        public double ResonanceScore { get; set; }
        public double PitchScore { get; set; }
        public double IntonationScore { get; set; }
        public double VoiceHealthScore { get; set; }
        public double CurrentPitch { get; set; }
        public double CurrentResonance { get; set; }
        public StabilityState Stability { get; set; }
        public HealthState Health { get; set; }
        public SessionType CurrentSessionType { get; set; }
    }
}
