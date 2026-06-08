using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using OxyPlot.Series;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint B (Bølge 2) — Agent VIZ: trend visualisation logic.
    ///
    /// Verifies the WPF-free mapping from <see cref="VoiceIntelligenceTrendPoint"/>
    /// (the LIVE SessionAnalyticsStore trend) into the analysis / progression view
    /// models: 0–100 preservation + clamping, chronology, empty-history handling
    /// without crashing, and exposure of all seven dimensions plus the composite.
    ///
    /// No mocking: real <see cref="InMemorySessionAnalyticsRepository"/> +
    /// <see cref="SessionAnalyticsStore"/> + real view-model logic.
    /// </summary>
    public class VoiceIntelligenceVizTests
    {
        private static VoiceIntelligenceTrendPoint Point(
            int sessionId,
            DateTime startedAt,
            double resonance = 60,
            double comfort = 70,
            double consistency = 55,
            double intonation = 50,
            double vocalWeight = 45,
            double recovery = 80,
            double pitch = 40,
            double composite = 58)
            => new VoiceIntelligenceTrendPoint
            {
                SessionId = sessionId,
                UserId = 1,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(10),
                ResonanceScore100 = resonance,
                ComfortScore100 = comfort,
                ConsistencyScore100 = consistency,
                IntonationScore100 = intonation,
                VocalWeightScore100 = vocalWeight,
                RecoveryScore100 = recovery,
                PitchScore100 = pitch,
                CompositeVoiceScore = composite
            };

        // ── Mapper: all dimensions preserved 0–100 ────────────────────────────────

        [Fact]
        public void Mapper_MapsAllSevenDimensionsPlusComposite_PreservingValues()
        {
            var started = new DateTime(2026, 5, 1, 9, 0, 0);
            var point = Point(1, started,
                resonance: 61, comfort: 72, consistency: 53, intonation: 49,
                vocalWeight: 44, recovery: 81, pitch: 38, composite: 59);

            var snapshot = VoiceIntelligenceTrendMapper.ToSnapshot(point);

            Assert.Equal(started, snapshot.Timestamp);
            Assert.Equal(61, snapshot.ResonanceDimension);
            Assert.Equal(72, snapshot.ComfortDimension);
            Assert.Equal(53, snapshot.ConsistencyDimension);
            Assert.Equal(49, snapshot.IntonationDimension);
            Assert.Equal(44, snapshot.VocalWeightDimension);
            Assert.Equal(81, snapshot.RecoveryDimension);
            Assert.Equal(38, snapshot.PitchDimension);
            Assert.Equal(59, snapshot.CompositeVoiceScore);

            // Legacy alias fields still populated so existing charts keep working.
            Assert.Equal(61, snapshot.ResonanceScore);
            Assert.Equal(38, snapshot.PitchScore);
            Assert.Equal(49, snapshot.IntonationScore);
            // Voice-health proxy = worst-of (min) the Health pair (Comfort + Recovery),
            // per Sprint D-A COMFORT-01: a rising comfort can never mask a falling recovery.
            Assert.Equal(Math.Min(72, 81), snapshot.VoiceHealthScore);
            // OverallScore mirrors the composite measurement.
            Assert.Equal(59, snapshot.OverallScore);
        }

        [Fact]
        public void Mapper_ClampsOutOfRangeAndNaN_Into0To100()
        {
            var started = new DateTime(2026, 5, 1, 9, 0, 0);
            var point = Point(1, started,
                resonance: 140,                // over 100 -> 100
                comfort: -25,                  // under 0  -> 0
                consistency: double.NaN,       // NaN      -> 0
                intonation: 100,
                vocalWeight: 0,
                recovery: 250,                 // over 100 -> 100
                pitch: -1,                     // under 0  -> 0
                composite: 999);               // over 100 -> 100

            var snapshot = VoiceIntelligenceTrendMapper.ToSnapshot(point);

            Assert.Equal(100, snapshot.ResonanceDimension);
            Assert.Equal(0, snapshot.ComfortDimension);
            Assert.Equal(0, snapshot.ConsistencyDimension);
            Assert.Equal(100, snapshot.IntonationDimension);
            Assert.Equal(0, snapshot.VocalWeightDimension);
            Assert.Equal(100, snapshot.RecoveryDimension);
            Assert.Equal(0, snapshot.PitchDimension);
            Assert.Equal(100, snapshot.CompositeVoiceScore);

            // Every mapped value must remain within the closed 0–100 band.
            foreach (var value in new[]
            {
                snapshot.ResonanceDimension, snapshot.ComfortDimension,
                snapshot.ConsistencyDimension, snapshot.IntonationDimension,
                snapshot.VocalWeightDimension, snapshot.RecoveryDimension,
                snapshot.PitchDimension, snapshot.CompositeVoiceScore,
                snapshot.VoiceHealthScore
            })
            {
                Assert.InRange(value, 0.0, 100.0);
            }
        }

        [Fact]
        public void Mapper_SortsByStartedAt_PreservingChronology()
        {
            var day1 = new DateTime(2026, 5, 1, 9, 0, 0);
            var day2 = new DateTime(2026, 5, 2, 9, 0, 0);
            var day3 = new DateTime(2026, 5, 3, 9, 0, 0);

            // Deliberately out of order on input.
            var trend = new[]
            {
                Point(2, day2, composite: 50),
                Point(3, day3, composite: 60),
                Point(1, day1, composite: 40),
            };

            var snapshots = VoiceIntelligenceTrendMapper.ToSnapshots(trend);

            Assert.Equal(3, snapshots.Count);
            Assert.Equal(day1, snapshots[0].Timestamp);
            Assert.Equal(day2, snapshots[1].Timestamp);
            Assert.Equal(day3, snapshots[2].Timestamp);
            Assert.Equal(40, snapshots[0].CompositeVoiceScore);
            Assert.Equal(50, snapshots[1].CompositeVoiceScore);
            Assert.Equal(60, snapshots[2].CompositeVoiceScore);
        }

        [Fact]
        public void Mapper_NullOrEmptyTrend_YieldsEmptyList_NoCrash()
        {
            Assert.Empty(VoiceIntelligenceTrendMapper.ToSnapshots(null));
            Assert.Empty(VoiceIntelligenceTrendMapper.ToSnapshots(
                Array.Empty<VoiceIntelligenceTrendPoint>()));
        }

        // ── AnalysisPageViewModel: trend application ──────────────────────────────

        [Fact]
        public void AnalysisVm_ApplyTrend_PopulatesCollectionsChronologicallyAndSetsFlag()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            var day1 = new DateTime(2026, 5, 1, 9, 0, 0);
            var day2 = new DateTime(2026, 5, 2, 9, 0, 0);

            vm.ApplyVoiceIntelligenceTrend(new[]
            {
                Point(2, day2, composite: 70),
                Point(1, day1, composite: 30),
            });

            Assert.True(vm.HasVoiceIntelligenceData);
            Assert.Equal(2, vm.VoiceIntelligenceTrend.Count);
            // Chronological after mapping.
            Assert.Equal(day1, vm.VoiceIntelligenceTrend[0].Timestamp);
            Assert.Equal(day2, vm.VoiceIntelligenceTrend[1].Timestamp);
            Assert.Equal(30, vm.VoiceIntelligenceTrend[0].CompositeVoiceScore);
            Assert.Equal(70, vm.VoiceIntelligenceTrend[1].CompositeVoiceScore);

            // Legacy ScoreHistory rebuilt from the same live source.
            Assert.Equal(2, vm.ScoreHistory.Count);

            // The composite "Voice Development" trend series got the two points.
            var devSeries = vm.VoiceDevelopmentPlotModel.Series
                .OfType<OxyPlot.Series.LineSeries>()
                .Single();
            Assert.Equal(2, devSeries.Points.Count);
            Assert.Equal(30, devSeries.Points[0].Y);
            Assert.Equal(70, devSeries.Points[1].Y);
        }

        [Fact]
        public void AnalysisVm_ApplyEmptyTrend_ClearsAndFlagsNoData_NoCrash()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);

            // Seed then clear to prove the collections are reset.
            vm.ApplyVoiceIntelligenceTrend(new[] { Point(1, new DateTime(2026, 5, 1)) });
            Assert.True(vm.HasVoiceIntelligenceData);

            vm.ApplyVoiceIntelligenceTrend(Array.Empty<VoiceIntelligenceTrendPoint>());

            Assert.False(vm.HasVoiceIntelligenceData);
            Assert.Empty(vm.VoiceIntelligenceTrend);
            Assert.Empty(vm.ScoreHistory);
        }

        [Fact]
        public async Task AnalysisVm_LoadFromLiveStore_RoundTripsTrend()
        {
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            var started = DateTime.Now.AddDays(-2);

            await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = 101,
                UserId = 1,
                StartedAt = started,
                EndedAt = started.AddMinutes(12),
                ResonanceScore100 = 64,
                ComfortScore100 = 71,
                ConsistencyScore100 = 58,
                IntonationScore100 = 52,
                VocalWeightScore100 = 47,
                RecoveryScore100 = 83,
                PitchScore100 = 41,
                CompositeVoiceScore = 62
            });

            var vm = new AnalysisPageViewModel(database: null, analyticsStore: store);
            await vm.LoadVoiceIntelligenceTrendAsync(days: 30, userId: 1);

            Assert.True(vm.HasVoiceIntelligenceData);
            Assert.Single(vm.VoiceIntelligenceTrend);
            var snapshot = vm.VoiceIntelligenceTrend[0];
            Assert.Equal(64, snapshot.ResonanceDimension);
            Assert.Equal(71, snapshot.ComfortDimension);
            Assert.Equal(58, snapshot.ConsistencyDimension);
            Assert.Equal(47, snapshot.VocalWeightDimension);
            Assert.Equal(83, snapshot.RecoveryDimension);
            Assert.Equal(62, snapshot.CompositeVoiceScore);
        }

        [Fact]
        public async Task AnalysisVm_LoadWithoutStore_RendersEmpty_NoCrash()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);

            await vm.LoadVoiceIntelligenceTrendAsync();

            Assert.False(vm.HasVoiceIntelligenceData);
            Assert.Empty(vm.VoiceIntelligenceTrend);
        }

        // ── ProgressionDashboardViewModel: dimension/composite exposure ───────────

        [Fact]
        public void ProgressionVm_ApplyTrend_ExposesAllSevenDimensionsPlusComposite()
        {
            // autoLoad=false => no DB / App.Services / WPF host touched.
            var vm = new FemVoiceStudio.Views.ProgressionDashboardViewModel(
                analyticsStore: null, autoLoad: false);

            var d1 = new DateTime(2026, 5, 1, 9, 0, 0);
            var d2 = new DateTime(2026, 5, 1, 18, 0, 0); // same day -> single bar
            var d3 = new DateTime(2026, 5, 2, 9, 0, 0);

            var snapshots = VoiceIntelligenceTrendMapper.ToSnapshots(new[]
            {
                Point(1, d1, resonance: 60, comfort: 70, consistency: 50, intonation: 40,
                    vocalWeight: 30, recovery: 80, pitch: 20, composite: 50),
                Point(2, d2, resonance: 80, comfort: 90, consistency: 70, intonation: 60,
                    vocalWeight: 50, recovery: 100, pitch: 40, composite: 70),
                Point(3, d3, resonance: 40, comfort: 50, consistency: 30, intonation: 20,
                    vocalWeight: 10, recovery: 60, pitch: 0, composite: 30),
            });

            vm.ApplyScoreTrend(snapshots);

            Assert.False(vm.ShowNoDataMessage);
            // Two distinct days -> two history bars.
            Assert.Equal(2, vm.ScoreHistory.Count);

            // Means across the three sessions.
            Assert.Equal(Math.Round((60 + 80 + 40) / 3.0), vm.ResonanceScore);
            Assert.Equal(Math.Round((70 + 90 + 50) / 3.0), vm.ComfortScore);
            Assert.Equal(Math.Round((50 + 70 + 30) / 3.0), vm.ConsistencyScore);
            Assert.Equal(Math.Round((40 + 60 + 20) / 3.0), vm.IntonationScore);
            Assert.Equal(Math.Round((30 + 50 + 10) / 3.0), vm.VocalWeightScore);
            Assert.Equal(Math.Round((80 + 100 + 60) / 3.0), vm.RecoveryScore);
            Assert.Equal(Math.Round((20 + 40 + 0) / 3.0), vm.PitchScore);
            Assert.Equal(Math.Round((50 + 70 + 30) / 3.0), vm.CompositeVoiceScore);

            // Voice-health proxy = worst-of (min) the rounded Comfort + Recovery means,
            // per Sprint D-A COMFORT-01 (a rising comfort can never mask a falling recovery).
            Assert.Equal(Math.Round(Math.Min(vm.ComfortScore, vm.RecoveryScore)), vm.VoiceHealthScore);

            // Bar widths track scores (max 200px).
            Assert.Equal(Math.Min(200, vm.ComfortScore * 2), vm.ComfortBarWidth);
            Assert.Equal(Math.Min(200, vm.RecoveryScore * 2), vm.RecoveryBarWidth);
            Assert.InRange(vm.ConsistencyBarWidth, 0, 200);
        }

        [Fact]
        public void ProgressionVm_EmptyTrend_ShowsNoData_NeutralHealthProtective()
        {
            var vm = new FemVoiceStudio.Views.ProgressionDashboardViewModel(
                analyticsStore: null, autoLoad: false);

            vm.ApplyScoreTrend(Array.Empty<ScoreSnapshot>());

            Assert.True(vm.ShowNoDataMessage);
            Assert.Empty(vm.ScoreHistory);

            // Health dimensions default protective (full), training dimensions neutral,
            // composite 0 — and nothing is gated by these (measurement only).
            Assert.Equal(100, vm.ComfortScore);
            Assert.Equal(100, vm.RecoveryScore);
            Assert.Equal(100, vm.VoiceHealthScore);
            Assert.Equal(50, vm.ResonanceScore);
            Assert.Equal(50, vm.ConsistencyScore);
            Assert.Equal(50, vm.VocalWeightScore);
            Assert.Equal(50, vm.PitchScore);
            Assert.Equal(0, vm.CompositeVoiceScore);
        }

        [Fact]
        public async Task ProgressionVm_LoadFromLiveStore_PopulatesHistoryFromTrend()
        {
            var repository = new InMemorySessionAnalyticsRepository();
            var store = new SessionAnalyticsStore(repository);
            var started = DateTime.Now.AddDays(-1);

            await store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = 7,
                UserId = 1,
                StartedAt = started,
                EndedAt = started.AddMinutes(8),
                ResonanceScore100 = 55,
                ComfortScore100 = 65,
                ConsistencyScore100 = 45,
                IntonationScore100 = 35,
                VocalWeightScore100 = 25,
                RecoveryScore100 = 75,
                PitchScore100 = 15,
                CompositeVoiceScore = 48
            });

            // autoLoad=true pulls the trend synchronously from the live store.
            var vm = new FemVoiceStudio.Views.ProgressionDashboardViewModel(
                analyticsStore: store, autoLoad: true);

            Assert.False(vm.ShowNoDataMessage);
            Assert.Single(vm.ScoreHistory);
            Assert.Equal(55, vm.ResonanceScore);
            Assert.Equal(65, vm.ComfortScore);
            Assert.Equal(75, vm.RecoveryScore);
            Assert.Equal(48, vm.CompositeVoiceScore);
        }

        // ── A9-Dashboard: five longitudinal PlotModel properties ─────────────────────

        /// <summary>
        /// Helper: build a minimal VoiceDevelopmentProfile with two weekly windows
        /// and one monthly window, all with HasEnoughData=true.
        /// </summary>
        private static VoiceDevelopmentProfile BuildTestProfile()
        {
            var now = new DateTime(2026, 5, 15, 12, 0, 0);
            var w7 = new TrendWindow
            {
                WindowDays = 7,
                From = now.AddDays(-7),
                To = now,
                CompositeMean = 60,
                CompositeSlope = 1.5,
                SessionCount = 4,
                HasEnoughData = true,
                DimensionSlopes = new Dictionary<VoiceDimension, double>
                {
                    { VoiceDimension.Recovery, 0.8 },
                    { VoiceDimension.Resonance, 1.2 }
                }
            };
            var w30 = new TrendWindow
            {
                WindowDays = 30,
                From = now.AddDays(-30),
                To = now,
                CompositeMean = 55,
                CompositeSlope = 0.7,
                SessionCount = 10,
                HasEnoughData = true,
                DimensionSlopes = new Dictionary<VoiceDimension, double>
                {
                    { VoiceDimension.Recovery, 0.3 }
                }
            };
            var w90 = new TrendWindow
            {
                WindowDays = 90,
                From = now.AddDays(-90),
                To = now,
                CompositeMean = 50,
                CompositeSlope = 0.4,
                SessionCount = 20,
                HasEnoughData = true,
                DimensionSlopes = new Dictionary<VoiceDimension, double>
                {
                    { VoiceDimension.Recovery, 0.1 }
                }
            };

            return new VoiceDevelopmentProfile
            {
                UserId = 1,
                GeneratedAt = now,
                WeeklyTrend = new[] { w7, w30 },
                MonthlyTrend = new[] { w90 },
                Breakthrough = new BreakthroughState
                {
                    ReasonCode = "BREAKTHROUGH_Resonance",
                    Dimension = VoiceDimension.Resonance,
                    SeverityScore = 72,
                    MagnitudeDelta = 1.1
                },
                Plateau = null,
                Regression = null,
                CompositeVoiceScore = 58,
                HasEnoughData = true
            };
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_AllFivePlotModelsNotNull()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            var profile = BuildTestProfile();

            vm.ApplyDevelopmentProfile(profile);

            Assert.NotNull(vm.WeeklyTrendPlotModel);
            Assert.NotNull(vm.MonthlyTrendPlotModel);
            Assert.NotNull(vm.VoiceDevelopmentLongPlotModel);
            Assert.NotNull(vm.BreakthroughsPlotModel);
            Assert.NotNull(vm.RecoveryPatternsPlotModel);
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_WeeklyTrendSeriesHasWindowCount()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            var profile = BuildTestProfile(); // 2 weekly windows

            vm.ApplyDevelopmentProfile(profile);

            var series = vm.WeeklyTrendPlotModel.Series
                .OfType<LineSeries>()
                .FirstOrDefault();
            Assert.NotNull(series);
            Assert.Equal(2, series!.Points.Count);
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_MonthlyTrendSeriesHasWindowCount()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            var profile = BuildTestProfile(); // 1 monthly window

            vm.ApplyDevelopmentProfile(profile);

            var series = vm.MonthlyTrendPlotModel.Series
                .OfType<LineSeries>()
                .FirstOrDefault();
            Assert.NotNull(series);
            Assert.Single(series!.Points);
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_VoiceDevelopmentLongCoversAllWindows()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            var profile = BuildTestProfile(); // 2 weekly + 1 monthly = 3 total

            vm.ApplyDevelopmentProfile(profile);

            var series = vm.VoiceDevelopmentLongPlotModel.Series
                .OfType<LineSeries>()
                .FirstOrDefault();
            Assert.NotNull(series);
            // All three windows (sorted by From date).
            Assert.Equal(3, series!.Points.Count);
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_BreakthroughsChartHasSeries()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            var profile = BuildTestProfile(); // has Breakthrough, no Plateau/Regression

            vm.ApplyDevelopmentProfile(profile);

            // Exactly one LinearBarSeries for the Breakthrough event.
            var bars = vm.BreakthroughsPlotModel.Series.OfType<LinearBarSeries>().ToList();
            Assert.Single(bars);
            Assert.Single(bars[0].Points);
            Assert.Equal(72, bars[0].Points[0].Y, precision: 1);
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_RecoveryPatternsSeriesHasWindowCount()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            var profile = BuildTestProfile(); // 2 weekly + 1 monthly = 3 windows

            vm.ApplyDevelopmentProfile(profile);

            var series = vm.RecoveryPatternsPlotModel.Series
                .OfType<LineSeries>()
                .FirstOrDefault();
            Assert.NotNull(series);
            Assert.Equal(3, series!.Points.Count);
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_NullProfile_NoCrashAndModelsNotNull()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);

            // Should not throw even with null profile.
            vm.ApplyDevelopmentProfile(null);

            Assert.NotNull(vm.WeeklyTrendPlotModel);
            Assert.NotNull(vm.MonthlyTrendPlotModel);
            Assert.NotNull(vm.VoiceDevelopmentLongPlotModel);
            Assert.NotNull(vm.BreakthroughsPlotModel);
            Assert.NotNull(vm.RecoveryPatternsPlotModel);
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_NullProfile_ChartsAreEmpty()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);

            vm.ApplyDevelopmentProfile(null);

            // Weekly/Monthly/VoiceDevelopmentLong: LineSeries with 0 points.
            var weekSeries = vm.WeeklyTrendPlotModel.Series.OfType<LineSeries>().FirstOrDefault();
            Assert.NotNull(weekSeries);
            Assert.Empty(weekSeries!.Points);

            var monthSeries = vm.MonthlyTrendPlotModel.Series.OfType<LineSeries>().FirstOrDefault();
            Assert.NotNull(monthSeries);
            Assert.Empty(monthSeries!.Points);

            var devSeries = vm.VoiceDevelopmentLongPlotModel.Series.OfType<LineSeries>().FirstOrDefault();
            Assert.NotNull(devSeries);
            Assert.Empty(devSeries!.Points);
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_NoEvents_BreakthroughsPlaceholderNotNull()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            // Profile without any pattern events.
            var emptyProfile = new VoiceDevelopmentProfile
            {
                UserId = 1,
                WeeklyTrend = Array.Empty<TrendWindow>(),
                MonthlyTrend = Array.Empty<TrendWindow>(),
                Breakthrough = null,
                Plateau = null,
                Regression = null,
                HasEnoughData = false
            };

            vm.ApplyDevelopmentProfile(emptyProfile);

            // No LinearBarSeries (no events detected); placeholder LineSeries present.
            Assert.Empty(vm.BreakthroughsPlotModel.Series.OfType<LinearBarSeries>());
            Assert.NotEmpty(vm.BreakthroughsPlotModel.Series.OfType<LineSeries>());
        }

        [Fact]
        public void AnalysisVm_RefreshDevelopmentProfileCharts_NullSmartCoach_NoCrash()
        {
            // smartCoach=null simulates DI unavailable — should degrade silently.
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null, smartCoach: null);

            // Should not throw.
            vm.RefreshDevelopmentProfileCharts();

            // Models must still be non-null (initialised to empty placeholder state).
            Assert.NotNull(vm.WeeklyTrendPlotModel);
            Assert.NotNull(vm.MonthlyTrendPlotModel);
            Assert.NotNull(vm.VoiceDevelopmentLongPlotModel);
            Assert.NotNull(vm.BreakthroughsPlotModel);
            Assert.NotNull(vm.RecoveryPatternsPlotModel);
        }

        [Fact]
        public void AnalysisVm_ApplyVoiceIntelligenceTrend_AlsoCallsRefreshDevelopmentProfile_NoCrash()
        {
            // Verify that the combined orchestration path does not crash when
            // _smartCoach is null (common in test contexts).
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null, smartCoach: null);

            vm.ApplyVoiceIntelligenceTrend(new[]
            {
                Point(1, new DateTime(2026, 5, 1), composite: 55),
                Point(2, new DateTime(2026, 5, 2), composite: 60),
            });

            // Per-session charts still populated.
            Assert.Equal(2, vm.VoiceIntelligenceTrend.Count);
            // Longitudinal models not null (empty placeholder state).
            Assert.NotNull(vm.WeeklyTrendPlotModel);
            Assert.NotNull(vm.RecoveryPatternsPlotModel);
        }

        // ── FIX-B: PatternDescriptions — localised Plateau/Breakthrough/Regression copy ─

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_WithEvents_PatternDescriptionsPopulated()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            // Profile with all three event types to exercise all three Pattern_*_Description paths.
            var profile = new VoiceDevelopmentProfile
            {
                UserId = 1,
                GeneratedAt = new DateTime(2026, 5, 15),
                WeeklyTrend = Array.Empty<TrendWindow>(),
                MonthlyTrend = Array.Empty<TrendWindow>(),
                Breakthrough = new BreakthroughState
                {
                    ReasonCode = "BREAKTHROUGH_Resonance",
                    Dimension = VoiceDimension.Resonance,
                    SeverityScore = 70,
                    MagnitudeDelta = 1.0
                },
                Plateau = new PlateauState
                {
                    ReasonCode = "PLATEAU_Consistency",
                    Dimension = VoiceDimension.Consistency,
                    SeverityScore = 40,
                    WindowDays = 7
                },
                Regression = new RegressionState
                {
                    ReasonCode = "REGRESSION_Comfort",
                    Dimension = VoiceDimension.Comfort,
                    SeverityScore = 55,
                    CompoundSeverity = 60,
                    DeclineSlope = -2.1,
                    WindowDays = 14
                },
                HasEnoughData = true
            };

            vm.ApplyDevelopmentProfile(profile);

            // Three events -> three descriptions.
            Assert.Equal(3, vm.PatternDescriptions.Count);

            // None of them should contain a raw ReasonCode (never shown to user).
            foreach (var desc in vm.PatternDescriptions)
            {
                Assert.DoesNotContain("BREAKTHROUGH_", desc);
                Assert.DoesNotContain("PLATEAU_", desc);
                Assert.DoesNotContain("REGRESSION_", desc);
                // Every description must be a non-empty string.
                Assert.False(string.IsNullOrWhiteSpace(desc));
            }
        }

        [Fact]
        public void AnalysisVm_ApplyDevelopmentProfile_NoEvents_PatternDescriptionsEmpty()
        {
            var vm = new AnalysisPageViewModel(database: null, analyticsStore: null);
            var emptyProfile = new VoiceDevelopmentProfile
            {
                UserId = 1,
                WeeklyTrend = Array.Empty<TrendWindow>(),
                MonthlyTrend = Array.Empty<TrendWindow>(),
                Breakthrough = null,
                Plateau = null,
                Regression = null,
                HasEnoughData = false
            };

            vm.ApplyDevelopmentProfile(emptyProfile);

            Assert.Empty(vm.PatternDescriptions);
        }
    }
}
