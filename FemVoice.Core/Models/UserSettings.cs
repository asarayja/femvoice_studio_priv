using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Model for brukerinnstillinger og progresjonsdata
    /// </summary>
    public class UserSettings
    {
        public int Id { get; set; } = 1;
        public DifficultyLevel CurrentDifficulty { get; set; } = DifficultyLevel.Nybegynner;
        public double PreferredMinPitch { get; set; } = 165;
        public double PreferredMaxPitch { get; set; } = 255;
        public double AveragePitchLast7Days { get; set; }
        public double ConsistencyScore { get; set; }
        public int TotalSessionsCompleted { get; set; }
        public int CurrentStreak { get; set; }
        public DateTime? LastSessionDate { get; set; }
        public int SessionsAtCurrentLevel { get; set; }
        public double VolumeThreshold { get; set; } = 0.01;
        public bool AutoAdvanceLevel { get; set; } = true;
        public bool HearOwnVoice { get; set; } = false;
        public string Theme { get; set; } = "System";
    }
}
