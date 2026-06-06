using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Represents the current phase in the periodization cycle.
    /// </summary>
    public enum TrainingPhase
    {
        /// <summary>Active training phase - building strength and skills</summary>
        Active,
        
        /// <summary>Maintenance phase - consolidating gains with reduced intensity</summary>
        Maintenance,
        
        /// <summary>Deload phase - active recovery with minimal load</summary>
        Deload
    }

    /// <summary>
    /// Configuration for periodization cycle settings.
    /// </summary>
    public class PeriodizationConfig
    {
        /// <summary>Number of active weeks before maintenance week</summary>
        public int ActiveWeeksPerCycle { get; set; } = 3;
        
        /// <summary>Number of maintenance weeks per cycle</summary>
        public int MaintenanceWeeksPerCycle { get; set; } = 1;
        
        /// <summary>Exercise difficulty multiplier during maintenance (0.0-1.0)</summary>
        public double MaintenanceDifficultyMultiplier { get; set; } = 0.7;
        
        /// <summary>Exercise difficulty multiplier during deload (0.0-1.0)</summary>
        public double DeloadDifficultyMultiplier { get; set; } = 0.5;
        
        /// <summary>Minimum sessions per week to count as active</summary>
        public int MinSessionsPerWeekForActive { get; set; } = 3;
        
        /// <summary>Minimum average score to progress</summary>
        public double MinScoreForProgression { get; set; } = 70.0;
    }

    /// <summary>
    /// Tracks the current state of the periodization cycle.
    /// </summary>
    public class PeriodizationState
    {
        /// <summary>Current training phase</summary>
        public TrainingPhase CurrentPhase { get; set; } = TrainingPhase.Active;
        
        /// <summary>Week number within current phase (1-based)</summary>
        public int WeekInPhase { get; set; } = 1;
        
        /// <summary>Total cycles completed</summary>
        public int CyclesCompleted { get; set; }
        
        /// <summary>Last phase transition date</summary>
        public DateTime? LastPhaseTransition { get; set; }
        
        /// <summary>Progression blocked due to health/safety</summary>
        public bool IsProgressionBlocked { get; set; }
        
        /// <summary>Reason for progression block</summary>
        public string? ProgressionBlockReason { get; set; }
        
        /// <summary>Date when progression will be re-evaluated</summary>
        public DateTime? ProgressionBlockExpires { get; set; }
        
        /// <summary>Number of moderate strain incidents in current phase</summary>
        public int ModerateStrainCount { get; set; }
        
        /// <summary>Number of critical strain incidents in current phase</summary>
        public int CriticalStrainCount { get; set; }
    }

    /// <summary>
    /// Result of periodization evaluation.
    /// </summary>
    public class PeriodizationResult
    {
        /// <summary>Current training phase</summary>
        public TrainingPhase Phase { get; set; }
        
        /// <summary>Week number within current phase</summary>
        public int WeekInPhase { get; set; }
        
        /// <summary>Recommended difficulty multiplier for exercises</summary>
        public double DifficultyMultiplier { get; set; }
        
        /// <summary>Recommended session duration in minutes</summary>
        public int RecommendedDurationMinutes { get; set; }
        
        /// <summary>Whether phase transition occurred</summary>
        public bool PhaseTransitionOccurred { get; set; }
        
        /// <summary>Message describing current state</summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>Total active weeks in current cycle</summary>
        public int ActiveWeeksInCycle { get; set; }
        
        /// <summary>Total maintenance weeks in current cycle</summary>
        public int MaintenanceWeeksInCycle { get; set; }
    }

    /// <summary>
    /// Tracks weekly training statistics for periodization.
    /// </summary>
    public class WeeklyTrainingStats
    {
        /// <summary>Week start date</summary>
        public DateTime WeekStart { get; set; }
        
        /// <summary>Number of sessions completed this week</summary>
        public int SessionsCompleted { get; set; }
        
        /// <summary>Average score this week</summary>
        public double AverageScore { get; set; }
        
        /// <summary>Total minutes trained this week</summary>
        public int TotalMinutes { get; set; }
        
        /// <summary>Whether week met active phase criteria</summary>
        public bool QualifiedAsActiveWeek { get; set; }
        
        /// <summary>Number of strain incidents this week</summary>
        public int StrainIncidents { get; set; }
    }
}
