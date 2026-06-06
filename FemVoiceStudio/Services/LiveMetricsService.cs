using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Services
{
    /// <summary>
    /// Service for real-time voice metrics calculation.
    /// Provides smoothed pitch, stability assessment, and health monitoring.
    /// </summary>
    public class LiveMetricsService
    {
        private readonly Queue<double> _recentPitches = new();
        private readonly Queue<double> _recentResonances = new();
        private readonly Queue<double> _recentIntensities = new();
        private readonly Queue<HealthSnapshot> _healthHistory = new();
        
        private const int PitchWindowSize = 15;     // ~500ms at 30fps
        private const int ResonanceWindowSize = 30;  // ~1s at 30fps
        private const int HealthWindowSize = 100;   // ~3.3s for health trends
        
        private double _lastPitch = 0;
        private StabilityState _lastStability = StabilityState.NoVoice;
        private HealthState _lastHealth = HealthState.NoVoice;
        
        // Pitch smoothing parameters
        private readonly double _smoothingFactor = 0.3;
        
        // Health thresholds
        private const double StrainThreshold = 50.0;
        private const double CriticalStrainThreshold = 75.0;
        private const double FatigueWindowMs = 30000; // 30 seconds
        private const double PitchVarianceThreshold = 50.0; // Hz variance threshold
        
        // Resonance estimation constants
        private const double F2LowIdeal = 1400.0;
        private const double F2HighIdeal = 2200.0;
        
        /// <summary>
        /// Calculate smoothed pitch using exponential moving average
        /// </summary>
        public double CalculateSmoothedPitch(double rawPitch, bool isVoiced)
        {
            if (!isVoiced || rawPitch <= 0)
            {
                _lastPitch = 0;
                return 0;
            }
            
            if (_lastPitch <= 0)
            {
                _lastPitch = rawPitch;
            }
            else
            {
                // Exponential moving average for smoothing
                _lastPitch = _smoothingFactor * rawPitch + (1 - _smoothingFactor) * _lastPitch;
            }
            
            // Add to recent pitches queue
            _recentPitches.Enqueue(_lastPitch);
            while (_recentPitches.Count > PitchWindowSize)
            {
                _recentPitches.Dequeue();
            }
            
            return _lastPitch;
        }
        
        /// <summary>
        /// Estimate resonance from spectral characteristics
        /// This is a proxy based on spectral centroid, not anatomical claims
        /// </summary>
        public double EstimateResonance(double spectralCentroid, double pitch, double intensity)
        {
            // Spectral centroid as a proxy for "brightness"
            // Higher centroid generally indicates brighter sound
            double brightnessScore = 0;
            
            if (spectralCentroid > 0)
            {
                brightnessScore = Math.Min(100, spectralCentroid / 25.0); // Rough scaling
            }
            
            // Higher pitch tends to have brighter perception - adjust
            if (pitch > 180)
            {
                brightnessScore = Math.Min(100, brightnessScore + (pitch - 180) / 10.0);
            }
            
            // Normalize to 0-100
            double resonance = Math.Clamp(brightnessScore, 0, 100);
            
            // Add to recent resonances queue
            _recentResonances.Enqueue(resonance);
            while (_recentResonances.Count > ResonanceWindowSize)
            {
                _recentResonances.Dequeue();
            }
            
            return resonance;
        }
        
        /// <summary>
        /// Calculate stability state based on recent pitch variance
        /// </summary>
        public StabilityState CalculateStability()
        {
            if (_recentPitches.Count < 5)
                return StabilityState.NoVoice;
            
            var recentList = _recentPitches.TakeLast(10).ToList();
            if (recentList.Count < 5)
                return StabilityState.NoVoice;
            
            // Calculate variance
            double mean = recentList.Average();
            double variance = recentList.Sum(p => Math.Pow(p - mean, 2)) / recentList.Count;
            double stdDev = Math.Sqrt(variance);
            
            // Determine stability state
            if (stdDev < 5)
                return StabilityState.VeryStable;
            if (stdDev < 15)
                return StabilityState.Stable;
            if (stdDev < 30)
                return StabilityState.Developing;
            
            return StabilityState.Unstable;
        }
        
        /// <summary>
        /// Calculate health state based on strain indicators
        /// </summary>
        public HealthState CalculateHealth(double strainLevel, double pitch, double intensity)
        {
            if (_recentPitches.Count < 5)
                return HealthState.NoVoice;
            
            HealthState state;
            
            // Check for critical strain
            if (strainLevel >= CriticalStrainThreshold || pitch > 280)
            {
                state = HealthState.Danger;
            }
            // Check for warning strain
            else if (strainLevel >= StrainThreshold || pitch > 260)
            {
                state = HealthState.Warning;
            }
            // Check for moderate strain
            else if (strainLevel > 30)
            {
                state = HealthState.Monitor;
            }
            else
            {
                state = HealthState.Safe;
            }
            
            // Record health snapshot
            var snapshot = new HealthSnapshot
            {
                Timestamp = DateTime.Now,
                StrainLevel = strainLevel,
                FatigueLevel = CalculateFatigueLevel(),
                IntensityControl = intensity > 0.7 ? 0 : 100 - intensity * 100,
                StrainDetected = state >= HealthState.Warning,
                FatigueWarning = CalculateFatigueLevel() > 70
            };
            
            _healthHistory.Enqueue(snapshot);
            while (_healthHistory.Count > HealthWindowSize)
            {
                _healthHistory.Dequeue();
            }
            
            _lastHealth = state;
            return state;
        }
        
        /// <summary>
        /// Calculate fatigue level based on recent health history
        /// </summary>
        private double CalculateFatigueLevel()
        {
            if (_healthHistory.Count < 10)
                return 0;
            
            var recent = _healthHistory.TakeLast(20).ToList();
            
            // Count warning/danger states
            double warningCount = recent.Count(h => h.StrainLevel > StrainThreshold);
            double fatigueRatio = warningCount / recent.Count;
            
            return Math.Min(100, fatigueRatio * 200);
        }
        
        /// <summary>
        /// Get current stability state
        /// </summary>
        public StabilityState CurrentStability => _lastStability;
        
        /// <summary>
        /// Get current health state
        /// </summary>
        public HealthState CurrentHealth => _lastHealth;
        
        /// <summary>
        /// Get average resonance from recent window
        /// </summary>
        public double GetAverageResonance()
        {
            if (_recentResonances.Count == 0)
                return 50;
            return _recentResonances.Average();
        }
        
        /// <summary>
        /// Get pitch variance over recent window
        /// </summary>
        public double GetPitchVariance()
        {
            if (_recentPitches.Count < 2)
                return 0;
            
            var recent = _recentPitches.ToList();
            double mean = recent.Average();
            return recent.Sum(p => Math.Pow(p - mean, 2)) / recent.Count;
        }
        
        /// <summary>
        /// Reset metrics for new session
        /// </summary>
        public void Reset()
        {
            _recentPitches.Clear();
            _recentResonances.Clear();
            _recentIntensities.Clear();
            _healthHistory.Clear();
            _lastPitch = 0;
            _lastStability = StabilityState.NoVoice;
            _lastHealth = HealthState.NoVoice;
        }
        
        /// <summary>
        /// Calculate estimated F2 from audio features (simplified)
        /// </summary>
        public double EstimateF2(double spectralCentroid)
        {
            // Simple estimation based on spectral centroid relationship
            // This is a rough proxy, not anatomical measurement
            if (spectralCentroid <= 0)
                return 1500;
            
            // Map centroid to F2 range
            double f2 = 1200 + (spectralCentroid / 30.0) * 600;
            return Math.Clamp(f2, 1000, 2500);
        }
    }
}
