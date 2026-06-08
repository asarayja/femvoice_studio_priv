using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using Microsoft.Data.Sqlite;

namespace FemVoiceStudio.Services
{
    // ── Repository interface ──────────────────────────────────────────────────────

    /// <summary>
    /// Persistence contract for OUTCOME-PROFILE snapshots (Sprint E, W0-A4). A snapshot is a
    /// whole <see cref="OutcomeProfile"/> serialised to JSON and keyed by a stable
    /// <c>OutcomeId</c> (upsert semantics). Reads can fetch one snapshot by id or the latest
    /// snapshot for a user.
    /// </summary>
    public interface IOutcomeProfileRepository
    {
        /// <summary>Persists a snapshot or updates an existing one (upsert on OutcomeId).</summary>
        Task SaveSnapshotAsync(Guid outcomeId, OutcomeProfile profile, CancellationToken cancellationToken = default);

        /// <summary>Returns the snapshot with <paramref name="outcomeId"/>, or null.</summary>
        Task<OutcomeProfile?> GetSnapshotAsync(Guid outcomeId, CancellationToken cancellationToken = default);

        /// <summary>Returns the most recently generated snapshot for <paramref name="userId"/>, or null.</summary>
        Task<OutcomeProfile?> GetLatestForUserAsync(int userId, CancellationToken cancellationToken = default);
    }

    // ── In-memory repository ──────────────────────────────────────────────────────

    /// <summary>
    /// In-memory implementation of <see cref="IOutcomeProfileRepository"/>. Thread-safe via a
    /// plain lock. Intended for unit tests and temporary dev flows.
    /// </summary>
    public sealed class InMemoryOutcomeProfileRepository : IOutcomeProfileRepository
    {
        private readonly object _sync = new();
        private readonly Dictionary<Guid, (int UserId, DateTime GeneratedAt, OutcomeProfile Profile)> _snapshots = new();

        public Task SaveSnapshotAsync(Guid outcomeId, OutcomeProfile profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(profile);
            lock (_sync)
            {
                _snapshots[outcomeId] = (profile.UserId, profile.GeneratedAt, profile);
            }
            return Task.CompletedTask;
        }

        public Task<OutcomeProfile?> GetSnapshotAsync(Guid outcomeId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                return Task.FromResult(_snapshots.TryGetValue(outcomeId, out var row) ? row.Profile : null);
            }
        }

        public Task<OutcomeProfile?> GetLatestForUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                var latest = _snapshots.Values
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.GeneratedAt)
                    .Select(s => s.Profile)
                    .FirstOrDefault();
                return Task.FromResult<OutcomeProfile?>(latest);
            }
        }
    }

    // ── SQLite repository ─────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite-backed implementation of <see cref="IOutcomeProfileRepository"/>. Persists in the
    /// <c>OutcomeProfileSnapshots</c> table inside the shared femvoice.db, storing the whole
    /// <see cref="OutcomeProfile"/> as a JSON column.
    ///
    /// Schema is created idempotently in the constructor via <c>EnsureSchema()</c> +
    /// PRAGMA table_info ALTER migration — the same pattern as
    /// <see cref="SqliteSmartCoachMemoryRepository"/>. NEVER SELECT * — always the explicit
    /// <see cref="SnapshotColumns"/> constant. DateTime via .ToString("o") +
    /// DateTimeStyles.RoundtripKind; Guid via .ToString("D")/Guid.Parse.
    /// </summary>
    public sealed class SqliteOutcomeProfileRepository : IOutcomeProfileRepository
    {
        // Web defaults match the JSON-snapshot convention used by ExerciseProfileStore.
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        // Explicit column list for SELECT — positional reads survive ALTER-added columns.
        private const string SnapshotColumns =
            "OutcomeId, UserId, GeneratedAt, ProfileJson";

        private readonly string _connectionString;

        public SqliteOutcomeProfileRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            EnsureSchema();
        }

        public async Task SaveSnapshotAsync(Guid outcomeId, OutcomeProfile profile, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);

            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = @"
                INSERT INTO OutcomeProfileSnapshots (
                    OutcomeId, UserId, GeneratedAt, ProfileJson)
                VALUES (
                    @OutcomeId, @UserId, @GeneratedAt, @ProfileJson)
                ON CONFLICT(OutcomeId) DO UPDATE SET
                    UserId = excluded.UserId,
                    GeneratedAt = excluded.GeneratedAt,
                    ProfileJson = excluded.ProfileJson;";

            command.Parameters.AddWithValue("@OutcomeId", outcomeId.ToString("D"));
            command.Parameters.AddWithValue("@UserId", profile.UserId);
            command.Parameters.AddWithValue("@GeneratedAt", profile.GeneratedAt.ToString("o"));
            command.Parameters.AddWithValue("@ProfileJson", JsonSerializer.Serialize(profile, JsonOptions));

            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<OutcomeProfile?> GetSnapshotAsync(Guid outcomeId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {SnapshotColumns} FROM OutcomeProfileSnapshots
                WHERE OutcomeId = @OutcomeId;";
            command.Parameters.AddWithValue("@OutcomeId", outcomeId.ToString("D"));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return MapSnapshot(reader);
        }

        public async Task<OutcomeProfile?> GetLatestForUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {SnapshotColumns} FROM OutcomeProfileSnapshots
                WHERE UserId = @UserId
                ORDER BY GeneratedAt DESC
                LIMIT 1;";
            command.Parameters.AddWithValue("@UserId", userId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return MapSnapshot(reader);
        }

        // ── Schema ────────────────────────────────────────────────────────────────

        private void EnsureSchema()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS OutcomeProfileSnapshots (
                    OutcomeId TEXT PRIMARY KEY,
                    UserId INTEGER NOT NULL,
                    GeneratedAt TEXT NOT NULL,
                    ProfileJson TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_outcome_snapshots_user_generated
                    ON OutcomeProfileSnapshots(UserId, GeneratedAt);";
            command.ExecuteNonQuery();

            // Idempotent ALTER migration — adds any missing columns to pre-existing tables.
            EnsureSnapshotColumns(connection, transaction);

            transaction.Commit();
        }

        /// <summary>
        /// Idempotent ALTER migration: adds any of the expected columns that a pre-existing
        /// OutcomeProfileSnapshots table is missing. Safe to run repeatedly — PRAGMA
        /// table_info guards each ALTER so re-running EnsureSchema never throws "duplicate
        /// column".
        /// </summary>
        private static void EnsureSnapshotColumns(SqliteConnection connection, SqliteTransaction transaction)
        {
            var existing = ReadColumnNames(connection, transaction, "OutcomeProfileSnapshots");

            // (column name, DDL). Order matches SnapshotColumns constant.
            (string Name, string Ddl)[] expectedColumns =
            {
                ("OutcomeId",   "TEXT PRIMARY KEY"),
                ("UserId",      "INTEGER NOT NULL"),
                ("GeneratedAt", "TEXT NOT NULL"),
                ("ProfileJson", "TEXT NOT NULL"),
            };

            foreach (var (name, ddl) in expectedColumns)
            {
                // PRIMARY KEY columns cannot be added with ALTER TABLE — skip them if missing
                // (they must have been created by the CREATE TABLE IF NOT EXISTS above).
                if (name == "OutcomeId") continue;
                if (existing.Contains(name)) continue;

                using var alter = connection.CreateCommand();
                alter.Transaction = transaction;
                // Column names are compile-time constants (never user input) → safe.
                alter.CommandText = $"ALTER TABLE OutcomeProfileSnapshots ADD COLUMN {name} {ddl};";
                alter.ExecuteNonQuery();
            }
        }

        private static HashSet<string> ReadColumnNames(
            SqliteConnection connection, SqliteTransaction transaction, string table)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"PRAGMA table_info({table});";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                // PRAGMA table_info columns: cid, name, type, notnull, dflt_value, pk
                columns.Add(reader.GetString(reader.GetOrdinal("name")));
            }
            return columns;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        private static OutcomeProfile? MapSnapshot(SqliteDataReader reader)
        {
            var json = reader.GetString(reader.GetOrdinal("ProfileJson"));
            return JsonSerializer.Deserialize<OutcomeProfile>(json, JsonOptions);
        }
    }

    // ── Facade ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Facade over <see cref="IOutcomeProfileRepository"/>. Saving generates a fresh
    /// <c>OutcomeId</c> per call (so each snapshot is independently retrievable) and returns
    /// it; an explicit id can be supplied to upsert a specific snapshot.
    ///
    /// This is a DESCRIPTIVE / REPORTING persistence layer only — it never overrides any
    /// safety, health, or recovery gate.
    /// </summary>
    public sealed class OutcomeProfileStore
    {
        private readonly IOutcomeProfileRepository _repository;

        public OutcomeProfileStore(IOutcomeProfileRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Saves <paramref name="profile"/> under a freshly generated OutcomeId and returns
        /// that id.
        /// </summary>
        public async Task<Guid> SaveSnapshotAsync(OutcomeProfile profile, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var outcomeId = Guid.NewGuid();
            await _repository.SaveSnapshotAsync(outcomeId, profile, cancellationToken).ConfigureAwait(false);
            return outcomeId;
        }

        /// <summary>Upserts <paramref name="profile"/> under the supplied <paramref name="outcomeId"/>.</summary>
        public Task SaveSnapshotAsync(Guid outcomeId, OutcomeProfile profile, CancellationToken cancellationToken = default)
            => _repository.SaveSnapshotAsync(outcomeId, profile, cancellationToken);

        /// <summary>Returns the snapshot with <paramref name="outcomeId"/>, or null.</summary>
        public Task<OutcomeProfile?> GetSnapshotAsync(Guid outcomeId, CancellationToken cancellationToken = default)
            => _repository.GetSnapshotAsync(outcomeId, cancellationToken);

        /// <summary>Returns the most recent snapshot for <paramref name="userId"/>, or null.</summary>
        public Task<OutcomeProfile?> GetLatestForUserAsync(int userId, CancellationToken cancellationToken = default)
            => _repository.GetLatestForUserAsync(userId, cancellationToken);
    }
}
