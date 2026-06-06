using System;
using System.Collections.ObjectModel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using CommunityToolkit.Mvvm.ComponentModel;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// ViewModel for sanntids pitch-graf med OxyPlot.
    /// </summary>
    public class PitchChartViewModel : ObservableObject
    {
        private readonly PlotModel _plotModel;
        private readonly LineSeries _pitchSeries;
        private readonly AreaSeries _targetAreaSeries;
        
        private const int MaxDataPoints = 300;
        
        public PlotModel PlotModel => _plotModel;
        
        public double TargetMinPitchValue { get; set; } = 165;
        public double TargetMaxPitchValue { get; set; } = 255;
        
        public PitchChartViewModel()
        {
            _plotModel = new PlotModel
            {
                Title = LocalizationService.Instance["AnalysisChart_PitchTitle"],
                TitleFontSize = 14,
                TitleColor = OxyColor.FromRgb(64, 64, 64),
                PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200),
                Background = OxyColors.White
            };
            
            // X-akse (tid)
            _plotModel.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = LocalizationService.Instance["Chart_TimeAxis"],
                StringFormat = "HH:mm:ss",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColor.FromRgb(250, 250, 250),
                FontSize = 10
            });
            
            // Y-akse (pitch i Hz)
            _plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = LocalizationService.Instance["Main_FrequencyHz"],
                Minimum = 100,
                Maximum = 350,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(230, 230, 230),
                FontSize = 10,
                TitleFontSize = 12
            });
            
            // Mål-område (grønt område)
            _targetAreaSeries = new AreaSeries
            {
                Color = OxyColor.FromArgb(50, 0, 200, 100),
                Fill = OxyColor.FromArgb(30, 0, 200, 100)
            };
            _plotModel.Series.Add(_targetAreaSeries);
            
            // Pitch serie (blå linje)
            _pitchSeries = new LineSeries
            {
                Title = LocalizationService.Instance["Main_YourPitch"],
                Color = OxyColor.FromRgb(0, 120, 215),
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };
            _plotModel.Series.Add(_pitchSeries);
            
            // Sett standard mål-område
            UpdateTargetArea(TargetMinPitchValue, TargetMaxPitchValue);
        }
        
        private void UpdateTargetArea(double minPitch, double maxPitch)
        {
            _targetAreaSeries.Points.Clear();
            _targetAreaSeries.Points2.Clear();
            
            var now = DateTimeAxis.ToDouble(DateTime.Now);
            
            // Legg til punkter for å lage et område
            for (int i = -MaxDataPoints; i <= 0; i += 10)
            {
                var t = now + i / 86400.0;
                _targetAreaSeries.Points.Add(new DataPoint(t, minPitch));
                _targetAreaSeries.Points2.Add(new DataPoint(t, maxPitch));
            }
            
            // Oppdater Y-akse
            var yAxis = _plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
            if (yAxis != null)
            {
                yAxis.Minimum = Math.Max(80, minPitch - 30);
                yAxis.Maximum = Math.Min(400, maxPitch + 50);
            }
            
            _plotModel.InvalidatePlot(true);
        }
        
        public void AddPitchPoint(double pitch, bool isInRange)
        {
            var timestamp = DateTimeAxis.ToDouble(DateTime.Now);
            
            _pitchSeries.Points.Add(new DataPoint(timestamp, pitch));
            
            // Fjern gamle punkter
            while (_pitchSeries.Points.Count > MaxDataPoints)
            {
                _pitchSeries.Points.RemoveAt(0);
            }
            
            // Oppdater farge basert på om pitch er i målområdet
            if (_pitchSeries.Points.Count > 0)
            {
                var color = isInRange ? OxyColor.FromRgb(0, 180, 0) : OxyColor.FromRgb(255, 100, 100);
                
                // Siste punkt med egen farge
                _plotModel.InvalidatePlot(false);
            }
        }
        
        public void Clear()
        {
            _pitchSeries.Points.Clear();
            _plotModel.InvalidatePlot(false);
        }
    }
}
