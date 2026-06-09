using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Converters;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Window for resonance analysis with real-time visualization.
    /// </summary>
    public partial class ResonanceWindow : Window
    {
        #region Services
        
        private AudioCaptureService? _audioCapture;
        private FormantDetectionService? _formantDetector;
        private ResonansScoringService? _resonanceScorer;
        
        #endregion
        
        #region ViewModel
        
        private ResonanceChartViewModel? _chartViewModel;
        
        #endregion
        
        #region State
        
        private bool _isRecording;
        private DateTime _sessionStartTime;
        private readonly List<FormantAnalysisResult> _formantHistory = new();
        private readonly List<ResonansScore> _resonanceHistory = new();
        
        #endregion
        
        #region Constructor
        
        public ResonanceWindow()
        {
            InitializeComponent();
            InitializeServices();
            InitializeCharts();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeServices()
        {
            try
            {
                // Initialize audio capture
                _audioCapture = new AudioCaptureService(44100, 1, 16);
                _audioCapture.AudioDataAvailable += OnAudioDataAvailable;
                _audioCapture.ErrorOccurred += OnAudioError;   // mikrofonfeil var tidligere helt stille her
                
                // Initialize formant detector
                _formantDetector = new FormantDetectionService(44100, 25, 10, 12);
                
                // Initialize resonance scorer
                _resonanceScorer = new ResonansScoringService(30, true, 0.7);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Error_InitializationFormat"), ex.Message), Loc.Get("UI_Error"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void InitializeCharts()
        {
            _chartViewModel = new ResonanceChartViewModel();
            
            // Bind PlotModels to Views with null check
            if (_chartViewModel?.F1F2ScatterModel != null)
            {
                F1F2PlotView.Model = _chartViewModel.F1F2ScatterModel;
            }
            
            if (_chartViewModel?.FormantTimelineModel != null)
            {
                TimelinePlotView.Model = _chartViewModel.FormantTimelineModel;
            }
        }
        
        #endregion

        // DatabaseService er DI-singleton; manuelle new re-kjørte skjema-init (integrasjonsaudit-funn).
        private static DatabaseService ResolveDatabase() =>
            App.Services?.GetService(typeof(DatabaseService)) as DatabaseService
            ?? new DatabaseService();

        private static bool GetHearOwnVoiceSetting()
        {
            try
            {
                return ResolveDatabase().GetUserSettings().HearOwnVoice;
            }
            catch
            {
                return false;
            }
        }
        
        #region Audio Processing
        
        private void OnAudioDataAvailable(object? sender, float[] audioData)
        {
            if (!_isRecording || _formantDetector == null) return;
            
            try
            {
                // Detect formants
                var formantResult = _formantDetector.ExtractFormants(audioData);
                
                if (formantResult.IsValid)
                {
                    // Score resonance
                    var resonanceScore = _resonanceScorer?.EvaluateResonance(formantResult);
                    
                    if (resonanceScore != null && resonanceScore.IsValid)
                    {
                        // Store history
                        _formantHistory.Add(formantResult);
                        _resonanceHistory.Add(resonanceScore);
                        
                        // Update UI on main thread
                        Dispatcher.Invoke(() =>
                        {
                            UpdateCharts(formantResult, resonanceScore);
                            UpdateDisplayValues(formantResult, resonanceScore);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audio processing error: {ex.Message}");
            }
        }
        
        private void UpdateCharts(FormantAnalysisResult formantResult, ResonansScore resonanceScore)
        {
            _chartViewModel?.AddFormantPoint(
                formantResult.SmoothedF1,
                formantResult.SmoothedF2,
                formantResult.SmoothedF3,
                resonanceScore.TotalScore
            );
        }
        
        private void UpdateDisplayValues(FormantAnalysisResult formantResult, ResonansScore resonanceScore)
        {
            // Update frequency displays
            ResonanceScoreText.Text = $"{resonanceScore.TotalScore:F0}";
            F1ValueText.Text = formantResult.SmoothedF1 > 0 ? $"{formantResult.SmoothedF1:F0}" : "--";
            F2ValueText.Text = formantResult.SmoothedF2 > 0 ? $"{formantResult.SmoothedF2:F0}" : "--";
            F3ValueText.Text = formantResult.SmoothedF3 > 0 ? $"{formantResult.SmoothedF3:F0}" : "--";
            
            // Update category
            string categoryText = resonanceScore.Category switch
            {
                Audio.AudioResonanceCategory.ForwardResonant => Loc.Get("ResonanceWindow_CategoryForward"),
                Audio.AudioResonanceCategory.NeutralResonant => Loc.Get("ResonanceWindow_CategoryNeutral"),
                Audio.AudioResonanceCategory.BackResonant => Loc.Get("ResonanceWindow_CategoryBack"),
                _ => "--"
            };
            CategoryText.Text = categoryText;
            
            // Update colors based on score with null-safe resource lookup.
            // VIKTIG: TryFindResource, ikke FindResource — FindResource KASTER når
            // nøkkelen mangler. Tidligere sto det FindResource("DangerBrush") her;
            // DangerBrush finnes ikke i temaene (de heter ErrorBrush), så HVER gyldige
            // lydframe kastet exception som ble svelget i OnAudioDataAvailable —
            // dermed oppdaterte UI-et seg aldri («Start analyse gjør ingenting»).
            var successBrush = TryFindResource("SuccessBrush") as System.Windows.Media.Brush;
            var warningBrush = TryFindResource("WarningBrush") as System.Windows.Media.Brush;
            var dangerBrush = TryFindResource("ErrorBrush") as System.Windows.Media.Brush;
            
            if (resonanceScore.TotalScore >= 70)
            {
                ResonanceScoreText.Foreground = successBrush ?? System.Windows.Media.Brushes.Green;
            }
            else if (resonanceScore.TotalScore >= 50)
            {
                ResonanceScoreText.Foreground = warningBrush ?? System.Windows.Media.Brushes.Orange;
            }
            else
            {
                ResonanceScoreText.Foreground = dangerBrush ?? System.Windows.Media.Brushes.Red;
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Start recording
                _audioCapture?.Initialize();
                if (_audioCapture != null)
                    _audioCapture.HearOwnVoice = GetHearOwnVoiceSetting();

                // Juster formant-RMS-terskelen mot mikrofonkalibreringen (samme mønster
                // som ExerciseWindow gjør for ResonanceProxyEngine) — default 0.01 er
                // for strengt for mikrofoner med lav inngangsforsterkning, og ga
                // «ingen data» selv ved normal tale.
                if (_audioCapture?.CalibrationProfile != null && _formantDetector != null)
                {
                    _formantDetector.RmsThreshold = Math.Clamp(
                        _audioCapture.CalibrationProfile.VoicedRmsThreshold * 0.75,
                        0.002,
                        0.01);
                }

                // Stil-bevisst resonans-scoring: pek scoringen mot brukerens faktiske
                // klangmål, ikke en universell feminin klang. Samme GetUserVoiceProfile-
                // kall som personaliseringen ellers bruker. Null-safe: ingen profil ⇒
                // Feminine-default (uendret historisk oppførsel).
                if (_resonanceScorer != null)
                {
                    var style = ResolveDatabase().GetUserVoiceProfile(1)?.PreferredVoiceStyle
                                ?? VoiceStyleGoal.Feminine;
                    _resonanceScorer.SetVoiceStyle(style);
                }

                if (_audioCapture == null)
                    throw new InvalidOperationException(Loc.Get("UI_MicNotReady"));

                _audioCapture.StartRecording();
                if (!_audioCapture.IsRecording)
                    throw new InvalidOperationException(Loc.Get("UI_MicNotReady"));

                _isRecording = true;
                _sessionStartTime = DateTime.Now;
                _formantHistory.Clear();
                _resonanceHistory.Clear();
                
                // Reset chart
                _chartViewModel?.Clear();
                
                // Update UI
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Recording_StartFailedFormat"), ex.Message), Loc.Get("UI_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Stop recording
                _audioCapture?.StopRecording();
                
                _isRecording = false;
                
                // Update UI
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                
                // Save session if we have data
                if (_resonanceHistory.Count > 10)
                {
                    SaveSession();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Recording_StopFailedFormat"), ex.Message), Loc.Get("UI_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OnAudioError(object? sender, string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                MessageBox.Show(message, Loc.Get("UI_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Warning)));
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear history and charts
            _formantHistory.Clear();
            _resonanceHistory.Clear();
            _chartViewModel?.Clear();
            
            // Reset display
            ResonanceScoreText.Text = "--";
            F1ValueText.Text = "--";
            F2ValueText.Text = "--";
            F3ValueText.Text = "--";
            CategoryText.Text = "--";
        }
        
        #endregion
        
        #region Database
        
        private void SaveSession()
        {
            if (_resonanceHistory.Count == 0) return;
            
            try
            {
                // Calculate session results
                var sessionResult = _resonanceScorer?.CalculateSessionResult(_resonanceHistory);
                
                if (sessionResult == null || !sessionResult.IsValid) return;
                
                // Create training session with resonance data
                var session = new TrainingSession
                {
                    StartTime = _sessionStartTime,
                    EndTime = DateTime.Now,
                    ResonanceScore = sessionResult.AverageScore,
                    AverageF1 = sessionResult.AverageF1,
                    AverageF2 = sessionResult.AverageF2,
                    AverageF3 = sessionResult.AverageF3,
                    ResonanceCategory = (Subsystems.Analysis.ResonanceCategory)sessionResult.Category,
                    SpectralCentroid = sessionResult.AverageSpectralCentroid,
                    OverallScore = sessionResult.AverageScore,
                    Feedback = GenerateFeedback(sessionResult)
                };
                
                // Save to database
                var db = ResolveDatabase();
                db.SaveTrainingSession(session);
                
                MessageBox.Show(Loc.Get("ResonanceWindow_SessionSaved"), Loc.Get("UI_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving session: {ex.Message}");
            }
        }
        
        private string GenerateFeedback(ResonansSessionResult result)
        {
            if (result.Category == Audio.AudioResonanceCategory.ForwardResonant)
            {
                return string.Format(
                    Loc.Get("ResonanceWindow_FeedbackForwardFormat"),
                    result.AverageScore,
                    result.AverageF1,
                    result.AverageF2);
            }
            else if (result.Category == Audio.AudioResonanceCategory.NeutralResonant)
            {
                return string.Format(
                    Loc.Get("ResonanceWindow_FeedbackNeutralFormat"),
                    result.AverageScore,
                    result.AverageF1,
                    result.AverageF2);
            }
            else
            {
                return string.Format(
                    Loc.Get("ResonanceWindow_FeedbackBackFormat"),
                    result.AverageScore,
                    result.AverageF1,
                    result.AverageF2);
            }
        }
        
        #endregion
        
        #region Window Lifecycle
        
        protected override void OnClosed(EventArgs e)
        {
            // Stop recording
            if (_isRecording)
            {
                _audioCapture?.StopRecording();
            }
            
            // Dispose services
            _audioCapture?.Dispose();
            
            base.OnClosed(e);
        }
        
        #endregion
    }
}
