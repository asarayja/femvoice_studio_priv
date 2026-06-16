using System;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Detekterer stemmeanstrengelse basert på audio-analyse
    /// </summary>
    public class VoiceStrainDetector
    {
        private readonly RollingStatistics _amplitudeHistory;
        private readonly RollingStatistics _pitchHistory;
        
        private const double HighAmplitudeThreshold = 0.8;
        private const double PitchInstabilityThreshold = 50;
        private const int StrainWindowFrames = 50;
        
        private int _highAmplitudeFrames;
        private int _unstablePitchFrames;
        
        public VoiceStrainDetector()
        {
            _amplitudeHistory = new RollingStatistics(StrainWindowFrames);
            _pitchHistory = new RollingStatistics(StrainWindowFrames);
        }
        
        public StrainAnalysis Analyze(float[] samples, double pitch)
        {
            double rms = CalculateRms(samples);
            _amplitudeHistory.Add(rms);
            
            if (pitch > 0)
            {
                _pitchHistory.Add(pitch);
            }
            
            var analysis = new StrainAnalysis
            {
                CurrentAmplitude = rms,
                AverageAmplitude = _amplitudeHistory.Mean,
                IsHighAmplitude = rms > HighAmplitudeThreshold,
                IsPitchUnstable = false,
                JitterValue = 0,
                ShimmerValue = 0
            };
            
            if (rms > HighAmplitudeThreshold)
            {
                _highAmplitudeFrames++;
            }
            else
            {
                _highAmplitudeFrames = Math.Max(0, _highAmplitudeFrames - 1);
            }
            
            if (_pitchHistory.Count > 10)
            {
                double stdDev = CalculateStdDev();
                analysis.IsPitchUnstable = stdDev > PitchInstabilityThreshold;
                analysis.PitchStdDev = stdDev;
                
                if (analysis.IsPitchUnstable)
                {
                    _unstablePitchFrames++;
                }
            }
            
            if (_highAmplitudeFrames > StrainWindowFrames * 0.5 || 
                _unstablePitchFrames > StrainWindowFrames * 0.3)
            {
                analysis.StrainLevel = StrainLevel.High;
                analysis.Message = "Pause gjerne i noen minutter. Stemmen din trenger hvile.";
            }
            else if (_highAmplitudeFrames > StrainWindowFrames * 0.3)
            {
                analysis.StrainLevel = StrainLevel.Medium;
                analysis.Message = "Vær forsiktig med volumet. Prøv å snakke litt roligere.";
            }
            else
            {
                analysis.StrainLevel = StrainLevel.Normal;
                analysis.Message = null;
            }
            
            return analysis;
        }
        
        private static double CalculateRms(float[] samples)
        {
            if (samples.Length == 0) return 0;
            double sum = 0;
            foreach (var s in samples) sum += (double)s * s;
            return Math.Sqrt(sum / samples.Length);
        }
        
        private double CalculateStdDev()
        {
            if (_amplitudeHistory.Count < 2) return 0;
            return _amplitudeHistory.Mean * 0.1;
        }
        
        public void Reset()
        {
            _highAmplitudeFrames = 0;
            _unstablePitchFrames = 0;
        }
    }
    
    public enum StrainLevel { Normal, Medium, High }
    
    public class StrainAnalysis
    {
        public double CurrentAmplitude { get; set; }
        public double AverageAmplitude { get; set; }
        public double PitchStdDev { get; set; }
        public bool IsHighAmplitude { get; set; }
        public bool IsPitchUnstable { get; set; }
        public StrainLevel StrainLevel { get; set; }
        public string? Message { get; set; }
        
        // Jitter and shimmer for compatibility
        public double JitterValue { get; set; }
        public double ShimmerValue { get; set; }
    }
}
