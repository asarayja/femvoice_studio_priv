using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Training level enumeration - three phases of voice feminization training
    /// </summary>
    public enum TrainingLevel
    {
        Beginner = 1,      // Nybegynner - focus on resonance first
        Intermediate = 2,  // Middels - combined exercises
        Advanced = 3        // Avansert - natural conversation
    }
    
    /// <summary>
    /// Direction of change recommendation for a parameter
    /// </summary>
    public enum Direction
    {
        Increase,   // Need to increase/boost this parameter
        Decrease,   // Need to decrease/reduce this parameter  
        Stabilize,  // Keep stable - good progress
        Maintain    // Maintain current state
    }
    
    /// <summary>
    /// Level classification result with transition info
    /// </summary>
    public class LevelClassificationResult
    {
        public TrainingLevel CurrentLevel { get; set; }
        public TrainingLevel? SuggestedLevel { get; set; }
        public bool ShouldUpgrade { get; set; }
        public bool ShouldDowngrade { get; set; }
        public string Reason { get; set; } = "";
        public double UpgradeProgress { get; set; } // 0-100%
        public double DowngradeRisk { get; set; } // 0-100%
        public DateTime? LastTransitionDate { get; set; }
        public int DaysSinceLastTransition { get; set; }
    }
    
    /// <summary>
    /// Training Level Classification System
    /// Implements automatic classification with evidence-based progression rules:
    /// - Upgrade: FemVoiceScore > threshold in 7 of 10 recent sessions
    /// - Downgrade: FemVoiceScore < threshold in 5 of 10 recent sessions OR strain detected
    /// - Minimum 14 days between transitions
    /// </summary>
    public class LevelClassificationSystem
    {
        #region Constants
        
        // Upgrade threshold: 7 of 10 sessions above this score
        private const int UpgradeThreshold = 70;
        
        // Downgrade threshold: 5 of 10 sessions below this score
        private const int DowngradeThreshold = 50;
        
        // Minimum days between level transitions
        private const int MinDaysBetweenTransition = 14;
        
        // Window for session analysis
        private const int SessionWindowSize = 10;
        
        // Beginner pitch targets
        private const double BeginnerMinPitch = 140;
        private const double BeginnerMaxPitch = 200;
        
        // Intermediate pitch targets
        private const double IntermediateMinPitch = 155;
        private const double IntermediateMaxPitch = 230;
        
        // Advanced pitch targets
        private const double AdvancedMinPitch = 165;
        private const double AdvancedMaxPitch = 255;
        
        // Resonance targets by level
        private const double BeginnerMinResonance = 40;
        private const double BeginnerMaxResonance = 80;
        private const double IntermediateMinResonance = 55;
        private const double AdvancedMinResonance = 70;
        
        // Tolerance multipliers by level (percentage of target range)
        private const double BeginnerTolerance = 0.20;  // ±20%
        private const double IntermediateTolerance = 0.10; // ±10%
        private const double AdvancedTolerance = 0.05;  // ±5%
        
        #endregion
        
        private readonly IDatabaseService? _database;
        private readonly ILocalizationService _localization;
        
        /// <summary>
        /// Default constructor for stateless operations
        /// </summary>
        public LevelClassificationSystem() 
            : this(null, null)
        {
        }
        
        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public LevelClassificationSystem(IDatabaseService? database, ILocalizationService? localization = null)
        {
            _database = database;
            _localization = localization ?? LocalizationService.Instance;
        }
        
        /// <summary>
        /// Constructor for backward compatibility
        /// </summary>
        [Obsolete("Use constructor with IDatabaseService interface instead")]
        public LevelClassificationSystem(DatabaseService database)
            : this(database, null)
        {
        }
        
        /// <summary>
        /// Get tolerance percentage for a level
        /// </summary>
        public static double GetTolerance(TrainingLevel level)
        {
            return level switch
            {
                TrainingLevel.Beginner => BeginnerTolerance,
                TrainingLevel.Intermediate => IntermediateTolerance,
                TrainingLevel.Advanced => AdvancedTolerance,
                _ => IntermediateTolerance
            };
        }
        
        /// <summary>
        /// Get pitch target range for a level
        /// </summary>
        public static (double min, double max) GetPitchRange(TrainingLevel level)
        {
            return level switch
            {
                TrainingLevel.Beginner => (BeginnerMinPitch, BeginnerMaxPitch),
                TrainingLevel.Intermediate => (IntermediateMinPitch, IntermediateMaxPitch),
                TrainingLevel.Advanced => (AdvancedMinPitch, AdvancedMaxPitch),
                _ => (BeginnerMinPitch, BeginnerMaxPitch)
            };
        }
        
        /// <summary>
        /// Get resonance minimum for a level
        /// </summary>
        public static double GetResonanceMinimum(TrainingLevel level)
        {
            return level switch
            {
                TrainingLevel.Beginner => BeginnerMinResonance,
                TrainingLevel.Intermediate => IntermediateMinResonance,
                TrainingLevel.Advanced => AdvancedMinResonance,
                _ => BeginnerMinResonance
            };
        }
        
        /// <summary>
        /// Get human-readable level name
        /// </summary>
        public static string GetLevelName(TrainingLevel level)
        {
            return GetLevelDisplayName(level);
        }

        /// <summary>
        /// Centralized display resolution for training levels using RESX localization.
        /// This is the single place that maps a `TrainingLevel` to a localized string key.
        /// </summary>
        public static string GetLevelDisplayName(TrainingLevel level)
        {
            // Prefer neutral (base) resource values as the canonical display strings
            // (Strings.resx contains the Norwegian default values). Fall back to
            // the active LocalizationService if resource lookup fails.
            var rm = new System.Resources.ResourceManager("FemVoiceStudio.Resources.Strings", typeof(LevelClassificationSystem).Assembly);
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            return level switch
            {
                TrainingLevel.Beginner => rm.GetString("Level_Beginner", culture) ?? LocalizationService.Instance.GetString("Level_Beginner"),
                TrainingLevel.Intermediate => rm.GetString("Level_Intermediate", culture) ?? LocalizationService.Instance.GetString("Level_Intermediate"),
                TrainingLevel.Advanced => rm.GetString("Level_Advanced", culture) ?? LocalizationService.Instance.GetString("Level_Advanced"),
                _ => rm.GetString("Level_Unknown", culture) ?? LocalizationService.Instance.GetString("Level_Unknown")
            };
        }
        
        /// <summary>
        /// Get focus description for level
        /// </summary>
        public static string GetLevelFocus(TrainingLevel level)
        {
            return level switch
            {
                TrainingLevel.Beginner => LocalizationService.Instance.GetString("Level_Focus_Beginner"),
                TrainingLevel.Intermediate => LocalizationService.Instance.GetString("Level_Focus_Intermediate"),
                TrainingLevel.Advanced => LocalizationService.Instance.GetString("Level_Focus_Advanced"),
                _ => ""
            };
        }
        
        /// <summary>
        /// Get emoji indicator for level
        /// </summary>
        public static string GetLevelEmoji(TrainingLevel level)
        {
            return level switch
            {
                TrainingLevel.Beginner => "🟢",
                TrainingLevel.Intermediate => "🟡",
                TrainingLevel.Advanced => "🔵",
                _ => "⚪"
            };
        }
        
        /// <summary>
        /// Classify current level based on recent sessions
        /// </summary>
        public LevelClassificationResult Classify(List<FemVoiceScoreResult> recentScores, TrainingLevel currentLevel, DateTime? lastTransitionDate = null)
        {
            var result = new LevelClassificationResult
            {
                CurrentLevel = currentLevel,
                LastTransitionDate = lastTransitionDate,
                DaysSinceLastTransition = lastTransitionDate.HasValue 
                    ? (int)(DateTime.Now - lastTransitionDate.Value).TotalDays 
                    : int.MaxValue
            };
            
            // Need at least some data to analyze
            if (recentScores == null || recentScores.Count < 3)
            {
                result.SuggestedLevel = currentLevel;
                result.Reason = LocalizationService.Instance.GetString("Level_NotEnoughDataReason");
                return result;
            }
            
            // Take last N sessions for analysis
            var analysisSessions = recentScores
                .OrderByDescending(s => s.CalculatedAt)
                .Take(SessionWindowSize)
                .ToList();
            
            // Count sessions above/below thresholds
            int aboveUpgradeThreshold = analysisSessions.Count(s => s.OverallScore >= UpgradeThreshold);
            int belowDowngradeThreshold = analysisSessions.Count(s => s.OverallScore < DowngradeThreshold);
            
            // Check for strain in recent sessions
            bool hasStrain = analysisSessions.Any(s => 
                !string.IsNullOrEmpty(s.WarningFlags) && 
                s.WarningFlags.Contains("STRAIN"));
            
            // Calculate upgrade progress (percentage of sessions above threshold)
            result.UpgradeProgress = (double)aboveUpgradeThreshold / Math.Min(analysisSessions.Count, SessionWindowSize) * 100;
            
            // Calculate downgrade risk
            result.DowngradeRisk = (double)belowDowngradeThreshold / Math.Min(analysisSessions.Count, SessionWindowSize) * 100;
            
            // Determine if transition should happen
            bool canTransition = result.DaysSinceLastTransition >= MinDaysBetweenTransition;
            
            // Check upgrade condition: 7 of 10 sessions above threshold
            if (canTransition && aboveUpgradeThreshold >= 7 && currentLevel != TrainingLevel.Advanced)
            {
                result.ShouldUpgrade = true;
                result.SuggestedLevel = currentLevel + 1;
                result.Reason = LocalizationService.Instance.GetFormattedString("Level_Promotion_Reason", aboveUpgradeThreshold, analysisSessions.Count, UpgradeThreshold);
            }
            // Check downgrade condition: 5 of 10 sessions below threshold OR strain detected
            else if (canTransition && (belowDowngradeThreshold >= 5 || hasStrain))
            {
                result.ShouldDowngrade = true;
                result.SuggestedLevel = currentLevel > TrainingLevel.Beginner ? currentLevel - 1 : currentLevel;
                
                if (hasStrain)
                    result.Reason = LocalizationService.Instance.GetString("Level_StrainDetectedReason");
                else
                    result.Reason = LocalizationService.Instance.GetFormattedString("Level_Downgrade_Reason", belowDowngradeThreshold, analysisSessions.Count, DowngradeThreshold);
            }
            else
            {
                // No transition recommended
                result.SuggestedLevel = currentLevel;
                
                if (!canTransition)
                    result.Reason = LocalizationService.Instance.GetFormattedString("Level_MinDaysSinceChangeReason", MinDaysBetweenTransition);
                else if (aboveUpgradeThreshold >= 5)
                    result.Reason = LocalizationService.Instance.GetFormattedString("Level_GoodProgress_Reason", aboveUpgradeThreshold, analysisSessions.Count);
                else
                    result.Reason = LocalizationService.Instance.GetString("Level_ContinueCurrentReason");
            }
            
            return result;
        }
        
        /// <summary>
        /// Get recommended exercises for current level
        /// </summary>
        public static List<string> GetRecommendedExercises(TrainingLevel level)
        {
            return level switch
            {
                TrainingLevel.Beginner => new List<string>
                {
                    LocalizationService.Instance.GetString("Level_Exercise_BasicResonance"),
                    LocalizationService.Instance.GetString("Level_Exercise_HummingNasalBridge"),
                    LocalizationService.Instance.GetString("Level_Exercise_RelaxedVoice")
                },
                TrainingLevel.Intermediate => new List<string>
                {
                    LocalizationService.Instance.GetString("Level_Exercise_CombinedResonancePitch"),
                    LocalizationService.Instance.GetString("Level_Exercise_Glissando"),
                    LocalizationService.Instance.GetString("Level_Exercise_SentenceReading")
                },
                TrainingLevel.Advanced => new List<string>
                {
                    LocalizationService.Instance.GetString("Level_Exercise_Conversation"),
                    LocalizationService.Instance.GetString("Level_Exercise_FreeSpeechReflection"),
                    LocalizationService.Instance.GetString("Level_Exercise_DynamicPitchControl")
                },
                _ => new List<string>()
            };
        }
        
        /// <summary>
        /// Get focus area for current level
        /// </summary>
        public static string GetFocusArea(TrainingLevel level)
        {
            return level switch
            {
                TrainingLevel.Beginner => LocalizationService.Instance.GetString("Level_FocusArea_Beginner"),
                TrainingLevel.Intermediate => LocalizationService.Instance.GetString("Level_FocusArea_Intermediate"),
                TrainingLevel.Advanced => LocalizationService.Instance.GetString("Level_FocusArea_Advanced"),
                _ => LocalizationService.Instance.GetString("Level_FocusArea_Default")
            };
        }
        
        /// <summary>
        /// Convert DifficultyLevel to TrainingLevel
        /// </summary>
        public static TrainingLevel FromDifficultyLevel(DifficultyLevel difficulty)
        {
            return difficulty switch
            {
                DifficultyLevel.Nybegynner => TrainingLevel.Beginner,
                DifficultyLevel.Middels => TrainingLevel.Intermediate,
                DifficultyLevel.Avansert => TrainingLevel.Advanced,
                _ => TrainingLevel.Beginner
            };
        }
        
        /// <summary>
        /// Convert TrainingLevel to DifficultyLevel
        /// </summary>
        public static DifficultyLevel ToDifficultyLevel(TrainingLevel level)
        {
            return level switch
            {
                TrainingLevel.Beginner => DifficultyLevel.Nybegynner,
                TrainingLevel.Intermediate => DifficultyLevel.Middels,
                TrainingLevel.Advanced => DifficultyLevel.Avansert,
                _ => DifficultyLevel.Nybegynner
            };
        }
    }
}
