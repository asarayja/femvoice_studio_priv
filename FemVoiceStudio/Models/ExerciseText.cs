using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Model for en øvelsestekst med norsk innhold
    /// </summary>
    public class ExerciseText
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DifficultyLevel Difficulty { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        // Målparametere for denne teksten
        public double TargetMinPitch { get; set; } = 165;
        public double TargetMaxPitch { get; set; } = 255;
        public double TargetPitchVariation { get; set; } = 20.0;
        public double TargetIntonationRise { get; set; } = 0.3;
    }
}
