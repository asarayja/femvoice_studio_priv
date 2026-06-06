using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Intensity levels for training sessions.
    /// Used to calculate weekly load and ensure appropriate progression.
    /// </summary>
    public enum IntensityLevel
    {
        /// <summary>
        /// Very light: Micro-practice, technique focus only
        /// </summary>
        Micro = 1,
        
        /// <summary>
        /// Light: Easy exercises, recovery focus
        /// </summary>
        Light = 2,
        
        /// <summary>
        /// Medium: Standard training with some challenge
        /// </summary>
        Medium = 3,
        
        /// <summary>
        /// High: Focused development, pushing comfort zone
        /// </summary>
        High = 4,
        
        /// <summary>
        /// Maximum: Only for very advanced users, limited duration
        /// </summary>
        Maximum = 5
    }
    
    /// <summary>
    /// Training focus areas for session planning.
    /// Only ONE main focus per session.
    /// </summary>
    public enum TrainingFocus
    {
        /// <summary>
        /// Resonance development - primary focus
        /// </summary>
        Resonance,
        
        /// <summary>
        /// Pitch control and extension
        /// </summary>
        Pitch,
        
        /// <summary>
        /// Prosody and natural flow
        /// </summary>
        Prosody,
        
        /// <summary>
        /// Recovery and rest
        /// </summary>
        Recovery,
        
        /// <summary>
        /// Micro-practice for daily integration
        /// </summary>
        MicroPractice,
        
        /// <summary>
        /// Full session with mixed focus
        /// </summary>
        Full
    }
    
    /// <summary>
    /// Day type in the weekly schedule.
    /// </summary>
    public enum TrainingDayType
    {
        /// <summary>
        /// High intensity progression session
        /// </summary>
        Progression,
        
        /// <summary>
        /// Medium intensity consolidation session
        /// </summary>
        Consolidation,
        
        /// <summary>
        /// Light technique practice
        /// </summary>
        Light,
        
        /// <summary>
        /// Recovery-focused session
        /// </summary>
        Recovery,
        
        /// <summary>
        /// Rest day - no training
        /// </summary>
        Rest,
        
        /// <summary>
        /// Micro-practice (1-5 minutes)
        /// </summary>
        Micro
    }
    
    /// <summary>
    /// Defines the training load for a single session.
    /// All sessions MUST include warm-up and cool-down phases.
    /// </summary>
    public class TrainingLoad
    {
        /// <summary>
        /// Unique identifier for this training load configuration.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Duration of warm-up phase (minimum 2-3 minutes required).
        /// </summary>
        public TimeSpan WarmUpDuration { get; set; } = TimeSpan.FromMinutes(3);
        
        /// <summary>
        /// Duration of main training phase.
        /// </summary>
        public TimeSpan MainTrainingDuration { get; set; } = TimeSpan.FromMinutes(10);
        
        /// <summary>
        /// Duration of cool-down phase (minimum 2 minutes required).
        /// </summary>
        public TimeSpan CoolDownDuration { get; set; } = TimeSpan.FromMinutes(2);
        
        /// <summary>
        /// Intensity level for this session.
        /// </summary>
        public IntensityLevel Intensity { get; set; } = IntensityLevel.Medium;
        
        /// <summary>
        /// Primary focus for this session.
        /// </summary>
        public TrainingFocus Focus { get; set; } = TrainingFocus.Full;
        
        /// <summary>
        /// Day type in the weekly structure.
        /// </summary>
        public TrainingDayType DayType { get; set; } = TrainingDayType.Consolidation;
        
        /// <summary>
        /// Associated exercise IDs for warm-up.
        /// </summary>
        public int[]? WarmUpExerciseIds { get; set; }
        
        /// <summary>
        /// Associated exercise IDs for main training.
        /// </summary>
        public int[]? MainExerciseIds { get; set; }
        
        /// <summary>
        /// Associated exercise IDs for cool-down.
        /// </summary>
        public int[]? CoolDownExerciseIds { get; set; }
        
        /// <summary>
        /// Total session duration.
        /// </summary>
        public TimeSpan TotalDuration => WarmUpDuration + MainTrainingDuration + CoolDownDuration;
        
        /// <summary>
        /// Total load calculated as Intensity × Duration.
        /// Used for weekly load tracking.
        /// </summary>
        public double LoadScore => (int)Intensity * TotalDuration.TotalMinutes;
        
        /// <summary>
        /// Whether this session counts toward weekly load.
        /// Micro-practice does not count.
        /// </summary>
        public bool CountsTowardWeeklyLoad => DayType != TrainingDayType.Micro;
        
        /// <summary>
        /// Default training loads for different day types.
        /// </summary>
        public static TrainingLoad CreateForDayType(TrainingDayType dayType)
        {
            return dayType switch
            {
                TrainingDayType.Progression => new TrainingLoad
                {
                    WarmUpDuration = TimeSpan.FromMinutes(3),
                    MainTrainingDuration = TimeSpan.FromMinutes(12),
                    CoolDownDuration = TimeSpan.FromMinutes(2),
                    Intensity = IntensityLevel.High,
                    Focus = TrainingFocus.Full,
                    DayType = TrainingDayType.Progression
                },
                
                TrainingDayType.Consolidation => new TrainingLoad
                {
                    WarmUpDuration = TimeSpan.FromMinutes(3),
                    MainTrainingDuration = TimeSpan.FromMinutes(10),
                    CoolDownDuration = TimeSpan.FromMinutes(2),
                    Intensity = IntensityLevel.Medium,
                    Focus = TrainingFocus.Full,
                    DayType = TrainingDayType.Consolidation
                },
                
                TrainingDayType.Light => new TrainingLoad
                {
                    WarmUpDuration = TimeSpan.FromMinutes(2),
                    MainTrainingDuration = TimeSpan.FromMinutes(6),
                    CoolDownDuration = TimeSpan.FromMinutes(2),
                    Intensity = IntensityLevel.Light,
                    Focus = TrainingFocus.Resonance,
                    DayType = TrainingDayType.Light
                },
                
                TrainingDayType.Recovery => new TrainingLoad
                {
                    WarmUpDuration = TimeSpan.FromMinutes(2),
                    MainTrainingDuration = TimeSpan.FromMinutes(5),
                    CoolDownDuration = TimeSpan.FromMinutes(3),
                    Intensity = IntensityLevel.Light,
                    Focus = TrainingFocus.Recovery,
                    DayType = TrainingDayType.Recovery
                },
                
                TrainingDayType.Micro => new TrainingLoad
                {
                    WarmUpDuration = TimeSpan.FromMinutes(0),
                    MainTrainingDuration = TimeSpan.FromMinutes(3),
                    CoolDownDuration = TimeSpan.FromMinutes(0),
                    Intensity = IntensityLevel.Micro,
                    Focus = TrainingFocus.MicroPractice,
                    DayType = TrainingDayType.Micro
                },
                
                TrainingDayType.Rest => new TrainingLoad
                {
                    WarmUpDuration = TimeSpan.Zero,
                    MainTrainingDuration = TimeSpan.Zero,
                    CoolDownDuration = TimeSpan.Zero,
                    Intensity = IntensityLevel.Micro,
                    Focus = TrainingFocus.Recovery,
                    DayType = TrainingDayType.Rest
                },
                
                _ => new TrainingLoad()
            };
        }
    }
}
