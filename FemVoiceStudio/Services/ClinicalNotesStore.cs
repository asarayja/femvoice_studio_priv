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
    /// Persistence contract for clinical and coaching notes (W0-A6).
    ///
    /// Mutability policy (enforced by the repository, not callers):
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="ClinicalNoteType.Coach"/> and <see cref="ClinicalNoteType.Clinical"/>
    ///     use upsert semantics — saving an existing NoteId replaces it.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="ClinicalNoteType.Review"/> and <see cref="ClinicalNoteType.GoalReview"/>
    ///     are APPEND-ONLY — an attempt to save a note whose NoteId already exists in the
    ///     store is a no-op (the existing row is kept unchanged).
    ///   </description></item>
    /// </list>
    /// </summary>
    public interface IClinicalNotesRepository
    {
        /// <summary>
        /// Persists a note. For Coach/Clinical types: upsert on NoteId.
        /// For Review/GoalReview types: insert-if-not-exists (append-only guard).
        /// </summary>
        Task SaveNoteAsync(ClinicalNote note, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all notes for <paramref name="userId"/> whose <see cref="ClinicalNote.NoteType"/>
        /// matches <paramref name="noteType"/> and whose <see cref="ClinicalNote.CreatedAt"/>
        /// falls in [<paramref name="from"/>, <paramref name="to"/>).
        /// Results are ordered by <see cref="ClinicalNote.CreatedAt"/> ascending.
        /// </summary>
        Task<IReadOnlyList<ClinicalNote>> GetNotesAsync(
            int userId, ClinicalNoteType noteType, DateTime from, DateTime to,
            CancellationToken cancellationToken = default);
    }

    // ── In-memory repository ──────────────────────────────────────────────────────

    /// <summary>
    /// In-memory implementation of <see cref="IClinicalNotesRepository"/>.
    /// Thread-safe via a plain lock. Intended for unit tests and temporary dev flows.
    /// Enforces append-only semantics for Review/GoalReview at write time.
    /// </summary>
    public sealed class InMemoryClinicalNotesRepository : IClinicalNotesRepository
    {
        private readonly object _sync = new();
        private readonly Dictionary<Guid, ClinicalNote> _notes = new();

        private static bool IsAppendOnly(ClinicalNoteType t) =>
            t == ClinicalNoteType.Review || t == ClinicalNoteType.GoalReview;

        public Task SaveNoteAsync(ClinicalNote note, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (IsAppendOnly(note.NoteType) && _notes.ContainsKey(note.NoteId))
                    return Task.CompletedTask; // append-only guard: no-op on existing key
                _notes[note.NoteId] = note;
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ClinicalNote>> GetNotesAsync(
            int userId, ClinicalNoteType noteType, DateTime from, DateTime to,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                var result = _notes.Values
                    .Where(n => n.UserId == userId
                             && n.NoteType == noteType
                             && n.CreatedAt >= from
                             && n.CreatedAt < to)
                    .OrderBy(n => n.CreatedAt)
                    .ToList();
                return Task.FromResult<IReadOnlyList<ClinicalNote>>(result);
            }
        }
    }

    // ── SQLite repository ─────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite-backed implementation of <see cref="IClinicalNotesRepository"/>.
    /// Persists in the <c>ClinicalNotes</c> table inside the shared femvoice.db.
    ///
    /// Schema is created idempotently in the constructor via <c>EnsureSchema()</c> +
    /// PRAGMA table_info ALTER migration — the same pattern as
    /// <see cref="SqliteSmartCoachMemoryRepository"/>. NEVER SELECT * — always use
    /// the explicit <see cref="NoteColumns"/> constant. DateTime via .ToString("o")
    /// + DateTimeStyles.RoundtripKind.
    ///
    /// Append-only semantics for Review/GoalReview are enforced at the SQL level:
    /// those note types use INSERT OR IGNORE (no DO UPDATE clause) so existing rows
    /// are never overwritten. Coach/Clinical use INSERT … ON CONFLICT DO UPDATE SET.
    /// </summary>
    public sealed class SqliteClinicalNotesRepository : IClinicalNotesRepository
    {
        // Explicit column list for SELECT — positional reads survive ALTER-added columns.
        private const string NoteColumns =
            "NoteId, UserId, NoteType, AuthorRole, CreatedAt, BodyText, " +
            "LinkedEntityType, LinkedEntityId";

        private readonly string _connectionString;

        public SqliteClinicalNotesRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            EnsureSchema();
        }

        public async Task SaveNoteAsync(ClinicalNote note, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;

            if (IsAppendOnly(note.NoteType))
            {
                // Review / GoalReview: INSERT OR IGNORE — existing rows are never touched.
                command.CommandText = @"
                    INSERT OR IGNORE INTO ClinicalNotes (
                        NoteId, UserId, NoteType, AuthorRole, CreatedAt, BodyText,
                        LinkedEntityType, LinkedEntityId)
                    VALUES (
                        @NoteId, @UserId, @NoteType, @AuthorRole, @CreatedAt, @BodyText,
                        @LinkedEntityType, @LinkedEntityId);";
            }
            else
            {
                // Coach / Clinical: upsert — update all mutable fields on conflict.
                command.CommandText = @"
                    INSERT INTO ClinicalNotes (
                        NoteId, UserId, NoteType, AuthorRole, CreatedAt, BodyText,
                        LinkedEntityType, LinkedEntityId)
                    VALUES (
                        @NoteId, @UserId, @NoteType, @AuthorRole, @CreatedAt, @BodyText,
                        @LinkedEntityType, @LinkedEntityId)
                    ON CONFLICT(NoteId) DO UPDATE SET
                        UserId           = excluded.UserId,
                        NoteType         = excluded.NoteType,
                        AuthorRole       = excluded.AuthorRole,
                        CreatedAt        = excluded.CreatedAt,
                        BodyText         = excluded.BodyText,
                        LinkedEntityType = excluded.LinkedEntityType,
                        LinkedEntityId   = excluded.LinkedEntityId;";
            }

            AddNoteParameters(command, note);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ClinicalNote>> GetNotesAsync(
            int userId, ClinicalNoteType noteType, DateTime from, DateTime to,
            CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {NoteColumns} FROM ClinicalNotes
                WHERE UserId = @UserId AND NoteType = @NoteType
                  AND CreatedAt >= @From AND CreatedAt < @To
                ORDER BY CreatedAt;";
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@NoteType", (int)noteType);
            command.Parameters.AddWithValue("@From", from.ToString("o"));
            command.Parameters.AddWithValue("@To", to.ToString("o"));

            var notes = new List<ClinicalNote>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                notes.Add(MapNote(reader));
            }
            return notes;
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
                CREATE TABLE IF NOT EXISTS ClinicalNotes (
                    NoteId           TEXT PRIMARY KEY,
                    UserId           INTEGER NOT NULL,
                    NoteType         INTEGER NOT NULL,
                    AuthorRole       TEXT NOT NULL,
                    CreatedAt        TEXT NOT NULL,
                    BodyText         TEXT NOT NULL,
                    LinkedEntityType TEXT NULL,
                    LinkedEntityId   TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_clinicalnotes_user_type_created
                    ON ClinicalNotes(UserId, NoteType, CreatedAt);";
            command.ExecuteNonQuery();

            // Idempotent ALTER migration — adds any missing columns to pre-existing tables.
            EnsureNoteColumns(connection, transaction);

            transaction.Commit();
        }

        /// <summary>
        /// Idempotent ALTER migration: adds any of the expected columns that a pre-existing
        /// ClinicalNotes table is missing. Safe to run repeatedly — PRAGMA table_info
        /// guards each ALTER so re-running EnsureSchema never throws "duplicate column".
        /// </summary>
        private static void EnsureNoteColumns(SqliteConnection connection, SqliteTransaction transaction)
        {
            var existing = ReadColumnNames(connection, transaction, "ClinicalNotes");

            // (column name, DDL). Order matches NoteColumns constant.
            (string Name, string Ddl)[] expectedColumns =
            {
                ("NoteId",           "TEXT PRIMARY KEY"),
                ("UserId",           "INTEGER NOT NULL"),
                ("NoteType",         "INTEGER NOT NULL"),
                ("AuthorRole",       "TEXT NOT NULL"),
                ("CreatedAt",        "TEXT NOT NULL"),
                ("BodyText",         "TEXT NOT NULL"),
                ("LinkedEntityType", "TEXT NULL"),
                ("LinkedEntityId",   "TEXT NULL"),
            };

            foreach (var (name, ddl) in expectedColumns)
            {
                // PRIMARY KEY columns cannot be added with ALTER TABLE — skip them if missing
                // (they must have been created by the CREATE TABLE IF NOT EXISTS above).
                if (name == "NoteId") continue;
                if (existing.Contains(name)) continue;

                using var alter = connection.CreateCommand();
                alter.Transaction = transaction;
                // Column names are compile-time constants (never user input) → safe.
                alter.CommandText = $"ALTER TABLE ClinicalNotes ADD COLUMN {name} {ddl};";
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

        private static void AddNoteParameters(SqliteCommand command, ClinicalNote note)
        {
            command.Parameters.AddWithValue("@NoteId", note.NoteId.ToString("D"));
            command.Parameters.AddWithValue("@UserId", note.UserId);
            command.Parameters.AddWithValue("@NoteType", (int)note.NoteType);
            command.Parameters.AddWithValue("@AuthorRole", note.AuthorRole);
            command.Parameters.AddWithValue("@CreatedAt", note.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("@BodyText", note.BodyText);
            command.Parameters.AddWithValue("@LinkedEntityType",
                note.LinkedEntityType is not null ? (object)note.LinkedEntityType : DBNull.Value);
            command.Parameters.AddWithValue("@LinkedEntityId",
                note.LinkedEntityId is not null ? (object)note.LinkedEntityId : DBNull.Value);
        }

        private static ClinicalNote MapNote(SqliteDataReader reader)
        {
            return new ClinicalNote
            {
                NoteId     = Guid.Parse(reader.GetString(reader.GetOrdinal("NoteId"))),
                UserId     = reader.GetInt32(reader.GetOrdinal("UserId")),
                NoteType   = (ClinicalNoteType)reader.GetInt32(reader.GetOrdinal("NoteType")),
                AuthorRole = reader.GetString(reader.GetOrdinal("AuthorRole")),
                CreatedAt  = DateTime.Parse(
                    reader.GetString(reader.GetOrdinal("CreatedAt")),
                    null, DateTimeStyles.RoundtripKind),
                BodyText         = reader.GetString(reader.GetOrdinal("BodyText")),
                LinkedEntityType = reader.IsDBNull(reader.GetOrdinal("LinkedEntityType"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("LinkedEntityType")),
                LinkedEntityId   = reader.IsDBNull(reader.GetOrdinal("LinkedEntityId"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("LinkedEntityId")),
            };
        }

        private static bool IsAppendOnly(ClinicalNoteType t) =>
            t == ClinicalNoteType.Review || t == ClinicalNoteType.GoalReview;
    }

    // ── Facade ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Facade over <see cref="IClinicalNotesRepository"/> that exposes save/query
    /// operations for clinical and coaching notes.
    ///
    /// Mutability policy is delegated entirely to the repository:
    /// <see cref="ClinicalNoteType.Coach"/> and <see cref="ClinicalNoteType.Clinical"/>
    /// are editable (upsert); <see cref="ClinicalNoteType.Review"/> and
    /// <see cref="ClinicalNoteType.GoalReview"/> are append-only (immutable once written).
    ///
    /// This is a DOCUMENTATION/AUDIT layer only — it never overrides health/safety gates.
    /// </summary>
    public sealed class ClinicalNotesStore
    {
        private readonly IClinicalNotesRepository _repository;

        public ClinicalNotesStore(IClinicalNotesRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Saves a note. Coach/Clinical are upserted; Review/GoalReview are append-only
        /// (a second save with the same NoteId is silently ignored).
        /// </summary>
        public Task SaveNoteAsync(ClinicalNote note, CancellationToken cancellationToken = default)
            => _repository.SaveNoteAsync(note, cancellationToken);

        /// <summary>
        /// Returns all notes for <paramref name="userId"/> of <paramref name="noteType"/>
        /// in the half-open interval [<paramref name="from"/>, <paramref name="to"/>),
        /// ordered by <see cref="ClinicalNote.CreatedAt"/> ascending.
        /// </summary>
        public Task<IReadOnlyList<ClinicalNote>> GetNotesAsync(
            int userId, ClinicalNoteType noteType, DateTime from, DateTime to,
            CancellationToken cancellationToken = default)
            => _repository.GetNotesAsync(userId, noteType, from, to, cancellationToken);
    }
}
