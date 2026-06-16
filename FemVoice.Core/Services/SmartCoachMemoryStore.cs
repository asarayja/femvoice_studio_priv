using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using Microsoft.Data.Sqlite;

namespace FemVoiceStudio.Services
{
    // ── Repository interface ──────────────────────────────────────────────────────

    /// <summary>
    /// Persistence contract for SmartCoach advice history (A8).
    /// All three methods are idempotent with respect to AdviceId (upsert semantics).
    /// </summary>
    public interface ISmartCoachMemoryRepository
    {
        /// <summary>Persists a new advice entry or updates an existing one (upsert on AdviceId).</summary>
        Task SaveAdviceAsync(SmartCoachAdviceEntry entry, CancellationToken cancellationToken = default);

        /// <summary>Returns all advice entries for <paramref name="userId"/> in [from, to).</summary>
        Task<IReadOnlyList<SmartCoachAdviceEntry>> GetAdviceHistoryAsync(
            int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records the outcome of a previously-saved advice entry.
        /// Fields that are null are written as SQL NULL (not skipped — a null outcomeGain
        /// means "outcome not yet available" and replaces any previous value).
        /// </summary>
        Task RecordOutcomeAsync(
            Guid adviceId,
            DateTime? started,
            DateTime? completed,
            bool followed,
            double? outcomeGain,
            bool success,
            CancellationToken cancellationToken = default);
    }

    // ── In-memory repository ──────────────────────────────────────────────────────

    /// <summary>
    /// In-memory implementation of <see cref="ISmartCoachMemoryRepository"/>.
    /// Thread-safe via a plain lock. Intended for unit tests and temporary dev flows.
    /// </summary>
    public sealed class InMemorySmartCoachMemoryRepository : ISmartCoachMemoryRepository
    {
        private readonly object _sync = new();
        private readonly Dictionary<Guid, SmartCoachAdviceEntry> _entries = new();

        public Task SaveAdviceAsync(SmartCoachAdviceEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _entries[entry.AdviceId] = entry;
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SmartCoachAdviceEntry>> GetAdviceHistoryAsync(
            int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                var result = _entries.Values
                    .Where(e => e.UserId == userId && e.RecommendedAt >= from && e.RecommendedAt < to)
                    .OrderBy(e => e.RecommendedAt)
                    .ToList();
                return Task.FromResult<IReadOnlyList<SmartCoachAdviceEntry>>(result);
            }
        }

        public Task RecordOutcomeAsync(
            Guid adviceId,
            DateTime? started,
            DateTime? completed,
            bool followed,
            double? outcomeGain,
            bool success,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!_entries.TryGetValue(adviceId, out var existing)) return Task.CompletedTask;
                _entries[adviceId] = existing with
                {
                    StartedAt = started,
                    CompletedAt = completed,
                    UserFollowedAdvice = followed,
                    OutcomeGain = outcomeGain,
                    Success = success
                };
            }
            return Task.CompletedTask;
        }
    }

    // ── SQLite repository ─────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite-backed implementation of <see cref="ISmartCoachMemoryRepository"/>.
    /// Persists in the <c>SmartCoachAdviceLog</c> table inside the shared femvoice.db.
    ///
    /// Schema is created idempotently in the constructor via <c>EnsureSchema()</c> +
    /// PRAGMA table_info ALTER migration — the same pattern as
    /// <see cref="SqliteSessionAnalyticsRepository"/>. NEVER SELECT * — always use
    /// the explicit <see cref="AdviceColumns"/> constant. DateTime via .ToString("o")
    /// + DateTimeStyles.RoundtripKind.
    /// </summary>
    public sealed class SqliteSmartCoachMemoryRepository : ISmartCoachMemoryRepository
    {
        // Explicit column list for SELECT — positional reads survive ALTER-added columns.
        private const string AdviceColumns =
            "AdviceId, UserId, RecommendedAt, FocusArea, RecommendedExerciseId, " +
            "StartedAt, CompletedAt, UserFollowedAdvice, OutcomeGain, Success";

        private readonly string _connectionString;

        public SqliteSmartCoachMemoryRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            EnsureSchema();
        }

        public async Task SaveAdviceAsync(SmartCoachAdviceEntry entry, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = @"
                INSERT INTO SmartCoachAdviceLog (
                    AdviceId, UserId, RecommendedAt, FocusArea, RecommendedExerciseId,
                    StartedAt, CompletedAt, UserFollowedAdvice, OutcomeGain, Success)
                VALUES (
                    @AdviceId, @UserId, @RecommendedAt, @FocusArea, @RecommendedExerciseId,
                    @StartedAt, @CompletedAt, @UserFollowedAdvice, @OutcomeGain, @Success)
                ON CONFLICT(AdviceId) DO UPDATE SET
                    UserId = excluded.UserId,
                    RecommendedAt = excluded.RecommendedAt,
                    FocusArea = excluded.FocusArea,
                    RecommendedExerciseId = excluded.RecommendedExerciseId,
                    StartedAt = excluded.StartedAt,
                    CompletedAt = excluded.CompletedAt,
                    UserFollowedAdvice = excluded.UserFollowedAdvice,
                    OutcomeGain = excluded.OutcomeGain,
                    Success = excluded.Success;";

            AddAdviceParameters(command, entry);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SmartCoachAdviceEntry>> GetAdviceHistoryAsync(
            int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {AdviceColumns} FROM SmartCoachAdviceLog
                WHERE UserId = @UserId AND RecommendedAt >= @From AND RecommendedAt < @To
                ORDER BY RecommendedAt;";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@From", from.ToString("o"));
            command.Parameters.AddWithValue("@To", to.ToString("o"));

            var entries = new List<SmartCoachAdviceEntry>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(MapEntry(reader));
            }
            return entries;
        }

        public async Task RecordOutcomeAsync(
            Guid adviceId,
            DateTime? started,
            DateTime? completed,
            bool followed,
            double? outcomeGain,
            bool success,
            CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = @"
                UPDATE SmartCoachAdviceLog
                SET StartedAt = @StartedAt,
                    CompletedAt = @CompletedAt,
                    UserFollowedAdvice = @UserFollowedAdvice,
                    OutcomeGain = @OutcomeGain,
                    Success = @Success
                WHERE AdviceId = @AdviceId;";

            command.Parameters.AddWithValue("@AdviceId", adviceId.ToString("D"));
            command.Parameters.AddWithValue("@StartedAt",
                started.HasValue ? (object)started.Value.ToString("o") : DBNull.Value);
            command.Parameters.AddWithValue("@CompletedAt",
                completed.HasValue ? (object)completed.Value.ToString("o") : DBNull.Value);
            command.Parameters.AddWithValue("@UserFollowedAdvice", followed ? 1 : 0);
            command.Parameters.AddWithValue("@OutcomeGain",
                outcomeGain.HasValue ? (object)outcomeGain.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Success", success ? 1 : 0);

            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
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
                CREATE TABLE IF NOT EXISTS SmartCoachAdviceLog (
                    AdviceId TEXT PRIMARY KEY,
                    UserId INTEGER NOT NULL,
                    RecommendedAt TEXT NOT NULL,
                    FocusArea TEXT,
                    RecommendedExerciseId INTEGER NULL,
                    StartedAt TEXT NULL,
                    CompletedAt TEXT NULL,
                    UserFollowedAdvice INTEGER NOT NULL DEFAULT 0,
                    OutcomeGain REAL NULL,
                    Success INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_smartcoach_advice_user_recommended
                    ON SmartCoachAdviceLog(UserId, RecommendedAt);";
            command.ExecuteNonQuery();

            // Idempotent ALTER migration — adds any missing columns to pre-existing tables.
            EnsureAdviceColumns(connection, transaction);

            transaction.Commit();
        }

        /// <summary>
        /// Idempotent ALTER migration: adds any of the expected columns that a pre-existing
        /// SmartCoachAdviceLog table is missing. Safe to run repeatedly — PRAGMA table_info
        /// guards each ALTER so re-running EnsureSchema never throws "duplicate column".
        /// </summary>
        private static void EnsureAdviceColumns(SqliteConnection connection, SqliteTransaction transaction)
        {
            var existing = ReadColumnNames(connection, transaction, "SmartCoachAdviceLog");

            // (column name, DDL). Order matches AdviceColumns constant.
            (string Name, string Ddl)[] expectedColumns =
            {
                ("AdviceId",              "TEXT PRIMARY KEY"),
                ("UserId",               "INTEGER NOT NULL"),
                ("RecommendedAt",        "TEXT NOT NULL"),
                ("FocusArea",            "TEXT"),
                ("RecommendedExerciseId","INTEGER NULL"),
                ("StartedAt",            "TEXT NULL"),
                ("CompletedAt",          "TEXT NULL"),
                ("UserFollowedAdvice",   "INTEGER NOT NULL DEFAULT 0"),
                ("OutcomeGain",          "REAL NULL"),
                ("Success",              "INTEGER NOT NULL DEFAULT 0"),
            };

            foreach (var (name, ddl) in expectedColumns)
            {
                // PRIMARY KEY columns cannot be added with ALTER TABLE — skip them if missing
                // (they must have been created by the CREATE TABLE IF NOT EXISTS above).
                if (name == "AdviceId") continue;
                if (existing.Contains(name)) continue;

                using var alter = connection.CreateCommand();
                alter.Transaction = transaction;
                // Column names are compile-time constants (never user input) → safe.
                alter.CommandText = $"ALTER TABLE SmartCoachAdviceLog ADD COLUMN {name} {ddl};";
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

        private static void AddAdviceParameters(SqliteCommand command, SmartCoachAdviceEntry entry)
        {
            command.Parameters.AddWithValue("@AdviceId", entry.AdviceId.ToString("D"));
            command.Parameters.AddWithValue("@UserId", entry.UserId);
            command.Parameters.AddWithValue("@RecommendedAt", entry.RecommendedAt.ToString("o"));
            command.Parameters.AddWithValue("@FocusArea", (object?)entry.FocusArea ?? DBNull.Value);
            command.Parameters.AddWithValue("@RecommendedExerciseId",
                entry.RecommendedExerciseId.HasValue ? (object)entry.RecommendedExerciseId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@StartedAt",
                entry.StartedAt.HasValue ? (object)entry.StartedAt.Value.ToString("o") : DBNull.Value);
            command.Parameters.AddWithValue("@CompletedAt",
                entry.CompletedAt.HasValue ? (object)entry.CompletedAt.Value.ToString("o") : DBNull.Value);
            command.Parameters.AddWithValue("@UserFollowedAdvice", entry.UserFollowedAdvice ? 1 : 0);
            command.Parameters.AddWithValue("@OutcomeGain",
                entry.OutcomeGain.HasValue ? (object)entry.OutcomeGain.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Success", entry.Success ? 1 : 0);
        }

        private static SmartCoachAdviceEntry MapEntry(SqliteDataReader reader)
        {
            return new SmartCoachAdviceEntry
            {
                AdviceId = Guid.Parse(reader.GetString(reader.GetOrdinal("AdviceId"))),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                RecommendedAt = DateTime.Parse(
                    reader.GetString(reader.GetOrdinal("RecommendedAt")),
                    null, DateTimeStyles.RoundtripKind),
                FocusArea = reader.IsDBNull(reader.GetOrdinal("FocusArea"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("FocusArea")),
                RecommendedExerciseId = reader.IsDBNull(reader.GetOrdinal("RecommendedExerciseId"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("RecommendedExerciseId")),
                StartedAt = ReadNullableDateTime(reader, "StartedAt"),
                CompletedAt = ReadNullableDateTime(reader, "CompletedAt"),
                UserFollowedAdvice = reader.GetInt32(reader.GetOrdinal("UserFollowedAdvice")) != 0,
                OutcomeGain = reader.IsDBNull(reader.GetOrdinal("OutcomeGain"))
                    ? null
                    : reader.GetDouble(reader.GetOrdinal("OutcomeGain")),
                Success = reader.GetInt32(reader.GetOrdinal("Success")) != 0
            };
        }

        private static DateTime? ReadNullableDateTime(SqliteDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal)
                ? null
                : DateTime.Parse(reader.GetString(ordinal), null, DateTimeStyles.RoundtripKind);
        }
    }

    // ── Facade ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Facade over <see cref="ISmartCoachMemoryRepository"/> providing the three
    /// persistence methods plus pure compute helpers that derive adherence/success
    /// statistics from already-fetched history.
    ///
    /// The compute helpers are PURE (no I/O) so they are directly unit-testable
    /// without a database — pass the list returned by GetAdviceHistoryAsync.
    ///
    /// This is a DESCRIPTIVE memory layer only — it never overrides health/safety gates.
    /// </summary>
    public sealed class SmartCoachMemoryStore
    {
        private readonly ISmartCoachMemoryRepository _repository;

        public SmartCoachMemoryStore(ISmartCoachMemoryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        // ── Persistence pass-through ──────────────────────────────────────────────

        /// <summary>Saves a new advice entry (upsert semantics).</summary>
        public Task SaveAdviceAsync(SmartCoachAdviceEntry entry, CancellationToken cancellationToken = default)
            => _repository.SaveAdviceAsync(entry, cancellationToken);

        /// <summary>Returns all advice for <paramref name="userId"/> in the half-open interval [from, to).</summary>
        public Task<IReadOnlyList<SmartCoachAdviceEntry>> GetAdviceHistoryAsync(
            int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
            => _repository.GetAdviceHistoryAsync(userId, from, to, cancellationToken);

        /// <summary>Records outcome fields on an existing advice entry.</summary>
        public Task RecordOutcomeAsync(
            Guid adviceId,
            DateTime? started,
            DateTime? completed,
            bool followed,
            double? outcomeGain,
            bool success,
            CancellationToken cancellationToken = default)
            => _repository.RecordOutcomeAsync(adviceId, started, completed, followed, outcomeGain, success, cancellationToken);

        // ── Pure compute helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Adherence rate: share of entries in <paramref name="history"/> where the user
        /// followed the advice (UserFollowedAdvice == true). Returns 0 for an empty list.
        /// </summary>
        public static double ComputeAdherenceRate(IReadOnlyList<SmartCoachAdviceEntry> history)
        {
            if (history == null || history.Count == 0) return 0.0;
            var followed = history.Count(e => e.UserFollowedAdvice);
            return (double)followed / history.Count * 100.0;
        }

        /// <summary>
        /// Recommendation success rate: share of FOLLOWED advice entries where
        /// <see cref="SmartCoachAdviceEntry.Success"/> is true.
        /// Entries the user did not follow are excluded (they carry no observable outcome).
        /// Returns 0 when no advice was followed.
        /// </summary>
        public static double ComputeRecommendationSuccessRate(IReadOnlyList<SmartCoachAdviceEntry> history)
        {
            if (history == null || history.Count == 0) return 0.0;
            var followed = history.Where(e => e.UserFollowedAdvice).ToList();
            if (followed.Count == 0) return 0.0;
            var successful = followed.Count(e => e.Success);
            return (double)successful / followed.Count * 100.0;
        }

        /// <summary>
        /// Returns the most recently recommended advice entry in
        /// <paramref name="history"/>, or null if the list is empty.
        /// </summary>
        public static SmartCoachAdviceEntry? GetLatestAdvice(IReadOnlyList<SmartCoachAdviceEntry> history)
        {
            if (history == null || history.Count == 0) return null;
            return history.OrderByDescending(e => e.RecommendedAt).First();
        }
    }
}
