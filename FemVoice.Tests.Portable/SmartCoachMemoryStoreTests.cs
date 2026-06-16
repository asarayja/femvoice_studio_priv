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
    /// A8 (SmartCoach Memory) — verifies <see cref="SmartCoachMemoryStore"/>,
    /// <see cref="InMemorySmartCoachMemoryRepository"/>, and
    /// <see cref="SqliteSmartCoachMemoryRepository"/>.
    ///
    /// House style: no mocking frameworks — real classes + in-memory fakes + temp SQLite
    /// files (mirrors VoiceIntelligencePersistenceTests). All dates are fixed constants.
    /// </summary>
    public class SmartCoachMemoryStoreTests
    {
        // Fixed window used in all range-query tests.
        private static readonly DateTime WindowFrom = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime WindowTo   = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── 1. InMemory round-trip: save and read back a single entry ─────────────
        [Fact]
        public async Task InMemory_SaveAndRetrieve_RoundTrips()
        {
            var store = new SmartCoachMemoryStore(new InMemorySmartCoachMemoryRepository());
            var entry = Advice(1, new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
                focusArea: "Resonance", exerciseId: 42);

            await store.SaveAdviceAsync(entry);
            var history = await store.GetAdviceHistoryAsync(1, WindowFrom, WindowTo);

            var got = Assert.Single(history);
            Assert.Equal(entry.AdviceId, got.AdviceId);
            Assert.Equal(1, got.UserId);
            Assert.Equal("Resonance", got.FocusArea);
            Assert.Equal(42, got.RecommendedExerciseId);
            Assert.False(got.UserFollowedAdvice);
            Assert.Null(got.OutcomeGain);
            Assert.False(got.Success);
        }

        // ── 2. InMemory upsert: saving the same AdviceId twice keeps latest ───────
        [Fact]
        public async Task InMemory_SaveTwice_UpsertKeepsLatest()
        {
            var repo = new InMemorySmartCoachMemoryRepository();
            var store = new SmartCoachMemoryStore(repo);
            var id = Guid.NewGuid();
            var at = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

            await store.SaveAdviceAsync(new SmartCoachAdviceEntry
            {
                AdviceId = id, UserId = 1, RecommendedAt = at, FocusArea = "Comfort"
            });
            await store.SaveAdviceAsync(new SmartCoachAdviceEntry
            {
                AdviceId = id, UserId = 1, RecommendedAt = at, FocusArea = "Resonance"
            });

            var history = await store.GetAdviceHistoryAsync(1, WindowFrom, WindowTo);
            Assert.Single(history);
            Assert.Equal("Resonance", history[0].FocusArea);
        }

        // ── 3. InMemory RecordOutcome: outcome fields are updated ────────────────
        [Fact]
        public async Task InMemory_RecordOutcome_UpdatesOutcomeFields()
        {
            var store = new SmartCoachMemoryStore(new InMemorySmartCoachMemoryRepository());
            var at = new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc);
            var entry = Advice(2, at, focusArea: "Recovery");
            await store.SaveAdviceAsync(entry);

            var started   = new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
            var completed = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc);
            await store.RecordOutcomeAsync(entry.AdviceId, started, completed,
                followed: true, outcomeGain: 12.5, success: true);

            var history = await store.GetAdviceHistoryAsync(2, WindowFrom, WindowTo);
            var got = Assert.Single(history);
            Assert.Equal(started, got.StartedAt);
            Assert.Equal(completed, got.CompletedAt);
            Assert.True(got.UserFollowedAdvice);
            Assert.Equal(12.5, got.OutcomeGain!.Value, 6);
            Assert.True(got.Success);
        }

        // ── 4. ComputeAdherenceRate: correct percentage ───────────────────────────
        [Fact]
        public void ComputeAdherenceRate_ReturnsCorrectPercentage()
        {
            var at = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc);
            var history = new[]
            {
                Advice(1, at, followed: true),
                Advice(1, at.AddHours(1), followed: true),
                Advice(1, at.AddHours(2), followed: false),
                Advice(1, at.AddHours(3), followed: false),
            };
            // 2/4 = 50 %
            Assert.Equal(50.0, SmartCoachMemoryStore.ComputeAdherenceRate(history), 6);
        }

        // ── 5. ComputeAdherenceRate: empty list returns 0 ────────────────────────
        [Fact]
        public void ComputeAdherenceRate_EmptyList_ReturnsZero()
        {
            Assert.Equal(0.0, SmartCoachMemoryStore.ComputeAdherenceRate(Array.Empty<SmartCoachAdviceEntry>()), 6);
        }

        // ── 6. ComputeRecommendationSuccessRate: only followed entries counted ───
        [Fact]
        public void ComputeRecommendationSuccessRate_OnlyFollowedEntriesCountedForSuccess()
        {
            var at = new DateTime(2026, 6, 4, 8, 0, 0, DateTimeKind.Utc);
            var history = new[]
            {
                // followed + succeeded ⇒ counts
                Advice(1, at, followed: true, success: true),
                // followed + not succeeded ⇒ counts against
                Advice(1, at.AddHours(1), followed: true, success: false),
                // not followed ⇒ excluded entirely
                Advice(1, at.AddHours(2), followed: false, success: true),
            };
            // Only the two followed entries: 1 success / 2 = 50 %
            Assert.Equal(50.0, SmartCoachMemoryStore.ComputeRecommendationSuccessRate(history), 6);
        }

        // ── 7. ComputeRecommendationSuccessRate: no followed ⇒ 0 ─────────────────
        [Fact]
        public void ComputeRecommendationSuccessRate_NoneFollowed_ReturnsZero()
        {
            var at = new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc);
            var history = new[]
            {
                Advice(1, at, followed: false, success: false),
                Advice(1, at.AddHours(1), followed: false, success: true),
            };
            Assert.Equal(0.0, SmartCoachMemoryStore.ComputeRecommendationSuccessRate(history), 6);
        }

        // ── 8. GetLatestAdvice: returns most recent by RecommendedAt ─────────────
        [Fact]
        public void GetLatestAdvice_ReturnsMostRecent()
        {
            var at = new DateTime(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc);
            var earlier = Advice(1, at);
            var later   = Advice(1, at.AddDays(1));
            var history = new[] { later, earlier }; // insert in reverse order

            var latest = SmartCoachMemoryStore.GetLatestAdvice(history);
            Assert.NotNull(latest);
            Assert.Equal(later.AdviceId, latest!.AdviceId);
        }

        // ── 9. GetLatestAdvice: empty ⇒ null ─────────────────────────────────────
        [Fact]
        public void GetLatestAdvice_EmptyHistory_ReturnsNull()
        {
            Assert.Null(SmartCoachMemoryStore.GetLatestAdvice(Array.Empty<SmartCoachAdviceEntry>()));
        }

        // ── 10. Window filter: only entries in [from,to) are returned ────────────
        [Fact]
        public async Task InMemory_GetAdviceHistory_FiltersToWindow()
        {
            var store = new SmartCoachMemoryStore(new InMemorySmartCoachMemoryRepository());
            var t = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            await store.SaveAdviceAsync(Advice(3, t.AddDays(-1)));  // before window
            await store.SaveAdviceAsync(Advice(3, t));               // in window
            await store.SaveAdviceAsync(Advice(3, t.AddDays(1)));   // in window
            await store.SaveAdviceAsync(Advice(3, t.AddDays(5)));   // after window

            var from = t.AddHours(-1);
            var to   = t.AddDays(2);
            var history = await store.GetAdviceHistoryAsync(3, from, to);
            Assert.Equal(2, history.Count);
            Assert.All(history, e => Assert.True(e.RecommendedAt >= from && e.RecommendedAt < to));
        }

        // ── 11. User isolation: entries for other users are not returned ──────────
        [Fact]
        public async Task InMemory_GetAdviceHistory_IsolatesUsers()
        {
            var store = new SmartCoachMemoryStore(new InMemorySmartCoachMemoryRepository());
            var at = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

            await store.SaveAdviceAsync(Advice(10, at));
            await store.SaveAdviceAsync(Advice(20, at));

            var user10 = await store.GetAdviceHistoryAsync(10, WindowFrom, WindowTo);
            Assert.Single(user10);
            Assert.Equal(10, user10[0].UserId);
        }

        // ── 12. SQLite round-trip across store instances ──────────────────────────
        [Fact]
        public async Task Sqlite_SaveAndRetrieve_RoundTripsAcrossInstances()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
            var entry = Advice(5, at, focusArea: "Intonation", exerciseId: 7);

            // Write with first instance.
            var store1 = new SmartCoachMemoryStore(
                new SqliteSmartCoachMemoryRepository(db.ConnectionString));
            await store1.SaveAdviceAsync(entry);

            // Read with fresh second instance (reads from disk, not memory).
            var repo2 = new SqliteSmartCoachMemoryRepository(db.ConnectionString);
            var history = await repo2.GetAdviceHistoryAsync(5, WindowFrom, WindowTo);

            var got = Assert.Single(history);
            Assert.Equal(entry.AdviceId, got.AdviceId);
            Assert.Equal("Intonation", got.FocusArea);
            Assert.Equal(7, got.RecommendedExerciseId);
        }

        // ── 13. SQLite DateTime round-trip uses RoundtripKind ────────────────────
        [Fact]
        public async Task Sqlite_DateTimes_RoundTripWithRoundtripKind()
        {
            using var db = new TempDb();
            var at        = new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc);
            var started   = new DateTime(2026, 6, 2, 15, 0, 0, DateTimeKind.Utc);
            var completed = new DateTime(2026, 6, 2, 16, 0, 0, DateTimeKind.Utc);

            var entry = new SmartCoachAdviceEntry
            {
                AdviceId = Guid.NewGuid(), UserId = 1, RecommendedAt = at,
                FocusArea = "Comfort", StartedAt = started, CompletedAt = completed,
                UserFollowedAdvice = true, OutcomeGain = 8.0, Success = true
            };

            var repo = new SqliteSmartCoachMemoryRepository(db.ConnectionString);
            await repo.SaveAdviceAsync(entry);

            var history = await repo.GetAdviceHistoryAsync(1, WindowFrom, WindowTo);
            var got = Assert.Single(history);

            Assert.Equal(at, got.RecommendedAt);
            Assert.Equal(DateTimeKind.Utc, got.RecommendedAt.Kind);
            Assert.Equal(started, got.StartedAt!.Value);
            Assert.Equal(DateTimeKind.Utc, got.StartedAt.Value.Kind);
            Assert.Equal(completed, got.CompletedAt!.Value);
            Assert.Equal(8.0, got.OutcomeGain!.Value, 6);
        }

        // ── 14. SQLite UPSERT idempotency: saving same entry twice keeps latest ──
        [Fact]
        public async Task Sqlite_Upsert_IsIdempotent()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc);
            var id = Guid.NewGuid();

            var repo = new SqliteSmartCoachMemoryRepository(db.ConnectionString);

            await repo.SaveAdviceAsync(new SmartCoachAdviceEntry
            {
                AdviceId = id, UserId = 1, RecommendedAt = at, FocusArea = "VocalWeight"
            });
            // Second save with updated FocusArea.
            await repo.SaveAdviceAsync(new SmartCoachAdviceEntry
            {
                AdviceId = id, UserId = 1, RecommendedAt = at, FocusArea = "Pitch"
            });

            var history = await repo.GetAdviceHistoryAsync(1, WindowFrom, WindowTo);
            Assert.Single(history);
            Assert.Equal("Pitch", history[0].FocusArea);
        }

        // ── 15. SQLite EnsureSchema idempotent: ctor called 3 times never throws ─
        [Fact]
        public async Task Sqlite_EnsureSchema_IsIdempotent_AcrossMultipleCtorCalls()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 4, 11, 0, 0, DateTimeKind.Utc);

            _ = new SqliteSmartCoachMemoryRepository(db.ConnectionString);
            _ = new SqliteSmartCoachMemoryRepository(db.ConnectionString);
            var third = new SqliteSmartCoachMemoryRepository(db.ConnectionString);

            var entry = Advice(1, at, focusArea: "Recovery");
            await third.SaveAdviceAsync(entry);

            var history = await third.GetAdviceHistoryAsync(1, WindowFrom, WindowTo);
            Assert.Single(history);
            Assert.Equal("Recovery", history[0].FocusArea);
        }

        // ── 16. SQLite RecordOutcome: outcome persists across instances ───────────
        [Fact]
        public async Task Sqlite_RecordOutcome_PersistsAcrossInstances()
        {
            using var db = new TempDb();
            var at      = new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc);
            var started = new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc);
            var entry   = Advice(1, at, focusArea: "Resonance");

            var repo1 = new SqliteSmartCoachMemoryRepository(db.ConnectionString);
            await repo1.SaveAdviceAsync(entry);
            await repo1.RecordOutcomeAsync(entry.AdviceId, started, completed: null,
                followed: true, outcomeGain: 7.3, success: false);

            // Read back with a fresh instance.
            var repo2 = new SqliteSmartCoachMemoryRepository(db.ConnectionString);
            var history = await repo2.GetAdviceHistoryAsync(1, WindowFrom, WindowTo);
            var got = Assert.Single(history);

            Assert.True(got.UserFollowedAdvice);
            Assert.Equal(started, got.StartedAt!.Value);
            Assert.Null(got.CompletedAt);
            Assert.Equal(7.3, got.OutcomeGain!.Value, 6);
            Assert.False(got.Success);
        }

        // ── 17. SQLite null OutcomeGain round-trips as null ──────────────────────
        [Fact]
        public async Task Sqlite_NullOutcomeGain_RoundTripsAsNull()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc);
            var entry = new SmartCoachAdviceEntry
            {
                AdviceId = Guid.NewGuid(), UserId = 1, RecommendedAt = at,
                FocusArea = "Consistency", OutcomeGain = null
            };
            var repo = new SqliteSmartCoachMemoryRepository(db.ConnectionString);
            await repo.SaveAdviceAsync(entry);

            var history = await repo.GetAdviceHistoryAsync(1, WindowFrom, WindowTo);
            Assert.Null(Assert.Single(history).OutcomeGain);
        }

        // ── 18. Adherence + success pipeline integration (InMemory) ──────────────
        [Fact]
        public async Task InMemory_AdherenceAndSuccessRateIntegration()
        {
            var store = new SmartCoachMemoryStore(new InMemorySmartCoachMemoryRepository());
            var t = new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc);

            // Save 4 entries; record outcomes on all.
            var e1 = Advice(1, t);
            var e2 = Advice(1, t.AddHours(1));
            var e3 = Advice(1, t.AddHours(2));
            var e4 = Advice(1, t.AddHours(3));

            foreach (var e in new[] { e1, e2, e3, e4 })
                await store.SaveAdviceAsync(e);

            await store.RecordOutcomeAsync(e1.AdviceId, null, null, followed: true, outcomeGain: 10, success: true);
            await store.RecordOutcomeAsync(e2.AdviceId, null, null, followed: true, outcomeGain: 5,  success: false);
            await store.RecordOutcomeAsync(e3.AdviceId, null, null, followed: false, outcomeGain: null, success: false);
            await store.RecordOutcomeAsync(e4.AdviceId, null, null, followed: false, outcomeGain: null, success: false);

            var history = await store.GetAdviceHistoryAsync(1, WindowFrom, WindowTo);
            Assert.Equal(4, history.Count);

            // 2 followed / 4 total = 50 %
            Assert.Equal(50.0, SmartCoachMemoryStore.ComputeAdherenceRate(history), 6);
            // 1 success / 2 followed = 50 %
            Assert.Equal(50.0, SmartCoachMemoryStore.ComputeRecommendationSuccessRate(history), 6);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static SmartCoachAdviceEntry Advice(
            int userId,
            DateTime recommendedAt,
            string focusArea = "Resonance",
            int? exerciseId = null,
            bool followed = false,
            bool success = false)
        {
            return new SmartCoachAdviceEntry
            {
                AdviceId = Guid.NewGuid(),
                UserId = userId,
                RecommendedAt = recommendedAt,
                FocusArea = focusArea,
                RecommendedExerciseId = exerciseId,
                UserFollowedAdvice = followed,
                Success = success
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
                    System.IO.Path.GetTempPath(), $"femvoice-scm-{Guid.NewGuid():N}.db");
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
