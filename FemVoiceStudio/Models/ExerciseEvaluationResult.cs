using System;
using System.Collections.Generic;

namespace FemVoiceStudio.Models
{
    /// <summary>
    /// Evalueringsstatus for en parameter
    /// </summary>
    public enum EvaluationStatus
    {
        NotApplicable = 0,
        Correct = 1,
        Adjust = 2,
        Stop = 3
    }
    
    /// <summary>
    /// Helseindikator for stemme
    /// </summary>
    public enum HealthIndicator
    {
        Safe = 0,
        Warning = 1,
        Critical = 2
    }
    
    /// <summary>
    /// Resultatomslag returnert fra CompositeEvaluator per evaluering
    /// </summary>
    public class ExerciseEvaluationResult
    {
        /// <summary>
        /// Overall status for øvelsen
        /// </summary>
        public EvaluationStatus Status { get; set; } = EvaluationStatus.Correct;
        
        /// <summary>
        /// Resonans-status (formanter F1/F2/F3)
        /// </summary>
        public EvaluationStatus ResonanceStatus { get; set; } = EvaluationStatus.Correct;
        
        /// <summary>
        /// Pitch-status (innenfor målsone)
        /// </summary>
        public EvaluationStatus PitchStatus { get; set; } = EvaluationStatus.Correct;
        
        /// <summary>
        /// Stabilitets-status (jitter)
        /// </summary>
        public EvaluationStatus StabilityStatus { get; set; } = EvaluationStatus.Correct;
        
        /// <summary>
        /// Intonasjons-status (pitch-variasjon)
        /// </summary>
        public EvaluationStatus IntonationStatus { get; set; } = EvaluationStatus.NotApplicable;
        
        /// <summary>
        /// Helseindikator for stemmen
        /// </summary>
        public HealthIndicator HealthIndicator { get; set; } = HealthIndicator.Safe;
        
        /// <summary>
        /// Lokaliseringnøkkel for coach-hint
        /// </summary>
        public string CoachHintKey { get; set; } = "";
        
        /// <summary>
        /// Tidsstempel for evalueringen
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Detaljert informasjon for logging
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();
        
        /// <summary>
        /// Beregn samlet korrekthetsprosent (0-100)
        /// </summary>
        public double CorrectPercentage
        {
            get
            {
                int correct = 0;
                int total = 0;
                
                // Pitch
                if (PitchStatus != EvaluationStatus.NotApplicable)
                {
                    total++;
                    if (PitchStatus == EvaluationStatus.Correct) correct++;
                }
                
                // Resonance
                if (ResonanceStatus != EvaluationStatus.NotApplicable)
                {
                    total++;
                    if (ResonanceStatus == EvaluationStatus.Correct) correct++;
                }
                
                // Stability
                if (StabilityStatus != EvaluationStatus.NotApplicable)
                {
                    total++;
                    if (StabilityStatus == EvaluationStatus.Correct) correct++;
                }
                
                // Intonation
                if (IntonationStatus != EvaluationStatus.NotApplicable)
                {
                    total++;
                    if (IntonationStatus == EvaluationStatus.Correct) correct++;
                }
                
                return total > 0 ? (correct * 100.0 / total) : 100.0;
            }
        }
        
        /// <summary>
        /// Opprett et korrekt resultat
        /// </summary>
        public static ExerciseEvaluationResult Correct(string hintKey = "")
        {
            return new ExerciseEvaluationResult
            {
                Status = EvaluationStatus.Correct,
                ResonanceStatus = EvaluationStatus.Correct,
                PitchStatus = EvaluationStatus.Correct,
                StabilityStatus = EvaluationStatus.Correct,
                IntonationStatus = EvaluationStatus.NotApplicable,
                HealthIndicator = HealthIndicator.Safe,
                CoachHintKey = hintKey
            };
        }
        
        /// <summary>
        /// Opprett et resultat som krever justering
        /// </summary>
        public static ExerciseEvaluationResult Adjust(string hintKey, EvaluationStatus parameterStatus)
        {
            return new ExerciseEvaluationResult
            {
                Status = EvaluationStatus.Adjust,
                ResonanceStatus = parameterStatus == EvaluationStatus.Correct ? EvaluationStatus.Correct : EvaluationStatus.Adjust,
                PitchStatus = parameterStatus == EvaluationStatus.Correct ? EvaluationStatus.Correct : EvaluationStatus.Adjust,
                StabilityStatus = parameterStatus == EvaluationStatus.Correct ? EvaluationStatus.Correct : EvaluationStatus.Adjust,
                IntonationStatus = EvaluationStatus.NotApplicable,
                HealthIndicator = HealthIndicator.Safe,
                CoachHintKey = hintKey
            };
        }
        
        /// <summary>
        /// Opprett et resultat som krever stopp (helseproblem)
        /// </summary>
        public static ExerciseEvaluationResult Stop(string hintKey, HealthIndicator health)
        {
            return new ExerciseEvaluationResult
            {
                Status = EvaluationStatus.Stop,
                ResonanceStatus = EvaluationStatus.Stop,
                PitchStatus = EvaluationStatus.Stop,
                StabilityStatus = EvaluationStatus.Stop,
                IntonationStatus = EvaluationStatus.Stop,
                HealthIndicator = health,
                CoachHintKey = hintKey
            };
        }
        
        /// <summary>
        /// Opprett et helseadvarsel-resultat
        /// </summary>
        public static ExerciseEvaluationResult Warning(string hintKey)
        {
            return new ExerciseEvaluationResult
            {
                Status = EvaluationStatus.Adjust,
                ResonanceStatus = EvaluationStatus.Correct,
                PitchStatus = EvaluationStatus.Adjust,
                StabilityStatus = EvaluationStatus.Correct,
                IntonationStatus = EvaluationStatus.NotApplicable,
                HealthIndicator = HealthIndicator.Warning,
                CoachHintKey = hintKey
            };
        }
    }
    
    /// <summary>
    /// Akkumulert resultat for en hel økt
    /// </summary>
    public class SessionEvaluationSummary
    {
        public int SessionId { get; set; }
        public int ExerciseId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        
        // Prosent korrekt tid per parameter
        public double ResonanceCorrectPercent { get; set; }
        public double PitchCorrectPercent { get; set; }
        public double StabilityCorrectPercent { get; set; }
        public double IntonationCorrectPercent { get; set; }
        
        // Gjennomsnittsverdier
        public double AverageF1 { get; set; }
        public double AverageF2 { get; set; }
        public double AverageF3 { get; set; }
        public double AveragePitch { get; set; }
        public double AverageJitter { get; set; }
        public double AverageShimmer { get; set; }
        
        // Strain-nivå
        public double AverageStrainLevel { get; set; }
        public string StrainLevel { get; set; } = "Low"; // Low, Moderate, High
        
        // Overall score
        public double OverallScore { get; set; }
        
        // Antall stopp grunnet helse
        public int HealthStopCount { get; set; }
        
        // Varighet
        public int DurationSeconds => (int)(EndTime - StartTime).TotalSeconds;
        
        /// <summary>
        /// Beregn strain-nivå basert på shimmer og amplitude
        /// </summary>
        public static string CalculateStrainLevel(double avgShimmer, double avgStrain)
        {
            if (avgShimmer > 4.0 || avgStrain > 0.7)
                return "High";
            if (avgShimmer > 2.5 || avgStrain > 0.4)
                return "Moderate";
            return "Low";
        }
        
        /// <summary>
        /// Beregn overall score basert på parameter-presisjon
        /// </summary>
        public static double CalculateOverallScore(
            double resonancePct, double pitchPct, double stabilityPct, double healthWeight)
        {
            // Resonans teller 50%, pitch 30%, stabilitet 15%, helse 5%
            double score = resonancePct * 0.50 + pitchPct * 0.30 + stabilityPct * 0.15;
            
            // Juster for helse
            score *= healthWeight;
            
            return Math.Min(100, Math.Max(0, score));
        }
    }
}
