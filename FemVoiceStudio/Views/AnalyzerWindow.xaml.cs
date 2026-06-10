using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;
using FemVoiceStudio.Converters;
using FemVoiceStudio.Data;
using FemVoiceStudio.Services;
using NAudio.Wave;

namespace FemVoiceStudio.Views
{
    public partial class AnalyzerWindow : Window
    {
        private AudioCaptureService? _audioCapture;
        private readonly DispatcherTimer _renderTimer;
        private readonly int _sampleRate = 44100;
        
        private const int SpectrogramWidth = 800;
        private const int SpectrogramHeight = 400;
        private const int FftSize = 2048;
        private const double MinSpectrogramFrequency = 80;
        private const double MaxSpectrogramFrequency = 4000;
        private const double MinPitchFrequency = 80;
        private const double MaxPitchFrequency = 500;
        
        private double _targetFrequency = 180;
        
        private bool _isRecording;
        private DateTime _recordingStartTime;
        private List<double> _recordedFrequencies = new();
        private WaveFileWriter? _waveWriter;
        private readonly ResonanceProxyEngine _resonanceEngine;
        private readonly SpectrogramResonanceMapper _resonanceMapper = new();
        private SpectrogramResonanceVisualState? _latestResonanceVisualState;
        private double _latestResonanceScore;
        private double _latestMainFrequency;
        
        private readonly SolidColorBrush _targetBrush = new(Colors.Yellow);
        private readonly SolidColorBrush _mainFreqBrush = new(Colors.White);
        private readonly SolidColorBrush _forwardZoneBrush = new(Color.FromArgb(40, 71, 209, 168));
        private readonly SolidColorBrush _f1Brush = new(Color.FromRgb(99, 179, 237));
        private readonly SolidColorBrush _f2Brush = new(Color.FromRgb(72, 187, 120));
        private readonly SolidColorBrush _f3Brush = new(Color.FromRgb(246, 173, 85));
        
        public AnalyzerWindow()
        {
            InitializeComponent();
            _resonanceEngine = new ResonanceProxyEngine(sampleRate: _sampleRate);
            _resonanceEngine.ResonanceScoreUpdated += score => _latestResonanceScore = Math.Clamp(score, 0, 1);
            _resonanceEngine.FormantsUpdated += OnFormantsUpdated;
            
            _renderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _renderTimer.Tick += RenderTimer_Tick;
            
            InitializeSpectrogram();
            InitializeNoteButtons();
            Loaded += AnalyzerWindow_Loaded;
            Closing += AnalyzerWindow_Closing;
        }
        
        private void AnalyzerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeAudio();
        }
        
        private void AnalyzerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopRecording();
            _renderTimer.Stop();
            _resonanceEngine.Stop();
            _resonanceEngine.Dispose();
            _audioCapture?.Dispose();
            _waveWriter?.Dispose();
        }
        
        private void InitializeAudio()
        {
            try
            {
                _audioCapture = new AudioCaptureService(_sampleRate);
                _audioCapture.Initialize();
                _audioCapture.HearOwnVoice = GetHearOwnVoiceSetting();
                _audioCapture.AudioDataAvailable += AudioCapture_AudioDataAvailable;
                // Stil-bevisst resonansscoring: en DarkFeminine-/Androgyn-bruker skal ikke
                // måles mot den universelle lyse feminine klangen (A.4 Goal Safety). Setter
                // brukerens PreferredVoiceStyle før Start() — null-safe Feminine-default.
                _resonanceEngine.SetVoiceStyle(GetPreferredVoiceStyle());
                // Configure resonance RMS threshold from calibration if available
                try
                {
                    var profile = _audioCapture.CalibrationProfile;
                    if (profile != null)
                        _resonanceEngine.ConfigureAdaptiveRmsThreshold(profile.CalibrationNoiseFloorRms, profile.CalibrationSpeechMedianRms);
                    else
                    {
                        var snap = _audioCapture.DiagnosticsSnapshot;
                        _resonanceEngine.ConfigureAdaptiveRmsThreshold(snap.NoiseFloorEstimate, snap.RmsLevel);
                    }
                }
                catch { }
                _resonanceEngine.Start();
                _audioCapture.StartRecording();
                if (!_audioCapture.IsRecording)
                    throw new InvalidOperationException(Loc.Get("UI_MicNotReady"));

                _renderTimer.Start();
            }
            catch (Exception ex)
            {
                _renderTimer.Stop();
                _resonanceEngine.Stop();
                MessageBox.Show(string.Format(Loc.Get("Audio_MicrophoneStartFailedFormat"), ex.Message), Loc.Get("UI_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void InitializeSpectrogram()
        {
            SpectrogramCanvas.Width = SpectrogramWidth;
            SpectrogramCanvas.Height = SpectrogramHeight;
            DrawFrequencyBackground();
        }

        private static bool GetHearOwnVoiceSetting()
        {
            try
            {
                // DatabaseService er DI-singleton; manuelle new re-kjørte skjema-init (integrasjonsaudit-funn).
                var db = App.Services?.GetService(typeof(DatabaseService)) as DatabaseService
                         ?? new DatabaseService();
                return db.GetUserSettings().HearOwnVoice;
            }
            catch
            {
                return false;
            }
        }

        private static VoiceStyleGoal GetPreferredVoiceStyle()
        {
            try
            {
                var db = App.Services?.GetService(typeof(DatabaseService)) as DatabaseService;
                return db?.GetUserVoiceProfile(1)?.PreferredVoiceStyle ?? VoiceStyleGoal.Feminine;
            }
            catch
            {
                return VoiceStyleGoal.Feminine;
            }
        }
        
        private void DrawFrequencyBackground()
        {
            double minFreq = MinSpectrogramFrequency;
            double maxFreq = MaxSpectrogramFrequency;
            
            for (int y = 0; y < SpectrogramHeight; y++)
            {
                double freq = maxFreq - (y * (maxFreq - minFreq) / SpectrogramHeight);
                SolidColorBrush brush = GetSpectrogramBackgroundColor(freq);
                
                var line = new Line
                {
                    X1 = 0,
                    X2 = SpectrogramWidth,
                    Y1 = y,
                    Y2 = y,
                    Stroke = brush,
                    StrokeThickness = 1
                };
                SpectrogramCanvas.Children.Add(line);
            }
        }
        
        private SolidColorBrush GetFrequencyColor(double frequency)
        {
            if (frequency < 85)
                return new SolidColorBrush(Color.FromRgb(128, 64, 64));
            else if (frequency <= 155)
                return new SolidColorBrush(Color.FromRgb(64, 64, 255));
            else if (frequency < 165)
                return new SolidColorBrush(Color.FromRgb(64, 128, 64));
            else if (frequency <= 320)
                return new SolidColorBrush(Color.FromRgb(255, 64, 64));
            else
                return new SolidColorBrush(Color.FromRgb(255, 128, 128));
        }

        private SolidColorBrush GetSpectrogramBackgroundColor(double frequency)
        {
            if (frequency >= 1800 && frequency <= 3200)
                return new SolidColorBrush(Color.FromRgb(9, 28, 31));

            if (frequency > 3200)
                return new SolidColorBrush(Color.FromRgb(26, 18, 18));

            return new SolidColorBrush(Color.FromRgb(8, 12, 22));
        }
        
        private void InitializeNoteButtons()
        {
            NotesPanel.Children.Clear();
            
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            
            for (int octave = 2; octave <= 4; octave++)
            {
                var octavePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                
                var octaveLabel = new TextBlock
                {
                    Text = string.Format(Loc.Get("Analyzer_OctaveFormat"), octave),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    FontWeight = FontWeights.Bold
                };
                octavePanel.Children.Add(octaveLabel);
                
                for (int noteIndex = 0; noteIndex < noteNames.Length; noteIndex++)
                {
                    double frequency = NoteToFrequency(noteIndex - 9, octave);
                    
                    var button = new Button
                    {
                        Content = noteNames[noteIndex],
                        Width = 40,
                        Height = 30,
                        Margin = new Thickness(2),
                        Background = GetFrequencyColor(frequency),
                        Foreground = Brushes.White,
                        Tag = frequency,
                        ToolTip = $"{frequency:F1} Hz"
                    };
                    button.Click += NoteButton_Click;
                    octavePanel.Children.Add(button);
                }
                
                NotesPanel.Children.Add(octavePanel);
            }
            
            TargetFrequencyText.Text = $"{_targetFrequency:F0} Hz";
            UpdateTargetLine();
        }
        
        private double NoteToFrequency(int noteIndex, int octave)
        {
            double a4 = 440.0;
            return a4 * Math.Pow(2, (noteIndex + octave - 4) / 12.0);
        }
        
        private void NoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is double frequency)
            {
                _targetFrequency = frequency;
                TargetFrequencyText.Text = $"{frequency:F0} Hz";
                UpdateTargetLine();
            }
        }
        
        private void UpdateTargetLine()
        {
            var linesToRemove = SpectrogramCanvas.Children.OfType<Line>()
                .Where(l => l.Tag?.ToString() == "target").ToList();
            foreach (var line in linesToRemove)
            {
                SpectrogramCanvas.Children.Remove(line);
            }
            
            double minFreq = MinSpectrogramFrequency;
            double maxFreq = MaxSpectrogramFrequency;
            int y = (int)((maxFreq - _targetFrequency) * SpectrogramHeight / (maxFreq - minFreq));
            y = Math.Clamp(y, 0, SpectrogramHeight - 1);
            
            var targetLine = new Line
            {
                X1 = 0,
                X2 = SpectrogramWidth,
                Y1 = y,
                Y2 = y,
                Stroke = _targetBrush,
                StrokeThickness = 2,
                Tag = "target",
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            SpectrogramCanvas.Children.Add(targetLine);
        }
        
        private double[] _fftBuffer = new double[FftSize];
        private int _fftIndex = 0;
        
        private void AudioCapture_AudioDataAvailable(object? sender, float[] samples)
        {
            _resonanceEngine.ProcessSamples(samples);

            for (int i = 0; i < samples.Length && _fftIndex < FftSize; i++)
            {
                _fftBuffer[_fftIndex++] = samples[i];
            }
            
            if (_isRecording && _waveWriter != null)
            {
                foreach (var sample in samples)
                {
                    _waveWriter.WriteSample(sample);
                }
            }
        }

        private void OnFormantsUpdated(FormantSnapshot snapshot)
        {
            _latestResonanceVisualState = _resonanceMapper.Map(
                snapshot,
                _latestResonanceScore,
                MinSpectrogramFrequency,
                MaxSpectrogramFrequency,
                SpectrogramHeight);
        }
        
        private void RenderTimer_Tick(object? sender, EventArgs e)
        {
            if (_fftIndex >= FftSize)
            {
                RenderSpectrogram();
                _fftIndex = 0;
            }
        }
        
        private void RenderSpectrogram()
        {
            var fftResult = PerformFFT(_fftBuffer);
            
            int maxIndex = 0;
            double maxValue = 0;
            double binSize = (double)_sampleRate / FftSize;
            
            for (int i = 0; i < fftResult.Length / 2; i++)
            {
                double freq = i * binSize;
                if (freq < MinPitchFrequency || freq > MaxPitchFrequency) continue;
                
                if (fftResult[i] > maxValue)
                {
                    maxValue = fftResult[i];
                    maxIndex = i;
                }
            }
            
            double mainFrequency = maxIndex * binSize;
            _latestMainFrequency = mainFrequency;
            
            Dispatcher.Invoke(() =>
            {
                MainFrequencyText.Text = $"{mainFrequency:F0} Hz";
                
                if (_isRecording && mainFrequency > 0)
                {
                    _recordedFrequencies.Add(mainFrequency);
                    
                    // Log analyzer data if debug is enabled
                    if (DebugSettingsService.Instance.EnableAnalyzerDebug)
                    {
                        string rangeResult = mainFrequency < 165
                            ? Loc.Get("Analyzer_RangeLow")
                            : (mainFrequency < 320 ? Loc.Get("Analyzer_RangeUpper") : Loc.Get("Analyzer_RangeVeryHigh"));
                        DebugSettingsService.Instance.LogAnalyzerData("Frequency", mainFrequency, rangeResult);
                        AnalyzerDebugText.Text = string.Format(Loc.Get("Analyzer_DebugFrequencyFormat"), mainFrequency, _targetFrequency, mainFrequency - _targetFrequency);
                    }
                }
            });
            
            var childrenToRemove = SpectrogramCanvas.Children.OfType<Rectangle>().ToList();
            foreach (var child in childrenToRemove)
            {
                if (child.Tag?.ToString() == "spectrum")
                {
                    double newX = Canvas.GetLeft(child) - 2;
                    if (newX < -10)
                    {
                        SpectrogramCanvas.Children.Remove(child);
                    }
                    else
                    {
                        Canvas.SetLeft(child, newX);
                    }
                }
            }
            
            var linesToRemove = SpectrogramCanvas.Children.OfType<Line>()
                .Where(l => l.Tag?.ToString() == "mainfreq").ToList();
            foreach (var line in linesToRemove)
            {
                SpectrogramCanvas.Children.Remove(line);
            }
            
            double minFreq = MinSpectrogramFrequency;
            double maxFreq = MaxSpectrogramFrequency;
            
            for (int i = 0; i < SpectrogramHeight; i += 2)
            {
                double freq = maxFreq - (i * (maxFreq - minFreq) / SpectrogramHeight);
                int binIndex = (int)(freq / binSize);
                
                if (binIndex >= 0 && binIndex < fftResult.Length / 2)
                {
                    double magnitude = fftResult[binIndex];
                    if (magnitude > 0.01)
                    {
                        byte intensity = (byte)Math.Min(255, magnitude * 255);
                        
                        var rect = new Rectangle
                        {
                            Width = 2,
                            Height = 2,
                            Fill = new SolidColorBrush(Color.FromArgb(intensity, 255, 255, 255)),
                            Tag = "spectrum"
                        };
                        Canvas.SetLeft(rect, SpectrogramWidth - 2);
                        Canvas.SetTop(rect, i);
                        SpectrogramCanvas.Children.Add(rect);
                    }
                }
            }
            
            if (mainFrequency >= minFreq && mainFrequency <= maxFreq)
            {
                int mainY = (int)((maxFreq - mainFrequency) * SpectrogramHeight / (maxFreq - minFreq));
                mainY = Math.Clamp(mainY, 0, SpectrogramHeight - 1);
                
                var mainFreqLine = new Line
                {
                    X1 = 0,
                    X2 = SpectrogramWidth,
                    Y1 = mainY,
                    Y2 = mainY,
                    Stroke = _mainFreqBrush,
                    StrokeThickness = 2,
                    Tag = "mainfreq"
                };
                SpectrogramCanvas.Children.Add(mainFreqLine);
            }

            DrawResonanceOverlay();
        }

        private void DrawResonanceOverlay()
        {
            var overlays = SpectrogramCanvas.Children
                .OfType<FrameworkElement>()
                .Where(element => element.Tag?.ToString() == "resonance-overlay")
                .ToList();

            foreach (var overlay in overlays)
            {
                SpectrogramCanvas.Children.Remove(overlay);
            }

            var state = _latestResonanceVisualState;
            if (state == null)
                return;

            var zoneTop = Math.Min(state.ForwardZoneTopY, state.ForwardZoneBottomY);
            var zoneHeight = Math.Abs(state.ForwardZoneBottomY - state.ForwardZoneTopY);
            var zone = new Rectangle
            {
                Width = SpectrogramWidth,
                Height = Math.Max(1, zoneHeight),
                Fill = _forwardZoneBrush,
                Tag = "resonance-overlay"
            };
            Canvas.SetLeft(zone, 0);
            Canvas.SetTop(zone, zoneTop);
            SpectrogramCanvas.Children.Add(zone);

            foreach (var marker in state.Formants)
            {
                var brush = marker.Name switch
                {
                    "F1" => _f1Brush,
                    "F2" => _f2Brush,
                    "F3" => _f3Brush,
                    _ => Brushes.White
                };

                var line = new Line
                {
                    X1 = 0,
                    X2 = SpectrogramWidth,
                    Y1 = marker.Y,
                    Y2 = marker.Y,
                    Stroke = brush,
                    StrokeThickness = marker.Name == "F2" ? 2.5 : 1.5,
                    Tag = "resonance-overlay",
                    StrokeDashArray = marker.Name == "F2" ? null : new DoubleCollection { 3, 3 }
                };
                SpectrogramCanvas.Children.Add(line);

                var label = new TextBlock
                {
                    Text = $"{marker.Name} {marker.FrequencyHz:F0} Hz",
                    Foreground = brush,
                    Background = Brushes.Black,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Tag = "resonance-overlay"
                };
                Canvas.SetLeft(label, 8);
                Canvas.SetTop(label, Math.Clamp(marker.Y - 10, 0, SpectrogramHeight - 20));
                SpectrogramCanvas.Children.Add(label);
            }

            ResonanceScoreText.Text = $"{state.ResonanceScore * 100:F0}%";
            ResonanceFocusText.Text = state.Tone switch
            {
                SpectrogramResonanceTone.Forward => Loc.Get("Analyzer_ResonanceForward"),
                SpectrogramResonanceTone.Balanced => Loc.Get("Analyzer_ResonanceBalanced"),
                SpectrogramResonanceTone.Back => Loc.Get("Analyzer_ResonanceBack"),
                SpectrogramResonanceTone.Pressed => Loc.Get("Analyzer_ResonancePressed"),
                _ => "--"
            };
            UpdateClinicalScore(state);
        }

        private void UpdateClinicalScore(SpectrogramResonanceVisualState state)
        {
            var pitchScore = CalculatePitchTraceScore(_latestMainFrequency, _targetFrequency);
            var resonanceScore = Math.Clamp(state.ResonanceScore, 0, 1);
            var pressurePenalty = state.Tone == SpectrogramResonanceTone.Pressed ? 0.25 : 0.0;
            var toneBonus = state.Tone == SpectrogramResonanceTone.Forward
                ? 0.10
                : state.Tone == SpectrogramResonanceTone.Balanced ? 0.05 : 0.0;

            var score = Math.Clamp((resonanceScore * 0.65) + (pitchScore * 0.25) + toneBonus - pressurePenalty, 0, 1);
            ClinicalScoreText.Text = $"{score * 100:F0}%";
            ClinicalScoreText.Foreground = score >= 0.75
                ? (Brush)FindResource("SuccessBrush")
                : score >= 0.50
                    ? (Brush)FindResource("WarningBrush")
                    : (Brush)FindResource("InfoBrush");

            ClinicalScoreStatusText.Text = state.Tone switch
            {
                SpectrogramResonanceTone.Pressed => Loc.Get("Analyzer_ClinicalPressed"),
                SpectrogramResonanceTone.Forward when score >= 0.75 => Loc.Get("Analyzer_ClinicalForwardStrong"),
                SpectrogramResonanceTone.Forward => Loc.Get("Analyzer_ClinicalForward"),
                SpectrogramResonanceTone.Back => Loc.Get("Analyzer_ClinicalBack"),
                _ => Loc.Get("Analyzer_ClinicalBalanced")
            };
        }

        private static double CalculatePitchTraceScore(double currentFrequency, double targetFrequency)
        {
            if (currentFrequency <= 0 || targetFrequency <= 0)
                return 0;

            var relativeError = Math.Abs(currentFrequency - targetFrequency) / targetFrequency;
            return Math.Clamp(1.0 - relativeError * 3.0, 0, 1);
        }
        
        private double[] PerformFFT(double[] data)
        {
            int n = data.Length;
            var real = new double[n];
            
            for (int i = 0; i < n; i++)
            {
                double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
                real[i] = data[i] * window;
            }
            
            var result = new double[n];
            
            for (int k = 0; k < n / 2; k++)
            {
                double sumReal = 0;
                double sumImag = 0;
                
                for (int t = 0; t < n; t++)
                {
                    double angle = 2 * Math.PI * t * k / n;
                    sumReal += real[t] * Math.Cos(angle);
                    sumImag += real[t] * Math.Sin(angle);
                }
                
                result[k] = Math.Sqrt(sumReal * sumReal + sumImag * sumImag);
            }
            
            return result;
        }
        
        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }
        
        private void StartRecording()
        {
            try
            {
                // Reload debug settings
                DebugSettingsService.Instance.Reload();
                
                string recordingsFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "FemVoiceStudio", "Recordings");
                
                System.IO.Directory.CreateDirectory(recordingsFolder);
                
                string filePath = System.IO.Path.Combine(
                    recordingsFolder,
                    $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                
                _waveWriter = new WaveFileWriter(filePath, 
                    new WaveFormat(_sampleRate, 16, 1));
                
                _recordedFrequencies.Clear();
                _recordingStartTime = DateTime.Now;
                _isRecording = true;
                
                // Show debug panel if enabled
                if (DebugSettingsService.Instance.EnableAnalyzerDebug)
                {
                    AnalyzerDebugPanel.Visibility = Visibility.Visible;
                }
                
                RecordButton.Content = Loc.Get("Analyzer_StopRecording");
                RecordButton.Background = Brushes.Red;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Recording_StartFailedFormat"), ex.Message), Loc.Get("UI_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void StopRecording()
        {
            if (!_isRecording) return;
            
            _isRecording = false;
            _waveWriter?.Dispose();
            _waveWriter = null;
            
            // Close debug logs
            DebugSettingsService.Instance.CloseLogs();
            AnalyzerDebugPanel.Visibility = Visibility.Collapsed;
            
            RecordButton.Content = Loc.Get("Analyzer_StartRecording");
            RecordButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            
            if (_recordedFrequencies.Count > 0)
            {
                var stats = CalculateRecordingStats();
                DisplayRecordingStats(stats);
            }
            
            string recordingsFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FemVoiceStudio", "Recordings");
                
            MessageBox.Show(string.Format(Loc.Get("Recording_SavedInFormat"), Environment.NewLine, recordingsFolder), Loc.Get("Recording_Completed"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private RecordingStats CalculateRecordingStats()
        {
            var validFreqs = _recordedFrequencies.Where(f => f > 0).ToList();
            
            var stats = new RecordingStats
            {
                AverageFrequency = validFreqs.Average(),
                MinFrequency = validFreqs.Min(),
                MaxFrequency = validFreqs.Max(),
                TotalDuration = (DateTime.Now - _recordingStartTime).TotalSeconds,
                SampleCount = validFreqs.Count
            };
            
            var sorted = validFreqs.OrderBy(f => f).ToList();
            stats.Quantile5 = GetQuantile(sorted, 0.05);
            stats.Quantile10 = GetQuantile(sorted, 0.10);
            stats.Quantile20 = GetQuantile(sorted, 0.20);
            stats.Quantile50 = GetQuantile(sorted, 0.50);
            stats.Quantile80 = GetQuantile(sorted, 0.80);
            stats.Quantile90 = GetQuantile(sorted, 0.90);
            stats.Quantile95 = GetQuantile(sorted, 0.95);
            
            stats.InfraMascShare = (double)validFreqs.Count(f => f < 85) / validFreqs.Count * 100;
            stats.MascShare = (double)validFreqs.Count(f => f >= 85 && f <= 155) / validFreqs.Count * 100;
            stats.EnbyShare = (double)validFreqs.Count(f => f > 155 && f < 165) / validFreqs.Count * 100;
            stats.FemShare = (double)validFreqs.Count(f => f >= 165 && f <= 320) / validFreqs.Count * 100;
            stats.UltraFemShare = (double)validFreqs.Count(f => f > 320) / validFreqs.Count * 100;
            
            return stats;
        }
        
        private double GetQuantile(List<double> sorted, double quantile)
        {
            if (sorted.Count == 0) return 0;
            int index = (int)(quantile * sorted.Count);
            return sorted[Math.Min(index, sorted.Count - 1)];
        }
        
        private void DisplayRecordingStats(RecordingStats stats)
        {
            StatsPanel.Visibility = Visibility.Visible;
            
            AveragePitchText.Text = $"{stats.AverageFrequency:F0} Hz";
            MinPitchText.Text = $"{stats.MinFrequency:F0} Hz";
            MaxPitchText.Text = $"{stats.MaxFrequency:F0} Hz";
            DurationText.Text = $"{stats.TotalDuration:F1}s";
            
            Quantile5Text.Text = $"{stats.Quantile5:F0}";
            Quantile10Text.Text = $"{stats.Quantile10:F0}";
            Quantile20Text.Text = $"{stats.Quantile20:F0}";
            Quantile50Text.Text = $"{stats.Quantile50:F0}";
            Quantile80Text.Text = $"{stats.Quantile80:F0}";
            Quantile90Text.Text = $"{stats.Quantile90:F0}";
            Quantile95Text.Text = $"{stats.Quantile95:F0}";
            
            InfraMascText.Text = $"{stats.InfraMascShare:F1}%";
            MascText.Text = $"{stats.MascShare:F1}%";
            EnbyText.Text = $"{stats.EnbyShare:F1}%";
            FemText.Text = $"{stats.FemShare:F1}%";
            UltraFemText.Text = $"{stats.UltraFemShare:F1}%";
            
            UpdateGenderBars(stats);
        }
        
        private void UpdateGenderBars(RecordingStats stats)
        {
            InfraMascBar.Width = stats.InfraMascShare * 2;
            MascBar.Width = stats.MascShare * 2;
            EnbyBar.Width = stats.EnbyShare * 2;
            FemBar.Width = stats.FemShare * 2;
            UltraFemBar.Width = stats.UltraFemShare * 2;
        }
    }
    
    public class RecordingStats
    {
        public double AverageFrequency { get; set; }
        public double MinFrequency { get; set; }
        public double MaxFrequency { get; set; }
        public double TotalDuration { get; set; }
        public int SampleCount { get; set; }
        
        public double Quantile5 { get; set; }
        public double Quantile10 { get; set; }
        public double Quantile20 { get; set; }
        public double Quantile50 { get; set; }
        public double Quantile80 { get; set; }
        public double Quantile90 { get; set; }
        public double Quantile95 { get; set; }
        
        public double InfraMascShare { get; set; }
        public double MascShare { get; set; }
        public double EnbyShare { get; set; }
        public double FemShare { get; set; }
        public double UltraFemShare { get; set; }
    }
}
