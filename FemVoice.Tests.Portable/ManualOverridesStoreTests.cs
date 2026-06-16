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
    /// Agent 7 (Manual Override log) — verifies <see cref="ManualOverridesStore"/>,
    /// <see cref="InMemoryManualOverridesRepository"/>, and
    /// <see cref="SqliteManualOverridesRepository"/>. The log is APPEND-ONLY: rows are
    /// only ever inserted, so duplicate appends produce duplicate rows (never an upsert).
    ///
    /// House style: no mocking frameworks — real classes + in-memory fakes + temp SQLite
    /// files (mirrors SmartCoachMemoryStoreTests). All dates are fixed constants.
    /// </summary>
    public class ManualOverridesStoreTests
    {
        // Fixed window used in all range-query tests.
        private static readonly DateTime WindowFrom = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime WindowTo   = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── 1. InMemory append + read round-trips ─────────────────────────────────
        [Fact]
        public async Task InMemory_AppendAndRead_RoundTrips()
        {
            var store = new ManualOverridesStore(new InMemoryManualOverridesRepository());
            var entry = Log(1, new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
                kind: ManualOverrideKind.ExerciseReco, exerciseId: 3, applied: true, clamped: true);

            await store.AppendAsync(entry);
            var rows = await store.GetOverridesAsync(1, WindowFrom, WindowTo);

            var got = Assert.Single(rows);
            Assert.Equal(entry.ManualOverrideId, got.ManualOverrideId);
            Assert.Equal(entry.AuditId, got.AuditId);
            Assert.Equal(ManualOverrideKind.ExerciseReco, got.OverrideKind);
            Assert.Equal(3, got.ExerciseId);
            Assert.True(got.WasApplied);
            Assert.True(got.WasClamped);
            Assert.Null(got.BlockedReasonCode);
        }

        // ── 2. InMemory is APPEND-ONLY: same id appended twice ⇒ two rows ──────────
        [Fact]
        public async Task InMemory_AppendOnly_DuplicateAppendsAreNotMerged()
        {
            var repo = new InMemoryManualOverridesRepository();
            var store = new ManualOverridesStore(repo);
            var entry = Log(1, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc));

            await store.AppendAsync(entry);
            await store.AppendAsync(entry);

            var rows = await store.GetOverridesAsync(1, WindowFrom, WindowTo);
            // No upsert — the same payload appended twice yields two log rows.
            Assert.Equal(2, rows.Count);
        }

        // ── 3. InMemory window filter ──────────────────────────────────────────────
        [Fact]
        public async Task InMemory_GetOverrides_FiltersToWindow()
        {
            var store = new ManualOverridesStore(new InMemoryManualOverridesRepository());
            var t = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            await store.AppendAsync(Log(3, t.AddDays(-1)));  // before
            await store.AppendAsync(Log(3, t));               // in
            await store.AppendAsync(Log(3, t.AddDays(1)));   // in
            await store.AppendAsync(Log(3, t.AddDays(5)));   // after

            var from = t.AddHours(-1);
            var to   = t.AddDays(2);
            var rows = await store.GetOverridesAsync(3, from, to);
            Assert.Equal(2, rows.Count);
            Assert.All(rows, e => Assert.True(e.RequestedAt >= from && e.RequestedAt < to));
        }

        // ── 4. InMemory user isolation ─────────────────────────────────────────────
        [Fact]
        public async Task InMemory_GetOverrides_IsolatesUsers()
        {
            var store = new ManualOverridesStore(new InMemoryManualOverridesRepository());
            var at = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

            await store.AppendAsync(Log(10, at));
            await store.AppendAsync(Log(20, at));

            var user10 = await store.GetOverridesAsync(10, WindowFrom, WindowTo);
            Assert.Single(user10);
            Assert.Equal(10, user10[0].UserId);
        }

        // ── 5. LogResultAsync maps a request+result into a row ─────────────────────
        [Fact]
        public async Task LogResultAsync_MapsRequestAndResult()
        {
            var store = new ManualOverridesStore(new InMemoryManualOverridesRepository());
            var request = new ManualOverrideRequest
            {
                OverrideKind = ManualOverrideKind.RecoveryPlan,
                UserId = 7,
                ExerciseId = null,
                ReasonCode = "CLINICIAN_REST",
                ActorRole = "Clinician",
                RequestedAt = new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc)
            };
            var result = new ManualOverrideResult
            {
                WasApplied = false,
                WasClamped = false,
                BlockedReasonCode = "NO_INTENDED_PROFILE"
            };

            await store.LogResultAsync(request, result);

            var rows = await store.GetOverridesAsync(7, WindowFrom, WindowTo);
            var got = Assert.Single(rows);
            Assert.Equal(result.ManualOverrideId, got.ManualOverrideId);
            Assert.Equal(result.AuditId, got.AuditId);
            Assert.Equal(ManualOverrideKind.RecoveryPlan, got.OverrideKind);
            Assert.Equal("CLINICIAN_REST", got.ReasonCode);
            Assert.Equal("Clinician", got.ActorRole);
            Assert.False(got.WasApplied);
            Assert.Equal("NO_INTENDED_PROFILE", got.BlockedReasonCode);
            Assert.Null(got.ExerciseId);
        }

        // ── 6. SQLite append + read across instances ───────────────────────────────
        [Fact]
        public async Task Sqlite_AppendAndRead_RoundTripsAcrossInstances()
        {
            using var db = new TempDb();
            var entry = Log(5, new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
                kind: ManualOverrideKind.ProgressionPace, exerciseId: 9,
                applied: true, clamped: false, reasonCode: "PACE_DOWN", actorRole: "Coach");

            var store1 = new ManualOverridesStore(
                new SqliteManualOverridesRepository(db.ConnectionString));
            await store1.AppendAsync(entry);

            // Fresh second instance reads from disk, not memory.
            var repo2 = new SqliteManualOverridesRepository(db.ConnectionString);
            var rows = await repo2.GetOverridesAsync(5, WindowFrom, WindowTo);

            var got = Assert.Single(rows);
            Assert.Equal(entry.ManualOverrideId, got.ManualOverrideId);
            Assert.Equal(entry.AuditId, got.AuditId);
            Assert.Equal(ManualOverrideKind.ProgressionPace, got.OverrideKind);
            Assert.Equal(9, got.ExerciseId);
            Assert.Equal("PACE_DOWN", got.ReasonCode);
            Assert.Equal("Coach", got.ActorRole);
            Assert.True(got.WasApplied);
            Assert.False(got.WasClamped);
        }

        // ── 7. SQLite DateTime round-trips with RoundtripKind ──────────────────────
        [Fact]
        public async Task Sqlite_RequestedAt_RoundTripsWithRoundtripKind()
        {
            using var db = new TempDb();
            var at = new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc);
            var entry = Log(1, at);

            var repo = new SqliteManualOverridesRepository(db.ConnectionString);
            await repo.AppendAsync(entry);

            var rows = await repo.GetOverridesAsync(1, WindowFrom, WindowTo);
            var got = Assert.Single(rows);
            Assert.Equal(at, got.RequestedAt);
            Assert.Equal(DateTimeKind.Utc, got.RequestedAt.Kind);
        }

        // ── 8. SQLite is APPEND-ONLY: same id appended twice ⇒ two rows ────────────
        [Fact]
        public async Task Sqlite_AppendOnly_DuplicateAppendsYieldTwoRows()
        {
            using var db = new TempDb();
            var entry = Log(1, new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc));
            // NOTE: ManualOverrideId is the PK; a true upsert would replace. An APPEND-ONLY
            // log must give a DISTINCT id per append, which the production engine does
            // (each result carries a fresh ManualOverrideId). We simulate two distinct
            // appends here to prove the read returns BOTH (no merge by content).
            var entry2 = entry with { ManualOverrideId = Guid.NewGuid() };

            var repo = new SqliteManualOverridesRepository(db.ConnectionString);
            await repo.AppendAsync(entry);
            await repo.AppendAsync(entry2);

            var rows = await repo.GetOverridesAsync(1, WindowFrom, WindowTo);
            Assert.Equal(2, rows.Count);
        }

        // ── 9. SQLite EnsureSchema idempotent: ctor called 3 times never throws ────
        [Fact]
        public async Task Sqlite_EnsureSchema_IsIdempotent_AcrossMultipleCtorCalls()
        {
            using var db = new TempDb();

            _ = new SqliteManualOverridesRepository(db.ConnectionString);
            _ = new SqliteManualOverridesRepository(db.ConnectionString);
            var third = new SqliteManualOverridesRepository(db.ConnectionString);

            var entry = Log(1, new DateTime(2026, 6, 4, 11, 0, 0, DateTimeKind.Utc),
                reasonCode: "IDEMPOTENT");
            await third.AppendAsync(entry);

            var rows = await third.GetOverridesAsync(1, WindowFrom, WindowTo);
            Assert.Single(rows);
            Assert.Equal("IDEMPOTENT", rows[0].ReasonCode);
        }

        // ── 10. SQLite null ExerciseId + BlockedReasonCode round-trip as null ──────
        [Fact]
        public async Task Sqlite_NullableFields_RoundTripAsNull()
        {
            using var db = new TempDb();
            var entry = new ManualOverrideLogEntry
            {
                ManualOverrideId = Guid.NewGuid(),
                AuditId = Guid.NewGuid(),
                UserId = 1,
                OverrideKind = ManualOverrideKind.VoiceGoals,
                ExerciseId = null,
                RequestedAt = new DateTime(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc),
                ActorRole = "Clinician",
                ReasonCode = "GOALS",
                WasApplied = false,
                WasClamped = false,
                BlockedReasonCode = null
            };

            var repo = new SqliteManualOverridesRepository(db.ConnectionString);
            await repo.AppendAsync(entry);

            var got = Assert.Single(await repo.GetOverridesAsync(1, WindowFrom, WindowTo));
            Assert.Null(got.ExerciseId);
            Assert.Null(got.BlockedReasonCode);
        }

        // ── 11. SQLite OverrideKind enum round-trips by name ───────────────────────
        [Fact]
        public async Task Sqlite_OverrideKind_RoundTripsByName()
        {
            using var db = new TempDb();
            var repo = new SqliteManualOverridesRepository(db.ConnectionString);
            var at = new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc);

            foreach (var kind in Enum.GetValues<ManualOverrideKind>())
                await repo.AppendAsync(Log(1, at.AddMinutes((int)kind), kind: kind));

            var rows = await repo.GetOverridesAsync(1, WindowFrom, WindowTo);
            Assert.Equal(Enum.GetValues<ManualOverrideKind>().Length, rows.Count);
            Assert.Equal(
                Enum.GetValues<ManualOverrideKind>().OrderBy(k => k).ToArray(),
                rows.Select(r => r.OverrideKind).OrderBy(k => k).ToArray());
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ManualOverrideLogEntry Log(
            int userId,
            DateTime requestedAt,
            ManualOverrideKind kind = ManualOverrideKind.ExerciseReco,
            int? exerciseId = null,
            bool applied = false,
            bool clamped = false,
            string reasonCode = "TEST",
            string actorRole = "Clinician")
        {
            return new ManualOverrideLogEntry
            {
                ManualOverrideId = Guid.NewGuid(),
                AuditId = Guid.NewGuid(),
                UserId = userId,
                OverrideKind = kind,
                ExerciseId = exerciseId,
                RequestedAt = requestedAt,
                ActorRole = actorRole,
                ReasonCode = reasonCode,
                WasApplied = applied,
                WasClamped = clamped,
                BlockedReasonCode = null
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
                    System.IO.Path.GetTempPath(), $"femvoice-mo-{Guid.NewGuid():N}.db");
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
