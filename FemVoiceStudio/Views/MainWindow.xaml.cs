using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        private CalendarWindow? _calendarWindow;
        private StatisticsWindow? _statisticsWindow;
        private ExerciseWindow? _exerciseWindow;
        private AnalyzerWindow? _analyzerWindow;
        private SmartCoachDetailWindow? _smartCoachWindow;
        private ResonanceWindow? _resonanceWindow;
        private ProgressionWindow? _progressionWindow;
        private AnalysisWindow? _analysisWindow;
        private SettingsWindow? _settingsWindow;
        private ClinicianDashboardWindow? _clinicianDashboardWindow;
        private CoachDashboardWindow? _coachDashboardWindow;
        private ReportExportWindow? _reportExportWindow;
        private ManualOverrideWindow? _manualOverrideWindow;
        private CaseReviewWindow? _caseReviewWindow;
        private bool _isClosing;

        // RC-0 graf-evidens: teller hvorfor forsidegrafen ev. slutter å tegne.
        // Ren observasjon — endrer aldri hva som rendres.
        private long _rc0GraphRendered;
        private long _rc0GraphSkipNotRecording;
        private long _rc0GraphSkipNoPitch;
        private long _rc0GraphSkipStabilizer;
        private long _rc0GraphSkipDuplicate;
        private DateTime _rc0LastGraphLogUtc = DateTime.MinValue;
        private bool _rc0GraphWasRecording;
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

            // Tilgjengelighet: ved ReducedVisualFeedback dempes SEKUNDÆRE indikatorer
            // (stabilitet) slik at de konkurrerer mindre om oppmerksomheten. Helse- og
            // mic-status forblir uendret/full prominens — Safety > Health > ... > UI.
            ApplyReducedVisualFeedback();
        }

        /// <summary>
        /// Demper sekundære indikatorer (kun stabilitet) når brukeren har slått på
        /// ReducedVisualFeedback. Helse-/mic-status røres ALDRI. Resolves null-safe via
        /// App.Services (kan mangle i design/test-kontekst) og kaster aldri.
        /// </summary>
        private void ApplyReducedVisualFeedback()
        {
            StressSensitiveExperience? stressSensitive = null;
            try { stressSensitive = App.Services?.GetService(typeof(StressSensitiveExperience)) as StressSensitiveExperience; }
            catch { stressSensitive = null; }

            if (stressSensitive?.IsReducedVisual != true)
                return;

            // Forenkling, ikke fjerning: stabilitets-badgen beholder sin informasjon,
            // men nedtones (lavere opasitet) så den ikke konkurrerer med helse-/mic-status.
            if (StabilityBorder != null)
                StabilityBorder.Opacity = 0.55;
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
            var comfortZoneColor = GetOxyColor("ChartTargetAreaBrush", OxyColors.Green);
            _comfortZoneAnnotation = new RectangleAnnotation
            {
                MinimumY = 165,
                MaximumY = 255,
                Fill = OxyColor.FromAColor(40, comfortZoneColor),
                Stroke = OxyColor.FromAColor(100, comfortZoneColor),
                StrokeThickness = 1,
                Layer = AnnotationLayer.BelowSeries
            };
            model.Annotations.Add(_comfortZoneAnnotation);
            
            // Pitch series
            _pitchSeries = new LineSeries
            {
                Title = Loc.Get("Main_YourPitch"),
                Color = GetOxyColor("ChartPitchBrush", OxyColors.OrangeRed),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = GetOxyColor("ChartPitchBrush", OxyColors.OrangeRed),
                MarkerStroke = GetOxyColor("ChartPitchBrush", OxyColors.OrangeRed),
                MarkerStrokeThickness = 1
            };
            model.Series.Add(_pitchSeries);

            ApplyPlotTheme(model);
            
            return model;
        }

        private void ApplyPlotTheme(PlotModel model)
        {
            var text = GetOxyColor("TextPrimaryBrush", OxyColors.Black);
            var secondaryText = GetOxyColor("TextSecondaryBrush", OxyColors.DimGray);
            var background = GetOxyColor("ChartBackgroundBrush", OxyColors.White);
            var grid = GetOxyColor("ChartGridBrush", OxyColors.LightGray);
            var border = GetOxyColor("BorderPrimaryBrush", OxyColors.Gray);

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
                        SetStatusBackground(StabilityBorder, "SuccessBrush");
                        StabilityText.Text = Loc.Get("Stability_VeryStable");
                        break;
                    case StabilityState.Stable:
                        SetStatusBackground(StabilityBorder, "SuccessBrush");
                        StabilityText.Text = Loc.Get("Stability_Stable");
                        break;
                    case StabilityState.Developing:
                        SetStatusBackground(StabilityBorder, "WarningBrush");
                        StabilityText.Text = Loc.Get("Stability_Developing");
                        break;
                    case StabilityState.Unstable:
                        SetStatusBackground(StabilityBorder, "WarningBrush");
                        StabilityText.Text = Loc.Get("Stability_Unstable");
                        break;
                    default:
                        SetStatusBackground(StabilityBorder, "BackgroundTertiaryBrush");
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
                        SetStatusBackground(HealthBorder, "SuccessBrush");
                        HealthText.Text = Loc.Get("Health_Safe");
                        break;
                    case HealthState.Monitor:
                        SetStatusBackground(HealthBorder, "WarningBrush");
                        HealthText.Text = Loc.Get("Health_Monitor");
                        break;
                    case HealthState.Warning:
                        SetStatusBackground(HealthBorder, "WarningBrush");
                        HealthText.Text = Loc.Get("Health_Warning");
                        break;
                    case HealthState.Danger:
                        SetStatusBackground(HealthBorder, softenDanger ? "WarningBrush" : "ErrorBrush");
                        HealthText.Text = Loc.Get("Health_Danger");
                        break;
                    default:
                        SetStatusBackground(HealthBorder, "BackgroundTertiaryBrush");
                        HealthText.Text = Loc.Get("Stability_NoVoice");
                        break;
                }
            });
        }

        private static void SetStatusBackground(Border border, string resourceKey)
        {
            border.SetResourceReference(Border.BackgroundProperty, resourceKey);
        }
        
        private void OnChartUpdate(object? sender, EventArgs e)
        {
            RenderLatestPitchPoint();
        }

        private void RenderLatestPitchPoint()
        {
            if (!_viewModel.IsRecording)
            {
                _rc0GraphSkipNotRecording++;
                if (_rc0GraphWasRecording)
                {
                    // Test B: «graph stop reason if stopped» — én sluttlinje med
                    // tellerstanden idet opptaket gikk fra aktivt til stoppet.
                    _rc0GraphWasRecording = false;
                    _rc0LastGraphLogUtc = DateTime.MinValue;
                    LogFrontPageGraphCounters();
                }
                return;
            }
            _rc0GraphWasRecording = true;

            var rawPitch = _viewModel.CurrentPitch > 0 ? _viewModel.CurrentPitch : _viewModel.SmoothedPitch;
            if (rawPitch <= 0)
            {
                _rc0GraphSkipNoPitch++;
                LogFrontPageGraphCounters();
                return;
            }

            var now = DateTime.Now;
            var pitch = _pitchTraceStabilizer.Filter(rawPitch, now);
            if (pitch <= 0)
            {
                _rc0GraphSkipStabilizer++;
                LogFrontPageGraphCounters();
                return;
            }

            var currentSequence = _viewModel.LivePitchUpdateSequence;
            if (currentSequence == _lastRenderedPitchSequence &&
                Math.Abs(pitch - _lastRenderedPitch) < 0.5 &&
                (now - _lastChartRenderAt).TotalMilliseconds < 100)
            {
                _rc0GraphSkipDuplicate++;
                return;
            }

            _rc0GraphRendered++;
            LogFrontPageGraphCounters();
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
                lineColor = GetOxyColor("TextTertiaryBrush", OxyColors.Gray);
            }
            else if (latestPoint.Health == HealthState.Danger || latestPoint.Health == HealthState.Warning)
            {
                lineColor = GetOxyColor("ErrorBrush", OxyColors.Red);
            }
            else if (latestPoint.InRange)
            {
                lineColor = GetOxyColor("SuccessBrush", OxyColors.Green);
            }
            else
            {
                lineColor = GetOxyColor("WarningBrush", OxyColors.Orange);
            }
            _pitchSeries.Color = lineColor;
            _pitchSeries.MarkerFill = lineColor;
            _pitchSeries.MarkerStroke = lineColor;

            UpdateLiveXAxis(xPos);
            UpdateLiveYAxis(xPos);
            _pitchPlotModel.InvalidatePlot(false);
        }

        /// <summary>
        /// 1 Hz tellerlinje for forsidegrafen (RC-0): viser om grafstopp skyldes
        /// manglende pitch, stabilizer-avvisning eller at opptaket er stoppet.
        /// </summary>
        private void LogFrontPageGraphCounters()
        {
            if (!DebugSettingsService.Instance.EnableRc0Diagnostics)
                return;

            var now = DateTime.UtcNow;
            if ((now - _rc0LastGraphLogUtc).TotalSeconds < 1)
                return;

            _rc0LastGraphLogUtc = now;
            Rc0RuntimeLog.Write("FrontPageGraph",
                $"Rendered={_rc0GraphRendered}; SkipNotRecording={_rc0GraphSkipNotRecording}; " +
                $"SkipNoPitch={_rc0GraphSkipNoPitch}; SkipStabilizerRejected={_rc0GraphSkipStabilizer}; " +
                $"SkipDuplicate={_rc0GraphSkipDuplicate}");
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
            _isClosing = true;
            CloseModelessChildWindows();
            _chartUpdateTimer.Stop();
            _viewModel.Dispose();
        }

        private T ShowOrActivateModelessWindow<T>(
            T? current,
            Action<T?> setCurrent,
            Func<T> createWindow,
            Action? onClosed = null) where T : Window
        {
            if (current is { IsVisible: true })
            {
                RestoreAndFocus(current);
                return current;
            }

            var window = createWindow();
            window.Owner = this;
            setCurrent(window);
            window.Closed += (_, _) =>
            {
                setCurrent(null);
                if (!_isClosing)
                    onClosed?.Invoke();
            };

            window.Show();
            RestoreAndFocus(window);
            return window;
        }

        private static void RestoreAndFocus(Window window)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
            window.Focus();
        }

        private void CloseModelessChildWindows()
        {
            var windows = new Window?[]
            {
                _calendarWindow,
                _statisticsWindow,
                _exerciseWindow,
                _analyzerWindow,
                _smartCoachWindow,
                _resonanceWindow,
                _progressionWindow,
                _analysisWindow,
                _settingsWindow,
                _clinicianDashboardWindow,
                _coachDashboardWindow,
                _reportExportWindow,
                _manualOverrideWindow,
                _caseReviewWindow
            };

            foreach (var window in windows)
            {
                if (window is not null && window.IsLoaded)
                    window.Close();
            }
        }
        
        private void OnOpenCalendar(object sender, RoutedEventArgs e)
        {
            ShowOrActivateModelessWindow(_calendarWindow, window => _calendarWindow = window, () => new CalendarWindow());
        }
        
        private void OnOpenStatistics(object sender, RoutedEventArgs e)
        {
            ShowOrActivateModelessWindow(_statisticsWindow, window => _statisticsWindow = window, () => new StatisticsWindow());
        }
        
        private void OnOpenExerciseGuide(object sender, RoutedEventArgs e)
        {
            ShowOrActivateModelessWindow(_exerciseWindow, window => _exerciseWindow = window, () => new ExerciseWindow());
        }
        
        private void OnOpenAnalyzer(object sender, RoutedEventArgs e)
        {
            ShowOrActivateModelessWindow(_analyzerWindow, window => _analyzerWindow = window, () => new AnalyzerWindow());
        }
        
        private void OnOpenSmartCoach(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowOrActivateModelessWindow(_smartCoachWindow, window => _smartCoachWindow = window, () => new SmartCoachDetailWindow());
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("Navigation", $"OpenSmartCoach FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnOpenResonance(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowOrActivateModelessWindow(_resonanceWindow, window => _resonanceWindow = window, () => new ResonanceWindow());
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("Navigation", $"OpenResonance FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnOpenProgression(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowOrActivateModelessWindow(_progressionWindow, window => _progressionWindow = window, () => new ProgressionWindow());
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("Navigation", $"OpenProgression FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnOpenAnalysis(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowOrActivateModelessWindow(_analysisWindow, window => _analysisWindow = window, () => new AnalysisWindow());
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("Navigation", $"OpenAnalysis FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            ShowOrActivateModelessWindow(
                _settingsWindow,
                window => _settingsWindow = window,
                () => new SettingsWindow(),
                RefreshSettingsDependentState);
        }
        
        private void OnOpenClinicianDashboard(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowOrActivateModelessWindow(_clinicianDashboardWindow, window => _clinicianDashboardWindow = window, () => new ClinicianDashboardWindow());
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("Navigation", $"OpenClinicianDashboard FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenCoachDashboard(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowOrActivateModelessWindow(_coachDashboardWindow, window => _coachDashboardWindow = window, () => new CoachDashboardWindow());
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("Navigation", $"OpenCoachDashboard FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenReportExport(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowOrActivateModelessWindow(_reportExportWindow, window => _reportExportWindow = window, () => new ReportExportWindow());
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("Navigation", $"OpenReportExport FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenManualOverride(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowOrActivateModelessWindow(_manualOverrideWindow, window => _manualOverrideWindow = window, () => new ManualOverrideWindow());
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("Navigation", $"OpenManualOverride FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenCaseReview(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowOrActivateModelessWindow(_caseReviewWindow, window => _caseReviewWindow = window, () => new CaseReviewWindow());
            }
            catch (Exception ex)
            {
                Rc0RuntimeLog.Write("Navigation", $"OpenCaseReview FAILED; {ex.GetType().Name}: {ex.Message}");
                MessageBox.Show(SafeFailureMessages.For(SafeFailureKind.General),
                    Loc.Get("UI_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshSettingsDependentState()
        {
            // Last inn progresjon på nytt etter reset.
            _viewModel.LoadUserSettings();

            // Re-les brukerprofilen (stilmål + komfortsone) og re-applier forsidens
            // pitch-målsone. Settings kan ha endret PreferredVoiceStyle/komfortsone —
            // uten dette ser MainViewModel ikke endringene før restart.
            _viewModel.ReloadUserVoiceProfile();
            UpdatePitchTargetZone();
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
