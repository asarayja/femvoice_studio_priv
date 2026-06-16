using System;
using System.Collections.Generic;
using System.Diagnostics;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Voice Metrics Calculator - beregner alle nødvendige parametere for klinisk stemmeevaluering.
    /// Kombinerer pitch detection, formant analysis, og voice quality metrics.
    /// Target: &lt;100ms latens per syklus.
    /// </summary>
    public class VoiceMetricsCalculator : IDisposable
    {
        #region Private Fields

        private readonly PitchDetectionService _pitchDetector;
        private readonly FormantDetectionService _formantDetector;
        
        // History buffers for stability calculations
        private readonly Queue<VoiceMetrics> _recentMetrics;
        private readonly int _historySize;
        
        // Pitch analysis history
        private readonly List<double> _pitchHistory;
        private readonly List<double> _intensityHistory;
        
        // Smoothing
        private double _smoothedPitch;
        private double _smoothedF1;
        private double _smoothedF2;
        private double _smoothedIntensity;
        private readonly double _smoothingFactor;
        
        // Strain detection
        private int _consecutiveStrainCycles;
        private double _lastIntensity;
        
        // Performance tracking
        private readonly Stopwatch _processingStopwatch;
        
        #endregion

        #region Constants

        // Target ranges for clinical voice feminization
        public const double TargetMinPitch = 165.0;
        public const double TargetMaxPitch = 255.0;
        public const double BeginnerMinPitch = 150.0;
        public const double BeginnerMaxPitch = 200.0;
        public const double IntermediateMinPitch = 160.0;
        public const double IntermediateMaxPitch = 240.0;
        public const double AdvancedMinPitch = 165.0;
        public const double AdvancedMaxPitch = 255.0;

        public const double F1OptimalMin = 400.0;
        public const double F1OptimalMax = 700.0;
        public const double F2OptimalMin = 1400.0;
        public const double F2OptimalMax = 2200.0;
        public const double F2OptimalTarget = 1800.0; // Forward resonance target
        public const double F2BackResonance = 1400.0; // Below this is back resonance

        public const double SpectralCentroidFeminine = 2000.0; // Above this = bright tone

        public const double IntonationRangeMin = 30.0;
        public const double IntonationRangeMax = 120.0;
        public const double IntonationFlat = 30.0; // Below this is monoton

        public const double JitterWarningThreshold = 1.5;
        public const double JitterCriticalThreshold = 2.5;
        public const double ShimmerWarningThreshold = 3.5;
        public const double ShimmerCriticalThreshold = 5.0;
        public const double HnrWarningThreshold = 15.0;

        public const double PitchPressThreshold = 180.0;
        public const double AmplitudeSpikeStdDev = 2.0;

        #endregion

        #region Properties

        public bool IsProcessing => _processingStopwatch.IsRunning;

        #endregion

        #region Constructor

        public VoiceMetricsCalculator(int sampleRate = 44100, int historySize = 50)
        {
            _pitchDetector = new PitchDetectionService(sampleRate, 80, 500);
            _formantDetector = new FormantDetectionService(sampleRate, 25, 10, 12);
            
            _historySize = historySize;
            _recentMetrics = new Queue<VoiceMetrics>();
            _pitchHistory = new List<double>();
            _intensityHistory = new List<double>();
            
            _smoothingFactor = 0.3;
            _processingStopwatch = new Stopwatch();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Calculate VoiceMetrics from raw audio samples.
        /// Target latency: &lt;100ms
        /// </summary>
        /// <param name="samples">Raw audio samples</param>
        /// <returns>VoiceMetrics with all parameters calculated</returns>
        public VoiceMetrics CalculateMetrics(float[] samples)
        {
            _processingStopwatch.Restart();
            
            var metrics = new VoiceMetrics
            {
                Timestamp = DateTime.Now
            };

            try
            {
                // 1. Pitch Detection
                var pitchResult = _pitchDetector.DetectPitch(samples);
                metrics.Pitch = pitchResult.Pitch;
                metrics.Intensity = pitchResult.Intensity;
                
                // Apply smoothing
                _smoothedPitch = _smoothedPitch * (1 - _smoothingFactor) + metrics.Pitch * _smoothingFactor;
                metrics.Pitch = _smoothedPitch > 0 ? _smoothedPitch : metrics.Pitch;
                
                // 2. Formant Detection
                var formantResult = _formantDetector.ExtractFormants(samples);
                metrics.F1 = formantResult.SmoothedF1 > 0 ? formantResult.SmoothedF1 : formantResult.F1;
                metrics.F2 = formantResult.SmoothedF2 > 0 ? formantResult.SmoothedF2 : formantResult.F2;
                metrics.F3 = formantResult.SmoothedF3 > 0 ? formantResult.SmoothedF3 : formantResult.F3;
                metrics.SpectralCentroid = formantResult.SpectralCentroid;
                
                // Apply smoothing
                _smoothedF1 = _smoothedF1 * (1 - _smoothingFactor) + metrics.F1 * _smoothingFactor;
                _smoothedF2 = _smoothedF2 * (1 - _smoothingFactor) + metrics.F2 * _smoothingFactor;
                metrics.F1 = _smoothedF1 > 0 ? _smoothedF1 : metrics.F1;
                metrics.F2 = _smoothedF2 > 0 ? _smoothedF2 : metrics.F2;
                
                _smoothedIntensity = _smoothedIntensity * (1 - _smoothingFactor) + metrics.Intensity * _smoothingFactor;
                metrics.Intensity = _smoothedIntensity > 0 ? _smoothedIntensity : metrics.Intensity;
                
                // 3. Calculate Voice Quality (Jitter, Shimmer, HNR)
                CalculateVoiceQuality(metrics, pitchResult);
                
                // 4. Update History and Calculate Stability
                UpdateHistoryAndStability(metrics);
                
                // 5. Calculate Intonation
                CalculateIntonation(metrics);
                
                // 6. Classify Resonance
                ClassifyResonance(metrics);
                
                // 7. Check for Strain
                CheckStrainIndicators(metrics);
                
                // 8. Determine Health Status
                DetermineHealthStatus(metrics);
                
                // 9. Set Target Range Status
                SetTargetRangeStatus(metrics);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating voice metrics: {ex.Message}");
            }
            
            _processingStopwatch.Stop();
            return metrics;
        }

        /// <summary>
        /// Reset calculator state for new session
        /// </summary>
        public void Reset()
        {
            _recentMetrics.Clear();
            _pitchHistory.Clear();
            _intensityHistory.Clear();
            _consecutiveStrainCycles = 0;
            _smoothedPitch = 0;
            _smoothedF1 = 0;
            _smoothedF2 = 0;
            _smoothedIntensity = 0;
            _lastIntensity = 0;
            _formantDetector.Reset();
        }

        #endregion

        #region Private Calculation Methods

        private void CalculateVoiceQuality(VoiceMetrics metrics, PitchAnalysisResult pitchResult)
        {
            // Jitter calculation - based on pitch variation over recent history
            if (_pitchHistory.Count >= 3)
            {
                var recentPitches = _pitchHistory.GetRange(Math.Max(0, _pitchHistory.Count - 5), Math.Min(5, _pitchHistory.Count));
                if (recentPitches.Count >= 2)
                {
                    double mean = recentPitches.Average();
                    double sumSquares = recentPitches.Sum(p => Math.Pow(p - mean, 2));
                    double stdDev = Math.Sqrt(sumSquares / recentPitches.Count);
                    metrics.Jitter = mean > 0 ? (stdDev / mean) * 100 : 0;
                }
            }

            // Shimmer calculation - based on intensity variation
            if (_intensityHistory.Count >= 3)
            {
                var recentIntensity = _intensityHistory.GetRange(Math.Max(0, _intensityHistory.Count - 5), Math.Min(5, _intensityHistory.Count));
                if (recentIntensity.Count >= 2)
                {
                    double mean = recentIntensity.Average();
                    double sumSquares = recentIntensity.Sum(i => Math.Pow(i - mean, 2));
                    double stdDev = Math.Sqrt(sumSquares / recentIntensity.Count);
                    metrics.Shimmer = mean > 0 ? (stdDev / mean) * 100 : 0;
                }
            }

            // HNR approximation - based on confidence and RMS
            if (pitchResult.Confidence > 0 && metrics.Intensity > 0.01)
            {
                // Higher confidence and steady signal = better HNR
                double confidenceFactor = pitchResult.Confidence;
                double stabilityFactor = 1.0 - Math.Min(1.0, metrics.Jitter / JitterCriticalThreshold);
                metrics.HNR = 20 * confidenceFactor * stabilityFactor * Math.Log10(1 / (metrics.Intensity + 0.001));
                metrics.HNR = Math.Max(0, Math.Min(30, metrics.HNR)); // Clamp to realistic range
            }
            else
            {
                metrics.HNR = 0;
            }
        }

        private void UpdateHistoryAndStability(VoiceMetrics metrics)
        {
            // Add to pitch history
            if (metrics.Pitch > 0)
            {
                _pitchHistory.Add(metrics.Pitch);
                if (_pitchHistory.Count > 100)
                    _pitchHistory.RemoveAt(0);
            }

            // Add to intensity history
            if (metrics.Intensity > 0)
            {
                _intensityHistory.Add(metrics.Intensity);
                if (_intensityHistory.Count > 100)
                    _intensityHistory.RemoveAt(0);
            }

            // Add to recent metrics queue
            _recentMetrics.Enqueue(metrics);
            if (_recentMetrics.Count > _historySize)
                _recentMetrics.Dequeue();

            // Calculate pitch variation from history
            if (_pitchHistory.Count >= 2)
            {
                metrics.PitchVariation = _pitchHistory.StandardDeviation();
                metrics.MinPitch = _pitchHistory.Min();
                metrics.MaxPitch = _pitchHistory.Max();
            }

            // Calculate intensity variance
            if (_intensityHistory.Count >= 2)
            {
                metrics.IntensityVariance = _intensityHistory.StandardDeviation();
            }

            // Check for amplitude spike
            if (_lastIntensity > 0 && metrics.Intensity > 0)
            {
                double change = Math.Abs((metrics.Intensity - _lastIntensity) / _lastIntensity);
                double stdDev = _intensityHistory.Count > 5 ? _intensityHistory.StandardDeviation() : 0.1;
                metrics.HasAmplitudeSpike = change > AmplitudeSpikeStdDev * stdDev && change > 0.5;
            }
            _lastIntensity = metrics.Intensity;

            // Count consecutive strain cycles
            if (metrics.HasStrainWarning)
            {
                _consecutiveStrainCycles++;
            }
            else
            {
                _consecutiveStrainCycles = 0;
            }
            metrics.ConsecutiveStrainCycles = _consecutiveStrainCycles;
        }

        private void CalculateIntonation(VoiceMetrics metrics)
        {
            if (_pitchHistory.Count < 5)
            {
                metrics.IntonationRange = 0;
                metrics.IntonationPattern = IntonationPattern.NotApplicable;
                return;
            }

            // Calculate pitch range
            var recentPitches = _pitchHistory.TakeLast(Math.Min(20, _pitchHistory.Count)).Where(p => p > 0).ToList();
            if (recentPitches.Count >= 5)
            {
                metrics.IntonationRange = recentPitches.Max() - recentPitches.Min();
                
                // Analyze pattern (rising/falling)
                int firstQuarter = recentPitches.Count / 4;
                int thirdQuarter = recentPitches.Count * 3 / 4;
                
                double firstAvg = recentPitches.Take(firstQuarter).Average();
                double lastAvg = recentPitches.Skip(thirdQuarter).Average();
                double slope = (lastAvg - firstAvg) / firstAvg;
                
                // Determine pattern
                if (metrics.IntonationRange < IntonationFlat)
                {
                    metrics.IntonationPattern = IntonationPattern.Flat;
                }
                else if (slope > 0.05)
                {
                    metrics.IntonationPattern = IntonationPattern.Rising;
                    metrics.IntonationRiseScore = Math.Min(1.0, slope * 5);
                }
                else if (slope < -0.05)
                {
                    metrics.IntonationPattern = IntonationPattern.Falling;
                }
                else
                {
                    metrics.IntonationPattern = IntonationPattern.Natural;
                }
            }
        }

        private void ClassifyResonance(VoiceMetrics metrics)
        {
            // Clinical principle: F2 is primary indicator for forward resonance
            if (metrics.F2 >= 1800)
            {
                metrics.ResonanceClassification = ResonanceClassification.Forward;
            }
            else if (metrics.F2 >= F2BackResonance)
            {
                metrics.ResonanceClassification = ResonanceClassification.Neutral;
            }
            else
            {
                metrics.ResonanceClassification = ResonanceClassification.Back;
            }
        }

        private void CheckStrainIndicators(VoiceMetrics metrics)
        {
            // Calculate overall strain level
            double strainLevel = 0;
            
            // Jitter contribution
            if (metrics.Jitter > JitterWarningThreshold)
            {
                strainLevel += (metrics.Jitter - JitterWarningThreshold) / JitterCriticalThreshold;
            }
            
            // Shimmer contribution
            if (metrics.Shimmer > ShimmerWarningThreshold)
            {
                strainLevel += (metrics.Shimmer - ShimmerWarningThreshold) / ShimmerCriticalThreshold;
            }
            
            // HNR contribution
            if (metrics.HNR < HnrWarningThreshold && metrics.HNR > 0)
            {
                strainLevel += (HnrWarningThreshold - metrics.HNR) / HnrWarningThreshold;
            }
            
            // Pitch press detection
            if (metrics.Pitch > PitchPressThreshold && (metrics.Jitter > 1.0 || metrics.Shimmer > 2.0))
            {
                metrics.IsPitchPress = true;
                strainLevel += 0.3;
            }
            
            metrics.StrainLevel = Math.Min(1.0, strainLevel);
        }

        private void DetermineHealthStatus(VoiceMetrics metrics)
        {
            // Critical strain indicators
            if (metrics.Jitter > JitterCriticalThreshold || 
                metrics.Shimmer > ShimmerCriticalThreshold ||
                metrics.StrainLevel > 0.75)
            {
                metrics.HealthStatus = HealthIndicator.Critical;
            }
            // Warning indicators
            else if (metrics.HasStrainWarning || 
                     metrics.IsPitchPress ||
                     metrics.ConsecutiveStrainCycles >= 3)
            {
                metrics.HealthStatus = HealthIndicator.Warning;
            }
            else
            {
                metrics.HealthStatus = HealthIndicator.Safe;
            }
        }

        private void SetTargetRangeStatus(VoiceMetrics metrics)
        {
            metrics.IsInRange["Pitch"] = metrics.Pitch >= TargetMinPitch && metrics.Pitch <= TargetMaxPitch;
            metrics.IsInRange["F1"] = metrics.F1 >= F1OptimalMin && metrics.F1 <= F1OptimalMax;
            metrics.IsInRange["F2"] = metrics.F2 >= F2OptimalMin && metrics.F2 <= F2OptimalMax;
            metrics.IsInRange["SpectralCentroid"] = metrics.SpectralCentroid >= SpectralCentroidFeminine;
            metrics.IsInRange["Intonation"] = metrics.IntonationRange >= IntonationRangeMin && metrics.IntonationRange <= IntonationRangeMax;
            metrics.IsInRange["Jitter"] = metrics.Jitter <= JitterWarningThreshold;
            metrics.IsInRange["Shimmer"] = metrics.Shimmer <= ShimmerWarningThreshold;
            metrics.IsInRange["HNR"] = metrics.HNR >= HnrWarningThreshold || metrics.HNR == 0;
            
            // Distance from targets
            metrics.DistanceFromTarget["Pitch"] = metrics.Pitch > 0 ? 
                (metrics.Pitch < TargetMinPitch ? TargetMinPitch - metrics.Pitch : 
                 metrics.Pitch > TargetMaxPitch ? metrics.Pitch - TargetMaxPitch : 0) : 0;
            
            metrics.DistanceFromTarget["F2"] = metrics.F2 > 0 ?
                (metrics.F2 < F2BackResonance ? F2BackResonance - metrics.F2 : 0) : 0;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _processingStopwatch.Stop();
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    #region Extension Methods

    public static class ListExtensions
    {
        public static double StandardDeviation(this List<double> values)
        {
            if (values.Count < 2)
                return 0;
            
            double avg = values.Average();
            double sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSquares / values.Count);
        }
    }

    #endregion
}
