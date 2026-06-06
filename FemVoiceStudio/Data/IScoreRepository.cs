using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Repository interface for persisting and retrieving score-related data.
    /// Enables testability through dependency injection of in-memory implementations.
    /// </summary>
    public interface IScoreRepository
    {
        /// <summary>
        /// Gets the user's score history within the specified date range.
        /// </summary>
        Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetScoreHistoryAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the most recent score snapshots.
        /// </summary>
        Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetRecentScoresAsync(int userId, int count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a score snapshot to persistence.
        /// </summary>
        Task SaveScoreAsync(FemVoiceScoreSnapshot snapshot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the user's baseline data for adaptive scoring.
        /// </summary>
        Task<UserScoreBaseline?> GetBaselineAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves or updates the user's baseline data.
        /// </summary>
        Task SaveBaselineAsync(UserScoreBaseline baseline, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the user's rolling score history for trend calculation.
        /// </summary>
        Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetRollingHistoryAsync(int userId, int days, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// User baseline data for adaptive scoring calculations.
    /// </summary>
    public class UserScoreBaseline
    {
        public int UserId { get; set; }
        public double BaselineScore { get; set; }
        public double BaselineResonance { get; set; }
        public double BaselinePitch { get; set; }
        public double BaselineStability { get; set; }
        public double ExponentialSmoothedScore { get; set; }
        public DateTime CalculatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public int DataPointCount { get; set; }
        public int ConsecutiveStableDays { get; set; }
        public bool IsPlateau { get; set; }
        public bool IsRegression { get; set; }
    }
}
