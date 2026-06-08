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
    /// Persistence contract for the immutable audit trail (W0-A11).
    ///
    /// <para>
    /// The audit log is <b>strictly append-only</b>: only INSERT is permitted.
    /// There is intentionally no update or delete method, and the schema has no
    /// UNIQUE constraint on AuditId so that even a duplicate append produces two
    /// rows rather than silently updating an existing entry.
    /// </para>
    /// </summary>
    public interface IAuditTrailRepository
    {
        /// <summary>
        /// Appends <paramref name="auditEvent"/> to the audit log.
        /// This is a pure INSERT — no upsert, no conflict resolution.
        /// </summary>
        Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all audit events that match the supplied filters, ordered by
        /// <see cref="AuditEvent.OccurredAt"/> ascending.
        /// </summary>
        /// <param name="userId">Required: only events for this user are returned.</param>
        /// <param name="entityType">Optional entity-type filter.</param>
        /// <param name="from">Optional inclusive lower bound on <see cref="AuditEvent.OccurredAt"/>.</param>
        /// <param name="to">Optional exclusive upper bound on <see cref="AuditEvent.OccurredAt"/>.</param>
        Task<IReadOnlyList<AuditEvent>> QueryAsync(
            int userId,
            AuditEntityType? entityType = null,
            DateTime? from = null,
            DateTime? to = null,
            CancellationToken cancellationToken = default);
    }

    // ── In-memory repository ──────────────────────────────────────────────────────

    /// <summary>
    /// In-memory implementation of <see cref="IAuditTrailRepository"/>.
    /// Thread-safe via a plain lock. Intended for unit tests and temporary dev flows.
    ///
    /// Append-only semantics are preserved: every call to
    /// <see cref="AppendAsync"/> adds a new entry even when the same AuditId is used.
    /// </summary>
    public sealed class InMemoryAuditTrailRepository : IAuditTrailRepository
    {
        private readonly object _sync = new();
        private readonly List<AuditEvent> _entries = new();

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _entries.Add(auditEvent);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> QueryAsync(
            int userId,
            AuditEntityType? entityType = null,
            DateTime? from = null,
            DateTime? to = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                var result = _entries
                    .Where(e => e.UserId == userId)
                    .Where(e => entityType == null || e.EntityType == entityType)
                    .Where(e => from == null || e.OccurredAt >= from.Value)
                    .Where(e => to == null || e.OccurredAt < to.Value)
                    .OrderBy(e => e.OccurredAt)
                    .ToList();
                return Task.FromResult<IReadOnlyList<AuditEvent>>(result);
            }
        }
    }

    // ── SQLite repository ─────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite-backed implementation of <see cref="IAuditTrailRepository"/>.
    /// Persists in the <c>AuditTrail</c> table inside the shared femvoice.db.
    ///
    /// Schema is created idempotently in the constructor via <c>EnsureSchema()</c> +
    /// PRAGMA table_info ALTER migration — the same pattern as
    /// <see cref="SqliteSmartCoachMemoryRepository"/>. NEVER SELECT * — always use
    /// the explicit <see cref="AuditColumns"/> constant. DateTime via .ToString("o")
    /// + DateTimeStyles.RoundtripKind.
    ///
    /// <para>
    /// JOURNAL MODE: WAL is enabled at schema time so concurrent readers are not
    /// blocked by a single writer, which is important for an append-only log.
    /// </para>
    ///
    /// <para>
    /// APPEND-ONLY GUARANTEE: the INSERT statement has no ON CONFLICT clause and
    /// the table has no UNIQUE constraint on AuditId, so duplicate AuditIds produce
    /// two rows. There is intentionally no UPDATE or DELETE path anywhere in this class.
    /// </para>
    /// </summary>
    public sealed class SqliteAuditTrailRepository : IAuditTrailRepository
    {
        // Explicit column list for SELECT — positional reads survive ALTER-added columns.
        private const string AuditColumns =
            "AuditId, UserId, OccurredAt, EntityType, EntityId, " +
            "ActorRole, ReasonCode, BeforeJson, AfterJson";

        private readonly string _connectionString;

        public SqliteAuditTrailRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            EnsureSchema();
        }

        public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            // Plain INSERT — no ON CONFLICT, no UNIQUE constraint on AuditId.
            // A duplicate AuditId produces two rows (append-only guarantee).
            command.CommandText = @"
                INSERT INTO AuditTrail (
                    AuditId, UserId, OccurredAt, EntityType, EntityId,
                    ActorRole, ReasonCode, BeforeJson, AfterJson)
                VALUES (
                    @AuditId, @UserId, @OccurredAt, @EntityType, @EntityId,
                    @ActorRole, @ReasonCode, @BeforeJson, @AfterJson);";

            AddAuditParameters(command, auditEvent);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AuditEvent>> QueryAsync(
            int userId,
            AuditEntityType? entityType = null,
            DateTime? from = null,
            DateTime? to = null,
            CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            var sql = $"SELECT {AuditColumns} FROM AuditTrail WHERE UserId = @UserId";
            command.Parameters.AddWithValue("@UserId", userId);

            if (entityType.HasValue)
            {
                sql += " AND EntityType = @EntityType";
                command.Parameters.AddWithValue("@EntityType", entityType.Value.ToString());
            }
            if (from.HasValue)
            {
                sql += " AND OccurredAt >= @From";
                command.Parameters.AddWithValue("@From", from.Value.ToString("o"));
            }
            if (to.HasValue)
            {
                sql += " AND OccurredAt < @To";
                command.Parameters.AddWithValue("@To", to.Value.ToString("o"));
            }
            sql += " ORDER BY OccurredAt;";
            command.CommandText = sql;

            var events = new List<AuditEvent>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(MapEvent(reader));
            }
            return events;
        }

        // ── Schema ────────────────────────────────────────────────────────────────

        private void EnsureSchema()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Enable WAL mode before creating tables so all subsequent writes
            // use WAL from the first transaction.
            using (var walCommand = connection.CreateCommand())
            {
                walCommand.CommandText = "PRAGMA journal_mode=WAL;";
                walCommand.ExecuteNonQuery();
            }

            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            // No UNIQUE constraint on AuditId — append-only, duplicate AuditIds allowed.
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS AuditTrail (
                    RowId   INTEGER PRIMARY KEY AUTOINCREMENT,
                    AuditId TEXT NOT NULL,
                    UserId  INTEGER NOT NULL,
                    OccurredAt TEXT NOT NULL,
                    EntityType TEXT NOT NULL,
                    EntityId TEXT NOT NULL,
                    ActorRole TEXT NOT NULL,
                    ReasonCode TEXT NOT NULL,
                    BeforeJson TEXT NULL,
                    AfterJson  TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_audit_user_occurred
                    ON AuditTrail(UserId, OccurredAt);

                CREATE INDEX IF NOT EXISTS idx_audit_entity
                    ON AuditTrail(EntityType, EntityId);";
            command.ExecuteNonQuery();

            // Idempotent ALTER migration — adds any missing columns to pre-existing tables.
            EnsureAuditColumns(connection, transaction);

            transaction.Commit();
        }

        /// <summary>
        /// Idempotent ALTER migration: adds any of the expected columns that a pre-existing
        /// AuditTrail table is missing. Safe to run repeatedly — PRAGMA table_info
        /// guards each ALTER so re-running EnsureSchema never throws "duplicate column".
        /// </summary>
        private static void EnsureAuditColumns(SqliteConnection connection, SqliteTransaction transaction)
        {
            var existing = ReadColumnNames(connection, transaction, "AuditTrail");

            // (column name, DDL). Order matches AuditColumns constant.
            (string Name, string Ddl)[] expectedColumns =
            {
                ("RowId",      "INTEGER PRIMARY KEY AUTOINCREMENT"),
                ("AuditId",    "TEXT NOT NULL"),
                ("UserId",     "INTEGER NOT NULL"),
                ("OccurredAt", "TEXT NOT NULL"),
                ("EntityType", "TEXT NOT NULL"),
                ("EntityId",   "TEXT NOT NULL"),
                ("ActorRole",  "TEXT NOT NULL"),
                ("ReasonCode", "TEXT NOT NULL"),
                ("BeforeJson", "TEXT NULL"),
                ("AfterJson",  "TEXT NULL"),
            };

            foreach (var (name, ddl) in expectedColumns)
            {
                // PRIMARY KEY columns cannot be added with ALTER TABLE — skip them if missing
                // (they must have been created by the CREATE TABLE IF NOT EXISTS above).
                if (name == "RowId") continue;
                if (existing.Contains(name)) continue;

                using var alter = connection.CreateCommand();
                alter.Transaction = transaction;
                // Column names are compile-time constants (never user input) → safe.
                alter.CommandText = $"ALTER TABLE AuditTrail ADD COLUMN {name} {ddl};";
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

        private static void AddAuditParameters(SqliteCommand command, AuditEvent auditEvent)
        {
            command.Parameters.AddWithValue("@AuditId", auditEvent.AuditId.ToString("D"));
            command.Parameters.AddWithValue("@UserId", auditEvent.UserId);
            command.Parameters.AddWithValue("@OccurredAt", auditEvent.OccurredAt.ToString("o"));
            command.Parameters.AddWithValue("@EntityType", auditEvent.EntityType.ToString());
            command.Parameters.AddWithValue("@EntityId", auditEvent.EntityId);
            command.Parameters.AddWithValue("@ActorRole", auditEvent.ActorRole);
            command.Parameters.AddWithValue("@ReasonCode", auditEvent.ReasonCode);
            command.Parameters.AddWithValue("@BeforeJson",
                auditEvent.BeforeJson != null ? (object)auditEvent.BeforeJson : DBNull.Value);
            command.Parameters.AddWithValue("@AfterJson",
                auditEvent.AfterJson != null ? (object)auditEvent.AfterJson : DBNull.Value);
        }

        private static AuditEvent MapEvent(SqliteDataReader reader)
        {
            return new AuditEvent
            {
                AuditId = Guid.Parse(reader.GetString(reader.GetOrdinal("AuditId"))),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                OccurredAt = DateTime.Parse(
                    reader.GetString(reader.GetOrdinal("OccurredAt")),
                    null, DateTimeStyles.RoundtripKind),
                EntityType = Enum.Parse<AuditEntityType>(
                    reader.GetString(reader.GetOrdinal("EntityType"))),
                EntityId = reader.GetString(reader.GetOrdinal("EntityId")),
                ActorRole = reader.GetString(reader.GetOrdinal("ActorRole")),
                ReasonCode = reader.GetString(reader.GetOrdinal("ReasonCode")),
                BeforeJson = reader.IsDBNull(reader.GetOrdinal("BeforeJson"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("BeforeJson")),
                AfterJson = reader.IsDBNull(reader.GetOrdinal("AfterJson"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("AfterJson")),
            };
        }
    }

    // ── Facade ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Facade over <see cref="IAuditTrailRepository"/> providing append and query
    /// access to the immutable audit trail.
    ///
    /// <para>
    /// All writes go through <see cref="AppendAsync"/> — there is intentionally no
    /// update or delete surface. The underlying repository enforces append-only
    /// semantics at the persistence level.
    /// </para>
    ///
    /// <para>
    /// This is an AUDIT/TRACEABILITY layer only — it never overrides any safety,
    /// health, or recovery gate.
    /// </para>
    /// </summary>
    public sealed class AuditTrailStore
    {
        private readonly IAuditTrailRepository _repository;

        public AuditTrailStore(IAuditTrailRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Appends <paramref name="auditEvent"/> to the audit log.
        /// Every call produces a new row — there is no upsert or update path.
        /// </summary>
        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
            => _repository.AppendAsync(auditEvent, cancellationToken);

        /// <summary>
        /// Returns all audit events for <paramref name="userId"/> that satisfy the
        /// optional filters, ordered by <see cref="AuditEvent.OccurredAt"/> ascending.
        /// </summary>
        /// <param name="userId">Required: only events for this user are returned.</param>
        /// <param name="entityType">Optional entity-type filter.</param>
        /// <param name="from">Optional inclusive lower bound on OccurredAt.</param>
        /// <param name="to">Optional exclusive upper bound on OccurredAt.</param>
        public Task<IReadOnlyList<AuditEvent>> QueryAsync(
            int userId,
            AuditEntityType? entityType = null,
            DateTime? from = null,
            DateTime? to = null,
            CancellationToken cancellationToken = default)
            => _repository.QueryAsync(userId, entityType, from, to, cancellationToken);
    }
}
