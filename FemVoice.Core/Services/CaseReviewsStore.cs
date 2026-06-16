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
    /// Persistence contract for case review records (W0-A8).
    /// Upsert semantics: saving a review with the same <c>ReviewId</c> updates all fields.
    /// This supports the Draft → Completed lifecycle transition without a separate UPDATE
    /// method.
    /// </summary>
    public interface ICaseReviewsRepository
    {
        /// <summary>
        /// Persists a new review or updates an existing one (upsert on ReviewId).
        /// </summary>
        Task SaveAsync(CaseReview review, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all reviews for <paramref name="userId"/>, ordered by
        /// <see cref="CaseReview.PeriodStart"/> ascending.
        /// </summary>
        Task<IReadOnlyList<CaseReview>> GetByUserAsync(
            int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all reviews for <paramref name="userId"/> with the given
        /// <paramref name="reviewType"/>, ordered by <see cref="CaseReview.PeriodStart"/>
        /// ascending.
        /// </summary>
        Task<IReadOnlyList<CaseReview>> GetByUserAndTypeAsync(
            int userId, ReviewType reviewType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a single review by its <paramref name="reviewId"/>, or null if not found.
        /// </summary>
        Task<CaseReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default);
    }

    // ── In-memory repository ──────────────────────────────────────────────────────

    /// <summary>
    /// In-memory implementation of <see cref="ICaseReviewsRepository"/>.
    /// Thread-safe via a plain lock. Intended for unit tests and temporary dev flows.
    /// </summary>
    public sealed class InMemoryCaseReviewsRepository : ICaseReviewsRepository
    {
        private readonly object _sync = new();
        private readonly Dictionary<Guid, CaseReview> _store = new();

        public Task SaveAsync(CaseReview review, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _store[review.ReviewId] = review;
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CaseReview>> GetByUserAsync(
            int userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                var result = _store.Values
                    .Where(r => r.UserId == userId)
                    .OrderBy(r => r.PeriodStart)
                    .ToList();
                return Task.FromResult<IReadOnlyList<CaseReview>>(result);
            }
        }

        public Task<IReadOnlyList<CaseReview>> GetByUserAndTypeAsync(
            int userId, ReviewType reviewType, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                var result = _store.Values
                    .Where(r => r.UserId == userId && r.ReviewType == reviewType)
                    .OrderBy(r => r.PeriodStart)
                    .ToList();
                return Task.FromResult<IReadOnlyList<CaseReview>>(result);
            }
        }

        public Task<CaseReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _store.TryGetValue(reviewId, out var review);
                return Task.FromResult(review);
            }
        }
    }

    // ── SQLite repository ─────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite-backed implementation of <see cref="ICaseReviewsRepository"/>.
    /// Persists in the <c>CaseReviews</c> table inside the shared femvoice.db.
    ///
    /// Schema is created idempotently in the constructor via <c>EnsureSchema()</c> +
    /// PRAGMA table_info ALTER migration — the same pattern as
    /// <see cref="SqliteSmartCoachMemoryRepository"/>. NEVER SELECT * — always use the
    /// explicit <see cref="ReviewColumns"/> constant. DateTime via .ToString("o") +
    /// DateTimeStyles.RoundtripKind. Guid via .ToString("D")/Guid.Parse.
    /// bool as int 0/1. nullable → DBNull.Value.
    /// </summary>
    public sealed class SqliteCaseReviewsRepository : ICaseReviewsRepository
    {
        // Explicit column list for SELECT — positional reads survive ALTER-added columns.
        private const string ReviewColumns =
            "ReviewId, UserId, ReviewType, PeriodStart, PeriodEnd, " +
            "OutcomeSnapshotJson, Status, CreatedAt, CompletedAt";

        private readonly string _connectionString;

        public SqliteCaseReviewsRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            EnsureSchema();
        }

        public async Task SaveAsync(CaseReview review, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = @"
                INSERT INTO CaseReviews (
                    ReviewId, UserId, ReviewType, PeriodStart, PeriodEnd,
                    OutcomeSnapshotJson, Status, CreatedAt, CompletedAt)
                VALUES (
                    @ReviewId, @UserId, @ReviewType, @PeriodStart, @PeriodEnd,
                    @OutcomeSnapshotJson, @Status, @CreatedAt, @CompletedAt)
                ON CONFLICT(ReviewId) DO UPDATE SET
                    UserId              = excluded.UserId,
                    ReviewType          = excluded.ReviewType,
                    PeriodStart         = excluded.PeriodStart,
                    PeriodEnd           = excluded.PeriodEnd,
                    OutcomeSnapshotJson = excluded.OutcomeSnapshotJson,
                    Status              = excluded.Status,
                    CreatedAt           = excluded.CreatedAt,
                    CompletedAt         = excluded.CompletedAt;";

            AddReviewParameters(command, review);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CaseReview>> GetByUserAsync(
            int userId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {ReviewColumns} FROM CaseReviews
                WHERE UserId = @UserId
                ORDER BY PeriodStart;";
            command.Parameters.AddWithValue("@UserId", userId);
            return await ReadReviewsAsync(command, cancellationToken);
        }

        public async Task<IReadOnlyList<CaseReview>> GetByUserAndTypeAsync(
            int userId, ReviewType reviewType, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {ReviewColumns} FROM CaseReviews
                WHERE UserId = @UserId AND ReviewType = @ReviewType
                ORDER BY PeriodStart;";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@ReviewType", reviewType.ToString());
            return await ReadReviewsAsync(command, cancellationToken);
        }

        public async Task<CaseReview?> GetByIdAsync(
            Guid reviewId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {ReviewColumns} FROM CaseReviews
                WHERE ReviewId = @ReviewId;";
            command.Parameters.AddWithValue("@ReviewId", reviewId.ToString("D"));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                return MapReview(reader);
            return null;
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
                CREATE TABLE IF NOT EXISTS CaseReviews (
                    ReviewId            TEXT PRIMARY KEY,
                    UserId              INTEGER NOT NULL,
                    ReviewType          TEXT NOT NULL,
                    PeriodStart         TEXT NOT NULL,
                    PeriodEnd           TEXT NOT NULL,
                    OutcomeSnapshotJson TEXT NOT NULL DEFAULT '',
                    Status              TEXT NOT NULL DEFAULT 'Draft',
                    CreatedAt           TEXT NOT NULL,
                    CompletedAt         TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_casereviews_user_type_start
                    ON CaseReviews(UserId, ReviewType, PeriodStart);";
            command.ExecuteNonQuery();

            // Idempotent ALTER migration — adds any missing columns to pre-existing tables.
            EnsureReviewColumns(connection, transaction);

            transaction.Commit();
        }

        /// <summary>
        /// Idempotent ALTER migration: adds any of the expected columns that a pre-existing
        /// CaseReviews table is missing. Safe to run repeatedly — PRAGMA table_info guards
        /// each ALTER so re-running EnsureSchema never throws "duplicate column".
        /// </summary>
        private static void EnsureReviewColumns(SqliteConnection connection, SqliteTransaction transaction)
        {
            var existing = ReadColumnNames(connection, transaction, "CaseReviews");

            // (column name, DDL). Order matches ReviewColumns constant.
            (string Name, string Ddl)[] expectedColumns =
            {
                ("ReviewId",            "TEXT PRIMARY KEY"),
                ("UserId",              "INTEGER NOT NULL"),
                ("ReviewType",          "TEXT NOT NULL"),
                ("PeriodStart",         "TEXT NOT NULL"),
                ("PeriodEnd",           "TEXT NOT NULL"),
                ("OutcomeSnapshotJson", "TEXT NOT NULL DEFAULT ''"),
                ("Status",              "TEXT NOT NULL DEFAULT 'Draft'"),
                ("CreatedAt",           "TEXT NOT NULL"),
                ("CompletedAt",         "TEXT NULL"),
            };

            foreach (var (name, ddl) in expectedColumns)
            {
                // PRIMARY KEY columns cannot be added with ALTER TABLE — skip them if missing
                // (they must have been created by the CREATE TABLE IF NOT EXISTS above).
                if (name == "ReviewId") continue;
                if (existing.Contains(name)) continue;

                using var alter = connection.CreateCommand();
                alter.Transaction = transaction;
                // Column names are compile-time constants (never user input) → safe.
                alter.CommandText = $"ALTER TABLE CaseReviews ADD COLUMN {name} {ddl};";
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

        private static async Task<IReadOnlyList<CaseReview>> ReadReviewsAsync(
            SqliteCommand command, CancellationToken cancellationToken)
        {
            var reviews = new List<CaseReview>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                reviews.Add(MapReview(reader));
            }
            return reviews;
        }

        private static void AddReviewParameters(SqliteCommand command, CaseReview review)
        {
            command.Parameters.AddWithValue("@ReviewId", review.ReviewId.ToString("D"));
            command.Parameters.AddWithValue("@UserId", review.UserId);
            command.Parameters.AddWithValue("@ReviewType", review.ReviewType.ToString());
            command.Parameters.AddWithValue("@PeriodStart", review.PeriodStart.ToString("o"));
            command.Parameters.AddWithValue("@PeriodEnd", review.PeriodEnd.ToString("o"));
            command.Parameters.AddWithValue("@OutcomeSnapshotJson", review.OutcomeSnapshotJson);
            command.Parameters.AddWithValue("@Status", review.Status.ToString());
            command.Parameters.AddWithValue("@CreatedAt", review.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("@CompletedAt",
                review.CompletedAt.HasValue ? (object)review.CompletedAt.Value.ToString("o") : DBNull.Value);
        }

        private static CaseReview MapReview(SqliteDataReader reader)
        {
            return new CaseReview
            {
                ReviewId = Guid.Parse(reader.GetString(reader.GetOrdinal("ReviewId"))),
                UserId   = reader.GetInt32(reader.GetOrdinal("UserId")),
                ReviewType = Enum.Parse<ReviewType>(reader.GetString(reader.GetOrdinal("ReviewType"))),
                PeriodStart = DateTime.Parse(
                    reader.GetString(reader.GetOrdinal("PeriodStart")),
                    null, DateTimeStyles.RoundtripKind),
                PeriodEnd = DateTime.Parse(
                    reader.GetString(reader.GetOrdinal("PeriodEnd")),
                    null, DateTimeStyles.RoundtripKind),
                OutcomeSnapshotJson = reader.GetString(reader.GetOrdinal("OutcomeSnapshotJson")),
                Status = Enum.Parse<ReviewStatus>(reader.GetString(reader.GetOrdinal("Status"))),
                CreatedAt = DateTime.Parse(
                    reader.GetString(reader.GetOrdinal("CreatedAt")),
                    null, DateTimeStyles.RoundtripKind),
                CompletedAt = ReadNullableDateTime(reader, "CompletedAt"),
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
    /// Facade over <see cref="ICaseReviewsRepository"/> providing the four persistence
    /// methods and a convenience <see cref="CompleteAsync"/> helper for the Draft →
    /// Completed lifecycle transition.
    ///
    /// This is a DESCRIPTIVE layer only — it never overrides health/safety gates.
    /// </summary>
    public sealed class CaseReviewsStore
    {
        private readonly ICaseReviewsRepository _repository;

        public CaseReviewsStore(ICaseReviewsRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        // ── Persistence pass-through ──────────────────────────────────────────────

        /// <summary>Saves a new review or updates an existing one (upsert on ReviewId).</summary>
        public Task SaveAsync(CaseReview review, CancellationToken cancellationToken = default)
            => _repository.SaveAsync(review, cancellationToken);

        /// <summary>Returns all reviews for <paramref name="userId"/>, ordered by PeriodStart.</summary>
        public Task<IReadOnlyList<CaseReview>> GetByUserAsync(
            int userId, CancellationToken cancellationToken = default)
            => _repository.GetByUserAsync(userId, cancellationToken);

        /// <summary>
        /// Returns all reviews for <paramref name="userId"/> with the given
        /// <paramref name="reviewType"/>, ordered by PeriodStart.
        /// </summary>
        public Task<IReadOnlyList<CaseReview>> GetByUserAndTypeAsync(
            int userId, ReviewType reviewType, CancellationToken cancellationToken = default)
            => _repository.GetByUserAndTypeAsync(userId, reviewType, cancellationToken);

        /// <summary>
        /// Returns a single review by its <paramref name="reviewId"/>, or null if not found.
        /// </summary>
        public Task<CaseReview?> GetByIdAsync(
            Guid reviewId, CancellationToken cancellationToken = default)
            => _repository.GetByIdAsync(reviewId, cancellationToken);

        // ── Lifecycle helper ──────────────────────────────────────────────────────

        /// <summary>
        /// Transitions an existing <see cref="ReviewStatus.Draft"/> review to
        /// <see cref="ReviewStatus.Completed"/>, stamping <paramref name="completedAt"/>
        /// as the completion time.
        ///
        /// Returns null and performs no write when <paramref name="reviewId"/> is not found.
        /// Returns the review unchanged when it is already <see cref="ReviewStatus.Completed"/>.
        /// </summary>
        public async Task<CaseReview?> CompleteAsync(
            Guid reviewId,
            DateTime completedAt,
            CancellationToken cancellationToken = default)
        {
            var existing = await _repository.GetByIdAsync(reviewId, cancellationToken);
            if (existing is null) return null;
            if (existing.Status == ReviewStatus.Completed) return existing;

            var completed = existing with
            {
                Status      = ReviewStatus.Completed,
                CompletedAt = completedAt,
            };
            await _repository.SaveAsync(completed, cancellationToken);
            return completed;
        }
    }
}
