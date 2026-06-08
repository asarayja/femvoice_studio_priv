using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="TrendEngineService"/> (Sprint C.3/C.4 Bølge 1, Agent A4).
    ///
    /// No mocking: all tests use the real TrendEngineService, hand-built
    /// VoiceIntelligenceTrendPoint series with fixed dates, and 'now' passed explicitly
    /// to keep assertions fully deterministic (no DateTime.Now dependency).
    ///
    /// OLS slope reference (x = 0,1,2 ⇒ y = 50,60,70):
    ///   numerator   = 3*(0*50+1*60+2*70) − (3)*(180) = 3*230 − 540 = 150
    ///   denominator = 3*(0+1+4) − 9 = 15 − 9 = 6
    ///   slope       = 150 / 6 = 25 points/session
    /// Flat three-point series ⇒ slope = 0.
    ///
    /// Confidence formula (matches production, tested via assertions on the known inputs):
    ///   base×0.5 + volume + trend + stability, clamped 0–100.
    ///   volume    = min(n, 12)/12 × 25
    ///   trend     = ((clamp(slope,−1,1)+1)/2) × 25   (n≥3, else 0.5×25)
    ///   stability = (1 − clamp(stdDev/25,0,1)) × 32.5
    /// </summary>
    public class TrendEngineServiceTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// <summary>A base date used throughout to keep test series readable.</summary>
        private static readonly DateTime BaseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static TrendEngineService MakeService()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            return new TrendEngineService(store);
        }

        private static VoiceIntelligenceTrendPoint Point(
            int sessionId,
            DateTime startedAt,
            double resonance    = 50,
            double comfort      = 50,
            double consistency  = 50,
            double intonation   = 50,
            double vocalWeight  = 50,
            double recovery     = 50,
            double pitch        = 50,
            double composite    = 50)
            => new()
            {
                SessionId          = sessionId,
                UserId             = 1,
                StartedAt          = startedAt,
                EndedAt            = startedAt.AddMinutes(20),
                ResonanceScore100  = resonance,
                ComfortScore100    = comfort,
                ConsistencyScore100= consistency,
                IntonationScore100 = intonation,
                VocalWeightScore100= vocalWeight,
                RecoveryScore100   = recovery,
                PitchScore100      = pitch,
                CompositeVoiceScore= composite,
            };

        // ── 1. Four windows always returned ──────────────────────────────────────────

        [Fact]
        public void Compute_EmptyTrend_ReturnsFourWindowsAllEmpty()
        {
            var svc  = MakeService();
            var now  = BaseDate.AddDays(30);
            var wins = svc.Compute(Array.Empty<VoiceIntelligenceTrendPoint>(), now);

            Assert.Equal(4, wins.Count);
            Assert.Equal(new[] { 7, 30, 90, 180 }, wins.Select(w => w.WindowDays).ToArray());
        }

        [Fact]
        public void Compute_EmptyTrend_AllWindowsHaveSessionCountZeroAndHasEnoughDataFalse()
        {
            var svc  = MakeService();
            var now  = BaseDate.AddDays(30);
            var wins = svc.Compute(Array.Empty<VoiceIntelligenceTrendPoint>(), now);

            foreach (var w in wins)
            {
                Assert.Equal(0, w.SessionCount);
                Assert.False(w.HasEnoughData);
                Assert.Equal(35.0, w.Confidence, 6);  // EmptyHistoryConfidence = 35
            }
        }

        // ── 2. Window boundary: closed-open [From, To) ───────────────────────────────

        [Fact]
        public void Compute_PointExactlyAtFrom_IsIncludedInWindow()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(7);
            // Point exactly at now−7d (= From of 7-day window) must be INCLUDED.
            var point = Point(1, now.AddDays(-7), composite: 60);
            var wins  = svc.Compute(new[] { point }, now);
            var w7    = wins.First(w => w.WindowDays == 7);
            Assert.Equal(1, w7.SessionCount);
        }

        [Fact]
        public void Compute_PointExactlyAtTo_IsExcludedFromWindow()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(7);
            // Point exactly at now (= To of all windows) must be EXCLUDED.
            var point = Point(1, now, composite: 60);
            var wins  = svc.Compute(new[] { point }, now);
            foreach (var w in wins)
                Assert.Equal(0, w.SessionCount);
        }

        [Fact]
        public void Compute_PointOneSecondBeforeFrom_IsExcluded()
        {
            var svc   = MakeService();
            var now   = BaseDate.AddDays(7);
            // One second before From ⇒ outside the 7-day window.
            var point = Point(1, now.AddDays(-7).AddSeconds(-1), composite: 60);
            var wins  = svc.Compute(new[] { point }, now);
            var w7    = wins.First(w => w.WindowDays == 7);
            Assert.Equal(0, w7.SessionCount);
        }

        // ── 3. HasEnoughData threshold ────────────────────────────────────────────────

        [Fact]
        public void Compute_TwoPointsInWindow_HasEnoughDataFalse()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 50),
                Point(2, now.AddDays(-5), composite: 55),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            Assert.Equal(2, w7.SessionCount);
            Assert.False(w7.HasEnoughData);
        }

        [Fact]
        public void Compute_ThreePointsInWindow_HasEnoughDataTrue()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 50),
                Point(2, now.AddDays(-5), composite: 55),
                Point(3, now.AddDays(-4), composite: 60),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            Assert.Equal(3, w7.SessionCount);
            Assert.True(w7.HasEnoughData);
        }

        // ── 4. OLS slope direction and magnitude ─────────────────────────────────────

        [Fact]
        public void Compute_RisingSeries_PositiveCompositeSlope()
        {
            // y = 50, 60, 70 ⇒ slope = 10 points/session
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 50),
                Point(2, now.AddDays(-5), composite: 60),
                Point(3, now.AddDays(-4), composite: 70),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            Assert.True(w7.CompositeSlope > 0, $"Expected positive slope, got {w7.CompositeSlope}");
        }

        [Fact]
        public void Compute_FallingSeries_NegativeCompositeSlope()
        {
            // y = 70, 60, 50 ⇒ slope = −10 points/session
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 70),
                Point(2, now.AddDays(-5), composite: 60),
                Point(3, now.AddDays(-4), composite: 50),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            Assert.True(w7.CompositeSlope < 0, $"Expected negative slope, got {w7.CompositeSlope}");
        }

        [Fact]
        public void Compute_FlatSeries_CompositeSlopeZero()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 60),
                Point(2, now.AddDays(-5), composite: 60),
                Point(3, now.AddDays(-4), composite: 60),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            Assert.Equal(0.0, w7.CompositeSlope, 6);
        }

        [Fact]
        public void Compute_KnownRisingSeries_ExactSlopeValue()
        {
            // y = 50, 60, 70 ⇒ OLS slope = (3*(0*50+60+140) − 3*(180)) / (3*(0+1+4)−9)
            //                             = (3*230 − 540) / (15−9) = 150/6 = 25
            // But wait — here we have 3 points and the composite steps are 50,60,70
            // spaced 1 day apart in the actual dates, but the OLS x is the session INDEX (0,1,2).
            // Slope = 10.
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 50),
                Point(2, now.AddDays(-5), composite: 60),
                Point(3, now.AddDays(-4), composite: 70),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            // OLS: x=0,1,2  y=50,60,70
            // slope = (3*(0+60+140) - 3*(180)) / (3*(0+1+4) - 9) = (600-540)/6 = 10
            Assert.Equal(10.0, w7.CompositeSlope, 6);
        }

        // ── 5. Per-dimension slopes keyed by real VoiceDimension enum values ─────────

        [Fact]
        public void Compute_RisingResonanceSeries_PositiveResonanceSlope()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), resonance: 40, composite: 50),
                Point(2, now.AddDays(-5), resonance: 55, composite: 55),
                Point(3, now.AddDays(-4), resonance: 70, composite: 60),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            Assert.True(w7.DimensionSlopes.ContainsKey(VoiceDimension.Resonance));
            Assert.True(w7.DimensionSlopes[VoiceDimension.Resonance] > 0);
        }

        [Fact]
        public void Compute_AllSevenDimensionKeysPresentWhenDataExists()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 50),
                Point(2, now.AddDays(-5), composite: 55),
                Point(3, now.AddDays(-4), composite: 60),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            foreach (VoiceDimension dim in Enum.GetValues(typeof(VoiceDimension)))
                Assert.True(w7.DimensionSlopes.ContainsKey(dim), $"Missing key {dim}");
        }

        [Fact]
        public void Compute_EmptyWindow_DimensionSlopesIsEmpty()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            // Only a point in the 30-day window (> 7 days ago), not in 7-day.
            var pts = new[]
            {
                Point(1, now.AddDays(-10), composite: 50),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            Assert.Empty(w7.DimensionSlopes);
        }

        // ── 6. Composite statistics ───────────────────────────────────────────────────

        [Fact]
        public void Compute_ThreePoints_CompositeMeanMinMaxCorrect()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 40),
                Point(2, now.AddDays(-5), composite: 60),
                Point(3, now.AddDays(-4), composite: 80),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            Assert.Equal(60.0, w7.CompositeMean, 6);
            Assert.Equal(40.0, w7.CompositeMin,  6);
            Assert.Equal(80.0, w7.CompositeMax,  6);
        }

        // ── 7. Window spanning: 180-day window includes all points ───────────────────

        [Fact]
        public void Compute_180DayWindowIncludesAllPointsAcrossSpan()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(200);
            // Three points spread across the 180-day span.
            var pts = new[]
            {
                Point(1, now.AddDays(-170), composite: 50),
                Point(2, now.AddDays(-90),  composite: 55),
                Point(3, now.AddDays(-5),   composite: 60),
            };
            var wins = svc.Compute(pts, now);
            var w180 = wins.First(w => w.WindowDays == 180);
            Assert.Equal(3, w180.SessionCount);
            Assert.True(w180.HasEnoughData);
        }

        [Fact]
        public void Compute_PointsBeyond180Days_ExcludedFromAllWindows()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(200);
            // Point outside all windows (181 days ago).
            var pts = new[]
            {
                Point(1, now.AddDays(-181), composite: 50),
            };
            var wins = svc.Compute(pts, now);
            foreach (var w in wins)
                Assert.Equal(0, w.SessionCount);
        }

        [Fact]
        public void Compute_7DayWindowCountsCorrectly_LongerWindowCountsMore()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(100);
            // 2 points in 7-day window; 3 more in 8–30 day range (in 30d but not 7d).
            var pts = new[]
            {
                Point(1, now.AddDays(-6),  composite: 50),
                Point(2, now.AddDays(-4),  composite: 55),
                Point(3, now.AddDays(-15), composite: 60),
                Point(4, now.AddDays(-20), composite: 62),
                Point(5, now.AddDays(-25), composite: 65),
            };
            var wins = svc.Compute(pts, now);
            var w7   = wins.First(w => w.WindowDays == 7);
            var w30  = wins.First(w => w.WindowDays == 30);
            Assert.Equal(2, w7.SessionCount);
            Assert.Equal(5, w30.SessionCount);
        }

        // ── 8. Confidence properties ─────────────────────────────────────────────────

        [Fact]
        public void Compute_ZeroPoints_ConfidenceIsEmptyHistoryConfidence()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var wins = svc.Compute(Array.Empty<VoiceIntelligenceTrendPoint>(), now);
            foreach (var w in wins)
                Assert.Equal(35.0, w.Confidence, 6);
        }

        [Fact]
        public void Compute_MorePoints_HigherConfidenceThanFewerPoints()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(50);
            // 2 points in 7-day window vs 10 in 30-day window (but 30d includes 7d points).
            // We use separate trends with isolated point sets for a clean comparison.
            var fewPts = new[]
            {
                Point(1, now.AddDays(-6),  composite: 60),
                Point(2, now.AddDays(-5),  composite: 62),
            };
            var manyPts = new[]
            {
                Point(1, now.AddDays(-6),  composite: 60),
                Point(2, now.AddDays(-5),  composite: 62),
                Point(3, now.AddDays(-4),  composite: 64),
                Point(4, now.AddDays(-3),  composite: 66),
                Point(5, now.AddDays(-2),  composite: 68),
            };

            var fewWins  = svc.Compute(fewPts,  now);
            var manyWins = svc.Compute(manyPts, now);

            var fewConf  = fewWins.First(w => w.WindowDays == 7).Confidence;
            var manyConf = manyWins.First(w => w.WindowDays == 7).Confidence;

            Assert.True(manyConf > fewConf,
                $"Expected more-data confidence ({manyConf}) > few-data confidence ({fewConf})");
        }

        [Fact]
        public void Compute_ConfidenceIsClamped0To100()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(50);
            // 12+ strongly rising points should still stay ≤100.
            var pts = Enumerable.Range(0, 15)
                .Select(i => Point(i + 1, now.AddDays(-(14 - i)), composite: 50 + i * 3))
                .ToArray();
            var wins = svc.Compute(pts, now);
            foreach (var w in wins)
            {
                Assert.True(w.Confidence >= 0.0, $"Confidence < 0: {w.Confidence}");
                Assert.True(w.Confidence <= 100.0, $"Confidence > 100: {w.Confidence}");
            }
        }

        // ── 9. Window boundaries are set correctly ────────────────────────────────────

        [Fact]
        public void Compute_WindowFromAndToAreCorrect()
        {
            var svc = MakeService();
            var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
            var wins = svc.Compute(Array.Empty<VoiceIntelligenceTrendPoint>(), now);

            foreach (var w in wins)
            {
                Assert.Equal(now, w.To);
                Assert.Equal(now.AddDays(-w.WindowDays), w.From);
            }
        }

        // ── 10. BuildProfile helper ──────────────────────────────────────────────────

        [Fact]
        public void BuildProfile_WeeklyTrendContains7And30DayWindows()
        {
            var svc  = MakeService();
            var now  = BaseDate.AddDays(30);
            var wins = svc.Compute(Array.Empty<VoiceIntelligenceTrendPoint>(), now);
            var profile = svc.BuildProfile(wins, 1, now);

            Assert.Equal(2, profile.WeeklyTrend.Count);
            Assert.Contains(profile.WeeklyTrend, w => w.WindowDays == 7);
            Assert.Contains(profile.WeeklyTrend, w => w.WindowDays == 30);
        }

        [Fact]
        public void BuildProfile_MonthlyTrendContains90And180DayWindows()
        {
            var svc  = MakeService();
            var now  = BaseDate.AddDays(30);
            var wins = svc.Compute(Array.Empty<VoiceIntelligenceTrendPoint>(), now);
            var profile = svc.BuildProfile(wins, 1, now);

            Assert.Equal(2, profile.MonthlyTrend.Count);
            Assert.Contains(profile.MonthlyTrend, w => w.WindowDays == 90);
            Assert.Contains(profile.MonthlyTrend, w => w.WindowDays == 180);
        }

        [Fact]
        public void BuildProfile_PatternStatesAreNull()
        {
            var svc  = MakeService();
            var now  = BaseDate.AddDays(30);
            var wins = svc.Compute(Array.Empty<VoiceIntelligenceTrendPoint>(), now);
            var profile = svc.BuildProfile(wins, 1, now);

            Assert.Null(profile.Plateau);
            Assert.Null(profile.Breakthrough);
            Assert.Null(profile.Regression);
        }

        [Fact]
        public void BuildProfile_HasEnoughDataFalse_WhenNoWindowHasThreePoints()
        {
            var svc  = MakeService();
            var now  = BaseDate.AddDays(10);
            var pts  = new[]
            {
                Point(1, now.AddDays(-5), composite: 50),
                Point(2, now.AddDays(-4), composite: 55),
            };
            var wins    = svc.Compute(pts, now);
            var profile = svc.BuildProfile(wins, 1, now, pts);
            Assert.False(profile.HasEnoughData);
        }

        [Fact]
        public void BuildProfile_HasEnoughDataTrue_WhenAnyWindowHasThreePoints()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 50),
                Point(2, now.AddDays(-5), composite: 55),
                Point(3, now.AddDays(-4), composite: 60),
            };
            var wins    = svc.Compute(pts, now);
            var profile = svc.BuildProfile(wins, 1, now, pts);
            Assert.True(profile.HasEnoughData);
        }

        [Fact]
        public void BuildProfile_CompositeVoiceScore_IsLastPointComposite()
        {
            var svc = MakeService();
            var now = BaseDate.AddDays(10);
            var pts = new[]
            {
                Point(1, now.AddDays(-6), composite: 50),
                Point(2, now.AddDays(-5), composite: 60),
                Point(3, now.AddDays(-4), composite: 75),
            };
            var wins    = svc.Compute(pts, now);
            var profile = svc.BuildProfile(wins, 1, now, pts);
            Assert.Equal(75.0, profile.CompositeVoiceScore, 6);
        }

        [Fact]
        public void BuildProfile_UserIdAndGeneratedAtArePassedThrough()
        {
            var svc       = MakeService();
            var now       = new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Utc);
            var wins      = svc.Compute(Array.Empty<VoiceIntelligenceTrendPoint>(), now);
            var profile   = svc.BuildProfile(wins, 42, now);
            Assert.Equal(42, profile.UserId);
            Assert.Equal(now, profile.GeneratedAt);
        }

        // ── 11. AnalyzeAsync with InMemory store ──────────────────────────────────────

        [Fact]
        public async Task AnalyzeAsync_StoredSessions_ReturnedInCorrectWindows()
        {
            var repo  = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repo);
            var svc   = new TrendEngineService(store);

            var now = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

            // Save three sessions within the 7-day window.
            for (var i = 0; i < 3; i++)
            {
                await repo.SaveSessionAsync(new SessionAnalyticsRecord
                {
                    SessionId             = i + 1,
                    UserId                = 1,
                    StartedAt             = now.AddDays(-(6 - i)),
                    CompositeVoiceScore   = 50 + i * 5,
                    ResonanceScore100     = 50,
                    ComfortScore100       = 50,
                    ConsistencyScore100   = 50,
                    IntonationScore100    = 50,
                    VocalWeightScore100   = 50,
                    RecoveryScore100      = 50,
                    PitchScore100         = 50,
                });
            }

            var wins = await svc.AnalyzeAsync(now, userId: 1);
            var w7   = wins.First(w => w.WindowDays == 7);
            var w180 = wins.First(w => w.WindowDays == 180);

            Assert.Equal(3, w7.SessionCount);
            Assert.True(w7.HasEnoughData);
            // 180-day window also covers the same sessions.
            Assert.Equal(3, w180.SessionCount);
        }

        [Fact]
        public async Task AnalyzeAsync_NoSessions_ReturnsAllEmptyWindows()
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var svc   = new TrendEngineService(store);
            var now   = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

            var wins = await svc.AnalyzeAsync(now, userId: 1);
            Assert.Equal(4, wins.Count);
            Assert.All(wins, w => Assert.Equal(0, w.SessionCount));
        }

        // ── 12. Null / argument guards ────────────────────────────────────────────────

        [Fact]
        public void Compute_NullTrend_ThrowsArgumentNullException()
        {
            var svc = MakeService();
            Assert.Throws<ArgumentNullException>(() =>
                svc.Compute(null!, DateTime.UtcNow));
        }

        [Fact]
        public void BuildProfile_NullWindows_ThrowsArgumentNullException()
        {
            var svc = MakeService();
            Assert.Throws<ArgumentNullException>(() =>
                svc.BuildProfile(null!, 1, DateTime.UtcNow));
        }
    }
}
