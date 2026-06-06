using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// ViewModel for LiveFeedbackView - viser sanntids tilbakemelding under ovelsesutfoersel
    /// </summary>
    public partial class LiveFeedbackViewModel : ObservableObject, IDisposable
    {
        private readonly ExerciseFeedbackEngine? _feedbackEngine;
        private SmartCoachExerciseAdapter? _adapter;
        private readonly Dispatcher _dispatcher;
        private DateTime _lastUpdate = DateTime.MinValue;
        private const int MinUpdateIntervalMs = 50; // Max ~20 updates per second
        
        // Status properties
        [ObservableProperty]
        private EvaluationStatus _resonanceStatus = EvaluationStatus.NotApplicable;
        
        [ObservableProperty]
        private EvaluationStatus _pitchStatus = EvaluationStatus.NotApplicable;
        
        [ObservableProperty]
        private EvaluationStatus _stabilityStatus = EvaluationStatus.NotApplicable;
        
        [ObservableProperty]
        private EvaluationStatus _intonationStatus = EvaluationStatus.NotApplicable;
        
        [ObservableProperty]
        private HealthIndicator _healthIndicator = HealthIndicator.Safe;
        
        // Live values
        [ObservableProperty]
        private string _livePitch = "--";
        
        [ObservableProperty]
        private string _liveF2 = "--";
        
        [ObservableProperty]
        private string _liveF1 = "--";
        
        [ObservableProperty]
        private string _liveJitter = "--";
        
        // Coach hint
        [ObservableProperty]
        private string _coachHint = "";
        
        // Visibility
        [ObservableProperty]
        private bool _showIntonation = false;
        
        // Paused state
        [ObservableProperty]
        private bool _isPaused = false;
        
        public LiveFeedbackViewModel()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }
        
        public LiveFeedbackViewModel(ExerciseFeedbackEngine feedbackEngine) : this()
        {
            _feedbackEngine = feedbackEngine;
            _adapter = new SmartCoachExerciseAdapter();
            
            if (_feedbackEngine != null)
            {
                _feedbackEngine.EvaluationCompleted += OnEvaluationCompleted;
                _feedbackEngine.HealthWarning += OnHealthWarning;
                _feedbackEngine.HealthCritical += OnHealthCritical;
            }
        }
        
        /// <summary>
        /// Koble til feedback engine
        /// </summary>
        public void AttachFeedbackEngine(ExerciseFeedbackEngine engine)
        {
            if (_feedbackEngine != null)
            {
                _feedbackEngine.EvaluationCompleted -= OnEvaluationCompleted;
            }
            
            // Use reflection or cast - but for now create new if needed
            _adapter = new SmartCoachExerciseAdapter();
            
            if (engine != null)
            {
                engine.EvaluationCompleted += OnEvaluationCompleted;
                engine.HealthWarning += OnHealthWarning;
                engine.HealthCritical += OnHealthCritical;
            }
        }
        
        private void OnEvaluationCompleted(object? sender, ExerciseEvaluationResult result)
        {
            // Throttle updates
            var now = DateTime.Now;
            if ((now - _lastUpdate).TotalMilliseconds < MinUpdateIntervalMs)
                return;
            _lastUpdate = now;
            
            _dispatcher.BeginInvoke(() => UpdateFromResult(result));
        }
        
        private void OnHealthWarning(object? sender, ExerciseEvaluationResult result)
        {
            _dispatcher.BeginInvoke(() =>
            {
                UpdateFromResult(result);
                // Show notification
                CoachHint = LocalizationService.Instance[result.CoachHintKey];
            });
        }
        
        private void OnHealthCritical(object? sender, ExerciseEvaluationResult result)
        {
            _dispatcher.BeginInvoke(() =>
            {
                UpdateFromResult(result);
                // Critical stop - show warning and pause
                CoachHint = LocalizationService.Instance[result.CoachHintKey];
                IsPaused = true;
            });
        }
        
        private void UpdateFromResult(ExerciseEvaluationResult result)
        {
            try
            {
                ResonanceStatus = result.ResonanceStatus;
                PitchStatus = result.PitchStatus;
                StabilityStatus = result.StabilityStatus;
                IntonationStatus = result.IntonationStatus;
                HealthIndicator = result.HealthIndicator;
                
                // Update hint
                var hintKey = result.CoachHintKey;
                if (!string.IsNullOrEmpty(hintKey))
                {
                    CoachHint = LocalizationService.Instance[hintKey];
                }
                
                // Show intonation if relevant
                ShowIntonation = result.IntonationStatus != EvaluationStatus.NotApplicable;
                
                // Update live values from details if available
                if (result.Details != null)
                {
                    if (result.Details.TryGetValue("Pitch", out var pitch))
                        LivePitch = $"{pitch:F0} Hz";
                    if (result.Details.TryGetValue("F2", out var f2))
                        LiveF2 = $"{f2:F0} Hz";
                    if (result.Details.TryGetValue("F1", out var f1))
                        LiveF1 = $"{f1:F0} Hz";
                    if (result.Details.TryGetValue("Jitter", out var jitter))
                        LiveJitter = $"{jitter:F2}%";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating feedback view: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Pause evalueringen
        /// </summary>
        [RelayCommand]
        private void Pause()
        {
            if (_feedbackEngine != null)
            {
                _feedbackEngine.Pause();
                IsPaused = true;
            }
        }
        
        /// <summary>
        /// Gjenoppta evalueringen
        /// </summary>
        [RelayCommand]
        private void Resume()
        {
            if (_feedbackEngine != null)
            {
                _feedbackEngine.Resume();
                IsPaused = false;
            }
        }
        
        /// <summary>
        /// Reset status indicators
        /// </summary>
        public void Reset()
        {
            ResonanceStatus = EvaluationStatus.NotApplicable;
            PitchStatus = EvaluationStatus.NotApplicable;
            StabilityStatus = EvaluationStatus.NotApplicable;
            IntonationStatus = EvaluationStatus.NotApplicable;
            HealthIndicator = HealthIndicator.Safe;
            LivePitch = "--";
            LiveF2 = "--";
            LiveF1 = "--";
            LiveJitter = "--";
            CoachHint = "";
            IsPaused = false;
        }
        
        public void Dispose()
        {
            if (_feedbackEngine != null)
            {
                _feedbackEngine.EvaluationCompleted -= OnEvaluationCompleted;
                _feedbackEngine.HealthWarning -= OnHealthWarning;
                _feedbackEngine.HealthCritical -= OnHealthCritical;
            }
            GC.SuppressFinalize(this);
        }
    }
}
