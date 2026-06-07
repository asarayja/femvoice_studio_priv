using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FemVoiceStudio.ViewModels;
using FemVoiceStudio.Models;
using FemVoiceStudio.Converters;
using FemVoiceStudio.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Annotations;
using OxyPlot.Series;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Main window for FemVoice Studio application.
    /// Enhanced with FemVoiceScore integration, comfort zones, and health indicators.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly DispatcherTimer _chartUpdateTimer;
        private PlotModel _pitchPlotModel = null!;
        private LineSeries _pitchSeries = null!;
        private RectangleAnnotation _comfortZoneAnnotation = null!;
        private readonly PitchTraceStabilizer _pitchTraceStabilizer = new();
        private DateTime _chartSessionStartTime;
        private DateTime _lastChartRenderAt = DateTime.MinValue;
        private int _lastRenderedPitchSequence;
        private double _lastRenderedPitch;
        private double _chartVoiceSeconds;
        
        private const int MaxDataPoints = 18000;
        private const double DefaultVisibleSeconds = 30;
        private const double MaximumReviewSeconds = 600;
        private const double PitchAxisAbsoluteMinimum = 60;
        private const double PitchAxisAbsoluteMaximum = 500;
        private const double PitchAxisMinimumRange = 50;
        private const double PitchAxisMaximumRange = PitchAxisAbsoluteMaximum - PitchAxisAbsoluteMinimum;
        
        // Data points with stability and health info
        private readonly List<(double X, double Y, bool InRange, StabilityState Stability, HealthState Health)> _pitchDataPoints = new();
        
        // Color constants - following clinical principle: green = comfortable/safe, yellow = outside zone, red = strain/health warning
        private static readonly OxyColor ColorInZone = OxyColor.FromRgb(76, 175, 80);      // Green - comfortable
        private static readonly OxyColor ColorOutOfZone = OxyColor.FromRgb(255, 193, 7);   // Yellow - outside comfort
        private static readonly OxyColor ColorStrain = OxyColor.FromRgb(244, 67, 54);      // Red - strain/health warning
        private static readonly OxyColor ColorNoVoice = OxyColor.FromRgb(158, 158, 158);   // Gray - no voice
        private static readonly OxyColor ColorComfortZoneFill = OxyColor.FromArgb(40, 76, 175, 80); // Green transparent
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize ViewModel
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            
            // Setup chart
            _pitchPlotModel = CreatePitchPlotModel();
            PitchPlotView.Model = _pitchPlotModel;
            
            // Timer for updating chart (30 FPS)
            _chartUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _chartUpdateTimer.Tick += OnChartUpdate;
            
            // Subscribe to ViewModel events
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            
            Closing += OnWindowClosing;
        }
        
        private PlotModel CreatePitchPlotModel()
        {
            var model = new PlotModel
            {
                Title = Loc.Get("Main_PitchGraph"),
                TitleFontSize = 14,
                PlotAreaBorderThickness = new OxyThickness(1)
            };
            
            // X-axis (time)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = Loc.Main_TimeSec,
                Minimum = 0,
                Maximum = DefaultVisibleSeconds,
                AbsoluteMinimum = 0,
                MinimumRange = 5,
                MaximumRange = MaximumReviewSeconds,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                MajorGridlineStyle = LineStyle.Solid,
                FontSize = 10,
                TickStyle = TickStyle.Outside
            });
            
            // Y-axis (pitch in Hz)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = Loc.Main_FrequencyHz,
                Minimum = 100,
                Maximum = 350,
                AbsoluteMinimum = PitchAxisAbsoluteMinimum,
                AbsoluteMaximum = PitchAxisAbsoluteMaximum,
                MinimumRange = PitchAxisMinimumRange,
                MaximumRange = PitchAxisMaximumRange,
                MajorGridlineStyle = LineStyle.Solid,
                FontSize = 10,
                TitleFontSize = 12,
                TickStyle = TickStyle.Outside,
                MajorStep = 50,
                MinorStep = 25
            });
            
            // Comfort zone as an annotation so it always spans the visible chart width.
            _comfortZoneAnnotation = new RectangleAnnotation
            {
                MinimumY = 165,
                MaximumY = 255,
                Fill = ColorComfortZoneFill,
                Stroke = OxyColor.FromArgb(100, 76, 175, 80),
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries
            };
            model.Annotations.Add(_comfortZoneAnnotation);
            
            // Pitch series
            _pitchSeries = new LineSeries
            {
                Title = Loc.Get("Main_YourPitch"),
                Color = OxyColor.FromRgb(0, 120, 215),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColor.FromRgb(0, 120, 215),
                MarkerStroke = OxyColor.FromRgb(0, 80, 160),
                MarkerStrokeThickness = 1
            };
            model.Series.Add(_pitchSeries);

            ApplyPlotTheme(model);
            
            return model;
        }

        private void ApplyPlotTheme(PlotModel model)
        {
            var text = GetOxyColor("TextPrimaryBrush", OxyColor.FromRgb(64, 64, 64));
            var secondaryText = GetOxyColor("TextSecondaryBrush", OxyColor.FromRgb(102, 102, 102));
            var background = GetOxyColor("ChartBackgroundBrush", OxyColors.White);
            var grid = GetOxyColor("ChartGridBrush", OxyColor.FromRgb(230, 230, 230));
            var border = GetOxyColor("BorderPrimaryBrush", OxyColor.FromRgb(200, 200, 200));

            model.Background = background;
            model.TextColor = text;
            model.TitleColor = text;
            model.PlotAreaBorderColor = border;

            foreach (var axis in model.Axes)
            {
                axis.TextColor = secondaryText;
                axis.TitleColor = text;
                axis.AxislineColor = border;
                axis.TicklineColor = border;
                axis.MajorGridlineColor = grid;
                axis.MinorGridlineColor = grid;
            }
        }

        private OxyColor GetOxyColor(string resourceKey, OxyColor fallback)
        {
            try
            {
                if (FindResource(resourceKey) is SolidColorBrush brush)
                    return OxyColor.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
            }
            catch { }

            return fallback;
        }
        
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.IsRecording):
                    if (_viewModel.IsRecording)
                    {
                        ClearChart();
                        _chartSessionStartTime = DateTime.Now;
                        _lastChartRenderAt = DateTime.MinValue;
                        _lastRenderedPitchSequence = 0;
                        _lastRenderedPitch = 0;
                        _chartVoiceSeconds = 0;
                        _pitchTraceStabilizer.Reset();
                        SetTimelineInteraction(false);
                        _chartUpdateTimer.Start();
                    }
                    else
                    {
                        _chartUpdateTimer.Stop();
                        SetTimelineInteraction(true);
                    }
                    break;
                    
                case nameof(MainViewModel.TargetMinPitch):
                case nameof(MainViewModel.TargetMaxPitch):
                case nameof(MainViewModel.ActivePitchTargetZone):
                case nameof(MainViewModel.ComfortZone):
                    UpdatePitchTargetZone();
                    break;
                    
                case nameof(MainViewModel.PitchStability):
                    UpdateStabilityIndicator();
                    break;
                    
                case nameof(MainViewModel.HealthIndicator):
                    UpdateHealthIndicator();
                    break;

                case nameof(MainViewModel.CurrentPitch):
                case nameof(MainViewModel.SmoothedPitch):
                case nameof(MainViewModel.LivePitchUpdateSequence):
                    RenderLatestPitchPoint();
                    break;
            }
        }
        
        private void UpdatePitchTargetZone()
        {
            var minPitch = _viewModel.ActivePitchTargetZone?.Min ?? _viewModel.TargetMinPitch;
            var maxPitch = _viewModel.ActivePitchTargetZone?.Max ?? _viewModel.TargetMaxPitch;
            
            if (_comfortZoneAnnotation != null)
            {
                _comfortZoneAnnotation.MinimumY = minPitch;
                _comfortZoneAnnotation.MaximumY = maxPitch;
            }
            
            // Update Y-axis
            UpdateLiveYAxis(_chartVoiceSeconds);
            
            _pitchPlotModel.InvalidatePlot(true);
        }
        
        private void UpdateStabilityIndicator()
        {
            var stability = _viewModel.PitchStability;
            
            Dispatcher.Invoke(() =>
            {
                switch (stability)
                {
                    case StabilityState.VeryStable:
                        StabilityBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                        StabilityText.Text = Loc.Get("Stability_VeryStable");
                        break;
                    case StabilityState.Stable:
                        StabilityBorder.Background = new SolidColorBrush(Color.FromRgb(139, 195, 74));
                        StabilityText.Text = Loc.Get("Stability_Stable");
                        break;
                    case StabilityState.Developing:
                        StabilityBorder.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                        StabilityText.Text = Loc.Get("Stability_Developing");
                        break;
                    case StabilityState.Unstable:
                        StabilityBorder.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                        StabilityText.Text = Loc.Get("Stability_Unstable");
                        break;
                    default:
                        StabilityBorder.Background = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                        StabilityText.Text = Loc.Get("Stability_NoVoice");
                        break;
                }
            });
        }
        
        private void UpdateHealthIndicator()
        {
            var health = _viewModel.HealthIndicator;

            // Tilgjengelighet: ved StressSensitiveMode skal Danger ikke vises i rødt —
            // selve helse-INFORMASJONEN består (teksten Health_Danger er uendret), men
            // fargen dempes til varm advarsel (gul/oransje) i stedet for rødt. Resolves
            // null-safe via App.Services (kan mangle i design/test-kontekst).
            StressSensitiveExperience? stressSensitive = null;
            try { stressSensitive = App.Services?.GetService(typeof(StressSensitiveExperience)) as StressSensitiveExperience; }
            catch { stressSensitive = null; }
            var softenDanger = stressSensitive?.IsStressSensitive ?? false;

            Dispatcher.Invoke(() =>
            {
                switch (health)
                {
                    case HealthState.Safe:
                        HealthBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                        HealthText.Text = Loc.Get("Health_Safe");
                        break;
                    case HealthState.Monitor:
                        HealthBorder.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                        HealthText.Text = Loc.Get("Health_Monitor");
                        break;
                    case HealthState.Warning:
                        HealthBorder.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                        HealthText.Text = Loc.Get("Health_Warning");
                        break;
                    case HealthState.Danger:
                        // Rød (244,67,54) erstattes av varm advarselsfarge (255,152,0)
                        // når StressSensitiveMode er på. Teksten er identisk uansett.
                        HealthBorder.Background = softenDanger
                            ? new SolidColorBrush(Color.FromRgb(255, 152, 0))
                            : new SolidColorBrush(Color.FromRgb(244, 67, 54));
                        HealthText.Text = Loc.Get("Health_Danger");
                        break;
                    default:
                        HealthBorder.Background = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                        HealthText.Text = Loc.Get("Stability_NoVoice");
                        break;
                }
            });
        }
        
        private void OnChartUpdate(object? sender, EventArgs e)
        {
            RenderLatestPitchPoint();
        }

        private void RenderLatestPitchPoint()
        {
            if (!_viewModel.IsRecording)
                return;

            var rawPitch = _viewModel.CurrentPitch > 0 ? _viewModel.CurrentPitch : _viewModel.SmoothedPitch;
            if (rawPitch <= 0)
                return;

            var now = DateTime.Now;
            var pitch = _pitchTraceStabilizer.Filter(rawPitch, now);
            if (pitch <= 0)
                return;

            var currentSequence = _viewModel.LivePitchUpdateSequence;
            if (currentSequence == _lastRenderedPitchSequence &&
                Math.Abs(pitch - _lastRenderedPitch) < 0.5 &&
                (now - _lastChartRenderAt).TotalMilliseconds < 100)
            {
                return;
            }

            _lastRenderedPitchSequence = currentSequence;
            _lastRenderedPitch = pitch;
            _lastChartRenderAt = now;
            var xPos = Math.Max(0, (now - _chartSessionStartTime).TotalSeconds);
            _chartVoiceSeconds = xPos;

            var minPitch = _viewModel.ActivePitchTargetZone?.Min ?? _viewModel.TargetMinPitch;
            var maxPitch = _viewModel.ActivePitchTargetZone?.Max ?? _viewModel.TargetMaxPitch;

            var isInRange = pitch >= minPitch && pitch <= maxPitch;
            var stability = _viewModel.PitchStability;
            var health = _viewModel.HealthIndicator;

            _pitchDataPoints.Add((xPos, pitch, isInRange, stability, health));

            while (_pitchDataPoints.Count > MaxDataPoints)
            {
                _pitchDataPoints.RemoveAt(0);
            }

            _pitchSeries.Points.Clear();
            foreach (var pt in _pitchDataPoints)
            {
                _pitchSeries.Points.Add(new DataPoint(pt.X, pt.Y));
            }

            var latestPoint = _pitchDataPoints.LastOrDefault();
            OxyColor lineColor;
            if (latestPoint.Y <= 0)
            {
                lineColor = ColorNoVoice;
            }
            else if (latestPoint.Health == HealthState.Danger || latestPoint.Health == HealthState.Warning)
            {
                lineColor = ColorStrain;
            }
            else if (latestPoint.InRange)
            {
                lineColor = ColorInZone;
            }
            else
            {
                lineColor = ColorOutOfZone;
            }
            _pitchSeries.Color = lineColor;
            _pitchSeries.MarkerFill = lineColor;
            _pitchSeries.MarkerStroke = lineColor;

            UpdateLiveXAxis(xPos);
            UpdateLiveYAxis(xPos);
            _pitchPlotModel.InvalidatePlot(false);
        }

        private void UpdateLiveXAxis(double elapsedSeconds)
        {
            var xAxis = _pitchPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            if (xAxis == null)
                return;

            var visibleEnd = Math.Max(DefaultVisibleSeconds, elapsedSeconds);
            var visibleStart = Math.Max(0, visibleEnd - DefaultVisibleSeconds);

            xAxis.AbsoluteMinimum = 0;
            xAxis.AbsoluteMaximum = Math.Max(DefaultVisibleSeconds, elapsedSeconds + 1);
            xAxis.Minimum = visibleStart;
            xAxis.Maximum = visibleEnd;
        }

        private void UpdateLiveYAxis(double elapsedSeconds)
        {
            var yAxis = _pitchPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
            if (yAxis == null)
                return;

            var visibleEnd = Math.Max(DefaultVisibleSeconds, elapsedSeconds);
            var visibleStart = Math.Max(0, visibleEnd - DefaultVisibleSeconds);
            var minPitch = _viewModel.ActivePitchTargetZone?.Min ?? _viewModel.TargetMinPitch;
            var maxPitch = _viewModel.ActivePitchTargetZone?.Max ?? _viewModel.TargetMaxPitch;
            var visiblePitches = _pitchDataPoints
                .Where(point => point.X >= visibleStart && point.X <= visibleEnd)
                .Select(point => point.Y);
            var range = PitchChartAxisRangeCalculator.Calculate(
                visiblePitches,
                minPitch,
                maxPitch,
                PitchAxisAbsoluteMinimum,
                PitchAxisAbsoluteMaximum,
                PitchAxisMinimumRange);

            yAxis.Minimum = range.Minimum;
            yAxis.Maximum = range.Maximum;
            yAxis.AbsoluteMinimum = PitchAxisAbsoluteMinimum;
            yAxis.AbsoluteMaximum = PitchAxisAbsoluteMaximum;
            yAxis.MinimumRange = PitchAxisMinimumRange;
            yAxis.MaximumRange = PitchAxisMaximumRange;
        }

        private void SetTimelineInteraction(bool enabled)
        {
            var xAxis = _pitchPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            if (xAxis == null)
                return;

            xAxis.IsPanEnabled = enabled;
            xAxis.IsZoomEnabled = enabled;
            _pitchPlotModel.InvalidatePlot(false);
        }
        
        private void ClearChart()
        {
            _pitchSeries.Points.Clear();
            _pitchDataPoints.Clear();
            UpdatePitchTargetZone();
            ApplyPlotTheme(_pitchPlotModel);
            _pitchPlotModel.InvalidatePlot(true);
        }
        
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _chartUpdateTimer.Stop();
            _viewModel.Dispose();
        }
        
        private void OnOpenCalendar(object sender, RoutedEventArgs e)
        {
            var calendarWindow = new CalendarWindow();
            calendarWindow.Owner = this;
            calendarWindow.ShowDialog();
        }
        
        private void OnOpenStatistics(object sender, RoutedEventArgs e)
        {
            var statsWindow = new StatisticsWindow();
            statsWindow.Owner = this;
            statsWindow.ShowDialog();
        }
        
        private void OnOpenExerciseGuide(object sender, RoutedEventArgs e)
        {
            var exerciseWindow = new ExerciseWindow();
            exerciseWindow.Owner = this;
            exerciseWindow.ShowDialog();
        }
        
        private void OnOpenAnalyzer(object sender, RoutedEventArgs e)
        {
            var analyzerWindow = new AnalyzerWindow();
            analyzerWindow.Owner = this;
            analyzerWindow.ShowDialog();
        }
        
        private void OnOpenSmartCoach(object sender, RoutedEventArgs e)
        {
            try
            {
                var smartCoachWindow = new SmartCoachDetailWindow();
                smartCoachWindow.Owner = this;
                smartCoachWindow.Show(); // Non-modal - allows using other windows
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Error_OpenWindowFormat"), Loc.Get("Main_SmartCoach"), ex.Message, Environment.NewLine, ex.StackTrace),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnOpenResonance(object sender, RoutedEventArgs e)
        {
            try
            {
                var resonanceWindow = new ResonanceWindow();
                resonanceWindow.Owner = this;
                resonanceWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Error_OpenWindowFormat"), Loc.Get("Main_Resonance"), ex.Message, Environment.NewLine, ex.StackTrace),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnOpenProgression(object sender, RoutedEventArgs e)
        {
            try
            {
                var progressionWindow = new ProgressionWindow();
                progressionWindow.Owner = this;
                progressionWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Error_OpenWindowFormat"), Loc.Get("Main_Progression"), ex.Message, Environment.NewLine, ex.StackTrace),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnOpenAnalysis(object sender, RoutedEventArgs e)
        {
            try
            {
                var analysisWindow = new AnalysisWindow();
                analysisWindow.Owner = this;
                analysisWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Error_OpenWindowFormat"), Loc.Get("Main_Analysis"), ex.Message, Environment.NewLine, ex.StackTrace),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
            
            // Last inn progresjon på nytt etter reset
            _viewModel.LoadUserSettings();
        }
        
        public void RefreshUI()
        {
            // Refresh the UI after language or theme change
            _viewModel.LoadUserSettings();
            UpdatePitchTargetZone();
            
            // Force rebinding of localized strings
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.DataContext = null;
                mainWindow.DataContext = _viewModel;
            }
        }
    }
}
