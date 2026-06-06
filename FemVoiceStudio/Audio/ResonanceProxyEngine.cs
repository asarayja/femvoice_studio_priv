using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using NAudio.Dsp;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Real-time resonance analysis engine for voice feminization biofeedback.
    /// </summary>
    public struct FormantSnapshot
    {
        public double F1 { get; set; }
        public double F2 { get; set; }
        public double F3 { get; set; }
        public double SpectralCentroid { get; set; }
        public double Stability { get; set; }
        public long Timestamp { get; set; }
        public double RmsValue { get; set; }
        public double Confidence { get; set; }
        public double F2MinusF1 => F2 - F1;
        public double F3MinusF2 => F3 - F2;
        
        public static FormantSnapshot Empty => new FormantSnapshot { F1 = 0, F2 = 0, F3 = 0 };
        public bool IsValid => F1 > 0 && F2 > 0 && F3 > 0 && Confidence > 0.1;
    }

    public enum ResonanceMode { Performance, Balanced, Precision }

    public class ResonanceProxyEngine : IDisposable
    {
        private const double TargetF1Optimal = 320.0;
        private const double TargetF2Optimal = 2300.0;
        private const double TargetF3Optimal = 2900.0;
        private const double TargetSpacingOptimal = 1900.0;
        private const double TargetCentroidOptimal = 2500.0;
        private const int DefaultSampleRate = 48000;
        private const int DefaultFftSize = 2048;
        private const int DefaultHopSize = 512;
        private const int DefaultSmoothingWindow = 5;
        private const double DefaultSmoothingFactor = 0.4;
        private const double MinRmsThreshold = 0.01;
        private const double PreEmphasisCoeff = 0.97;

        private readonly int _sampleRate;
        private readonly int _fftSize;
        private readonly int _hopSize;
        private readonly Complex[] _fftBuffer;
        private readonly float[] _windowBuffer;
        private readonly float[] _inputBuffer;
        private int _bufferPosition;
        private readonly ResonanceMode _mode;
        private readonly int _smoothingWindowSize;
        private double _formantShiftWeight = 0.40;
        private double _spacingWeight = 0.25;
        private double _brightnessWeight = 0.25;
        private double _stabilityWeight = 0.10;
        private readonly Queue<FormantSnapshot> _formantHistory;
        private FormantSnapshot _lastValidSnapshot;
        private volatile bool _isProcessing;
        private volatile bool _isDisposed;
        private long _frameCount;
        private readonly SynchronizationContext? _syncContext;
        private double _lastSample;

        public event Action<double>? ResonanceScoreUpdated;
        public event Action<FormantSnapshot>? FormantsUpdated;
        public event Action<string>? ErrorOccurred;

        public double FormantShiftWeight { get => _formantShiftWeight; set => _formantShiftWeight = Math.Clamp(value, 0.0, 1.0); }
        public double SpacingWeight { get => _spacingWeight; set => _spacingWeight = Math.Clamp(value, 0.0, 1.0); }
        public double BrightnessWeight { get => _brightnessWeight; set => _brightnessWeight = Math.Clamp(value, 0.0, 1.0); }
        public double StabilityWeight { get => _stabilityWeight; set => _stabilityWeight = Math.Clamp(value, 0.0, 1.0); }
        public double RmsThreshold { get; set; } = MinRmsThreshold;
        public ResonanceMode Mode => _mode;
        public int SampleRate => _sampleRate;
        public int FftSize => _fftSize;
        public bool IsProcessing => _isProcessing;

        public ResonanceProxyEngine(int sampleRate = 48000, int fftSize = 2048, ResonanceMode mode = ResonanceMode.Balanced, int smoothingWindowSize = 5)
        {
            int validFftSize = 1024;
            while (validFftSize < fftSize) validFftSize *= 2;
            _fftSize = validFftSize;
            _sampleRate = sampleRate;
            _mode = mode;
            _smoothingWindowSize = smoothingWindowSize;
            _hopSize = mode switch { ResonanceMode.Performance => _fftSize / 4, ResonanceMode.Balanced => _fftSize / 4, ResonanceMode.Precision => _fftSize / 2, _ => DefaultHopSize };
            _fftBuffer = new Complex[_fftSize];
            _windowBuffer = new float[_fftSize];
            _inputBuffer = new float[_fftSize];
            _formantHistory = new Queue<FormantSnapshot>(_smoothingWindowSize + 1);
            GenerateWindowFunction();
            _syncContext = SynchronizationContext.Current;
        }

        public void ProcessSamples(float[] samples)
        {
            if (_isDisposed || samples == null || samples.Length == 0) return;
            foreach (float sample in samples)
            {
                double emphasized = sample - PreEmphasisCoeff * _lastSample;
                _lastSample = sample;
                _inputBuffer[_bufferPosition] = (float)emphasized;
                _bufferPosition++;
                if (_bufferPosition >= _fftSize)
                {
                    ProcessFrame();
                    _bufferPosition = _fftSize - _hopSize;
                    if (_bufferPosition > 0 && _bufferPosition < _fftSize)
                        Array.Copy(_inputBuffer, _hopSize, _inputBuffer, 0, _bufferPosition);
                }
            }
        }

        public void ProcessByteBuffer(byte[] buffer, int bytesRecorded)
        {
            if (_isDisposed || buffer == null || bytesRecorded == 0) return;
            int sampleCount = bytesRecorded / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++) { short s = BitConverter.ToInt16(buffer, i * 2); samples[i] = s / 32768f; }
            ProcessSamples(samples);
        }

        public void Reset()
        {
            Array.Clear(_fftBuffer, 0, _fftBuffer.Length);
            Array.Clear(_inputBuffer, 0, _inputBuffer.Length);
            Array.Clear(_windowBuffer, 0, _windowBuffer.Length);
            _bufferPosition = 0;
            _frameCount = 0;
            _lastSample = 0;
            _formantHistory.Clear();
            _lastValidSnapshot = FormantSnapshot.Empty;
        }

        public void Start() { if (!_isProcessing && !_isDisposed) { Reset(); _isProcessing = true; } }
        public void Stop() { _isProcessing = false; }

        private void ProcessFrame()
        {
            if (!_isProcessing) return;

            try
            {
                _frameCount++;
                double rms = CalculateRms(_inputBuffer, _fftSize);
                if (rms < RmsThreshold) return;
                for (int i = 0; i < _fftSize; i++) { _fftBuffer[i].X = _inputBuffer[i] * _windowBuffer[i]; _fftBuffer[i].Y = 0; }
                FastFourierTransform.FFT(true, (int)Math.Log2(_fftSize), _fftBuffer);
                float[] magnitudes = new float[_fftSize / 2];
                double totalEnergy = 0;
                for (int i = 0; i < magnitudes.Length; i++) { magnitudes[i] = (float)Math.Sqrt(_fftBuffer[i].X * _fftBuffer[i].X + _fftBuffer[i].Y * _fftBuffer[i].Y); totalEnergy += magnitudes[i] * magnitudes[i]; }
                if (totalEnergy < 0.0001f) return;
                double centroid = CalculateSpectralCentroid(magnitudes, totalEnergy);
                var formants = ExtractFormants(magnitudes);
                formants.SpectralCentroid = centroid;
                formants.RmsValue = rms;
                formants.Confidence = CalculateFormantConfidence(magnitudes, formants);
                formants.Timestamp = _frameCount;
                if (!formants.IsValid) return;
                double stability = CalculateStability(formants);
                formants.Stability = stability;
                ApplySmoothing(formants);
                double resonanceScore = CalculateResonanceScore(formants, stability);
                RaiseEvents(resonanceScore, formants);
            }
            catch (Exception ex)
            {
                // Surface internal engine errors to subscribers (health/logging)
                try { RaiseError($"Resonance processing error: {ex.Message}"); } catch { }
            }
        }

        private void RaiseError(string message)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(_ => ErrorOccurred?.Invoke(message), null);
            }
            else
            {
                ErrorOccurred?.Invoke(message);
            }
        }

        private FormantSnapshot ExtractFormants(float[] magnitudes)
        {
            var snapshot = FormantSnapshot.Empty;
            int minBin = (200 * _fftSize) / _sampleRate;
            int maxBin = (4000 * _fftSize) / _sampleRate;
            maxBin = Math.Min(maxBin, magnitudes.Length - 1);
            var peaks = FindPeaksInRange(magnitudes, minBin, maxBin, 3);
            if (peaks.Count >= 1) snapshot.F1 = peaks[0];
            if (peaks.Count >= 2) snapshot.F2 = peaks[1];
            if (peaks.Count >= 3) snapshot.F3 = peaks[2];
            if (snapshot.F1 == 0 || snapshot.F2 == 0) { snapshot.F1 = 350; snapshot.F2 = 2000; snapshot.F3 = 2800; }
            return snapshot;
        }

        private List<double> FindPeaksInRange(float[] magnitudes, int minBin, int maxBin, int numPeaks)
        {
            var peaks = new List<double>();
            int freqResolution = _sampleRate / _fftSize;
            for (int i = minBin + 2; i < maxBin - 2; i++)
            {
                float current = magnitudes[i];
                if (current > magnitudes[i-1] && current > magnitudes[i-2] && current > magnitudes[i+1] && current > magnitudes[i+2])
                {
                    double refinedBin = ParabolicInterpolation(magnitudes, i);
                    double frequency = refinedBin * freqResolution;
                    peaks.Add(frequency);
                }
            }
            peaks.Sort((a, b) => magnitudes[(int)(b * _fftSize / _sampleRate)].CompareTo(magnitudes[(int)(a * _fftSize / _sampleRate)]));
            return peaks.Take(numPeaks).ToList();
        }

        private double ParabolicInterpolation(float[] magnitudes, int peakIndex)
        {
            if (peakIndex <= 0 || peakIndex >= magnitudes.Length - 1) return peakIndex;
            float alpha = magnitudes[peakIndex - 1], beta = magnitudes[peakIndex], gamma = magnitudes[peakIndex + 1];
            double denominator = alpha - 2 * beta + gamma;
            if (Math.Abs(denominator) < 1e-10) return peakIndex;
            return peakIndex + 0.5 * (alpha - gamma) / denominator;
        }

        private double CalculateSpectralCentroid(float[] magnitudes, double totalEnergy)
        {
            double weightedSum = 0;
            int minBin = (200 * _fftSize) / _sampleRate;
            int maxBin = (4000 * _fftSize) / _sampleRate;
            maxBin = Math.Min(maxBin, magnitudes.Length - 1);
            double freqResolution = (double)_sampleRate / _fftSize;
            for (int i = minBin; i <= maxBin; i++) weightedSum += i * freqResolution * magnitudes[i];
            return totalEnergy > 0 ? weightedSum / totalEnergy : 0;
        }

        private double CalculateFormantConfidence(float[] magnitudes, FormantSnapshot formants)
        {
            if (formants.F1 <= 0 || formants.F2 <= 0) return 0;
            int f1Bin = (int)(formants.F1 * _fftSize / _sampleRate);
            int f2Bin = (int)(formants.F2 * _fftSize / _sampleRate);
            if (f1Bin >= magnitudes.Length || f2Bin >= magnitudes.Length) return 0.3;
            double f1Mag = magnitudes[f1Bin], f2Mag = magnitudes[f2Bin];
            double peakEnergy = f1Mag * f1Mag + f2Mag * f2Mag;
            double totalEnergy = magnitudes.Sum(m => m * m);
            if (totalEnergy < 0.0001) return 0;
            return Math.Min(1.0, (peakEnergy / totalEnergy) * 10);
        }

        private double CalculateStability(FormantSnapshot current)
        {
            if (_lastValidSnapshot.F1 <= 0 || !_lastValidSnapshot.IsValid) { _lastValidSnapshot = current; return 1.0; }
            double f1Change = Math.Abs(current.F1 - _lastValidSnapshot.F1) / _lastValidSnapshot.F1;
            double f2Change = Math.Abs(current.F2 - _lastValidSnapshot.F2) / _lastValidSnapshot.F2;
            double stability = 1.0 - Math.Min(1.0, (f1Change + f2Change) / 2.0 * 10);
            _lastValidSnapshot = current;
            return stability;
        }

        private void ApplySmoothing(FormantSnapshot current)
        {
            _formantHistory.Enqueue(current);
            if (_formantHistory.Count > _smoothingWindowSize) _formantHistory.Dequeue();
        }

        private double CalculateResonanceScore(FormantSnapshot formants, double stability)
        {
            double formantScore = NormalizeFormantShift(formants.F1, formants.F2, formants.F3);
            double spacingScore = NormalizeSpacing(formants.F2MinusF1);
            double brightnessScore = NormalizeBrightness(formants.SpectralCentroid);
            double stabilityScore = stability;
            double totalWeight = _formantShiftWeight + _spacingWeight + _brightnessWeight + _stabilityWeight;
            return (_formantShiftWeight * formantScore + _spacingWeight * spacingScore + _brightnessWeight * brightnessScore + _stabilityWeight * stabilityScore) / totalWeight;
        }

        private double NormalizeFormantShift(double f1, double f2, double f3)
        {
            double f1Score = 1.0 - Math.Abs(f1 - TargetF1Optimal) / 200.0;
            double f2Score = 1.0 - Math.Abs(f2 - TargetF2Optimal) / 400.0;
            double f3Score = 1.0 - Math.Abs(f3 - TargetF3Optimal) / 400.0;
            return Math.Clamp((f1Score * 0.3 + f2Score * 0.4 + f3Score * 0.3), 0.0, 1.0);
        }

        private double NormalizeSpacing(double spacing) => Math.Clamp(1.0 - Math.Abs(spacing - TargetSpacingOptimal) / 500.0, 0.0, 1.0);
        private double NormalizeBrightness(double centroid) => Math.Clamp(1.0 - Math.Abs(centroid - TargetCentroidOptimal) / 600.0, 0.0, 1.0);

        private void RaiseEvents(double score, FormantSnapshot formants)
        {
            void Raise() { ResonanceScoreUpdated?.Invoke(score); FormantsUpdated?.Invoke(formants); }
            if (_syncContext != null) _syncContext.Post(_ => Raise(), null);
            else Raise();
        }

        private double CalculateRms(float[] buffer, int length)
        {
            double sum = 0;
            for (int i = 0; i < length; i++) sum += buffer[i] * buffer[i];
            return Math.Sqrt(sum / length);
        }

        private void GenerateWindowFunction()
        {
            for (int i = 0; i < _fftSize; i++)
            {
                double n = (double)i / (_fftSize - 1);
                _windowBuffer[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * n)));
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _isProcessing = false;
            ResonanceScoreUpdated = null;
            FormantsUpdated = null;
            ErrorOccurred = null;
        }
    }
}
