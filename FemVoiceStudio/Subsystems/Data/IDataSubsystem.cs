using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Subsystems.SmartCoach;

namespace FemVoiceStudio.Subsystems.Data
{
    /// <summary>
    /// User profile data
    /// </summary>
    public class UserProfile
    {
        public int UserId { get; set; } = 1;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastActiveAt { get; set; } = DateTime.Now;
        public DifficultyLevel CurrentDifficulty { get; set; } = DifficultyLevel.Nybegynner;
        public int TotalSessionsCompleted { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public int TotalMinutes { get; set; }
    }

    /// <summary>
    /// Data subsystem interface - handles data persistence and repository operations
    /// </summary>
    public interface IDataSubsystem : IDisposable
    {
        /// <summary>
        /// Get training sessions within date range
        /// </summary>
        Task<IEnumerable<TrainingSession>> GetSessionsAsync(DateTime from, DateTime to, CancellationToken ct = default);

        /// <summary>
        /// Get a single session by ID
        /// </summary>
        Task<TrainingSession?> GetSessionAsync(int sessionId, CancellationToken ct = default);

        /// <summary>
        /// Save a training session
        /// </summary>
        Task<int> SaveSessionAsync(TrainingSession session, CancellationToken ct = default);

        /// <summary>
        /// Update an existing session
        /// </summary>
        Task UpdateSessionAsync(TrainingSession session, CancellationToken ct = default);

        /// <summary>
        /// Delete a session
        /// </summary>
        Task DeleteSessionAsync(int sessionId, CancellationToken ct = default);

        /// <summary>
        /// Get user profile
        /// </summary>
        Task<UserProfile> GetUserProfileAsync(CancellationToken ct = default);

        /// <summary>
        /// Save user profile
        /// </summary>
        Task SaveUserProfileAsync(UserProfile profile, CancellationToken ct = default);

        /// <summary>
        /// Get user settings
        /// </summary>
        Task<UserSettings> GetSettingsAsync(CancellationToken ct = default);

        /// <summary>
        /// Save user settings
        /// </summary>
        Task SaveSettingsAsync(UserSettings settings, CancellationToken ct = default);

        /// <summary>
        /// Get voice profile
        /// </summary>
        Task<SmartCoach.VoiceProfile> GetVoiceProfileAsync(CancellationToken ct = default);

        /// <summary>
        /// Save voice profile
        /// </summary>
        Task SaveVoiceProfileAsync(SmartCoach.VoiceProfile profile, CancellationToken ct = default);

        /// <summary>
        /// Export data to specified format (json, csv)
        /// </summary>
        Task ExportDataAsync(string format, string filePath, CancellationToken ct = default);

        /// <summary>
        /// Import data from specified format
        /// </summary>
        Task ImportDataAsync(string format, string filePath, CancellationToken ct = default);

        /// <summary>
        /// Create database backup
        /// </summary>
        Task<string> CreateBackupAsync(string? customPath = null, CancellationToken ct = default);

        /// <summary>
        /// Restore from backup
        /// </summary>
        Task RestoreBackupAsync(string backupPath, CancellationToken ct = default);

        /// <summary>
        /// Get statistics summary
        /// </summary>
        Task<StatisticsSummary> GetStatisticsSummaryAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Statistics summary
    /// </summary>
    public class StatisticsSummary
    {
        public int TotalSessions { get; set; }
        public int TotalMinutes { get; set; }
        public double AverageScore { get; set; }
        public double BestScore { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateTime? FirstSessionDate { get; set; }
        public DateTime? LastSessionDate { get; set; }
    }
}
