using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Periodization Service - Manages training cycles with 3 active → 1 maintenance pattern.
    /// 
    /// Implements:
    /// - Active phase: 3 weeks of progressive training
    /// - Maintenance phase: 1 week of reduced intensity (70% difficulty)
    /// - Automatic phase transitions based on weekly stats
    /// - Safety integration: blocks progression on strain incidents
    /// 
    /// Clinical principle: Health overrides progression
    /// </summary>
    public class PeriodizationService
    {
        private readonly IDatabaseService _database;
        private readonly ILocalizationService _localization;
        
        private PeriodizationConfig _config;
        private PeriodizationState _state;
        
        // In-memory cache
        private List<WeeklyTrainingStats> _recentWeeks = new();
        private DateTime _lastEvaluation = DateTime.MinValue;
        
        /// <summary>
        /// Event raised when phase transition occurs
        /// </summary>
        public event EventHandler<PeriodizationResult>? PhaseTransition;
        
        /// <summary>
        /// Event raised when progression is blocked
        /// </summary>
        public event EventHandler<string>? ProgressionBlocked;
        
        /// <summary>
        /// Constructor with dependency injection (recommended)
        /// </summary>
        public PeriodizationService(IDatabaseService database, ILocalizationService? localization = null, PeriodizationConfig? config = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _localization = localization ?? LocalizationService.Instance;
            _config = config ?? new PeriodizationConfig();
            _state = LoadOrCreateState();
        }
        
        /// <summary>
        /// Constructor for backward compatibility (uses concrete DatabaseService)
        /// </summary>
        [Obsolete("Use constructor with IDatabaseService interface instead")]
        public PeriodizationService(DatabaseService database, PeriodizationConfig? config = null)
            : this(database, null, config)
        {
        }
        
        #region Public API
        
        /// <summary>
        /// Evaluates the current periodization state and returns recommendations.
        /// Should be called at the start of each week.
        /// </summary>
        /// <param name="userId">User ID (default: 1)</param>
        /// <returns>PeriodizationResult with current phase and recommendations</returns>
        public PeriodizationResult Evaluate(int userId = 1)
        {
            // Refresh weekly data if needed
            if ((DateTime.Now - _lastEvaluation).TotalHours >= 24)
            {
                RefreshWeeklyData(userId);
            }
            
            // Check if progression is still blocked
            CheckProgressionBlockExpiry();
            
            // Update strain counts from recent sessions
            UpdateStrainCounts(userId);
            
            // Determine if we should transition phases
            var shouldTransition = CheckPhaseTransition();
            
            if (shouldTransition)
            {
                TransitionPhase();
            }
            
            // Build result
            return BuildResult();
        }
        
        /// <summary>
        /// Records a completed session and updates periodization state.
        /// </summary>
        /// <param name="session">The completed training session</param>
        /// <param name="userId">User ID</param>
        public void RecordSession(TrainingSession session, int userId = 1)
        {
            // Update weekly stats
            var weekStart = GetWeekStart(session.StartTime);
            var weekStats = _recentWeeks.FirstOrDefault(w => w.WeekStart == weekStart);
            
            if (weekStats == null)
            {
                weekStats = new WeeklyTrainingStats { WeekStart = weekStart };
                _recentWeeks.Add(weekStats);
            }
            
            weekStats.SessionsCompleted++;
            var duration = session.EndTime - session.StartTime;
            weekStats.TotalMinutes += (int)(duration?.TotalMinutes ?? 0);
            weekStats.AverageScore = ((weekStats.AverageScore * (weekStats.SessionsCompleted - 1)) + session.OverallScore) 
                                      / weekStats.SessionsCompleted;
            
            // Check if this week qualifies as active
            weekStats.QualifiedAsActiveWeek = weekStats.SessionsCompleted >= _config.MinSessionsPerWeekForActive
                                              && weekStats.AverageScore >= _config.MinScoreForProgression;
            
            // Save to database
            SaveState();
        }
        
        /// <summary>
        /// Records a strain incident for safety tracking.
        /// </summary>
        /// <param name="strainLevel">The level of strain (0-100)</param>
        /// <param name="userId">User ID</param>
        public void RecordStrain(int strainLevel, int userId = 1)
        {
            bool wasBlocked = _state.IsProgressionBlocked;
            
            if (strainLevel >= 70) // Critical strain
            {
                _state.CriticalStrainCount++;
                ApplySafetyLock("Critical strain detected");
            }
            else if (strainLevel >= 40) // Moderate strain
            {
                _state.ModerateStrainCount++;
                
                // Block if 2+ moderate strains
                if (_state.ModerateStrainCount >= 2)
                {
                    ApplySafetyLock("2+ moderate strain incidents");
                }
            }
            
            // Reset strain counts if transitioning phases
            if (_state.CurrentPhase != TrainingPhase.Active)
            {
                _state.ModerateStrainCount = 0;
                _state.CriticalStrainCount = 0;
            }
            
            if (!wasBlocked && _state.IsProgressionBlocked)
            {
                ProgressionBlocked?.Invoke(this, _state.ProgressionBlockReason ?? "Unknown");
            }
            
            SaveState();
        }
        
        /// <summary>
        /// Gets the current periodization state.
        /// </summary>
        public PeriodizationState GetState() => _state;
        
        /// <summary>
        /// Gets the current configuration.
        /// </summary>
        public PeriodizationConfig GetConfig() => _config;
        
        /// <summary>
        /// Updates the periodization configuration.
        /// </summary>
        public void UpdateConfig(PeriodizationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            SaveState();
        }
        
        /// <summary>
        /// Manually transitions to a specific phase (admin function).
        /// </summary>
        public void ForcePhaseTransition(TrainingPhase newPhase)
        {
            var oldPhase = _state.CurrentPhase;
            _state.CurrentPhase = newPhase;
            _state.WeekInPhase = 1;
            _state.LastPhaseTransition = DateTime.Now;
            
            if (oldPhase != newPhase)
            {
                var result = BuildResult();
                result.PhaseTransitionOccurred = true;
                PhaseTransition?.Invoke(this, result);
            }
            
            SaveState();
        }
        
        /// <summary>
        /// Releases the progression lock after appropriate rest period.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>True if lock was released</returns>
        public bool ReleaseProgressionLock(int userId = 1)
        {
            if (!_state.IsProgressionBlocked)
                return true;
            
            // Check if lock has expired
            if (_state.ProgressionBlockExpires.HasValue && DateTime.Now >= _state.ProgressionBlockExpires.Value)
            {
                _state.IsProgressionBlocked = false;
                _state.ProgressionBlockReason = null;
                _state.ProgressionBlockExpires = null;
                _state.ModerateStrainCount = 0;
                _state.CriticalStrainCount = 0;
                SaveState();
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets a difficulty-adjusted target based on current phase.
        /// </summary>
        /// <param name="baseTarget">The base difficulty target</param>
        /// <returns>Adjusted target based on phase</returns>
        public double GetAdjustedTarget(double baseTarget)
        {
            var multiplier = _state.CurrentPhase switch
            {
                TrainingPhase.Maintenance => _config.MaintenanceDifficultyMultiplier,
                TrainingPhase.Deload => _config.DeloadDifficultyMultiplier,
                _ => 1.0
            };
            
            return baseTarget * multiplier;
        }
        
        #endregion
        
        #region Private Methods
        
        private PeriodizationState LoadOrCreateState()
        {
            // Try to load from database (stored in UserSettings or separate table)
            var settings = _database.GetUserSettings();
            
            // For now, create new state - could be extended to persist to DB
            return new PeriodizationState
            {
                CurrentPhase = TrainingPhase.Active,
                WeekInPhase = 1,
                LastPhaseTransition = DateTime.Now
            };
        }
        
        private void SaveState()
        {
            // In production, persist to database
            // For now, keep in memory
            _lastEvaluation = DateTime.Now;
        }
        
        private void RefreshWeeklyData(int userId)
        {
            _recentWeeks.Clear();
            
            // Get last 8 weeks of data
            var startDate = DateTime.Today.AddDays(-56);
            
            for (int i = 0; i < 8; i++)
            {
                var weekStart = startDate.AddDays(i * 7);
                var weekEnd = weekStart.AddDays(6);
                
                var sessions = _database.GetTrainingSessions(weekStart, weekEnd)
                    .Where(s => s.StartTime >= weekStart && s.StartTime <= weekEnd).ToList();
                
                if (sessions.Any())
                {
                    _recentWeeks.Add(new WeeklyTrainingStats
                    {
                        WeekStart = weekStart,
                        SessionsCompleted = sessions.Count,
                        TotalMinutes = (int)sessions.Sum(s => (s.EndTime - s.StartTime)?.TotalMinutes ?? 0),
                        AverageScore = sessions.Average(s => s.OverallScore),
                        QualifiedAsActiveWeek = sessions.Count >= _config.MinSessionsPerWeekForActive
                                                && sessions.Average(s => s.OverallScore) >= _config.MinScoreForProgression
                    });
                }
            }
        }
        
        private void UpdateStrainCounts(int userId)
        {
            // Get recent health monitoring data
            var recentHealth = _database.GetRecentHealthIssues(userId: userId, days: 7);
            
            if (!recentHealth.Any())
                return;
            
            // Count recent strains
            _state.ModerateStrainCount = recentHealth.Count(h => h.StrainLevel >= 40 && h.StrainLevel < 70);
            _state.CriticalStrainCount = recentHealth.Count(h => h.StrainLevel >= 70);
        }
        
        private bool CheckPhaseTransition()
        {
            if (_state.CurrentPhase == TrainingPhase.Maintenance || _state.CurrentPhase == TrainingPhase.Deload)
            {
                // In maintenance/deload, transition after 1 week
                return _state.WeekInPhase >= 1;
            }
            
            // In active phase, check if we've completed enough active weeks
            var activeWeeksThisCycle = _recentWeeks
                .Where(w => w.QualifiedAsActiveWeek)
                .Count();
            
            // Check current week's status
            var thisWeek = _recentWeeks.LastOrDefault();
            bool thisWeekActive = thisWeek?.QualifiedAsActiveWeek ?? false;
            
            // Transition if: we've had enough active weeks OR current week wasn't active
            int weeksNeeded = _config.ActiveWeeksPerCycle;
            
            return (activeWeeksThisCycle >= weeksNeeded) || 
                   (_state.WeekInPhase >= 4 && !thisWeekActive);
        }
        
        private void TransitionPhase()
        {
            var oldPhase = _state.CurrentPhase;
            
            if (_state.CurrentPhase == TrainingPhase.Active)
            {
                // Transition to maintenance
                _state.CurrentPhase = TrainingPhase.Maintenance;
                _state.WeekInPhase = 1;
                _state.CyclesCompleted++;
            }
            else
            {
                // Transition back to active from maintenance/deload
                _state.CurrentPhase = TrainingPhase.Active;
                _state.WeekInPhase = 1;
            }
            
            // Reset strain counts on phase transition
            _state.ModerateStrainCount = 0;
            _state.CriticalStrainCount = 0;
            _state.LastPhaseTransition = DateTime.Now;
            
            // Release any existing lock when transitioning phases
            if (_state.IsProgressionBlocked)
            {
                _state.IsProgressionBlocked = false;
                _state.ProgressionBlockReason = null;
                _state.ProgressionBlockExpires = null;
            }
            
            SaveState();
            
            // Raise event
            var result = BuildResult();
            result.PhaseTransitionOccurred = true;
            PhaseTransition?.Invoke(this, result);
        }
        
        private void ApplySafetyLock(string reason)
        {
            _state.IsProgressionBlocked = true;
            _state.ProgressionBlockReason = reason;
            _state.ProgressionBlockExpires = DateTime.Now.AddDays(2); // 2-day minimum rest
        }
        
        private void CheckProgressionBlockExpiry()
        {
            if (_state.IsProgressionBlocked && _state.ProgressionBlockExpires.HasValue)
            {
                if (DateTime.Now >= _state.ProgressionBlockExpires.Value)
                {
                    // Check if user has had rest - could add more sophisticated logic
                    _state.IsProgressionBlocked = false;
                    _state.ProgressionBlockReason = null;
                    _state.ProgressionBlockExpires = null;
                }
            }
        }
        
        private PeriodizationResult BuildResult()
        {
            var difficultyMultiplier = _state.CurrentPhase switch
            {
                TrainingPhase.Maintenance => _config.MaintenanceDifficultyMultiplier,
                TrainingPhase.Deload => _config.DeloadDifficultyMultiplier,
                _ => 1.0
            };
            
            var recommendedDuration = _state.CurrentPhase switch
            {
                TrainingPhase.Maintenance => 20, // 70% of typical 30 min
                TrainingPhase.Deload => 15,
                _ => 30
            };
            
            var activeWeeks = _recentWeeks.Count(w => w.QualifiedAsActiveWeek);
            var maintenanceWeeks = _recentWeeks.Count(w => !w.QualifiedAsActiveWeek && w.SessionsCompleted > 0);
            
            var result = new PeriodizationResult
            {
                Phase = _state.CurrentPhase,
                WeekInPhase = _state.WeekInPhase,
                DifficultyMultiplier = difficultyMultiplier,
                RecommendedDurationMinutes = recommendedDuration,
                PhaseTransitionOccurred = false,
                ActiveWeeksInCycle = Math.Min(activeWeeks, _config.ActiveWeeksPerCycle),
                MaintenanceWeeksInCycle = Math.Min(maintenanceWeeks, _config.MaintenanceWeeksPerCycle),
                Message = GetPhaseMessage()
            };
            
            return result;
        }
        
        private string GetPhaseMessage()
        {
            return _state.CurrentPhase switch
            {
                TrainingPhase.Active when _state.WeekInPhase == 1 =>
                    _localization.GetString("Periodization_ActiveWeek1"),
                TrainingPhase.Active when _state.WeekInPhase == 2 =>
                    _localization.GetString("Periodization_ActiveWeek2"),
                TrainingPhase.Active when _state.WeekInPhase >= 3 =>
                    _localization.GetString("Periodization_ActiveWeek3"),
                TrainingPhase.Maintenance =>
                    _localization.GetString("Periodization_Maintenance"),
                TrainingPhase.Deload =>
                    _localization.GetString("Periodization_Deload"),
                _ => _localization.GetString("Periodization_Default")
            };
        }
        
        private static DateTime GetWeekStart(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }
        
        #endregion
    }
}
