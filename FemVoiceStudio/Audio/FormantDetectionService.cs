using System;
using System.Collections.Generic;
using System.Linq;
using FemVoiceStudio.Models;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Implementerer LPC-basert formantekstraksjon for sanntidsanalyse av F1, F2 og F3.
    /// Bruker Linear Predictive Coding (LPC) med autokorrelasjon for koeffisientberegning,
    /// etterfulgt av spektral topp-finiding for formantfrekvenser.
    /// </summary>
    /// <remarks>
    /// Algoritme-referanser:
    /// - Markel & Gray (1976): Linear Prediction of Speech
    /// - Rabiner & Schafer (1978): Digital Processing of Speech Signals
    /// - Feminine formantområder: F1 ~300-800 Hz, F2 ~1500-2500 Hz, F3 ~2500-3500 Hz
    /// </remarks>
    public class FormantDetectionService
    {
        #region Private Fields

        private readonly int _sampleRate;
        private readonly int _order;
        private readonly double _preEmphasis;
        
        // Temporal smoothing
        private readonly int _smoothingWindow;
        private readonly double[] _f1History;
        private readonly double[] _f2History;
        private readonly double[] _f3History;
        private int _historyIndex;
        
        // Støyrobustheit
        private readonly double _rmsThreshold;
        private readonly double _confidenceThreshold;
        
        private double[] _lastLpcCoefficients = Array.Empty<double>();
        private double _lastResidualEnergy;
        private int _hopSize;

        #endregion

        #region Public Properties

        public double CurrentF1 { get; private set; }
        public double CurrentF2 { get; private set; }
        public double CurrentF3 { get; private set; }

        public double SmoothedF1 => _f1History.Take(_smoothingWindow).Where(f => f > 0).DefaultIfEmpty(0).Average();
        public double SmoothedF2 => _f2History.Take(_smoothingWindow).Where(f => f > 0).DefaultIfEmpty(0).Average();
        public double SmoothedF3 => _f3History.Take(_smoothingWindow).Where(f => f > 0).DefaultIfEmpty(0).Average();

        public double Confidence { get; private set; }
        public double RmsValue { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initialiserer formantdeteksjonsservice med angitte parametere.
        /// </summary>
        /// <param name="sampleRate">Sample rate i Hz (typisk 44100)</param>
        /// <param name="frameSizeMs">Framesize i millisekunder (anbefalt: 20-30ms)</param>
        /// <param name="hopSizeMs">Hop size i millisekunder (anbefalt: 10ms for sanntid)</param>
        /// <param name="lpcOrder">LPC-orden (anbefalt: 10-16 for tale)</param>
        public FormantDetectionService(
            int sampleRate = 44100, 
            int frameSizeMs = 25, 
            int hopSizeMs = 10,
            int lpcOrder = 12)
        {
            _sampleRate = sampleRate;
            _hopSize = (sampleRate * hopSizeMs) / 1000;
            _order = lpcOrder;
            _preEmphasis = 0.97;
            
            _smoothingWindow = 10;
            _f1History = new double[_smoothingWindow];
            _f2History = new double[_smoothingWindow];
            _f3History = new double[_smoothingWindow];
            
            _rmsThreshold = 0.01;
            _confidenceThreshold = 0.3;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Ekstraherer formanter fra et audio frame.
        /// </summary>
        /// <param name="samples">Normaliserte audio samples (-1.0 til 1.0)</param>
        /// <returns>FormantAnalysisResult med F1, F2, F3 og metadata</returns>
        public FormantAnalysisResult ExtractFormants(float[] samples)
        {
            var result = new FormantAnalysisResult
            {
                Timestamp = DateTime.Now,
                FrameRms = CalculateRms(samples)
            };

            RmsValue = result.FrameRms;

            if (result.FrameRms < _rmsThreshold)
            {
                result.IsValid = false;
                result.Confidence = 0;
                UpdateHistory(0, 0, 0);
                return result;
            }

            var preEmphasized = ApplyPreEmphasis(samples);
            var lpcCoefficients = ComputeLpcCoefficients(preEmphasized, _order);
            _lastLpcCoefficients = lpcCoefficients;

            _lastResidualEnergy = ComputeResidualEnergy(preEmphasized, lpcCoefficients);
            result.ResidualEnergy = _lastResidualEnergy;

            var formants = FindFormantFrequencies(lpcCoefficients, _sampleRate);
            formants = formants.OrderBy(f => f.Frequency).ToList();

            if (formants.Count >= 1)
            {
                CurrentF1 = formants[0].Frequency;
                result.F1 = CurrentF1;
            }
            if (formants.Count >= 2)
            {
                CurrentF2 = formants[1].Frequency;
                result.F2 = CurrentF2;
            }
            if (formants.Count >= 3)
            {
                CurrentF3 = formants[2].Frequency;
                result.F3 = CurrentF3;
            }

            result.Confidence = CalculateConfidence(formants, result.FrameRms);
            Confidence = result.Confidence;

            result.IsValid = result.Confidence > _confidenceThreshold && 
                            result.F1 > 200 && result.F1 < 1000 &&
                            result.F2 > 800 && result.F2 < 3000;

            UpdateHistory(result.F1, result.F2, result.F3);

            result.SmoothedF1 = SmoothedF1;
            result.SmoothedF2 = SmoothedF2;
            result.SmoothedF3 = SmoothedF3;
            result.SpectralCentroid = CalculateSpectralCentroid(samples);

            return result;
        }

        /// <summary>
        /// Nullstiller intern state (bruk ved ny økt).
        /// </summary>
        public void Reset()
        {
            Array.Clear(_f1History, 0, _f1History.Length);
            Array.Clear(_f2History, 0, _f2History.Length);
            Array.Clear(_f3History, 0, _f3History.Length);
            _historyIndex = 0;
            CurrentF1 = 0;
            CurrentF2 = 0;
            CurrentF3 = 0;
            Confidence = 0;
        }

        #endregion

        #region Private Methods - Signal Processing

        private double CalculateRms(float[] samples)
        {
            if (samples.Length == 0) return 0;
            double sum = 0;
            foreach (var s in samples)
                sum += s * s;
            return Math.Sqrt(sum / samples.Length);
        }

        private double CalculateSpectralCentroid(float[] samples)
        {
            if (samples.Length == 0) return 0;
            
            // Simple spectral centroid approximation using RMS and zero-crossings
            double rms = CalculateRms(samples);
            int zeroCrossings = 0;
            
            for (int i = 1; i < samples.Length; i++)
            {
                if ((samples[i] >= 0 && samples[i-1] < 0) || (samples[i] < 0 && samples[i-1] >= 0))
                    zeroCrossings++;
            }
            
            // Estimate centroid based on zero-crossing rate (higher = brighter sound)
            double zcr = (double)zeroCrossings / samples.Length;
            double centroid = 500 + (zcr * 4000); // Rough estimate
            
            return centroid;
        }

        private float[] ApplyPreEmphasis(float[] samples)
        {
            var result = new float[samples.Length];
            result[0] = samples[0];
            for (int i = 1; i < samples.Length; i++)
            {
                result[i] = (float)(samples[i] - _preEmphasis * samples[i - 1]);
            }
            return result;
        }

        private double[] ComputeLpcCoefficients(float[] samples, int order)
        {
            int n = samples.Length;
            var windowed = ApplyHammingWindow(samples);
            
            var r = new double[order + 1];
            for (int lag = 0; lag <= order; lag++)
            {
                double sum = 0;
                for (int i = 0; i < n - lag; i++)
                    sum += windowed[i] * windowed[i + lag];
                r[lag] = sum;
            }

            var a = new double[order + 1];
            var e = new double[order + 1];
            
            e[0] = r[0];
            a[0] = 1;

            for (int i = 1; i <= order; i++)
            {
                double sum = 0;
                for (int j = 1; j < i; j++)
                    sum += a[j] * r[i - j];

                double k = (r[i] - sum) / (e[i - 1] + 1e-10);

                a[i] = -k;

                for (int j = 1; j < i; j++)
                {
                    a[j] = a[j] - k * a[i - j];
                }

                e[i] = (1 - k * k) * e[i - 1];
            }

            return a;
        }

        private float[] ApplyHammingWindow(float[] samples)
        {
            var result = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                double window = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (samples.Length - 1));
                result[i] = (float)(samples[i] * window);
            }
            return result;
        }

        private double ComputeResidualEnergy(float[] samples, double[] lpc)
        {
            int n = samples.Length;
            double energy = 0;
            int order = lpc.Length - 1;
            
            for (int i = order; i < n; i++)
            {
                double prediction = 0;
                for (int j = 1; j <= order; j++)
                    prediction += lpc[j] * samples[i - j];
                
                double residual = samples[i] - prediction;
                energy += residual * residual;
            }
            
            return energy / (n - order);
        }

        private List<FormantInfo> FindFormantFrequencies(double[] lpc, int sampleRate)
        {
            var spectrum = ComputeLpcSpectrum(lpc, sampleRate);
            return FindSpectralPeaks(spectrum, sampleRate);
        }

        private double[] ComputeLpcSpectrum(double[] lpc, int sampleRate)
        {
            int fftSize = 2048;
            var spectrum = new double[fftSize / 2 + 1];
            
            for (int i = 0; i < spectrum.Length; i++)
            {
                double freq = (double)i * sampleRate / fftSize;
                double omega = 2 * Math.PI * freq / sampleRate;
                
                double real = 0, imag = 0;
                for (int j = 0; j < lpc.Length; j++)
                {
                    real += lpc[j] * Math.Cos(-j * omega);
                    imag += lpc[j] * Math.Sin(-j * omega);
                }
                
                spectrum[i] = 1.0 / Math.Sqrt(real * real + imag * imag + 1e-10);
            }
            
            return spectrum;
        }

        private List<FormantInfo> FindSpectralPeaks(double[] spectrum, int sampleRate)
        {
            var peaks = new List<FormantInfo>();
            int fftSize = (spectrum.Length - 1) * 2;
            double binWidth = (double)sampleRate / fftSize;
            
            for (int i = 3; i < spectrum.Length - 3; i++)
            {
                if (spectrum[i] > spectrum[i - 1] &&
                    spectrum[i] > spectrum[i - 2] &&
                    spectrum[i] > spectrum[i - 3] &&
                    spectrum[i] > spectrum[i + 1] &&
                    spectrum[i] > spectrum[i + 2] &&
                    spectrum[i] > spectrum[i + 3])
                {
                    double freq = i * binWidth;
                    
                    if (freq > 150 && freq < 5000)
                    {
                        bool tooClose = false;
                        foreach (var p in peaks)
                        {
                            if (Math.Abs(p.Frequency - freq) < 250)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                        
                        if (!tooClose)
                        {
                            peaks.Add(new FormantInfo
                            {
                                Frequency = freq,
                                Bandwidth = CalculateBandwidth(spectrum, i, binWidth),
                                Amplitude = spectrum[i]
                            });
                        }
                    }
                }
            }
            
            return peaks.OrderByDescending(p => p.Amplitude).Take(4).ToList();
        }

        private double CalculateBandwidth(double[] spectrum, int peakBin, double binWidth)
        {
            double peakAmp = spectrum[peakBin];
            double threshold = peakAmp / Math.Sqrt(2);
            
            int lowerBin = peakBin;
            for (int i = peakBin; i > 0 && spectrum[i] > threshold; i--)
                lowerBin = i;
            
            int upperBin = peakBin;
            for (int i = peakBin; i < spectrum.Length - 1 && spectrum[i] > threshold; i++)
                upperBin = i;
            
            return (upperBin - lowerBin) * binWidth;
        }

        private double CalculateConfidence(List<FormantInfo> formants, double rms)
        {
            double confidence = 0;
            
            if (formants.Count >= 2)
                confidence += 0.3;
            if (formants.Count >= 3)
                confidence += 0.2;
            
            bool f1Valid = formants.Any(f => f.Frequency >= 250 && f.Frequency <= 900);
            bool f2Valid = formants.Any(f => f.Frequency >= 800 && f.Frequency <= 2800);
            
            if (f1Valid) confidence += 0.25;
            if (f2Valid) confidence += 0.25;
            
            if (rms > 0.02) confidence += 0.1;
            
            return Math.Min(1.0, confidence);
        }

        private void UpdateHistory(double f1, double f2, double f3)
        {
            _f1History[_historyIndex] = f1;
            _f2History[_historyIndex] = f2;
            _f3History[_historyIndex] = f3;
            _historyIndex = (_historyIndex + 1) % _smoothingWindow;
        }
    }

    #endregion

    /// <summary>
    /// Resultat av formantanalyse for en frame.
    /// </summary>
    public class FormantAnalysisResult
    {
        public DateTime Timestamp { get; set; }
        public double F1 { get; set; }
        public double F2 { get; set; }
        public double F3 { get; set; }
        public double SmoothedF1 { get; set; }
        public double SmoothedF2 { get; set; }
        public double SmoothedF3 { get; set; }
        public double FrameRms { get; set; }
        public double ResidualEnergy { get; set; }
        public double Confidence { get; set; }
        public bool IsValid { get; set; }
        public double SpectralCentroid { get; set; }
    }

    /// <summary>
    /// Informasjon om en enkelt formant.
    /// </summary>
    public class FormantInfo
    {
        public double Frequency { get; set; }
        public double Bandwidth { get; set; }
        public double Amplitude { get; set; }
    }
}
