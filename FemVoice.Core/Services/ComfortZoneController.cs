using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Dynamic comfort zone controller that manages safe pitch training boundaries.
    /// Adapts to user performance, health status, and stability metrics.
    /// </summary>
    public sealed class ComfortZoneController : IDisposable
    {
        #region Constants

        /// <summary>Default minimum pitch in Hz for new users.</summary>
        public const double DefaultMinPitch = 165;

        /// <summary>Default maximum pitch in Hz for new users.</summary>
        public const double DefaultMaxPitch = 255;

        /// <summary>Default zone width in Hz.</summary>
        public const double DefaultZoneWidth = 90;

        /// <summary>Minimum consecutive stable days required for zone expansion.</summary>
        public const int RequiredStableDaysForExpansion = 7;

        /// <summary>Maximum zone expansion rate per week (5%).</summary>
        public const double MaxWeeklyExpansionRate = 0.05;

        /// <summary>Safety lock duration in days after strain incident.</summary>
        public const int SafetyLockDurationDays = 3;

        /// <summary>Health score threshold below which zone is contracted.</summary>
        public const double HealthScoreContractionThreshold = 70;

        /// <summary>Stability score threshold below which expansion is frozen.</summary>
        public const double StabilityThresholdForExpansion = 60;

        /// <summary>Default maximum zone width in Hz.</summary>
        public const double DefaultMaxZoneWidth = 150;

        /// <summary>Default minimum zone width in Hz.</summary>
        public const double DefaultMinZoneWidth = 40;

        /// <summary>Maximum pitch limit in Hz (safety cap).</summary>
        public const double MaxPitchLimit = 350;

        /// <summary>Minimum pitch limit in Hz (safety cap).</summary>
        public const double MinPitchLimit = 100;

        #endregion

        #region Private Fields

        private readonly IUserRepository _userRepository;
        private readonly IScoreRepository _scoreRepository;
        private readonly SynchronizationContext? _syncContext;
        private readonly ReaderWriterLockSlim _stateLock = new();

        private int _currentUserId;
        private ComfortZoneState _currentState = ComfortZoneState.Empty;
        private ZoneConfiguration _configuration;
        private bool _isDisposed;

        #endregion

        #region Events

        /// <summary>Raised when the comfort zone is updated.</summary>
        public event Action<ComfortZoneState>? ZoneUpdated;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new ComfortZoneController with dependency injection.
        /// </summary>
        public ComfortZoneController(
            IUserRepository userRepository,
            IScoreRepository scoreRepository,
            ZoneConfiguration? configuration = null)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _scoreRepository = scoreRepository ?? throw new ArgumentNullException(nameof(scoreRepository));
            _configuration = configuration ?? new ZoneConfiguration();
            _syncContext = SynchronizationContext.Current;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the controller for a specific user and loads their zone state.
        /// </summary>
        public async Task InitializeAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

            _stateLock.EnterWriteLock();
            try
            {
                _currentUserId = userId;

                var userSettings = await _userRepository.GetComfortZoneSettingsAsync(userId);
                var healthData = await _userRepository.GetUserHealthDataAsync(userId);
                var toleranceData = await _userRepository.GetToleranceDataAsync(userId);
                var recentIncidents = await _userRepository.GetRecentStrainIncidentsAsync(userId, SafetyLockDurationDays);
                var recentScores = await _scoreRepository.GetRecentScoresAsync(userId, RequiredStableDaysForExpansion + 7, cancellationToken);

                _currentState = await CalculateZoneStateAsync(userSettings, healthData, toleranceData, recentIncidents, recentScores, cancellationToken);
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the comfort zone based on current session metrics.
        /// </summary>
        public async Task<ComfortZoneState> UpdateZoneAsync(
            double resonanceScore,
            double pitchScore,
            double stabilityScore,
            double adaptiveScore,
            double healthScore,
            CancellationToken cancellationToken = default)
        {
            _stateLock.EnterWriteLock();
            try
            {
                if (_currentUserId == 0)
                    throw new InvalidOperationException("Controller not initialized. Call InitializeAsync first.");

                var recentScores = await _scoreRepository.GetRecentScoresAsync(_currentUserId, 14, cancellationToken);
                var recentIncidents = await _userRepository.GetRecentStrainIncidentsAsync(_currentUserId, SafetyLockDurationDays);

                var zoneChanges = await DetermineZoneChangesAsync(resonanceScore, stabilityScore, adaptiveScore, healthScore, recentScores, recentIncidents);
                _currentState = ApplyZoneChanges(_currentState, zoneChanges);
                await PersistZoneStateAsync(cancellationToken);
                RaiseZoneUpdatedEvent(_currentState);

                return _currentState;
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Records a strain incident and triggers safety lock if needed.
        /// </summary>
        public async Task RecordStrainIncidentAsync(
            double strainLevel,
            string strainType,
            double pitchAtIncident,
            CancellationToken cancellationToken = default)
        {
            if (_currentUserId == 0)
                throw new InvalidOperationException("Controller not initialized. Call InitializeAsync first.");

            var incident = new StrainIncident
            {
                UserId = _currentUserId,
                Timestamp = DateTime.Now,
                StrainLevel = strainLevel,
                StrainType = strainType,
                PitchAtIncident = pitchAtIncident
            };

            await _userRepository.RecordStrainIncidentAsync(_currentUserId, incident);

            if (strainLevel >= 70)
            {
                _stateLock.EnterWriteLock();
                try
                {
                    _currentState = new ComfortZoneState
                    {
                        MinPitch = _currentState.MinPitch,
                        MaxPitch = _currentState.MaxPitch,
                        ZoneWidth = _currentState.ZoneWidth,
                        OptimalPitch = _currentState.OptimalPitch,
                        IsExpansionAllowed = false,
                        IsSafetyLocked = true,
                        Reason = string.Format("Strain incident detected ({0}, level {1})", strainType, strainLevel),
                        Timestamp = DateTime.Now,
                        UserId = _currentUserId,
                        SafetyLockDaysRemaining = SafetyLockDurationDays,
                        ConsecutiveStableDays = _currentState.ConsecutiveStableDays
                    };

                    await PersistZoneStateAsync(cancellationToken);
                    RaiseZoneUpdatedEvent(_currentState);
                }
                finally
                {
                    _stateLock.ExitWriteLock();
                }
            }
        }

        /// <summary>Gets the current zone state.</summary>
        public ComfortZoneState GetCurrentState()
        {
            _stateLock.EnterReadLock();
            try { return _currentState; }
            finally { _stateLock.ExitReadLock(); }
        }

        /// <summary>Checks if zone expansion is currently allowed.</summary>
        public bool IsExpansionAllowed()
        {
            _stateLock.EnterReadLock();
            try { return _currentState.IsExpansionAllowed && !_currentState.IsSafetyLocked; }
            finally { _stateLock.ExitReadLock(); }
        }

        /// <summary>Gets the configuration.</summary>
        public ZoneConfiguration GetConfiguration() => _configuration;

        #endregion
        #region Private Calculation Methods

        private Task<ComfortZoneState> CalculateZoneStateAsync(
            UserComfortZoneSettings? userSettings,
            UserHealthData? healthData,
            UserToleranceData? toleranceData,
            IReadOnlyList<StrainIncident> recentIncidents,
            IReadOnlyList<FemVoiceScoreSnapshot> recentScores,
            CancellationToken cancellationToken)
        {
            var activeIncident = recentIncidents.FirstOrDefault(i => i.Timestamp.AddDays(SafetyLockDurationDays) > DateTime.Now);

            if (activeIncident != null)
            {
                var daysRemaining = (int)Math.Ceiling((activeIncident.Timestamp.AddDays(SafetyLockDurationDays) - DateTime.Now).TotalDays);
                return Task.FromResult(new ComfortZoneState
                {
                    MinPitch = userSettings?.PreferredMinPitch ?? DefaultMinPitch,
                    MaxPitch = userSettings?.PreferredMaxPitch ?? DefaultMaxPitch,
                    ZoneWidth = userSettings?.ZoneWidth ?? DefaultZoneWidth,
                    OptimalPitch = ((userSettings?.PreferredMinPitch ?? DefaultMinPitch) + (userSettings?.PreferredMaxPitch ?? DefaultMaxPitch)) / 2,
                    IsExpansionAllowed = false,
                    IsSafetyLocked = true,
                    Reason = string.Format("Active safety lock - {0} days remaining", daysRemaining),
                    Timestamp = DateTime.Now,
                    UserId = _currentUserId,
                    SafetyLockDaysRemaining = daysRemaining,
                    ConsecutiveStableDays = 0
                });
            }

            if (healthData != null && healthData.HealthScore < HealthScoreContractionThreshold)
            {
                return Task.FromResult(ContractZoneForHealth(userSettings, healthData.HealthScore, toleranceData));
            }

            var consecutiveStableDays = CalculateConsecutiveStableDays(recentScores);
            var isStable = CalculateStabilityStatus(recentScores);
            var individualizedWidth = toleranceData?.AverageToleratedWidth ?? DefaultZoneWidth;
            var hasRapidIncrease = CheckForRapidScoreIncrease(recentScores);

            var minPitch = userSettings?.PreferredMinPitch ?? DefaultMinPitch;
            var maxPitch = userSettings?.PreferredMaxPitch ?? DefaultMaxPitch;
            var zoneWidth = Math.Min(individualizedWidth, Math.Max(DefaultMinZoneWidth, userSettings?.ZoneWidth ?? DefaultZoneWidth));

            return Task.FromResult(new ComfortZoneState
            {
                MinPitch = minPitch,
                MaxPitch = maxPitch,
                ZoneWidth = zoneWidth,
                OptimalPitch = (minPitch + maxPitch) / 2,
                IsExpansionAllowed = isStable && consecutiveStableDays >= RequiredStableDaysForExpansion && !hasRapidIncrease,
                IsSafetyLocked = false,
                Reason = DetermineZoneReason(isStable, consecutiveStableDays, hasRapidIncrease),
                Timestamp = DateTime.Now,
                UserId = _currentUserId,
                SafetyLockDaysRemaining = 0,
                ConsecutiveStableDays = consecutiveStableDays
            });
        }

        private async Task<ZoneChangeRequest> DetermineZoneChangesAsync(
            double resonanceScore,
            double stabilityScore,
            double adaptiveScore,
            double healthScore,
            IReadOnlyList<FemVoiceScoreSnapshot> recentScores,
            IReadOnlyList<StrainIncident> recentIncidents)
        {
            var changes = new ZoneChangeRequest();

            if (healthScore < HealthScoreContractionThreshold)
            {
                changes.Action = ZoneChangeAction.Contract;
                changes.Reason = string.Format("Health score ({0:F1}) below threshold ({1})", healthScore, HealthScoreContractionThreshold);
                return changes;
            }

            var hasRecentStrain = recentIncidents.Any(i => i.Timestamp.AddDays(SafetyLockDurationDays) > DateTime.Now);
            if (hasRecentStrain)
            {
                changes.Action = ZoneChangeAction.Lock;
                changes.Reason = "Recent strain incident - safety lock active";
                return changes;
            }

            var isStable = stabilityScore >= StabilityThresholdForExpansion;
            if (!isStable)
            {
                changes.Action = ZoneChangeAction.Freeze;
                changes.Reason = string.Format("Stability score ({0:F1}) below threshold", stabilityScore);
                return changes;
            }

            var hasRapidIncrease = recentScores.Count >= 3 && CheckForRapidScoreIncrease(recentScores);
            if (hasRapidIncrease)
            {
                changes.Action = ZoneChangeAction.LimitedExpansion;
                changes.Reason = "Rapid score increase detected - limited expansion";
                return changes;
            }

            var consecutiveStableDays = CalculateConsecutiveStableDays(recentScores);
            if (consecutiveStableDays >= RequiredStableDaysForExpansion)
            {
                var currentWidth = _currentState.ZoneWidth;
                var maxAllowedExpansion = currentWidth * MaxWeeklyExpansionRate;
                var toleranceData = await _userRepository.GetToleranceDataAsync(_currentUserId);
                var maxWidth = Math.Min(toleranceData?.MaxToleratedWidth ?? DefaultMaxZoneWidth, DefaultMaxZoneWidth);
                var actualExpansion = Math.Min(maxAllowedExpansion, maxWidth - currentWidth);

                changes.Action = ZoneChangeAction.Expand;
                changes.ExpansionAmount = actualExpansion;
                changes.ConsecutiveStableDays = consecutiveStableDays;
                changes.Reason = string.Format("Stable for {0} days - zone expanded by {1:F1} Hz", consecutiveStableDays, actualExpansion);
                return changes;
            }

            changes.Action = ZoneChangeAction.Maintain;
            changes.ConsecutiveStableDays = consecutiveStableDays;
            changes.Reason = string.Format("Stable for {0} days - awaiting expansion criteria", consecutiveStableDays);
            return changes;
        }

        private ComfortZoneState ApplyZoneChanges(ComfortZoneState state, ZoneChangeRequest changes)
        {
            switch (changes.Action)
            {
                case ZoneChangeAction.Expand: return ExpandZone(state, changes);
                case ZoneChangeAction.LimitedExpansion: return LimitedExpandZone(state, changes);
                case ZoneChangeAction.Contract: return ContractZone(state);
                case ZoneChangeAction.Freeze: return FreezeExpansion(state);
                case ZoneChangeAction.Lock: return LockZone(state, changes.Reason);
                case ZoneChangeAction.Maintain: return MaintainZone(state, changes.ConsecutiveStableDays);
                default: return state;
            }
        }

        private ComfortZoneState ExpandZone(ComfortZoneState state, ZoneChangeRequest changes)
        {
            var newWidth = state.ZoneWidth + changes.ExpansionAmount;
            var newMinPitch = Math.Max(MinPitchLimit, state.MinPitch - (changes.ExpansionAmount / 2));
            var newMaxPitch = Math.Min(MaxPitchLimit, state.MaxPitch + (changes.ExpansionAmount / 2));
            newWidth = newMaxPitch - newMinPitch;

            return new ComfortZoneState
            {
                MinPitch = newMinPitch,
                MaxPitch = newMaxPitch,
                ZoneWidth = newWidth,
                OptimalPitch = (newMinPitch + newMaxPitch) / 2,
                IsExpansionAllowed = true,
                IsSafetyLocked = false,
                Reason = changes.Reason,
                Timestamp = DateTime.Now,
                UserId = _currentUserId,
                SafetyLockDaysRemaining = 0,
                ConsecutiveStableDays = changes.ConsecutiveStableDays
            };
        }

        private ComfortZoneState LimitedExpandZone(ComfortZoneState state, ZoneChangeRequest changes)
        {
            var limitedExpansion = state.ZoneWidth * (MaxWeeklyExpansionRate / 2);
            var newWidth = state.ZoneWidth + limitedExpansion;
            var newMinPitch = Math.Max(MinPitchLimit, state.MinPitch - (limitedExpansion / 2));
            var newMaxPitch = Math.Min(MaxPitchLimit, state.MaxPitch + (limitedExpansion / 2));
            newWidth = newMaxPitch - newMinPitch;

            return new ComfortZoneState
            {
                MinPitch = newMinPitch,
                MaxPitch = newMaxPitch,
                ZoneWidth = newWidth,
                OptimalPitch = (newMinPitch + newMaxPitch) / 2,
                IsExpansionAllowed = false,
                IsSafetyLocked = false,
                Reason = changes.Reason,
                Timestamp = DateTime.Now,
                UserId = _currentUserId,
                SafetyLockDaysRemaining = 0,
                ConsecutiveStableDays = state.ConsecutiveStableDays
            };
        }

        private ComfortZoneState ContractZone(ComfortZoneState state)
        {
            var contractionAmount = state.ZoneWidth * 0.2;
            var newWidth = Math.Max(DefaultMinZoneWidth, state.ZoneWidth - contractionAmount);
            var centerPitch = (state.MinPitch + state.MaxPitch) / 2;
            var newMinPitch = Math.Max(MinPitchLimit, centerPitch - (newWidth / 2));
            var newMaxPitch = Math.Min(MaxPitchLimit, centerPitch + (newWidth / 2));

            return new ComfortZoneState
            {
                MinPitch = newMinPitch,
                MaxPitch = newMaxPitch,
                ZoneWidth = newMaxPitch - newMinPitch,
                OptimalPitch = centerPitch,
                IsExpansionAllowed = false,
                IsSafetyLocked = false,
                Reason = "Zone contracted due to health concerns",
                Timestamp = DateTime.Now,
                UserId = _currentUserId,
                SafetyLockDaysRemaining = 0,
                ConsecutiveStableDays = 0
            };
        }

        private ComfortZoneState FreezeExpansion(ComfortZoneState state)
        {
            return new ComfortZoneState
            {
                MinPitch = state.MinPitch,
                MaxPitch = state.MaxPitch,
                ZoneWidth = state.ZoneWidth,
                OptimalPitch = state.OptimalPitch,
                IsExpansionAllowed = false,
                IsSafetyLocked = state.IsSafetyLocked,
                Reason = "Expansion frozen - stability below threshold",
                Timestamp = DateTime.Now,
                UserId = _currentUserId,
                SafetyLockDaysRemaining = state.SafetyLockDaysRemaining,
                ConsecutiveStableDays = state.ConsecutiveStableDays
            };
        }

        private ComfortZoneState LockZone(ComfortZoneState state, string reason)
        {
            return new ComfortZoneState
            {
                MinPitch = state.MinPitch,
                MaxPitch = state.MaxPitch,
                ZoneWidth = state.ZoneWidth,
                OptimalPitch = state.OptimalPitch,
                IsExpansionAllowed = false,
                IsSafetyLocked = true,
                Reason = reason,
                Timestamp = DateTime.Now,
                UserId = _currentUserId,
                SafetyLockDaysRemaining = SafetyLockDurationDays,
                ConsecutiveStableDays = 0
            };
        }

        private ComfortZoneState MaintainZone(ComfortZoneState state, int consecutiveStableDays)
        {
            return new ComfortZoneState
            {
                MinPitch = state.MinPitch,
                MaxPitch = state.MaxPitch,
                ZoneWidth = state.ZoneWidth,
                OptimalPitch = state.OptimalPitch,
                IsExpansionAllowed = state.IsExpansionAllowed,
                IsSafetyLocked = state.IsSafetyLocked,
                Reason = string.Format("Maintaining zone - {0} stable days", consecutiveStableDays),
                Timestamp = DateTime.Now,
                UserId = _currentUserId,
                SafetyLockDaysRemaining = state.SafetyLockDaysRemaining > 0 ? state.SafetyLockDaysRemaining - 1 : 0,
                ConsecutiveStableDays = consecutiveStableDays
            };
        }

        private ComfortZoneState ContractZoneForHealth(UserComfortZoneSettings? settings, double healthScore, UserToleranceData? tolerance)
        {
            var minPitch = settings?.PreferredMinPitch ?? DefaultMinPitch;
            var maxPitch = settings?.PreferredMaxPitch ?? DefaultMaxPitch;
            var contraction = 0.25;
            var newWidth = (maxPitch - minPitch) * (1 - contraction);
            newWidth = Math.Max(DefaultMinZoneWidth, newWidth);
            var center = (minPitch + maxPitch) / 2;
            var newMin = Math.Max(MinPitchLimit, center - (newWidth / 2));
            var newMax = Math.Min(MaxPitchLimit, center + (newWidth / 2));

            return new ComfortZoneState
            {
                MinPitch = newMin,
                MaxPitch = newMax,
                ZoneWidth = newMax - newMin,
                OptimalPitch = center,
                IsExpansionAllowed = false,
                IsSafetyLocked = false,
                Reason = string.Format("Health score ({0:F1}) below threshold - zone contracted", healthScore),
                Timestamp = DateTime.Now,
                UserId = _currentUserId,
                SafetyLockDaysRemaining = 0,
                ConsecutiveStableDays = 0
            };
        }

        #endregion

        #region Helper Methods

        private int CalculateConsecutiveStableDays(IReadOnlyList<FemVoiceScoreSnapshot> recentScores)
        {
            if (recentScores == null || recentScores.Count == 0)
                return 0;

            var orderedScores = recentScores.OrderByDescending(s => s.Timestamp).ToList();
            int stableDays = 0;
            var lastDate = DateTime.MinValue;

            foreach (var score in orderedScores)
            {
                var scoreDate = score.Timestamp.Date;
                if (lastDate == DateTime.MinValue)
                {
                    lastDate = scoreDate;
                    stableDays = 1;
                }
                else if ((lastDate - scoreDate).TotalDays <= 1)
                {
                    if (score.StabilityScore >= StabilityThresholdForExpansion)
                        stableDays++;
                    lastDate = scoreDate;
                }
                else
                {
                    break;
                }
            }

            return stableDays;
        }

        private bool CalculateStabilityStatus(IReadOnlyList<FemVoiceScoreSnapshot> recentScores)
        {
            if (recentScores == null || recentScores.Count < 3)
                return false;

            var recent = recentScores.OrderByDescending(s => s.Timestamp).Take(7).ToList();
            var avgStability = recent.Average(s => s.StabilityScore);
            return avgStability >= StabilityThresholdForExpansion;
        }

        private bool CheckForRapidScoreIncrease(IReadOnlyList<FemVoiceScoreSnapshot> recentScores)
        {
            if (recentScores == null || recentScores.Count < 3)
                return false;

            var ordered = recentScores.OrderBy(s => s.Timestamp).ToList();
            var recentScoresList = ordered.TakeLast(5).ToList();

            if (recentScoresList.Count < 3)
                return false;

            var first = recentScoresList.First().AdaptiveScore;
            var last = recentScoresList.Last().AdaptiveScore;
            var increase = last - first;

            return increase >= 15;
        }

        private string DetermineZoneReason(bool isStable, int consecutiveDays, bool hasRapidIncrease)
        {
            if (!isStable)
                return "Stability below threshold";
            if (hasRapidIncrease)
                return "Rapid progress detected - limiting expansion";
            if (consecutiveDays >= RequiredStableDaysForExpansion)
                return string.Format("Ready for expansion - {0} stable days", consecutiveDays);
            return string.Format("Building stability - {0}/{1} days", consecutiveDays, RequiredStableDaysForExpansion);
        }

        private async Task PersistZoneStateAsync(CancellationToken cancellationToken)
        {
            var settings = new UserComfortZoneSettings
            {
                UserId = _currentUserId,
                PreferredMinPitch = _currentState.MinPitch,
                PreferredMaxPitch = _currentState.MaxPitch,
                ZoneWidth = _currentState.ZoneWidth,
                IndividualizedWidthMultiplier = 1.0,
                LastZoneUpdate = DateTime.Now
            };

            await _userRepository.SaveComfortZoneSettingsAsync(_currentUserId, settings);
        }

        private void RaiseZoneUpdatedEvent(ComfortZoneState state)
        {
            var handler = ZoneUpdated;
            if (handler == null)
                return;

            void Raise() => handler(state);

            if (_syncContext != null)
                _syncContext.Post(_ => Raise(), null);
            else
                Raise();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            ZoneUpdated = null;
            _stateLock.Dispose();
        }

        #endregion
    }
}
