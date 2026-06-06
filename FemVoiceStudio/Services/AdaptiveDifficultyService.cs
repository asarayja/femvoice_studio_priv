using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Adaptiv vanskelighet basert på brukerens prestasjoner
    /// </summary>
    public class AdaptiveDifficultyService
    {
        private const int MinSessionsForPromotion = 3;
        private const int MinAvgScoreForPromotion = 70;
        private const int MaxAvgScoreForDemotion = 40;
        
        /// <summary>
        /// Evaluer om brukeren skal flyttes til annen vanskelighetsgrad
        /// </summary>
        public DifficultyRecommendation Evaluate(
            List<SessionPerformance> recentSessions,
            DifficultyLevel currentLevel)
        {
            if (recentSessions.Count < MinSessionsForPromotion)
            {
                return new DifficultyRecommendation
                {
                    RecommendedLevel = currentLevel,
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_NotEnoughData"],
                    Confidence = 0
                };
            }
            
            double avgScore = recentSessions.Average(s => s.OverallScore);
            double avgPitchAccuracy = recentSessions.Average(s => s.PitchAccuracy);
            double consistency = CalculateConsistency(recentSessions);
            
            var recommendation = new DifficultyRecommendation
            {
                RecommendedLevel = currentLevel,
                AverageScore = avgScore,
                Consistency = consistency,
                Confidence = Math.Min(1.0, recentSessions.Count / 10.0)
            };
            
            if (avgScore >= MinAvgScoreForPromotion && 
                consistency >= 60 && 
                recentSessions.Count >= MinSessionsForPromotion)
            {
                recommendation.RecommendedLevel = GetNextLevel(currentLevel);
                recommendation.Reason = LocalizationService.Instance.GetFormattedString("AdaptiveDifficulty_PromotionFormat", avgScore);
                recommendation.ShouldCelebrate = true;
            }
            else if (avgScore <= MaxAvgScoreForDemotion && recentSessions.Count >= 5)
            {
                recommendation.RecommendedLevel = GetPreviousLevel(currentLevel);
                recommendation.Reason = LocalizationService.Instance.GetFormattedString("AdaptiveDifficulty_DemotionFormat", avgScore);
                recommendation.ShouldEncourage = true;
            }
            else if (avgScore >= MinAvgScoreForPromotion)
            {
                recommendation.Reason = LocalizationService.Instance.GetFormattedString("AdaptiveDifficulty_GoodScoreFormat", avgScore);
            }
            else
            {
                recommendation.Reason = LocalizationService.Instance.GetFormattedString("AdaptiveDifficulty_KeepPracticingFormat", avgScore);
            }
            
            return recommendation;
        }
        
        /// <summary>
        /// Anbefal spesifikk øvelse basert på svakheter
        /// </summary>
        public ExerciseRecommendation RecommendExercise(List<SessionPerformance> recentSessions)
        {
            if (recentSessions.Count < 3)
            {
                return new ExerciseRecommendation
                {
                    Category = "random",
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_PracticeVariedTexts"]
                };
            }
            
            double avgPitchAccuracy = recentSessions.Average(s => s.PitchAccuracy);
            double avgVariation = recentSessions.Average(s => s.VariationScore);
            double avgIntonation = recentSessions.Average(s => s.IntonationScore);
            
            var scores = new Dictionary<string, double>
            {
                ["pitch"] = avgPitchAccuracy,
                ["variation"] = avgVariation,
                ["intonation"] = avgIntonation
            };
            
            var weakest = scores.OrderBy(s => s.Value).First();
            
            return weakest.Key switch
            {
                "pitch" => new ExerciseRecommendation
                {
                    Category = "pitch_focus",
                    TargetPitch = "tight",
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_PitchFocus"]
                },
                "variation" => new ExerciseRecommendation
                {
                    Category = "variation_focus", 
                    TargetVariation = "high",
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_VariationFocus"]
                },
                "intonation" => new ExerciseRecommendation
                {
                    Category = "question_focus",
                    TargetIntonation = "rising",
                    Reason = LocalizationService.Instance["AdaptiveDifficulty_IntonationFocus"]
                },
                _ => new ExerciseRecommendation { Category = "random" }
            };
        }
        
        private DifficultyLevel GetNextLevel(DifficultyLevel current)
        {
            return current switch
            {
                DifficultyLevel.Nybegynner => DifficultyLevel.Middels,
                DifficultyLevel.Middels => DifficultyLevel.Avansert,
                _ => DifficultyLevel.Avansert
            };
        }
        
        private DifficultyLevel GetPreviousLevel(DifficultyLevel current)
        {
            return current switch
            {
                DifficultyLevel.Avansert => DifficultyLevel.Middels,
                DifficultyLevel.Middels => DifficultyLevel.Nybegynner,
                _ => DifficultyLevel.Nybegynner
            };
        }
        
        private double CalculateConsistency(List<SessionPerformance> sessions)
        {
            if (sessions.Count < 2) return 100;
            double avg = sessions.Average(s => s.OverallScore);
            double sumSquares = sessions.Sum(s => Math.Pow(s.OverallScore - avg, 2));
            double stdDev = Math.Sqrt(sumSquares / sessions.Count);
            return Math.Max(0, 100 - stdDev);
        }
    }
    
    public class SessionPerformance
    {
        public DateTime Date { get; set; }
        public double OverallScore { get; set; }
        public double PitchAccuracy { get; set; }
        public double VariationScore { get; set; }
        public double IntonationScore { get; set; }
    }
    
    public class DifficultyRecommendation
    {
        public DifficultyLevel RecommendedLevel { get; set; }
        public string Reason { get; set; } = "";
        public double Confidence { get; set; }
        public double AverageScore { get; set; }
        public double Consistency { get; set; }
        public bool ShouldCelebrate { get; set; }
        public bool ShouldEncourage { get; set; }
    }
    
    public class ExerciseRecommendation
    {
        public string Category { get; set; } = "";
        public string? TargetPitch { get; set; }
        public string? TargetVariation { get; set; }
        public string? TargetIntonation { get; set; }
        public string Reason { get; set; } = "";
    }
}
