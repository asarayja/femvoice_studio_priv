using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Extended milestone tracking for progression system.
    /// Automatically detects and records when users achieve significant achievements.
    /// </summary>
    public class ProgressionMilestone
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 1;
        
        /// <summary>
        /// Unique identifier for the milestone type
        /// </summary>
        public string MilestoneType { get; set; } = string.Empty;
        
        /// <summary>
        /// Display name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of what was achieved
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Category for grouping milestones
        /// </summary>
        public MilestoneCategory Category { get; set; }
        
        /// <summary>
        /// Target value needed to achieve
        /// </summary>
        public double TargetValue { get; set; }
        
        /// <summary>
        /// Current progress towards milestone
        /// </summary>
        public double CurrentValue { get; set; }
        
        /// <summary>
        /// Whether milestone has been achieved
        /// </summary>
        public bool IsAchieved { get; set; }
        
        /// <summary>
        /// When milestone was achieved (null if not yet)
        /// </summary>
        public DateTime? AchievedAt { get; set; }
        
        /// <summary>
        /// Icon/emoji representation
        /// </summary>
        public string Icon { get; set; } = "🏆";
        
        /// <summary>
        /// Sort order for display
        /// </summary>
        public int SortOrder { get; set; }
        
        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double ProgressPercentage => TargetValue > 0 
            ? Math.Min(100, CurrentValue / TargetValue * 100) 
            : 0;
        
        /// <summary>
        /// Creates predefined milestones for progression system
        /// </summary>
        public static System.Collections.Generic.List<ProgressionMilestone> CreateDefaultMilestones()
        {
            return new System.Collections.Generic.List<ProgressionMilestone>
            {
                // Resonance milestones
                new ProgressionMilestone
                {
                    MilestoneType = "resonance_stable_14d",
                    Name = "Stabil resonans",
                    Description = "Stabil resonans i 14 sammenhengende dager",
                    Category = MilestoneCategory.Resonance,
                    TargetValue = 14,
                    Icon = "🎯",
                    SortOrder = 1
                },
                new ProgressionMilestone
                {
                    MilestoneType = "resonance_score_80",
                    Name = "Sterk resonans",
                    Description = "Resonansscore over 80",
                    Category = MilestoneCategory.Resonance,
                    TargetValue = 80,
                    Icon = "✨",
                    SortOrder = 2
                },
                
                // Pitch milestones
                new ProgressionMilestone
                {
                    MilestoneType = "pitch_increase_20hz",
                    Name = "20 Hz økning",
                    Description = "Pitch-komfortsone økt med 20+ Hz",
                    Category = MilestoneCategory.Pitch,
                    TargetValue = 20,
                    Icon = "📈",
                    SortOrder = 3
                },
                new ProgressionMilestone
                {
                    MilestoneType = "pitch_comfort_180",
                    Name = "Komfortsone 180",
                    Description = "Komfortsone nådd 180 Hz",
                    Category = MilestoneCategory.Pitch,
                    TargetValue = 180,
                    Icon = "🎤",
                    SortOrder = 4
                },
                new ProgressionMilestone
                {
                    MilestoneType = "pitch_comfort_200",
                    Name = "Komfortsone 200",
                    Description = "Komfortsone nådd 200 Hz",
                    Category = MilestoneCategory.Pitch,
                    TargetValue = 200,
                    Icon = "🌟",
                    SortOrder = 5
                },
                
                // Strain/Safety milestones
                new ProgressionMilestone
                {
                    MilestoneType = "sessions_no_strain_10",
                    Name = "Trygg trening",
                    Description = "10 økter uten strain-hendelser",
                    Category = MilestoneCategory.Consistency,
                    TargetValue = 10,
                    Icon = "🛡️",
                    SortOrder = 6
                },
                new ProgressionMilestone
                {
                    MilestoneType = "sessions_no_strain_50",
                    Name = "Sikker stemme",
                    Description = "50 økter uten strain-hendelser",
                    Category = MilestoneCategory.Consistency,
                    TargetValue = 50,
                    Icon = "💪",
                    SortOrder = 7
                },
                
                // Prosody milestones
                new ProgressionMilestone
                {
                    MilestoneType = "prosody_expansion_30",
                    Name = "Naturlig flyt",
                    Description = "Prosodi-range utvidet med 30%+",
                    Category = MilestoneCategory.Score,
                    TargetValue = 30,
                    Icon = "🌊",
                    SortOrder = 8
                },
                
                // Health milestones
                new ProgressionMilestone
                {
                    MilestoneType = "health_score_85_7d",
                    Name = "Høy stemmehelse",
                    Description = "VoiceHealthScore > 85 i 7 dager",
                    Category = MilestoneCategory.Score,
                    TargetValue = 7,
                    Icon = "❤️",
                    SortOrder = 9
                },
                
                // Combined milestones
                new ProgressionMilestone
                {
                    MilestoneType = "all_scores_70",
                    Name = "Balansert progresjon",
                    Description = "Første gang alle scores > 70 samtidig",
                    Category = MilestoneCategory.Score,
                    TargetValue = 70,
                    Icon = "🎖️",
                    SortOrder = 10
                },
                
                // Session milestones
                new ProgressionMilestone
                {
                    MilestoneType = "sessions_total_25",
                    Name = "Treningsglede",
                    Description = "Fullfør 25 treningsøkter",
                    Category = MilestoneCategory.Sessions,
                    TargetValue = 25,
                    Icon = "🔥",
                    SortOrder = 11
                },
                new ProgressionMilestone
                {
                    MilestoneType = "sessions_total_50",
                    Name = "Dedikasjon",
                    Description = "Fullfør 50 treningsøkter",
                    Category = MilestoneCategory.Sessions,
                    TargetValue = 50,
                    Icon = "⚡",
                    SortOrder = 12
                },
                new ProgressionMilestone
                {
                    MilestoneType = "sessions_total_100",
                    Name = "Mesterlig",
                    Description = "Fullfør 100 treningsøkter",
                    Category = MilestoneCategory.Sessions,
                    TargetValue = 100,
                    Icon = "👑",
                    SortOrder = 13
                }
            };
        }
    }
}
