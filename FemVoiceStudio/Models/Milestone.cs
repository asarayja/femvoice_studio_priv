using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Represents a milestone achievement in the user's voice training journey
    /// </summary>
    public class Milestone
    {
        public int Id { get; set; }
        
        /// <summary>
        /// User ID (typically 1 for single-user app)
        /// </summary>
        public int UserId { get; set; } = 1;
        
        /// <summary>
        /// Unique identifier for the milestone type (e.g., "pitch_165", "streak_7", "sessions_10")
        /// </summary>
        public string MilestoneType { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of what the user achieved
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// When the milestone was achieved
        /// </summary>
        public DateTime? AchievedAt { get; set; }
        
        /// <summary>
        /// Display name of the milestone
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// The category of milestone (Pitch, Streak, Sessions, Score)
        /// </summary>
        public MilestoneCategory Category { get; set; }
        
        /// <summary>
        /// The target value needed to achieve this milestone
        /// </summary>
        public double TargetValue { get; set; }
        
        /// <summary>
        /// Whether the milestone has been achieved
        /// </summary>
        public bool IsAchieved { get; set; }
        
        /// <summary>
        /// Icon or emoji representing the milestone
        /// </summary>
        public string Icon { get; set; } = string.Empty;
        
        /// <summary>
        /// Sort order for display
        /// </summary>
        public int SortOrder { get; set; }
    }
    
    /// <summary>
    /// Categories for milestones
    /// </summary>
    public enum MilestoneCategory
    {
        Pitch,
        Streak,
        Sessions,
        Score,
        Resonance,
        Consistency
    }
}
