using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Converters;
using FemVoiceStudio.Audio;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Service som genererer tilbakemeldinger på norsk basert på analyse-resultater.
    /// Gir konstruktiv, pedagogisk feedback som hjelper brukeren å forbedre seg.
    /// </summary>
    public class FeedbackService
    {
        private readonly ILocalizationService _localization;
        
        // Målverdier for feminine stemmeparametere
        private const double TargetMinPitch = 165.0;
        private const double TargetMaxPitch = 255.0;
        private const double TargetAvgPitch = 200.0;
        private const double MinPitchVariation = 15.0; // std dev i Hz
        private const double MinIntonationScore = 0.25;
        
        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public FeedbackService(ILocalizationService? localization = null)
        {
            _localization = localization ?? LocalizationService.Instance;
        }
        
        /// <summary>
        /// Genererer tilbakemelding basert på analyse-resultater og mål-tekst
        /// </summary>
        public FeedbackCollection GenerateFeedback(SessionAnalysis analysis, ExerciseText? targetText)
        {
            var feedback = new FeedbackCollection();
            
            // Bestem målverdier (fra tekst eller standard)
            double minPitch = targetText?.TargetMinPitch ?? TargetMinPitch;
            double maxPitch = targetText?.TargetMaxPitch ?? TargetMaxPitch;
            double targetVariation = targetText?.TargetPitchVariation ?? MinPitchVariation;
            double targetIntonation = targetText?.TargetIntonationRise ?? MinIntonationScore;
            
            // 1. Pitch-vurdering
            if (analysis.AveragePitch < minPitch)
            {
                double diff = minPitch - analysis.AveragePitch;
                feedback.Feedbacks.Add(new Feedback
                {
                    Type = FeedbackType.PitchTooLow,
                    Message = _localization.GetFormattedString("Feedback_Pitch_BelowTarget", analysis.AveragePitch, minPitch, maxPitch),
                    Hint = _localization.GetString("Feedback_Hint_PitchTooLow"),
                    Improvement = Math.Min(50, diff * 2),
                    IsPositive = false
                });
            }
            else if (analysis.AveragePitch > maxPitch)
            {
                double diff = analysis.AveragePitch - maxPitch;
                feedback.Feedbacks.Add(new Feedback
                {
                    Type = FeedbackType.PitchTooHigh,
                    Message = _localization.GetFormattedString("Feedback_Pitch_AboveTarget", analysis.AveragePitch, minPitch, maxPitch),
                    Hint = _localization.GetString("Feedback_Hint_PitchTooHigh"),
                    Improvement = Math.Min(50, diff * 2),
                    IsPositive = false
                });
            }
            else
            {
                // Pitch er innenfor målområdet
                feedback.Feedbacks.Add(new Feedback
                {
                    Type = FeedbackType.PitchInRange,
                    Message = _localization.GetFormattedString("Feedback_Pitch_WithinTarget", analysis.AveragePitch),
                    Hint = _localization.GetString("Feedback_Hint_PitchInRange"),
                    Improvement = 0,
                    IsPositive = true
                });
            }
            
            // 2. Pitch-variasjon vurdering
            if (analysis.PitchStandardDeviation < targetVariation)
            {
                double diff = targetVariation - analysis.PitchStandardDeviation;
                feedback.Feedbacks.Add(new Feedback
                {
                    Type = FeedbackType.VariationTooLow,
                    Message = _localization.GetFormattedString("Feedback_PitchVariation_Low", analysis.PitchStandardDeviation, targetVariation),
                    Hint = _localization.GetString("Feedback_Hint_VariationTooLow"),
                    Improvement = Math.Min(30, diff * 3),
                    IsPositive = false
                });
            }
            else
            {
                feedback.Feedbacks.Add(new Feedback
                {
                    Type = FeedbackType.VariationGood,
                    Message = _localization.GetFormattedString("Feedback_PitchVariation_Good", analysis.PitchStandardDeviation),
                    Hint = _localization.GetString("Feedback_Hint_VariationGood"),
                    Improvement = 0,
                    IsPositive = true
                });
            }
            
            // 3. Intonasjon vurdering
            if (analysis.IntonationRiseScore < targetIntonation)
            {
                double diff = targetIntonation - analysis.IntonationRiseScore;
                feedback.Feedbacks.Add(new Feedback
                {
                    Type = FeedbackType.IntonationNeedsWork,
                    Message = _localization.GetString("Feedback_Message_IntonationLow"),
                    Hint = _localization.GetString("Feedback_Hint_IntonationNeedsWork"),
                    Improvement = Math.Min(25, diff * 50),
                    IsPositive = false
                });
            }
            else
                feedback.Feedbacks.Add(new Feedback
                {
                    Type = FeedbackType.IntonationGood,
                    Message = _localization.GetString("Feedback_Message_IntonationGood"),
                    Hint = _localization.GetString("Feedback_Hint_IntonationGood"),
                    Improvement = 0,
                    IsPositive = true
                });
            
            // 4. Beregn overall score
            feedback.OverallScore = CalculateOverallScore(analysis, minPitch, maxPitch, targetVariation, targetIntonation);
            
            // 5. Generer oppsummering
            feedback.Summary = GenerateSummary(feedback.OverallScore, feedback.Feedbacks);
            
            // 6. Bestem om brukeren skal avansere
            feedback.ShouldAdvanceLevel = feedback.OverallScore >= 75;
            
            return feedback;
        }
        
        /// <summary>
        /// Beregner overall score basert på ulike metrics
        /// </summary>
        private double CalculateOverallScore(SessionAnalysis analysis, double minPitch, double maxPitch, 
            double targetVariation, double targetIntonation)
        {
            double pitchScore = 0;
            double variationScore = 0;
            double intonationScore = 0;
            
            // Pitch score (0-100)
            if (analysis.AveragePitch >= minPitch && analysis.AveragePitch <= maxPitch)
            {
                // Beregne closeness til optimal
                double optimalDist = Math.Abs(analysis.AveragePitch - TargetAvgPitch);
                pitchScore = Math.Max(0, 100 - optimalDist * 2);
            }
            else if (analysis.AveragePitch > 0)
            {
                pitchScore = Math.Max(0, 50 - Math.Abs(analysis.AveragePitch - TargetAvgPitch));
            }
            
            // Variation score (0-100)
            if (analysis.PitchStandardDeviation > 0)
            {
                // Feminint mål: 25-50 Hz standardavvik
                double targetSD = 35; // Midtpunkt
                variationScore = Math.Max(0, 100 - Math.Abs(analysis.PitchStandardDeviation - targetSD) * 3);
            }
            
            // Intonation score (0-100)
            intonationScore = Math.Min(100, (analysis.IntonationRiseScore / targetIntonation) * 100);
            
            // Tempo score (0-100) - norsk: 160-200 WPM er mål
            double tempoScore = 50; // Standard
            if (analysis.Duration.TotalSeconds > 0)
            {
                double estimatedWPM = (analysis.Duration.TotalSeconds / 60) * 100; // Estimat
                // Mål: 160-200 WPM for feminin tale
                if (estimatedWPM >= 160 && estimatedWPM <= 200)
                    tempoScore = 100;
                else if (estimatedWPM > 200)
                    tempoScore = Math.Max(0, 100 - (estimatedWPM - 200) * 2);
                else if (estimatedWPM < 160)
                    tempoScore = Math.Max(0, 100 - (160 - estimatedWPM) * 2);
            }
            
            // Veid gjennomsnitt etter SPEC.md:
            // F0: 40%, Variasjon: 30%, Prosodi: 20%, Tempo: 10%
            return (pitchScore * 0.40) + (variationScore * 0.30) + (intonationScore * 0.20) + (tempoScore * 0.10);
        }
        
        /// <summary>
        /// Genererer en oppsummering av tilbakemeldingen
        /// </summary>
        private string GenerateSummary(double overallScore, List<Feedback> feedbacks)
        {
            var positiveCount = feedbacks.Count(f => f.IsPositive);
            var improvementCount = feedbacks.Count(f => !f.IsPositive && f.Improvement > 0);
            
            return _localization.GetFormattedString("Feedback_Summary_Top", overallScore) + "\n\n" +
                   _localization.GetFormattedString("Feedback_Summary_PositiveImprovement", positiveCount, improvementCount);
        }
        
        /// <summary>
        /// Genererer实时 tilbakemelding under en økt
        /// </summary>
        public string GetRealtimeFeedback(PitchAnalysisResult current, double targetMinPitch, double targetMaxPitch)
        {
            if (!current.IsVoiced)
            {
                return _localization.GetString("Feedback_Realtime_NoVoice");
            }
            
            if (current.Pitch < targetMinPitch)
            {
                return _localization.GetFormattedString("Realtime_PitchBelowZoneFormat", current.Pitch, targetMinPitch, targetMaxPitch);
            }
            else if (current.Pitch > targetMaxPitch)
            {
                return _localization.GetFormattedString("Realtime_PitchAboveZoneFormat", current.Pitch, targetMinPitch, targetMaxPitch);
            }
            else
            {
                return _localization.GetFormattedString("Realtime_PitchInZoneFormat", current.Pitch, targetMinPitch, targetMaxPitch);
            }
        }
        
        /// <summary>
        /// Genererer pedagogiske tips basert på brukerens historikk
        /// </summary>
        public string GetProgressiveTip(int sessionsCompleted, double avgPitch, double consistency)
        {
            if (sessionsCompleted < 3)
            {
                return Loc.Tip_Beginner;
            }
            else if (sessionsCompleted < 10)
            {
                if (avgPitch < 180)
                {
                    return Loc.Tip_Intermediate;
                }
                else
                {
                    return Loc.Tip_Intonation;
                }
            }
            else
            {
                if (consistency < 50)
                {
                    return Loc.Tip_Consistency;
                }
                else
                {
                    return Loc.Tip_Advanced;
                }
            }
        }
        
        /// <summary>
        /// Genererer resonans-tilbakemelding basert på formantanalyse.
        /// </summary>
        /// <param name="resonansResult">Resultat fra resonans-evaluering</param>
        /// <returns>Feedback med resonans-vurdering</returns>
        public Feedback GenerateResonanceFeedback(ResonansSessionResult resonansResult)
        {
            var feedback = new Feedback();
            
            if (!resonansResult.IsValid)
            {
                feedback.Type = FeedbackType.ResonanceTooLowConfidence;
                feedback.Message = _localization.GetString("Feedback_Message_IntonationLow"); // Reuse message
                feedback.Hint = _localization.GetString("Feedback_Hint_ResonanceLowConfidence");
                feedback.IsPositive = false;
                return feedback;
            }
            
            feedback.ResonanceScore = resonansResult.AverageScore;
            feedback.AverageF1 = resonansResult.AverageF1;
            feedback.AverageF2 = resonansResult.AverageF2;
            
            switch (resonansResult.Category)
            {
                case AudioResonanceCategory.ForwardResonant:
                    if (resonansResult.AverageScore >= 70)
                    {
                        feedback.Type = FeedbackType.ResonanceOptimal;
                        feedback.Message = _localization.GetFormattedString("Feedback_Resonance_Optimal", resonansResult.AverageF1, resonansResult.AverageF2);
                        feedback.Hint = _localization.GetString("Feedback_Hint_ResonanceOptimal");
                        feedback.IsPositive = true;
                    }
                    else
                    {
                        feedback.Type = FeedbackType.ResonanceForward;
                        feedback.Message = _localization.GetFormattedString("Feedback_Resonance_Good", resonansResult.AverageF1, resonansResult.AverageF2);
                        feedback.Hint = _localization.GetString("Feedback_Hint_ResonanceForward");
                        feedback.IsPositive = true;
                    }
                    break;
                    
                case AudioResonanceCategory.NeutralResonant:
                    if (resonansResult.AverageScore >= 50)
                    {
                        feedback.Type = FeedbackType.FormantRatioOptimal;
                        feedback.Message = _localization.GetString("Feedback_Message_ResonanceNeutralPotential");
                        feedback.Hint = _localization.GetString("Feedback_Hint_ResonanceNeutralPotential");
                        feedback.Improvement = 20;
                    }
                    else
                    {
                        feedback.Type = FeedbackType.FormantRatioNeedsWork;
                        feedback.Message = _localization.GetString("Feedback_Message_ResonanceNeutral");
                        feedback.Hint = _localization.GetString("Feedback_Hint_ResonanceNeedsWork");
                        feedback.Improvement = 30;
                    }
                    feedback.IsPositive = false;
                    break;
                    
                case AudioResonanceCategory.BackResonant:
                    if (resonansResult.AverageScore >= 30)
                    {
                        feedback.Type = FeedbackType.ResonanceNeutral;
                        feedback.Message = _localization.GetString("Feedback_Message_ResonanceBack");
                        feedback.Hint = _localization.GetString("Feedback_Hint_ResonanceBack");
                        feedback.Improvement = 40;
                    }
                    else
                    {
                        feedback.Type = FeedbackType.ResonanceTooBack;
                        feedback.Message = _localization.GetFormattedString("Feedback_Resonance_Back", resonansResult.AverageF1, resonansResult.AverageF2);
                        feedback.Hint = _localization.GetString("Feedback_Hint_ResonanceTooBack");
                        feedback.Improvement = 50;
                    }
                    feedback.IsPositive = false;
                    break;
            }
            
            return feedback;
        }
        
        /// <summary>
        /// Utvider FeedbackCollection med resonans-resultater.
        /// </summary>
        public void AddResonanceFeedback(FeedbackCollection collection, ResonansSessionResult resonansResult)
        {
            var resonanceFeedback = GenerateResonanceFeedback(resonansResult);
            collection.Feedbacks.Add(resonanceFeedback);
            
            // Add resonance data to collection
            collection.ResonanceScore = resonansResult.AverageScore;
            collection.ResonanceCategory = resonansResult.Category switch 
            { 
                Audio.AudioResonanceCategory.ForwardResonant => Subsystems.Analysis.ResonanceCategory.Forward,
                Audio.AudioResonanceCategory.NeutralResonant => Subsystems.Analysis.ResonanceCategory.Neutral,
                Audio.AudioResonanceCategory.BackResonant => Subsystems.Analysis.ResonanceCategory.Back,
                _ => Subsystems.Analysis.ResonanceCategory.Unknown
            };
            collection.AverageF1 = resonansResult.AverageF1;
            collection.AverageF2 = resonansResult.AverageF2;
            collection.AverageF3 = resonansResult.AverageF3;
            
            // Update overall score with resonance component
            // Original: F0: 40%, Variation: 30%, Prosodi: 20%, Tempo: 10%
            // New with resonance: F0: 30%, Resonance: 25%, Variation: 20%, Prosodi: 15%, Tempo: 10%
            if (resonansResult.IsValid)
            {
                collection.OverallScore = (collection.OverallScore * 0.70) + (resonansResult.AverageScore * 0.30);
            }
        }
    }
}
