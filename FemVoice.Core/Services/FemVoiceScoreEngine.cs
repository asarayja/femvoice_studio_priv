using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Adaptive composite scoring engine that calculates FemVoiceScore based on multiple voice metrics
    /// with personalized normalization against individual user baselines.
    /// </summary>
    /// <remarks>
    /// Score Composition:
    /// FemVoiceScore = 0.45 × ResonanceScore + 0.30 × PitchScore + 0.15 × StabilityScore + 0.10 × HealthModifier
    /// 
    /// Adaptive Intelligence:
    /// - Tracks 30-day rolling baseline per user
    /// - Applies exponential smoothing for trend calculation
    /// - Normalizes against personal trend (not absolute scale)
    /// - Detects plateaus (≥14 days stagnation)
    /// - Detects regression (>10% sustained drop from baseline)
    /// - Prevents score inflation during unstable sessions
    /// </remarks>
    public sealed class FemVoiceScoreEngine : IDisposable
    {
        #region Constants (Default values - can be overridden via configuration)

        /// <summary>
        /// Default weight for resonance component (45%).
        /// </summary>
        public const double DefaultResonanceWeight = 0.45;

        /// <summary>
        /// Default weight for pitch component (30%).
        /// </summary>
        public const double DefaultPitchWeight = 0.30;

        /// <summary>
        /// Default weight for stability component (15%).
        /// </summary>
        public const double DefaultStabilityWeight = 0.15;

        /// <summary>
        /// Default weight for health modifier (10%).
        /// </summary>
        public const double DefaultHealthWeight = 0.10;

        /// <summary>
        /// Default rolling baseline window in days.
        /// </summary>
        public const int DefaultBaselineRollingDays = 30;

        /// <summary>
        /// Threshold in days to detect plateau.
        /// </summary>
        public const int DefaultPlateauThresholdDays = 14;

        /// <summary>
        /// Threshold percentage for regression detection (10%).
        /// </summary>
        public const double DefaultRegressionThresholdPercent = 0.10;

        /// <summary>
        /// Exponential smoothing alpha value for trend calculation.
        /// </summary>
        public const double DefaultSmoothingAlpha = 0.3;

        /// <summary>
        /// Minimum number of data points required for baseline calculation.
        /// </summary>
        public const int MinimumDataPointsForBaseline = 3;

        /// <summary>
        /// Minimum score variance to consider as active training (not idle).
        /// </summary>
        public const double MinimumVarianceThreshold = 0.5;

        #endregion

        #region Private Fields

        private readonly IScoreRepository _scoreRepository;
        private readonly IUserRepository _userRepository;
        private readonly ScoringConfiguration _configuration;
        private readonly SynchronizationContext? _syncContext;
        private readonly ReaderWriterLockSlim _stateLock = new();
        
        private int _currentUserId;
        private UserScoreBaseline? _currentBaseline;
        private List<FemVoiceScoreSnapshot> _recentSnapshots = new();
        private bool _isDisposed;

        #endregion

        #region Events

        /// <summary>
        /// Raised when a new score snapshot is calculated.
        /// Thread-safe: uses synchronization context or raises directly.
        /// </summary>
        public event Action<FemVoiceScoreSnapshot>? ScoreUpdated;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new FemVoiceScoreEngine with dependency injection.
        /// </summary>
        /// <param name="scoreRepository">Repository for score persistence.</param>
        /// <param name="userRepository">Repository for user settings.</param>
        /// <param name="configuration">Optional configuration overrides. Uses defaults if null.</param>
        public FemVoiceScoreEngine(
            IScoreRepository scoreRepository,
            IUserRepository userRepository,
            ScoringConfiguration? configuration = null)
        {
            _scoreRepository = scoreRepository ?? throw new ArgumentNullException(nameof(scoreRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _configuration = configuration ?? new ScoringConfiguration();
            _syncContext = SynchronizationContext.Current;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the current user context and loads their baseline data.
        /// </summary>
        /// <param name="userId">The user ID to score for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SetUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

            _stateLock.EnterWriteLock();
            try
            {
                _currentUserId = userId;
                _currentBaseline = await _scoreRepository.GetBaselineAsync(userId, cancellationToken);
                
                if (_currentBaseline == null)
                {
                    _currentBaseline = CreateNewBaseline(userId);
                }

                var recentHistory = await _scoreRepository.GetRollingHistoryAsync(
                    userId, 
                    _configuration.BaselineRollingDays, 
                    cancellationToken);
                
                _recentSnapshots = recentHistory?.ToList() ?? new List<FemVoiceScoreSnapshot>();
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Calculates a new FemVoiceScoreSnapshot based on the provided metrics.
        /// </summary>
        /// <param name="resonanceScore">Current resonance score (0-100).</param>
        /// <param name="pitchScore">Current pitch score (0-100).</param>
        /// <param name="stabilityScore">Current stability score (0-100).</param>
        /// <param name="healthModifier">Current health modifier (0-100).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The calculated score snapshot.</returns>
        public async Task<FemVoiceScoreSnapshot> CalculateScoreAsync(
            double resonanceScore,
            double pitchScore,
            double stabilityScore,
            double healthModifier,
            CancellationToken cancellationToken = default)
        {
            // Check if disposed before acquiring lock
            if (_isDisposed)
                return new FemVoiceScoreSnapshot
                {
                    UserId = _currentUserId,
                    RawScore = resonanceScore * _configuration.ResonanceWeight + pitchScore * _configuration.PitchWeight + 
                               stabilityScore * _configuration.StabilityWeight + healthModifier * _configuration.HealthWeight,
                    AdaptiveScore = 0,
                    TrendSlope = 0,
                    PlateauDetected = false,
                    RegressionDetected = false,
                    Timestamp = DateTime.Now
                };

            _stateLock.EnterReadLock();
            try
            {
                if (_currentUserId == 0)
                    throw new InvalidOperationException("User context not set. Call SetUserAsync first.");

                var rawScore = CalculateRawScore(resonanceScore, pitchScore, stabilityScore, healthModifier);
                var (adaptiveScore, trendSlope) = await CalculateAdaptiveScoreAsync(rawScore, cancellationToken);
                var (plateauDetected, regressionDetected) = await DetectPatternsAsync(cancellationToken);

                var snapshot = new FemVoiceScoreSnapshot
                {
                    UserId = _currentUserId,
                    RawScore = rawScore,
                    AdaptiveScore = adaptiveScore,
                    TrendSlope = trendSlope,
                    PlateauDetected = plateauDetected,
                    RegressionDetected = regressionDetected,
                    Timestamp = DateTime.Now,
                    ResonanceScore = resonanceScore,
                    PitchScore = pitchScore,
                    StabilityScore = stabilityScore,
                    HealthModifier = healthModifier
                };

                await PersistSnapshotAsync(snapshot, cancellationToken);
                RaiseScoreUpdatedEvent(snapshot);

                return snapshot;
            }
            finally
            {
                _stateLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets the current baseline for the active user.
        /// </summary>
        /// <returns>The current user baseline or null if not initialized.</returns>
        public UserScoreBaseline? GetCurrentBaseline()
        {
            _stateLock.EnterReadLock();
            try
            {
                return _currentBaseline;
            }
            finally
            {
                _stateLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets the current configuration.
        /// </summary>
        public ScoringConfiguration GetConfiguration() => _configuration;

        #endregion

        #region Private Calculation Methods

        /// <summary>
        /// Calculates the raw weighted composite score.
        /// </summary>
        private double CalculateRawScore(
            double resonanceScore,
            double pitchScore,
            double stabilityScore,
            double healthModifier)
        {
            var weightedSum = 
                resonanceScore * _configuration.ResonanceWeight +
                pitchScore * _configuration.PitchWeight +
                stabilityScore * _configuration.StabilityWeight +
                healthModifier * _configuration.HealthWeight;

            return Math.Clamp(weightedSum, 0, 100);
        }

        /// <summary>
        /// Calculates adaptive score normalized against user's personal baseline trend.
        /// </summary>
        private async Task<(double adaptiveScore, double trendSlope)> CalculateAdaptiveScoreAsync(
            double rawScore,
            CancellationToken cancellationToken)
        {
            // Always increment data point count for tracking
            if (_currentBaseline != null)
            {
                _currentBaseline.DataPointCount++;
            }

            if (_currentBaseline == null || _currentBaseline.DataPointCount < MinimumDataPointsForBaseline)
            {
                // Not enough data - return raw score with neutral trend
                // Save baseline to persist the data point count
                if (_currentBaseline != null)
                {
                    _currentBaseline.LastUpdated = DateTime.Now;
                    await _scoreRepository.SaveBaselineAsync(_currentBaseline, cancellationToken);
                }
                return (rawScore, 0);
            }

            // Update baseline with exponential smoothing
            var smoothedScore = ExponentialSmoothing(
                rawScore, 
                _currentBaseline.ExponentialSmoothedScore, 
                _configuration.SmoothingAlpha);

            // Calculate trend slope using linear regression on recent scores
            var trendSlope = CalculateTrendSlope();

            // Normalize against baseline
            var baselineScore = _currentBaseline.BaselineScore;
            var normalizedScore = baselineScore > 0 
                ? (rawScore / baselineScore) * 50 + 50  // Normalize to ~50-100 range
                : rawScore;

            // Apply inflation prevention for unstable sessions
            var stabilityCheck = await CheckSessionStabilityAsync(cancellationToken);
            if (!stabilityCheck.IsStable)
            {
                normalizedScore = Math.Min(normalizedScore, stabilityCheck.MaxAllowedScore);
            }

            // Update baseline
            _currentBaseline.ExponentialSmoothedScore = smoothedScore;
            _currentBaseline.LastUpdated = DateTime.Now;

            // Check for regression
            var scoreDropPercent = (baselineScore - rawScore) / baselineScore;
            _currentBaseline.IsRegression = scoreDropPercent >= _configuration.RegressionThresholdPercent;

            await _scoreRepository.SaveBaselineAsync(_currentBaseline, cancellationToken);

            return (Math.Clamp(normalizedScore, 0, 100), trendSlope);
        }

        /// <summary>
        /// Applies exponential smoothing for trend calculation.
        /// </summary>
        private static double ExponentialSmoothing(double currentValue, double previousSmoothedValue, double alpha)
        {
            return alpha * currentValue + (1 - alpha) * previousSmoothedValue;
        }

        /// <summary>
        /// Calculates trend slope using linear regression on recent scores.
        /// </summary>
        private double CalculateTrendSlope()
        {
            if (_recentSnapshots.Count < 2)
                return 0;

            var recentScores = _recentSnapshots
                .OrderByDescending(s => s.Timestamp)
                .Take(Math.Min(7, _recentSnapshots.Count))
                .Reverse()
                .Select(s => s.RawScore)
                .ToList();

            if (recentScores.Count < 2)
                return 0;

            // Simple linear regression: y = mx + b
            var n = recentScores.Count;
            var sumX = 0.0;
            var sumY = recentScores.Sum();
            var sumXY = 0.0;
            var sumX2 = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumXY += i * recentScores[i];
                sumX2 += i * i;
            }

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 0.0001)
                return 0;

            var slope = (n * sumXY - sumX * sumY) / denominator;
            
            // Normalize to -1 to 1 range (approximate daily change)
            return Math.Clamp(slope / 10.0, -1, 1);
        }

        /// <summary>
        /// Detects plateau and regression patterns.
        /// </summary>
        private async Task<(bool plateauDetected, bool regressionDetected)> DetectPatternsAsync(
            CancellationToken cancellationToken)
        {
            if (_currentBaseline == null || _recentSnapshots.Count < MinimumDataPointsForBaseline)
                return (false, false);

            // Detect plateau: minimal variance over threshold period
            var plateauScores = _recentSnapshots
                .Where(s => s.Timestamp >= DateTime.Now.AddDays(-_configuration.PlateauThresholdDays))
                .Select(s => s.RawScore)
                .ToList();

            bool plateauDetected = false;
            if (plateauScores.Count >= _configuration.PlateauThresholdDays)
            {
                var variance = CalculateVariance(plateauScores);
                plateauDetected = variance < MinimumVarianceThreshold;
                _currentBaseline.IsPlateau = plateauDetected;
            }

            // Detect regression: sustained drop from baseline
            var recentAvg = _recentSnapshots
                .Where(s => s.Timestamp >= DateTime.Now.AddDays(-7))
                .Select(s => s.RawScore)
                .DefaultIfEmpty(0)
                .Average();

            var baselineScore = _currentBaseline.BaselineScore;
            var regressionDetected = baselineScore > 0 && 
                (baselineScore - recentAvg) / baselineScore >= _configuration.RegressionThresholdPercent;

            _currentBaseline.IsRegression = regressionDetected;
            
            // Update baseline
            if (_currentBaseline.DataPointCount >= MinimumDataPointsForBaseline)
            {
                _currentBaseline.BaselineScore = (_currentBaseline.BaselineScore * 0.7) + (recentAvg * 0.3);
            }
            else
            {
                _currentBaseline.BaselineScore = recentAvg;
            }

            await _scoreRepository.SaveBaselineAsync(_currentBaseline, cancellationToken);

            return (plateauDetected, regressionDetected);
        }

        /// <summary>
        /// Calculates variance of a dataset.
        /// </summary>
        private static double CalculateVariance(List<double> values)
        {
            if (values.Count <= 1)
                return 0;

            var mean = values.Average();
            var sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
            return sumSquaredDiffs / (values.Count - 1);
        }

        /// <summary>
        /// Checks if the current session is stable (for inflation prevention).
        /// </summary>
        private async Task<(bool IsStable, double MaxAllowedScore)> CheckSessionStabilityAsync(
            CancellationToken cancellationToken)
        {
            var recentScores = await _scoreRepository.GetRecentScoresAsync(_currentUserId, 5, cancellationToken);
            
            if (recentScores.Count < 3)
                return (true, 100);

            var scores = recentScores.Select(s => s.RawScore).ToList();
            var variance = CalculateVariance(scores);
            var isStable = variance >= MinimumVarianceThreshold;

            // Cap score during unstable sessions
            var maxAllowed = isStable ? 100.0 : Math.Min(80.0, scores.Average() + 10);

            return (isStable, maxAllowed);
        }

        /// <summary>
        /// Persists the score snapshot to storage.
        /// </summary>
        private async Task PersistSnapshotAsync(
            FemVoiceScoreSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            await _scoreRepository.SaveScoreAsync(snapshot, cancellationToken);
            
            _recentSnapshots.Add(snapshot);
            
            // Trim to rolling window
            var cutoff = DateTime.Now.AddDays(-_configuration.BaselineRollingDays);
            _recentSnapshots.RemoveAll(s => s.Timestamp < cutoff);
        }

        /// <summary>
        /// Raises the ScoreUpdated event in a thread-safe manner.
        /// </summary>
        private void RaiseScoreUpdatedEvent(FemVoiceScoreSnapshot snapshot)
        {
            var handler = ScoreUpdated;
            if (handler == null)
                return;

            // Always raise synchronously to ensure event is raised before method returns
            // This is important for unit testing scenarios
            handler(snapshot);
        }

        /// <summary>
        /// Creates a new baseline for a user.
        /// </summary>
        private static UserScoreBaseline CreateNewBaseline(int userId)
        {
            return new UserScoreBaseline
            {
                UserId = userId,
                BaselineScore = 0,
                BaselineResonance = 0,
                BaselinePitch = 0,
                BaselineStability = 0,
                ExponentialSmoothedScore = 0,
                CalculatedAt = DateTime.Now,
                LastUpdated = DateTime.Now,
                DataPointCount = 0,
                ConsecutiveStableDays = 0,
                IsPlateau = false,
                IsRegression = false
            };
        }

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            ScoreUpdated = null;
            _stateLock.Dispose();
        }

        #endregion
    }
}
