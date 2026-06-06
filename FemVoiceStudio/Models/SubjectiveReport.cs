using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Subjective report submitted by user after each training session.
    /// These values override automated measurements for progression decisions.
    /// </summary>
    public class SubjectiveReport
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        
        /// <summary>
        /// User's perceived comfort level during the session (1-5).
        /// 1 = Very uncomfortable, 5 = Very comfortable
        /// </summary>
        public int ComfortLevel { get; set; }
        
        /// <summary>
        /// User's perceived fatigue after the session (1-5).
        /// 1 = Not fatigued at all, 5 = Extremely fatigued
        /// </summary>
        public int FatigueFeeling { get; set; }
        
        /// <summary>
        /// Optional notes from the user about the session.
        /// </summary>
        public string? OptionalNotes { get; set; }
        
        /// <summary>
        /// When the report was submitted.
        /// </summary>
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Associated session ID (if any).
        /// </summary>
        public int? SessionId { get; set; }
        
        /// <summary>
        /// Whether the user experienced any strain or discomfort.
        /// </summary>
        public bool ExperiencedStrain { get; set; }
        
        /// <summary>
        /// Whether the user wants to continue at current intensity.
        /// </summary>
        public bool WantsToContinue { get; set; } = true;
        
        /// <summary>
        /// Calculated fatigue score from subjective report (0-100).
        /// Higher values indicate more fatigue.
        /// </summary>
        public double FatigueScore => (FatigueFeeling - 1) / 4.0 * 100;
        
        /// <summary>
        /// Whether the report indicates health concerns that should pause progression.
        /// </summary>
        public bool IndicatesHealthConcern => FatigueFeeling >= 4 || ExperiencedStrain || ComfortLevel <= 2;
    }
}
