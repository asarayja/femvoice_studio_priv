using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using CommunityToolkit.Mvvm.ComponentModel;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// ViewModel for resonans-visualisering med OxyPlot.
    /// Inkluderer F1/F2 scatter plot, formant-timeline og resonans-score gauge.
    /// </summary>
    public class ResonanceChartViewModel : ObservableObject
    {
        #region PlotModels
        
        private readonly PlotModel _f1F2ScatterModel;
        private readonly PlotModel _formantTimelineModel;
        
        #endregion
        
        #region Scatter Plot Series
        
        private readonly ScatterSeries _formantSeries;
        private readonly ScatterSeries _targetAreaSeries;
        private readonly ScatterSeries _currentPositionSeries;
        
        #endregion
        
        #region Timeline Series
        
        private readonly LineSeries _f1Series;
        private readonly LineSeries _f2Series;
        private readonly LineSeries _f3Series;
        private readonly LineSeries _resonanceScoreSeries;
        
        #endregion
        
        #region Configuration
        
        private const int MaxTimelinePoints = 300;
        
        // Feminine formant target areas
        private const double TargetF1Min = 280.0;
        private const double TargetF1Max = 450.0;
        private const double TargetF2Min = 1800.0;
        private const double TargetF2Max = 2600.0;
        private const double TargetF1Optimal = 330.0;
        private const double TargetF2Optimal = 2200.0;
        
        #endregion
        
        #region Public Properties
        
        public PlotModel F1F2ScatterModel => _f1F2ScatterModel;
        public PlotModel FormantTimelineModel => _formantTimelineModel;
        
        /// <summary>
        /// Current resonance score (0-100)
        /// </summary>
        public double CurrentResonanceScore { get; private set; }
        
        /// <summary>
        /// Current formant position
        /// </summary>
        public double CurrentF1 { get; private set; }
        public double CurrentF2 { get; private set; }
        public double CurrentF3 { get; private set; }
        
        /// <summary>
        /// Resonance category description
        /// </summary>
        public string ResonanceCategoryDescription => GetCategoryDescription();
        
        /// <summary>
        /// Is current position in target area
        /// </summary>
        public bool IsInTargetArea => CurrentF1 >= TargetF1Min && CurrentF1 <= TargetF1Max && 
                                       CurrentF2 >= TargetF2Min && CurrentF2 <= TargetF2Max;
        
        #endregion
        
        #region Constructor
        
        public ResonanceChartViewModel()
        {
            // Initialize F1/F2 Scatter Plot
            _f1F2ScatterModel = CreateF1F2ScatterModel();
            
            // Initialize Formant Timeline
            _formantTimelineModel = CreateFormantTimelineModel();
            
            // Initialize scatter series
            _formantSeries = new ScatterSeries
            {
                Title = LocalizationService.Instance["ResonanceChart_FormantHistory"],
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColor.FromArgb(150, 0, 120, 215),
                MarkerStroke = OxyColor.FromRgb(0, 100, 180),
                MarkerStrokeThickness = 1
            };
            
            _targetAreaSeries = new ScatterSeries
            {
                Title = LocalizationService.Instance["ResonanceChart_TargetArea"],
                MarkerType = MarkerType.Square,
                MarkerSize = 8,
                MarkerFill = OxyColor.FromArgb(50, 0, 200, 100),
                MarkerStroke = OxyColor.FromRgb(0, 180, 80),
                MarkerStrokeThickness = 2
            };
            
            _currentPositionSeries = new ScatterSeries
            {
                Title = LocalizationService.Instance["ResonanceChart_Current"],
                MarkerType = MarkerType.Circle,
                MarkerSize = 10,
                MarkerFill = OxyColor.FromRgb(255, 100, 100),
                MarkerStroke = OxyColor.FromRgb(200, 50, 50),
                MarkerStrokeThickness = 2
            };
            
            // Initialize timeline series
            _f1Series = new LineSeries
            {
                Title = LocalizationService.Instance["ResonanceChart_F1Series"],
                Color = OxyColor.FromRgb(255, 100, 100),
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };
            
            _f2Series = new LineSeries
            {
                Title = LocalizationService.Instance["ResonanceChart_F2Series"],
                Color = OxyColor.FromRgb(100, 150, 255),
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };
            
            _f3Series = new LineSeries
            {
                Title = LocalizationService.Instance["ResonanceChart_F3Series"],
                Color = OxyColor.FromRgb(100, 200, 100),
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };
            
            _resonanceScoreSeries = new LineSeries
            {
                Title = LocalizationService.Instance["ResonanceWindow_ResonanceScore"],
                Color = OxyColor.FromRgb(200, 100, 200),
                StrokeThickness = 2,
                MarkerType = MarkerType.None,
                YAxisKey = "ScoreAxis"
            };
            
            // Add series to models
            _f1F2ScatterModel.Series.Add(_formantSeries);
            _f1F2ScatterModel.Series.Add(_targetAreaSeries);
            _f1F2ScatterModel.Series.Add(_currentPositionSeries);
            
            _formantTimelineModel.Series.Add(_f1Series);
            _formantTimelineModel.Series.Add(_f2Series);
            _formantTimelineModel.Series.Add(_f3Series);
            _formantTimelineModel.Series.Add(_resonanceScoreSeries);
            
            // Add target area to timeline
            AddTargetAreaToTimeline();
            
            // Initialize target area points
            InitializeTargetArea();
        }
        
        #endregion
        
        #region Private Methods - Model Creation
        
        private PlotModel CreateF1F2ScatterModel()
        {
            var model = new PlotModel
            {
                Title = LocalizationService.Instance["ResonanceWindow_F1F2Position"],
                TitleFontSize = 14,
                TitleColor = OxyColor.FromRgb(64, 64, 64),
                PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200),
                Background = OxyColors.White,
                PlotAreaBackground = OxyColors.White
            };
            
            // X-axis (F2 - higher = more forward)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = LocalizationService.Instance["ResonanceChart_F2ForwardAxis"],
                Minimum = 800,
                Maximum = 3000,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColor.FromRgb(250, 250, 250),
                FontSize = 10,
                TitleFontSize = 12,
                MajorStep = 500
            });
            
            // Y-axis (F1 - lower = more forward)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LocalizationService.Instance["ResonanceChart_F1ForwardAxis"],
                Minimum = 200,
                Maximum = 700,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                FontSize = 10,
                TitleFontSize = 12,
                MajorStep = 100
            });
            
            return model;
        }
        
        private PlotModel CreateFormantTimelineModel()
        {
            var model = new PlotModel
            {
                Title = LocalizationService.Instance["ResonanceWindow_FormantTimeline"],
                TitleFontSize = 14,
                TitleColor = OxyColor.FromRgb(64, 64, 64),
                PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200),
                Background = OxyColors.White,
                PlotAreaBackground = OxyColors.White
            };
            
            // X-axis (time)
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = LocalizationService.Instance["Chart_TimeAxis"],
                StringFormat = "HH:mm:ss",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
                FontSize = 10,
                TitleFontSize = 12
            });
            
            // Y-axis (frequency)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LocalizationService.Instance["Main_FrequencyHz"],
                Minimum = 0,
                Maximum = 3500,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                FontSize = 10,
                TitleFontSize = 12,
                MajorStep = 500
            });
            
            // Secondary Y-axis for resonance score
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Right,
                Key = "ScoreAxis",
                Title = LocalizationService.Instance["Dashboard_Score"],
                Minimum = 0,
                Maximum = 100,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(240, 230, 240),
                FontSize = 10,
                TitleFontSize = 12,
                MajorStep = 20,
                AxislineColor = OxyColor.FromRgb(200, 100, 200)
            });
            
            return model;
        }
        
        private void AddTargetAreaToTimeline()
        {
            var now = DateTimeAxis.ToDouble(DateTime.Now);
            
            // F1 target area (horizontal band)
            var f1TargetLine = new LineSeries
            {
                Color = OxyColor.FromArgb(80, 255, 100, 100),
                StrokeThickness = 8,
                MarkerType = MarkerType.None
            };
            
            // F2 target area (horizontal band)
            var f2TargetLine = new LineSeries
            {
                Color = OxyColor.FromArgb(80, 100, 150, 255),
                StrokeThickness = 8,
                MarkerType = MarkerType.None
            };
            
            for (int i = -MaxTimelinePoints; i <= 0; i += 10)
            {
                var t = now + i / 86400.0;
                f1TargetLine.Points.Add(new DataPoint(t, TargetF1Optimal));
                f2TargetLine.Points.Add(new DataPoint(t, TargetF2Optimal));
            }
            
            _formantTimelineModel.Series.Insert(0, f1TargetLine);
            _formantTimelineModel.Series.Insert(1, f2TargetLine);
        }
        
        private void InitializeTargetArea()
        {
            // Add target area corner points
            _targetAreaSeries.Points.Add(new ScatterPoint(TargetF1Min, TargetF2Min));
            _targetAreaSeries.Points.Add(new ScatterPoint(TargetF1Min, TargetF2Max));
            _targetAreaSeries.Points.Add(new ScatterPoint(TargetF1Max, TargetF2Max));
            _targetAreaSeries.Points.Add(new ScatterPoint(TargetF1Max, TargetF2Min));
            
            // Add optimal point
            _targetAreaSeries.Points.Add(new ScatterPoint(TargetF1Optimal, TargetF2Optimal));
        }
        
        private string GetCategoryDescription()
        {
            if (CurrentResonanceScore >= 70)
                return LocalizationService.Instance["ResonanceChart_CategoryForwardComfortable"];
            else if (CurrentResonanceScore >= 50)
                return LocalizationService.Instance["ResonanceChart_CategoryDevelopingForward"];
            else if (CurrentF2 > 0)
                return LocalizationService.Instance["ResonanceChart_CategoryBackResonance"];
            else return LocalizationService.Instance["ResonanceChart_CategoryAnalyzing"];
        }
        
        #endregion
        
        #region Public Methods - Data Updates
        
        /// <summary>
        /// Updates scatter plot with new formant data point.
        /// </summary>
        public void AddFormantPoint(double f1, double f2, double f3, double resonanceScore)
        {
            var timestamp = DateTimeAxis.ToDouble(DateTime.Now);
            
            // Add to scatter history
            if (f1 > 0 && f2 > 0)
            {
                _formantSeries.Points.Add(new ScatterPoint(f1, f2, 1.0));
                
                // Limit history
                while (_formantSeries.Points.Count > MaxTimelinePoints)
                {
                    _formantSeries.Points.RemoveAt(0);
                }
                
                CurrentF1 = f1;
                CurrentF2 = f2;
                CurrentF3 = f3;
            }
            
            CurrentResonanceScore = resonanceScore;
            
            // Update current position marker
            _currentPositionSeries.Points.Clear();
            if (f1 > 0 && f2 > 0)
            {
                _currentPositionSeries.Points.Add(new ScatterPoint(f1, f2, 2.0));
            }
            
            // Add to timeline
            _f1Series.Points.Add(new DataPoint(timestamp, f1));
            _f2Series.Points.Add(new DataPoint(timestamp, f2));
            _f3Series.Points.Add(new DataPoint(timestamp, f3));
            _resonanceScoreSeries.Points.Add(new DataPoint(timestamp, resonanceScore));
            
            // Limit timeline data
            while (_f1Series.Points.Count > MaxTimelinePoints)
            {
                _f1Series.Points.RemoveAt(0);
                _f2Series.Points.RemoveAt(0);
                _f3Series.Points.RemoveAt(0);
                _resonanceScoreSeries.Points.RemoveAt(0);
            }
            
            // Invalidate plots
            _f1F2ScatterModel.InvalidatePlot(true);
            _formantTimelineModel.InvalidatePlot(true);
            
            // Notify property changes
            OnPropertyChanged(nameof(CurrentResonanceScore));
            OnPropertyChanged(nameof(CurrentF1));
            OnPropertyChanged(nameof(CurrentF2));
            OnPropertyChanged(nameof(CurrentF3));
            OnPropertyChanged(nameof(ResonanceCategoryDescription));
            OnPropertyChanged(nameof(IsInTargetArea));
        }
        
        /// <summary>
        /// Clears all data (for new session).
        /// </summary>
        public void Clear()
        {
            _formantSeries.Points.Clear();
            _currentPositionSeries.Points.Clear();
            _f1Series.Points.Clear();
            _f2Series.Points.Clear();
            _f3Series.Points.Clear();
            _resonanceScoreSeries.Points.Clear();
            
            CurrentF1 = 0;
            CurrentF2 = 0;
            CurrentF3 = 0;
            CurrentResonanceScore = 0;
            
            _f1F2ScatterModel.InvalidatePlot(true);
            _formantTimelineModel.InvalidatePlot(true);
            
            OnPropertyChanged(nameof(CurrentResonanceScore));
            OnPropertyChanged(nameof(CurrentF1));
            OnPropertyChanged(nameof(CurrentF2));
            OnPropertyChanged(nameof(CurrentF3));
            OnPropertyChanged(nameof(ResonanceCategoryDescription));
            OnPropertyChanged(nameof(IsInTargetArea));
        }
        
        /// <summary>
        /// Updates target area boundaries.
        /// </summary>
        public void UpdateTargetArea(double f1Min, double f1Max, double f2Min, double f2Max)
        {
            // Update axes
            foreach (var axis in _f1F2ScatterModel.Axes)
            {
                if (axis.Position == AxisPosition.Left)
                {
                    axis.Minimum = Math.Min(f1Min - 50, axis.Minimum);
                    axis.Maximum = Math.Max(f1Max + 50, axis.Maximum);
                }
                else if (axis.Position == AxisPosition.Bottom)
                {
                    axis.Minimum = Math.Min(f2Min - 100, axis.Minimum);
                    axis.Maximum = Math.Max(f2Max + 100, axis.Maximum);
                }
            }
            
            // Update target area points
            _targetAreaSeries.Points.Clear();
            _targetAreaSeries.Points.Add(new ScatterPoint(f1Min, f2Min));
            _targetAreaSeries.Points.Add(new ScatterPoint(f1Min, f2Max));
            _targetAreaSeries.Points.Add(new ScatterPoint(f1Max, f2Max));
            _targetAreaSeries.Points.Add(new ScatterPoint(f1Max, f2Min));
            
            _f1F2ScatterModel.InvalidatePlot(true);
        }

        #endregion
    }
}
