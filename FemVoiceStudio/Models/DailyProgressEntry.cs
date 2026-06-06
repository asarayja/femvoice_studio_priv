using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Represents a daily progress entry for tracking training sessions
    /// </summary>
    public class DailyProgressEntry
    {
        public int Id { get; set; }
        
        /// <summary>
        /// User ID (typically 1 for single-user app)
        /// </summary>
        public int UserId { get; set; } = 1;
        
        /// <summary>
        /// The date of the progress entry
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// Overall FemVoice score for the day
        /// </summary>
        public double FemVoiceScore { get; set; }
        
        /// <summary>
        /// Resonance score for the day
        /// </summary>
        public double ResonanceScore { get; set; }
        
        /// <summary>
        /// Pitch score for the day
        /// </summary>
        public double PitchScore { get; set; }
        
        /// <summary>
        /// Intonation score for the day
        /// </summary>
        public double IntonationScore { get; set; }
        
        /// <summary>
        /// Voice health score for the day
        /// </summary>
        public double VoiceHealthScore { get; set; }
        
        /// <summary>
        /// Total minutes spent training that day
        /// </summary>
        public int SessionMinutes { get; set; }
        
        /// <summary>
        /// Number of sessions completed that day
        /// </summary>
        public int SessionsCompleted { get; set; }
        
        /// <summary>
        /// Type of exercise performed (e.g., "Reading", "Humming", "Vocal Fry")
        /// </summary>
        public string? ExerciseType { get; set; }
        
        /// <summary>
        /// Average pitch achieved that day (in Hz)
        /// </summary>
        public double AveragePitch { get; set; }
        
        /// <summary>
        /// Minimum pitch achieved that day (in Hz)
        /// </summary>
        public double MinPitch { get; set; }
        
        /// <summary>
        /// Maximum pitch achieved that day (in Hz)
        /// </summary>
        public double MaxPitch { get; set; }
        
        /// <summary>
        /// Notes or comments about the day's training
        /// </summary>
        public string? Notes { get; set; }
        
        /// <summary>
        /// Whether the user completed their daily goal
        /// </summary>
        public bool GoalCompleted { get; set; }
    }
}
