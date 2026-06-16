using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="AuditTrailStore"/> / <see cref="IAuditTrailRepository"/>
    /// (W0-A11, Audit Trail).
    ///
    /// House style: no mocking frameworks — real classes + in-memory fakes
    /// (<see cref="InMemoryAuditTrailRepository"/>) and real SQLite temp files.
    ///
    /// Key invariant verified: the audit trail is <b>strictly append-only</b>.
    /// Appending the same AuditId twice produces two rows, not one updated row.
    /// </summary>
    public class AuditTrailStoreTests
    {
        // ── 1. In-memory append + query round-trip ────────────────────────────────

        [Fact]
        public async Task InMemory_AppendAndQuery_RoundTripsAllFields()
        {
            var store = new AuditTrailStore(new InMemoryAuditTrailRepository());
            var auditId = Guid.NewGuid();
            var occurredAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

            await store.AppendAsync(new AuditEvent
            {
                AuditId = auditId,
                UserId = 42,
                OccurredAt = occurredAt,
                EntityType = AuditEntityType.GoalChange,
                EntityId = "goal-001",
                ActorRole = "Coach",
                ReasonCode = "GOAL_ACHIEVED",
                BeforeJson = "{\"state\":\"active\"}",
                AfterJson = "{\"state\":\"achieved\"}"
            });

            var results = await store.QueryAsync(userId: 42);

            var e = Assert.Single(results);
            Assert.Equal(auditId, e.AuditId);
            Assert.Equal(42, e.UserId);
            Assert.Equal(occurredAt, e.OccurredAt);
            Assert.Equal(AuditEntityType.GoalChange, e.EntityType);
            Assert.Equal("goal-001", e.EntityId);
            Assert.Equal("Coach", e.ActorRole);
            Assert.Equal("GOAL_ACHIEVED", e.ReasonCode);
            Assert.Equal("{\"state\":\"active\"}", e.BeforeJson);
            Assert.Equal("{\"state\":\"achieved\"}", e.AfterJson);
        }

        // ── 2. In-memory: nullable BeforeJson / AfterJson survive as null ─────────

        [Fact]
        public async Task InMemory_NullableJsonFields_SurviveAsNull()
        {
            var store = new AuditTrailStore(new InMemoryAuditTrailRepository());

            await store.AppendAsync(new AuditEvent
            {
                UserId = 1,
                OccurredAt = DateTime.UtcNow,
                EntityType = AuditEntityType.RecoveryEvent,
                EntityId = "rec-1",
                ActorRole = "System",
                ReasonCode = "ACWR_ALERT",
                BeforeJson = null,
                AfterJson = null
            });

            var results = await store.QueryAsync(userId: 1);
            var e = Assert.Single(results);
            Assert.Null(e.BeforeJson);
            Assert.Null(e.AfterJson);
        }

        // ── 3. In-memory: entityType filter ──────────────────────────────────────

        [Fact]
        public async Task InMemory_EntityTypeFilter_ReturnOnlyMatchingType()
        {
            var repo = new InMemoryAuditTrailRepository();
            var store = new AuditTrailStore(repo);
            var t = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

            await store.AppendAsync(MakeEvent(userId: 5, type: AuditEntityType.Recommendation, entityId: "r1", occurredAt: t));
            await store.AppendAsync(MakeEvent(userId: 5, type: AuditEntityType.Override,        entityId: "o1", occurredAt: t.AddMinutes(1)));
            await store.AppendAsync(MakeEvent(userId: 5, type: AuditEntityType.Recommendation, entityId: "r2", occurredAt: t.AddMinutes(2)));

            var recommendations = await store.QueryAsync(userId: 5, entityType: AuditEntityType.Recommendation);
            Assert.Equal(2, recommendations.Count);
            Assert.All(recommendations, e => Assert.Equal(AuditEntityType.Recommendation, e.EntityType));
        }

        // ── 4. In-memory: date-range filter ──────────────────────────────────────

        [Fact]
        public async Task InMemory_DateRangeFilter_ReturnsEventsInHalfOpenInterval()
        {
            var repo = new InMemoryAuditTrailRepository();
            var store = new AuditTrailStore(repo);
            var base_ = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (var i = 0; i < 5; i++)
                await store.AppendAsync(MakeEvent(userId: 7, type: AuditEntityType.ReviewAction,
                    entityId: $"ev-{i}", occurredAt: base_.AddDays(i)));

            // [day 1, day 4) → days 1, 2, 3 (not 0, not 4)
            var filtered = await store.QueryAsync(
                userId: 7,
                from: base_.AddDays(1),
                to: base_.AddDays(4));

            Assert.Equal(3, filtered.Count);
            Assert.All(filtered, e =>
            {
                Assert.True(e.OccurredAt >= base_.AddDays(1));
                Assert.True(e.OccurredAt < base_.AddDays(4));
            });
        }

        // ── 5. In-memory: userId isolation ────────────────────────────────────────

        [Fact]
        public async Task InMemory_UserIsolation_QueryReturnsOnlyOwnEvents()
        {
            var store = new AuditTrailStore(new InMemoryAuditTrailRepository());
            var t = DateTime.UtcNow;

            await store.AppendAsync(MakeEvent(userId: 10, type: AuditEntityType.GoalChange, entityId: "g1", occurredAt: t));
            await store.AppendAsync(MakeEvent(userId: 20, type: AuditEntityType.GoalChange, entityId: "g2", occurredAt: t));

            var user10 = await store.QueryAsync(userId: 10);
            var user20 = await store.QueryAsync(userId: 20);

            Assert.Single(user10);
            Assert.Equal(10, user10[0].UserId);
            Assert.Single(user20);
            Assert.Equal(20, user20[0].UserId);
        }

        // ── 6. APPEND-ONLY invariant: duplicate AuditId ⇒ two rows, not one ──────

        [Fact]
        public async Task InMemory_AppendOnly_DuplicateAuditIdProducesTwoRows()
        {
            var store = new AuditTrailStore(new InMemoryAuditTrailRepository());
            var sharedId = Guid.NewGuid();
            var t = DateTime.UtcNow;

            await store.AppendAsync(new AuditEvent
            {
                AuditId = sharedId,
                UserId = 3,
                OccurredAt = t,
                EntityType = AuditEntityType.Override,
                EntityId = "ov-1",
                ActorRole = "Clinician",
                ReasonCode = "SAFETY_OVERRIDE_APPLIED",
                BeforeJson = "{\"v\":1}",
                AfterJson = "{\"v\":2}"
            });

            // Second append with the SAME AuditId — must produce a second row.
            await store.AppendAsync(new AuditEvent
            {
                AuditId = sharedId,
                UserId = 3,
                OccurredAt = t.AddSeconds(1),
                EntityType = AuditEntityType.Override,
                EntityId = "ov-1",
                ActorRole = "Clinician",
                ReasonCode = "DUPLICATE_WRITE",
                BeforeJson = null,
                AfterJson = null
            });

            var all = await store.QueryAsync(userId: 3);
            Assert.Equal(2, all.Count);
            // Both rows carry the same AuditId (no dedup, no update).
            Assert.Equal(2, all.Count(e => e.AuditId == sharedId));
        }

        // ── 7. SQLite: durable round-trip across store instances ──────────────────

        [Fact]
        public async Task Sqlite_AppendAndQuery_RoundTripAcrossInstances()
        {
            using var db = new TempDb();
            var auditId = Guid.NewGuid();
            var occurredAt = new DateTime(2026, 6, 2, 10, 30, 0, DateTimeKind.Utc);

            var firstStore = new AuditTrailStore(new SqliteAuditTrailRepository(db.ConnectionString));
            await firstStore.AppendAsync(new AuditEvent
            {
                AuditId = auditId,
                UserId = 99,
                OccurredAt = occurredAt,
                EntityType = AuditEntityType.Recommendation,
                EntityId = "rec-abc",
                ActorRole = "System",
                ReasonCode = "RECOMMENDATION_FOLLOWED",
                BeforeJson = null,
                AfterJson = "{\"exerciseId\":7}"
            });

            // Fresh instance reads from disk.
            var secondRepo = new SqliteAuditTrailRepository(db.ConnectionString);
            var results = await secondRepo.QueryAsync(userId: 99);

            var e = Assert.Single(results);
            Assert.Equal(auditId, e.AuditId);
            Assert.Equal(99, e.UserId);
            Assert.Equal(occurredAt, e.OccurredAt);
            Assert.Equal(AuditEntityType.Recommendation, e.EntityType);
            Assert.Equal("rec-abc", e.EntityId);
            Assert.Equal("System", e.ActorRole);
            Assert.Equal("RECOMMENDATION_FOLLOWED", e.ReasonCode);
            Assert.Null(e.BeforeJson);
            Assert.Equal("{\"exerciseId\":7}", e.AfterJson);
        }

        // ── 8. SQLite: filters work on persisted data ─────────────────────────────

        [Fact]
        public async Task Sqlite_Filters_EntityTypeAndDateRange_WorkOnPersistedData()
        {
            using var db = new TempDb();
            var repo = new SqliteAuditTrailRepository(db.ConnectionString);
            var store = new AuditTrailStore(repo);
            var base_ = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

            await store.AppendAsync(MakeEvent(userId: 11, type: AuditEntityType.GoalChange,     entityId: "g1", occurredAt: base_));
            await store.AppendAsync(MakeEvent(userId: 11, type: AuditEntityType.Override,       entityId: "o1", occurredAt: base_.AddHours(1)));
            await store.AppendAsync(MakeEvent(userId: 11, type: AuditEntityType.GoalChange,     entityId: "g2", occurredAt: base_.AddHours(2)));
            await store.AppendAsync(MakeEvent(userId: 11, type: AuditEntityType.RecoveryEvent,  entityId: "r1", occurredAt: base_.AddHours(3)));

            // Entity-type filter
            var goalEvents = await store.QueryAsync(userId: 11, entityType: AuditEntityType.GoalChange);
            Assert.Equal(2, goalEvents.Count);
            Assert.All(goalEvents, e => Assert.Equal(AuditEntityType.GoalChange, e.EntityType));

            // Date-range filter: [base+1h, base+3h) → Override + GoalChange(g2)
            var inRange = await store.QueryAsync(
                userId: 11,
                from: base_.AddHours(1),
                to: base_.AddHours(3));
            Assert.Equal(2, inRange.Count);

            // Combined filter
            var combined = await store.QueryAsync(
                userId: 11,
                entityType: AuditEntityType.GoalChange,
                from: base_.AddHours(1));
            Assert.Single(combined);
            Assert.Equal("g2", combined[0].EntityId);
        }

        // ── 9. SQLite: idempotent ctor — EnsureSchema three times never throws ────

        [Fact]
        public void Sqlite_EnsureSchema_IsIdempotent_ThreeConstructions()
        {
            using var db = new TempDb();

            // Each ctor calls EnsureSchema. A non-idempotent migration throws "duplicate column".
            var ex = Record.Exception(() =>
            {
                _ = new SqliteAuditTrailRepository(db.ConnectionString);
                _ = new SqliteAuditTrailRepository(db.ConnectionString);
                _ = new SqliteAuditTrailRepository(db.ConnectionString);
            });

            Assert.Null(ex);
        }

        // ── 10. SQLite: APPEND-ONLY invariant — same AuditId ⇒ two rows ──────────

        [Fact]
        public async Task Sqlite_AppendOnly_DuplicateAuditIdProducesTwoRows_NotOneUpdatedRow()
        {
            using var db = new TempDb();
            var store = new AuditTrailStore(new SqliteAuditTrailRepository(db.ConnectionString));
            var sharedId = Guid.NewGuid();
            var t = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc);

            await store.AppendAsync(new AuditEvent
            {
                AuditId = sharedId,
                UserId = 55,
                OccurredAt = t,
                EntityType = AuditEntityType.Override,
                EntityId = "ov-x",
                ActorRole = "Clinician",
                ReasonCode = "FIRST_WRITE",
                BeforeJson = null,
                AfterJson = "{\"v\":1}"
            });

            // Second append with the SAME AuditId — must NOT silently update the existing row.
            await store.AppendAsync(new AuditEvent
            {
                AuditId = sharedId,
                UserId = 55,
                OccurredAt = t.AddSeconds(5),
                EntityType = AuditEntityType.Override,
                EntityId = "ov-x",
                ActorRole = "Clinician",
                ReasonCode = "SECOND_WRITE",
                BeforeJson = null,
                AfterJson = "{\"v\":2}"
            });

            var all = await store.QueryAsync(userId: 55);

            // Two rows must exist — the second write appended, never updated.
            Assert.Equal(2, all.Count);
            Assert.Equal(2, all.Count(e => e.AuditId == sharedId));

            // Both original reason codes are preserved (no overwrite).
            Assert.Contains(all, e => e.ReasonCode == "FIRST_WRITE");
            Assert.Contains(all, e => e.ReasonCode == "SECOND_WRITE");
        }

        // ── 11. SQLite: results are ordered by OccurredAt ascending ───────────────

        [Fact]
        public async Task Sqlite_Query_ResultsOrderedByOccurredAtAscending()
        {
            using var db = new TempDb();
            var store = new AuditTrailStore(new SqliteAuditTrailRepository(db.ConnectionString));
            var base_ = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

            // Append in reverse order.
            for (var i = 4; i >= 0; i--)
                await store.AppendAsync(MakeEvent(userId: 8, type: AuditEntityType.ReviewAction,
                    entityId: $"ev-{i}", occurredAt: base_.AddHours(i)));

            var results = await store.QueryAsync(userId: 8);

            Assert.Equal(5, results.Count);
            for (var i = 1; i < results.Count; i++)
                Assert.True(results[i].OccurredAt >= results[i - 1].OccurredAt,
                    "Results must be ordered OccurredAt ascending.");
        }

        // ── 12. SQLite: WAL mode is active after EnsureSchema ─────────────────────

        [Fact]
        public void Sqlite_WalMode_IsEnabledAfterEnsureSchema()
        {
            using var db = new TempDb();
            _ = new SqliteAuditTrailRepository(db.ConnectionString);

            using var connection = new SqliteConnection(db.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            var journalMode = command.ExecuteScalar()?.ToString();

            Assert.Equal("wal", journalMode, ignoreCase: true);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static AuditEvent MakeEvent(
            int userId,
            AuditEntityType type,
            string entityId,
            DateTime occurredAt)
        {
            return new AuditEvent
            {
                AuditId = Guid.NewGuid(),
                UserId = userId,
                OccurredAt = occurredAt,
                EntityType = type,
                EntityId = entityId,
                ActorRole = "System",
                ReasonCode = "TEST_EVENT",
                BeforeJson = null,
                AfterJson = null
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
                    System.IO.Path.GetTempPath(), $"femvoice-audit-{Guid.NewGuid():N}.db");
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
