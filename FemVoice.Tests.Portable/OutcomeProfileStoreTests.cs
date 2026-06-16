using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Microsoft.Data.Sqlite;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// W0-A4 (Outcome Tracking) — verifies <see cref="OutcomeProfileStore"/>,
    /// <see cref="InMemoryOutcomeProfileRepository"/>, and
    /// <see cref="SqliteOutcomeProfileRepository"/>.
    ///
    /// House style: no mocking frameworks — real classes + in-memory fakes + temp SQLite
    /// files (mirrors SmartCoachMemoryStoreTests). All dates are fixed constants.
    /// </summary>
    public class OutcomeProfileStoreTests
    {
        private static readonly DateTime At = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // ── 1. InMemory round-trip: save and read back by id ──────────────────────
        [Fact]
        public async Task InMemory_SaveAndGetById_RoundTrips()
        {
            var store = new OutcomeProfileStore(new InMemoryOutcomeProfileRepository());
            var profile = SampleProfile(userId: 7, composite: 61.0);

            var id = await store.SaveSnapshotAsync(profile);
            var got = await store.GetSnapshotAsync(id);

            Assert.NotNull(got);
            Assert.Equal(7, got!.UserId);
            Assert.Equal(61.0, got.LongTermDevelopment.CompositeVoiceScore, 6);
            Assert.True(got.HasEnoughData);
        }

        // ── 2. InMemory: latest-for-user returns the most recent by GeneratedAt ───
        [Fact]
        public async Task InMemory_GetLatestForUser_ReturnsMostRecent()
        {
            var store = new OutcomeProfileStore(new InMemoryOutcomeProfileRepository());

            var older   = SampleProfile(userId: 3, composite: 50.0) with { GeneratedAt = At };
            var newer   = SampleProfile(userId: 3, composite: 55.0) with { GeneratedAt = At.AddDays(1) };
            var another = SampleProfile(userId: 9, composite: 99.0) with { GeneratedAt = At.AddDays(2) };

            await store.SaveSnapshotAsync(older);
            await store.SaveSnapshotAsync(newer);
            await store.SaveSnapshotAsync(another); // different user — must be ignored

            var latest = await store.GetLatestForUserAsync(3);
            Assert.NotNull(latest);
            Assert.Equal(55.0, latest!.LongTermDevelopment.CompositeVoiceScore, 6);
        }

        // ── 3. InMemory: missing id ⇒ null ────────────────────────────────────────
        [Fact]
        public async Task InMemory_GetById_Missing_ReturnsNull()
        {
            var store = new OutcomeProfileStore(new InMemoryOutcomeProfileRepository());
            Assert.Null(await store.GetSnapshotAsync(Guid.NewGuid()));
        }

        // ── 4. InMemory: explicit-id upsert keeps latest ─────────────────────────
        [Fact]
        public async Task InMemory_ExplicitId_Upsert_KeepsLatest()
        {
            var store = new OutcomeProfileStore(new InMemoryOutcomeProfileRepository());
            var id = Guid.NewGuid();

            await store.SaveSnapshotAsync(id, SampleProfile(1, composite: 40.0));
            await store.SaveSnapshotAsync(id, SampleProfile(1, composite: 70.0));

            var got = await store.GetSnapshotAsync(id);
            Assert.NotNull(got);
            Assert.Equal(70.0, got!.LongTermDevelopment.CompositeVoiceScore, 6);
        }

        // ── 5. SQLite round-trip across instances, full structure preserved ───────
        [Fact]
        public async Task Sqlite_SaveAndGetById_RoundTripsAcrossInstances()
        {
            using var db = new TempDb();
            var profile = SampleProfile(userId: 5, composite: 63.0);

            var store1 = new OutcomeProfileStore(
                new SqliteOutcomeProfileRepository(db.ConnectionString));
            var id = await store1.SaveSnapshotAsync(profile);

            // Read with a fresh instance (reads from disk, not memory).
            var store2 = new OutcomeProfileStore(
                new SqliteOutcomeProfileRepository(db.ConnectionString));
            var got = await store2.GetSnapshotAsync(id);

            Assert.NotNull(got);
            Assert.Equal(5, got!.UserId);
            // Nested structure survives JSON round-trip.
            Assert.Equal(63.0, got.LongTermDevelopment.CompositeVoiceScore, 6);
            Assert.Single(got.GoalProgress.Goals);
            Assert.Equal(VoiceDimension.Resonance, got.GoalProgress.Goals[0].PrimaryFocus);
            Assert.Equal("Strained", got.RecoveryProgress.Status);
            Assert.Single(got.ExerciseEffectiveness.Ranked);
            Assert.Equal(2, got.ExerciseEffectiveness.Ranked[0].ExerciseId);
            Assert.NotNull(got.LongTermDevelopment.Plateau);
            Assert.Equal("PLATEAU_Resonance", got.LongTermDevelopment.Plateau!.ReasonCode);
        }

        // ── 6. SQLite DateTime (GeneratedAt) round-trips with RoundtripKind ───────
        [Fact]
        public async Task Sqlite_GeneratedAt_RoundTripsUtc()
        {
            using var db = new TempDb();
            var profile = SampleProfile(1, composite: 50.0) with { GeneratedAt = At };

            var repo = new SqliteOutcomeProfileRepository(db.ConnectionString);
            var store = new OutcomeProfileStore(repo);
            var id = await store.SaveSnapshotAsync(profile);

            var got = await store.GetSnapshotAsync(id);
            Assert.NotNull(got);
            Assert.Equal(At, got!.GeneratedAt);
            Assert.Equal(DateTimeKind.Utc, got.GeneratedAt.Kind);
        }

        // ── 7. SQLite latest-for-user picks newest by GeneratedAt ────────────────
        [Fact]
        public async Task Sqlite_GetLatestForUser_ReturnsNewest()
        {
            using var db = new TempDb();
            var store = new OutcomeProfileStore(
                new SqliteOutcomeProfileRepository(db.ConnectionString));

            await store.SaveSnapshotAsync(SampleProfile(2, 40.0) with { GeneratedAt = At });
            await store.SaveSnapshotAsync(SampleProfile(2, 60.0) with { GeneratedAt = At.AddDays(2) });
            await store.SaveSnapshotAsync(SampleProfile(2, 50.0) with { GeneratedAt = At.AddDays(1) });

            var latest = await store.GetLatestForUserAsync(2);
            Assert.NotNull(latest);
            Assert.Equal(60.0, latest!.LongTermDevelopment.CompositeVoiceScore, 6);
        }

        // ── 8. SQLite explicit-id UPSERT is idempotent ───────────────────────────
        [Fact]
        public async Task Sqlite_Upsert_IsIdempotent()
        {
            using var db = new TempDb();
            var id = Guid.NewGuid();
            var store = new OutcomeProfileStore(
                new SqliteOutcomeProfileRepository(db.ConnectionString));

            await store.SaveSnapshotAsync(id, SampleProfile(1, 30.0));
            await store.SaveSnapshotAsync(id, SampleProfile(1, 80.0));

            var got = await store.GetSnapshotAsync(id);
            Assert.NotNull(got);
            Assert.Equal(80.0, got!.LongTermDevelopment.CompositeVoiceScore, 6);
        }

        // ── 9. SQLite EnsureSchema idempotent: ctor called 3 times never throws ──
        [Fact]
        public async Task Sqlite_EnsureSchema_IsIdempotent_AcrossMultipleCtorCalls()
        {
            using var db = new TempDb();

            _ = new SqliteOutcomeProfileRepository(db.ConnectionString);
            _ = new SqliteOutcomeProfileRepository(db.ConnectionString);
            var third = new SqliteOutcomeProfileRepository(db.ConnectionString);

            var store = new OutcomeProfileStore(third);
            var id = await store.SaveSnapshotAsync(SampleProfile(1, 55.0));

            var got = await store.GetSnapshotAsync(id);
            Assert.NotNull(got);
            Assert.Equal(55.0, got!.LongTermDevelopment.CompositeVoiceScore, 6);
        }

        // ── 10. SQLite missing id ⇒ null ─────────────────────────────────────────
        [Fact]
        public async Task Sqlite_GetById_Missing_ReturnsNull()
        {
            using var db = new TempDb();
            var store = new OutcomeProfileStore(
                new SqliteOutcomeProfileRepository(db.ConnectionString));
            Assert.Null(await store.GetSnapshotAsync(Guid.NewGuid()));
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static OutcomeProfile SampleProfile(int userId, double composite) => new()
        {
            UserId = userId,
            GeneratedAt = At,
            GoalProgress = new GoalProgress
            {
                Goals = new List<GoalProgressEntry>
                {
                    new()
                    {
                        GoalType = "resonance",
                        PrimaryFocus = VoiceDimension.Resonance,
                        TargetValue = 100,
                        CurrentValue = 75,
                        DeltaToGoal = 25,
                        PercentComplete = 75,
                        IsAchieved = false
                    }
                }
            },
            RecoveryProgress = new RecoveryProgress
            {
                CurrentScore0to100 = 42,
                Status = "Strained",
                OvertrainingPredicted = true,
                RecoveryDebt = 70,
                AcuteChronicWorkloadRatio = 1.6,
                Severity = "Urgent",
                RecommendationText = "Rest soon."
            },
            ExerciseEffectiveness = new ExerciseEffectivenessSummary
            {
                Ranked = new List<ExerciseEffectivenessProfile>
                {
                    new() { ExerciseId = 2, SessionCount = 6, HasEnoughData = true,
                            CompositeEffectiveness = 80, UserSuccessRate = 70, RecoveryCost = 10 }
                },
                Concerns = new List<ExerciseEffectivenessFlag>
                {
                    new() { ExerciseId = 4, ReasonCode = "HIGH_RECOVERY_COST", Magnitude = 75 }
                }
            },
            LongTermDevelopment = new LongTermDevelopment
            {
                WeeklyTrend = Array.Empty<TrendWindow>(),
                MonthlyTrend = Array.Empty<TrendWindow>(),
                CompositeVoiceScore = composite,
                Plateau = new PlateauState
                {
                    ReasonCode = "PLATEAU_Resonance",
                    Dimension = VoiceDimension.Resonance,
                    SeverityScore = 40
                },
                Insights = new List<LongitudinalInsight>
                {
                    new() { ReasonCode = "IMPROVEMENT", Dimension = VoiceDimension.Resonance, Confidence = 70 }
                }
            },
            HasEnoughData = true
        };

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
