using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services.VoiceHealthModule
{
    /// <summary>
    /// Monitors voice strain in real-time and triggers warnings/critical events.
    /// Implements escalation logic: warning -> temporary pitch reduction -> critical pause.
    /// </summary>
    public class StrainMonitor : IDisposable
    {
        #region Constants

        // Thresholds (from clinical principles)
        public const double JitterWarningThreshold = 1.5;
        public const double JitterCriticalThreshold = 2.5;
        public const double ShimmerWarningThreshold = 3.5;
        public const double ShimmerCriticalThreshold = 5.0;
        public const double AmplitudeSpikeStdDev = 2.0;
        public const double HnrWarningThreshold = 15.0;
        
        // Pitch press detection
        public const double PitchPressThreshold = 180.0;
        public const double PitchPressJitterThreshold = 1.0;
        public const double PitchPressShimmerThreshold = 2.0;
        
        // Consecutive warning limits
        public const int WarningLimitForEscalation = 3;
        public const int CriticalLimitForPause = 3;

        #endregion

        #region State

        private int _consecutiveWarnings;
        private int _consecutiveCritical;
        private DateTime _lastWarningTime;
        private DateTime _lastCriticalTime;
        private readonly Queue<StrainSnapshot> _strainHistory;
        private readonly int _historySize;
        
        // Events
        public event EventHandler<StrainEventArgs>? WarningDetected;
        public event EventHandler<StrainEventArgs>? CriticalDetected;
        public event EventHandler<StrainEventArgs>? PitchTemporarilyLowered;
        public event EventHandler? HealthRestRecommended;

        #endregion

        #region Properties

        public int ConsecutiveWarnings => _consecutiveWarnings;
        public int ConsecutiveCritical => _consecutiveCritical;
        public bool IsInWarningState => _consecutiveWarnings > 0;
        public bool IsInCriticalState => _consecutiveCritical > 0;
        public DateTime? LastWarningTime => _consecutiveWarnings > 0 ? _lastWarningTime : null;
        public DateTime? LastCriticalTime => _consecutiveCritical > 0 ? _lastCriticalTime : null;

        #endregion

        #region Constructor

        public StrainMonitor(int historySize = 100)
        {
            _historySize = historySize;
            _strainHistory = new Queue<StrainSnapshot>();
            Reset();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Process new voice metrics and check for strain indicators.
        /// Returns the recommended action based on current state.
        /// </summary>
        public StrainAction ProcessMetrics(VoiceMetrics metrics)
        {
            // Add to history
            AddToHistory(metrics);

            // Check for critical indicators
            bool hasCriticalStrain = CheckCriticalStrain(metrics);
            if (hasCriticalStrain)
            {
                _consecutiveCritical++;
                _lastCriticalTime = DateTime.Now;
                
                var args = CreateStrainEventArgs(metrics, StrainLevel.Critical);
                CriticalDetected?.Invoke(this, args);
                
                // Escalate: 3+ critical = automatic pause
                if (_consecutiveCritical >= CriticalLimitForPause)
                {
                    HealthRestRecommended?.Invoke(this, EventArgs.Empty);
                    return StrainAction.PauseAndRest;
                }
                
                return StrainAction.ContinueWithWarning;
            }

            // Check for warning indicators
            bool hasWarningStrain = CheckWarningStrain(metrics);
            if (hasWarningStrain)
            {
                _consecutiveWarnings++;
                _lastWarningTime = DateTime.Now;
                
                var args = CreateStrainEventArgs(metrics, StrainLevel.Warning);
                WarningDetected?.Invoke(this, args);
                
                // Escalation logic
                if (_consecutiveWarnings >= WarningLimitForEscalation)
                {
                    // Lower pitch temporarily (10 Hz reduction)
                    var loweredArgs = CreateStrainEventArgs(metrics, StrainLevel.Warning);
                    loweredArgs.RecommendedPitchOffset = -10;
                    PitchTemporarilyLowered?.Invoke(this, loweredArgs);
                    
                    return StrainAction.LowerPitchTemporarily;
                }
                
                return StrainAction.ContinueWithWarning;
            }

            // No strain detected - reset counters (with gradual decay)
            DecayCounters();

            return StrainAction.Continue;
        }

        /// <summary>
        /// Reset monitor state for new session
        /// </summary>
        public void Reset()
        {
            _consecutiveWarnings = 0;
            _consecutiveCritical = 0;
            _lastWarningTime = DateTime.MinValue;
            _lastCriticalTime = DateTime.MinValue;
            _strainHistory.Clear();
        }

        /// <summary>
        /// Get current strain level assessment
        /// </summary>
        public StrainLevel GetCurrentStrainLevel()
        {
            if (_consecutiveCritical >= CriticalLimitForPause || _strainHistory.Count == 0)
                return StrainLevel.Critical;
            
            if (_consecutiveWarnings >= WarningLimitForEscalation)
                return StrainLevel.Warning;
            
            if (_consecutiveWarnings > 0)
                return StrainLevel.Mild;
            
            return StrainLevel.Normal;
        }

        /// <summary>
        /// Get strain statistics from history
        /// </summary>
        public StrainStatistics GetStatistics()
        {
            var stats = new StrainStatistics();
            
            if (_strainHistory.Count == 0)
                return stats;

            var recentHistory = _strainHistory.ToArray();
            
            stats.TotalSamples = recentHistory.Length;
            stats.WarningCount = recentHistory.Count(s => s.Level == StrainLevel.Warning);
            stats.CriticalCount = recentHistory.Count(s => s.Level == StrainLevel.Critical);
            stats.AverageJitter = recentHistory.Average(s => s.Jitter);
            stats.AverageShimmer = recentHistory.Average(s => s.Shimmer);
            stats.AverageHnr = recentHistory.Where(s => s.HNR > 0).Select(s => s.HNR).DefaultIfEmpty(0).Average();
            stats.MaxStrainLevel = recentHistory.Max(s => s.StrainValue);
            
            return stats;
        }

        #endregion

        #region Private Methods

        private void AddToHistory(VoiceMetrics metrics)
        {
            var snapshot = new StrainSnapshot
            {
                Timestamp = metrics.Timestamp,
                Jitter = metrics.Jitter,
                Shimmer = metrics.Shimmer,
                HNR = metrics.HNR,
                Intensity = metrics.Intensity,
                Pitch = metrics.Pitch,
                StrainValue = metrics.StrainLevel,
                Level = GetSnapshotLevel(metrics)
            };

            _strainHistory.Enqueue(snapshot);
            
            if (_strainHistory.Count > _historySize)
            {
                _strainHistory.Dequeue();
            }
        }

        private StrainLevel GetSnapshotLevel(VoiceMetrics metrics)
        {
            if (metrics.Jitter > JitterCriticalThreshold || 
                metrics.Shimmer > ShimmerCriticalThreshold ||
                metrics.StrainLevel > 0.75)
            {
                return StrainLevel.Critical;
            }
            
            if (metrics.Jitter > JitterWarningThreshold ||
                metrics.Shimmer > ShimmerWarningThreshold ||
                metrics.IsPitchPress)
            {
                return StrainLevel.Warning;
            }
            
            return StrainLevel.Normal;
        }

        private bool CheckCriticalStrain(VoiceMetrics metrics)
        {
            // Jitter critical
            if (metrics.Jitter > JitterCriticalThreshold)
                return true;
            
            // Shimmer critical
            if (metrics.Shimmer > ShimmerCriticalThreshold)
                return true;
            
            // Very high strain
            if (metrics.StrainLevel > 0.75)
                return true;
            
            // Pitch press with instability
            if (metrics.Pitch > PitchPressThreshold && 
                (metrics.Jitter > PitchPressJitterThreshold || 
                 metrics.Shimmer > PitchPressShimmerThreshold))
                return true;
            
            // Amplitude spike
            if (metrics.HasAmplitudeSpike)
                return true;
            
            return false;
        }

        private bool CheckWarningStrain(VoiceMetrics metrics)
        {
            // Jitter warning
            if (metrics.Jitter > JitterWarningThreshold)
                return true;
            
            // Shimmer warning
            if (metrics.Shimmer > ShimmerWarningThreshold)
                return true;
            
            // Low HNR
            if (metrics.HNR > 0 && metrics.HNR < HnrWarningThreshold)
                return true;
            
            // Pitch press
            if (metrics.IsPitchPress)
                return true;
            
            // Elevated strain
            if (metrics.StrainLevel > 0.5)
                return true;
            
            // Consecutive warning cycles
            if (metrics.ConsecutiveStrainCycles >= 3)
                return true;
            
            return false;
        }

        private void DecayCounters()
        {
            // Gradually reset counters if no new warnings
            if (_consecutiveWarnings > 0)
            {
                var timeSinceWarning = DateTime.Now - _lastWarningTime;
                if (timeSinceWarning.TotalSeconds > 30)
                {
                    _consecutiveWarnings = 0;
                }
            }
            
            if (_consecutiveCritical > 0)
            {
                var timeSinceCritical = DateTime.Now - _lastCriticalTime;
                if (timeSinceCritical.TotalSeconds > 60)
                {
                    _consecutiveCritical = 0;
                }
            }
        }

        private StrainEventArgs CreateStrainEventArgs(VoiceMetrics metrics, StrainLevel level)
        {
            return new StrainEventArgs
            {
                Timestamp = DateTime.Now,
                StrainLevel = level,
                Jitter = metrics.Jitter,
                Shimmer = metrics.Shimmer,
                HNR = metrics.HNR,
                Pitch = metrics.Pitch,
                StrainValue = metrics.StrainLevel,
                ConsecutiveWarnings = _consecutiveWarnings,
                ConsecutiveCritical = _consecutiveCritical,
                IsPitchPress = metrics.IsPitchPress,
                HasAmplitudeSpike = metrics.HasAmplitudeSpike
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            WarningDetected = null;
            CriticalDetected = null;
            PitchTemporarilyLowered = null;
            HealthRestRecommended = null;
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    #region Supporting Types

    public enum StrainAction
    {
        Continue,
        ContinueWithWarning,
        LowerPitchTemporarily,
        PauseAndRest
    }

    public enum StrainLevel
    {
        Normal,
        Mild,
        Warning,
        Critical
    }

    public class StrainSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double Jitter { get; set; }
        public double Shimmer { get; set; }
        public double HNR { get; set; }
        public double Intensity { get; set; }
        public double Pitch { get; set; }
        public double StrainValue { get; set; }
        public StrainLevel Level { get; set; }
    }

    public class StrainStatistics
    {
        public int TotalSamples { get; set; }
        public int WarningCount { get; set; }
        public int CriticalCount { get; set; }
        public double AverageJitter { get; set; }
        public double AverageShimmer { get; set; }
        public double AverageHnr { get; set; }
        public double MaxStrainLevel { get; set; }
    }

    public class StrainEventArgs : EventArgs
    {
        public DateTime Timestamp { get; set; }
        public StrainLevel StrainLevel { get; set; }
        public double Jitter { get; set; }
        public double Shimmer { get; set; }
        public double HNR { get; set; }
        public double Pitch { get; set; }
        public double StrainValue { get; set; }
        public int ConsecutiveWarnings { get; set; }
        public int ConsecutiveCritical { get; set; }
        public bool IsPitchPress { get; set; }
        public bool HasAmplitudeSpike { get; set; }
        public double? RecommendedPitchOffset { get; set; }
    }

    #endregion
}
