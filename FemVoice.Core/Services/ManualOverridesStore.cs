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
    /// <summary>
    /// A single APPEND-ONLY manual-override log row (Sprint E, Agent 7). One row is
    /// written per evaluated override; rows are never updated or deleted — the log is the
    /// audit trail of every professional override request and its clamped outcome.
    /// </summary>
    public sealed record ManualOverrideLogEntry
    {
        /// <summary>Unique identifier for this log row.</summary>
        public Guid ManualOverrideId { get; init; } = Guid.NewGuid();

        /// <summary>Identifier of the audit event paired with this override.</summary>
        public Guid AuditId { get; init; } = Guid.NewGuid();

        /// <summary>The user the override applied to.</summary>
        public int UserId { get; init; }

        /// <summary>Which clinical decision was overridden.</summary>
        public ManualOverrideKind OverrideKind { get; init; }

        /// <summary>The catalog exercise targeted (1–15). Null for non-exercise overrides.</summary>
        public int? ExerciseId { get; init; }

        /// <summary>When the override was requested.</summary>
        public DateTime RequestedAt { get; init; }

        /// <summary>Role of the actor that requested the override.</summary>
        public string ActorRole { get; init; } = string.Empty;

        /// <summary>Machine reason code for the override.</summary>
        public string ReasonCode { get; init; } = string.Empty;

        /// <summary>True when the override produced a persisted, clamped profile.</summary>
        public bool WasApplied { get; init; }

        /// <summary>True when the two-stage clamp made the applied profile more
        /// conservative than the professional's intent.</summary>
        public bool WasClamped { get; init; }

        /// <summary>Reason code when the override was not applied as requested; null when
        /// applied.</summary>
        public string? BlockedReasonCode { get; init; }
    }

    // ── Repository interface ──────────────────────────────────────────────────────

    /// <summary>
    /// APPEND-ONLY persistence contract for manual-override log rows (Agent 7).
    /// <see cref="AppendAsync"/> performs a plain INSERT with NO upsert/ON CONFLICT — the
    /// log is immutable, so each override is recorded exactly once.
    /// </summary>
    public interface IManualOverridesRepository
    {
        /// <summary>Appends a new override log row. INSERT-only; never updates.</summary>
        Task AppendAsync(ManualOverrideLogEntry entry, CancellationToken cancellationToken = default);

        /// <summary>Returns all override log rows for <paramref name="userId"/> in [from, to),
        /// ordered by RequestedAt.</summary>
        Task<IReadOnlyList<ManualOverrideLogEntry>> GetOverridesAsync(
            int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    }

    // ── In-memory repository ──────────────────────────────────────────────────────

    /// <summary>
    /// In-memory implementation of <see cref="IManualOverridesRepository"/>. Thread-safe
    /// via a plain lock. APPEND-ONLY — entries are only ever added. Intended for unit
    /// tests and temporary dev flows.
    /// </summary>
    public sealed class InMemoryManualOverridesRepository : IManualOverridesRepository
    {
        private readonly object _sync = new();
        private readonly List<ManualOverrideLogEntry> _entries = new();

        public Task AppendAsync(ManualOverrideLogEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _entries.Add(entry);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ManualOverrideLogEntry>> GetOverridesAsync(
            int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                var result = _entries
                    .Where(e => e.UserId == userId && e.RequestedAt >= from && e.RequestedAt < to)
                    .OrderBy(e => e.RequestedAt)
                    .ToList();
                return Task.FromResult<IReadOnlyList<ManualOverrideLogEntry>>(result);
            }
        }
    }

    // ── SQLite repository ─────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite-backed implementation of <see cref="IManualOverridesRepository"/>.
    /// Persists in the APPEND-ONLY <c>ManualOverrideLog</c> table inside the shared
    /// femvoice.db.
    ///
    /// Schema is created idempotently in the constructor via <c>EnsureSchema()</c> +
    /// PRAGMA table_info ALTER migration — the same pattern as
    /// <see cref="SqliteSmartCoachMemoryRepository"/>. NEVER SELECT * — always use the
    /// explicit <see cref="OverrideColumns"/> constant. DateTime via .ToString("o") +
    /// DateTimeStyles.RoundtripKind; Guid via .ToString("D") / Guid.Parse; bool as int
    /// 0/1; nullable → DBNull.Value. <see cref="AppendAsync"/> is a plain INSERT (no
    /// ON CONFLICT) — the log is immutable.
    /// </summary>
    public sealed class SqliteManualOverridesRepository : IManualOverridesRepository
    {
        // Explicit column list for SELECT — positional reads survive ALTER-added columns.
        private const string OverrideColumns =
            "ManualOverrideId, AuditId, UserId, OverrideKind, ExerciseId, " +
            "RequestedAt, ActorRole, ReasonCode, WasApplied, WasClamped, BlockedReasonCode";

        private readonly string _connectionString;

        public SqliteManualOverridesRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            EnsureSchema();
        }

        public async Task AppendAsync(ManualOverrideLogEntry entry, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            // APPEND-ONLY: plain INSERT, no ON CONFLICT — the log is immutable.
            command.CommandText = @"
                INSERT INTO ManualOverrideLog (
                    ManualOverrideId, AuditId, UserId, OverrideKind, ExerciseId,
                    RequestedAt, ActorRole, ReasonCode, WasApplied, WasClamped, BlockedReasonCode)
                VALUES (
                    @ManualOverrideId, @AuditId, @UserId, @OverrideKind, @ExerciseId,
                    @RequestedAt, @ActorRole, @ReasonCode, @WasApplied, @WasClamped, @BlockedReasonCode);";

            AddOverrideParameters(command, entry);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ManualOverrideLogEntry>> GetOverridesAsync(
            int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {OverrideColumns} FROM ManualOverrideLog
                WHERE UserId = @UserId AND RequestedAt >= @From AND RequestedAt < @To
                ORDER BY RequestedAt;";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@From", from.ToString("o"));
            command.Parameters.AddWithValue("@To", to.ToString("o"));

            var entries = new List<ManualOverrideLogEntry>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(MapEntry(reader));
            }
            return entries;
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
                CREATE TABLE IF NOT EXISTS ManualOverrideLog (
                    ManualOverrideId TEXT PRIMARY KEY,
                    AuditId TEXT NOT NULL,
                    UserId INTEGER NOT NULL,
                    OverrideKind TEXT NOT NULL,
                    ExerciseId INTEGER NULL,
                    RequestedAt TEXT NOT NULL,
                    ActorRole TEXT,
                    ReasonCode TEXT,
                    WasApplied INTEGER NOT NULL DEFAULT 0,
                    WasClamped INTEGER NOT NULL DEFAULT 0,
                    BlockedReasonCode TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_manual_override_user_requested
                    ON ManualOverrideLog(UserId, RequestedAt);";
            command.ExecuteNonQuery();

            // Idempotent ALTER migration — adds any missing columns to pre-existing tables.
            EnsureOverrideColumns(connection, transaction);

            transaction.Commit();
        }

        /// <summary>
        /// Idempotent ALTER migration: adds any of the expected columns that a pre-existing
        /// ManualOverrideLog table is missing. Safe to run repeatedly — PRAGMA table_info
        /// guards each ALTER so re-running EnsureSchema never throws "duplicate column".
        /// </summary>
        private static void EnsureOverrideColumns(SqliteConnection connection, SqliteTransaction transaction)
        {
            var existing = ReadColumnNames(connection, transaction, "ManualOverrideLog");

            // (column name, DDL). Order matches OverrideColumns constant.
            (string Name, string Ddl)[] expectedColumns =
            {
                ("ManualOverrideId",  "TEXT PRIMARY KEY"),
                ("AuditId",           "TEXT NOT NULL"),
                ("UserId",            "INTEGER NOT NULL"),
                ("OverrideKind",      "TEXT NOT NULL"),
                ("ExerciseId",        "INTEGER NULL"),
                ("RequestedAt",       "TEXT NOT NULL"),
                ("ActorRole",         "TEXT"),
                ("ReasonCode",        "TEXT"),
                ("WasApplied",        "INTEGER NOT NULL DEFAULT 0"),
                ("WasClamped",        "INTEGER NOT NULL DEFAULT 0"),
                ("BlockedReasonCode", "TEXT NULL"),
            };

            foreach (var (name, ddl) in expectedColumns)
            {
                // PRIMARY KEY columns cannot be added with ALTER TABLE — skip them if missing
                // (they must have been created by the CREATE TABLE IF NOT EXISTS above).
                if (name == "ManualOverrideId") continue;
                if (existing.Contains(name)) continue;

                using var alter = connection.CreateCommand();
                alter.Transaction = transaction;
                // Column names are compile-time constants (never user input) → safe.
                alter.CommandText = $"ALTER TABLE ManualOverrideLog ADD COLUMN {name} {ddl};";
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

        private static void AddOverrideParameters(SqliteCommand command, ManualOverrideLogEntry entry)
        {
            command.Parameters.AddWithValue("@ManualOverrideId", entry.ManualOverrideId.ToString("D"));
            command.Parameters.AddWithValue("@AuditId", entry.AuditId.ToString("D"));
            command.Parameters.AddWithValue("@UserId", entry.UserId);
            command.Parameters.AddWithValue("@OverrideKind", entry.OverrideKind.ToString());
            command.Parameters.AddWithValue("@ExerciseId",
                entry.ExerciseId.HasValue ? (object)entry.ExerciseId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@RequestedAt", entry.RequestedAt.ToString("o"));
            command.Parameters.AddWithValue("@ActorRole", (object?)entry.ActorRole ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReasonCode", (object?)entry.ReasonCode ?? DBNull.Value);
            command.Parameters.AddWithValue("@WasApplied", entry.WasApplied ? 1 : 0);
            command.Parameters.AddWithValue("@WasClamped", entry.WasClamped ? 1 : 0);
            command.Parameters.AddWithValue("@BlockedReasonCode",
                (object?)entry.BlockedReasonCode ?? DBNull.Value);
        }

        private static ManualOverrideLogEntry MapEntry(SqliteDataReader reader)
        {
            return new ManualOverrideLogEntry
            {
                ManualOverrideId = Guid.Parse(reader.GetString(reader.GetOrdinal("ManualOverrideId"))),
                AuditId = Guid.Parse(reader.GetString(reader.GetOrdinal("AuditId"))),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                OverrideKind = Enum.Parse<ManualOverrideKind>(
                    reader.GetString(reader.GetOrdinal("OverrideKind"))),
                ExerciseId = reader.IsDBNull(reader.GetOrdinal("ExerciseId"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("ExerciseId")),
                RequestedAt = DateTime.Parse(
                    reader.GetString(reader.GetOrdinal("RequestedAt")),
                    null, DateTimeStyles.RoundtripKind),
                ActorRole = reader.IsDBNull(reader.GetOrdinal("ActorRole"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("ActorRole")),
                ReasonCode = reader.IsDBNull(reader.GetOrdinal("ReasonCode"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("ReasonCode")),
                WasApplied = reader.GetInt32(reader.GetOrdinal("WasApplied")) != 0,
                WasClamped = reader.GetInt32(reader.GetOrdinal("WasClamped")) != 0,
                BlockedReasonCode = reader.IsDBNull(reader.GetOrdinal("BlockedReasonCode"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("BlockedReasonCode"))
            };
        }
    }

    // ── Facade ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Facade over <see cref="IManualOverridesRepository"/> providing the append + read
    /// methods for the APPEND-ONLY manual-override log. A convenience
    /// <see cref="LogResultAsync"/> maps a <see cref="ManualOverrideRequest"/> +
    /// <see cref="ManualOverrideResult"/> into a log row.
    ///
    /// This is a DESCRIPTIVE audit layer only — it never overrides health/safety gates.
    /// </summary>
    public sealed class ManualOverridesStore
    {
        private readonly IManualOverridesRepository _repository;

        public ManualOverridesStore(IManualOverridesRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>Appends a log row (INSERT-only).</summary>
        public Task AppendAsync(ManualOverrideLogEntry entry, CancellationToken cancellationToken = default)
            => _repository.AppendAsync(entry, cancellationToken);

        /// <summary>Returns all override log rows for <paramref name="userId"/> in [from, to).</summary>
        public Task<IReadOnlyList<ManualOverrideLogEntry>> GetOverridesAsync(
            int userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
            => _repository.GetOverridesAsync(userId, from, to, cancellationToken);

        /// <summary>
        /// Maps an evaluated override (<paramref name="request"/> + <paramref name="result"/>)
        /// into a log row and appends it. The row carries the result's AuditId /
        /// ManualOverrideId so the log and the returned result share identifiers.
        /// </summary>
        public Task LogResultAsync(
            ManualOverrideRequest request,
            ManualOverrideResult result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(result);

            var entry = new ManualOverrideLogEntry
            {
                ManualOverrideId = result.ManualOverrideId,
                AuditId = result.AuditId,
                UserId = request.UserId,
                OverrideKind = request.OverrideKind,
                ExerciseId = request.ExerciseId,
                RequestedAt = request.RequestedAt,
                ActorRole = request.ActorRole,
                ReasonCode = request.ReasonCode,
                WasApplied = result.WasApplied,
                WasClamped = result.WasClamped,
                BlockedReasonCode = result.BlockedReasonCode
            };
            return _repository.AppendAsync(entry, cancellationToken);
        }
    }
}
