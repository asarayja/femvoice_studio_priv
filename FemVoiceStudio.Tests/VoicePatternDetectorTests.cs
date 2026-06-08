using System;
using System.Collections.Generic;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="VoicePatternDetector"/>.
    ///
    /// Strategy: hand-build <see cref="TrendWindow"/> arrays that drive each detection
    /// branch, plus a low-data case that produces all-null. No mocking; no DateTime.Now;
    /// assertions target ReasonCode and numeric fields only — never localised copy.
    ///
    /// Fixed reference date throughout: 2026-06-01 UTC.
    /// </summary>
    public class VoicePatternDetectorTests
    {
        private static readonly DateTime Epoch = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly VoicePatternDetector _detector = new VoicePatternDetector();

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a <see cref="TrendWindow"/> with the given composite slope and a uniform
        /// per-dimension slope map. <paramref name="offsetDays"/> is the start offset from
        /// <see cref="Epoch"/>; the window spans <paramref name="windowDays"/> days.
        /// </summary>
        private static TrendWindow Window(
            double compositeSlope,
            int windowDays,
            int offsetDays,
            bool hasEnoughData = true,
            IReadOnlyDictionary<VoiceDimension, double>? dimensionSlopes = null)
        {
            var from = Epoch.AddDays(offsetDays);
            var to   = from.AddDays(windowDays);
            var slopes = dimensionSlopes ?? UniformDimSlopes(compositeSlope);
            return new TrendWindow
            {
                WindowDays     = windowDays,
                From           = from,
                To             = to,
                CompositeSlope = compositeSlope,
                CompositeMean  = 60.0,
                CompositeMin   = 50.0,
                CompositeMax   = 70.0,
                SessionCount   = hasEnoughData ? 5 : 1,
                Confidence     = hasEnoughData ? 70.0 : 20.0,
                HasEnoughData  = hasEnoughData,
                DimensionSlopes = slopes
            };
        }

        private static IReadOnlyDictionary<VoiceDimension, double> UniformDimSlopes(double slope)
            => new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    = slope,
                [VoiceDimension.Comfort]     = slope,
                [VoiceDimension.Resonance]   = slope,
                [VoiceDimension.Consistency] = slope,
                [VoiceDimension.Intonation]  = slope,
                [VoiceDimension.VocalWeight] = slope,
                [VoiceDimension.Pitch]       = slope,
            };

        // ─────────────────────────────────────────────────────────────────────────
        // 1. Low-data case: all detectors return null
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Compute_NoDataWindows_AllNull()
        {
            var windows = new[]
            {
                Window(0.0, 7, 0, hasEnoughData: false),
                Window(0.0, 7, 7, hasEnoughData: false)
            };

            var (plateau, breakthrough, regression) = _detector.Compute(windows);

            Assert.Null(plateau);
            Assert.Null(breakthrough);
            Assert.Null(regression);
        }

        [Fact]
        public void Compute_SingleWindow_AllNull()
        {
            var windows = new[] { Window(0.1, 7, 0) };

            var (plateau, breakthrough, regression) = _detector.Compute(windows);

            Assert.Null(plateau);
            Assert.Null(breakthrough);
            Assert.Null(regression);
        }

        [Fact]
        public void Compute_EmptyList_AllNull()
        {
            var (plateau, breakthrough, regression) =
                _detector.Compute(Array.Empty<TrendWindow>());

            Assert.Null(plateau);
            Assert.Null(breakthrough);
            Assert.Null(regression);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 2. Plateau detection
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Compute_TwoFlatWindows_PlateauDetected()
        {
            // Both windows have |CompositeSlope| < PlateauSlopeBand (0.2)
            var windows = new[]
            {
                Window(0.05, 7, 0),
                Window(-0.05, 7, 7)
            };

            var (plateau, breakthrough, regression) = _detector.Compute(windows);

            Assert.NotNull(plateau);
            Assert.StartsWith("PLATEAU_", plateau!.ReasonCode);
            Assert.True(plateau.SeverityScore >= 1.0);
            Assert.True(plateau.SeverityScore <= 100.0);
            Assert.True(plateau.PlateauDurationDays >= 0);
        }

        [Fact]
        public void Compute_TwoFlatWindows_StartedAtMatchesFirstWindowFrom()
        {
            var w1 = Window(0.1, 7, 0);
            var w2 = Window(-0.1, 7, 7);
            var windows = new[] { w1, w2 };

            var (plateau, _, _) = _detector.Compute(windows);

            Assert.NotNull(plateau);
            Assert.Equal(w1.From, plateau!.StartedAt);
        }

        [Fact]
        public void Compute_OneFlatOneSteeplyRising_NoPlateauDetected()
        {
            // Only one flat window — below the PlateauMinWindows=2 threshold.
            var windows = new[]
            {
                Window(0.05, 7, 0),
                Window(2.0,  7, 7)
            };

            var (plateau, _, _) = _detector.Compute(windows);

            Assert.Null(plateau);
        }

        [Fact]
        public void Compute_ThreeFlatWindows_SeverityHigherThanTwoFlatWindows()
        {
            var twoFlat = new[]
            {
                Window(0.05, 7, 0),
                Window(-0.05, 7, 7)
            };
            var threeFlat = new[]
            {
                Window(0.05, 7, 0),
                Window(-0.05, 7, 7),
                Window(0.0, 7, 14)
            };

            var (p2, _, _) = _detector.Compute(twoFlat);
            var (p3, _, _) = _detector.Compute(threeFlat);

            Assert.NotNull(p2);
            Assert.NotNull(p3);
            // Three windows = longer duration ⇒ higher or equal severity.
            Assert.True(p3!.SeverityScore >= p2!.SeverityScore);
        }

        [Fact]
        public void Compute_FlatWindowWithSingleDataWindow_NoPlateauDetected()
        {
            // Only one data-sufficient flat window; the other has no data.
            var windows = new[]
            {
                Window(0.05, 7, 0, hasEnoughData: false),
                Window(0.05, 7, 7, hasEnoughData: true)
            };

            var (plateau, _, _) = _detector.Compute(windows);

            Assert.Null(plateau);
        }

        [Fact]
        public void Compute_Plateau_ReasonCodeContainsDimensionName()
        {
            var windows = new[]
            {
                Window(0.05, 7, 0),
                Window(-0.05, 7, 7)
            };

            var (plateau, _, _) = _detector.Compute(windows);

            Assert.NotNull(plateau);
            // ReasonCode must be PLATEAU_<VoiceDimension member name>
            Assert.True(Enum.IsDefined(typeof(VoiceDimension), plateau!.Dimension));
            Assert.Equal($"PLATEAU_{plateau.Dimension}", plateau.ReasonCode);
        }

        [Fact]
        public void Compute_Plateau_ObservedSlopeIsAverageOfRunWindows()
        {
            var windows = new[]
            {
                Window(0.1, 7, 0),
                Window(-0.1, 7, 7)
            };

            var (plateau, _, _) = _detector.Compute(windows);

            Assert.NotNull(plateau);
            // Average of 0.1 and -0.1 is 0.0
            Assert.Equal(0.0, plateau!.ObservedSlope, 6);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 3. Breakthrough detection
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Compute_ShortWindowMuchMorePositiveThanPrior_BreakthroughDetected()
        {
            // Prior window: slightly positive; recent window: strongly positive.
            // Delta = 2.0 - 0.1 = 1.9 > BreakthroughMinDelta (0.5).
            var windows = new[]
            {
                Window(0.1, 30, -30),
                Window(2.0,  7,   0)
            };

            var (_, breakthrough, _) = _detector.Compute(windows);

            Assert.NotNull(breakthrough);
            Assert.StartsWith("BREAKTHROUGH_", breakthrough!.ReasonCode);
            Assert.True(breakthrough.MagnitudeDelta > VoicePatternDetector.BreakthroughMinDelta);
        }

        [Fact]
        public void Compute_Breakthrough_MagnitudeDeltaIsShortMinusLong()
        {
            double priorSlope  = 0.2;
            double recentSlope = 1.8;
            var windows = new[]
            {
                Window(priorSlope,  30, -30),
                Window(recentSlope,  7,   0)
            };

            var (_, breakthrough, _) = _detector.Compute(windows);

            Assert.NotNull(breakthrough);
            Assert.Equal(recentSlope - priorSlope, breakthrough!.MagnitudeDelta, 6);
        }

        [Fact]
        public void Compute_Breakthrough_DetectedAtMatchesRecentWindowFrom()
        {
            var recentWindow = Window(2.0, 7, 0);
            var windows = new[]
            {
                Window(0.1, 30, -30),
                recentWindow
            };

            var (_, breakthrough, _) = _detector.Compute(windows);

            Assert.NotNull(breakthrough);
            Assert.Equal(recentWindow.From, breakthrough!.DetectedAt);
        }

        [Fact]
        public void Compute_Breakthrough_ReasonCodeContainsDimensionName()
        {
            var windows = new[]
            {
                Window(0.1, 30, -30),
                Window(2.0,  7,   0)
            };

            var (_, breakthrough, _) = _detector.Compute(windows);

            Assert.NotNull(breakthrough);
            Assert.Equal($"BREAKTHROUGH_{breakthrough!.Dimension}", breakthrough.ReasonCode);
            Assert.True(Enum.IsDefined(typeof(VoiceDimension), breakthrough.Dimension));
        }

        [Fact]
        public void Compute_SmallDelta_NoBreakthroughDetected()
        {
            // Delta = 0.6 - 0.2 = 0.4 < BreakthroughMinDelta (0.5)
            var windows = new[]
            {
                Window(0.2, 30, -30),
                Window(0.6,  7,   0)
            };

            var (_, breakthrough, _) = _detector.Compute(windows);

            Assert.Null(breakthrough);
        }

        [Fact]
        public void Compute_RecentWindowNegativeSlope_NoBreakthroughEvenIfDeltaLarge()
        {
            // Recent slope is negative despite large delta — not a breakthrough.
            var windows = new[]
            {
                Window(-3.0, 30, -30),
                Window(-0.5,  7,   0)
            };

            var (_, breakthrough, _) = _detector.Compute(windows);

            Assert.Null(breakthrough);
        }

        [Fact]
        public void Compute_Breakthrough_SeverityScaleWithDelta()
        {
            // Larger delta ⇒ larger or equal severity.
            var smallDeltaWindows = new[]
            {
                Window(0.2, 30, -30),
                Window(0.8,  7,   0)   // delta = 0.6
            };
            var largeDeltaWindows = new[]
            {
                Window(0.2, 30, -30),
                Window(3.0,  7,   0)   // delta = 2.8
            };

            var (_, btSmall, _) = _detector.Compute(smallDeltaWindows);
            var (_, btLarge, _) = _detector.Compute(largeDeltaWindows);

            Assert.NotNull(btSmall);
            Assert.NotNull(btLarge);
            Assert.True(btLarge!.SeverityScore >= btSmall!.SeverityScore);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 4. Regression detection
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Compute_OneDimensionBelowThreshold_RegressionDetected()
        {
            var dimSlopes = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  0.5,
                [VoiceDimension.Comfort]     = -2.0,   // below -1.5 threshold
                [VoiceDimension.Resonance]   =  0.3,
                [VoiceDimension.Consistency] =  0.1,
                [VoiceDimension.Intonation]  =  0.2,
                [VoiceDimension.VocalWeight] =  0.0,
                [VoiceDimension.Pitch]       =  0.1,
            };
            var windows = new[]
            {
                Window(0.2, 30, -30),
                Window(-2.0, 7, 0, dimensionSlopes: dimSlopes)
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.StartsWith("REGRESSION_", regression!.ReasonCode);
        }

        [Fact]
        public void Compute_Regression_DeclineSlopeAtOrBelowThreshold()
        {
            var dimSlopes = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  0.0,
                [VoiceDimension.Comfort]     = -2.5,
                [VoiceDimension.Resonance]   =  0.1,
                [VoiceDimension.Consistency] =  0.0,
                [VoiceDimension.Intonation]  =  0.0,
                [VoiceDimension.VocalWeight] =  0.0,
                [VoiceDimension.Pitch]       =  0.0,
            };
            var windows = new[]
            {
                Window(0.0, 30, -30),
                Window(-2.5, 7, 0, dimensionSlopes: dimSlopes)
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.True(regression!.DeclineSlope <= VoicePatternDetector.RegressionSlopeThreshold);
        }

        [Fact]
        public void Compute_Regression_ReasonCodeContainsPrimaryDimensionName()
        {
            var dimSlopes = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  0.0,
                [VoiceDimension.Comfort]     = -3.0,
                [VoiceDimension.Resonance]   =  0.5,
                [VoiceDimension.Consistency] =  0.1,
                [VoiceDimension.Intonation]  =  0.2,
                [VoiceDimension.VocalWeight] =  0.0,
                [VoiceDimension.Pitch]       =  0.1,
            };
            var windows = new[]
            {
                Window(0.0, 30, -30),
                Window(-3.0, 7, 0, dimensionSlopes: dimSlopes)
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.Equal($"REGRESSION_{regression!.Dimension}", regression.ReasonCode);
            Assert.True(Enum.IsDefined(typeof(VoiceDimension), regression.Dimension));
        }

        [Fact]
        public void Compute_MultipleDecliningDimensions_CompoundSeverityHigherThanBase()
        {
            var dimSlopes = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    = -2.0,
                [VoiceDimension.Comfort]     = -1.8,
                [VoiceDimension.Resonance]   = -2.5,
                [VoiceDimension.Consistency] =  0.1,
                [VoiceDimension.Intonation]  =  0.0,
                [VoiceDimension.VocalWeight] =  0.0,
                [VoiceDimension.Pitch]       =  0.0,
            };
            var windows = new[]
            {
                Window(0.0, 30, -30),
                Window(-2.1, 7, 0, dimensionSlopes: dimSlopes)
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.True(regression!.CompoundSeverity >= regression.SeverityScore);
        }

        [Fact]
        public void Compute_SingleDecliningDimension_CompoundSeverityEqualsBase()
        {
            var dimSlopes = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  0.5,
                [VoiceDimension.Comfort]     = -2.0,
                [VoiceDimension.Resonance]   =  0.3,
                [VoiceDimension.Consistency] =  0.1,
                [VoiceDimension.Intonation]  =  0.2,
                [VoiceDimension.VocalWeight] =  0.0,
                [VoiceDimension.Pitch]       =  0.1,
            };
            var windows = new[]
            {
                Window(0.0, 30, -30),
                Window(-2.0, 7, 0, dimensionSlopes: dimSlopes)
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            // Only one dimension declining ⇒ compoundFactor = 1.0 ⇒ CompoundSeverity == SeverityScore.
            Assert.Equal(regression!.SeverityScore, regression.CompoundSeverity, 6);
        }

        [Fact]
        public void Compute_SlopesAboveThreshold_NoRegressionDetected()
        {
            // All slopes are -1.0, above the -1.5 threshold.
            var dimSlopes = UniformDimSlopes(-1.0);
            var windows = new[]
            {
                Window(-1.0, 30, -30, dimensionSlopes: dimSlopes),
                Window(-1.0,  7,   0, dimensionSlopes: dimSlopes)
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.Null(regression);
        }

        [Fact]
        public void Compute_Regression_DetectedAtMatchesLatestSufficientWindowFrom()
        {
            var dimSlopes = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    = -2.0,
                [VoiceDimension.Comfort]     = -2.0,
                [VoiceDimension.Resonance]   = -2.0,
                [VoiceDimension.Consistency] = -2.0,
                [VoiceDimension.Intonation]  = -2.0,
                [VoiceDimension.VocalWeight] = -2.0,
                [VoiceDimension.Pitch]       = -2.0,
            };
            var latestWindow = Window(-2.0, 7, 7, dimensionSlopes: dimSlopes);
            var windows = new[]
            {
                Window(-2.0, 30, -30, dimensionSlopes: dimSlopes),
                latestWindow
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.Equal(latestWindow.From, regression!.DetectedAt);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 5. Independence: all three patterns detected simultaneously
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Compute_ThreeWindowsWithDifferentPatterns_AllThreeCanDetectIndependently()
        {
            // Three windows: first two are flat (plateau in first pair), last one has
            // both a large positive composite (breakthrough vs prior) AND declining dims
            // (regression). Verifies detectors run independently.

            var regressionDimSlopes = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  2.5,
                [VoiceDimension.Comfort]     = -2.0,   // regression trigger
                [VoiceDimension.Resonance]   =  2.5,
                [VoiceDimension.Consistency] =  2.5,
                [VoiceDimension.Intonation]  =  2.5,
                [VoiceDimension.VocalWeight] =  2.5,
                [VoiceDimension.Pitch]       =  2.5,
            };

            var windows = new[]
            {
                Window(0.05,  7,  0),              // flat
                Window(-0.05, 7,  7),              // flat  → plateau in first two
                Window(2.5,   7, 14, dimensionSlopes: regressionDimSlopes)  // breakthrough vs w2; regression per dim
            };

            var (plateau, breakthrough, regression) = _detector.Compute(windows);

            // Plateau: found in the first two windows.
            Assert.NotNull(plateau);
            // Breakthrough: window[2] vs window[1] delta = 2.5 - (-0.05) = 2.55 > 0.5.
            Assert.NotNull(breakthrough);
            // Regression: Comfort slope = -2.0 <= -1.5.
            Assert.NotNull(regression);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 6. Boundary: exactly at threshold values
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Compute_CompositeSlope_ExactlyAtPlateauBand_Triggers()
        {
            // |0.2| == PlateauSlopeBand → should trigger (inclusive boundary).
            var windows = new[]
            {
                Window(VoicePatternDetector.PlateauSlopeBand, 7, 0),
                Window(VoicePatternDetector.PlateauSlopeBand, 7, 7)
            };

            var (plateau, _, _) = _detector.Compute(windows);

            Assert.NotNull(plateau);
        }

        [Fact]
        public void Compute_DimSlope_ExactlyAtRegressionThreshold_Triggers()
        {
            double threshold = VoicePatternDetector.RegressionSlopeThreshold; // -1.5
            var dimSlopes = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  0.0,
                [VoiceDimension.Comfort]     =  threshold,  // exactly -1.5
                [VoiceDimension.Resonance]   =  0.0,
                [VoiceDimension.Consistency] =  0.0,
                [VoiceDimension.Intonation]  =  0.0,
                [VoiceDimension.VocalWeight] =  0.0,
                [VoiceDimension.Pitch]       =  0.0,
            };
            var windows = new[]
            {
                Window(0.0, 30, -30),
                Window(threshold, 7, 0, dimensionSlopes: dimSlopes)
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.Equal(threshold, regression!.DeclineSlope, 6);
        }

        [Fact]
        public void Compute_DimSlope_SlightlyAboveThreshold_NoRegression()
        {
            double justAbove = VoicePatternDetector.RegressionSlopeThreshold + 0.01; // -1.49
            var dimSlopes = UniformDimSlopes(justAbove);
            var windows = new[]
            {
                Window(justAbove, 30, -30, dimensionSlopes: dimSlopes),
                Window(justAbove,  7,   0, dimensionSlopes: dimSlopes)
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.Null(regression);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 7. SeverityScore is clamped to [1, 100]
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Compute_PlateauSeverityScore_ClampedBetween1And100()
        {
            var windows = new[]
            {
                Window(0.0, 7, 0),
                Window(0.0, 7, 7)
            };

            var (plateau, _, _) = _detector.Compute(windows);

            Assert.NotNull(plateau);
            Assert.True(plateau!.SeverityScore >= 1.0);
            Assert.True(plateau.SeverityScore <= 100.0);
        }

        [Fact]
        public void Compute_RegressionSeverityScore_ClampedBetween1And100()
        {
            var dimSlopes = UniformDimSlopes(-20.0);  // extreme slope
            var windows = new[]
            {
                Window(-20.0, 30, -30, dimensionSlopes: dimSlopes),
                Window(-20.0,  7,   0, dimensionSlopes: dimSlopes)
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.True(regression!.SeverityScore >= 1.0);
            Assert.True(regression.SeverityScore <= 100.0);
            Assert.True(regression.CompoundSeverity >= 1.0);
            Assert.True(regression.CompoundSeverity <= 100.0);
        }
    }
}
