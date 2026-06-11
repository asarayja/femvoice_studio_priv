using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Microsoft.Data.Sqlite;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// W0-A6 (Clinical Notes) — verifies <see cref="ClinicalNotesStore"/>,
    /// <see cref="InMemoryClinicalNotesRepository"/>, and
    /// <see cref="SqliteClinicalNotesRepository"/>.
    ///
    /// House style: no mocking frameworks — real classes + in-memory fakes + temp SQLite
    /// files (mirrors SmartCoachMemoryStoreTests). All dates are fixed constants.
    /// </summary>
    public class ClinicalNotesStoreTests
    {
        // Fixed window used in all range-query tests.
        private static readonly DateTime WindowFrom = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime WindowTo   = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── 1. InMemory round-trip: save Coach note and read back ────────────────
        [Fact]
        public async Task InMemory_SaveCoachNote_RoundTrips()
        {
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository());
            var note = Note(1, ClinicalNoteType.Coach,
                new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
                body: "Good session today.");

            await store.SaveNoteAsync(note);
            var results = await store.GetNotesAsync(1, ClinicalNoteType.Coach, WindowFrom, WindowTo);

            var got = Assert.Single(results);
            Assert.Equal(note.NoteId, got.NoteId);
            Assert.Equal(1, got.UserId);
            Assert.Equal(ClinicalNoteType.Coach, got.NoteType);
            Assert.Equal("Good session today.", got.BodyText);
            Assert.Equal("Coach", got.AuthorRole);
            Assert.Null(got.LinkedEntityType);
            Assert.Null(got.LinkedEntityId);
        }

        // ── Sprint E (Agent 12): a configured audit trail records a ProfessionalNote
        //    event on every note save — closes the audit-completeness gap. ──
        [Fact]
        public async Task SaveNote_WithAuditTrail_EmitsProfessionalNoteAuditEvent()
        {
            var audit = new AuditTrailStore(new InMemoryAuditTrailRepository());
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository(), audit);
            var note = new ClinicalNote
            {
                NoteId = Guid.NewGuid(), UserId = 7, NoteType = ClinicalNoteType.Clinical,
                AuthorRole = "Clinician",
                CreatedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
                BodyText = "Resonance improving."
            };

            await store.SaveNoteAsync(note);

            var audits = await audit.QueryAsync(7, AuditEntityType.ProfessionalNote);
            var ev = Assert.Single(audits);
            Assert.Equal(AuditEntityType.ProfessionalNote, ev.EntityType);
            Assert.Equal(note.NoteId.ToString("D"), ev.EntityId);
            Assert.Equal("Clinician", ev.ActorRole);
            Assert.Equal("PROFESSIONAL_NOTE_SAVED", ev.ReasonCode);
        }

        // ── Sprint E: notes are NOT used in score calculation and the audit trail is
        //    optional — without one, save still works and never throws. ──
        [Fact]
        public async Task SaveNote_WithoutAuditTrail_StillSavesAndDoesNotThrow()
        {
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository());
            var note = new ClinicalNote
            {
                NoteId = Guid.NewGuid(), UserId = 1, NoteType = ClinicalNoteType.Coach,
                AuthorRole = "Coach",
                CreatedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
                BodyText = "No audit configured."
            };

            await store.SaveNoteAsync(note);

            var results = await store.GetNotesAsync(1, ClinicalNoteType.Coach, WindowFrom, WindowTo);
            Assert.Single(results);
        }

        // ── 2. InMemory upsert: saving same NoteId twice for Coach keeps latest ──
        [Fact]
        public async Task InMemory_CoachNote_SaveTwice_UpsertKeepsLatest()
        {
            var repo = new InMemoryClinicalNotesRepository();
            var store = new ClinicalNotesStore(repo);
            var id = Guid.NewGuid();
            var at = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

            await store.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 1, NoteType = ClinicalNoteType.Coach,
                AuthorRole = "Coach", CreatedAt = at, BodyText = "First version"
            });
            await store.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 1, NoteType = ClinicalNoteType.Coach,
                AuthorRole = "Coach", CreatedAt = at, BodyText = "Updated version"
            });

            var results = await store.GetNotesAsync(1, ClinicalNoteType.Coach, WindowFrom, WindowTo);
            Assert.Single(results);
            Assert.Equal("Updated version", results[0].BodyText);
        }

        // ── 3. InMemory upsert: saving same NoteId twice for Clinical keeps latest
        [Fact]
        public async Task InMemory_ClinicalNote_SaveTwice_UpsertKeepsLatest()
        {
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository());
            var id = Guid.NewGuid();
            var at = new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc);

            await store.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 2, NoteType = ClinicalNoteType.Clinical,
                AuthorRole = "Clinician", CreatedAt = at, BodyText = "Original"
            });
            await store.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 2, NoteType = ClinicalNoteType.Clinical,
                AuthorRole = "Clinician", CreatedAt = at, BodyText = "Amended"
            });

            var results = await store.GetNotesAsync(2, ClinicalNoteType.Clinical, WindowFrom, WindowTo);
            Assert.Single(results);
            Assert.Equal("Amended", results[0].BodyText);
        }

        // ── 4. InMemory append-only: Review note cannot be overwritten ───────────
        [Fact]
        public async Task InMemory_ReviewNote_IsAppendOnly_SecondSaveIsNoOp()
        {
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository());
            var id = Guid.NewGuid();
            var at = new DateTime(2026, 6, 3, 9, 0, 0, DateTimeKind.Utc);

            await store.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 1, NoteType = ClinicalNoteType.Review,
                AuthorRole = "System", CreatedAt = at, BodyText = "Immutable review"
            });
            // Attempt to overwrite — must be silently ignored.
            await store.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 1, NoteType = ClinicalNoteType.Review,
                AuthorRole = "System", CreatedAt = at, BodyText = "Overwrite attempt"
            });

            var results = await store.GetNotesAsync(1, ClinicalNoteType.Review, WindowFrom, WindowTo);
            Assert.Single(results);
            Assert.Equal("Immutable review", results[0].BodyText);
        }

        // ── 5. InMemory append-only: GoalReview note cannot be overwritten ────────
        [Fact]
        public async Task InMemory_GoalReviewNote_IsAppendOnly_SecondSaveIsNoOp()
        {
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository());
            var id = Guid.NewGuid();
            var at = new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

            await store.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 3, NoteType = ClinicalNoteType.GoalReview,
                AuthorRole = "System", CreatedAt = at, BodyText = "Original goal review"
            });
            await store.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 3, NoteType = ClinicalNoteType.GoalReview,
                AuthorRole = "System", CreatedAt = at, BodyText = "Attempted overwrite"
            });

            var results = await store.GetNotesAsync(3, ClinicalNoteType.GoalReview, WindowFrom, WindowTo);
            Assert.Single(results);
            Assert.Equal("Original goal review", results[0].BodyText);
        }

        // ── 6. InMemory range filter: only notes in [from,to) are returned ────────
        [Fact]
        public async Task InMemory_GetNotes_FiltersToWindow()
        {
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository());
            var t = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            await store.SaveNoteAsync(Note(4, ClinicalNoteType.Coach, t.AddDays(-1)));  // before window
            await store.SaveNoteAsync(Note(4, ClinicalNoteType.Coach, t));              // in window
            await store.SaveNoteAsync(Note(4, ClinicalNoteType.Coach, t.AddDays(1)));  // in window
            await store.SaveNoteAsync(Note(4, ClinicalNoteType.Coach, t.AddDays(5)));  // after window

            var from = t.AddHours(-1);
            var to   = t.AddDays(2);
            var results = await store.GetNotesAsync(4, ClinicalNoteType.Coach, from, to);
            Assert.Equal(2, results.Count);
            Assert.All(results, n => Assert.True(n.CreatedAt >= from && n.CreatedAt < to));
        }

        // ── 7. InMemory user isolation: notes for other users are not returned ─────
        [Fact]
        public async Task InMemory_GetNotes_IsolatesUsers()
        {
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository());
            var at = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

            await store.SaveNoteAsync(Note(10, ClinicalNoteType.Coach, at));
            await store.SaveNoteAsync(Note(20, ClinicalNoteType.Coach, at));

            var user10 = await store.GetNotesAsync(10, ClinicalNoteType.Coach, WindowFrom, WindowTo);
            Assert.Single(user10);
            Assert.Equal(10, user10[0].UserId);
        }

        // ── 8. InMemory NoteType filter: only matching type returned ──────────────
        [Fact]
        public async Task InMemory_GetNotes_FiltersByNoteType()
        {
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository());
            var at = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

            await store.SaveNoteAsync(Note(5, ClinicalNoteType.Coach, at));
            await store.SaveNoteAsync(Note(5, ClinicalNoteType.Clinical, at));
            await store.SaveNoteAsync(Note(5, ClinicalNoteType.Review, at));

            var coachNotes = await store.GetNotesAsync(5, ClinicalNoteType.Coach, WindowFrom, WindowTo);
            Assert.Single(coachNotes);
            Assert.Equal(ClinicalNoteType.Coach, coachNotes[0].NoteType);
        }

        // ── 9. InMemory linked entity fields round-trip ───────────────────────────
        [Fact]
        public async Task InMemory_LinkedEntityFields_RoundTrip()
        {
            var store = new ClinicalNotesStore(new InMemoryClinicalNotesRepository());
            var at = new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc);
            var note = new ClinicalNote
            {
                NoteId = Guid.NewGuid(), UserId = 6, NoteType = ClinicalNoteType.Clinical,
                AuthorRole = "Clinician", CreatedAt = at, BodyText = "Session note",
                LinkedEntityType = "Session", LinkedEntityId = "abc-123"
            };

            await store.SaveNoteAsync(note);
            var results = await store.GetNotesAsync(6, ClinicalNoteType.Clinical, WindowFrom, WindowTo);

            var got = Assert.Single(results);
            Assert.Equal("Session", got.LinkedEntityType);
            Assert.Equal("abc-123", got.LinkedEntityId);
        }

        // ── 10. SQLite round-trip across store instances ──────────────────────────
        [Fact]
        public async Task Sqlite_SaveAndRetrieve_RoundTripsAcrossInstances()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
            var note = new ClinicalNote
            {
                NoteId = Guid.NewGuid(), UserId = 7, NoteType = ClinicalNoteType.Coach,
                AuthorRole = "Coach", CreatedAt = at, BodyText = "Persistent note",
                LinkedEntityType = "Goal", LinkedEntityId = "goal-42"
            };

            // Write with first instance.
            var store1 = new ClinicalNotesStore(
                new SqliteClinicalNotesRepository(db.ConnectionString));
            await store1.SaveNoteAsync(note);

            // Read with fresh second instance (reads from disk, not memory).
            var repo2 = new SqliteClinicalNotesRepository(db.ConnectionString);
            var results = await repo2.GetNotesAsync(7, ClinicalNoteType.Coach, WindowFrom, WindowTo);

            var got = Assert.Single(results);
            Assert.Equal(note.NoteId, got.NoteId);
            Assert.Equal("Persistent note", got.BodyText);
            Assert.Equal("Goal", got.LinkedEntityType);
            Assert.Equal("goal-42", got.LinkedEntityId);
        }

        // ── 11. SQLite DateTime round-trip uses RoundtripKind ────────────────────
        [Fact]
        public async Task Sqlite_DateTimes_RoundTripWithRoundtripKind()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc);

            var note = new ClinicalNote
            {
                NoteId = Guid.NewGuid(), UserId = 1, NoteType = ClinicalNoteType.Clinical,
                AuthorRole = "Clinician", CreatedAt = at, BodyText = "Datetime test"
            };

            var repo = new SqliteClinicalNotesRepository(db.ConnectionString);
            await repo.SaveNoteAsync(note);

            var results = await repo.GetNotesAsync(1, ClinicalNoteType.Clinical, WindowFrom, WindowTo);
            var got = Assert.Single(results);

            Assert.Equal(at, got.CreatedAt);
            Assert.Equal(DateTimeKind.Utc, got.CreatedAt.Kind);
        }

        // ── 12. SQLite upsert: Coach note saved twice keeps latest ────────────────
        [Fact]
        public async Task Sqlite_CoachNote_Upsert_IsIdempotent()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc);
            var id = Guid.NewGuid();

            var repo = new SqliteClinicalNotesRepository(db.ConnectionString);

            await repo.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 1, NoteType = ClinicalNoteType.Coach,
                AuthorRole = "Coach", CreatedAt = at, BodyText = "Version 1"
            });
            await repo.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 1, NoteType = ClinicalNoteType.Coach,
                AuthorRole = "Coach", CreatedAt = at, BodyText = "Version 2"
            });

            var results = await repo.GetNotesAsync(1, ClinicalNoteType.Coach, WindowFrom, WindowTo);
            Assert.Single(results);
            Assert.Equal("Version 2", results[0].BodyText);
        }

        // ── 13. SQLite append-only: Review note cannot be overwritten ─────────────
        [Fact]
        public async Task Sqlite_ReviewNote_IsAppendOnly_SecondSaveIsNoOp()
        {
            using var db = new TempDb();
            var id = Guid.NewGuid();
            var at = new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc);

            var repo = new SqliteClinicalNotesRepository(db.ConnectionString);

            await repo.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 1, NoteType = ClinicalNoteType.Review,
                AuthorRole = "System", CreatedAt = at, BodyText = "Immutable review"
            });
            // Attempt overwrite — INSERT OR IGNORE keeps the first row.
            await repo.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 1, NoteType = ClinicalNoteType.Review,
                AuthorRole = "System", CreatedAt = at, BodyText = "Overwrite attempt"
            });

            var results = await repo.GetNotesAsync(1, ClinicalNoteType.Review, WindowFrom, WindowTo);
            Assert.Single(results);
            Assert.Equal("Immutable review", results[0].BodyText);
        }

        // ── 14. SQLite append-only: GoalReview note cannot be overwritten ─────────
        [Fact]
        public async Task Sqlite_GoalReviewNote_IsAppendOnly_SecondSaveIsNoOp()
        {
            using var db = new TempDb();
            var id = Guid.NewGuid();
            var at = new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc);

            var repo = new SqliteClinicalNotesRepository(db.ConnectionString);

            await repo.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 2, NoteType = ClinicalNoteType.GoalReview,
                AuthorRole = "System", CreatedAt = at, BodyText = "Original goal review"
            });
            await repo.SaveNoteAsync(new ClinicalNote
            {
                NoteId = id, UserId = 2, NoteType = ClinicalNoteType.GoalReview,
                AuthorRole = "System", CreatedAt = at, BodyText = "Attempted overwrite"
            });

            var results = await repo.GetNotesAsync(2, ClinicalNoteType.GoalReview, WindowFrom, WindowTo);
            Assert.Single(results);
            Assert.Equal("Original goal review", results[0].BodyText);
        }

        // ── 15. SQLite EnsureSchema idempotent: ctor called 3 times never throws ──
        [Fact]
        public async Task Sqlite_EnsureSchema_IsIdempotent_AcrossMultipleCtorCalls()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 6, 11, 0, 0, DateTimeKind.Utc);

            _ = new SqliteClinicalNotesRepository(db.ConnectionString);
            _ = new SqliteClinicalNotesRepository(db.ConnectionString);
            var third = new SqliteClinicalNotesRepository(db.ConnectionString);

            var note = Note(1, ClinicalNoteType.Coach, at, body: "After triple ctor");
            await third.SaveNoteAsync(note);

            var results = await third.GetNotesAsync(1, ClinicalNoteType.Coach, WindowFrom, WindowTo);
            Assert.Single(results);
            Assert.Equal("After triple ctor", results[0].BodyText);
        }

        // ── 16. SQLite NoteType stored as int: enum round-trips correctly ──────────
        [Fact]
        public async Task Sqlite_NoteType_IntColumn_RoundTrips()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc);
            var repo = new SqliteClinicalNotesRepository(db.ConnectionString);

            foreach (var type in Enum.GetValues<ClinicalNoteType>())
            {
                await repo.SaveNoteAsync(Note(1, type, at.AddHours((int)type)));
            }

            foreach (var type in Enum.GetValues<ClinicalNoteType>())
            {
                var results = await repo.GetNotesAsync(1, type, WindowFrom, WindowTo);
                Assert.Single(results);
                Assert.Equal(type, results[0].NoteType);
            }
        }

        // ── 17. SQLite null linked entity fields round-trip as null ──────────────
        [Fact]
        public async Task Sqlite_NullLinkedEntityFields_RoundTripsAsNull()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc);
            var note = new ClinicalNote
            {
                NoteId = Guid.NewGuid(), UserId = 1, NoteType = ClinicalNoteType.Coach,
                AuthorRole = "Coach", CreatedAt = at, BodyText = "No linked entity",
                LinkedEntityType = null, LinkedEntityId = null
            };

            var repo = new SqliteClinicalNotesRepository(db.ConnectionString);
            await repo.SaveNoteAsync(note);

            var results = await repo.GetNotesAsync(1, ClinicalNoteType.Coach, WindowFrom, WindowTo);
            var got = Assert.Single(results);
            Assert.Null(got.LinkedEntityType);
            Assert.Null(got.LinkedEntityId);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ClinicalNote Note(
            int userId,
            ClinicalNoteType noteType,
            DateTime createdAt,
            string body = "Test note body",
            string authorRole = "Coach")
        {
            return new ClinicalNote
            {
                NoteId     = Guid.NewGuid(),
                UserId     = userId,
                NoteType   = noteType,
                AuthorRole = authorRole,
                CreatedAt  = createdAt,
                BodyText   = body,
            };
        }

        /// <summary>Disposable temp SQLite database file (no mocks, real disk I/O).</summary>
        private sealed class TempDb : IDisposable
        {
            public string Path { get; }
            public string ConnectionString { get; }

            public TempDb()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"femvoice-{Guid.NewGuid():N}.db");
                ConnectionString = $"Data Source={Path}";
            }

            public void Dispose()
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(Path))
                    File.Delete(Path);
            }
        }
    }
}
