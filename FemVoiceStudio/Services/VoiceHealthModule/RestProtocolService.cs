using System;
using System.Timers;

namespace FemVoiceStudio.Services.VoiceHealthModule
{
    /// <summary>
    /// Manages rest periods and lock-outs after health warnings.
    /// Enforces minimum rest time and tracks daily training load.
    /// </summary>
    public class RestProtocolService : IDisposable
    {
        #region Constants

        // Rest period durations
        public const int MinimumRestSeconds = 30;
        public const int ShortRestSeconds = 30;
        public const int MediumRestSeconds = 60;
        public const int LongRestSeconds = 300; // 5 minutes
        
        // Lock-out period after critical strain
        public const int CriticalLockoutMinutes = 15;
        public const int WarningLockoutMinutes = 5;
        
        // Session limits
        public const int MaxSessionMinutes = 20;
        public const int WarningAtPercent = 80; // Warn at 80% of limit
        public const int RecommendedActiveSeconds = 15; // 15 sec active
        public const int RecommendedBreakSeconds = 30; // 30 sec break

        #endregion

        #region State

        private DateTime? _restStartTime;
        private DateTime? _lockoutEndTime;
        private DateTime _sessionStartTime;
        private bool _isResting;
        private bool _isLockedOut;
        
        // Daily tracking
        private int _totalTrainingSecondsToday;
        private DateTime _lastTrainingDate;

        // Events
        public event EventHandler? RestRequired;
        public event EventHandler? RestCompleted;
        public event EventHandler? LockoutStarted;
        public event EventHandler? LockoutEnded;
        public event EventHandler<SessionWarningEventArgs>? SessionWarning;
        public event EventHandler? SessionLimitReached;

        #endregion

        #region Properties

        public bool IsResting => _isResting;
        public bool IsLockedOut => _isLockedOut;
        public DateTime? RestStartTime => _restStartTime;
        public DateTime? LockoutEndTime => _lockoutEndTime;
        public TimeSpan RestTimeRemaining => _restStartTime.HasValue && _isResting 
            ? TimeSpan.FromSeconds(MinimumRestSeconds) - (DateTime.Now - _restStartTime.Value) 
            : TimeSpan.Zero;
        public TimeSpan LockoutTimeRemaining => _lockoutEndTime.HasValue && _isLockedOut
            ? _lockoutEndTime.Value - DateTime.Now
            : TimeSpan.Zero;
        
        public int SessionElapsedSeconds => (int)(DateTime.Now - _sessionStartTime).TotalSeconds;
        public int SessionRemainingSeconds => Math.Max(0, MaxSessionMinutes * 60 - SessionElapsedSeconds);
        public double SessionPercentComplete => (double)SessionElapsedSeconds / (MaxSessionMinutes * 60) * 100;

        #endregion

        #region Constructor

        public RestProtocolService()
        {
            ResetSession();
            LoadDailyTotal();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Start a new training session
        /// </summary>
        public void StartSession()
        {
            _sessionStartTime = DateTime.Now;
            _isResting = false;
            _isLockedOut = false;
            CheckSessionWarning();
        }

        /// <summary>
        /// End current training session
        /// </summary>
        public void EndSession()
        {
            if (_sessionStartTime != DateTime.MinValue)
            {
                int sessionDuration = SessionElapsedSeconds;
                _totalTrainingSecondsToday += sessionDuration;
                SaveDailyTotal();
            }
            
            _isResting = false;
        }

        /// <summary>
        /// Request rest after strain warning
        /// Returns true if rest is allowed, false if in lock-out
        /// </summary>
        public bool RequestRest(StrainLevel severity = StrainLevel.Warning)
        {
            // Check if locked out
            if (_isLockedOut)
            {
                return false;
            }

            int restDuration = severity switch
            {
                StrainLevel.Critical => MediumRestSeconds,
                StrainLevel.Warning => ShortRestSeconds,
                _ => MinimumRestSeconds
            };

            _isResting = true;
            _restStartTime = DateTime.Now;
            RestRequired?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// End rest period early (user choice)
        /// </summary>
        public void EndRest()
        {
            if (_isResting)
            {
                _isResting = false;
                _restStartTime = null;
                RestCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Start lock-out period after critical strain
        /// </summary>
        public void StartLockout(StrainLevel severity = StrainLevel.Critical)
        {
            int lockoutMinutes = severity switch
            {
                StrainLevel.Critical => CriticalLockoutMinutes,
                StrainLevel.Warning => WarningLockoutMinutes,
                _ => WarningLockoutMinutes
            };

            _isLockedOut = true;
            _isResting = false;
            _lockoutEndTime = DateTime.Now.AddMinutes(lockoutMinutes);
            
            LockoutStarted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Check if training is currently allowed
        /// </summary>
        public bool CanTrain()
        {
            // Check lock-out
            if (_isLockedOut)
            {
                // Check if lock-out has expired
                if (_lockoutEndTime.HasValue && DateTime.Now >= _lockoutEndTime.Value)
                {
                    _isLockedOut = false;
                    _lockoutEndTime = null;
                    LockoutEnded?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    return false;
                }
            }

            // Check session limit
            if (SessionElapsedSeconds >= MaxSessionMinutes * 60)
            {
                SessionLimitReached?.Invoke(this, EventArgs.Empty);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check and trigger session warnings
        /// </summary>
        public void CheckSessionWarning()
        {
            if (SessionPercentComplete >= WarningAtPercent)
            {
                SessionWarning?.Invoke(this, new SessionWarningEventArgs
                {
                    WarningType = SessionWarningType.ApproachingLimit,
                    Message = LocalizationService.Instance["HealthWarning_SessionApproachingLimit"],
                    RemainingSeconds = SessionRemainingSeconds
                });
            }
        }

        /// <summary>
        /// Reset all state
        /// </summary>
        public void Reset()
        {
            ResetSession();
            _totalTrainingSecondsToday = 0;
            _isLockedOut = false;
            _lockoutEndTime = null;
        }

        /// <summary>
        /// Get rest instruction text
        /// </summary>
        public string GetRestInstruction()
        {
            var loc = LocalizationService.Instance;
            
            if (_isLockedOut)
            {
                return loc["HealthWarning_LockoutActive"];
            }
            
            if (_isResting)
            {
                int remaining = (int)RestTimeRemaining.TotalSeconds;
                return loc.GetFormattedString("HealthWarning_RestRemaining", remaining);
            }
            
            return loc["HealthWarning_DefaultRest"];
        }

        /// <summary>
        /// Get daily training summary
        /// </summary>
        public (int TotalMinutes, int SessionCount, bool NearLimit) GetDailySummary()
        {
            int totalMinutes = _totalTrainingSecondsToday / 60;
            return (totalMinutes, 1, totalMinutes >= 45);
        }

        #endregion

        #region Private Methods

        private void ResetSession()
        {
            _sessionStartTime = DateTime.Now;
            _isResting = false;
            _restStartTime = null;
        }

        private void LoadDailyTotal()
        {
            try
            {
                // Load from settings/file if needed
                _lastTrainingDate = DateTime.Today;
            }
            catch
            {
                _totalTrainingSecondsToday = 0;
            }
        }

        private void SaveDailyTotal()
        {
            try
            {
                // Save to settings/file if needed
            }
            catch
            {
                // Ignore save errors
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    #region Supporting Types

    public enum SessionWarningType
    {
        ApproachingLimit,
        SessionEnded,
        RestRecommended
    }

    public class SessionWarningEventArgs : EventArgs
    {
        public SessionWarningType WarningType { get; set; }
        public string Message { get; set; } = "";
        public int RemainingSeconds { get; set; }
    }

    #endregion
}
