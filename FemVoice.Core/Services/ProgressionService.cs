using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Data;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Service som håndterer progresjonssystemet.
    /// Justerer vanskelighetsgrad basert på brukerens prestasjoner.
    /// </summary>
    public class ProgressionService
    {
        private readonly IDatabaseService _database;
        private readonly ILocalizationService _localization;
        
        // Parametre for progresjon
        private const int SessionsRequiredForPromotion = 5;
        private const double MinScoreForPromotion = 75.0;
        private const double MinConsistencyForPromotion = 60.0;
        
        // Antall økter som kreves for å gå ned i vanskelighet
        private const int SessionsForDemotionCheck = 3;
        private const double MinScoreForStay = 50.0;
        
        // Safety lock thresholds
        private const int ModerateStrainThreshold = 2; // Block on 2+ moderate strains
        private const int CriticalStrainThreshold = 1;   // Block on 1 critical strain
        private const int SafetyLockDurationDays = 2;    // Minimum 2 days before re-evaluation
        
        // In-memory safety lock state (in production, persist to database)
        private bool _safetyLockActive;
        private string? _safetyLockReason;
        private DateTime? _safetyLockExpires;
        private int _moderateStrainCount;
        private int _criticalStrainCount;
        
        /// <summary>
        /// Event raised when safety lock is engaged
        /// </summary>
        public event EventHandler<SafetyLockEventArgs>? SafetyLockEngaged;
        
        /// <summary>
        /// Event raised when safety lock is released
        /// </summary>
        public event EventHandler? SafetyLockReleased;
        
        /// <summary>
        /// Constructor with dependency injection (recommended)
        /// </summary>
        public ProgressionService(IDatabaseService database, ILocalizationService? localization = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _localization = localization ?? LocalizationService.Instance;
        }
        
        /// <summary>
        /// Constructor for backward compatibility (uses concrete DatabaseService)
        /// </summary>
        [Obsolete("Use constructor with IDatabaseService interface instead")]
        public ProgressionService(DatabaseService database) 
            : this(database, null)
        {
        }
        
        /// <summary>
        /// Evaluerer om brukeren skal avansere til neste nivå basert på siste økt
        /// </summary>
        public ProgressionResult EvaluateProgression(TrainingSession lastSession)
        {
            var settings = _database.GetUserSettings();
            var result = new ProgressionResult
            {
                CurrentDifficulty = settings.CurrentDifficulty,
                NewDifficulty = settings.CurrentDifficulty,
                Reason = _localization.GetString("Progression_NoChange")
            };
            
            // Hvis auto-advance er av, ikke endre nivå
            if (!settings.AutoAdvanceLevel)
            {
                return result;
            }
            
            // SAFETY CHECK: Blokker promotering/degradering ved aktiv sikkerhetslås —
            // men ALDRI aktivitetsstatistikken (streak/totaler/7-dagers-snitt), som må
            // oppdateres for hver fullført økt. (Tidligere returnerte denne grenen før
            // UpdateStreak/UpdateUserSettings; det var harmløst så lenge låsen aldri
            // ble engasjert fra opptaksflyten, men ville nå frosset streaken.)
            bool blockedBySafety = _safetyLockActive;
            if (blockedBySafety)
            {
                result.Reason = $"Progression blocked: {_safetyLockReason ?? "Safety concern"}";
                result.IsBlockedBySafety = true;
            }

            // Sjekk om brukeren kvalifiserer for promotering
            if (!blockedBySafety && settings.CurrentDifficulty < DifficultyLevel.Avansert)
            {
                if (lastSession.OverallScore >= MinScoreForPromotion)
                {
                    settings.SessionsAtCurrentLevel++;
                    
                    if (settings.SessionsAtCurrentLevel >= SessionsRequiredForPromotion)
                    {
                        // Promoter!
                        settings.CurrentDifficulty++;
                        settings.SessionsAtCurrentLevel = 0;
                        result.NewDifficulty = settings.CurrentDifficulty;
                        result.Reason = _localization.GetFormattedString("Progression_LevelUp", SessionsRequiredForPromotion, MinScoreForPromotion);
                        result.ShouldShowCelebration = true;
                    }
                    else
                    {
                        result.Reason = _localization.GetFormattedString("Progression_SessionsToNext", settings.SessionsAtCurrentLevel, SessionsRequiredForPromotion);
                    }
                }
                else
                {
                    // Tilbakestill tellingen hvis score er for lav
                    if (lastSession.OverallScore < MinScoreForStay)
                    {
                        settings.SessionsAtCurrentLevel = 0;
                    }
                }
            }
            
            // Sjekk om brukeren skal degraderes (for lavt over tid)
            if (!blockedBySafety && settings.CurrentDifficulty > DifficultyLevel.Nybegynner)
            {
                var recentSessions = _database.GetTrainingSessions(
                    DateTime.Now.AddDays(-7), 
                    DateTime.Now);
                    
                if (recentSessions.Count >= SessionsForDemotionCheck)
                {
                    var recentAvgScore = 0.0;
                    foreach (var s in recentSessions)
                    {
                        recentAvgScore += s.OverallScore;
                    }
                    recentAvgScore /= recentSessions.Count;
                    
                    if (recentAvgScore < MinScoreForStay)
                    {
                        settings.CurrentDifficulty--;
                        settings.SessionsAtCurrentLevel = 0;
                        result.NewDifficulty = settings.CurrentDifficulty;
                        result.Reason = _localization.GetString("Progression_DemotedLowScore");
                        result.ShouldShowDemotion = true;
                    }
                }
            }
            
            // Oppdater streaks og statistikk
            UpdateStreak(settings, lastSession);
            
            // Lagre endringer
            _database.UpdateUserSettings(settings);
            
            return result;
        }
        
        /// <summary>
        /// Oppdaterer streak og andre statistikker
        /// </summary>
        private void UpdateStreak(UserSettings settings, TrainingSession lastSession)
        {
            var today = DateTime.Today;
            
            if (settings.LastSessionDate.HasValue)
            {
                var daysSinceLastSession = (today - settings.LastSessionDate.Value.Date).Days;
                
                if (daysSinceLastSession == 1)
                {
                    // Konsekuttiv dag
                    settings.CurrentStreak++;
                }
                else if (daysSinceLastSession > 1)
                {
                    // Streak brutt
                    settings.CurrentStreak = 1;
                }
                // Samme dag = ikke endre streak
            }
            else
            {
                settings.CurrentStreak = 1;
            }
            
            settings.TotalSessionsCompleted++;
            settings.LastSessionDate = DateTime.Now;
            
            // Oppdater progresjonsdata for siste 7 dager
            var weekSessions = _database.GetTrainingSessions(
                DateTime.Now.AddDays(-7),
                DateTime.Now);
                
            if (weekSessions.Count > 0)
            {
                settings.AveragePitchLast7Days = 0;
                foreach (var s in weekSessions)
                {
                    settings.AveragePitchLast7Days += s.AveragePitch;
                }
                settings.AveragePitchLast7Days /= weekSessions.Count;
                
                // Konsistens = gjennomsnittlig pitch-variasjon
                double totalVariation = 0;
                foreach (var s in weekSessions)
                {
                    totalVariation += s.PitchVariation;
                }
                settings.ConsistencyScore = totalVariation / weekSessions.Count;
            }
        }
        
        /// <summary>
        /// Hent anbefalt vanskelighetsgrad for brukeren
        /// </summary>
        public DifficultyLevel GetRecommendedDifficulty()
        {
            var settings = _database.GetUserSettings();
            return settings.CurrentDifficulty;
        }
        
        /// <summary>
        /// Hent progresjonsstatus
        /// </summary>
        public ProgressionStatus GetProgressionStatus()
        {
            var settings = _database.GetUserSettings();
            var stats = _database.GetProgressionStats();
            
            return new ProgressionStatus
            {
                CurrentLevel = settings.CurrentDifficulty,
                SessionsAtCurrentLevel = settings.SessionsAtCurrentLevel,
                SessionsRequiredForPromotion = SessionsRequiredForPromotion,
                CurrentStreak = settings.CurrentStreak,
                TotalSessions = settings.TotalSessionsCompleted,
                AveragePitch = stats.AvgPitch,
                Consistency = stats.Consistency,
                AveragePitchGoal = (settings.CurrentDifficulty == DifficultyLevel.Nybegynner) ? 180 :
                                   (settings.CurrentDifficulty == DifficultyLevel.Middels) ? 200 : 220
            };
        }
        
        /// <summary>
        /// Nullstill progresjon (for testing eller ny start)
        /// </summary>
        public void ResetProgression()
        {
            var settings = new UserSettings
            {
                CurrentDifficulty = DifficultyLevel.Nybegynner,
                SessionsAtCurrentLevel = 0,
                CurrentStreak = 0,
                TotalSessionsCompleted = 0,
                AutoAdvanceLevel = true
            };
            _database.UpdateUserSettings(settings);
        }
        
        /// <summary>
        /// Genererer milepæl-feedback basert på progresjon
        /// </summary>
        public MilestoneFeedback GetMilestoneFeedback(TrainingSession lastSession)
        {
            var feedback = new MilestoneFeedback();
            var settings = _database.GetUserSettings();
            
            // Sjekk pitch-milepæler
            if (lastSession.AveragePitch >= 165 && lastSession.AveragePitch < 180)
            {
                feedback.HasMilestone = true;
                feedback.MilestoneType = "pitch_165";
                feedback.Message = _localization.GetString("Progression_MilestonePitch165");
            }
            else if (lastSession.AveragePitch >= 180 && lastSession.AveragePitch < 200)
            {
                feedback.HasMilestone = true;
                feedback.MilestoneType = "pitch_180";
                feedback.Message = _localization.GetString("Progression_MilestonePitch180");
            }
            else if (lastSession.AveragePitch >= 200)
            {
                feedback.HasMilestone = true;
                feedback.MilestoneType = "pitch_200";
                feedback.Message = _localization.GetString("Progression_MilestonePitch200");
            }
            
            // Sjekk streak-milepæler
            if (settings.CurrentStreak == 7)
            {
                feedback.HasMilestone = true;
                feedback.MilestoneType = "streak_7";
                feedback.Message = _localization.GetString("Progression_MilestoneStreak7");
            }
            else if (settings.CurrentStreak == 30)
            {
                feedback.HasMilestone = true;
                feedback.MilestoneType = "streak_30";
                feedback.Message = _localization.GetString("Progression_MilestoneStreak30");
            }
            
            // Sjekk session-milepæler
            if (settings.TotalSessionsCompleted == 1)
            {
                feedback.HasMilestone = true;
                feedback.MilestoneType = "first_session";
                feedback.Message = _localization.GetString("Progression_FirstSession");
            }
            else if (settings.TotalSessionsCompleted == 10)
            {
                feedback.HasMilestone = true;
                feedback.MilestoneType = "sessions_10";
                feedback.Message = _localization.GetString("Progression_TenSessions");
            }
            else if (settings.TotalSessionsCompleted == 50)
            {
                feedback.HasMilestone = true;
                feedback.MilestoneType = "sessions_50";
                feedback.Message = _localization.GetString("Progression_FiftySessions");
            }
            
            // Sjekk score-milepæler
            if (lastSession.OverallScore >= 90)
            {
                feedback.HasMilestone = true;
                feedback.MilestoneType = "score_90";
                feedback.Message = _localization.GetString("Progression_ExcellentScore");
            }
            
            return feedback;
        }
        
        /// <summary>
        /// Genererer progresjonsoppsummering for motivasjon
        /// </summary>
        public string GetProgressionSummary()
        {
            var settings = _database.GetUserSettings();
            var stats = _database.GetProgressionStats();
            
            var pitchChange = settings.AveragePitchLast7Days > 0 
                ? settings.AveragePitchLast7Days - 140 // Anta start-pitch
                : 0;
                
            return _localization.GetString("Progression_SummaryTitle") + "\n" +
                   _localization.GetFormattedString("Progression_TotalSessions", settings.TotalSessionsCompleted) + "\n" +
                   _localization.GetFormattedString("Progression_AveragePitchLine", stats.AvgPitch) + "\n" +
                   _localization.GetFormattedString("Progression_CurrentStreakLine", settings.CurrentStreak) + "\n" +
                   _localization.GetFormattedString("Progression_ConsistencyLine", settings.ConsistencyScore);
        }
        
        #region Safety Lock Methods
        
        /// <summary>
        /// Checks if progression is currently blocked by safety lock.
        /// </summary>
        /// <returns>True if progression is blocked</returns>
        public bool IsProgressionBlocked()
        {
            // Check if lock has expired
            if (_safetyLockActive && _safetyLockExpires.HasValue && DateTime.Now >= _safetyLockExpires.Value)
            {
                ReleaseSafetyLock();
            }
            
            return _safetyLockActive;
        }
        
        /// <summary>
        /// Gets the current safety lock status.
        /// </summary>
        public SafetyLockStatus GetSafetyLockStatus()
        {
            return new SafetyLockStatus
            {
                IsBlocked = _safetyLockActive,
                Reason = _safetyLockReason,
                ExpiresAt = _safetyLockExpires,
                ModerateStrainCount = _moderateStrainCount,
                CriticalStrainCount = _criticalStrainCount
            };
        }
        
        /// <summary>
        /// Records a strain incident and applies safety lock if thresholds are exceeded.
        /// </summary>
        /// <param name="strainLevel">Strain level (0-100)</param>
        /// <param name="strainType">Type of strain detected</param>
        public void RecordStrainIncident(int strainLevel, string? strainType = null)
        {
            bool wasBlocked = _safetyLockActive;
            
            if (strainLevel >= 70) // Critical strain
            {
                _criticalStrainCount++;
                
                // Block immediately on critical strain
                if (!wasBlocked)
                {
                    ApplySafetyLock($"Critical strain detected ({strainLevel}%)", strainType);
                }
            }
            else if (strainLevel >= 40) // Moderate strain
            {
                _moderateStrainCount++;
                
                // Block on 2+ moderate strains
                if (_moderateStrainCount >= ModerateStrainThreshold && !wasBlocked)
                {
                    ApplySafetyLock($"Multiple moderate strain incidents ({_moderateStrainCount})", strainType);
                }
            }
        }
        
        /// <summary>
        /// Engages the safety lock from an external clinical gate (e.g.
        /// ProgressionSafetyGate, which evaluates persisted health-event history).
        /// The gate itself is stateless — re-evaluated from history on every session —
        /// so this in-memory lock only needs to hold within the current app run.
        /// </summary>
        /// <param name="reason">Reason code from the external gate (logged, not shown raw in UI).</param>
        /// <param name="restDays">Recommended rest period before re-evaluation.</param>
        public void ApplyExternalSafetyBlock(string reason, int restDays = SafetyLockDurationDays)
        {
            if (_safetyLockActive)
                return;

            _safetyLockExpires = DateTime.Now.AddDays(Math.Max(1, restDays));
            _safetyLockActive = true;
            _safetyLockReason = reason;

            SafetyLockEngaged?.Invoke(this, new SafetyLockEventArgs
            {
                Reason = reason,
                ExpiresAt = _safetyLockExpires.Value
            });
        }

        /// <summary>
        /// Resets strain counters (call at start of new week).
        /// </summary>
        public void ResetStrainCounters()
        {
            _moderateStrainCount = 0;
            _criticalStrainCount = 0;
        }
        
        /// <summary>
        /// Applies safety lock with reason and expiration.
        /// </summary>
        private void ApplySafetyLock(string reason, string? strainType = null)
        {
            _safetyLockActive = true;
            _safetyLockReason = reason;
            _safetyLockExpires = DateTime.Now.AddDays(SafetyLockDurationDays);
            
            SafetyLockEngaged?.Invoke(this, new SafetyLockEventArgs
            {
                Reason = reason,
                StrainType = strainType,
                ExpiresAt = _safetyLockExpires.Value
            });
        }
        
        /// <summary>
        /// Releases the safety lock (after rest period).
        /// </summary>
        public bool ReleaseSafetyLock()
        {
            if (!_safetyLockActive)
                return true;
            
            _safetyLockActive = false;
            _safetyLockReason = null;
            _safetyLockExpires = null;
            _moderateStrainCount = 0;
            _criticalStrainCount = 0;
            
            SafetyLockReleased?.Invoke(this, EventArgs.Empty);
            
            return true;
        }
        
        /// <summary>
        /// Evaluates progression with safety lock check.
        /// </summary>
        /// <param name="lastSession">The last training session</param>
        /// <returns>ProgressionResult with safety lock consideration</returns>
        public ProgressionResult EvaluateProgressionWithSafety(TrainingSession lastSession)
        {
            var result = EvaluateProgression(lastSession);
            
            // Override promotion if safety lock is active
            if (_safetyLockActive && result.NewDifficulty > result.CurrentDifficulty)
            {
                result.NewDifficulty = result.CurrentDifficulty;
                result.Reason = $"Progression blocked: {_safetyLockReason ?? "Safety concern"}";
                result.ShouldShowCelebration = false;
            }
            
            return result;
        }
        
        #endregion
        
    }
    
    public class ProgressionResult
    {
        public DifficultyLevel CurrentDifficulty { get; set; }
        public DifficultyLevel NewDifficulty { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool ShouldShowCelebration { get; set; }
        public bool ShouldShowDemotion { get; set; }
        public bool IsBlockedBySafety { get; set; }
    }
    
    public class ProgressionStatus
    {
        public DifficultyLevel CurrentLevel { get; set; }
        public int SessionsAtCurrentLevel { get; set; }
        public int SessionsRequiredForPromotion { get; set; }
        public int CurrentStreak { get; set; }
        public int TotalSessions { get; set; }
        public double AveragePitch { get; set; }
        public double Consistency { get; set; }
        public double AveragePitchGoal { get; set; }
        
        public double ProgressPercentage => SessionsRequiredForPromotion > 0 
            ? (double)SessionsAtCurrentLevel / SessionsRequiredForPromotion * 100 
            : 0;
    }
    
    public class MilestoneFeedback
    {
        public bool HasMilestone { get; set; }
        public string MilestoneType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Safety lock event args.
    /// </summary>
    public class SafetyLockEventArgs : EventArgs
    {
        public string Reason { get; set; } = string.Empty;
        public string? StrainType { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
    
    /// <summary>
    /// Safety lock status information.
    /// </summary>
    public class SafetyLockStatus
    {
        public bool IsBlocked { get; set; }
        public string? Reason { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int ModerateStrainCount { get; set; }
        public int CriticalStrainCount { get; set; }
    }
}
