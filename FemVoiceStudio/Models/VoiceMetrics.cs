using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Voice metrics extracted from audio analysis in real-time.
    /// Contains all parameters needed for clinical voice feminization evaluation.
    /// </summary>
    public class VoiceMetrics
    {
        /// <summary>
        /// Timestamp when these metrics were calculated
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        #region Pitch Parameters

        /// <summary>
        /// Fundamental frequency (F0) in Hz
        /// </summary>
        public double Pitch { get; set; }

        /// <summary>
        /// Minimum pitch detected in current analysis window
        /// </summary>
        public double MinPitch { get; set; }

        /// <summary>
        /// Maximum pitch detected in current analysis window
        /// </summary>
        public double MaxPitch { get; set; }

        /// <summary>
        /// Pitch variation/standard deviation in Hz
        /// </summary>
        public double PitchVariation { get; set; }

        /// <summary>
        /// Whether pitch press is detected (F0 > 180 Hz + instability)
        /// </summary>
        public bool IsPitchPress { get; set; }

        #endregion

        #region Resonance/Formant Parameters

        /// <summary>
        /// First formant (F1) in Hz - relates to vowel openness
        /// Optimal range: 400-700 Hz
        /// </summary>
        public double F1 { get; set; }

        /// <summary>
        /// Second formant (F2) in Hz - relates to tongue position
        /// Critical for voice feminization: Forward resonance = F2 > 1400 Hz
        /// Optimal range: 1400-2200 Hz (target 1800+)
        /// </summary>
        public double F2 { get; set; }

        /// <summary>
        /// Third formant (F3) in Hz - relates to tongue curvature
        /// Optimal range: 2500-3500 Hz
        /// </summary>
        public double F3 { get; set; }

        /// <summary>
        /// Spectral centroid in Hz - indicates "brightness" of tone
        /// &gt;2000 Hz = bright/feminine tone
        /// </summary>
        public double SpectralCentroid { get; set; }

        /// <summary>
        /// Resonance classification: Forward, Neutral, or Back
        /// </summary>
        public ResonanceClassification ResonanceClassification { get; set; }

        #endregion

        #region Stability/Voice Quality Parameters

        /// <summary>
        /// Jitter - frequency variation percentage
        /// Threshold: &gt;1.5% indicates instability, &gt;2.5% is critical
        /// </summary>
        public double Jitter { get; set; }

        /// <summary>
        /// Shimmer - amplitude variation percentage
        /// Threshold: &gt;3.5% indicates instability, &gt;5.0% is critical
        /// </summary>
        public double Shimmer { get; set; }

        /// <summary>
        /// Harmonics-to-Noise Ratio in dB
        /// Threshold: &lt;15 dB indicates hoarseness/strain
        /// </summary>
        public double HNR { get; set; }

        #endregion

        #region Intensity/Breathing Parameters

        /// <summary>
        /// Root Mean Square of amplitude (0.0-1.0)
        /// Optimal range: 0.1-0.8
        /// </summary>
        public double Intensity { get; set; }

        /// <summary>
        /// Intensity variation/variance over time
        /// Low variance = good breath control
        /// </summary>
        public double IntensityVariance { get; set; }

        /// <summary>
        /// Whether intensity indicates air starvation (declining &gt;30% toward end)
        /// </summary>
        public bool IsAirStarved { get; set; }

        #endregion

        #region Intonation/Prosody Parameters

        /// <summary>
        /// Pitch range in Hz (max - min) over analysis window
        /// Optimal range: 30-120 Hz
        /// &lt;30 Hz = monoton/flat
        /// </summary>
        public double IntonationRange { get; set; }

        /// <summary>
        /// Intonation rise score (0.0-1.0)
        /// &gt;0.2 indicates rising intonation (questions)
        /// </summary>
        public double IntonationRiseScore { get; set; }

        /// <summary>
        /// Intonation pattern: Rising, Falling, Flat, or Natural
        /// </summary>
        public IntonationPattern IntonationPattern { get; set; }

        #endregion

        #region Health/Strain Parameters

        /// <summary>
        /// Overall strain level (0.0-1.0)
        /// &gt;0.5 indicates moderate strain, &gt;0.75 is critical
        /// </summary>
        public double StrainLevel { get; set; }

        /// <summary>
        /// Whether amplitude spike detected (&gt;2.0 std dev)
        /// </summary>
        public bool HasAmplitudeSpike { get; set; }

        /// <summary>
        /// Number of consecutive cycles with elevated strain indicators
        /// </summary>
        public int ConsecutiveStrainCycles { get; set; }

        /// <summary>
        /// Health status indicator derived from all metrics
        /// </summary>
        public HealthIndicator HealthStatus { get; set; } = HealthIndicator.Safe;

        #endregion

        #region Target Zone Tracking

        /// <summary>
        /// Dictionary tracking whether each parameter is in its target range
        /// </summary>
        public Dictionary<string, bool> IsInRange { get; set; } = new();

        /// <summary>
        /// Dictionary storing distance from target for each parameter
        /// </summary>
        public Dictionary<string, double> DistanceFromTarget { get; set; } = new();

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if pitch is within feminine target range (165-255 Hz)
        /// </summary>
        public bool IsPitchInFeminineRange => Pitch >= 165 && Pitch <= 255;

        /// <summary>
        /// Check if F2 indicates forward resonance (F2 &gt; 1400 Hz)
        /// </summary>
        public bool IsForwardResonance => F2 >= 1400;

        /// <summary>
        /// Check if metrics indicate strain requiring immediate attention
        /// </summary>
        public bool IsStrained => Jitter > 2.5 || Shimmer > 5.0 || StrainLevel > 0.75 || HNR < 15;

        /// <summary>
        /// Check if metrics indicate moderate strain (warning level)
        /// </summary>
        public bool HasStrainWarning => Jitter > 1.5 || Shimmer > 3.5 || StrainLevel > 0.5;

        /// <summary>
        /// Create default/empty metrics
        /// </summary>
        public static VoiceMetrics Empty => new VoiceMetrics
        {
            Timestamp = DateTime.Now,
            Pitch = 0,
            F1 = 0,
            F2 = 0,
            F3 = 0,
            Jitter = 0,
            Shimmer = 0,
            HNR = 0,
            Intensity = 0,
            StrainLevel = 0,
            HealthStatus = HealthIndicator.Safe,
            ResonanceClassification = ResonanceClassification.Neutral,
            IntonationPattern = IntonationPattern.Flat
        };

        #endregion
    }

    /// <summary>
    /// Resonance classification based on formant analysis
    /// </summary>
    public enum ResonanceClassification
    {
        NotApplicable = 0,
        Forward = 1,      // F2 &gt; 1800 Hz - target for voice feminization
        Neutral = 2,      // F2 1400-1800 Hz
        Back = 3          // F2 &lt; 1400 Hz - needs improvement
    }

    /// <summary>
    /// Intonation pattern classification
    /// </summary>
    public enum IntonationPattern
    {
        NotApplicable = 0,
        Flat = 1,         // &lt;30 Hz range - needs variation
        Rising = 2,       // Question-like rising pattern
        Falling = 3,      // Statement-like falling pattern
        Natural = 4       // 40-120 Hz range with appropriate pattern
    }
}
