using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Services.Progression;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// ViewModel for detailed voice analysis and session review.
    /// Provides deep insights into resonance, pitch, prosody, and health trends.
    /// </summary>
    public partial class AnalysisPageViewModel : ObservableObject
    {
        private readonly DatabaseService _database;
        private readonly FemVoiceScore _femVoiceScore;
        private readonly SessionAnalyticsStore? _analyticsStore;

        // SmartCoachEngine: source for longitudinal development profile (Bølge 2).
        // Resolved from DI; null in design-time / test contexts (handled gracefully).
        private readonly SmartCoachEngine? _smartCoach;

        // Collections for charts
        public ObservableCollection<PitchSample> PitchHistory { get; } = new();
        public ObservableCollection<FormantSample> ResonanceTrend { get; } = new();
        public ObservableCollection<ScoreSnapshot> ScoreHistory { get; } = new();
        public ObservableCollection<HealthSnapshot> HealthTrend { get; } = new();

        // Voice Intelligence trend (Sprint B): the eight 0–100 scores per completed
        // session, chronological, sourced from SessionAnalyticsStore (the LIVE table) —
        // replacing the dead GetTrainingSessions/GetFemVoiceScores source for the new
        // dimension trends. ScoreHistory above is rebuilt from the same trend.
        public ObservableCollection<ScoreSnapshot> VoiceIntelligenceTrend { get; } = new();

        // True once a Voice Intelligence trend load found at least one session.
        [ObservableProperty]
        private bool _hasVoiceIntelligenceData;

        // OxyPlot models for XAML binding - enable declarative chart definition
        [ObservableProperty]
        private PlotModel _resonancePlotModel = CreateResonancePlotModel();

        [ObservableProperty]
        private PlotModel _pitchPlotModel = CreatePitchPlotModel();

        [ObservableProperty]
        private PlotModel _intonationPlotModel = CreateIntonationPlotModel();

        [ObservableProperty]
        private PlotModel _healthPlotModel = CreateHealthPlotModel();

        // ── Sprint B: trend models for the previously-unvisualised dimensions ───────
        // {loc}-TODO: dimension titles are English literals; add Strings.resx keys later.
        [ObservableProperty]
        private PlotModel _comfortPlotModel = CreateDimensionPlotModel("Comfort");

        [ObservableProperty]
        private PlotModel _consistencyPlotModel = CreateDimensionPlotModel("Consistency");

        [ObservableProperty]
        private PlotModel _vocalWeightPlotModel = CreateDimensionPlotModel("Vocal Weight");

        [ObservableProperty]
        private PlotModel _recoveryPlotModel = CreateDimensionPlotModel("Recovery");

        [ObservableProperty]
        private PlotModel _voiceDevelopmentPlotModel = CreateDimensionPlotModel("Voice Development");

        // ── A9-Dashboard: five longitudinal sections sourced from SmartCoachEngine ──
        // Titles via {loc:Loc Dashboard_*} in XAML; models use window-bucketed data.

        /// <summary>Weekly trend windows (7/30 d) — composite slope per window.</summary>
        [ObservableProperty]
        private PlotModel _weeklyTrendPlotModel = CreateDimensionPlotModel("Weekly Trend");

        /// <summary>Monthly trend windows (90/180 d) — composite slope per window.</summary>
        [ObservableProperty]
        private PlotModel _monthlyTrendPlotModel = CreateDimensionPlotModel("Monthly Trend");

        /// <summary>Voice Development — composite score across all windows.</summary>
        [ObservableProperty]
        private PlotModel _voiceDevelopmentLongPlotModel = CreateDimensionPlotModel("Voice Development");

        /// <summary>Breakthroughs / Plateau / Regression — severity per detected event.</summary>
        [ObservableProperty]
        private PlotModel _breakthroughsPlotModel = CreateDimensionPlotModel("Breakthroughs");

        /// <summary>Recovery patterns — Recovery-dimension slope per window.</summary>
        [ObservableProperty]
        private PlotModel _recoveryPatternsPlotModel = CreateDimensionPlotModel("Recovery Patterns");

        [ObservableProperty]
        private double _averagePitch;
        
        [ObservableProperty]
        private double _pitchStability;
        
        [ObservableProperty]
        private double _pitchRange;
        
        [ObservableProperty]
        private double _averageResonance;
        
        [ObservableProperty]
        private double _resonanceStability;
        
        [ObservableProperty]
        private double _intonationRange;
        
        [ObservableProperty]
        private double _intonationVariation;
        
        [ObservableProperty]
        private double _strainLevel;
        
        [ObservableProperty]
        private double _fatigueLevel;
        
        [ObservableProperty]
        private double _intensityControl;
        
        [ObservableProperty]
        private DateTime _sessionStartTime;
        
        [ObservableProperty]
        private DateTime _sessionEndTime;
        
        [ObservableProperty]
        private TimeSpan _sessionDuration;
        
        [ObservableProperty]
        private double _totalScore;
        
        [ObservableProperty]
        private string _sessionSummary = "";
        
        // ============================
        // Complexity Properties
        // ============================
        
        [ObservableProperty]
        private SpeechComplexityLevel _currentComplexityLevel;
        
        [ObservableProperty]
        private string _currentComplexityLevelDisplay = "";
        
        [ObservableProperty]
        private int _sessionsAtCurrentComplexity;
        
        [ObservableProperty]
        private bool _isComplexityReadyForNext;
        
        [ObservableProperty]
        private string _complexityStatusSummary = "";
        
        [ObservableProperty]
        private List<ComplexityLevelStep> _complexityProgressionSteps = new();
        
        public AnalysisPageViewModel()
        {
            // DatabaseService er DI-singleton; manuelle new re-kjørte skjema-init (integrasjonsaudit-funn).
            _database = App.Services?.GetService(typeof(DatabaseService)) as DatabaseService
                        ?? new DatabaseService();
            _femVoiceScore = new FemVoiceScore();
            // SessionAnalyticsStore is the LIVE Voice Intelligence trend source (Bølge 1).
            // Resolve from DI; null in design-time / no-DI contexts (handled gracefully).
            _analyticsStore = App.Services?.GetService(typeof(SessionAnalyticsStore)) as SessionAnalyticsStore;
            // SmartCoachEngine is the source for the longitudinal development profile (Bølge 2).
            // Same resolution pattern as MainViewModel. Null if DI container is not available.
            _smartCoach = App.Services?.GetService(typeof(SmartCoachEngine)) as SmartCoachEngine;
        }

        /// <summary>
        /// Test/DI seam: inject the analytics store and (optionally) SmartCoachEngine
        /// directly so both the Voice Intelligence trend and the longitudinal development
        /// profile can be exercised without the App.Services container or any WPF host.
        /// <paramref name="database"/> may be null for pure trend-mapping tests — the
        /// trend path (<see cref="ApplyVoiceIntelligenceTrend"/>/
        /// <see cref="LoadVoiceIntelligenceTrendAsync"/>) never touches the database.
        /// </summary>
        public AnalysisPageViewModel(
            DatabaseService? database,
            SessionAnalyticsStore? analyticsStore,
            SmartCoachEngine? smartCoach = null)
        {
            _database = database!;
            _femVoiceScore = new FemVoiceScore();
            _analyticsStore = analyticsStore;
            _smartCoach = smartCoach;
        }
        
        /// <summary>
        /// Load analysis data for a specific session
        /// </summary>
        public void LoadSessionAnalysis(int sessionId)
        {
            // Get sessions and find by ID - simplified approach
            var sessions = _database.GetRecentSessions(100);
            var session = sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session == null)
                return;
            
            LoadSessionData(session);
        }
        
        /// <summary>
        /// Load analysis data from session
        /// </summary>
        public void LoadSessionData(TrainingSession session)
        {
            SessionStartTime = session.StartTime;
            SessionEndTime = session.EndTime ?? DateTime.Now;
            SessionDuration = session.EndTime.HasValue 
                ? session.EndTime.Value - session.StartTime 
                : TimeSpan.Zero;
            
            AveragePitch = session.AveragePitch;
            PitchRange = session.MaxPitch - session.MinPitch;
            PitchStability = CalculateStabilityScore(session);
            
            AverageResonance = session.ResonanceScore;
            ResonanceStability = CalculateResonanceStability(session);
            
            IntonationRange = session.IntonationScore;
            IntonationVariation = session.PitchVariation;
            
            // Health metrics (simplified - estimate from available data)
            StrainLevel = CalculateStrainLevel(session);
            FatigueLevel = CalculateFatigueLevel(session);
            IntensityControl = 70; // Default estimate
            
            // Calculate total score
            TotalScore = session.OverallScore;
            
            // Generate summary
            GenerateSummary(session);
            
            // Load historical data for trends
            LoadHistoricalData();
        }
        
        /// <summary>
        /// Calculate pitch stability score (0-100)
        /// Lower variance = higher stability
        /// </summary>
        private double CalculateStabilityScore(TrainingSession session)
        {
            if (session.PitchVariation <= 0)
                return 50;
            
            // Invert variation to create stability score
            // Low variation = high stability
            double stability = Math.Max(0, 100 - session.PitchVariation * 3);
            return Math.Min(100, stability);
        }
        
        /// <summary>
        /// Calculate resonance stability
        /// </summary>
        private double CalculateResonanceStability(TrainingSession session)
        {
            if (session.ResonanceScore <= 0)
                return 50;
            
            // Resonance stability based on score
            return session.ResonanceScore;
        }
        
        /// <summary>
        /// Calculate strain level (simplified)
        /// </summary>
        private double CalculateStrainLevel(TrainingSession session)
        {
            // Would use detailed audio analysis in production
            // Here we estimate from pitch and intensity
            double strain = 0;
            
            if (session.AveragePitch > 260)
                strain += 20;
            
            // Estimate intensity from available data
            double estimatedIntensity = session.SpectralCentroid > 0 ? session.SpectralCentroid / 5000.0 : 0.5;
            if (estimatedIntensity > 0.7)
                strain += 30;
            
            return Math.Min(100, strain);
        }
        
        /// <summary>
        /// Calculate fatigue level
        /// </summary>
        private double CalculateFatigueLevel(TrainingSession session)
        {
            // Would use historical data in production
            // Estimate from session duration and variation
            double fatigue = 0;
            
            if (SessionDuration.TotalMinutes > 15)
                fatigue += 20;
            if (session.PitchVariation > 30)
                fatigue += 20;
            
            return Math.Min(100, fatigue);
        }
        
        /// <summary>
        /// Generate human-readable session summary
        /// </summary>
        private void GenerateSummary(TrainingSession session)
        {
            var summary = new System.Text.StringBuilder();
            
            // Pitch summary
            if (AveragePitch >= 165 && AveragePitch <= 255)
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_PitchInComfort"]);
            else if (AveragePitch < 165)
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_PitchCanRise"]);
            else
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_PitchHigh"]);
            
            // Resonance summary
            if (AverageResonance >= 60)
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_ResonanceGood"]);
            else if (AverageResonance >= 40)
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_ResonanceProgress"]);
            else
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_ResonanceFocus"]);
            
            // Stability summary
            if (PitchStability >= 70)
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_StabilityGood"]);
            else if (PitchStability >= 50)
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_StabilityOk"]);
            else
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_StabilityImprove"]);
            
            // Health summary
            if (StrainLevel < 30)
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_LoadLow"]);
            else if (StrainLevel < 60)
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_LoadWatch"]);
            else
                summary.AppendLine(LocalizationService.Instance["AnalysisSummary_LoadRest"]);
            
            SessionSummary = summary.ToString();
        }
        
        /// <summary>
        /// Load historical data for trend visualization
        /// </summary>
        private void LoadHistoricalData()
        {
            // Load complexity data
            LoadComplexityData();
            
            // Get recent sessions for trend analysis
            var recentSessions = _database.GetRecentSessions(10);
            
            foreach (var session in recentSessions.OrderBy(s => s.StartTime))
            {
                // Add to pitch history
                PitchHistory.Add(new PitchSample
                {
                    Timestamp = session.StartTime,
                    Pitch = session.AveragePitch,
                    SmoothedPitch = session.AveragePitch,
                    Confidence = 0.8,
                    IsVoiced = true,
                    IsInComfortZone = session.AveragePitch >= 165 && session.AveragePitch <= 255
                });
                
                // Add to resonance trend
                ResonanceTrend.Add(new FormantSample
                {
                    Timestamp = session.StartTime,
                    F2 = session.AverageF2 > 0 ? session.AverageF2 : 1500,
                    ResonanceScore = session.ResonanceScore,
                    IsForwardResonance = session.ResonanceScore > 50
                });
                
                // Add to score history
                ScoreHistory.Add(new ScoreSnapshot
                {
                    Timestamp = session.StartTime,
                    OverallScore = session.OverallScore,
                    ResonanceScore = session.ResonanceScore,
                    PitchScore = CalculatePitchScore(session),
                    IntonationScore = session.IntonationScore,
                    VoiceHealthScore = 100 - StrainLevel
                });
            }
        }
        
        private double CalculatePitchScore(TrainingSession session)
        {
            // Simplified pitch score calculation
            if (session.AveragePitch >= 165 && session.AveragePitch <= 255)
                return 80;
            if (session.AveragePitch < 165)
                return Math.Max(0, 70 - (165 - session.AveragePitch));
            return Math.Max(0, 70 - (session.AveragePitch - 255) * 2);
        }
        
        /// <summary>
        /// Get analysis for multiple sessions (trend analysis)
        /// </summary>
        public void LoadTrendAnalysis(int days = 30)
        {
            var from = DateTime.Now.AddDays(-days);
            var sessions = _database.GetTrainingSessions(from, DateTime.Now);
            
            PitchHistory.Clear();
            ResonanceTrend.Clear();
            ScoreHistory.Clear();
            HealthTrend.Clear();
            
            foreach (var session in sessions.OrderBy(s => s.StartTime))
            {
                ScoreHistory.Add(new ScoreSnapshot
                {
                    Timestamp = session.StartTime,
                    OverallScore = session.OverallScore,
                    ResonanceScore = session.ResonanceScore,
                    PitchScore = CalculatePitchScore(session),
                    IntonationScore = session.IntonationScore,
                    VoiceHealthScore = 80
                });
                
                HealthTrend.Add(new HealthSnapshot
                {
                    Timestamp = session.StartTime,
                    StrainLevel = CalculateStrainLevel(session),
                    FatigueLevel = CalculateFatigueLevel(session),
                    IntensityControl = 80,
                    StrainDetected = false
                });
            }
        }

        /// <summary>
        /// Sprint B: load the Voice Intelligence trend (the eight 0–100 scores per
        /// completed session, chronological) from the LIVE
        /// <see cref="SessionAnalyticsStore"/> — replacing the dead
        /// <c>GetTrainingSessions</c>/<c>GetFemVoiceScores</c> tables for the new
        /// dimension trends. Falls back to an empty trend (no crash) when no store is
        /// wired or the window is empty.
        /// </summary>
        public async Task LoadVoiceIntelligenceTrendAsync(int days = 30, int userId = 1)
        {
            IReadOnlyList<VoiceIntelligenceTrendPoint> points = Array.Empty<VoiceIntelligenceTrendPoint>();

            if (_analyticsStore != null)
            {
                var to = DateTime.Now;
                var from = to.AddDays(-days);
                try
                {
                    points = await _analyticsStore.GetVoiceIntelligenceTrendAsync(from, to, userId)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Trend is a read-only visualisation; never let a load failure crash
                    // the analysis surface. Empty trend => "ikke nok data".
                    points = Array.Empty<VoiceIntelligenceTrendPoint>();
                }
            }

            ApplyVoiceIntelligenceTrend(points);
        }

        /// <summary>
        /// Pure VM step: map a Voice Intelligence trend into the bound collections and
        /// refresh the dimension plot models. Extracted (no DB, no async, no WPF host)
        /// so the mapping — 0–100 preservation, chronology, empty-history handling, and
        /// exposure of all seven dimensions + composite — is unit-testable.
        /// </summary>
        public void ApplyVoiceIntelligenceTrend(IEnumerable<VoiceIntelligenceTrendPoint>? trend)
        {
            var snapshots = VoiceIntelligenceTrendMapper.ToSnapshots(trend);

            VoiceIntelligenceTrend.Clear();
            ScoreHistory.Clear();
            foreach (var snapshot in snapshots)
            {
                VoiceIntelligenceTrend.Add(snapshot);
                // Rebuild legacy ScoreHistory from the same live source so existing
                // resonance/pitch/intonation charts no longer read the dead tables.
                ScoreHistory.Add(snapshot);
            }

            HasVoiceIntelligenceData = snapshots.Count > 0;

            UpdateComfortChartData();
            UpdateConsistencyChartData();
            UpdateVocalWeightChartData();
            UpdateRecoveryChartData();
            UpdateVoiceDevelopmentChartData();

            // A9: refresh the five longitudinal (window-bucketed) sections.
            // RefreshDevelopmentProfileCharts is null-safe — no crash if _smartCoach is null.
            RefreshDevelopmentProfileCharts();
        }
        
        #region OxyPlot Model Creation
        
        /// <summary>
        /// Creates the resonance plot model (F2 trend)
        /// </summary>
        private static PlotModel CreateResonancePlotModel()
        {
            var model = new PlotModel
            {
                Title = LocalizationService.Instance["AnalysisChart_ResonanceTitle"],
                TitleFontSize = 12,
                TitleColor = OxyColor.FromRgb(64, 64, 64),
                Background = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200)
            };
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = LocalizationService.Instance["AnalysisChart_SessionAxis"],
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
                FontSize = 10
            });
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LocalizationService.Instance["AnalysisChart_F2Axis"],
                Minimum = 1000,
                Maximum = 2500,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                FontSize = 10,
                TitleFontSize = 11
            });
            
            return model;
        }
        
        /// <summary>
        /// Creates the pitch plot model with comfort zone
        /// </summary>
        private static PlotModel CreatePitchPlotModel()
        {
            var model = new PlotModel
            {
                Title = LocalizationService.Instance["AnalysisChart_PitchTitle"],
                TitleFontSize = 12,
                TitleColor = OxyColor.FromRgb(64, 64, 64),
                Background = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200)
            };
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = LocalizationService.Instance["AnalysisChart_SessionAxis"],
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
                FontSize = 10
            });
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LocalizationService.Instance["AnalysisChart_PitchAxis"],
                Minimum = 100,
                Maximum = 350,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                FontSize = 10,
                TitleFontSize = 11
            });
            
            // Add comfort zone as AreaSeries
            var comfortZone = new AreaSeries
            {
                Color = OxyColor.FromArgb(80, 76, 175, 80),
                Fill = OxyColor.FromArgb(40, 76, 175, 80)
            };
            for (int i = 0; i < 30; i++)
            {
                comfortZone.Points.Add(new DataPoint(i, 165));
                comfortZone.Points2.Add(new DataPoint(i, 255));
            }
            model.Series.Add(comfortZone);
            
            return model;
        }
        
        /// <summary>
        /// Creates the intonation/prosody plot model
        /// </summary>
        private static PlotModel CreateIntonationPlotModel()
        {
            var model = new PlotModel
            {
                Title = LocalizationService.Instance["AnalysisChart_IntonationTitle"],
                TitleFontSize = 12,
                TitleColor = OxyColor.FromRgb(64, 64, 64),
                Background = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200)
            };
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = LocalizationService.Instance["AnalysisChart_SessionAxis"],
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
                FontSize = 10
            });
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LocalizationService.Instance["AnalysisChart_RangeAxis"],
                Minimum = 0,
                Maximum = 150,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                FontSize = 10,
                TitleFontSize = 11
            });
            
            return model;
        }
        
        /// <summary>
        /// Creates the health monitoring plot model
        /// </summary>
        private static PlotModel CreateHealthPlotModel()
        {
            var model = new PlotModel
            {
                Title = LocalizationService.Instance["AnalysisChart_HealthTitle"],
                TitleFontSize = 12,
                TitleColor = OxyColor.FromRgb(64, 64, 64),
                Background = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200)
            };
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = LocalizationService.Instance["AnalysisChart_SessionAxis"],
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
                FontSize = 10
            });
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LocalizationService.Instance["AnalysisChart_LevelAxis"],
                Minimum = 0,
                Maximum = 100,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                FontSize = 10,
                TitleFontSize = 11
            });
            
            return model;
        }
        
        /// <summary>
        /// Creates a generic 0–100 Voice Intelligence dimension trend model (Sprint B).
        /// Same OxyPlot template as the existing charts; one factory for all new
        /// dimensions (Comfort/Consistency/VocalWeight/Recovery/Voice Development).
        /// {loc}-TODO: <paramref name="title"/> is an English literal; add resx keys.
        /// </summary>
        private static PlotModel CreateDimensionPlotModel(string title)
        {
            var model = new PlotModel
            {
                Title = title,
                TitleFontSize = 12,
                TitleColor = OxyColor.FromRgb(64, 64, 64),
                Background = OxyColors.White,
                PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200)
            };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = LocalizationService.Instance["AnalysisChart_SessionAxis"],
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
                FontSize = 10
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LocalizationService.Instance["AnalysisChart_LevelAxis"],
                Minimum = 0,
                Maximum = 100,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                FontSize = 10,
                TitleFontSize = 11
            });

            return model;
        }

        /// <summary>
        /// Shared helper: rebuild a 0–100 dimension line series from the Voice
        /// Intelligence trend using a per-snapshot selector. Keeps chronology (index
        /// order = chronological because the source is ordered by StartedAt).
        /// </summary>
        private void UpdateDimensionChartData(
            PlotModel model,
            string seriesTitle,
            OxyColor color,
            Func<ScoreSnapshot, double> selector)
        {
            model.Series.Clear();

            var series = new LineSeries
            {
                Title = seriesTitle,
                Color = color,
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };

            int index = 0;
            foreach (var snapshot in VoiceIntelligenceTrend)
            {
                series.Points.Add(new DataPoint(index++, selector(snapshot)));
            }

            model.Series.Add(series);
            model.InvalidatePlot(false);
        }

        /// <summary>Updates the Comfort (Health) 0–100 trend.</summary>
        public void UpdateComfortChartData() =>
            UpdateDimensionChartData(ComfortPlotModel, "Comfort",
                OxyColor.FromRgb(102, 187, 106), s => s.ComfortDimension);

        /// <summary>Updates the Consistency 0–100 trend.</summary>
        public void UpdateConsistencyChartData() =>
            UpdateDimensionChartData(ConsistencyPlotModel, "Consistency",
                OxyColor.FromRgb(126, 87, 194), s => s.ConsistencyDimension);

        /// <summary>Updates the Vocal Weight 0–100 trend.</summary>
        public void UpdateVocalWeightChartData() =>
            UpdateDimensionChartData(VocalWeightPlotModel, "Vocal Weight",
                OxyColor.FromRgb(255, 167, 38), s => s.VocalWeightDimension);

        /// <summary>Updates the Recovery (Health) 0–100 trend.</summary>
        public void UpdateRecoveryChartData() =>
            UpdateDimensionChartData(RecoveryPlotModel, "Recovery",
                OxyColor.FromRgb(38, 198, 218), s => s.RecoveryDimension);

        /// <summary>Updates the composite "Voice Development" 0–100 trend.</summary>
        public void UpdateVoiceDevelopmentChartData() =>
            UpdateDimensionChartData(VoiceDevelopmentPlotModel, "Voice Development",
                OxyColor.FromRgb(255, 107, 157), s => s.CompositeVoiceScore);

        // ── A9-Dashboard: longitudinal window-bucketed chart updates ─────────────────
        // These five methods consume VoiceDevelopmentProfile (window-bucketed, one point
        // per TrendWindow) rather than VoiceIntelligenceTrend (per-session). They share
        // the same CreateDimensionPlotModel template and are orchestrated by
        // ApplyDevelopmentProfile, called from ApplyVoiceIntelligenceTrend.

        /// <summary>
        /// Shared helper: build a window-bucketed LineSeries from a list of TrendWindows.
        /// One DataPoint per window; X = window index (chronological), Y = selector result.
        /// Safe for empty/null window lists (yields an empty but valid chart).
        /// </summary>
        private static void UpdateWindowChartData(
            PlotModel model,
            string seriesTitle,
            OxyColor color,
            IReadOnlyList<TrendWindow> windows,
            Func<TrendWindow, double> ySelector)
        {
            model.Series.Clear();

            var series = new LineSeries
            {
                Title = seriesTitle,
                Color = color,
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };

            for (int i = 0; i < windows.Count; i++)
            {
                series.Points.Add(new DataPoint(i, ySelector(windows[i])));
            }

            model.Series.Add(series);
            model.InvalidatePlot(false);
        }

        /// <summary>
        /// Updates the Weekly Trend chart from WeeklyTrend windows (7/30 d).
        /// Plots composite mean per window; clears and shows placeholder on no data.
        /// </summary>
        public void UpdateWeeklyTrendChartData(IReadOnlyList<TrendWindow>? windows)
        {
            var safe = windows ?? Array.Empty<TrendWindow>();
            UpdateWindowChartData(
                WeeklyTrendPlotModel,
                LocalizationService.Instance["Dashboard_WeeklyTrend"],
                OxyColor.FromRgb(126, 87, 194),
                safe,
                w => w.CompositeMean);
        }

        /// <summary>
        /// Updates the Monthly Trend chart from MonthlyTrend windows (90/180 d).
        /// </summary>
        public void UpdateMonthlyTrendChartData(IReadOnlyList<TrendWindow>? windows)
        {
            var safe = windows ?? Array.Empty<TrendWindow>();
            UpdateWindowChartData(
                MonthlyTrendPlotModel,
                LocalizationService.Instance["Dashboard_MonthlyTrend"],
                OxyColor.FromRgb(38, 198, 218),
                safe,
                w => w.CompositeMean);
        }

        /// <summary>
        /// Updates the Voice Development (longitudinal) chart: composite score per window,
        /// all windows (weekly + monthly) in chronological order by From date.
        /// </summary>
        public void UpdateVoiceDevelopmentLongChartData(
            IReadOnlyList<TrendWindow>? weeklyWindows,
            IReadOnlyList<TrendWindow>? monthlyWindows)
        {
            VoiceDevelopmentLongPlotModel.Series.Clear();

            var allWindows = (weeklyWindows ?? Array.Empty<TrendWindow>())
                .Concat(monthlyWindows ?? Array.Empty<TrendWindow>())
                .OrderBy(w => w.From)
                .ToList();

            var series = new LineSeries
            {
                Title = LocalizationService.Instance["Dashboard_VoiceDevelopment"],
                Color = OxyColor.FromRgb(255, 107, 157),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };

            for (int i = 0; i < allWindows.Count; i++)
            {
                series.Points.Add(new DataPoint(i, allWindows[i].CompositeMean));
            }

            VoiceDevelopmentLongPlotModel.Series.Add(series);
            VoiceDevelopmentLongPlotModel.InvalidatePlot(false);
        }

        /// <summary>
        /// Updates the Breakthroughs chart: visualises detected Breakthrough, Plateau and
        /// Regression events as vertical bars. Each event maps to a severity score on the
        /// Y-axis; X-axis is a sequential event-index. Empty when no events detected.
        /// LinearBarSeries is used (OxyPlot 2.x compatible; uses DataPoints on linear axes).
        /// </summary>
        public void UpdateBreakthroughsChartData(
            BreakthroughState? breakthrough,
            PlateauState? plateau,
            RegressionState? regression)
        {
            BreakthroughsPlotModel.Series.Clear();

            // Build (label, value, color) triples for each detected event.
            var events = new List<(string label, double severity, OxyColor color)>();
            if (breakthrough != null)
                events.Add(("B", breakthrough.SeverityScore, OxyColor.FromRgb(102, 187, 106)));
            if (plateau != null)
                events.Add(("P", plateau.SeverityScore, OxyColor.FromRgb(255, 167, 38)));
            if (regression != null)
                events.Add(("R", regression.CompoundSeverity, OxyColor.FromRgb(244, 67, 54)));

            if (events.Count > 0)
            {
                // One LinearBarSeries per event so each can have its own colour.
                for (int i = 0; i < events.Count; i++)
                {
                    var (label, severity, color) = events[i];
                    var bar = new LinearBarSeries
                    {
                        Title = label,
                        FillColor = color,
                        StrokeColor = OxyColors.White,
                        StrokeThickness = 1,
                        BarWidth = 0.6
                    };
                    bar.Points.Add(new DataPoint(i, severity));
                    BreakthroughsPlotModel.Series.Add(bar);
                }
            }
            else
            {
                // Placeholder: empty series — chart is valid, just blank.
                BreakthroughsPlotModel.Series.Add(new LineSeries
                {
                    Title = LocalizationService.Instance["Dashboard_Breakthroughs"],
                    Color = OxyColor.FromRgb(200, 200, 200),
                    StrokeThickness = 1
                });
            }

            BreakthroughsPlotModel.InvalidatePlot(false);
        }

        /// <summary>
        /// Updates the Recovery Patterns chart: Recovery-dimension slope per window
        /// (weekly + monthly, chronological). Positive slope = improving recovery.
        /// </summary>
        public void UpdateRecoveryPatternsChartData(
            IReadOnlyList<TrendWindow>? weeklyWindows,
            IReadOnlyList<TrendWindow>? monthlyWindows)
        {
            RecoveryPatternsPlotModel.Series.Clear();

            var allWindows = (weeklyWindows ?? Array.Empty<TrendWindow>())
                .Concat(monthlyWindows ?? Array.Empty<TrendWindow>())
                .OrderBy(w => w.From)
                .ToList();

            var series = new LineSeries
            {
                Title = LocalizationService.Instance["Dashboard_RecoveryPatterns"],
                Color = OxyColor.FromRgb(38, 198, 218),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };

            for (int i = 0; i < allWindows.Count; i++)
            {
                var w = allWindows[i];
                double recoverySlope = w.DimensionSlopes.TryGetValue(VoiceDimension.Recovery, out var slope)
                    ? slope
                    : 0.0;
                series.Points.Add(new DataPoint(i, recoverySlope));
            }

            RecoveryPatternsPlotModel.Series.Add(series);
            RecoveryPatternsPlotModel.InvalidatePlot(false);
        }

        /// <summary>
        /// Orchestrates all five A9 longitudinal charts from a
        /// <see cref="VoiceDevelopmentProfile"/>. Null-safe: null profile or
        /// HasEnoughData==false leaves the charts empty (no crash).
        /// Called from <see cref="ApplyVoiceIntelligenceTrend"/> via
        /// <see cref="RefreshDevelopmentProfileCharts"/>.
        /// </summary>
        public void ApplyDevelopmentProfile(VoiceDevelopmentProfile? profile)
        {
            if (profile == null)
            {
                // Render empty placeholder charts — no crash.
                UpdateWeeklyTrendChartData(null);
                UpdateMonthlyTrendChartData(null);
                UpdateVoiceDevelopmentLongChartData(null, null);
                UpdateBreakthroughsChartData(null, null, null);
                UpdateRecoveryPatternsChartData(null, null);
                return;
            }

            UpdateWeeklyTrendChartData(profile.WeeklyTrend);
            UpdateMonthlyTrendChartData(profile.MonthlyTrend);
            UpdateVoiceDevelopmentLongChartData(profile.WeeklyTrend, profile.MonthlyTrend);
            UpdateBreakthroughsChartData(profile.Breakthrough, profile.Plateau, profile.Regression);
            UpdateRecoveryPatternsChartData(profile.WeeklyTrend, profile.MonthlyTrend);
        }

        /// <summary>
        /// Fetches the development profile from SmartCoachEngine (null-safe) and
        /// applies it to the five longitudinal charts. Never throws.
        /// </summary>
        public void RefreshDevelopmentProfileCharts(int userId = 1)
        {
            try
            {
                var profile = _smartCoach?.TryBuildDevelopmentProfile(userId, DateTime.Now);
                ApplyDevelopmentProfile(profile);
            }
            catch
            {
                // Descriptive visualisation only — never crash the dashboard.
                ApplyDevelopmentProfile(null);
            }
        }

        /// <summary>
        /// Efficiently updates resonance chart data using partial invalidation
        /// </summary>
        public void UpdateResonanceChartData()
        {
            ResonancePlotModel.Series.Clear();
            
            var series = new LineSeries
            {
                Title = LocalizationService.Instance["AnalysisChart_F2Series"],
                Color = OxyColor.FromRgb(255, 107, 157),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };
            
            int index = 0;
            foreach (var sample in ResonanceTrend)
            {
                series.Points.Add(new DataPoint(index++, sample.F2));
            }
            
            // Add forward resonance threshold line (F2 > 1400)
            var thresholdLine = new LineSeries
            {
                Color = OxyColor.FromRgb(76, 175, 80),
                StrokeThickness = 1,
                LineStyle = LineStyle.Dash
            };
            for (int i = 0; i < Math.Max(1, ResonanceTrend.Count); i++)
            {
                thresholdLine.Points.Add(new DataPoint(i, 1400));
            }
            
            ResonancePlotModel.Series.Add(series);
            ResonancePlotModel.Series.Add(thresholdLine);
            
            // Partial update for performance
            ResonancePlotModel.InvalidatePlot(false);
        }
        
        /// <summary>
        /// Efficiently updates pitch chart data using partial invalidation
        /// </summary>
        public void UpdatePitchChartData()
        {
            PitchPlotModel.Series.Clear();
            
            var series = new LineSeries
            {
                Title = LocalizationService.Instance["Dashboard_Pitch"],
                Color = OxyColor.FromRgb(79, 195, 247),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };
            
            int index = 0;
            foreach (var sample in PitchHistory)
            {
                series.Points.Add(new DataPoint(index++, sample.Pitch));
            }
            
            // Add average line if data exists
            if (PitchHistory.Count > 0)
            {
                var avgPitch = PitchHistory.Average(p => p.Pitch);
                var avgLine = new LineSeries
                {
                    Color = OxyColor.FromRgb(255, 152, 0),
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Dash
                };
                for (int i = 0; i < Math.Max(1, PitchHistory.Count); i++)
                {
                    avgLine.Points.Add(new DataPoint(i, avgPitch));
                }
                PitchPlotModel.Series.Add(avgLine);
            }
            
            PitchPlotModel.Series.Add(series);
            
            // Update Y-axis based on data
            var yAxis = PitchPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
            if (yAxis != null && PitchHistory.Count > 0)
            {
                var pitches = PitchHistory.Select(p => p.Pitch).ToList();
                yAxis.Minimum = Math.Max(80, pitches.Min() - 30);
                yAxis.Maximum = Math.Min(400, pitches.Max() + 30);
            }
            
            PitchPlotModel.InvalidatePlot(false);
        }
        
        /// <summary>
        /// Efficiently updates intonation chart data using partial invalidation
        /// </summary>
        public void UpdateIntonationChartData()
        {
            IntonationPlotModel.Series.Clear();
            
            var series = new LineSeries
            {
                Title = LocalizationService.Instance["Dashboard_Intonation"],
                Color = OxyColor.FromRgb(129, 199, 132),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };
            
            int index = 0;
            foreach (var score in ScoreHistory)
            {
                series.Points.Add(new DataPoint(index++, score.IntonationScore));
            }
            
            // Add ideal range as AreaSeries (30-120 Hz)
            var idealRange = new AreaSeries
            {
                Color = OxyColor.FromArgb(50, 76, 175, 80),
                Fill = OxyColor.FromArgb(30, 76, 175, 80)
            };
            int rangeIndex = 0;
            foreach (var _ in ScoreHistory)
            {
                idealRange.Points.Add(new DataPoint(rangeIndex, 30));
                idealRange.Points2.Add(new DataPoint(rangeIndex, 120));
                rangeIndex++;
            }
            IntonationPlotModel.Series.Add(idealRange);
            
            IntonationPlotModel.Series.Add(series);
            IntonationPlotModel.InvalidatePlot(false);
        }
        
        /// <summary>
        /// Efficiently updates health chart data using partial invalidation
        /// </summary>
        public void UpdateHealthChartData()
        {
            HealthPlotModel.Series.Clear();
            
            var strainSeries = new LineSeries
            {
                Title = LocalizationService.Instance["AnalysisChart_StrainSeries"],
                Color = OxyColor.FromRgb(244, 67, 54),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };
            
            var fatigueSeries = new LineSeries
            {
                Title = LocalizationService.Instance["AnalysisChart_FatigueSeries"],
                Color = OxyColor.FromRgb(255, 152, 0),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };
            
            int index = 0;
            foreach (var health in HealthTrend)
            {
                strainSeries.Points.Add(new DataPoint(index, health.StrainLevel));
                fatigueSeries.Points.Add(new DataPoint(index, health.FatigueLevel));
                index++;
            }
            
            // Add safe threshold
            var safeLine = new LineSeries
            {
                Color = OxyColor.FromRgb(76, 175, 80),
                StrokeThickness = 1,
                LineStyle = LineStyle.Dash
            };
            for (int i = 0; i < Math.Max(1, HealthTrend.Count); i++)
            {
                safeLine.Points.Add(new DataPoint(i, 30));
            }
            
            HealthPlotModel.Series.Add(strainSeries);
            HealthPlotModel.Series.Add(fatigueSeries);
            HealthPlotModel.Series.Add(safeLine);
            
            HealthPlotModel.InvalidatePlot(false);
        }
        
        /// <summary>
        /// Loads complexity progression data for analysis page.
        /// </summary>
        private void LoadComplexityData()
        {
            try
            {
                var complexityEngine = new ComplexityEngine(_database);
                var evaluation = complexityEngine.EvaluateCurrentLevel(1);
                
                CurrentComplexityLevel = evaluation.CurrentLevel;
                CurrentComplexityLevelDisplay = ComplexityLevelStep.GetDisplayName(evaluation.CurrentLevel);
                SessionsAtCurrentComplexity = evaluation.SessionsAtCurrentLevel;
                IsComplexityReadyForNext = evaluation.IsReadyForNext;
                
                // Generate status summary
                var complexityEngine2 = new ComplexityEngine(_database);
                ComplexityStatusSummary = complexityEngine2.GenerateComplexityFeedback(evaluation);
                
                ComplexityProgressionSteps = complexityEngine.GetProgressionSteps(1);
            }
            catch
            {
                // Fallback values
                CurrentComplexityLevel = SpeechComplexityLevel.IsolatedSounds;
                CurrentComplexityLevelDisplay = LocalizationService.Instance["Complexity_IsolatedSounds"];
                SessionsAtCurrentComplexity = 0;
                IsComplexityReadyForNext = false;
                ComplexityStatusSummary = LocalizationService.Instance["Analysis_ComplexityLoadFailed"];
            }
        }
        
        #endregion
    }
}
