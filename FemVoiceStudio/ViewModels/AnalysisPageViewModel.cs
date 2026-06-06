using System;
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
        
        // Collections for charts
        public ObservableCollection<PitchSample> PitchHistory { get; } = new();
        public ObservableCollection<FormantSample> ResonanceTrend { get; } = new();
        public ObservableCollection<ScoreSnapshot> ScoreHistory { get; } = new();
        public ObservableCollection<HealthSnapshot> HealthTrend { get; } = new();
        
        // OxyPlot models for XAML binding - enable declarative chart definition
        [ObservableProperty]
        private PlotModel _resonancePlotModel = CreateResonancePlotModel();
        
        [ObservableProperty]
        private PlotModel _pitchPlotModel = CreatePitchPlotModel();
        
        [ObservableProperty]
        private PlotModel _intonationPlotModel = CreateIntonationPlotModel();
        
        [ObservableProperty]
        private PlotModel _healthPlotModel = CreateHealthPlotModel();
        
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
