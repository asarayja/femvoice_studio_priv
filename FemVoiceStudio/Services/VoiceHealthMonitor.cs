using System;
using System.Collections.Generic;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Voice Health Monitor - kontinuerlig overvåking av stemmehelse under treningsøkter.
    /// Implementerer proaktiv beskyttelse basert på kliniske retningslinjer for stemmeterapi.
    /// 
    /// Funksjonalitet:
    /// - Strain-deteksjon basert på jitter, shimmer, og HNR
    /// - Proaktive advarsler ved forhøyede verdier
    /// - Lockout-mekanisme ved kritisk strain
    /// - Automatisk målsone-justering ved gjentatt strain
    /// </summary>
    public class VoiceHealthMonitor
    {
        #region Configuration
        
        private readonly VoiceHealthConfig _config;
        
        /// <summary>
        /// Konfigurasjon for stemmehelse-overvåking
        /// </summary>
        public class VoiceHealthConfig
        {
            // Jitter threshold (%)
            public double WarningJitterThreshold { get; set; } = 1.5;
            public double CriticalJitterThreshold { get; set; } = 2.5;
            
            // Shimmer threshold (%)
            public double WarningShimmerThreshold { get; set; } = 3.5;
            public double CriticalShimmerThreshold { get; set; } = 5.0;
            
            // HNR threshold (dB)
            public double WarningHNRThreshold { get; set; } = 18.0;
            public double CriticalHNRThreshold { get; set; } = 15.0;
            
            // Strain counter thresholds
            public int WarningFramesBeforeAlert { get; set; } = 30;
            public int CriticalFramesBeforeLockout { get; set; } = 100;
            
            // Lockout duration
            public int LockoutDurationMinutes { get; set; } = 15;
            
            // Recovery rate (frames to decrement counter when healthy)
            public int RecoveryRate { get; set; } = 2;
            
            // Target zone adjustment
            public double TargetZoneReductionPercent { get; set; } = 0.05; // 5% reduction
            
            public static VoiceHealthConfig Default => new();
        }
        
        #endregion
        
        #region State
        
        private int _consecutiveStrainFrames;
        private int _consecutiveWarningFrames;
        private DateTime? _lastLockoutTime;
        private bool _isLockedOut;
        
        private int _totalStrainIncidents;
        private int _totalWarningIncidents;
        
        private double _currentTargetZoneReduction = 0;
        
        // History of health incidents
        private readonly List<HealthIncident> _incidentHistory = new();
        
        #endregion
        
        #region Events
        
        public event EventHandler<HealthAlertEventArgs>? HealthWarning;
        public event EventHandler<HealthAlertEventArgs>? HealthCritical;
        public event EventHandler<LockoutEventArgs>? LockoutTriggered;
        public event EventHandler? LockoutEnded;
        public event EventHandler<HealthAlertEventArgs>? HealthRecovered;
        
        #endregion
        
        #region Properties
        
        public bool IsLockedOut => _isLockedOut;
        
        public DateTime? LockoutEndTime => _isLockedOut && _lastLockoutTime.HasValue
            ? _lastLockoutTime.Value.AddMinutes(_config.LockoutDurationMinutes)
            : null;
        
        public int ConsecutiveStrainFrames => _consecutiveStrainFrames;
        
        public int TotalStrainIncidents => _totalStrainIncidents;
        
        public double CurrentTargetZoneReduction => _currentTargetZoneReduction;
        
        public HealthIndicator CurrentHealthStatus => GetCurrentHealthStatus();
        
        #endregion
        
        public VoiceHealthMonitor() : this(VoiceHealthConfig.Default) { }
        
        public VoiceHealthMonitor(VoiceHealthConfig config)
        {
            _config = config;
        }
        
        #region Public Methods
        
        /// <summary>
        /// Analyzer en stemme-prøve og oppdater helsestatus
        /// </summary>
        public HealthAnalysisResult Analyze(VoiceMetrics metrics)
        {
            var result = new HealthAnalysisResult
            {
                Timestamp = DateTime.Now,
                JitterValue = metrics.Jitter,
                ShimmerValue = metrics.Shimmer,
                HNRValue = metrics.HNR,
                StrainLevel = metrics.StrainLevel
            };
            
            // Check for critical strain indicators
            bool isCritical = IsCriticalStrain(metrics);
            bool isWarning = IsWarningStrain(metrics);
            
            if (isCritical)
            {
                _consecutiveStrainFrames++;
                _consecutiveWarningFrames++;
                _totalStrainIncidents++;
                
                result.Status = Models.HealthIndicator.Critical;
                result.Message = LocalizationService.Instance["VoiceHealthMonitor_CriticalStrain"];
                result.Recommendation = LocalizationService.Instance["VoiceHealthMonitor_StopAndRest"];
                
                // Record incident
                RecordIncident(IncidentType.Critical, metrics);
                
                // Check for lockout
                if (_consecutiveStrainFrames >= _config.CriticalFramesBeforeLockout)
                {
                    TriggerLockout();
                    result.LockoutTriggered = true;
                }
                else
                {
                    HealthCritical?.Invoke(this, new HealthAlertEventArgs
                    {
                        Timestamp = DateTime.Now,
                        AlertType = IncidentType.Critical,
                        Message = result.Message,
                        Recommendation = result.Recommendation,
                        Jitter = metrics.Jitter,
                        Shimmer = metrics.Shimmer,
                        HNR = metrics.HNR
                    });
                }
            }
            else if (isWarning)
            {
                _consecutiveWarningFrames++;
                _consecutiveStrainFrames = Math.Max(0, _consecutiveStrainFrames - 1);
                _totalWarningIncidents++;
                
                result.Status = HealthIndicator.Warning;
                result.Message = LocalizationService.Instance["VoiceHealthMonitor_ElevatedStrain"];
                result.Recommendation = LocalizationService.Instance["VoiceHealthMonitor_CautionRest"];
                
                // Record incident
                RecordIncident(IncidentType.Warning, metrics);
                
                // Warning alert
                if (_consecutiveWarningFrames >= _config.WarningFramesBeforeAlert)
                {
                    HealthWarning?.Invoke(this, new HealthAlertEventArgs
                    {
                        Timestamp = DateTime.Now,
                        AlertType = IncidentType.Warning,
                        Message = result.Message,
                        Recommendation = result.Recommendation,
                        Jitter = metrics.Jitter,
                        Shimmer = metrics.Shimmer,
                        HNR = metrics.HNR
                    });
                }
                
                // Apply target zone reduction after repeated warnings
                if (_totalWarningIncidents >= 5 && _currentTargetZoneReduction < _config.TargetZoneReductionPercent * 3)
                {
                    _currentTargetZoneReduction += _config.TargetZoneReductionPercent;
                    result.AppliedTargetZoneReduction = _currentTargetZoneReduction;
                }
            }
            else
            {
                // Recovery - decrement counters
                _consecutiveStrainFrames = Math.Max(0, _consecutiveStrainFrames - _config.RecoveryRate);
                _consecutiveWarningFrames = Math.Max(0, _consecutiveWarningFrames - _config.RecoveryRate);
                
                result.Status = HealthIndicator.Safe;
                result.Message = LocalizationService.Instance["VoiceHealthMonitor_HealthOk"];
                result.Recommendation = LocalizationService.Instance["VoiceHealthMonitor_ContinueTraining"];
                
                // Notify recovery if previously was critical
                if (_consecutiveStrainFrames == 0 && _consecutiveWarningFrames == 0 && 
                    (_totalStrainIncidents > 0 || _totalWarningIncidents > 0))
                {
                    HealthRecovered?.Invoke(this, new HealthAlertEventArgs
                    {
                        Timestamp = DateTime.Now,
                        AlertType = IncidentType.Recovery,
                        Message = LocalizationService.Instance["VoiceHealthMonitor_HealthRecovered"],
                        Recommendation = LocalizationService.Instance["VoiceHealthMonitor_ContinueTraining"]
                    });
                    
                    // Reset incident counters after recovery
                    _totalStrainIncidents = 0;
                    _totalWarningIncidents = 0;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Sjekk om brukeren er låst ut grunnet stemmehelse
        /// </summary>
        public bool CheckLockoutStatus()
        {
            if (!_isLockedOut || !_lastLockoutTime.HasValue)
                return _isLockedOut;
            
            var elapsed = DateTime.Now - _lastLockoutTime.Value;
            if (elapsed.TotalMinutes >= _config.LockoutDurationMinutes)
            {
                EndLockout();
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Tving slutt på lockout (for administratorer eller etter hvile)
        /// </summary>
        public void ForceEndLockout()
        {
            EndLockout();
        }
        
        /// <summary>
        /// Nullstill monitoren for ny økt
        /// </summary>
        public void Reset()
        {
            _consecutiveStrainFrames = 0;
            _consecutiveWarningFrames = 0;
            _currentTargetZoneReduction = 0;
            _totalStrainIncidents = 0;
            _totalWarningIncidents = 0;
            _incidentHistory.Clear();
        }
        
        /// <summary>
        /// Hent helse-rapport for en periode
        /// </summary>
        public HealthReport GetHealthReport(TimeSpan period)
        {
            var cutoff = DateTime.Now - period;
            var recentIncidents = _incidentHistory.Where(i => i.Timestamp >= cutoff).ToList();
            
            return new HealthReport
            {
                PeriodStart = cutoff,
                PeriodEnd = DateTime.Now,
                TotalIncidents = recentIncidents.Count,
                CriticalIncidents = recentIncidents.Count(i => i.Type == IncidentType.Critical),
                WarningIncidents = recentIncidents.Count(i => i.Type == IncidentType.Warning),
                AverageJitter = recentIncidents.Any() ? recentIncidents.Average(i => i.Jitter) : 0,
                AverageShimmer = recentIncidents.Any() ? recentIncidents.Average(i => i.Shimmer) : 0,
                AverageHNR = recentIncidents.Any() ? recentIncidents.Average(i => i.HNR) : 0,
                CurrentLockoutStatus = _isLockedOut,
                LockoutEndTime = LockoutEndTime,
                CurrentTargetZoneReduction = _currentTargetZoneReduction
            };
        }
        
        /// <summary>
        /// Gets recent strain incidents for external tracking (e.g., periodization).
        /// Returns incidents from the last 7 days with their severity levels.
        /// </summary>
        /// <param name="days">Number of days to look back</param>
        /// <returns>List of recent strain incidents</returns>
        public List<StrainIncidentSummary> GetRecentStrainIncidents(int days = 7)
        {
            var cutoff = DateTime.Now.AddDays(-days);
            return _incidentHistory
                .Where(i => i.Timestamp >= cutoff)
                .Select(i => new StrainIncidentSummary
                {
                    Timestamp = i.Timestamp,
                    SeverityLevel = i.Type switch
                    {
                        IncidentType.Critical => 80,
                        IncidentType.Warning => 50,
                        _ => 20
                    },
                    IncidentType = i.Type,
                    Jitter = i.Jitter,
                    Shimmer = i.Shimmer,
                    HNR = i.HNR
                })
                .ToList();
        }
        
        #endregion
        
        #region Private Methods
        
        private bool IsCriticalStrain(VoiceMetrics metrics)
        {
            return metrics.Jitter > _config.CriticalJitterThreshold ||
                   metrics.Shimmer > _config.CriticalShimmerThreshold ||
                   metrics.HNR < _config.CriticalHNRThreshold ||
                   metrics.StrainLevel > 0.75;
        }
        
        private bool IsWarningStrain(VoiceMetrics metrics)
        {
            return metrics.Jitter > _config.WarningJitterThreshold ||
                   metrics.Shimmer > _config.WarningShimmerThreshold ||
                   metrics.HNR < _config.WarningHNRThreshold ||
                   metrics.StrainLevel > 0.5;
        }
        
        private HealthIndicator GetCurrentHealthStatus()
        {
            if (_isLockedOut || _consecutiveStrainFrames >= _config.CriticalFramesBeforeLockout)
                return HealthIndicator.Critical;
            
            if (_consecutiveStrainFrames >= _config.WarningFramesBeforeAlert ||
                _consecutiveWarningFrames >= _config.WarningFramesBeforeAlert)
                return HealthIndicator.Warning;
            
            return HealthIndicator.Safe;
        }
        
        private void TriggerLockout()
        {
            if (_isLockedOut) return;
            
            _isLockedOut = true;
            _lastLockoutTime = DateTime.Now;
            
            LockoutTriggered?.Invoke(this, new LockoutEventArgs
            {
                LockoutStartTime = _lastLockoutTime.Value,
                LockoutDuration = TimeSpan.FromMinutes(_config.LockoutDurationMinutes),
                LockoutEndTime = _lastLockoutTime.Value.AddMinutes(_config.LockoutDurationMinutes),
                Reason = LocalizationService.Instance.GetFormattedString("VoiceHealthMonitor_LockoutReasonFormat", _consecutiveStrainFrames),
                RecommendedAction = LocalizationService.Instance["VoiceHealthMonitor_Rest15Minutes"]
            });
        }
        
        private void EndLockout()
        {
            if (!_isLockedOut) return;
            
            _isLockedOut = false;
            
            // Reduce target zone reduction on lockout recovery
            _currentTargetZoneReduction = Math.Max(0, _currentTargetZoneReduction - _config.TargetZoneReductionPercent);
            
            LockoutEnded?.Invoke(this, EventArgs.Empty);
        }
        
        private void RecordIncident(IncidentType type, VoiceMetrics metrics)
        {
            _incidentHistory.Add(new HealthIncident
            {
                Timestamp = DateTime.Now,
                Type = type,
                Jitter = metrics.Jitter,
                Shimmer = metrics.Shimmer,
                HNR = metrics.HNR,
                StrainLevel = metrics.StrainLevel
            });
            
            // Keep only recent incidents
            while (_incidentHistory.Count > 1000)
            {
                _incidentHistory.RemoveAt(0);
            }
        }
        
        #endregion
    }
    
    #region Event Args Classes
    
    public class HealthAlertEventArgs : EventArgs
    {
        public DateTime Timestamp { get; set; }
        public IncidentType AlertType { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public double Jitter { get; set; }
        public double Shimmer { get; set; }
        public double HNR { get; set; }
    }
    
    public class LockoutEventArgs : EventArgs
    {
        public DateTime LockoutStartTime { get; set; }
        public TimeSpan LockoutDuration { get; set; }
        public DateTime LockoutEndTime { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
    }
    
    #endregion
    
    #region Result Classes
    
    public class HealthAnalysisResult
    {
        public DateTime Timestamp { get; set; }
        public HealthIndicator Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public double JitterValue { get; set; }
        public double ShimmerValue { get; set; }
        public double HNRValue { get; set; }
        public double StrainLevel { get; set; }
        public bool LockoutTriggered { get; set; }
        public double? AppliedTargetZoneReduction { get; set; }
    }
    
    public class HealthReport
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalIncidents { get; set; }
        public int CriticalIncidents { get; set; }
        public int WarningIncidents { get; set; }
        public double AverageJitter { get; set; }
        public double AverageShimmer { get; set; }
        public double AverageHNR { get; set; }
        public bool CurrentLockoutStatus { get; set; }
        public DateTime? LockoutEndTime { get; set; }
        public double CurrentTargetZoneReduction { get; set; }
    }
    
    public enum IncidentType
    {
        Info,
        Warning,
        Critical,
        Recovery
    }
    
    public class HealthIncident
    {
        public DateTime Timestamp { get; set; }
        public IncidentType Type { get; set; }
        public double Jitter { get; set; }
        public double Shimmer { get; set; }
        public double HNR { get; set; }
        public double StrainLevel { get; set; }
    }
    
    /// <summary>
    /// Summary of a strain incident for external tracking.
    /// </summary>
    public class StrainIncidentSummary
    {
        public DateTime Timestamp { get; set; }
        public int SeverityLevel { get; set; }
        public IncidentType IncidentType { get; set; }
        public double Jitter { get; set; }
        public double Shimmer { get; set; }
        public double HNR { get; set; }
    }
    
    #endregion
}
