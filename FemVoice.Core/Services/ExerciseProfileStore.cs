using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using Microsoft.Data.Sqlite;

namespace FemVoiceStudio.Services
{
    public sealed record ExerciseProfileOverride
    {
        public int UserId { get; init; } = 1;
        public int ExerciseId { get; init; }
        public ExerciseTargetProfile Profile { get; init; } = ExerciseTargetProfile.CreateResonanceHumming();
        public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
        public string ReasonCode { get; init; } = string.Empty;
        public string Source { get; init; } = "ProgressionOrchestrator";
    }

    public interface IExerciseProfileStore
    {
        Task SaveAsync(ExerciseProfileOverride profileOverride, CancellationToken cancellationToken = default);
        Task<ExerciseProfileOverride?> GetAsync(int userId, int exerciseId, CancellationToken cancellationToken = default);
    }

    public sealed class InMemoryExerciseProfileStore : IExerciseProfileStore
    {
        private readonly object _sync = new();
        private readonly Dictionary<(int UserId, int ExerciseId), ExerciseProfileOverride> _profiles = new();

        public Task SaveAsync(ExerciseProfileOverride profileOverride, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            profileOverride.Profile.Validate();

            lock (_sync)
            {
                _profiles[(profileOverride.UserId, profileOverride.ExerciseId)] = profileOverride;
            }

            return Task.CompletedTask;
        }

        public Task<ExerciseProfileOverride?> GetAsync(int userId, int exerciseId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                _profiles.TryGetValue((userId, exerciseId), out var profileOverride);
                return Task.FromResult(profileOverride);
            }
        }
    }

    public sealed class SqliteExerciseProfileStore : IExerciseProfileStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly string _connectionString;

        public SqliteExerciseProfileStore(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            EnsureSchema();
        }

        public async Task SaveAsync(ExerciseProfileOverride profileOverride, CancellationToken cancellationToken = default)
        {
            profileOverride.Profile.Validate();

            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ExerciseProfileOverrides (
                    UserId, ExerciseId, UpdatedAt, ReasonCode, Source, ProfileJson)
                VALUES (
                    @UserId, @ExerciseId, @UpdatedAt, @ReasonCode, @Source, @ProfileJson)
                ON CONFLICT(UserId, ExerciseId) DO UPDATE SET
                    UpdatedAt = excluded.UpdatedAt,
                    ReasonCode = excluded.ReasonCode,
                    Source = excluded.Source,
                    ProfileJson = excluded.ProfileJson;";

            command.Parameters.AddWithValue("@UserId", profileOverride.UserId);
            command.Parameters.AddWithValue("@ExerciseId", profileOverride.ExerciseId);
            command.Parameters.AddWithValue("@UpdatedAt", profileOverride.UpdatedAt.ToString("o"));
            command.Parameters.AddWithValue("@ReasonCode", profileOverride.ReasonCode);
            command.Parameters.AddWithValue("@Source", profileOverride.Source);
            command.Parameters.AddWithValue("@ProfileJson", JsonSerializer.Serialize(profileOverride.Profile, JsonOptions));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<ExerciseProfileOverride?> GetAsync(
            int userId,
            int exerciseId,
            CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT UserId, ExerciseId, UpdatedAt, ReasonCode, Source, ProfileJson
                FROM ExerciseProfileOverrides
                WHERE UserId = @UserId AND ExerciseId = @ExerciseId
                LIMIT 1;";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@ExerciseId", exerciseId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            var profile = JsonSerializer.Deserialize<ExerciseTargetProfile>(
                reader.GetString(reader.GetOrdinal("ProfileJson")),
                JsonOptions);

            if (profile == null)
                return null;

            profile.Validate();

            return new ExerciseProfileOverride
            {
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                ExerciseId = reader.GetInt32(reader.GetOrdinal("ExerciseId")),
                UpdatedAt = DateTime.Parse(
                    reader.GetString(reader.GetOrdinal("UpdatedAt")),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind),
                ReasonCode = reader.GetString(reader.GetOrdinal("ReasonCode")),
                Source = reader.GetString(reader.GetOrdinal("Source")),
                Profile = profile
            };
        }

        private void EnsureSchema()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ExerciseProfileOverrides (
                    UserId INTEGER NOT NULL DEFAULT 1,
                    ExerciseId INTEGER NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    ReasonCode TEXT NOT NULL DEFAULT '',
                    Source TEXT NOT NULL DEFAULT '',
                    ProfileJson TEXT NOT NULL,
                    PRIMARY KEY (UserId, ExerciseId)
                );";
            command.ExecuteNonQuery();
        }

        private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }
}
