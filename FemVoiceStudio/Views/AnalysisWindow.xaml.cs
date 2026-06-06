using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FemVoiceStudio.ViewModels;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Analysis window for detailed voice analysis and session review.
    /// Provides deep insights into resonance, pitch, prosody, and health trends.
    /// </summary>
    public partial class AnalysisWindow : Window
    {
        private readonly AnalysisPageViewModel _viewModel;
        
        public AnalysisWindow()
        {
            InitializeComponent();
            
            _viewModel = new AnalysisPageViewModel();
            
            // Initialize charts
            InitializeResonanceChart();
            InitializePitchChart();
            InitializeIntonationChart();
            InitializeHealthChart();
            
            // Load data
            LoadLatestSessionData();
        }
        
        /// <summary>
        /// Load data from the most recent session
        /// </summary>
        private void LoadLatestSessionData()
        {
            // Get the most recent session data
            _viewModel.LoadTrendAnalysis(30);
            
            // If we have data, display it
            if (_viewModel.ScoreHistory.Count > 0)
            {
                UpdateUIFromViewModel();
            }
        }
        
        private void UpdateUIFromViewModel()
        {
            // Update stats
            TotalScoreText.Text = $"{_viewModel.TotalScore:F0}%";
            AvgPitchText.Text = $"{_viewModel.AveragePitch:F0} Hz";
            StabilityText.Text = $"{_viewModel.PitchStability:F0}%";
            DurationText.Text = $"{_viewModel.SessionDuration.TotalMinutes:F0} min";
            
            // Update score bars
            UpdateScoreBar(ResonanceBar, _viewModel.AverageResonance, 250);
            UpdateScoreBar(PitchBar, _viewModel.PitchStability, 250); // Using stability as proxy
            UpdateScoreBar(IntonationBar, _viewModel.IntonationRange, 250);
            UpdateScoreBar(VoiceHealthBar, 100 - _viewModel.StrainLevel, 250);
            
            // Update score text
            ResonanceScoreText.Text = $"{_viewModel.AverageResonance:F0}%";
            PitchScoreText.Text = $"{_viewModel.PitchStability:F0}%";
            IntonationScoreText.Text = $"{_viewModel.IntonationRange:F0}%";
            HealthScoreText.Text = $"{100 - _viewModel.StrainLevel:F0}%";
            
            // Update summary
            SessionSummaryText.Text = _viewModel.SessionSummary;
            
            // Update charts with data
            UpdateCharts();
        }
        
        private void UpdateScoreBar(System.Windows.Controls.Border bar, double score, double maxWidth)
        {
            if (bar != null && maxWidth > 0)
            {
                bar.Width = Math.Max(0, Math.Min(maxWidth, (score / 100.0) * maxWidth));
            }
        }
        
        private void InitializeResonanceChart()
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
            
            ResonancePlotView.Model = model;
        }
        
        private void InitializePitchChart()
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
            
            PitchPlotView.Model = model;
        }
        
        private void InitializeIntonationChart()
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
            
            IntonationPlotView.Model = model;
        }
        
        private void InitializeHealthChart()
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
            
            HealthPlotView.Model = model;
        }
        
        private void UpdateCharts()
        {
            UpdateResonanceChart();
            UpdatePitchChart();
            UpdateIntonationChart();
            UpdateHealthChart();
        }
        
        private void UpdateResonanceChart()
        {
            var model = ResonancePlotView.Model;
            if (model == null) return;
            
            model.Series.Clear();
            
            var series = new LineSeries
            {
                Title = LocalizationService.Instance["AnalysisChart_F2Series"],
                Color = OxyColor.FromRgb(255, 107, 157),  // Pink
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColor.FromRgb(255, 107, 157)
            };
            
            int index = 0;
            foreach (var sample in _viewModel.ResonanceTrend)
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
            for (int i = 0; i < Math.Max(1, _viewModel.ResonanceTrend.Count); i++)
            {
                thresholdLine.Points.Add(new DataPoint(i, 1400));
            }
            
            model.Series.Add(series);
            model.Series.Add(thresholdLine);
            
            model.InvalidatePlot(true);
            
            // Update forward indicator
            if (_viewModel.AverageResonance > 50)
            {
                ForwardIndicator.Visibility = Visibility.Visible;
            }
            else
            {
                ForwardIndicator.Visibility = Visibility.Collapsed;
            }
        }
        
        private void UpdatePitchChart()
        {
            var model = PitchPlotView.Model;
            if (model == null) return;
            
            model.Series.Clear();
            
            var series = new LineSeries
            {
                Title = LocalizationService.Instance["Dashboard_Pitch"],
                Color = OxyColor.FromRgb(79, 195, 247),  // Light blue
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColor.FromRgb(79, 195, 247)
            };
            
            int index = 0;
            foreach (var sample in _viewModel.PitchHistory)
            {
                series.Points.Add(new DataPoint(index++, sample.Pitch));
            }
            
            // Add average line
            if (_viewModel.PitchHistory.Count > 0)
            {
                var avgPitch = _viewModel.AveragePitch;
                var avgLine = new LineSeries
                {
                    Color = OxyColor.FromRgb(255, 152, 0),
                    StrokeThickness = 1,
                    LineStyle = LineStyle.Dash
                };
                for (int i = 0; i < Math.Max(1, _viewModel.PitchHistory.Count); i++)
                {
                    avgLine.Points.Add(new DataPoint(i, avgPitch));
                }
                model.Series.Add(avgLine);
            }
            
            model.Series.Add(series);
            
            // Update Y-axis based on data
            var yAxis = model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
            if (yAxis != null && _viewModel.PitchHistory.Count > 0)
            {
                var pitches = _viewModel.PitchHistory.Select(p => p.Pitch).ToList();
                yAxis.Minimum = Math.Max(80, pitches.Min() - 30);
                yAxis.Maximum = Math.Min(400, pitches.Max() + 30);
            }
            
            model.InvalidatePlot(true);
        }
        
        private void UpdateIntonationChart()
        {
            var model = IntonationPlotView.Model;
            if (model == null) return;
            
            model.Series.Clear();
            
            var series = new LineSeries
            {
                Title = LocalizationService.Instance["Dashboard_Intonation"],
                Color = OxyColor.FromRgb(129, 199, 132),  // Green
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColor.FromRgb(129, 199, 132)
            };
            
            int index = 0;
            foreach (var score in _viewModel.ScoreHistory)
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
            foreach (var _ in _viewModel.ScoreHistory)
            {
                idealRange.Points.Add(new DataPoint(rangeIndex, 30));
                idealRange.Points2.Add(new DataPoint(rangeIndex, 120));
                rangeIndex++;
            }
            model.Series.Add(idealRange);
            
            model.Series.Add(series);
            
            model.InvalidatePlot(true);
            
            // Update monotony indicator
            if (_viewModel.IntonationRange < 30)
            {
                MonotonyIndicator.Visibility = Visibility.Visible;
            }
            else
            {
                MonotonyIndicator.Visibility = Visibility.Collapsed;
            }
        }
        
        private void UpdateHealthChart()
        {
            var model = HealthPlotView.Model;
            if (model == null) return;
            
            model.Series.Clear();
            
            // Strain series
            var strainSeries = new LineSeries
            {
                Title = LocalizationService.Instance["AnalysisChart_StrainSeries"],
                Color = OxyColor.FromRgb(244, 67, 54),  // Red
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColor.FromRgb(244, 67, 54)
            };
            
            // Fatigue series
            var fatigueSeries = new LineSeries
            {
                Title = LocalizationService.Instance["AnalysisChart_FatigueSeries"],
                Color = OxyColor.FromRgb(255, 152, 0),  // Orange
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColor.FromRgb(255, 152, 0)
            };
            
            int index = 0;
            foreach (var health in _viewModel.HealthTrend)
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
            for (int i = 0; i < Math.Max(1, _viewModel.HealthTrend.Count); i++)
            {
                safeLine.Points.Add(new DataPoint(i, 30));
            }
            
            model.Series.Add(strainSeries);
            model.Series.Add(fatigueSeries);
            model.Series.Add(safeLine);
            
            model.InvalidatePlot(true);
        }
    }
}
