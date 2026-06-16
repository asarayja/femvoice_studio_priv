using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// User progression profile storing baseline and historical data for adaptive training.
    /// Establishes the foundation for safe, personalized progression based on clinical principles.
    /// </summary>
    public class UserProgressionProfile
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        
        /// <summary>
        /// Comfort pitch range - user's natural speaking range (measured in Hz)
        /// </summary>
        public double ComfortPitchMin { get; set; }
        public double ComfortPitchMax { get; set; }
        public double ComfortPitchMedian { get; set; }
        
        /// <summary>
        /// Resonance median - typical stability level (0-100)
        /// </summary>
        public double ResonanceMedian { get; set; }
        
        /// <summary>
        /// Health tolerance - user's voice load capacity (0-100)
        /// </summary>
        public double HealthTolerance { get; set; }
        
        /// <summary>
        /// Prosody baseline - natural intonation range in Hz
        /// </summary>
        public double ProsodyRangeMin { get; set; }
        public double ProsodyRangeMax { get; set; }
        
        /// <summary>
        /// Target pitch zone for current training
        /// </summary>
        public double TargetPitchMin { get; set; }
        public double TargetPitchMax { get; set; }
        
        /// <summary>
        /// Current progression mode
        /// </summary>
        public ProgressionMode CurrentMode { get; set; } = ProgressionMode.Maintenance;
        
        /// <summary>
        /// Current progression decision
        /// </summary>
        public ProgressionDecision CurrentDecision { get; set; } = ProgressionDecision.Maintain;
        
        /// <summary>
        /// Timestamps
        /// </summary>
        public DateTime? BaselineEstablishedAt { get; set; }
        public DateTime? LastModeChangeAt { get; set; }
        public DateTime? LastDecisionAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Baseline establishment status
        /// </summary>
        public bool IsBaselineEstablished => BaselineSessionsCount >= 3;
        public int BaselineSessionsCount { get; set; }
        
        /// <summary>
        /// Consecutive stable sessions for progression gate
        /// </summary>
        public int ConsecutiveStableSessions { get; set; }
        
        /// <summary>
        /// Fatigue tracking
        /// </summary>
        public double CurrentFatigueLevel { get; set; }
        public int RecoverySessionsNeeded { get; set; }
        
        /// <summary>
        /// Streak and milestone tracking
        /// </summary>
        public int CurrentStreak { get; set; }
        public int TotalSessionsCompleted { get; set; }
        public List<ProgressionMilestone> AchievedMilestones { get; set; } = new();
        
        /// <summary>
        /// Creates a default profile for new users
        /// </summary>
        public static UserProgressionProfile CreateDefault(int userId = 1)
        {
            return new UserProgressionProfile
            {
                UserId = userId,
                ComfortPitchMin = 140,
                ComfortPitchMax = 180,
                ComfortPitchMedian = 160,
                ResonanceMedian = 50,
                HealthTolerance = 80,
                ProsodyRangeMin = 20,
                ProsodyRangeMax = 80,
                TargetPitchMin = 165,
                TargetPitchMax = 200,
                CurrentMode = ProgressionMode.Maintenance,
                CurrentDecision = ProgressionDecision.Maintain,
                BaselineSessionsCount = 0,
                ConsecutiveStableSessions = 0,
                CurrentFatigueLevel = 0,
                RecoverySessionsNeeded = 0,
                CurrentStreak = 0,
                TotalSessionsCompleted = 0
            };
        }
    }
}
