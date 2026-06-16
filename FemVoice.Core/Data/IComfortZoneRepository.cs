using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Data
{
    /// <summary>
    /// Repository interface for persisting and retrieving comfort zone data.
    /// Enables testability through dependency injection of in-memory implementations.
    /// </summary>
    public interface IComfortZoneRepository
    {
        /// <summary>
        /// Gets the user's current comfort zone state.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current comfort zone state or null if not set.</returns>
        Task<ComfortZoneState?> GetZoneAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves or updates the user's comfort zone state.
        /// </summary>
        /// <param name="zone">The zone state to save.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveZoneAsync(ComfortZoneState zone, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the user's comfort zone history for trend analysis.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="days">Number of days to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of historical zone states.</returns>
        Task<IReadOnlyList<ComfortZoneState>> GetZoneHistoryAsync(int userId, int days, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets stability data for zone management decisions.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="days">Number of days to analyze.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of daily stability records.</returns>
        Task<IReadOnlyList<DailyStabilityRecord>> GetStabilityHistoryAsync(int userId, int days, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a daily stability measurement.
        /// </summary>
        /// <param name="record">The stability record to save.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveStabilityRecordAsync(DailyStabilityRecord record, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a daily stability measurement for zone management.
    /// </summary>
    public class DailyStabilityRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public double ResonanceStability { get; set; }
        public double PitchStability { get; set; }
        public double OverallStability { get; set; }
        public bool WasInZone { get; set; }
        public bool HadStrain { get; set; }
    }
}
