using System;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Model for resultatet av pitch-analyse
    /// </summary>
    public class PitchAnalysisResult
    {
        public double Pitch { get; set; }           // Grunnfrekvens i Hz
        public double Intensity { get; set; }        // Intensitet/volum (0-1)
        public double RmsValue { get; set; }         // RMS-verdi for volum
        public bool IsVoiced { get; set; }          // Er det stemme eller stille?
        public DateTime Timestamp { get; set; }
        public double Confidence { get; set; }       // Konfidens for pitch-deteksjon
        
        // Beregnede verdier
        public double PitchDeviation { get; set; }  // Avvik fra mål-pitch
        public bool IsInTargetRange { get; set; }    // Er pitch innenfor målområdet
    }
    
    /// <summary>
    /// Aggregert analyse for en hel økt
    /// </summary>
    public class SessionAnalysis
    {
        public double AveragePitch { get; set; }
        public double MedianPitch { get; set; }
        public double MinPitch { get; set; }
        public double MaxPitch { get; set; }
        public double PitchStandardDeviation { get; set; }
        public double PitchVariationRange { get; set; }
        public double AverageIntensity { get; set; }
        public double VoicedPercentage { get; set; }
        
        // Intonasjonsanalyse
        public double IntonationRiseScore { get; set; }  // Hvor mye stigende intonasjon
        public double IntonationFallScore { get; set; }  // Hvor mye fallende intonasjon
        public int QuestionIntonationCount { get; set; } // Antall spørsmålsintonasjoner
        
        // Tidspunkter
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        
        // Mål-sammenligning
        public double PitchTargetScore { get; set; }      // 0-100
        public double VariationTargetScore { get; set; }    // 0-100
        public double IntonationTargetScore { get; set; }  // 0-100
        public double OverallScore { get; set; }            // 0-100
    }
}
