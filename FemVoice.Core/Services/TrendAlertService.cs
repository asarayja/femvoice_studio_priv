using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Trend Alert Service - trendbasert varsling for stemmehelse.
    /// 
    /// Implementerer:
    /// - VoiceHealthScore sammenligning over 3 økter
    /// - Automatisk anbefaling av hviledag ved 15+ poeng nedgang
    /// - Automatisk pitch-målsonereduksjon ved pitch >280Hz
    /// - Varsel ved intensitet >0.8 RMS
    /// 
    /// Klinisk prinsipal: Helse overstyrer all progresjon
    /// </summary>
    public class TrendAlertService
    {
        private readonly DatabaseService _database;
        private readonly ILocalizationService _localization;
        
        // Konstanter for varsling
        private const double HealthDeclineThreshold = 15.0; // Poeng
        private const int HealthDeclineSessionCount = 3;
        private const double HighIntensityThreshold = 0.8;
        private const double HighPitchThreshold = 280.0;
        private const double PitchAutoReductionHz = 10.0;
        
        public TrendAlertService(DatabaseService database, ILocalizationService? localization = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _localization = localization ?? LocalizationService.Instance;
        }
        
        #region Health Trend Alerts
        
        /// <summary>
        /// Sjekker om VoiceHealthScore har sunket 15+ poeng over 3 økter.
        /// </summary>
        /// <returns>True hvis helsescore har sunket betydelig</returns>
        public bool CheckHealthDecline(int userId = 1)
        {
            var recentSessions = _database.GetRecentSessions(HealthDeclineSessionCount, userId);
            
            if (recentSessions.Count < HealthDeclineSessionCount)
                return false;
            
            // Sorter etter tid (eldste først)
            var ordered = recentSessions.OrderBy(s => s.StartTime).ToList();
            
            // Sammenlign gjennomsnitt av første 2 med siste 1
            var earlierAvg = ordered.Take(2).Average(s => CalculateHealthScore(s));
            var latestScore = CalculateHealthScore(ordered.Last());
            
            var decline = earlierAvg - latestScore;
            
            return decline >= HealthDeclineThreshold;
        }
        
        /// <summary>
        /// Genererer anbefaling basert på helsetrend.
        /// </summary>
        public HealthTrendRecommendation GetHealthTrendRecommendation(int userId = 1)
        {
            var recommendation = new HealthTrendRecommendation
            {
                UserId = userId,
                CheckedAt = DateTime.Now
            };
            
            var hasHealthDecline = CheckHealthDecline(userId);
            
            if (hasHealthDecline)
            {
                recommendation.RecommendationType = RecommendationType.RestDay;
                recommendation.Title = _localization.GetString("TrendAlert_RestDayTitle");
                recommendation.Message = _localization.GetString("TrendAlert_RestDayMessage");
                recommendation.Severity = AlertSeverity.Warning;
                recommendation.ActionRequired = true;
            }
            else
            {
                recommendation.RecommendationType = RecommendationType.Continue;
                recommendation.Title = _localization.GetString("TrendAlert_ContinueTitle");
                recommendation.Message = _localization.GetString("TrendAlert_ContinueMessage");
                recommendation.Severity = AlertSeverity.Info;
                recommendation.ActionRequired = false;
            }
            
            return recommendation;
        }
        
        /// <summary>
        /// Beregner estimert helsescore fra en treningsøkt.
        /// </summary>
        private double CalculateHealthScore(TrainingSession session)
        {
            // Estimér helsescore basert på tilgjengelige data
            // Dette er en forenkling - i virkeligheten ville vi brukt mer detaljerte data
            if (session.OverallScore > 0)
            {
                // Basert på overall score og resonans, estimer helse
                var baseHealth = session.OverallScore;
                var resonancePenalty = session.ResonanceScore > 0 ? (100 - session.ResonanceScore) / 10 : 0;
                return Math.Max(0, Math.Min(100, baseHealth - resonancePenalty));
            }
            
            return 50; // Default
        }
        
        #endregion
        
        #region Pitch Alerts
        
        /// <summary>
        /// Sjekker om pitch overstiger sikker terskel og anbefaler reduksjon.
        /// </summary>
        public PitchAlert CheckPitchThreshold(double averagePitch, int userId = 1)
        {
            var alert = new PitchAlert
            {
                UserId = userId,
                CurrentPitch = averagePitch,
                CheckedAt = DateTime.Now
            };
            
            if (averagePitch > HighPitchThreshold)
            {
                alert.IsHighPitch = true;
                alert.RecommendedPitchReduction = (averagePitch - HighPitchThreshold) + PitchAutoReductionHz;
                alert.Message = _localization.GetFormattedString("TrendAlert_HighPitchMessage", averagePitch, HighPitchThreshold, alert.RecommendedPitchReduction);
                alert.Severity = AlertSeverity.Warning;
                alert.ActionRequired = true;
            }
            else
            {
                alert.IsHighPitch = false;
                alert.Message = _localization.GetFormattedString("TrendAlert_SafePitchMessage", averagePitch);
                alert.Severity = AlertSeverity.Info;
                alert.ActionRequired = false;
            }
            
            return alert;
        }
        
        #endregion
        
        #region Intensity Alerts
        
        /// <summary>
        /// Sjekker om intensitet overstiger anbefalt nivå.
        /// </summary>
        public IntensityAlert CheckIntensityThreshold(double intensityRms)
        {
            var alert = new IntensityAlert
            {
                CurrentIntensity = intensityRms,
                CheckedAt = DateTime.Now
            };
            
            if (intensityRms > HighIntensityThreshold)
            {
                alert.IsHighIntensity = true;
                alert.Message = _localization.GetString("TrendAlert_HighIntensityMessage");
                alert.Severity = AlertSeverity.Warning;
                alert.ActionRequired = true;
            }
            else
            {
                alert.IsHighIntensity = false;
                alert.Message = _localization.GetString("TrendAlert_IntensityOkMessage");
                alert.Severity = AlertSeverity.Info;
                alert.ActionRequired = false;
            }
            
            return alert;
        }
        
        #endregion
        
        #region Combined Alerts
        
        /// <summary>
        /// Kjører alle sikkerhetssjekker og returnerer kombinert resultat.
        /// </summary>
        public SafetyCheckResult RunSafetyCheck(double currentPitch, double intensityRms, int userId = 1)
        {
            var result = new SafetyCheckResult
            {
                UserId = userId,
                CheckedAt = DateTime.Now,
                IsSafeToContinue = true
            };
            
            // Sjekk pitch
            var pitchAlert = CheckPitchThreshold(currentPitch, userId);
            result.PitchAlert = pitchAlert;
            
            if (pitchAlert.ActionRequired)
            {
                result.IsSafeToContinue = false;
                result.Alerts.Add(pitchAlert);
            }
            
            // Sjekk intensitet
            var intensityAlert = CheckIntensityThreshold(intensityRms);
            result.IntensityAlert = intensityAlert;
            
            if (intensityAlert.ActionRequired)
            {
                result.Alerts.Add(intensityAlert);
            }
            
            // Sjekk helsetrend (ikke blokkerende, men advarsel)
            var healthRecommendation = GetHealthTrendRecommendation(userId);
            result.HealthTrendRecommendation = healthRecommendation;
            
            if (healthRecommendation.ActionRequired)
            {
                result.Alerts.Add(new BaseAlert
                {
                    Title = healthRecommendation.Title,
                    Message = healthRecommendation.Message,
                    Severity = healthRecommendation.Severity,
                    ActionRequired = healthRecommendation.ActionRequired,
                    CheckedAt = DateTime.Now
                });
            }
            
            return result;
        }
        
        #endregion
    }
    
    #region Alert Classes
    
    /// <summary>
    /// Basisklasse for alle varsler.
    /// </summary>
    public class BaseAlert
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public AlertSeverity Severity { get; set; }
        public bool ActionRequired { get; set; }
        public DateTime CheckedAt { get; set; }
    }
    
    /// <summary>
    /// Helsetrennanbefaling.
    /// </summary>
    public class HealthTrendRecommendation : BaseAlert
    {
        public int UserId { get; set; }
        public RecommendationType RecommendationType { get; set; }
    }
    
    /// <summary>
    /// Pitch-varsel.
    /// </summary>
    public class PitchAlert : BaseAlert
    {
        public int UserId { get; set; }
        public double CurrentPitch { get; set; }
        public bool IsHighPitch { get; set; }
        public double RecommendedPitchReduction { get; set; }
    }
    
    /// <summary>
    /// Intensitet-varsel.
    /// </summary>
    public class IntensityAlert : BaseAlert
    {
        public double CurrentIntensity { get; set; }
        public bool IsHighIntensity { get; set; }
    }
    
    /// <summary>
    /// Kombinerte sikkerhetsjekk-resultat.
    /// </summary>
    public class SafetyCheckResult
    {
        public int UserId { get; set; }
        public DateTime CheckedAt { get; set; }
        public bool IsSafeToContinue { get; set; }
        public List<BaseAlert> Alerts { get; set; } = new();
        
        public PitchAlert? PitchAlert { get; set; }
        public IntensityAlert? IntensityAlert { get; set; }
        public HealthTrendRecommendation? HealthTrendRecommendation { get; set; }
    }
    
    /// <summary>
    /// Varsel-alvorlighet.
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }
    
    /// <summary>
    /// Anbefalingstype.
    /// </summary>
    public enum RecommendationType
    {
        Continue,
        RestDay,
        ReduceIntensity,
        ReducePitch
    }
    
    #endregion
}
