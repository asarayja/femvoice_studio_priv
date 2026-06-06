using System;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Session data used for progression evaluation.
    /// Captures all metrics needed for progression decision making.
    /// </summary>
    public class ProgressionSessionData
    {
        public int SessionId { get; set; }
        public DateTime SessionDate { get; set; }
        
        /// <summary>
        /// FemVoiceScore results for this session
        /// </summary>
        public double ResonanceScore { get; set; }
        public double PitchScore { get; set; }
        public double ProsodyScore { get; set; }
        public double VoiceHealthScore { get; set; }
        public double OverallScore { get; set; }
        
        /// <summary>
        /// Raw pitch data
        /// </summary>
        public double AveragePitch { get; set; }
        public double MinPitch { get; set; }
        public double MaxPitch { get; set; }
        public double PitchVariation { get; set; }
        
        /// <summary>
        /// Resonans data
        /// </summary>
        public double AverageF1 { get; set; }
        public double AverageF2 { get; set; }
        public double ResonanceStability { get; set; }
        
        /// <summary>
        /// Prosody data
        /// </summary>
        public double IntonationRange { get; set; }
        
        /// <summary>
        /// Stability state from SmartCoach
        /// </summary>
        public StabilityState PitchStability { get; set; } = StabilityState.NoVoice;
        
        /// <summary>
        /// Health indicators from SmartCoach
        /// </summary>
        public HealthState HealthState { get; set; } = HealthState.NoVoice;
        
        /// <summary>
        /// Strain detection
        /// </summary>
        public bool StrainDetected { get; set; }
        public double StrainLevel { get; set; }
        
        /// <summary>
        /// Session type based on periodization
        /// </summary>
        public SessionType SessionType { get; set; } = SessionType.Progressive;
        
        /// <summary>
        /// Progression decision made for this session
        /// </summary>
        public ProgressionDecision Decision { get; set; } = ProgressionDecision.Maintain;
        
        /// <summary>
        /// Whether this session passed all progression gates
        /// </summary>
        public bool PassedProgressionGates { get; set; }
        
        /// <summary>
        /// Fatigue accumulation (0-100)
        /// </summary>
        public double FatigueLevel { get; set; }
        
        /// <summary>
        /// Creates from TrainingSession and FemVoiceScoreResult
        /// </summary>
        public static ProgressionSessionData FromSession(
            TrainingSession session, 
            FemVoiceScoreResult? scores = null)
        {
            return new ProgressionSessionData
            {
                SessionId = session.Id,
                SessionDate = session.StartTime,
                ResonanceScore = scores?.ResonanceScore ?? session.ResonanceScore,
                PitchScore = scores?.PitchScore ?? 0,
                ProsodyScore = scores?.IntonationScore ?? session.IntonationScore,
                VoiceHealthScore = scores?.VoiceHealthScore ?? session.VoiceHealthScore,
                OverallScore = scores?.OverallScore ?? session.OverallScore,
                AveragePitch = session.AveragePitch,
                MinPitch = session.MinPitch,
                MaxPitch = session.MaxPitch,
                PitchVariation = session.PitchVariation,
                AverageF1 = session.AverageF1,
                AverageF2 = session.AverageF2,
                StrainDetected = (scores?.WarningFlags?.Contains("STRAIN") ?? false) || session.StrainLevel > 50,
                StrainLevel = session.StrainLevel
            };
        }
    }
    
    /// <summary>
    /// Gate status for progression validation
    /// </summary>
    public class ProgressionGateStatus
    {
        public bool IsResonanceGateOpen { get; set; }
        public bool IsHealthGateOpen { get; set; }
        public bool IsStabilityGateOpen { get; set; }
        public bool IsConsecutiveSessionsGateOpen { get; set; }
        
        public bool AllGatesOpen => IsResonanceGateOpen && IsHealthGateOpen && 
                                    IsStabilityGateOpen && IsConsecutiveSessionsGateOpen;
        
        public string BlockedReason { get; set; } = string.Empty;
        
        public static ProgressionGateStatus AllGatesOpenStatus()
        {
            return new ProgressionGateStatus
            {
                IsResonanceGateOpen = true,
                IsHealthGateOpen = true,
                IsStabilityGateOpen = true,
                IsConsecutiveSessionsGateOpen = true
            };
        }
    }
    
    /// <summary>
    /// Result of regression detection
    /// </summary>
    public class RegressionDetectionResult
    {
        public bool IsRegressing { get; set; }
        public RegressionType? RegressionType { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Severity { get; set; }
    }
    
    /// <summary>
    /// Types of regression that can occur
    /// </summary>
    public enum RegressionType
    {
        Strain,
        HealthDecline,
        PitchInstability,
        ResonanceDrift,
        Plateau
    }
    
    /// <summary>
    /// Periodization cycle type
    /// </summary>
    public enum PeriodizationCycle
    {
        Standard,       // 3 active → 1 consolidation
        HighFatigue,    // 2 active → 2 recovery
        Recovery,       // Recovery after strain
        Maintenance     // No active progression
    }
}
