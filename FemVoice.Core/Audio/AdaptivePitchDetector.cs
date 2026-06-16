using System;
using System.Linq;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Adaptiv pitch-deteksjon med dynamisk threshold og støyestimering
    /// </summary>
    public class AdaptivePitchDetector
    {
        private readonly int _sampleRate;
        private readonly double _minFrequency;
        private readonly double _maxFrequency;
        
        // Dynamisk støyestimering
        private readonly RollingStatistics _noiseFloor;
        private readonly RollingStatistics _signalLevel;
        
        // Adaptiv threshold
        private double _adaptiveThreshold = 0.15;
        private const double MinThreshold = 0.05;
        private const double MaxThreshold = 0.30;
        private const double ThresholdAdaptationRate = 0.02;
        
        public AdaptivePitchDetector(int sampleRate = 44100, 
            double minFreq = 80, double maxFreq = 500)
        {
            _sampleRate = sampleRate;
            _minFrequency = minFreq;
            _maxFrequency = maxFreq;
            _noiseFloor = new RollingStatistics(50);
            _signalLevel = new RollingStatistics(100);
        }
        
        /// <summary>
        /// Detekterer pitch med adaptiv threshold
        /// </summary>
        public PitchResult DetectPitch(float[] samples)
        {
            double rms = CalculateRms(samples);
            
            _noiseFloor.Add(rms);
            double noiseLevel = _noiseFloor.Mean * 1.5;
            
            var result = new PitchResult
            {
                Timestamp = DateTime.Now,
                RmsValue = rms,
                IsVoiced = false
            };
            
            if (rms < noiseLevel)
            {
                result.IsVoiced = false;
                result.Pitch = 0;
                return result;
            }
            
            _signalLevel.Add(rms);
            
            double snr = rms / Math.Max(noiseLevel, 0.0001);
            if (snr < 2)
                _adaptiveThreshold = Math.Min(MaxThreshold, _adaptiveThreshold + ThresholdAdaptationRate);
            else if (snr > 5)
                _adaptiveThreshold = Math.Max(MinThreshold, _adaptiveThreshold - ThresholdAdaptationRate);
            
            var yinResult = YinDetect(samples, _adaptiveThreshold);
            
            if (yinResult.frequency > 0 && yinResult.probability > _adaptiveThreshold)
            {
                result.IsVoiced = true;
                result.Pitch = yinResult.frequency;
                result.Confidence = yinResult.probability;
                result.Snr = snr;
            }
            
            return result;
        }
        
        private (double frequency, double probability) YinDetect(float[] samples, double threshold)
        {
            int bufferSize = samples.Length;
            int halfBuffer = bufferSize / 2;
            double[] yinBuffer = new double[halfBuffer];
            
            for (int tau = 0; tau < halfBuffer; tau++)
            {
                yinBuffer[tau] = 0;
                for (int i = 0; i < halfBuffer; i++)
                {
                    float delta = samples[i] - samples[i + tau];
                    yinBuffer[tau] += delta * delta;
                }
            }
            
            yinBuffer[0] = 1;
            double runningSum = 0;
            for (int tau = 1; tau < halfBuffer; tau++)
            {
                runningSum += yinBuffer[tau];
                yinBuffer[tau] *= tau / runningSum;
            }
            
            int tauEstimate = -1;
            int minLag = _sampleRate / (int)_maxFrequency;
            int maxLag = _sampleRate / (int)_minFrequency;
            
            for (int tau = minLag; tau < Math.Min(maxLag, halfBuffer); tau++)
            {
                if (yinBuffer[tau] < threshold)
                {
                    while (tau + 1 < halfBuffer && yinBuffer[tau + 1] < yinBuffer[tau])
                        tau++;
                    tauEstimate = tau;
                    break;
                }
            }
            
            if (tauEstimate == -1)
                return (0, 0);
            
            double betterTau = tauEstimate;
            if (tauEstimate > 1 && tauEstimate < halfBuffer - 1)
            {
                double s0 = yinBuffer[tauEstimate - 1];
                double s1 = yinBuffer[tauEstimate];
                double s2 = yinBuffer[tauEstimate + 1];
                betterTau = tauEstimate + (s2 - s0) / (2 * (2 * s1 - s2 - s0));
            }
            
            double frequency = _sampleRate / betterTau;
            double probability = 1 - yinBuffer[tauEstimate];
            
            if (frequency < _minFrequency || frequency > _maxFrequency)
                return (0, probability);
            
            return (frequency, probability);
        }
        
        private static double CalculateRms(float[] samples)
        {
            if (samples.Length == 0) return 0;
            double sum = samples.Sum(s => (double)s * s);
            return Math.Sqrt(sum / samples.Length);
        }
        
        public void Calibrate(float[] backgroundSamples)
        {
            double noiseRms = CalculateRms(backgroundSamples);
            _noiseFloor.Clear();
            for (int i = 0; i < 30; i++)
                _noiseFloor.Add(noiseRms);
        }
        
        public double CurrentThreshold => _adaptiveThreshold;
    }
    
    public class RollingStatistics
    {
        private readonly double[] _values;
        private int _index;
        private int _count;
        private double _sum;
        
        public RollingStatistics(int windowSize)
        {
            _values = new double[windowSize];
        }
        
        public void Add(double value)
        {
            if (_count > 0) _sum -= _values[_index];
            _values[_index] = value;
            _sum += value;
            _index = (_index + 1) % _values.Length;
            if (_count < _values.Length) _count++;
        }
        
        public void Clear()
        {
            _index = 0;
            _count = 0;
            _sum = 0;
            Array.Clear(_values, 0, _values.Length);
        }
        
        public double Mean => _count > 0 ? _sum / _count : 0;
        public int Count => _count;
    }
    
    public class PitchResult
    {
        public DateTime Timestamp { get; set; }
        public double RmsValue { get; set; }
        public bool IsVoiced { get; set; }
        public double Pitch { get; set; }
        public double Confidence { get; set; }
        public double Snr { get; set; }
    }
}
