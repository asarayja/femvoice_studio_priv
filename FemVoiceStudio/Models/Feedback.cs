using System;
using System.Collections.Generic;
using FemVoiceStudio.Subsystems.Analysis;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Model for tilbakemelding til brukeren
    /// </summary>
    public class Feedback
    {
        public FeedbackType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public double Improvement { get; set; } // Forventet forbedring i prosent
        public bool IsPositive { get; set; }
        
        // Resonance data
        public double ResonanceScore { get; set; }
        public double AverageF1 { get; set; }
        public double AverageF2 { get; set; }
    }
    
    public enum FeedbackType
    {
        // Pitch feedback
        PitchTooLow,
        PitchTooHigh,
        PitchInRange,
        
        // Variation feedback
        VariationTooLow,
        VariationGood,
        
        // Intonation feedback
        IntonationNeedsWork,
        IntonationGood,
        
        // Overall feedback
        OverallGood,
        OverallNeedsWork,
        
        // Volume feedback
        VolumeTooLow,
        VolumeOk,
        
        // Resonance feedback (NEW)
        ResonanceTooBack,
        ResonanceNeutral,
        ResonanceForward,
        FormantRatioOptimal,
        FormantRatioNeedsWork,
        ResonanceTooLowConfidence,
        ResonanceOptimal
    }
    
    /// <summary>
    /// Samling av tilbakemeldinger for en analyse
    /// </summary>
    public class FeedbackCollection
    {
        public List<Feedback> Feedbacks { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public double OverallScore { get; set; }
        public bool ShouldAdvanceLevel { get; set; }
        
        // Resonance data (NEW)
        public double ResonanceScore { get; set; }
        public ResonanceCategory? ResonanceCategory { get; set; }
        public double? AverageF1 { get; set; }
        public double? AverageF2 { get; set; }
        public double? AverageF3 { get; set; }
        
        public string GetFormattedFeedback()
        {
            if (Feedbacks.Count == 0)
                return "Ingen tilbakemelding tilgjengelig.";
                
            var result = "";
            foreach (var fb in Feedbacks)
            {
                if (fb.IsPositive)
                    result += $"✅ {fb.Message}\n";
                else
                    result += $"💡 {fb.Message}\n";
                    
                if (!string.IsNullOrEmpty(fb.Hint))
                    result += $"   Hint: {fb.Hint}\n";
            }
            
            // Add resonance info if available
            if (ResonanceScore > 0)
            {
                result += $"\n🎵 Resonans: {ResonanceScore:F0}% ({ResonanceCategory})";
                if (AverageF1 > 0 && AverageF2 > 0)
                    result += $"\n   Formanter: F1={AverageF1:F0}Hz, F2={AverageF2:F0}Hz";
            }
            
            return result;
        }
    }
}
