using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Time-series data point for FemVoiceScore visualization
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
