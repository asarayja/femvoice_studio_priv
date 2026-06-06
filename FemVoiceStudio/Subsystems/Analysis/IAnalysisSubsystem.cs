using System;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoiceStudio.Subsystems.Analysis
{
    /// <summary>
    /// Voice metrics from real-time analysis
    /// </summary>
    public class VoiceMetrics
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        // Pitch/F0 metrics
        public double Pitch { get; set; }
        public double MinPitch { get; set; }
        public double MaxPitch { get; set; }
        public double PitchVariation { get; set; }
        public double PitchConfidence { get; set; }
        
        // Resonance/Formant metrics
        public double F1 { get; set; }
        public double F2 { get; set; }
        public double F3 { get; set; }
        public double SmoothedF1 { get; set; }
        public double SmoothedF2 { get; set; }
        public double ResonanceScore { get; set; }
        public ResonanceCategory ResonanceCategory { get; set; }
        
        // Intonation metrics
        public double IntonationRange { get; set; }
        public double IntonationRiseScore { get; set; }
        public double IntonationFallScore { get; set; }
        
        // Volume/Energy metrics
        public double RmsValue { get; set; }
        public double Intensity { get; set; }
        public double SpectralCentroid { get; set; }
        
        // Voice activity
        public bool IsVoiced { get; set; }
        public bool IsSpeaking { get; set; }
        
        // Health indicators
        public double StrainLevel { get; set; }
        public double Jitter { get; set; }
        public double Shimmer { get; set; }
    }

    /// <summary>
    /// Resonance category classification
    /// </summary>
    public enum ResonanceCategory
    {
        Unknown = 0,
        Back = 1,      // Masculine
        Neutral = 2,
        Forward = 3    // Feminine
    }

    /// <summary>
    /// Voice parameter types
    /// </summary>
    public enum VoiceParameter
    {
        Pitch,
        Resonance,
        Intonation,
        Volume,
        VoiceHealth
    }

    /// <summary>
    /// Target zone for a voice parameter
    /// </summary>
    public class TargetZone
    {
        public VoiceParameter Parameter { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double OptimalValue { get; set; }
        public bool IsPercentage { get; set; }
    }

    /// <summary>
    /// Health indicators from voice analysis
    /// </summary>
    public class HealthIndicators
    {
        public double StrainLevel { get; set; }
        public double Jitter { get; set; }
        public double Shimmer { get; set; }
        public bool IsHealthy { get; set; }
        public bool RequiresBreak { get; set; }
        public string? WarningMessage { get; set; }
    }

    /// <summary>
    /// Analysis subsystem interface - handles voice analysis and metrics calculation
    /// </summary>
    public interface IAnalysisSubsystem
    {
        /// <summary>
        /// Current real-time voice metrics
        /// </summary>
        VoiceMetrics? CurrentMetrics { get; }

        /// <summary>
        /// Analyze audio samples and return voice metrics
        /// </summary>
        Task<VoiceMetrics> AnalyzeAsync(float[] samples, CancellationToken ct = default);

        /// <summary>
        /// Analyze a complete training session
        /// </summary>
        Task<VoiceMetrics> AnalyzeSessionAsync(Models.TrainingSession session);

        /// <summary>
        /// Get target zone for a voice parameter based on user level
        /// </summary>
        TargetZone GetTargetZone(VoiceParameter parameter, Models.DifficultyLevel level);

        /// <summary>
        /// Check if current metrics are within target zones
        /// </summary>
        bool IsInTargetZone(VoiceMetrics metrics);

        /// <summary>
        /// Get health indicators from current metrics
        /// </summary>
        HealthIndicators GetHealthIndicators(VoiceMetrics metrics);

        /// <summary>
        /// Update target values based on user baseline
        /// </summary>
        void UpdateBaseline(VoiceMetrics baseline);

        /// <summary>
        /// Get smoothed/averaged metrics over recent window
        /// </summary>
        VoiceMetrics GetSmoothedMetrics(int windowSizeMs = 500);
    }
}
