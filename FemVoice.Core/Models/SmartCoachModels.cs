using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Adaptive target zone for voice parameters
    /// </summary>
    public class AdaptiveTargetZone
    {
        public double Min { get; set; }
        public double Optimal { get; set; }
        public double Max { get; set; }
        
        public AdaptiveTargetZone(double min, double optimal, double max)
        {
            Min = min;
            Optimal = optimal;
            Max = max;
        }
        
        public AdaptiveTargetZone() { }
        
        public bool IsInRange(double value) => value >= Min && value <= Max;
        
        public double DistanceFromOptimal(double value) => Math.Abs(value - Optimal);
    }
    
    /// <summary>
    /// Real-time feedback for current voice analysis
    /// </summary>
    public class RealtimeFeedback
    {
        public DateTime Timestamp { get; set; }
        public double CurrentPitch { get; set; }
        public double CurrentF2 { get; set; }
        public double StrainLevel { get; set; }
        
        public ParameterStatus PitchStatus { get; set; }
        public string PitchMessage { get; set; } = string.Empty;
        
        public ParameterStatus ResonanceStatus { get; set; }
        public string ResonanceMessage { get; set; } = string.Empty;
        
        public Models.HealthIndicator HealthStatus { get; set; }
        public string HealthMessage { get; set; } = string.Empty;
        
        public RealtimeFeedbackLevel OverallStatus { get; set; }
    }
    
    public enum ParameterStatus
    {
        NoVoice,
        TooLow,
        InRange,
        TooHigh
    }
    
    public enum RealtimeFeedbackLevel
    {
        Neutral,
        Adjust,
        Success,
        Stop
    }
    
    /// <summary>
    /// Health alert for voice safety
    /// </summary>
    public class HealthAlert
    {
        public HealthAlertType AlertType { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
    
    public enum HealthAlertType
    {
        Info,
        Warning,
        ImmediateRest,
        Lockout
    }
    
    /// <summary>
    /// Session analysis summary after stopping analysis
    /// </summary>
    public class SessionAnalysisSummary
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        
        public double AveragePitch { get; set; }
        public double MinPitch { get; set; }
        public double MaxPitch { get; set; }
        
        public double AverageF1 { get; set; }
        public double AverageF2 { get; set; }
        public double AverageF3 { get; set; }
        
        public double AverageJitter { get; set; }
        public double AverageShimmer { get; set; }
        public double AverageHNR { get; set; }
        
        public double VoicedPercentage { get; set; }
        
        public bool StrainDetected { get; set; }
        public bool LockoutTriggered { get; set; }
        
        public TimeSpan Duration => EndTime - StartTime;
    }
}
