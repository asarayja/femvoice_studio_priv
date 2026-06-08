using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tests for <see cref="VoicePatternDetector"/> — the HORIZON-based redesign.
    ///
    /// REALITY THESE TESTS MIRROR: <see cref="TrendEngineService.Compute"/> returns CUMULATIVE,
    /// NESTED windows [now−7,now) / [now−30,now) / [now−90,now) / [now−180,now), ascending on
    /// <see cref="TrendWindow.WindowDays"/>. The shortest window (7d) is the freshest resolution;
    /// the longest (180d) is the most aggregated long-run average. These are NOT a disjoint
    /// "oldest-first" chronological series, so the detector reasons by HORIZON, never by list
    /// position. Every window therefore shares the SAME exclusive upper bound (To = now); only
    /// the From boundary differs by WindowDays. The helpers below build windows exactly that way.
    ///
    /// Strategy: hand-build cumulative nested windows; assert on ReasonCode and numeric fields
    /// (WindowDays / DetectedAt / MagnitudeDelta / DeclineSlope / PlateauDurationDays) only —
    /// never localised copy. No mocking; no DateTime.Now.
    ///
    /// Fixed reference "now" throughout: 2026-06-01 UTC.
    /// </summary>
    public class VoicePatternDetectorTests
    {
        private static readonly DateTime Now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly VoicePatternDetector _detector = new VoicePatternDetector();

        private static readonly VoiceDimension[] AllDims =
        {
            VoiceDimension.Recovery,
            VoiceDimension.Comfort,
            VoiceDimension.Resonance,
            VoiceDimension.Consistency,
            VoiceDimension.Intonation,
            VoiceDimension.VocalWeight,
            VoiceDimension.Pitch,
        };

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers — build CUMULATIVE NESTED horizon windows as TrendEngineService does.
        // From = now − windowDays, To = now (shared exclusive upper bound).
        // ─────────────────────────────────────────────────────────────────────────

        private static TrendWindow Horizon(
            int windowDays,
            double compositeSlope,
            bool hasEnoughData = true,
            IReadOnlyDictionary<VoiceDimension, double>? dimensionSlopes = null)
        {
            return new TrendWindow
            {
                WindowDays      = windowDays,
                From            = Now.AddDays(-windowDays),
                To              = Now,
                CompositeSlope  = compositeSlope,
                CompositeMean   = 60.0,
                CompositeMin    = 50.0,
                CompositeMax    = 70.0,
                SessionCount    = hasEnoughData ? 5 : 1,
                Confidence      = hasEnoughData ? 70.0 : 20.0,
                HasEnoughData   = hasEnoughData,
                DimensionSlopes = dimensionSlopes ?? Uniform(compositeSlope),
            };
        }

        private static IReadOnlyDictionary<VoiceDimension, double> Uniform(double slope)
            => AllDims.ToDictionary(d => d, _ => slope);

        // ═════════════════════════════════════════════════════════════════════════
        // DEGRADATION — too few sufficient horizons ⇒ correct nulls (spec §5).
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Compute_EmptyList_AllNull()
        {
            var (p, b, r) = _detector.Compute(Array.Empty<TrendWindow>());
            Assert.Null(p);
            Assert.Null(b);
            Assert.Null(r);
        }

        [Fact]
        public void Compute_AllLowDataHorizons_AllNull()
        {
            var windows = new[]
            {
                Horizon(7,   0.0, hasEnoughData: false),
                Horizon(30,  0.0, hasEnoughData: false),
                Horizon(90,  0.0, hasEnoughData: false),
                Horizon(180, 0.0, hasEnoughData: false),
            };

            var (p, b, r) = _detector.Compute(windows);
            Assert.Null(p);
            Assert.Null(b);
            Assert.Null(r);
        }

        [Fact]
        public void Compute_SingleSufficientHorizon_BreakthroughAndPlateauNull_RegressionMayRun()
        {
            // Only the 7d horizon has data; it is steeply declining on every dimension.
            // Breakthrough and Plateau both need a short+long contrast ⇒ null.
            // Regression can still fire on the one sufficient horizon.
            var windows = new[]
            {
                Horizon(7,   -2.0, hasEnoughData: true,  dimensionSlopes: Uniform(-2.0)),
                Horizon(30,   0.0, hasEnoughData: false),
                Horizon(90,   0.0, hasEnoughData: false),
                Horizon(180,  0.0, hasEnoughData: false),
            };

            var (plateau, breakthrough, regression) = _detector.Compute(windows);

            Assert.Null(plateau);
            Assert.Null(breakthrough);
            Assert.NotNull(regression);
            Assert.Equal(7, regression!.WindowDays);
            Assert.Equal(Now.AddDays(-7), regression.DetectedAt);
        }

        [Fact]
        public void Compute_SingleSufficientFlatHorizon_PlateauStillNull()
        {
            // One flat sufficient horizon is NOT a persistent plateau (needs long reach too).
            var windows = new[]
            {
                Horizon(7,   0.05, hasEnoughData: true),
                Horizon(30,  0.0,  hasEnoughData: false),
                Horizon(90,  0.0,  hasEnoughData: false),
                Horizon(180, 0.0,  hasEnoughData: false),
            };

            var (plateau, breakthrough, regression) = _detector.Compute(windows);

            Assert.Null(plateau);
            Assert.Null(breakthrough);
            Assert.Null(regression);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // DEFECT #2 — acceleration visible ONLY in the 7d horizon must fire breakthrough
        //             with WindowDays = 7, DetectedAt = now − 7d. The 7d signal is USED.
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Compute_AccelerationOnlyInRecentHorizon_BreakthroughOn7dHorizon()
        {
            // Long-run average is essentially flat (180d ≈ 0.2); the freshest 7d horizon has
            // surged to +4.0. delta = 4.0 − 0.2 = 3.8 ≥ BreakthroughDeltaThreshold (3.0).
            // This is the regression test against defect #2: it must read the 7d horizon, not
            // compare 180d vs 90d.
            var windows = new[]
            {
                Horizon(7,   4.0),
                Horizon(30,  1.5),
                Horizon(90,  0.6),
                Horizon(180, 0.2),
            };

            var (_, breakthrough, _) = _detector.Compute(windows);

            Assert.NotNull(breakthrough);
            Assert.StartsWith("BREAKTHROUGH_", breakthrough!.ReasonCode);
            Assert.Equal(7, breakthrough.WindowDays);
            Assert.Equal(Now.AddDays(-7), breakthrough.DetectedAt);
            // delta = recent.CompositeSlope − longest.CompositeSlope = 4.0 − 0.2 = 3.8
            Assert.Equal(3.8, breakthrough.MagnitudeDelta, 6);
        }

        [Fact]
        public void Compute_RecentSlopeNotPositive_NoBreakthroughEvenIfDeltaLarge()
        {
            // Recent (7d) slope is 0 / negative — not an improvement, so no breakthrough even
            // though it is far above the (very negative) long-run average.
            var windows = new[]
            {
                Horizon(7,   -0.5),
                Horizon(30,  -2.0),
                Horizon(90,  -3.5),
                Horizon(180, -5.0),
            };

            var (_, breakthrough, _) = _detector.Compute(windows);
            Assert.Null(breakthrough);
        }

        [Fact]
        public void Compute_AccelerationBelowThreshold_NoBreakthrough()
        {
            // delta = 2.0 − 0.5 = 1.5 < BreakthroughDeltaThreshold (3.0): ordinary improvement.
            var windows = new[]
            {
                Horizon(7,   2.0),
                Horizon(30,  1.2),
                Horizon(90,  0.8),
                Horizon(180, 0.5),
            };

            var (_, breakthrough, _) = _detector.Compute(windows);
            Assert.Null(breakthrough);
        }

        [Fact]
        public void Compute_Breakthrough_PicksDimensionWithLargestRecentMinusLongestDelta()
        {
            // Resonance accelerates hardest in the 7d horizon relative to the 180d average.
            var recentDims = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    = 1.0,
                [VoiceDimension.Comfort]     = 1.0,
                [VoiceDimension.Resonance]   = 6.0,   // biggest jump vs longest
                [VoiceDimension.Consistency] = 1.0,
                [VoiceDimension.Intonation]  = 1.0,
                [VoiceDimension.VocalWeight] = 1.0,
                [VoiceDimension.Pitch]       = 1.0,
            };
            var longestDims = Uniform(0.5);

            var windows = new[]
            {
                Horizon(7,   4.0, dimensionSlopes: recentDims),
                Horizon(30,  1.0),
                Horizon(90,  0.7),
                Horizon(180, 0.5, dimensionSlopes: longestDims),
            };

            var (_, breakthrough, _) = _detector.Compute(windows);

            Assert.NotNull(breakthrough);
            Assert.Equal(VoiceDimension.Resonance, breakthrough!.Dimension);
            Assert.Equal("BREAKTHROUGH_Resonance", breakthrough.ReasonCode);
        }

        [Fact]
        public void Compute_Breakthrough_SeverityScalesWithDelta()
        {
            var small = new[]
            {
                Horizon(7,   3.2),
                Horizon(180, 0.0),    // delta = 3.2
            };
            var large = new[]
            {
                Horizon(7,   6.5),
                Horizon(180, 0.0),    // delta = 6.5
            };

            var (_, btSmall, _) = _detector.Compute(small);
            var (_, btLarge, _) = _detector.Compute(large);

            Assert.NotNull(btSmall);
            Assert.NotNull(btLarge);
            Assert.True(btLarge!.SeverityScore >= btSmall!.SeverityScore);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // DEFECT #3 — plateau when all horizons are flat ⇒ PlateauDurationDays = longest
        //             window's WindowDays (180), NOT an overlapping To−From subtraction.
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Compute_AllHorizonsFlat_PlateauDurationIsLongestWindowDays()
        {
            var windows = new[]
            {
                Horizon(7,   0.05),
                Horizon(30,  0.10),
                Horizon(90,  -0.05),
                Horizon(180, 0.0),
            };

            var (plateau, _, _) = _detector.Compute(windows);

            Assert.NotNull(plateau);
            Assert.StartsWith("PLATEAU_", plateau!.ReasonCode);
            // The flatness reaches as far back as the longest horizon: exactly its WindowDays.
            Assert.Equal(180, plateau.PlateauDurationDays);
            // StartedAt = longest window's From = now − 180d.
            Assert.Equal(Now.AddDays(-180), plateau.StartedAt);
            // WindowDays of the detection itself is the freshest horizon (7d observation).
            Assert.Equal(7, plateau.WindowDays);
            // ObservedSlope is the recent (freshest) reading: 0.05.
            Assert.Equal(0.05, plateau.ObservedSlope, 6);
        }

        [Fact]
        public void Compute_PlateauDuration_TracksLongestSufficientHorizon_Not180WhenShorter()
        {
            // Only 7d and 30d are data-sufficient; 90d/180d are low-data and ignored.
            // Therefore the longest SUFFICIENT horizon is 30d ⇒ duration = 30, not 180.
            var windows = new[]
            {
                Horizon(7,   0.05, hasEnoughData: true),
                Horizon(30,  0.05, hasEnoughData: true),
                Horizon(90,  0.0,  hasEnoughData: false),
                Horizon(180, 0.0,  hasEnoughData: false),
            };

            var (plateau, _, _) = _detector.Compute(windows);

            Assert.NotNull(plateau);
            Assert.Equal(30, plateau!.PlateauDurationDays);
            Assert.Equal(Now.AddDays(-30), plateau.StartedAt);
        }

        [Fact]
        public void Compute_RecentMovingButLongFlat_NoPlateau()
        {
            // Long reach flat, but the freshest horizon is climbing ⇒ not a persistent plateau.
            var windows = new[]
            {
                Horizon(7,   2.0),
                Horizon(30,  0.8),
                Horizon(90,  0.1),
                Horizon(180, 0.05),
            };

            var (plateau, _, _) = _detector.Compute(windows);
            Assert.Null(plateau);
        }

        [Fact]
        public void Compute_Plateau_SeverityWithin1To100_AndScalesWithFlatness()
        {
            var deadFlat = new[]
            {
                Horizon(7,   0.0),
                Horizon(180, 0.0),
            };
            var lessFlat = new[]
            {
                Horizon(7,   0.18),
                Horizon(180, 0.18),
            };

            var (pDead, _, _) = _detector.Compute(deadFlat);
            var (pLess, _, _) = _detector.Compute(lessFlat);

            Assert.NotNull(pDead);
            Assert.NotNull(pLess);
            Assert.InRange(pDead!.SeverityScore, 1.0, 100.0);
            Assert.InRange(pLess!.SeverityScore, 1.0, 100.0);
            // Deader-flat ⇒ higher (or equal) severity.
            Assert.True(pDead.SeverityScore >= pLess.SeverityScore);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // DEFECT #4 — regression reads the RECENT (shortest) horizon's per-dim slopes,
        //             NOT the diluted 180d slope.
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Compute_RecentHorizonDeclining_RegressionDetectedOn7dHorizon()
        {
            // The 7d horizon shows a sharp Comfort decline (−2.4); the long-run 180d average
            // still looks fine (+0.5). Detection must read the 7d horizon (defect #4).
            var recentDims = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  0.3,
                [VoiceDimension.Comfort]     = -2.4,   // below −1.5
                [VoiceDimension.Resonance]   =  0.2,
                [VoiceDimension.Consistency] =  0.1,
                [VoiceDimension.Intonation]  =  0.0,
                [VoiceDimension.VocalWeight] =  0.1,
                [VoiceDimension.Pitch]       =  0.0,
            };
            var longHealthyDims = Uniform(0.5);

            var windows = new[]
            {
                Horizon(7,   -0.3, dimensionSlopes: recentDims),
                Horizon(30,   0.1, dimensionSlopes: Uniform(0.1)),
                Horizon(90,   0.3, dimensionSlopes: Uniform(0.3)),
                Horizon(180,  0.5, dimensionSlopes: longHealthyDims),
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.Equal("REGRESSION_Comfort", regression!.ReasonCode);
            Assert.Equal(VoiceDimension.Comfort, regression.Dimension);
            Assert.Equal(7, regression.WindowDays);
            Assert.Equal(Now.AddDays(-7), regression.DetectedAt);
            Assert.Equal(-2.4, regression.DeclineSlope, 6);
            Assert.True(regression.DeclineSlope <= VoicePatternDetector.RegressionDeclineThreshold);
        }

        [Fact]
        public void Compute_LongHorizonDecliningButRecentFine_NoRegression()
        {
            // The diluted 180d horizon is steeply negative, but the FRESH 7d horizon is healthy.
            // The old position-based code would have fired off the 180d slope; the redesign
            // must NOT — there is no current regression.
            var recentHealthy = Uniform(0.4);
            var longDeclining = Uniform(-3.0);

            var windows = new[]
            {
                Horizon(7,   0.4,  dimensionSlopes: recentHealthy),
                Horizon(30,  0.0,  dimensionSlopes: Uniform(0.0)),
                Horizon(90,  -1.5, dimensionSlopes: Uniform(-1.5)),
                Horizon(180, -3.0, dimensionSlopes: longDeclining),
            };

            var (_, _, regression) = _detector.Compute(windows);
            Assert.Null(regression);
        }

        [Fact]
        public void Compute_Regression_ExactlyAtThreshold_Triggers()
        {
            var dims = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  0.0,
                [VoiceDimension.Comfort]     = VoicePatternDetector.RegressionDeclineThreshold, // -1.5
                [VoiceDimension.Resonance]   =  0.0,
                [VoiceDimension.Consistency] =  0.0,
                [VoiceDimension.Intonation]  =  0.0,
                [VoiceDimension.VocalWeight] =  0.0,
                [VoiceDimension.Pitch]       =  0.0,
            };
            var windows = new[]
            {
                Horizon(7,   -0.2, dimensionSlopes: dims),
                Horizon(180,  0.0),
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.Equal(VoicePatternDetector.RegressionDeclineThreshold, regression!.DeclineSlope, 6);
        }

        [Fact]
        public void Compute_Regression_SlightlyAboveThreshold_NoRegression()
        {
            double justAbove = VoicePatternDetector.RegressionDeclineThreshold + 0.01; // -1.49
            var windows = new[]
            {
                Horizon(7,   justAbove, dimensionSlopes: Uniform(justAbove)),
                Horizon(180, justAbove, dimensionSlopes: Uniform(justAbove)),
            };

            var (_, _, regression) = _detector.Compute(windows);
            Assert.Null(regression);
        }

        [Fact]
        public void Compute_Regression_MultipleDecliningDims_CompoundSeverityHigherThanBase()
        {
            var dims = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    = -2.0,
                [VoiceDimension.Comfort]     = -1.8,
                [VoiceDimension.Resonance]   = -2.5,   // worst
                [VoiceDimension.Consistency] =  0.1,
                [VoiceDimension.Intonation]  =  0.0,
                [VoiceDimension.VocalWeight] =  0.0,
                [VoiceDimension.Pitch]       =  0.0,
            };
            var windows = new[]
            {
                Horizon(7,   -2.1, dimensionSlopes: dims),
                Horizon(180,  0.0),
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.Equal(VoiceDimension.Resonance, regression!.Dimension);
            Assert.True(regression.CompoundSeverity > regression.SeverityScore);
            Assert.InRange(regression.SeverityScore, 1.0, 100.0);
            Assert.InRange(regression.CompoundSeverity, 1.0, 100.0);
        }

        [Fact]
        public void Compute_Regression_SingleDecliningDim_CompoundSeverityEqualsBase()
        {
            var dims = new Dictionary<VoiceDimension, double>
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
                Horizon(7,   -0.2, dimensionSlopes: dims),
                Horizon(180,  0.0),
            };

            var (_, _, regression) = _detector.Compute(windows);

            Assert.NotNull(regression);
            Assert.Equal(regression!.SeverityScore, regression.CompoundSeverity, 6);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // DEFECT #1 — ORDER INDEPENDENCE: identical windows in reversed order ⇒ identical
        //             result. The detector must not key off list position.
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Compute_ReversedWindowOrder_IdenticalBreakthroughResult()
        {
            var recentDims = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    = 1.0,
                [VoiceDimension.Comfort]     = 1.0,
                [VoiceDimension.Resonance]   = 6.0,
                [VoiceDimension.Consistency] = 1.0,
                [VoiceDimension.Intonation]  = 1.0,
                [VoiceDimension.VocalWeight] = 1.0,
                [VoiceDimension.Pitch]       = 1.0,
            };

            var ascending = new[]
            {
                Horizon(7,   4.0, dimensionSlopes: recentDims),
                Horizon(30,  1.0),
                Horizon(90,  0.7),
                Horizon(180, 0.2),
            };
            var descending = ascending.Reverse().ToArray();

            var (_, btAsc, _)  = _detector.Compute(ascending);
            var (_, btDesc, _) = _detector.Compute(descending);

            Assert.NotNull(btAsc);
            Assert.NotNull(btDesc);
            Assert.Equal(btAsc!.ReasonCode,     btDesc!.ReasonCode);
            Assert.Equal(btAsc.Dimension,       btDesc.Dimension);
            Assert.Equal(btAsc.WindowDays,      btDesc.WindowDays);
            Assert.Equal(btAsc.DetectedAt,      btDesc.DetectedAt);
            Assert.Equal(btAsc.MagnitudeDelta,  btDesc.MagnitudeDelta, 6);
            Assert.Equal(btAsc.SeverityScore,   btDesc.SeverityScore, 6);
        }

        [Fact]
        public void Compute_ReversedWindowOrder_IdenticalAcrossAllThreePatterns()
        {
            // A window set that simultaneously yields a regression and exercises plateau/breakthrough
            // gating, then assert full order-independence on every non-null field.
            var recentDims = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  0.2,
                [VoiceDimension.Comfort]     = -2.4,
                [VoiceDimension.Resonance]   =  0.1,
                [VoiceDimension.Consistency] =  0.0,
                [VoiceDimension.Intonation]  =  0.0,
                [VoiceDimension.VocalWeight] =  0.0,
                [VoiceDimension.Pitch]       =  0.0,
            };
            var ascending = new[]
            {
                Horizon(7,   -0.3, dimensionSlopes: recentDims),
                Horizon(30,   0.1),
                Horizon(90,   0.3),
                Horizon(180,  0.5),
            };
            var descending = ascending.Reverse().ToArray();

            var asc  = _detector.Compute(ascending);
            var desc = _detector.Compute(descending);

            // Plateau: null in both (recent not flat).
            Assert.Equal(asc.Plateau is null, desc.Plateau is null);
            // Breakthrough: null in both (recent slope negative).
            Assert.Equal(asc.Breakthrough is null, desc.Breakthrough is null);
            // Regression: identical.
            Assert.NotNull(asc.Regression);
            Assert.NotNull(desc.Regression);
            Assert.Equal(asc.Regression!.ReasonCode,    desc.Regression!.ReasonCode);
            Assert.Equal(asc.Regression.WindowDays,      desc.Regression.WindowDays);
            Assert.Equal(asc.Regression.DetectedAt,      desc.Regression.DetectedAt);
            Assert.Equal(asc.Regression.DeclineSlope,    desc.Regression.DeclineSlope, 6);
            Assert.Equal(asc.Regression.CompoundSeverity, desc.Regression.CompoundSeverity, 6);
        }

        [Fact]
        public void Compute_ReversedWindowOrder_IdenticalPlateauResult()
        {
            var ascending = new[]
            {
                Horizon(7,   0.05),
                Horizon(30,  0.10),
                Horizon(90,  -0.05),
                Horizon(180, 0.0),
            };
            var descending = ascending.Reverse().ToArray();

            var (pAsc, _, _)  = _detector.Compute(ascending);
            var (pDesc, _, _) = _detector.Compute(descending);

            Assert.NotNull(pAsc);
            Assert.NotNull(pDesc);
            Assert.Equal(pAsc!.PlateauDurationDays, pDesc!.PlateauDurationDays);
            Assert.Equal(pAsc.StartedAt,            pDesc.StartedAt);
            Assert.Equal(pAsc.WindowDays,           pDesc.WindowDays);
            Assert.Equal(pAsc.ObservedSlope,        pDesc.ObservedSlope, 6);
            Assert.Equal(pAsc.ReasonCode,           pDesc.ReasonCode);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // INDEPENDENCE — all three detectors run on the same input without interference.
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Compute_BreakthroughAndRegressionTogether_BothDetected()
        {
            // Recent (7d) horizon: composite surging (breakthrough), but Comfort collapsing
            // (regression). Both must fire; plateau must not (recent not flat).
            var recentDims = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    =  5.0,
                [VoiceDimension.Comfort]     = -2.0,   // regression trigger
                [VoiceDimension.Resonance]   =  5.0,
                [VoiceDimension.Consistency] =  5.0,
                [VoiceDimension.Intonation]  =  5.0,
                [VoiceDimension.VocalWeight] =  5.0,
                [VoiceDimension.Pitch]       =  5.0,
            };
            var windows = new[]
            {
                Horizon(7,   4.0, dimensionSlopes: recentDims),
                Horizon(30,  1.0),
                Horizon(90,  0.5),
                Horizon(180, 0.2),
            };

            var (plateau, breakthrough, regression) = _detector.Compute(windows);

            Assert.Null(plateau);
            Assert.NotNull(breakthrough);
            Assert.NotNull(regression);
            Assert.Equal("REGRESSION_Comfort", regression!.ReasonCode);
            Assert.StartsWith("BREAKTHROUGH_", breakthrough!.ReasonCode);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // ROBUSTNESS — NaN / Inf must never throw.
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Compute_NaNAndInfSlopes_DoesNotThrow()
        {
            var nanDims = new Dictionary<VoiceDimension, double>
            {
                [VoiceDimension.Recovery]    = double.NaN,
                [VoiceDimension.Comfort]     = double.PositiveInfinity,
                [VoiceDimension.Resonance]   = double.NegativeInfinity,
                [VoiceDimension.Consistency] = 0.0,
                [VoiceDimension.Intonation]  = 0.0,
                [VoiceDimension.VocalWeight] = 0.0,
                [VoiceDimension.Pitch]       = 0.0,
            };
            var windows = new[]
            {
                Horizon(7,   double.NaN, dimensionSlopes: nanDims),
                Horizon(180, double.PositiveInfinity, dimensionSlopes: nanDims),
            };

            var ex = Record.Exception(() => _detector.Compute(windows));
            Assert.Null(ex);
        }

        // ═════════════════════════════════════════════════════════════════════════
        // CONTRACT INTEGRATION — feed the ACTUAL output of TrendEngineService.Compute
        // (cumulative nested windows) straight into the detector, so a contract mismatch
        // between the two services cannot silently reappear.
        // ═════════════════════════════════════════════════════════════════════════

        [Fact]
        public void Compute_OnRealTrendEngineOutput_DetectsRecentBreakthrough()
        {
            // Build a real Voice Intelligence point series whose composite is flat for months
            // and then surges sharply in the final week. TrendEngineService.Compute partitions
            // this into [now−7] / [now−30] / [now−90] / [now−180); the 7d window's slope is
            // steep, the 180d window's slope is shallow ⇒ breakthrough on the 7d horizon.
            var engine = new TrendEngineService(
                new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository()));

            var points = new List<VoiceIntelligenceTrendPoint>();

            // Long flat tail: one point per week from 180d ago up to ~10d ago, composite ≈ 50.
            int sid = 1;
            for (int day = 175; day >= 10; day -= 7)
            {
                points.Add(Point(sid++, Now.AddDays(-day), composite: 50.0, dim: 50.0));
            }

            // Final-week surge: three closely-spaced points climbing steeply, inside the 7d window.
            points.Add(Point(sid++, Now.AddDays(-5), composite: 60.0, dim: 60.0));
            points.Add(Point(sid++, Now.AddDays(-3), composite: 72.0, dim: 72.0));
            points.Add(Point(sid++, Now.AddDays(-1), composite: 85.0, dim: 85.0));

            var windows = engine.Compute(points, Now);

            // Sanity: the engine returns exactly the four canonical horizons, ascending.
            Assert.Equal(new[] { 7, 30, 90, 180 }, windows.Select(w => w.WindowDays).ToArray());

            var (_, breakthrough, _) = _detector.Compute(windows);

            // The acceleration is concentrated in the final week ⇒ detected on the 7d horizon.
            Assert.NotNull(breakthrough);
            Assert.Equal(7, breakthrough!.WindowDays);
            Assert.Equal(Now.AddDays(-7), breakthrough.DetectedAt);
            Assert.True(breakthrough.MagnitudeDelta >= VoicePatternDetector.BreakthroughDeltaThreshold);
        }

        [Fact]
        public void Compute_OnRealTrendEngineOutput_FlatHistory_PlateauDurationIs180()
        {
            // A genuinely flat history (composite hovering around 55) across the full 180d span.
            var engine = new TrendEngineService(
                new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository()));

            var points = new List<VoiceIntelligenceTrendPoint>();
            int sid = 1;
            // Dense, near-constant series so every horizon has ≥3 points and a ~flat slope.
            for (int day = 178; day >= 1; day -= 3)
            {
                double jitter = (sid % 2 == 0) ? 0.4 : -0.4;   // tiny oscillation, slope ≈ 0
                points.Add(Point(sid++, Now.AddDays(-day), composite: 55.0 + jitter, dim: 55.0 + jitter));
            }

            var windows = engine.Compute(points, Now);
            var (plateau, _, _) = _detector.Compute(windows);

            Assert.NotNull(plateau);
            Assert.StartsWith("PLATEAU_", plateau!.ReasonCode);
            // Flatness reaches the longest data-sufficient horizon — the full 180d span.
            Assert.Equal(180, plateau.PlateauDurationDays);
            Assert.Equal(Now.AddDays(-180), plateau.StartedAt);
            Assert.Equal(7, plateau.WindowDays);
        }

        private static VoiceIntelligenceTrendPoint Point(
            int sessionId, DateTime startedAt, double composite, double dim)
            => new VoiceIntelligenceTrendPoint
            {
                SessionId           = sessionId,
                UserId              = 1,
                StartedAt           = startedAt,
                EndedAt             = startedAt.AddMinutes(20),
                ResonanceScore100   = dim,
                ComfortScore100     = dim,
                ConsistencyScore100 = dim,
                IntonationScore100  = dim,
                VocalWeightScore100 = dim,
                RecoveryScore100    = dim,
                PitchScore100       = dim,
                CompositeVoiceScore = composite,
            };
    }
}
