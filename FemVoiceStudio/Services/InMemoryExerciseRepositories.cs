using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Lightweight runtime repository for exercise biofeedback services.
    /// Keeps the live exercise ViewModel constructible until these values are backed
    /// by the main SQLite data model.
    /// </summary>
    public sealed class InMemoryExerciseRepository : IUserRepository, IScoreRepository
    {
        private readonly object _lock = new();
        private readonly Dictionary<int, ScoringConfiguration> _scoringConfigurations = new();
        private readonly Dictionary<int, UserComfortZoneSettings> _comfortZoneSettings = new();
        private readonly Dictionary<int, UserHealthData> _healthData = new();
        private readonly Dictionary<int, UserToleranceData> _toleranceData = new();
        private readonly Dictionary<int, UserScoreBaseline> _baselines = new();
        private readonly List<StrainIncident> _strainIncidents = new();
        private readonly List<FemVoiceScoreSnapshot> _scores = new();

        public Task<ScoringConfiguration?> GetScoringConfigurationAsync(int userId)
        {
            lock (_lock)
            {
                return Task.FromResult<ScoringConfiguration?>(_scoringConfigurations.TryGetValue(userId, out var config)
                    ? config
                    : new ScoringConfiguration { UserId = userId });
            }
        }

        public Task SaveScoringConfigurationAsync(int userId, ScoringConfiguration config)
        {
            lock (_lock)
            {
                config.UserId = userId;
                _scoringConfigurations[userId] = config;
            }

            return Task.CompletedTask;
        }

        public Task<UserComfortZoneSettings?> GetComfortZoneSettingsAsync(int userId)
        {
            lock (_lock)
            {
                return Task.FromResult<UserComfortZoneSettings?>(_comfortZoneSettings.TryGetValue(userId, out var settings)
                    ? settings
                    : new UserComfortZoneSettings
                    {
                        UserId = userId,
                        LastZoneUpdate = DateTime.Now
                    });
            }
        }

        public Task SaveComfortZoneSettingsAsync(int userId, UserComfortZoneSettings settings)
        {
            lock (_lock)
            {
                settings.UserId = userId;
                _comfortZoneSettings[userId] = settings;
            }

            return Task.CompletedTask;
        }

        public Task<UserHealthData?> GetUserHealthDataAsync(int userId)
        {
            lock (_lock)
            {
                return Task.FromResult<UserHealthData?>(_healthData.TryGetValue(userId, out var data)
                    ? data
                    : new UserHealthData { UserId = userId, HealthScore = 100 });
            }
        }

        public Task RecordStrainIncidentAsync(int userId, StrainIncident incident)
        {
            lock (_lock)
            {
                incident.UserId = userId;
                if (incident.Timestamp == default)
                    incident.Timestamp = DateTime.Now;

                _strainIncidents.Add(incident);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StrainIncident>> GetRecentStrainIncidentsAsync(int userId, int days)
        {
            lock (_lock)
            {
                var cutoff = DateTime.Now.AddDays(-days);
                var incidents = _strainIncidents
                    .Where(i => i.UserId == userId && i.Timestamp >= cutoff)
                    .OrderByDescending(i => i.Timestamp)
                    .ToList();

                return Task.FromResult<IReadOnlyList<StrainIncident>>(incidents);
            }
        }

        public Task<UserToleranceData?> GetToleranceDataAsync(int userId)
        {
            lock (_lock)
            {
                return Task.FromResult<UserToleranceData?>(_toleranceData.TryGetValue(userId, out var data)
                    ? data
                    : new UserToleranceData { UserId = userId, LastUpdated = DateTime.Now });
            }
        }

        public Task UpdateToleranceDataAsync(int userId, UserToleranceData data)
        {
            lock (_lock)
            {
                data.UserId = userId;
                data.LastUpdated = DateTime.Now;
                _toleranceData[userId] = data;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetScoreHistoryAsync(
            int userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var scores = _scores
                    .Where(s => s.UserId == userId && s.Timestamp >= from && s.Timestamp <= to)
                    .OrderBy(s => s.Timestamp)
                    .ToList();

                return Task.FromResult<IReadOnlyList<FemVoiceScoreSnapshot>>(scores);
            }
        }

        public Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetRecentScoresAsync(
            int userId,
            int count,
            CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var scores = _scores
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.Timestamp)
                    .Take(count)
                    .ToList();

                return Task.FromResult<IReadOnlyList<FemVoiceScoreSnapshot>>(scores);
            }
        }

        public Task SaveScoreAsync(FemVoiceScoreSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _scores.Add(snapshot);
            }

            return Task.CompletedTask;
        }

        public Task<UserScoreBaseline?> GetBaselineAsync(int userId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_baselines.TryGetValue(userId, out var baseline)
                    ? baseline
                    : null);
            }
        }

        public Task SaveBaselineAsync(UserScoreBaseline baseline, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _baselines[baseline.UserId] = baseline;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FemVoiceScoreSnapshot>> GetRollingHistoryAsync(
            int userId,
            int days,
            CancellationToken cancellationToken = default)
        {
            var from = DateTime.Now.AddDays(-days);
            return GetScoreHistoryAsync(userId, from, DateTime.Now, cancellationToken);
        }
    }
}
