using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Unit tests for <see cref="LongitudinalInsightEngine"/>. No mocking — the engine is
    /// a pure function over hand-built <see cref="TrendWindow"/> / <see cref="VoiceDevelopmentProfile"/>
    /// records.
    ///
    /// ALL assertions are on <see cref="LongitudinalInsight.ReasonCode"/>,
    /// <see cref="LongitudinalInsight.Dimension"/>, <see cref="LongitudinalInsight.Confidence"/>,
    /// and <see cref="LongitudinalInsight.Evidence"/>. NEVER on localised What/Why strings —
    /// those are owned and scanned by RES-Strings.
    ///
    /// Coverage:
    ///   • Improvement: dimension with slope ≥ +1.0 ⇒ ReasonCode "IMPROVEMENT".
    ///   • Decline: dimension with slope ≤ −1.0 ⇒ ReasonCode "DECLINE".
    ///   • Neutral filter: |slope| &lt; 1.0, no pattern ⇒ no insight emitted for that dimension.
    ///   • Ranking: strongest |delta| first; Recovery &gt; Comfort tie-break.
    ///   • Insufficient data: no window has HasEnoughData ⇒ ReasonCode "INSUFFICIENT_DATA".
    ///   • Pattern overrides: Plateau / Breakthrough / Regression override slope-based codes.
    ///   • Evidence fields are present and contain window/slope/sessions tokens.
    ///   • Confidence increases with session count and window width.
    ///   • Empty windows + no patterns ⇒ empty list (no crash).
    ///   • Regression pattern takes priority over Plateau for the same dimension.
    /// </summary>
    public class LongitudinalInsightEngineTests
    {
        private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly ILocalizationService Loc = new TestLocalizationService();
        private static LongitudinalInsightEngine NewEngine() => new(Loc);

        // ── Helpers ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a TrendWindow with explicit per-dimension slopes and a given session count.
        /// HasEnoughData = sessionCount >= 3.
        /// </summary>
        private static TrendWindow Window(
            int windowDays,
            int sessionCount,
            IReadOnlyDictionary<VoiceDimension, double> slopes,
            double compositeSlope = 0.0,
            double compositeMean = 50.0,
            double compositeMin = 40.0,
            double compositeMax = 60.0)
        {
            var from = Epoch;
            var to = Epoch.AddDays(windowDays);
            return new TrendWindow
            {
                WindowDays = windowDays,
                From = from,
                To = to,
                DimensionSlopes = slopes,
                CompositeSlope = compositeSlope,
                CompositeMean = compositeMean,
                CompositeMin = compositeMin,
                CompositeMax = compositeMax,
                SessionCount = sessionCount,
                Confidence = 50.0,
                HasEnoughData = sessionCount >= 3,
            };
        }

        private static VoiceDevelopmentProfile EmptyProfile(int userId = 1) =>
            new VoiceDevelopmentProfile
            {
                UserId = userId,
                GeneratedAt = Epoch,
                WeeklyTrend = Array.Empty<TrendWindow>(),
                MonthlyTrend = Array.Empty<TrendWindow>(),
                Plateau = null,
                Breakthrough = null,
                Regression = null,
                CompositeVoiceScore = 50.0,
                HasEnoughData = false,
            };

        private static VoiceDevelopmentProfile ProfileWith(
            IReadOnlyList<TrendWindow> windows,
            PlateauState? plateau = null,
            BreakthroughState? breakthrough = null,
            RegressionState? regression = null,
            double compositeScore = 50.0,
            bool hasEnoughData = true) =>
            new VoiceDevelopmentProfile
            {
                UserId = 1,
                GeneratedAt = Epoch,
                WeeklyTrend = windows.Where(w => w.WindowDays <= 30).ToList(),
                MonthlyTrend = windows.Where(w => w.WindowDays > 30).ToList(),
                Plateau = plateau,
                Breakthrough = breakthrough,
                Regression = regression,
                CompositeVoiceScore = compositeScore,
                HasEnoughData = hasEnoughData,
            };

        private static Dictionary<VoiceDimension, double> SingleSlope(VoiceDimension dim, double slope) =>
            new() { [dim] = slope };

        private static Dictionary<VoiceDimension, double> Slopes(params (VoiceDimension dim, double slope)[] entries) =>
            entries.ToDictionary(e => e.dim, e => e.slope);

        // ── Tests ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Improvement_WhenResonanceSlopeAboveThreshold_EmitsImprovementInsight()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Resonance, 2.5);
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            var resonanceInsight = insights.FirstOrDefault(i => i.Dimension == VoiceDimension.Resonance);
            Assert.NotNull(resonanceInsight);
            Assert.Equal("IMPROVEMENT", resonanceInsight.ReasonCode);
        }

        [Fact]
        public void Decline_WhenComfortSlopeBelowNegativeThreshold_EmitsDeclineInsight()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Comfort, -1.8);
            var window = Window(30, sessionCount: 4, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            var comfortInsight = insights.FirstOrDefault(i => i.Dimension == VoiceDimension.Comfort);
            Assert.NotNull(comfortInsight);
            Assert.Equal("DECLINE", comfortInsight.ReasonCode);
        }

        [Fact]
        public void NeutralFilter_WhenSlopeBelowThreshold_NoDimensionInsightEmitted()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Pitch, 0.4);   // |slope| < 1.0
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            Assert.DoesNotContain(insights, i => i.Dimension == VoiceDimension.Pitch);
        }

        [Fact]
        public void Ranking_StrongerDeltaFirst()
        {
            var engine = NewEngine();
            var slopes = Slopes(
                (VoiceDimension.Resonance, 1.5),
                (VoiceDimension.Intonation, 3.2));
            var window = Window(30, sessionCount: 6, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            // Intonation has slope 3.2, Resonance 1.5 → Intonation should come first.
            var first = insights.First();
            Assert.Equal(VoiceDimension.Intonation, first.Dimension);
        }

        [Fact]
        public void TieBreak_RecoveryBeforeComfortWhenEqualSlope()
        {
            var engine = NewEngine();
            // Both with exactly 2.0 — Recovery (=0) should beat Comfort (=1).
            var slopes = Slopes(
                (VoiceDimension.Comfort, 2.0),
                (VoiceDimension.Recovery, 2.0));
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            var first = insights.First();
            Assert.Equal(VoiceDimension.Recovery, first.Dimension);
        }

        [Fact]
        public void TieBreak_ComfortBeforeResonanceWhenEqualSlope()
        {
            var engine = NewEngine();
            var slopes = Slopes(
                (VoiceDimension.Resonance, 2.0),
                (VoiceDimension.Comfort, 2.0));
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            var dims = insights.Select(i => i.Dimension).ToList();
            var comfortIdx = dims.IndexOf(VoiceDimension.Comfort);
            var resonanceIdx = dims.IndexOf(VoiceDimension.Resonance);
            Assert.True(comfortIdx < resonanceIdx, "Comfort should rank before Resonance on equal slope.");
        }

        [Fact]
        public void InsufficientData_WhenNoWindowHasEnoughData_EmitsInsufficientDataReasonCode()
        {
            var engine = NewEngine();
            // Only 2 sessions ⇒ HasEnoughData = false.
            var slopes = SingleSlope(VoiceDimension.Resonance, 2.0);
            var window = Window(30, sessionCount: 2, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows, hasEnoughData: false);

            var insights = engine.Compute(windows, null, null, null, profile);

            Assert.All(insights, i => Assert.Equal("INSUFFICIENT_DATA", i.ReasonCode));
        }

        [Fact]
        public void PlateauOverride_PatternDimensionGetPlateauCode_EvenWithPositiveSlope()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Resonance, 1.5);
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var plateau = new PlateauState
            {
                ReasonCode = "PLATEAU_RESONANCE",
                Dimension = VoiceDimension.Resonance,
                SeverityScore = 60.0,
                WindowDays = 30,
                StartedAt = Epoch,
                PlateauDurationDays = 14,
                ObservedSlope = 0.1,
            };
            var profile = ProfileWith(windows, plateau: plateau);

            var insights = engine.Compute(windows, plateau, null, null, profile);

            var resonanceInsight = insights.First(i => i.Dimension == VoiceDimension.Resonance);
            Assert.Equal("PLATEAU", resonanceInsight.ReasonCode);
        }

        [Fact]
        public void BreakthroughOverride_PatternDimensionGetsBreakthroughCode()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Comfort, 1.2);
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var breakthrough = new BreakthroughState
            {
                ReasonCode = "BREAKTHROUGH_COMFORT",
                Dimension = VoiceDimension.Comfort,
                SeverityScore = 80.0,
                WindowDays = 30,
                DetectedAt = Epoch,
                MagnitudeDelta = 12.0,
            };
            var profile = ProfileWith(windows, breakthrough: breakthrough);

            var insights = engine.Compute(windows, null, breakthrough, null, profile);

            var comfortInsight = insights.First(i => i.Dimension == VoiceDimension.Comfort);
            Assert.Equal("BREAKTHROUGH", comfortInsight.ReasonCode);
        }

        [Fact]
        public void RegressionOverride_TakesPriorityOverPlateauForSameDimension()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.VocalWeight, -2.0);
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var plateau = new PlateauState
            {
                ReasonCode = "PLATEAU_VOCALWEIGHT",
                Dimension = VoiceDimension.VocalWeight,
                SeverityScore = 40.0,
                WindowDays = 30,
                StartedAt = Epoch,
                PlateauDurationDays = 7,
                ObservedSlope = -0.1,
            };
            var regression = new RegressionState
            {
                ReasonCode = "REGRESSION_VOCALWEIGHT",
                Dimension = VoiceDimension.VocalWeight,
                SeverityScore = 75.0,
                WindowDays = 30,
                DetectedAt = Epoch,
                DeclineSlope = -2.0,
                CompoundSeverity = 80.0,
            };
            var profile = ProfileWith(windows, plateau: plateau, regression: regression);

            var insights = engine.Compute(windows, plateau, null, regression, profile);

            var vocalWeightInsight = insights.First(i => i.Dimension == VoiceDimension.VocalWeight);
            Assert.Equal("REGRESSION", vocalWeightInsight.ReasonCode);
        }

        [Fact]
        public void Evidence_ContainsWindowSlopeSessions()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Resonance, 2.0);
            var window = Window(30, sessionCount: 6, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            var resonanceInsight = insights.First(i => i.Dimension == VoiceDimension.Resonance);
            Assert.Contains(resonanceInsight.Evidence, e => e.StartsWith("window="));
            Assert.Contains(resonanceInsight.Evidence, e => e.StartsWith("slope="));
            Assert.Contains(resonanceInsight.Evidence, e => e.StartsWith("sessions="));
        }

        [Fact]
        public void Evidence_PlateauInsightContainsPlateauDuration()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Consistency, 0.0);
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var plateau = new PlateauState
            {
                ReasonCode = "PLATEAU_CONSISTENCY",
                Dimension = VoiceDimension.Consistency,
                SeverityScore = 55.0,
                WindowDays = 30,
                StartedAt = Epoch,
                PlateauDurationDays = 21,
                ObservedSlope = 0.05,
            };
            var profile = ProfileWith(windows, plateau: plateau);

            var insights = engine.Compute(windows, plateau, null, null, profile);

            var consistencyInsight = insights.First(i => i.Dimension == VoiceDimension.Consistency);
            Assert.Contains(consistencyInsight.Evidence, e => e.StartsWith("plateau_duration="));
        }

        [Fact]
        public void Evidence_RegressionInsightContainsRegressionSlope()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Resonance, -2.5);
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var regression = new RegressionState
            {
                ReasonCode = "REGRESSION_RESONANCE",
                Dimension = VoiceDimension.Resonance,
                SeverityScore = 70.0,
                WindowDays = 30,
                DetectedAt = Epoch,
                DeclineSlope = -2.5,
                CompoundSeverity = 75.0,
            };
            var profile = ProfileWith(windows, regression: regression);

            var insights = engine.Compute(windows, null, null, regression, profile);

            var resonanceInsight = insights.First(i => i.Dimension == VoiceDimension.Resonance);
            Assert.Contains(resonanceInsight.Evidence, e => e.StartsWith("regression_slope="));
            Assert.Contains(resonanceInsight.Evidence, e => e.StartsWith("regression_severity="));
        }

        [Fact]
        public void Confidence_IncreasesWithMoreSessions()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Resonance, 2.0);

            var windowFew = Window(30, sessionCount: 3, slopes);
            var windowMany = Window(30, sessionCount: 12, slopes);

            var insightsFew = engine.Compute(new[] { windowFew }, null, null, null,
                ProfileWith(new[] { windowFew }));
            var insightsMany = engine.Compute(new[] { windowMany }, null, null, null,
                ProfileWith(new[] { windowMany }));

            var confidenceFew = insightsFew.First(i => i.Dimension == VoiceDimension.Resonance).Confidence;
            var confidenceMany = insightsMany.First(i => i.Dimension == VoiceDimension.Resonance).Confidence;

            Assert.True(confidenceMany > confidenceFew,
                $"More sessions should yield higher confidence. Got few={confidenceFew:0.0}, many={confidenceMany:0.0}");
        }

        [Fact]
        public void Confidence_WiderWindowYieldsHigherConfidence()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Intonation, 1.5);

            var narrow = Window(7, sessionCount: 5, slopes);
            var wide = Window(180, sessionCount: 5, slopes);

            var insightsNarrow = engine.Compute(new[] { narrow }, null, null, null,
                ProfileWith(new[] { narrow }));
            var insightsWide = engine.Compute(new[] { wide }, null, null, null,
                ProfileWith(new[] { wide }));

            var confNarrow = insightsNarrow.First(i => i.Dimension == VoiceDimension.Intonation).Confidence;
            var confWide = insightsWide.First(i => i.Dimension == VoiceDimension.Intonation).Confidence;

            Assert.True(confWide > confNarrow,
                $"Wider window should yield higher confidence. Got narrow={confNarrow:0.0}, wide={confWide:0.0}");
        }

        [Fact]
        public void EmptyWindows_AndNoPatterns_ReturnsEmptyList()
        {
            var engine = NewEngine();
            var profile = EmptyProfile();

            var insights = engine.Compute(Array.Empty<TrendWindow>(), null, null, null, profile);

            Assert.Empty(insights);
        }

        [Fact]
        public void NullWindows_ThrowsArgumentNullException()
        {
            var engine = NewEngine();
            Assert.Throws<ArgumentNullException>(() =>
                engine.Compute(null!, null, null, null, EmptyProfile()));
        }

        [Fact]
        public void NullProfile_ThrowsArgumentNullException()
        {
            var engine = NewEngine();
            Assert.Throws<ArgumentNullException>(() =>
                engine.Compute(Array.Empty<TrendWindow>(), null, null, null, null!));
        }

        [Fact]
        public void MaxSevenInsights_ReturnedWhenManyDimensionsSignificant()
        {
            var engine = NewEngine();
            // All 7 dimensions with significant slopes.
            var slopes = Slopes(
                (VoiceDimension.Recovery, 3.0),
                (VoiceDimension.Comfort, 2.5),
                (VoiceDimension.Resonance, 2.0),
                (VoiceDimension.Consistency, 1.8),
                (VoiceDimension.Intonation, 1.5),
                (VoiceDimension.VocalWeight, 1.3),
                (VoiceDimension.Pitch, 1.1));
            var window = Window(30, sessionCount: 10, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            Assert.True(insights.Count <= 7, $"Expected at most 7 insights, got {insights.Count}.");
        }

        [Fact]
        public void WiderWindowWins_WhenMultipleWindowsHaveEnoughData()
        {
            var engine = NewEngine();
            // 7-day window: Resonance slope = 1.5 (enough data).
            // 30-day window: Resonance slope = 2.5 (also enough data — should win because it's widest).
            var slopesNarrow = SingleSlope(VoiceDimension.Resonance, 1.5);
            var slopesWide = SingleSlope(VoiceDimension.Resonance, 2.5);

            var narrow = Window(7, sessionCount: 3, slopesNarrow);
            var wide = Window(30, sessionCount: 6, slopesWide);
            var windows = new[] { narrow, wide };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            var resonanceInsight = insights.First(i => i.Dimension == VoiceDimension.Resonance);
            // The evidence should show window=30d (the wider one wins).
            Assert.Contains(resonanceInsight.Evidence, e => e == "window=30d");
        }

        [Fact]
        public void ConfidenceIsInRange_ZeroToOneHundred()
        {
            var engine = NewEngine();
            var slopes = SingleSlope(VoiceDimension.Resonance, 5.0);
            var window = Window(180, sessionCount: 20, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            Assert.All(insights, i =>
            {
                Assert.True(i.Confidence >= 0.0 && i.Confidence <= 100.0,
                    $"Confidence {i.Confidence} out of [0,100].");
            });
        }

        [Fact]
        public void PatternDimension_IncludedEvenIfSlopeBelowThreshold()
        {
            var engine = NewEngine();
            // Slope = 0.5 (below threshold), but Plateau makes this dimension eligible.
            var slopes = SingleSlope(VoiceDimension.Intonation, 0.5);
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var plateau = new PlateauState
            {
                ReasonCode = "PLATEAU_INTONATION",
                Dimension = VoiceDimension.Intonation,
                SeverityScore = 50.0,
                WindowDays = 30,
                StartedAt = Epoch,
                PlateauDurationDays = 10,
                ObservedSlope = 0.5,
            };
            var profile = ProfileWith(windows, plateau: plateau);

            var insights = engine.Compute(windows, plateau, null, null, profile);

            Assert.Contains(insights, i => i.Dimension == VoiceDimension.Intonation);
        }

        [Fact]
        public void ReasonCode_IsNeverNull_OrEmpty()
        {
            var engine = NewEngine();
            var slopes = Slopes(
                (VoiceDimension.Recovery, -2.0),
                (VoiceDimension.Comfort, 1.5));
            var window = Window(30, sessionCount: 5, slopes);
            var windows = new[] { window };
            var profile = ProfileWith(windows);

            var insights = engine.Compute(windows, null, null, null, profile);

            Assert.All(insights, i => Assert.False(string.IsNullOrEmpty(i.ReasonCode)));
        }
    }
}
