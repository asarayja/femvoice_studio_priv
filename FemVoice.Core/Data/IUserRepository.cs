using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Repository interface for user-specific settings and preferences.
    /// Enables testability through dependency injection of in-memory implementations.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Gets user-specific scoring configuration (weights and thresholds).
        /// </summary>
        Task<ScoringConfiguration?> GetScoringConfigurationAsync(int userId);

        /// <summary>
        /// Saves user-specific scoring configuration.
        /// </summary>
        Task SaveScoringConfigurationAsync(int userId, ScoringConfiguration config);

        /// <summary>
        /// Gets user-specific comfort zone settings.
        /// </summary>
        Task<UserComfortZoneSettings?> GetComfortZoneSettingsAsync(int userId);

        /// <summary>
        /// Saves user-specific comfort zone settings.
        /// </summary>
        Task SaveComfortZoneSettingsAsync(int userId, UserComfortZoneSettings settings);

        /// <summary>
        /// Gets user health data for scoring and zone decisions.
        /// </summary>
        Task<UserHealthData?> GetUserHealthDataAsync(int userId);

        /// <summary>
        /// Records a strain incident for the user.
        /// </summary>
        Task RecordStrainIncidentAsync(int userId, StrainIncident incident);

        /// <summary>
        /// Gets recent strain incidents for the user.
        /// </summary>
        Task<IReadOnlyList<StrainIncident>> GetRecentStrainIncidentsAsync(int userId, int days);

        /// <summary>
        /// Gets user tolerance data for individualized zone width.
        /// </summary>
        Task<UserToleranceData?> GetToleranceDataAsync(int userId);

        /// <summary>
        /// Updates user tolerance data after a session.
        /// </summary>
        Task UpdateToleranceDataAsync(int userId, UserToleranceData data);
    }

    /// <summary>
    /// User-specific scoring weights and thresholds.
    /// </summary>
    public class ScoringConfiguration
    {
        public int UserId { get; set; }
        public double ResonanceWeight { get; set; } = 0.45;
        public double PitchWeight { get; set; } = 0.30;
        public double StabilityWeight { get; set; } = 0.15;
        public double HealthWeight { get; set; } = 0.10;
        public double PlateauThresholdDays { get; set; } = 14;
        public double RegressionThresholdPercent { get; set; } = 0.10;
        public int BaselineRollingDays { get; set; } = 30;
        public double SmoothingAlpha { get; set; } = 0.3;
    }

    /// <summary>
    /// User-specific comfort zone preferences.
    /// </summary>
    public class UserComfortZoneSettings
    {
        public int UserId { get; set; }
        public double PreferredMinPitch { get; set; } = 165;
        public double PreferredMaxPitch { get; set; } = 255;
        public double ZoneWidth { get; set; } = 90;
        public double IndividualizedWidthMultiplier { get; set; } = 1.0;
        public DateTime LastZoneUpdate { get; set; }
    }

    /// <summary>
    /// User health data for scoring and safety decisions.
    /// </summary>
    public class UserHealthData
    {
        public int UserId { get; set; }
        public double HealthScore { get; set; } = 100;
        public double CurrentStrainLevel { get; set; }
        public DateTime LastStrainIncident { get; set; }
        public int StrainIncidentCount { get; set; }
    }

    /// <summary>
    /// Record of a strain incident.
    /// </summary>
    public class StrainIncident
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public double StrainLevel { get; set; }
        public string StrainType { get; set; } = string.Empty;
        public double PitchAtIncident { get; set; }
        public double ResonanceAtIncident { get; set; }
    }

    /// <summary>
    /// User tolerance data for individualized zone width.
    /// </summary>
    public class UserToleranceData
    {
        public int UserId { get; set; }
        public double AverageToleratedWidth { get; set; } = 90;
        public double MaxToleratedWidth { get; set; } = 120;
        public double MinToleratedWidth { get; set; } = 60;
        public int SampleCount { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
