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
    /// W0-A8 (CaseReviews Store) — verifies <see cref="CaseReviewsStore"/>,
    /// <see cref="InMemoryCaseReviewsRepository"/>, and
    /// <see cref="SqliteCaseReviewsRepository"/>.
    ///
    /// House style: no mocking frameworks — real classes + in-memory fakes + temp SQLite
    /// files (mirrors SmartCoachMemoryStoreTests). All dates are fixed constants.
    /// </summary>
    public class CaseReviewsStoreTests
    {
        private static readonly DateTime FixedNow =
            new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // ── 1. InMemory round-trip: save and read back a single review ────────────
        [Fact]
        public async Task InMemory_SaveAndRetrieve_RoundTrips()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var review = MakeReview(userId: 1, ReviewType.Monthly);

            await store.SaveAsync(review);
            var all = await store.GetByUserAsync(1);

            var got = Assert.Single(all);
            Assert.Equal(review.ReviewId,  got.ReviewId);
            Assert.Equal(1,                got.UserId);
            Assert.Equal(ReviewType.Monthly, got.ReviewType);
            Assert.Equal(ReviewStatus.Draft, got.Status);
            Assert.Null(got.CompletedAt);
        }

        // ── 2. InMemory upsert: saving same ReviewId twice keeps latest ───────────
        [Fact]
        public async Task InMemory_SaveTwice_UpsertKeepsLatest()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var id = Guid.NewGuid();
            var draft = MakeReview(userId: 1, ReviewType.Monthly, id: id);

            await store.SaveAsync(draft);
            var completed = draft with
            {
                Status      = ReviewStatus.Completed,
                CompletedAt = FixedNow.AddDays(1),
            };
            await store.SaveAsync(completed);

            var all = await store.GetByUserAsync(1);
            Assert.Single(all);
            Assert.Equal(ReviewStatus.Completed, all[0].Status);
            Assert.NotNull(all[0].CompletedAt);
        }

        // ── 3. InMemory GetByUserAndType: filters by ReviewType ───────────────────
        [Fact]
        public async Task InMemory_GetByUserAndType_FiltersCorrectly()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());

            await store.SaveAsync(MakeReview(userId: 1, ReviewType.Monthly));
            await store.SaveAsync(MakeReview(userId: 1, ReviewType.Goal));
            await store.SaveAsync(MakeReview(userId: 1, ReviewType.Monthly));

            var monthly = await store.GetByUserAndTypeAsync(1, ReviewType.Monthly);
            Assert.Equal(2, monthly.Count);
            Assert.All(monthly, r => Assert.Equal(ReviewType.Monthly, r.ReviewType));
        }

        // ── 4. InMemory GetById: returns correct review ────────────────────────────
        [Fact]
        public async Task InMemory_GetById_ReturnsCorrectReview()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var review = MakeReview(userId: 1, ReviewType.Progress);
            await store.SaveAsync(review);

            var found = await store.GetByIdAsync(review.ReviewId);
            Assert.NotNull(found);
            Assert.Equal(review.ReviewId, found!.ReviewId);
        }

        // ── 5. InMemory GetById: unknown id returns null ───────────────────────────
        [Fact]
        public async Task InMemory_GetById_UnknownId_ReturnsNull()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());

            var found = await store.GetByIdAsync(Guid.NewGuid());
            Assert.Null(found);
        }

        // ── 6. InMemory Complete: Draft → Completed transition ────────────────────
        [Fact]
        public async Task InMemory_Complete_TransitionsDraftToCompleted()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var review = MakeReview(userId: 1, ReviewType.Monthly);
            await store.SaveAsync(review);

            var completedAt = FixedNow.AddDays(2);
            var result = await store.CompleteAsync(review.ReviewId, completedAt);

            Assert.NotNull(result);
            Assert.Equal(ReviewStatus.Completed, result!.Status);
            Assert.Equal(completedAt, result.CompletedAt);
        }

        // ── 7. InMemory Complete: already Completed returns unchanged ─────────────
        [Fact]
        public async Task InMemory_Complete_AlreadyCompleted_ReturnsUnchanged()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var completedAt = FixedNow.AddDays(1);
            var review = MakeReview(userId: 1, ReviewType.Monthly) with
            {
                Status      = ReviewStatus.Completed,
                CompletedAt = completedAt,
            };
            await store.SaveAsync(review);

            var result = await store.CompleteAsync(review.ReviewId, FixedNow.AddDays(5));

            Assert.NotNull(result);
            Assert.Equal(completedAt, result!.CompletedAt); // original completedAt preserved
        }

        // ── 8. InMemory Complete: unknown id returns null ─────────────────────────
        [Fact]
        public async Task InMemory_Complete_UnknownId_ReturnsNull()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());

            var result = await store.CompleteAsync(Guid.NewGuid(), FixedNow);
            Assert.Null(result);
        }

        // ── 9. InMemory user isolation: other users' reviews not returned ──────────
        [Fact]
        public async Task InMemory_GetByUser_IsolatesUsers()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());

            await store.SaveAsync(MakeReview(userId: 10, ReviewType.Monthly));
            await store.SaveAsync(MakeReview(userId: 20, ReviewType.Monthly));

            var user10 = await store.GetByUserAsync(10);
            Assert.Single(user10);
            Assert.Equal(10, user10[0].UserId);
        }

        // ── 10. InMemory ordering: results ordered by PeriodStart ascending ────────
        [Fact]
        public async Task InMemory_GetByUser_OrderedByPeriodStart()
        {
            var store = new CaseReviewsStore(new InMemoryCaseReviewsRepository());
            var t = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

            // Insert out-of-order.
            await store.SaveAsync(MakeReview(userId: 1, ReviewType.Monthly, periodStart: t.AddMonths(2)));
            await store.SaveAsync(MakeReview(userId: 1, ReviewType.Monthly, periodStart: t));
            await store.SaveAsync(MakeReview(userId: 1, ReviewType.Monthly, periodStart: t.AddMonths(1)));

            var all = await store.GetByUserAsync(1);
            Assert.Equal(3, all.Count);
            Assert.Equal(t,              all[0].PeriodStart);
            Assert.Equal(t.AddMonths(1), all[1].PeriodStart);
            Assert.Equal(t.AddMonths(2), all[2].PeriodStart);
        }

        // ── 11. SQLite round-trip across store instances ──────────────────────────
        [Fact]
        public async Task Sqlite_SaveAndRetrieve_RoundTripsAcrossInstances()
        {
            using var db = new TempDb();
            var review = MakeReview(userId: 5, ReviewType.Goal);

            // Write with first instance.
            var store1 = new CaseReviewsStore(new SqliteCaseReviewsRepository(db.ConnectionString));
            await store1.SaveAsync(review);

            // Read with fresh second instance.
            var repo2 = new SqliteCaseReviewsRepository(db.ConnectionString);
            var all = await repo2.GetByUserAsync(5);

            var got = Assert.Single(all);
            Assert.Equal(review.ReviewId, got.ReviewId);
            Assert.Equal(5,               got.UserId);
            Assert.Equal(ReviewType.Goal, got.ReviewType);
        }

        // ── 12. SQLite DateTime round-trip uses RoundtripKind ────────────────────
        [Fact]
        public async Task Sqlite_DateTimes_RoundTripWithRoundtripKind()
        {
            using var db = new TempDb();
            var start       = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            var end         = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
            var createdAt   = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var completedAt = new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc);

            var review = MakeReview(userId: 1, ReviewType.Monthly,
                periodStart: start, periodEnd: end, createdAt: createdAt) with
            {
                Status      = ReviewStatus.Completed,
                CompletedAt = completedAt,
            };

            var repo = new SqliteCaseReviewsRepository(db.ConnectionString);
            await repo.SaveAsync(review);
            var all = await repo.GetByUserAsync(1);
            var got = Assert.Single(all);

            Assert.Equal(start,       got.PeriodStart);
            Assert.Equal(DateTimeKind.Utc, got.PeriodStart.Kind);
            Assert.Equal(end,         got.PeriodEnd);
            Assert.Equal(DateTimeKind.Utc, got.PeriodEnd.Kind);
            Assert.Equal(createdAt,   got.CreatedAt);
            Assert.Equal(completedAt, got.CompletedAt!.Value);
            Assert.Equal(DateTimeKind.Utc, got.CompletedAt.Value.Kind);
        }

        // ── 13. SQLite null CompletedAt round-trips as null ───────────────────────
        [Fact]
        public async Task Sqlite_NullCompletedAt_RoundTripsAsNull()
        {
            using var db = new TempDb();
            var review = MakeReview(userId: 1, ReviewType.Monthly);

            var repo = new SqliteCaseReviewsRepository(db.ConnectionString);
            await repo.SaveAsync(review);
            var all = await repo.GetByUserAsync(1);

            Assert.Null(Assert.Single(all).CompletedAt);
        }

        // ── 14. SQLite OutcomeSnapshotJson round-trips verbatim ───────────────────
        [Fact]
        public async Task Sqlite_OutcomeSnapshotJson_RoundTripsVerbatim()
        {
            using var db = new TempDb();
            var json = "{\"UserId\":99,\"HasEnoughData\":true}";
            var review = MakeReview(userId: 1, ReviewType.Monthly) with
            {
                OutcomeSnapshotJson = json,
            };

            var repo = new SqliteCaseReviewsRepository(db.ConnectionString);
            await repo.SaveAsync(review);
            var got = Assert.Single(await repo.GetByUserAsync(1));

            Assert.Equal(json, got.OutcomeSnapshotJson);
        }

        // ── 15. SQLite UPSERT idempotency: Draft → Completed transition ───────────
        [Fact]
        public async Task Sqlite_Upsert_DraftToCompleted_IsIdempotent()
        {
            using var db = new TempDb();
            var id = Guid.NewGuid();
            var draft = MakeReview(userId: 1, ReviewType.Monthly, id: id);

            var repo = new SqliteCaseReviewsRepository(db.ConnectionString);
            await repo.SaveAsync(draft);

            var completed = draft with
            {
                Status      = ReviewStatus.Completed,
                CompletedAt = FixedNow.AddDays(1),
            };
            await repo.SaveAsync(completed);

            var all = await repo.GetByUserAsync(1);
            Assert.Single(all);
            Assert.Equal(ReviewStatus.Completed, all[0].Status);
        }

        // ── 16. SQLite EnsureSchema idempotent: ctor called 3 times never throws ──
        [Fact]
        public async Task Sqlite_EnsureSchema_IsIdempotent_AcrossMultipleCtorCalls()
        {
            using var db = new TempDb();

            _ = new SqliteCaseReviewsRepository(db.ConnectionString);
            _ = new SqliteCaseReviewsRepository(db.ConnectionString);
            var third = new SqliteCaseReviewsRepository(db.ConnectionString);

            var review = MakeReview(userId: 1, ReviewType.Recovery);
            await third.SaveAsync(review);
            var all = await third.GetByUserAsync(1);
            Assert.Single(all);
        }

        // ── 17. SQLite GetByUserAndType: filters ReviewType correctly ─────────────
        [Fact]
        public async Task Sqlite_GetByUserAndType_FiltersCorrectly()
        {
            using var db = new TempDb();
            var repo = new SqliteCaseReviewsRepository(db.ConnectionString);

            await repo.SaveAsync(MakeReview(userId: 2, ReviewType.Monthly));
            await repo.SaveAsync(MakeReview(userId: 2, ReviewType.Goal));
            await repo.SaveAsync(MakeReview(userId: 2, ReviewType.Monthly));

            var monthly = await repo.GetByUserAndTypeAsync(2, ReviewType.Monthly);
            Assert.Equal(2, monthly.Count);
            Assert.All(monthly, r => Assert.Equal(ReviewType.Monthly, r.ReviewType));
        }

        // ── 18. SQLite GetById: returns correct review ────────────────────────────
        [Fact]
        public async Task Sqlite_GetById_ReturnsCorrectReview()
        {
            using var db = new TempDb();
            var repo   = new SqliteCaseReviewsRepository(db.ConnectionString);
            var review = MakeReview(userId: 3, ReviewType.Progress);
            await repo.SaveAsync(review);

            var found = await repo.GetByIdAsync(review.ReviewId);
            Assert.NotNull(found);
            Assert.Equal(review.ReviewId, found!.ReviewId);
        }

        // ── 19. SQLite GetById: unknown id returns null ───────────────────────────
        [Fact]
        public async Task Sqlite_GetById_UnknownId_ReturnsNull()
        {
            using var db = new TempDb();
            var repo = new SqliteCaseReviewsRepository(db.ConnectionString);

            var found = await repo.GetByIdAsync(Guid.NewGuid());
            Assert.Null(found);
        }

        // ── 20. SQLite CompleteAsync: persists Completed status ───────────────────
        [Fact]
        public async Task Sqlite_CompleteAsync_PersistsCompletedStatus()
        {
            using var db = new TempDb();
            var store  = new CaseReviewsStore(new SqliteCaseReviewsRepository(db.ConnectionString));
            var review = MakeReview(userId: 1, ReviewType.Monthly);
            await store.SaveAsync(review);

            var completedAt = FixedNow.AddDays(3);
            var result = await store.CompleteAsync(review.ReviewId, completedAt);

            Assert.NotNull(result);
            Assert.Equal(ReviewStatus.Completed, result!.Status);

            // Verify persisted across a fresh instance.
            var repo2 = new SqliteCaseReviewsRepository(db.ConnectionString);
            var persisted = await repo2.GetByIdAsync(review.ReviewId);
            Assert.NotNull(persisted);
            Assert.Equal(ReviewStatus.Completed, persisted!.Status);
            Assert.Equal(completedAt, persisted.CompletedAt);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static CaseReview MakeReview(
            int userId,
            ReviewType type,
            Guid? id = null,
            DateTime? periodStart = null,
            DateTime? periodEnd = null,
            DateTime? createdAt = null)
        {
            var start = periodStart ?? new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            var end   = periodEnd   ?? start.AddDays(30);
            return new CaseReview
            {
                ReviewId            = id ?? Guid.NewGuid(),
                UserId              = userId,
                ReviewType          = type,
                PeriodStart         = start,
                PeriodEnd           = end,
                OutcomeSnapshotJson = "{}",
                Status              = ReviewStatus.Draft,
                CreatedAt           = createdAt ?? FixedNow,
                CompletedAt         = null,
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
