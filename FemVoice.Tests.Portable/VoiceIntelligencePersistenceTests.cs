using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Microsoft.Data.Sqlite;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint B — Agent P (persistence). Verifies the eight Voice Intelligence 0–100
    /// scores (seven dimensions + composite) are persisted durably in the live path:
    /// round-tripped, kept on the 0–100 scale (never crushed to 0–1), the ALTER
    /// migration is idempotent, missing scores default to 0 without crashing, the
    /// trend read is chronological, and the real <see cref="ExerciseSessionRecorder"/>
    /// actually writes scores at session end.
    ///
    /// House style: no mocking frameworks — real classes + in-memory fakes
    /// (<see cref="InMemorySessionAnalyticsRepository"/>, <see cref="TestDatabaseService"/>
    /// pattern via real Sqlite temp files).
    /// </summary>
    public class VoiceIntelligencePersistenceTests
    {
        private static readonly DateTime WindowFrom = new(2000, 1, 1, 0, 0, 0);
        private static readonly DateTime WindowTo = new(2100, 1, 1, 0, 0, 0);

        // ── 1. Round-trip: all eight scores survive save→read (in-memory) ──────────
        [Fact]
        public async Task SessionScores_RoundTripThroughInMemoryRepository()
        {
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            var day = new DateOnly(2026, 6, 1);
            var startedAt = day.ToDateTime(new TimeOnly(9, 0));

            await store.RecordSessionCompletedAsync(ScoredSession(7, startedAt,
                resonance: 82, comfort: 91, consistency: 77, intonation: 64,
                vocalWeight: 58, recovery: 88, pitch: 49, composite: 79.5));

            var sessions = await repository.GetSessionsAsync(1, WindowFrom, WindowTo);
            var s = Assert.Single(sessions);

            Assert.Equal(82, s.ResonanceScore100, 3);
            Assert.Equal(91, s.ComfortScore100, 3);
            Assert.Equal(77, s.ConsistencyScore100, 3);
            Assert.Equal(64, s.IntonationScore100, 3);
            Assert.Equal(58, s.VocalWeightScore100, 3);
            Assert.Equal(88, s.RecoveryScore100, 3);
            Assert.Equal(49, s.PitchScore100, 3);
            Assert.Equal(79.5, s.CompositeVoiceScore, 3);
        }

        // ── 2. Round-trip through real SQLite (durable across store instances) ─────
        [Fact]
        public async Task SessionScores_RoundTripThroughSqlite_AcrossStoreInstances()
        {
            using var db = new TempDb();
            var startedAt = new DateOnly(2026, 6, 1).ToDateTime(new TimeOnly(10, 0));

            var first = new SessionAnalyticsStore(new SqliteSessionAnalyticsRepository(db.ConnectionString));
            await first.RecordSessionCompletedAsync(ScoredSession(44, startedAt,
                resonance: 70, comfort: 80, consistency: 60, intonation: 55,
                vocalWeight: 50, recovery: 65, pitch: 40, composite: 62.3));

            // Fresh repository instance ⇒ reads from disk, not from memory.
            var secondRepository = new SqliteSessionAnalyticsRepository(db.ConnectionString);
            var sessions = await secondRepository.GetSessionsAsync(1, WindowFrom, WindowTo);
            var s = Assert.Single(sessions);

            Assert.Equal(70, s.ResonanceScore100, 3);
            Assert.Equal(80, s.ComfortScore100, 3);
            Assert.Equal(40, s.PitchScore100, 3);
            Assert.Equal(62.3, s.CompositeVoiceScore, 3);
        }

        // ── 3. Scale preserved: a 0–100 score is NOT crushed to 0–1 by normalisation ─
        [Fact]
        public async Task SessionScores_KeepZeroToHundredScale_NotClampedToOne()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var startedAt = new DateOnly(2026, 6, 1).ToDateTime(new TimeOnly(11, 0));

            // A realistic 73.4 must survive intact — the Average*-fields are 0–1, but the
            // *Score100 fields are 0–100 and must use Clamp0To100, not Clamp01.
            await store.RecordSessionCompletedAsync(ScoredSession(9, startedAt,
                resonance: 73.4, comfort: 73.4, consistency: 73.4, intonation: 73.4,
                vocalWeight: 73.4, recovery: 73.4, pitch: 73.4, composite: 73.4));

            var trend = await store.GetVoiceIntelligenceTrendAsync(WindowFrom, WindowTo);
            var p = Assert.Single(trend);

            Assert.Equal(73.4, p.ResonanceScore100, 3);
            Assert.Equal(73.4, p.CompositeVoiceScore, 3);
            Assert.True(p.ResonanceScore100 > 1.0, "0–100 score must not be crushed onto the 0–1 scale.");
        }

        // ── 4. Out-of-range scores clamp to [0,100] (not to [0,1]) ────────────────
        [Fact]
        public async Task SessionScores_OutOfRangeValues_ClampToZeroHundred()
        {
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            var startedAt = new DateOnly(2026, 6, 1).ToDateTime(new TimeOnly(12, 0));

            await store.RecordSessionCompletedAsync(ScoredSession(11, startedAt,
                resonance: 140, comfort: -20, consistency: double.NaN,
                intonation: double.PositiveInfinity, vocalWeight: 50, recovery: 50,
                pitch: 50, composite: 250));

            var sessions = await repository.GetSessionsAsync(1, WindowFrom, WindowTo);
            var s = Assert.Single(sessions);

            Assert.Equal(100, s.ResonanceScore100, 3);      // 140 → 100
            Assert.Equal(0, s.ComfortScore100, 3);          // -20 → 0
            Assert.Equal(0, s.ConsistencyScore100, 3);      // NaN → 0
            Assert.Equal(0, s.IntonationScore100, 3);       // +∞ → 0
            Assert.Equal(100, s.CompositeVoiceScore, 3);    // 250 → 100
        }

        // ── 5. ALTER migration is idempotent: EnsureSchema twice never throws ──────
        [Fact]
        public async Task AlterMigration_IsIdempotent_AcrossRepeatedSchemaEnsures()
        {
            using var db = new TempDb();
            var startedAt = new DateOnly(2026, 6, 1).ToDateTime(new TimeOnly(13, 0));

            // Each ctor calls EnsureSchema → the ALTER path runs three times against the
            // same DB. A non-idempotent migration would throw "duplicate column".
            _ = new SqliteSessionAnalyticsRepository(db.ConnectionString);
            _ = new SqliteSessionAnalyticsRepository(db.ConnectionString);
            var third = new SqliteSessionAnalyticsRepository(db.ConnectionString);

            var store = new SessionAnalyticsStore(third);
            await store.RecordSessionCompletedAsync(ScoredSession(5, startedAt,
                resonance: 60, comfort: 60, consistency: 60, intonation: 60,
                vocalWeight: 60, recovery: 60, pitch: 60, composite: 60));

            var sessions = await third.GetSessionsAsync(1, WindowFrom, WindowTo);
            Assert.Equal(60, Assert.Single(sessions).ResonanceScore100, 3);
        }

        // ── 6. Legacy DB (no score columns) is healed without data loss ───────────
        [Fact]
        public async Task LegacyDatabaseWithoutScoreColumns_IsHealedAndOldRowsReadAsZero()
        {
            using var db = new TempDb();
            var startedAt = new DateOnly(2026, 6, 1).ToDateTime(new TimeOnly(14, 0));

            // Build a "pre-Sprint-B" sessions table WITHOUT the eight score columns and
            // seed one legacy row (the old INSERT shape).
            using (var connection = new SqliteConnection(db.ConnectionString))
            {
                connection.Open();
                using var create = connection.CreateCommand();
                create.CommandText = @"
                    CREATE TABLE SessionAnalyticsSessions (
                        SessionId INTEGER PRIMARY KEY,
                        UserId INTEGER NOT NULL DEFAULT 1,
                        StartedAt TEXT NOT NULL,
                        EndedAt TEXT,
                        ExerciseCount INTEGER NOT NULL DEFAULT 0,
                        AverageResonance REAL NOT NULL DEFAULT 0,
                        AverageStability REAL NOT NULL DEFAULT 0,
                        AveragePitchComfort REAL NOT NULL DEFAULT 0,
                        AverageHealthScore REAL NOT NULL DEFAULT 0,
                        SafetyEventsCount INTEGER NOT NULL DEFAULT 0,
                        PauseRecommendationsCount INTEGER NOT NULL DEFAULT 0,
                        HydrationSuggestionsCount INTEGER NOT NULL DEFAULT 0,
                        FatigueIndicatorCount INTEGER NOT NULL DEFAULT 0
                    );";
                create.ExecuteNonQuery();

                using var seed = connection.CreateCommand();
                seed.CommandText = @"
                    INSERT INTO SessionAnalyticsSessions
                        (SessionId, UserId, StartedAt, EndedAt, ExerciseCount, AverageResonance)
                    VALUES (1, 1, @StartedAt, @EndedAt, 1, 0.66);";
                seed.Parameters.AddWithValue("@StartedAt", startedAt.ToString("o"));
                seed.Parameters.AddWithValue("@EndedAt", startedAt.AddMinutes(10).ToString("o"));
                seed.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            // Opening the repository runs EnsureSchema → the ALTER migration heals the table.
            var repository = new SqliteSessionAnalyticsRepository(db.ConnectionString);
            var store = new SessionAnalyticsStore(repository);
            var sessions = await repository.GetSessionsAsync(1, WindowFrom, WindowTo);
            var legacy = Assert.Single(sessions);

            // Old data is intact …
            Assert.Equal(0.66, legacy.AverageResonance, 3);
            Assert.Equal(1, legacy.ExerciseCount);
            // … and the newly-added score columns read as 0 (not computed for legacy rows).
            Assert.Equal(0, legacy.ResonanceScore100, 3);
            Assert.Equal(0, legacy.CompositeVoiceScore, 3);

            // And new rows can now be written with scores.
            await store.RecordSessionCompletedAsync(ScoredSession(2, startedAt.AddHours(1),
                resonance: 88, comfort: 88, consistency: 88, intonation: 88,
                vocalWeight: 88, recovery: 88, pitch: 88, composite: 88));
            var after = await store.GetVoiceIntelligenceTrendAsync(WindowFrom, WindowTo);
            Assert.Equal(2, after.Count);
            Assert.Equal(88, after.Single(p => p.SessionId == 2).ResonanceScore100, 3);
        }

        // ── 7. Missing scores default to 0 without crashing ───────────────────────
        [Fact]
        public async Task MissingScores_DefaultToZero_WithoutCrash()
        {
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            var startedAt = new DateOnly(2026, 6, 1).ToDateTime(new TimeOnly(15, 0));

            // A record built the old way (no score fields set) must persist & read as 0.
            await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = 3,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(5),
                ExerciseCount = 1,
                AverageResonance = 0.5
            });

            var sessions = await repository.GetSessionsAsync(1, WindowFrom, WindowTo);
            var s = Assert.Single(sessions);
            Assert.Equal(0, s.ResonanceScore100, 3);
            Assert.Equal(0, s.ComfortScore100, 3);
            Assert.Equal(0, s.CompositeVoiceScore, 3);
        }

        // ── 8. Trend read is chronological ────────────────────────────────────────
        [Fact]
        public async Task VoiceIntelligenceTrend_ReturnsChronologicalOrder()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var day = new DateOnly(2026, 6, 1);

            // Insert out of order; expect chronological read.
            await store.RecordSessionCompletedAsync(ScoredSession(30, day.ToDateTime(new TimeOnly(12, 0)),
                resonance: 60, comfort: 60, consistency: 60, intonation: 60,
                vocalWeight: 60, recovery: 60, pitch: 60, composite: 60));
            await store.RecordSessionCompletedAsync(ScoredSession(10, day.ToDateTime(new TimeOnly(9, 0)),
                resonance: 50, comfort: 50, consistency: 50, intonation: 50,
                vocalWeight: 50, recovery: 50, pitch: 50, composite: 50));
            await store.RecordSessionCompletedAsync(ScoredSession(20, day.ToDateTime(new TimeOnly(10, 30)),
                resonance: 70, comfort: 70, consistency: 70, intonation: 70,
                vocalWeight: 70, recovery: 70, pitch: 70, composite: 70));

            var trend = await store.GetVoiceIntelligenceTrendAsync(WindowFrom, WindowTo);

            Assert.Equal(3, trend.Count);
            Assert.Equal(new[] { 10, 20, 30 }, trend.Select(p => p.SessionId).ToArray());
            Assert.True(trend[0].StartedAt < trend[1].StartedAt && trend[1].StartedAt < trend[2].StartedAt);
            // Kronologisk: session 10 @ 09:00 (composite 50), 20 @ 10:30 (70), 30 @ 12:00 (60).
            // trend[2] er den siste i TID (session 30) = 60 — ikke den med høyest sessionId.
            Assert.Equal(50, trend[0].CompositeVoiceScore, 3);
            Assert.Equal(60, trend[2].CompositeVoiceScore, 3);
        }

        // ── 9. Trend honours the user/time window filter ──────────────────────────
        [Fact]
        public async Task VoiceIntelligenceTrend_FiltersByUserAndWindow()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var day = new DateOnly(2026, 6, 1);
            var inWindow = day.ToDateTime(new TimeOnly(9, 0));

            await store.RecordSessionCompletedAsync(ScoredSession(1, inWindow, 60, 60, 60, 60, 60, 60, 60, 60, userId: 1));
            await store.RecordSessionCompletedAsync(ScoredSession(2, inWindow, 70, 70, 70, 70, 70, 70, 70, 70, userId: 2));

            var user1 = await store.GetVoiceIntelligenceTrendAsync(
                day.ToDateTime(TimeOnly.MinValue), day.ToDateTime(TimeOnly.MinValue).AddDays(1), userId: 1);

            Assert.Single(user1);
            Assert.Equal(1, user1[0].SessionId);
        }

        // ── 10. End-to-end: the real recorder writes scores at session end ────────
        [Fact]
        public async Task ExerciseSessionRecorder_PersistsVoiceIntelligenceScores_OnCompletion()
        {
            var coordinator = new ExerciseIntelligenceCoordinator();
            var supervisor = new VocalHealthSupervisor();
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            using var recorder = new ExerciseSessionRecorder(coordinator, supervisor, store);

            coordinator.StartExercise(ExerciseTargetProfile.ResonanceExercise(), 1);
            recorder.BeginSession(exerciseId: 7, sessionId: 900, userId: 1);

            // A few healthy, high-resonance, in-comfort ticks.
            coordinator.UpdateMetrics(resonanceScore: 0.9, pitch: 200, stability: 0.9, health: 100);
            Thread.Sleep(150);
            coordinator.UpdateMetrics(resonanceScore: 0.85, pitch: 200, stability: 0.85, health: 100);
            Thread.Sleep(150);
            coordinator.UpdateMetrics(resonanceScore: 0.95, pitch: 200, stability: 0.95, health: 100);

            recorder.CompleteSession();
            if (recorder.LastPersistTask is { } persist) await persist;

            var sessions = await repository.GetSessionsAsync(1, WindowFrom, WindowTo);
            var s = sessions.Single(x => x.SessionId == 900);

            // Resonance/Consistency derive from the (high) aggregates ⇒ clearly > 50.
            Assert.True(s.ResonanceScore100 > 50, $"Resonance was {s.ResonanceScore100}");
            Assert.True(s.ConsistencyScore100 > 50, $"Consistency was {s.ConsistencyScore100}");
            // Comfort is high (in-zone) ⇒ > 50.
            Assert.True(s.ComfortScore100 > 50, $"Comfort was {s.ComfortScore100}");
            // No fatigue/strain/locks ⇒ Recovery stays high.
            Assert.True(s.RecoveryScore100 >= 75, $"Recovery was {s.RecoveryScore100}");
            // Intonation stays neutral: a flat (constant) pitch contour has range 0.
            // VocalWeight stays neutral: the parameterless UpdateMetrics path carries no
            // formants (no F1/centroid). Both are intended, not gaps.
            Assert.Equal(50, s.IntonationScore100, 0);
            Assert.Equal(50, s.VocalWeightScore100, 0);
            // Pitch is now a REAL measurement (Bølge 2 signal-wiring): the measured F0
            // is aggregated and scored, so it no longer falls back to neutral 50.
            Assert.InRange(s.PitchScore100, 1, 100);
            Assert.True(System.Math.Abs(s.PitchScore100 - 50) > 0.001,
                $"Pitch should be a real measurement, not the neutral fallback, but was {s.PitchScore100}");
            // Composite is a real measurement on the 0–100 scale.
            Assert.InRange(s.CompositeVoiceScore, 1, 100);

            // And it shows up in the trend read.
            var trend = await store.GetVoiceIntelligenceTrendAsync(WindowFrom, WindowTo);
            Assert.Contains(trend, p => p.SessionId == 900 && p.CompositeVoiceScore > 0);
        }

        // ── 11. Composite math for a clean, fully-measured-where-possible session ──
        [Fact]
        public void VoiceIntelligenceScorer_CleanSession_ComputesExpectedComposite()
        {
            // Perfect resonance/comfort/consistency, fresh recovery, the three unmeasured
            // axes neutral (50). Composite = 100*(0.22+0.18+0.15+0.15) + 50*(0.12+0.10+0.08)
            //                              = 100*0.70 + 50*0.30 = 70 + 15 = 85.
            var scorer = new VoiceIntelligenceScorer();
            var input = VoiceIntelligenceInput.Empty() with
            {
                AverageResonance01 = 1.0,
                ComfortCompliance01 = 1.0,
                ComfortBreaches = 0,
                AverageStability01 = 1.0,
                Recovery = new RecoveryScoreInput()
            };

            var scores = scorer.Compute(input);

            Assert.Equal(100, scores.Resonance.Score, 3);
            Assert.Equal(100, scores.Comfort.Score, 3);
            Assert.Equal(100, scores.Consistency.Score, 3);
            Assert.Equal(100, scores.Recovery.Score, 3);
            Assert.Equal(50, scores.Intonation.Score, 3);
            Assert.Equal(50, scores.VocalWeight.Score, 3);
            Assert.Equal(50, scores.Pitch.Score, 3);
            Assert.Equal(85, scores.CompositeVoiceScore, 3);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static SessionAnalyticsRecord ScoredSession(
            int sessionId, DateTime startedAt,
            double resonance, double comfort, double consistency, double intonation,
            double vocalWeight, double recovery, double pitch, double composite,
            int userId = 1)
        {
            return new SessionAnalyticsRecord
            {
                SessionId = sessionId,
                UserId = userId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(10),
                ExerciseCount = 1,
                AverageResonance = 0.7,
                AverageStability = 0.6,
                AveragePitchComfort = 0.7,
                AverageHealthScore = 0.8,
                ResonanceScore100 = resonance,
                ComfortScore100 = comfort,
                ConsistencyScore100 = consistency,
                IntonationScore100 = intonation,
                VocalWeightScore100 = vocalWeight,
                RecoveryScore100 = recovery,
                PitchScore100 = pitch,
                CompositeVoiceScore = composite
            };
        }

        /// <summary>Disposable temp SQLite database file (real DB, no mocks).</summary>
        private sealed class TempDb : IDisposable
        {
            public string Path { get; }
            public string ConnectionString { get; }

            public TempDb()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"femvoice-vi-{Guid.NewGuid():N}.db");
                ConnectionString = $"Data Source={Path}";
            }

            public void Dispose()
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
            }
        }
    }
}
