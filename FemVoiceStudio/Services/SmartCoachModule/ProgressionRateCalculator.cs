using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Services.SmartCoachModule
{
    /// <summary>
    /// Calculates safe progression rates based on user history and performance.
    /// Implements clinical principle: Adaptive progression based on improvement rate.
    /// </summary>
    public class ProgressionRateCalculator
    {
        #region Constants

        // Progression thresholds
        public const double GoodScoreThreshold = 70.0;
        public const double ExcellentScoreThreshold = 85.0;
        public const double StagnationThreshold = 5.0; // % change
        
        // Weekly progression rates (Hz)
        public const double FastProgressionRate = 3.0;
        public const double NormalProgressionRate = 2.0;
        public const double SlowProgressionRate = 1.0;
        public const double MaintenanceRate = 0.0;
        
        // Frequency recommendations
        public const int OptimalSessionsPerWeek = 3;
        public const int MinSessionsForProgress = 2;

        #endregion

        private readonly DatabaseService _database;
        private readonly ILocalizationService _localization;

        public ProgressionRateCalculator(DatabaseService database, ILocalizationService? localization = null)
        {
            _database = database;
            _localization = localization ?? LocalizationService.Instance;
        }

        /// <summary>
        /// Calculate recommended progression rate for pitch.
        /// </summary>
        public ProgressionRecommendation CalculatePitchProgression(int userId = 1)
        {
            var sessions = _database.GetRecentSessions(10, userId);
            var recommendation = new ProgressionRecommendation
            {
                Parameter = "pitch",
                CalculatedAt = DateTime.Now
            };

            if (sessions.Count < 3)
            {
                recommendation.Rate = 0;
                recommendation.Confidence = "low";
                recommendation.Reason = _localization.GetString("ProgressionRate_NotEnoughData");
                return recommendation;
            }

            // Calculate improvement rate
            var improvement = CalculateImprovementRate(sessions);
            
            // Determine rate based on improvement
            if (improvement > 15)
            {
                recommendation.Rate = FastProgressionRate;
                recommendation.Confidence = "high";
                recommendation.Reason = _localization.GetString("ProgressionRate_GoodImprovement");
            }
            else if (improvement > 5)
            {
                recommendation.Rate = NormalProgressionRate;
                "medium".Equals(recommendation.Confidence = "medium");
                recommendation.Reason = _localization.GetString("ProgressionRate_ModerateImprovement");
            }
            else if (improvement > -5)
            {
                recommendation.Rate = SlowProgressionRate;
                recommendation.Confidence = "medium";
                recommendation.Reason = _localization.GetString("ProgressionRate_Stagnation");
            }
            else
            {
                recommendation.Rate = MaintenanceRate;
                recommendation.Confidence = "high";
                recommendation.Reason = _localization.GetString("ProgressionRate_NoImprovement");
            }

            return recommendation;
        }

        /// <summary>
        /// Calculate recommended progression rate for resonance.
        /// </summary>
        public ProgressionRecommendation CalculateResonanceProgression(int userId = 1)
        {
            var sessions = _database.GetRecentSessions(10, userId);
            var recommendation = new ProgressionRecommendation
            {
                Parameter = "resonance",
                CalculatedAt = DateTime.Now
            };

            if (sessions.Count < 3)
            {
                recommendation.Rate = 0;
                recommendation.Confidence = "low";
                recommendation.Reason = _localization.GetString("ProgressionRate_NotEnoughData");
                return recommendation;
            }

            // Resonance typically progresses slower than pitch
            var avgResonance = sessions.Average(s => s.ResonanceScore);
            
            if (avgResonance < 50)
            {
                recommendation.Rate = 0;
                recommendation.Confidence = "medium";
                recommendation.Reason = _localization.GetString("ProgressionRate_ResonanceFoundation");
            }
            else if (avgResonance < 70)
            {
                recommendation.Rate = 3.0; // 3% per week
                recommendation.Confidence = "medium";
                recommendation.Reason = _localization.GetString("ProgressionRate_ResonanceDevelopment");
            }
            else
            {
                recommendation.Rate = 2.0;
                recommendation.Confidence = "high";
                recommendation.Reason = _localization.GetString("ProgressionRate_StrongResonance");
            }

            return recommendation;
        }

        /// <summary>
        /// Calculate recommended training frequency.
        /// </summary>
        public TrainingFrequencyRecommendation CalculateTrainingFrequency(int userId = 1)
        {
            var thisWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var sessionsThisWeek = _database.GetTrainingSessions(thisWeekStart, DateTime.Today);
            
            var recommendation = new TrainingFrequencyRecommendation
            {
                SessionsThisWeek = sessionsThisWeek.Count,
                RecommendedSessions = OptimalSessionsPerWeek,
                RecommendedDays = new[]
                {
                    _localization.GetString("Day_Sunday"),
                    _localization.GetString("Day_Tuesday"),
                    _localization.GetString("Day_Thursday")
                }
            };

            if (sessionsThisWeek.Count >= OptimalSessionsPerWeek)
            {
                recommendation.Status = "optimal";
                recommendation.Message = _localization.GetString("ProgressionRate_FrequencyOptimal");
            }
            else if (sessionsThisWeek.Count >= MinSessionsForProgress)
            {
                recommendation.Status = "good";
                recommendation.Message = _localization.GetFormattedString("ProgressionRate_FrequencyGood", OptimalSessionsPerWeek - sessionsThisWeek.Count);
            }
            else if (sessionsThisWeek.Count > 0)
            {
                recommendation.Status = "minimal";
                recommendation.Message = _localization.GetString("ProgressionRate_FrequencyMinimal");
            }
            else
            {
                recommendation.Status = "none";
                recommendation.Message = _localization.GetString("ProgressionRate_FrequencyNone");
            }

            return recommendation;
        }

        /// <summary>
        /// Calculate overall progression summary.
        /// </summary>
        public ProgressionSummary GetProgressionSummary(int userId = 1)
        {
            var summary = new ProgressionSummary
            {
                UserId = userId,
                CalculatedAt = DateTime.Now
            };

            var sessions = _database.GetRecentSessions(14, userId);
            
            if (sessions.Count == 0)
            {
                summary.HasData = false;
                summary.Message = _localization.GetString("ProgressionRate_StartTrainingSummary");
                return summary;
            }

            summary.HasData = true;
            summary.TotalSessions = sessions.Count;
            summary.AverageScore = sessions.Average(s => s.OverallScore);
            
            // Calculate weekly progress
            var thisWeek = sessions.Where(s => s.StartTime >= DateTime.Today.AddDays(-7)).ToList();
            var lastWeek = sessions.Where(s => s.StartTime >= DateTime.Today.AddDays(-14) && s.StartTime < DateTime.Today.AddDays(-7)).ToList();
            
            if (thisWeek.Count > 0 && lastWeek.Count > 0)
            {
                summary.WeeklyChange = thisWeek.Average(s => s.OverallScore) - lastWeek.Average(s => s.OverallScore);
            }

            // Determine trend
            var recentGoodSessions = sessions.Take(5).Count(s => s.OverallScore >= GoodScoreThreshold);
            if (recentGoodSessions >= 4)
            {
                summary.Trend = "improving";
                summary.TrendDescription = _localization.GetString("ProgressionRate_TrendImproving");
            }
            else if (recentGoodSessions >= 2)
            {
                summary.Trend = "stable";
                summary.TrendDescription = _localization.GetString("ProgressionRate_TrendStable");
            }
            else
            {
                summary.Trend = "needswork";
                summary.TrendDescription = _localization.GetString("ProgressionRate_TrendNeedsWork");
            }

            return summary;
        }

        /// <summary>
        /// Calculate improvement rate from sessions.
        /// </summary>
        private double CalculateImprovementRate(List<TrainingSession> sessions)
        {
            if (sessions.Count < 2)
                return 0;

            // Compare first half to second half
            int half = sessions.Count / 2;
            var firstHalf = sessions.Take(half).ToList();
            var secondHalf = sessions.Skip(half).ToList();

            double firstAvg = firstHalf.Average(s => s.OverallScore);
            double secondAvg = secondHalf.Average(s => s.OverallScore);

            return secondAvg - firstAvg;
        }
    }

    #region Supporting Types

    public class ProgressionRecommendation
    {
        public string Parameter { get; set; } = "";
        public double Rate { get; set; }
        public string Confidence { get; set; } = "low";
        public string Reason { get; set; } = "";
        public DateTime CalculatedAt { get; set; }
    }

    public class TrainingFrequencyRecommendation
    {
        public int SessionsThisWeek { get; set; }
        public int RecommendedSessions { get; set; }
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public string[] RecommendedDays { get; set; } = Array.Empty<string>();
    }

    public class ProgressionSummary
    {
        public int UserId { get; set; }
        public bool HasData { get; set; }
        public string Message { get; set; } = "";
        public int TotalSessions { get; set; }
        public double AverageScore { get; set; }
        public double WeeklyChange { get; set; }
        public string Trend { get; set; } = "";
        public string TrendDescription { get; set; } = "";
        public DateTime CalculatedAt { get; set; }
    }

    #endregion
}
