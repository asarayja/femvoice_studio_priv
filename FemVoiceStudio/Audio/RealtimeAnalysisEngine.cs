using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Real-time audio analysis engine for voice feminization training.
    /// Provides low-latency (less than 100ms) analysis of pitch, resonance, intonation, and voice quality.
    /// 
    /// Clinical Rationale:
    /// - Pitch (F0): Primary feminization target, range 160-260 Hz for most users
    /// - Resonance (F1/F2): More important than pitch for perceived femininity
    /// - Jitter/Shimmer: Voice stability indicators, greater than 2.5% jitter indicates strain
    /// - HNR: Harmonics-to-noise ratio, less than 15dB indicates hoarseness
    /// </summary>
    public class RealtimeAnalysisEngine : IDisposable
    {
        private readonly AudioCaptureService _audioCapture;
        private readonly PitchDetectionService _pitchDetector;
        private readonly FormantDetectionService _formantDetector;
        private readonly VoiceStrainDetector _strainDetector;
        
        private readonly int _sampleRate;
        private readonly int _analysisWindowMs;
        private readonly int _hopSizeMs;
        
        private readonly RollingBuffer<PitchAnalysisResult> _pitchBuffer;
        private readonly RollingBuffer<VoiceMetrics> _metricsBuffer;
        private readonly RollingBuffer<double> _intensityBuffer;
        
        public event EventHandler<VoiceMetrics>? MetricsUpdated;
        public event EventHandler<RealtimeFeedback>? FeedbackChanged;
        public event EventHandler<HealthAlert>? HealthAlertRaised;
        public event EventHandler<string>? ErrorOccurred;
        
        public class ClinicalThresholds
        {
            public double MinPitch { get; set; } = 165;
            public double MaxPitch { get; set; } = 255;
            public double OptimalPitch { get; set; } = 200;
            public double PitchPressThreshold { get; set; } = 180;
            public double MinF1 { get; set; } = 400;
            public double MaxF1 { get; set; } = 700;
            public double MinF2 { get; set; } = 1400;
            public double OptimalF2 { get; set; } = 1800;
            public double MaxJitter { get; set; } = 1.5;
            public double CriticalJitter { get; set; } = 2.5;
            public double MaxShimmer { get; set; } = 3.5;
            public double CriticalShimmer { get; set; } = 5.0;
            public double MinHNR { get; set; } = 15.0;
            public double MinIntonationRange { get; set; } = 30;
            public double OptimalIntonationRange { get; set; } = 60;
            public double MinIntensity { get; set; } = 0.05;
            public double MaxIntensity { get; set; } = 0.8;
        }
        
        public ClinicalThresholds Thresholds { get; } = new();
        
        public bool IsAnalyzing => _audioCapture.IsRecording;
        public int SampleRate => _sampleRate;
        
        private AdaptiveTargetZone _currentPitchZone;
        private AdaptiveTargetZone _currentResonanceZone;
        
        private int _strainConsecutiveFrames;
        private int _strainWarningFrames;
        private DateTime? _lastLockoutTime;
        private bool _isLockedOut;
        
        // Signal smoothing state for pitch visualization
        private double _smoothedPitch;
        private double _smoothedF2;
        private double _smoothedIntensity;
        
        // Smoothing parameters - balanced for real-time feedback
        private const double PitchSmoothingAlpha = 0.3;    // Responsive enough for real-time
        private const double ResonanceSmoothingAlpha = 0.2; // More stable for resonance
        private const double IntensitySmoothingAlpha = 0.4; // Quick response for intensity
        
        private const int MaxHistorySize = 500;
        private const int LockoutDurationMinutes = 15;
        private const int MaxStrainFramesBeforeLockout = 100;
        
        public RealtimeAnalysisEngine(int sampleRate = 44100, int analysisWindowMs = 50, int hopSizeMs = 25)
        {
            _sampleRate = sampleRate;
            _analysisWindowMs = analysisWindowMs;
            _hopSizeMs = hopSizeMs;
            
            _audioCapture = new AudioCaptureService(sampleRate);
            _pitchDetector = new PitchDetectionService(sampleRate);
            _formantDetector = new FormantDetectionService();
            _strainDetector = new VoiceStrainDetector();
            
            _pitchBuffer = new RollingBuffer<PitchAnalysisResult>(MaxHistorySize);
            _metricsBuffer = new RollingBuffer<VoiceMetrics>(MaxHistorySize);
            _intensityBuffer = new RollingBuffer<double>(50);
            
            _currentPitchZone = new AdaptiveTargetZone(165, 200, 255);
            _currentResonanceZone = new AdaptiveTargetZone(1400, 1800, 2200);
            
            _audioCapture.AudioDataAvailable += OnAudioDataAvailable;
        }
        
        public void Initialize()
        {
            _audioCapture.Initialize();
            if (_audioCapture.CalibrationProfile != null)
            {
                _pitchDetector.VoicedRmsThreshold = _audioCapture.CalibrationProfile.VoicedRmsThreshold;
                Thresholds.MinIntensity = Math.Clamp(
                    _audioCapture.CalibrationProfile.VoicedRmsThreshold * 0.8,
                    0.001,
                    Thresholds.MinIntensity);
            }

            _formantDetector.Reset();
            _strainDetector.Reset();
            _pitchBuffer.Clear();
            _metricsBuffer.Clear();
            _intensityBuffer.Clear();
            _strainConsecutiveFrames = 0;
            _strainWarningFrames = 0;
            _isLockedOut = false;
        }
        
        public void StartAnalysis()
        {
            if (_isLockedOut && _lastLockoutTime.HasValue)
            {
                var remaining = DateTime.Now - _lastLockoutTime.Value;
                if (remaining.TotalMinutes < LockoutDurationMinutes)
                {
                    ErrorOccurred?.Invoke(this, $"Voice health lockout active. Try again in {LockoutDurationMinutes - (int)remaining.TotalMinutes} minutes.");
                    return;
                }
                _isLockedOut = false;
            }
            
            _pitchBuffer.Clear();
            _metricsBuffer.Clear();
            _intensityBuffer.Clear();
            _strainConsecutiveFrames = 0;
            _strainWarningFrames = 0;
            
            // Reset smoothing state for new session
            ResetSmoothing();
            
            _audioCapture.StartRecording();
        }
        
        public SessionAnalysisSummary StopAnalysis()
        {
            _audioCapture.StopRecording();
            
            var pitches = _pitchBuffer.ToList();
            var metrics = _metricsBuffer.ToList();
            
            return new SessionAnalysisSummary
            {
                StartTime = _pitchBuffer.FirstOrDefault()?.Timestamp ?? DateTime.Now,
                EndTime = DateTime.Now,
                AveragePitch = pitches.Where(p => p.IsVoiced).Any() ? pitches.Where(p => p.IsVoiced).Average(p => p.Pitch) : 0,
                MinPitch = pitches.Where(p => p.IsVoiced).Any() ? pitches.Where(p => p.IsVoiced).Min(p => p.Pitch) : 0,
                MaxPitch = pitches.Where(p => p.IsVoiced).Any() ? pitches.Where(p => p.IsVoiced).Max(p => p.Pitch) : 0,
                AverageF1 = metrics.Where(m => m.F1 > 0).Any() ? metrics.Where(m => m.F1 > 0).Average(m => m.F1) : 0,
                AverageF2 = metrics.Where(m => m.F2 > 0).Any() ? metrics.Where(m => m.F2 > 0).Average(m => m.F2) : 0,
                AverageJitter = metrics.Where(m => m.Jitter > 0).Any() ? metrics.Where(m => m.Jitter > 0).Average(m => m.Jitter) : 0,
                AverageShimmer = metrics.Where(m => m.Shimmer > 0).Any() ? metrics.Where(m => m.Shimmer > 0).Average(m => m.Shimmer) : 0,
                AverageHNR = metrics.Where(m => m.HNR > 0).Any() ? metrics.Where(m => m.HNR > 0).Average(m => m.HNR) : 0,
                VoicedPercentage = _pitchBuffer.Count > 0 
                    ? (double)_pitchBuffer.CountWhere(p => p.IsVoiced) / _pitchBuffer.Count * 100 
                    : 0,
                StrainDetected = _strainConsecutiveFrames > MaxStrainFramesBeforeLockout / 2,
                LockoutTriggered = _isLockedOut
            };
        }
        
        public void UpdateTargetZones(AdaptiveTargetZone pitchZone, AdaptiveTargetZone resonanceZone)
        {
            _currentPitchZone = pitchZone;
            _currentResonanceZone = resonanceZone;
        }
        
        /// <summary>
        /// Get the current smoothed pitch value for visualization.
        /// Uses exponential moving average for smooth curves without UI freezing.
        /// </summary>
        public double GetSmoothedPitch() => _smoothedPitch;
        
        /// <summary>
        /// Get the current smoothed F2 value for visualization.
        /// </summary>
        public double GetSmoothedF2() => _smoothedF2;
        
        /// <summary>
        /// Get the current smoothed intensity value.
        /// </summary>
        public double GetSmoothedIntensity() => _smoothedIntensity;
        
        /// <summary>
        /// Reset smoothing state for new session.
        /// </summary>
        public void ResetSmoothing()
        {
            _smoothedPitch = 0;
            _smoothedF2 = 0;
            _smoothedIntensity = 0;
        }
        
        private void OnAudioDataAvailable(object? sender, float[] samples)
        {
            if (_isLockedOut) return;
            Task.Run(() => AnalyzeFrame(samples));
        }
        
        private void AnalyzeFrame(float[] samples)
        {
            try
            {
                var pitchResult = _pitchDetector.DetectPitch(samples);
                _pitchBuffer.Add(pitchResult);
                
                double rms = CalculateRms(samples);
                _intensityBuffer.Add(rms);
                
                var formantResult = _formantDetector.ExtractFormants(samples);
                var strainAnalysis = _strainDetector.Analyze(samples, pitchResult.Pitch);
                
                var metrics = new VoiceMetrics
                {
                    Timestamp = DateTime.Now,
                    Pitch = pitchResult.IsVoiced ? pitchResult.Pitch : 0,
                    Intensity = rms,
                    F1 = formantResult.F1,
                    F2 = formantResult.F2,
                    F3 = formantResult.F3,
                    SpectralCentroid = formantResult.SpectralCentroid,
                    ResonanceClassification = ClassifyResonance(formantResult.F1, formantResult.F2),
                    Jitter = strainAnalysis.JitterValue,
                    Shimmer = strainAnalysis.ShimmerValue,
                    HNR = pitchResult.Confidence * 20,
                    IntonationRange = CalculateIntonationRange(),
                    StrainLevel = strainAnalysis.StrainLevel switch
                    {
                        StrainLevel.High => 0.9,
                        StrainLevel.Medium => 0.5,
                        _ => 0.1
                    },
                    HealthStatus = DetermineHealthStatus(strainAnalysis, pitchResult),
                    IsInRange = new Dictionary<string, bool>
                    {
                        ["Pitch"] = IsPitchInRange(pitchResult.Pitch),
                        ["Resonance"] = IsResonanceInRange(formantResult.F2),
                        ["Stability"] = strainAnalysis.StrainLevel == StrainLevel.Normal
                    },
                    DistanceFromTarget = new Dictionary<string, double>
                    {
                        ["Pitch"] = Math.Abs(pitchResult.Pitch - Thresholds.OptimalPitch),
                        ["Resonance"] = Math.Abs(formantResult.F2 - Thresholds.OptimalF2)
                    }
                };
                
                // Apply signal smoothing for visualization
                // Using EMA for smooth curves without UI freezing
                if (pitchResult.IsVoiced)
                {
                    _smoothedPitch = SignalSmoothing.CalculateEMA(
                        pitchResult.Pitch, 
                        _smoothedPitch, 
                        PitchSmoothingAlpha);
                    metrics.Pitch = _smoothedPitch; // Use smoothed for display
                }
                
                if (formantResult.F2 > 0)
                {
                    _smoothedF2 = SignalSmoothing.CalculateEMA(
                        formantResult.F2, 
                        _smoothedF2, 
                        ResonanceSmoothingAlpha);
                }
                
                _smoothedIntensity = SignalSmoothing.CalculateEMA(
                    rms, 
                    _smoothedIntensity, 
                    IntensitySmoothingAlpha);
                
                if (_pitchBuffer.Count > 10)
                {
                    var recentPitches = _pitchBuffer.TakeLast(10).Where(p => p.IsVoiced).Select(p => p.Pitch).ToList();
                    if (recentPitches.Count > 1)
                    {
                        metrics.PitchVariation = CalculateStandardDeviation(recentPitches);
                    }
                }
                
                _metricsBuffer.Add(metrics);
                
                var feedback = GenerateRealtimeFeedback(metrics, pitchResult, strainAnalysis);
                CheckHealthAlerts(strainAnalysis, pitchResult);
                
                MetricsUpdated?.Invoke(this, metrics);
                FeedbackChanged?.Invoke(this, feedback);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Analysis error: {ex.Message}");
            }
        }
        
        private bool IsPitchInRange(double pitch)
        {
            return pitch >= _currentPitchZone.Min && pitch <= _currentPitchZone.Max;
        }
        
        private bool IsResonanceInRange(double f2)
        {
            return f2 >= _currentResonanceZone.Min && f2 <= _currentResonanceZone.Max;
        }
        
        private ResonanceClassification ClassifyResonance(double f1, double f2)
        {
            if (f2 >= 1800) return ResonanceClassification.Forward;
            if (f2 >= 1400) return ResonanceClassification.Neutral;
            return ResonanceClassification.Back;
        }
        
        private double CalculateIntonationRange()
        {
            if (_pitchBuffer.Count < 5) return 0;
            
            var recentPitches = _pitchBuffer.TakeLast(20)
                .Where(p => p.IsVoiced)
                .Select(p => p.Pitch)
                .ToList();
            
            if (recentPitches.Count < 2) return 0;
            
            return recentPitches.Max() - recentPitches.Min();
        }
        
        private HealthIndicator DetermineHealthStatus(StrainAnalysis strain, PitchAnalysisResult pitch)
        {
            if (strain.StrainLevel == StrainLevel.High || 
                strain.JitterValue > Thresholds.CriticalJitter ||
                strain.ShimmerValue > Thresholds.CriticalShimmer)
            {
                return HealthIndicator.Critical;
            }
            
            if (strain.StrainLevel == StrainLevel.Medium ||
                strain.JitterValue > Thresholds.MaxJitter ||
                strain.ShimmerValue > Thresholds.MaxShimmer)
            {
                return HealthIndicator.Warning;
            }
            
            if (pitch.IsVoiced && pitch.Pitch > Thresholds.PitchPressThreshold && 
                _metricsBuffer.LastOrDefault()?.PitchVariation > 30)
            {
                return HealthIndicator.Warning;
            }
            
            return HealthIndicator.Safe;
        }
        
        private RealtimeFeedback GenerateRealtimeFeedback(VoiceMetrics metrics, PitchAnalysisResult pitch, StrainAnalysis strain)
        {
            var feedback = new RealtimeFeedback
            {
                Timestamp = DateTime.Now,
                CurrentPitch = metrics.Pitch,
                CurrentF2 = metrics.F2,
                StrainLevel = metrics.StrainLevel
            };
            
            if (pitch.IsVoiced)
            {
                if (metrics.Pitch < _currentPitchZone.Min)
                {
                    feedback.PitchStatus = ParameterStatus.TooLow;
                    feedback.PitchMessage = $"Pitch: {metrics.Pitch:F0} Hz - Prov hoeyere mot {_currentPitchZone.Min:F0} Hz";
                }
                else if (metrics.Pitch > _currentPitchZone.Max)
                {
                    feedback.PitchStatus = ParameterStatus.TooHigh;
                    feedback.PitchMessage = $"Pitch: {metrics.Pitch:F0} Hz - Senk litt mot {_currentPitchZone.Max:F0} Hz";
                }
                else
                {
                    feedback.PitchStatus = ParameterStatus.InRange;
                    feedback.PitchMessage = $"Pitch: {metrics.Pitch:F0} Hz - Bra!";
                }
            }
            else
            {
                feedback.PitchStatus = ParameterStatus.NoVoice;
                feedback.PitchMessage = "Ingen stemme detektert";
            }
            
            if (metrics.F2 > 0)
            {
                if (metrics.F2 < _currentResonanceZone.Min)
                {
                    feedback.ResonanceStatus = ParameterStatus.TooLow;
                    feedback.ResonanceMessage = $"Resonans: F2={metrics.F2:F0} Hz - Prov mer fremover mot tennene";
                }
                else if (metrics.F2 > _currentResonanceZone.Max)
                {
                    feedback.ResonanceStatus = ParameterStatus.TooHigh;
                    feedback.ResonanceMessage = $"Resonans: F2={metrics.F2:F0} Hz - Veldig lys stemme!";
                }
                else
                {
                    feedback.ResonanceStatus = ParameterStatus.InRange;
                    feedback.ResonanceMessage = $"Resonans: F2={metrics.F2:F0} Hz - Bra fremoverresonans!";
                }
            }
            
            if (strain.StrainLevel == StrainLevel.High)
            {
                feedback.HealthStatus = (Models.HealthIndicator)2; // Critical
                feedback.HealthMessage = "STOPP! Ta en pause - stemmen trenger hvile";
            }
            else if (strain.StrainLevel == StrainLevel.Medium)
            {
                feedback.HealthStatus = (Models.HealthIndicator)1; // Warning
                feedback.HealthMessage = "Vaer forsiktig - lett press registrert";
            }
            else
            {
                feedback.HealthStatus = (Models.HealthIndicator)0; // Safe
            }
            
            if (feedback.HealthStatus == (Models.HealthIndicator)2)
            {
                feedback.OverallStatus = RealtimeFeedbackLevel.Stop;
            }
            else if (feedback.PitchStatus == ParameterStatus.InRange && 
                     feedback.ResonanceStatus == ParameterStatus.InRange)
            {
                feedback.OverallStatus = RealtimeFeedbackLevel.Success;
            }
            else if (feedback.PitchStatus == ParameterStatus.NoVoice)
            {
                feedback.OverallStatus = RealtimeFeedbackLevel.Neutral;
            }
            else
            {
                feedback.OverallStatus = RealtimeFeedbackLevel.Adjust;
            }
            
            return feedback;
        }
        
        private void CheckHealthAlerts(StrainAnalysis strain, PitchAnalysisResult pitch)
        {
            if (strain.StrainLevel == StrainLevel.High)
            {
                _strainConsecutiveFrames++;
                _strainWarningFrames++;
                
                if (_strainConsecutiveFrames >= MaxStrainFramesBeforeLockout)
                {
                    _isLockedOut = true;
                    _lastLockoutTime = DateTime.Now;
                    
                    HealthAlertRaised?.Invoke(this, new HealthAlert
                    {
                        AlertType = HealthAlertType.Lockout,
                        Message = $"Stemmehvile paakrevd i {LockoutDurationMinutes} minutter",
                        Timestamp = DateTime.Now
                    });
                }
                else if (_strainConsecutiveFrames >= MaxStrainFramesBeforeLockout / 2)
                {
                    HealthAlertRaised?.Invoke(this, new HealthAlert
                    {
                        AlertType = HealthAlertType.ImmediateRest,
                        Message = "Ta en pause naa! Stigende strain detektert.",
                        Timestamp = DateTime.Now
                    });
                }
            }
            else if (strain.StrainLevel == StrainLevel.Medium)
            {
                _strainWarningFrames++;
                _strainConsecutiveFrames = Math.Max(0, _strainConsecutiveFrames - 1);
                
                if (_strainWarningFrames >= 50)
                {
                    HealthAlertRaised?.Invoke(this, new HealthAlert
                    {
                        AlertType = HealthAlertType.Warning,
                        Message = "Vaer forsiktig med volum og intensitet",
                        Timestamp = DateTime.Now
                    });
                }
            }
            else
            {
                _strainConsecutiveFrames = Math.Max(0, _strainConsecutiveFrames - 2);
                _strainWarningFrames = Math.Max(0, _strainWarningFrames - 1);
            }
            
            if (strain.JitterValue > Thresholds.CriticalJitter || strain.ShimmerValue > Thresholds.CriticalShimmer)
            {
                HealthAlertRaised?.Invoke(this, new HealthAlert
                {
                    AlertType = HealthAlertType.Warning,
                    Message = "Ustabil stemme detektert - ta en pust",
                    Timestamp = DateTime.Now
                });
            }
        }
        
        private static double CalculateRms(float[] samples)
        {
            if (samples.Length == 0) return 0;
            double sum = 0;
            foreach (var s in samples) sum += (double)s * s;
            return Math.Sqrt(sum / samples.Length);
        }
        
        private static double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2) return 0;
            double avg = values.Average();
            double sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSquares / values.Count);
        }
        
        public void Dispose()
        {
            _audioCapture.Dispose();
        }
    }
    
    /// <summary>
    /// Rolling buffer with fixed maximum size - automatically removes oldest items
    /// </summary>
    public class RollingBuffer<T>
    {
        private readonly List<T> _items = new();
        private readonly int _maxSize;
        private readonly object _lock = new();
        
        public int Count
        {
            get { lock (_lock) return _items.Count; }
        }
        
        public RollingBuffer(int maxSize)
        {
            _maxSize = maxSize;
        }
        
        public void Add(T item)
        {
            lock (_lock)
            {
                _items.Add(item);
                if (_items.Count > _maxSize)
                {
                    _items.RemoveAt(0);
                }
            }
        }
        
        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
            }
        }
        
        public List<T> ToList()
        {
            lock (_lock)
            {
                return new List<T>(_items);
            }
        }
        
        public T? LastOrDefault()
        {
            lock (_lock)
            {
                return _items.Count > 0 ? _items[_items.Count - 1] : default;
            }
        }
        
        public T? FirstOrDefault()
        {
            lock (_lock)
            {
                return _items.Count > 0 ? _items[0] : default;
            }
        }
        
        public IEnumerable<T> TakeLast(int count)
        {
            lock (_lock)
            {
                int start = Math.Max(0, _items.Count - count);
                return _items.Skip(start).ToList();
            }
        }
        
        public bool Any(Func<T, bool> predicate)
        {
            lock (_lock)
            {
                return _items.Any(predicate);
            }
        }
        
        public int CountWhere(Func<T, bool> predicate)
        {
            lock (_lock)
            {
                return _items.Count(predicate);
            }
        }
    }
    
    /// <summary>
    /// Signal smoothing utilities for audio visualization.
    /// Implements exponential moving average for smooth pitch curves.
    /// </summary>
    public static class SignalSmoothing
    {
        /// <summary>
        /// Exponential moving average coefficient.
        /// Higher values = more responsive but less smooth.
        /// Lower values = smoother but more lag.
        /// </summary>
        public const double DefaultAlpha = 0.3;
        
        /// <summary>
        /// Calculate exponential moving average for smoothing signal data.
        /// </summary>
        /// <param name="currentValue">Current signal value</param>
        /// <param name="previousSmoothed">Previous smoothed value</param>
        /// <param name="alpha">Smoothing factor (0-1, default 0.3)</param>
        /// <returns>Smoothed value</returns>
        public static double CalculateEMA(double currentValue, double previousSmoothed, double alpha = DefaultAlpha)
        {
            if (double.IsNaN(currentValue) || double.IsInfinity(currentValue))
                return previousSmoothed;
            
            if (double.IsNaN(previousSmoothed) || previousSmoothed == 0)
                return currentValue;
            
            return alpha * currentValue + (1 - alpha) * previousSmoothed;
        }
        
        /// <summary>
        /// Apply simple moving average to a buffer of values.
        /// </summary>
        /// <param name="values">Collection of values to average</param>
        /// <param name="windowSize">Number of values to include in average</param>
        /// <returns>Average of last windowSize values</returns>
        public static double CalculateSMA(IEnumerable<double> values, int windowSize = 5)
        {
            var recentValues = values.TakeLast(windowSize).ToList();
            if (recentValues.Count == 0)
                return 0;
            
            return recentValues.Average();
        }
        
        /// <summary>
        /// Apply median filter for removing outliers while preserving edges.
        /// </summary>
        /// <param name="values">Collection of values</param>
        /// <param name="windowSize">Odd number window size</param>
        /// <returns>Median-filtered value</returns>
        public static double CalculateMedian(IEnumerable<double> values, int windowSize = 3)
        {
            var window = values.TakeLast(windowSize).OrderBy(v => v).ToList();
            if (window.Count == 0)
                return 0;
            
            int mid = window.Count / 2;
            if (window.Count % 2 == 0)
                return (window[mid - 1] + window[mid]) / 2;
            
            return window[mid];
        }
        
        /// <summary>
        /// Apply low-pass Butterworth filter approximation using exponential smoothing.
        /// Good for removing high-frequency noise while preserving signal shape.
        /// </summary>
        /// <param name="currentValue">Current signal value</param>
        /// <param name="previousFiltered">Previous filtered value</param>
        /// <param name="cutoffFrequency">Normalized cutoff frequency (0-1)</param>
        /// <param name="sampleRate">Sample rate for time constant calculation</param>
        /// <returns>Filtered value</returns>
        public static double ApplyLowPassFilter(double currentValue, double previousFiltered, 
            double cutoffFrequency = 0.1, int sampleRate = 44100)
        {
            // Calculate time constant from cutoff frequency
            double rc = 1.0 / (2 * Math.PI * cutoffFrequency);
            double dt = 1.0 / sampleRate;
            double alpha = dt / (rc + dt);
            
            return CalculateEMA(currentValue, previousFiltered, alpha);
        }
    }
}
