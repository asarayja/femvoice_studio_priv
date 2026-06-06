using System;
using FemVoiceStudio.Subsystems.Analysis;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Model for en treningsøkt
    /// </summary>
    public class TrainingSession
    {
        public int Id { get; set; }
        
        // User association
        public int UserId { get; set; } = 1;
        
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int ExerciseTextId { get; set; }
        public double AveragePitch { get; set; }
        public double MinPitch { get; set; }
        public double MaxPitch { get; set; }
        public double PitchVariation { get; set; }
        public double IntonationScore { get; set; }
        public double OverallScore { get; set; }
        
        // Voice health score (NEW)
        public double VoiceHealthScore { get; set; }
        
        // Strain level (0-100)
        public double StrainLevel { get; set; }

        // Recovery practice counts toward healthy training frequency without being treated as a performance session.
        public bool IsRecoveryPractice { get; set; }
        
        public string Feedback { get; set; } = string.Empty;
        public DifficultyLevel DifficultyLevel { get; set; }
        
        // Resonance fields (NEW)
        public double ResonanceScore { get; set; }
        public double AverageF1 { get; set; }
        public double AverageF2 { get; set; }
        public double AverageF3 { get; set; }
        public ResonanceCategory ResonanceCategory { get; set; }
        public double SpectralCentroid { get; set; }
        
        // Beregnet varighet i sekunder
        public int DurationSeconds => EndTime.HasValue 
            ? (int)(EndTime.Value - StartTime).TotalSeconds 
            : 0;
    }
}
