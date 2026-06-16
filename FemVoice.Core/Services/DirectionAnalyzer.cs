using System;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Direction recommendation for a voice parameter
    /// </summary>
    public class DirectionRecommendation
    {
        public string Parameter { get; set; } = "";
        public Direction Direction { get; set; }
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public double ChangeAmount { get; set; }
        public string Reason { get; set; } = "";
        public string SafetyNote { get; set; } = "";
    }
    
    /// <summary>
    /// Complete direction analysis result
    /// </summary>
    public class DirectionAnalysisResult
    {
        public DirectionRecommendation Pitch { get; set; } = new();
        public DirectionRecommendation Resonance { get; set; } = new();
        public DirectionRecommendation Intonation { get; set; } = new();
        public DirectionRecommendation VoiceHealth { get; set; } = new();
        public string PrimaryFocus { get; set; } = "";
        public string Summary { get; set; } = "";
        public bool HasSafetyConcern { get; set; }
    }
    
    /// <summary>
    /// Direction Analyzer - determines direction (increase/decrease/stabilize) for each parameter
    /// Core principle: Resonance is ALWAYS prioritized over pitch
    /// </summary>
    public class DirectionAnalyzer
    {
        private const double MaxPitchIncreasePerSession = 5.0;
        private const double PitchPressThreshold = 180.0;
        private const double ResonancePoorThreshold = 40.0;
        private const double ResonanceUnstableThreshold = 30.0;
        private const double IntonationFlatThreshold = 30.0;
        private const double IntonationGoodThreshold = 40.0;
        private const double TargetMinF2 = 1400;
        
        public DirectionAnalysisResult Analyze(
            double currentPitch, 
            double pitchVariation,
            double currentF1, 
            double currentF2,
            double intonationRange,
            double strainLevel,
            DifficultyLevel difficulty)
        {
            var result = new DirectionAnalysisResult();
            
            result.Pitch = AnalyzePitch(currentPitch, pitchVariation, strainLevel, difficulty);
            result.Resonance = AnalyzeResonance(currentF1, currentF2, pitchVariation);
            result.Intonation = AnalyzeIntonation(intonationRange);
            result.VoiceHealth = AnalyzeVoiceHealth(strainLevel);
            
            result.PrimaryFocus = DeterminePrimaryFocus(result);
            result.Summary = GenerateSummary(result);
            result.HasSafetyConcern = strainLevel > 50 || currentPitch > 280;
            
            return result;
        }
        
        private DirectionRecommendation AnalyzePitch(double currentPitch, double pitchVariation, double strainLevel, DifficultyLevel difficulty)
        {
            var rec = new DirectionRecommendation
            {
                Parameter = LocalizationService.Instance["Dashboard_Pitch"],
                CurrentValue = currentPitch
            };
            
            var (targetMin, targetMax) = GetTargetPitchRange(difficulty);
            rec.TargetValue = targetMin;
            
            if (strainLevel > 50 || currentPitch > PitchPressThreshold)
            {
                rec.Direction = Direction.Decrease;
                rec.ChangeAmount = -Math.Min(10, currentPitch - targetMin);
                rec.Reason = LocalizationService.Instance["Direction_PitchPressureReason"];
                rec.SafetyNote = LocalizationService.Instance["Direction_AvoidPushing"];
                return rec;
            }
            
            if (currentPitch < targetMin)
            {
                rec.Direction = Direction.Increase;
                rec.ChangeAmount = Math.Min(MaxPitchIncreasePerSession, targetMin - currentPitch);
                rec.Reason = LocalizationService.Instance.GetFormattedString("Direction_PitchBelowRangeFormat", targetMin, targetMax);
                return rec;
            }
            
            if (currentPitch > targetMax)
            {
                rec.Direction = Direction.Decrease;
                rec.ChangeAmount = Math.Min(5, currentPitch - targetMax);
                rec.Reason = LocalizationService.Instance["Direction_PitchAboveRange"];
                rec.SafetyNote = LocalizationService.Instance["Direction_StayInComfortZone"];
                return rec;
            }
            
            rec.Direction = Direction.Stabilize;
            rec.Reason = LocalizationService.Instance["Direction_PitchInRange"];
            return rec;
        }
        
        private DirectionRecommendation AnalyzeResonance(double currentF1, double currentF2, double pitchVariation)
        {
            var rec = new DirectionRecommendation
            {
                Parameter = LocalizationService.Instance["Dashboard_Resonance"],
                CurrentValue = currentF2,
                TargetValue = TargetMinF2
            };
            
            if (currentF2 < TargetMinF2)
            {
                rec.Direction = Direction.Increase;
                rec.ChangeAmount = TargetMinF2 - currentF2;
                rec.Reason = LocalizationService.Instance["Direction_ResonanceBack"];
                rec.SafetyNote = LocalizationService.Instance["Direction_ResonanceForwardSafety"];
                return rec;
            }
            
            if (pitchVariation > ResonanceUnstableThreshold)
            {
                rec.Direction = Direction.Stabilize;
                rec.Reason = LocalizationService.Instance["Direction_ResonanceUnstable"];
                return rec;
            }
            
            rec.Direction = Direction.Maintain;
            rec.Reason = LocalizationService.Instance["Direction_ResonanceGood"];
            return rec;
        }
        
        private DirectionRecommendation AnalyzeIntonation(double intonationRange)
        {
            var rec = new DirectionRecommendation
            {
                Parameter = LocalizationService.Instance["Dashboard_Intonation"],
                CurrentValue = intonationRange,
                TargetValue = IntonationGoodThreshold
            };
            
            if (intonationRange < IntonationFlatThreshold)
            {
                rec.Direction = Direction.Increase;
                rec.ChangeAmount = IntonationGoodThreshold - intonationRange;
                rec.Reason = LocalizationService.Instance["Direction_IntonationFlat"];
                rec.SafetyNote = LocalizationService.Instance["Direction_IntonationPractice"];
                return rec;
            }
            
            rec.Direction = Direction.Maintain;
            rec.Reason = LocalizationService.Instance["Direction_IntonationGood"];
            return rec;
        }
        
        private DirectionRecommendation AnalyzeVoiceHealth(double strainLevel)
        {
            var rec = new DirectionRecommendation
            {
                Parameter = LocalizationService.Instance["Dashboard_VoiceHealth"],
                CurrentValue = 100 - strainLevel,
                TargetValue = 100
            };
            
            if (strainLevel > 50)
            {
                rec.Direction = Direction.Decrease;
                rec.Reason = LocalizationService.Instance["Direction_HealthPressure"];
                rec.SafetyNote = LocalizationService.Instance["Direction_HealthRestSafety"];
                return rec;
            }
            
            if (strainLevel > 25)
            {
                rec.Direction = Direction.Stabilize;
                rec.Reason = LocalizationService.Instance["Direction_HealthMonitorPressure"];
                return rec;
            }
            
            rec.Direction = Direction.Maintain;
            rec.Reason = LocalizationService.Instance["Direction_HealthGood"];
            return rec;
        }
        
        private (double min, double max) GetTargetPitchRange(DifficultyLevel difficulty)
        {
            return difficulty switch
            {
                DifficultyLevel.Nybegynner => (150, 200),
                DifficultyLevel.Middels => (160, 240),
                DifficultyLevel.Avansert => (165, 255),
                _ => (150, 200)
            };
        }
        
        private string DeterminePrimaryFocus(DirectionAnalysisResult result)
        {
            // Resonance ALWAYS has highest priority
            if (result.Resonance.Direction == Direction.Increase)
                return LocalizationService.Instance["Dashboard_Resonance"];
            
            // Then check voice health
            if (result.VoiceHealth.Direction == Direction.Decrease)
                return LocalizationService.Instance["Dashboard_VoiceHealth"];
            
            // Then pitch
            if (result.Pitch.Direction == Direction.Increase || result.Pitch.Direction == Direction.Decrease)
                return LocalizationService.Instance["Dashboard_Pitch"];
            
            // Then intonation
            if (result.Intonation.Direction == Direction.Increase)
                return LocalizationService.Instance["Dashboard_Intonation"];
            
            return LocalizationService.Instance["Direction_Maintenance"];
        }
        
        private string GenerateSummary(DirectionAnalysisResult result)
        {
            var parts = new List<string>();
            
            if (result.Resonance.Direction == Direction.Increase)
                parts.Add(LocalizationService.Instance["Direction_SummaryResonanceForward"]);
            else if (result.Resonance.Direction == Direction.Maintain)
                parts.Add(LocalizationService.Instance["Direction_SummaryResonanceGood"]);
            
            if (result.Pitch.Direction == Direction.Increase)
                parts.Add(LocalizationService.Instance["Direction_SummaryPitchIncrease"]);
            else if (result.Pitch.Direction == Direction.Decrease)
                parts.Add(LocalizationService.Instance["Direction_SummaryPitchDecrease"]);
            else
                parts.Add(LocalizationService.Instance["Direction_SummaryPitchStable"]);
            
            if (result.Intonation.Direction == Direction.Increase)
                parts.Add(LocalizationService.Instance["Direction_SummaryIntonationVariation"]);
            
            return string.Join(" | ", parts);
        }
    }
}
