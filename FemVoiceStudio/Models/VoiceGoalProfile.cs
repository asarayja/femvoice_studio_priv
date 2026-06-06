using System;

namespace FemVoiceStudio.Models
{
    public sealed class VoiceGoalProfile
    {
        public int UserId { get; set; } = 1;
        public string GoalStyleKey { get; set; } = "soft_feminine";
        public string PrimaryFocus { get; set; } = "";
        public string PracticeContexts { get; set; } = "";
        public string SafetyPreferences { get; set; } = "";
        public string PreferredCueStyle { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
