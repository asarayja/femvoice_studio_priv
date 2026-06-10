using System;
using System.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// Pitch detection service som implementerer YIN-algoritmen og autocorrelation.
    /// YIN er valgt fordi den er mer robust mot støy og harmoniske.
    /// </summary>
    public class PitchDetectionService
    {
        private readonly int _sampleRate;
        private readonly double _minFrequency;
        private readonly double _maxFrequency;
        
        // YIN spesifikke parametere
        private readonly double _threshold = 0.15;
        public double VoicedRmsThreshold { get; set; } = 0.01;
        
        public PitchDetectionService(int sampleRate = 44100, double minFrequency = 80, double maxFrequency = 500, double voicedRmsThreshold = 0.01)
        {
            _sampleRate = sampleRate;
            _minFrequency = minFrequency;
            _maxFrequency = maxFrequency;
            VoicedRmsThreshold = voicedRmsThreshold;
        }
        
        /// <summary>
        /// Detekterer pitch fra et array med audio samples ved hjelp av YIN-algoritmen.
        /// </summary>
        /// <param name="samples">Normaliserte audio samples (-1.0 til 1.0)</param>
        /// <returns>PitchAnalysisResult med pitch-verdi og metadata</returns>
        public PitchAnalysisResult DetectPitch(float[] samples)
        {
            double rms = CalculateRms(samples);
            var result = new PitchAnalysisResult
            {
                Timestamp = DateTime.Now,
                RmsValue = rms,
                Intensity = rms
            };
            
            // Sjekk om det er nok volum til å analysere
            // Conservative early reject only on extreme low RMS; otherwise allow detector
            if (result.RmsValue <= 0)
            {
                result.IsVoiced = false;
                result.Pitch = 0;
                result.Confidence = 0;
                return result;
            }

            // Run YIN first; combine RMS and detector confidence for acceptance.
            var yinResult = YinPitchDetection(samples);
            if (yinResult.frequency > 0 && yinResult.probability > _threshold)
            {
                // Accept if RMS meets threshold or the detector confidence is high enough
                if (result.RmsValue >= VoicedRmsThreshold * 0.6 || yinResult.probability > 0.80)
                {
                    result.IsVoiced = true;
                    result.Pitch = yinResult.frequency;
                    result.Confidence = yinResult.probability;
                    return result;
                }
            }

            // Try autocorrelation as backup; accept if strong correlation or RMS reasonably high
            var autocorrResult = AutocorrelationPitchDetection(samples);
            if (autocorrResult.frequency > 0 && (autocorrResult.probability > 0.75 || result.RmsValue >= VoicedRmsThreshold * 0.8))
            {
                result.IsVoiced = true;
                result.Pitch = autocorrResult.frequency;
                result.Confidence = autocorrResult.probability;
                // If accepted despite being below the calibrated voiced threshold, record RC-0 diagnostic with variance
                if (result.RmsValue < VoicedRmsThreshold)
                {
                    try
                    {
                        Rc0RuntimeLog.Write("PitchDetection",
                            $"AcceptedBelowVoicedThreshold: RMS={result.RmsValue:F6}, VoiceThreshold={VoicedRmsThreshold:F6}, Confidence={result.Confidence:F3}, Variance={autocorrResult.variance:E6}, Reason=AUTOCORR_HIGH_CONFIDENCE");
                    }
                    catch { }
                }
                return result;
            }

            // Otherwise mark unvoiced
            result.IsVoiced = false;
            result.Pitch = 0;
            result.Confidence = 0;
            return result;
            
            return result;
        }
        
        /// <summary>
        /// YIN pitch detection algoritme - god for tale
        /// </summary>
        private (double frequency, double probability) YinPitchDetection(float[] samples)
        {
            int bufferSize = samples.Length;
            double[] yinBuffer = new double[bufferSize / 2];
            
            // Step 1: Compute difference function
            for (int tau = 0; tau < yinBuffer.Length; tau++)
            {
                yinBuffer[tau] = 0;
                for (int i = 0; i < bufferSize / 2; i++)
                {
                    float delta = samples[i] - samples[i + tau];
                    yinBuffer[tau] += delta * delta;
                }
            }
            
            // Step 2: Cumulative mean normalized difference
            yinBuffer[0] = 1;
            double runningSum = 0;
            for (int tau = 1; tau < yinBuffer.Length; tau++)
            {
                runningSum += yinBuffer[tau];
                yinBuffer[tau] *= tau / runningSum;
            }
            
            // Step 3: Absolute threshold
            int tauEstimate = -1;
            for (int tau = 2; tau < yinBuffer.Length; tau++)
            {
                if (yinBuffer[tau] < _threshold)
                {
                    while (tau + 1 < yinBuffer.Length && yinBuffer[tau + 1] < yinBuffer[tau])
                    {
                        tau++;
                    }
                    tauEstimate = tau;
                    break;
                }
            }
            
            if (tauEstimate == -1)
            {
                // No pitch found
                return (0, 0);
            }
            
            // Step 4: Parabolic interpolation for better precision
            double betterTau;
            if (tauEstimate < 1 || tauEstimate >= yinBuffer.Length - 1)
            {
                betterTau = tauEstimate;
            }
            else
            {
                double s0 = yinBuffer[tauEstimate - 1];
                double s1 = yinBuffer[tauEstimate];
                double s2 = yinBuffer[tauEstimate + 1];
                betterTau = tauEstimate + (s2 - s0) / (2 * (2 * s1 - s2 - s0));
            }
            
            double frequency = _sampleRate / betterTau;
            double probability = 1 - yinBuffer[tauEstimate];
            
            // Begrens til gyldig frekvensområde
            if (frequency < _minFrequency || frequency > _maxFrequency)
            {
                return (0, probability);
            }
            
            return (frequency, probability);
        }
        
        /// <summary>
        /// Autocorrelation pitch detection - backup metode
        /// </summary>
        private (double frequency, double probability, double variance) AutocorrelationPitchDetection(float[] samples)
        {
            int minLag = _sampleRate / (int)_maxFrequency;
            int maxLag = _sampleRate / (int)_minFrequency;

            double maxCorrelation = 0;
            int bestLag = 0;
            double computedVariance = 0;

            // Quick DC / variance check: subtract mean and compute variance to reject near-constant frames
            double mean = 0;
            for (int i = 0; i < samples.Length; i++) mean += samples[i];
            mean /= Math.Max(1, samples.Length);
            double varSum = 0;
            for (int i = 0; i < samples.Length; i++) { varSum += (samples[i] - mean) * (samples[i] - mean); }
            computedVariance = varSum / Math.Max(1, samples.Length);
            // If variance is vanishingly small, reject early as constant/DC
            if (computedVariance < 1e-10)
            {
                return (0, 0, computedVariance);
            }

            // Beregn autocorrelation using zero-mean signals to avoid DC bias
            for (int lag = minLag; lag < maxLag && lag < samples.Length / 2; lag++)
            {
                double correlation = 0;
                double norm1 = 0;
                double norm2 = 0;

                for (int i = 0; i < samples.Length - lag; i++)
                {
                    double x = samples[i] - mean;
                    double y = samples[i + lag] - mean;
                    correlation += x * y;
                    norm1 += x * x;
                    norm2 += y * y;
                }

                if (norm1 > 0 && norm2 > 0)
                {
                    correlation /= Math.Sqrt(norm1 * norm2);
                }

                if (correlation > maxCorrelation)
                {
                    maxCorrelation = correlation;
                    bestLag = lag;
                }
            }

            if (maxCorrelation > 0.5 && bestLag > 0)
            {
                double frequency = (double)_sampleRate / bestLag;
                return (frequency, maxCorrelation, computedVariance);
            }

            return (0, 0, computedVariance);
        }
        
        /// <summary>
        /// Beregner RMS-verdi (Root Mean Square) for volum-måling
        /// </summary>
        private double CalculateRms(float[] samples)
        {
            if (samples.Length == 0)
                return 0;
                
            double sum = 0;
            foreach (var sample in samples)
            {
                sum += sample * sample;
            }
            return Math.Sqrt(sum / samples.Length);
        }
        
        /// <summary>
        /// Beregner pitch-variasjon (standard avvik) fra en liste med pitch-verdier
        /// </summary>
        public static double CalculatePitchVariation(double[] pitches)
        {
            if (pitches.Length < 2)
                return 0;
                
            var validPitches = pitches.Where(p => p > 0).ToArray();
            if (validPitches.Length < 2)
                return 0;
                
            double avg = validPitches.Average();
            double sumSquares = validPitches.Sum(p => (p - avg) * (p - avg));
            return Math.Sqrt(sumSquares / validPitches.Length);
        }
        
        /// <summary>
        /// Beregner gjennomsnittlig pitch fra en liste
        /// </summary>
        public static double CalculateAveragePitch(double[] pitches)
        {
            var validPitches = pitches.Where(p => p > 0).ToArray();
            if (validPitches.Length == 0)
                return 0;
            return validPitches.Average();
        }
        
        /// <summary>
        /// Analyserer intonasjonsmønster - sjekker for stigende/fallende intonasjon
        /// </summary>
        public static IntonationAnalysis AnalyzeIntonation(double[] pitches, double[] intensities)
        {
            var analysis = new IntonationAnalysis();

            try
            {
                if (pitches == null || pitches.Length == 0)
                {
                    analysis.FailureReason = "INSUFFICIENT_PITCH_DATA";
                    Rc0RuntimeLog.Write("PitchDetection", "AnalyzeIntonation: no pitch data (INSUFFICIENT_PITCH_DATA)");
                    return analysis;
                }

                if (intensities == null || intensities.Length == 0)
                {
                    // we allow pitch-based analysis even without intensities, but record reason
                    analysis.FailureReason = "INSUFFICIENT_INTENSITY_DATA";
                    Rc0RuntimeLog.Write("PitchDetection", "AnalyzeIntonation: intensity array missing (INSUFFICIENT_INTENSITY_DATA)");
                    // continue with pitch-only analysis
                }

                if (intensities != null && pitches.Length != intensities.Length)
                {
                    analysis.FailureReason = "MISMATCHED_SIGNAL_ARRAY_LENGTHS";
                    Rc0RuntimeLog.Write("PitchDetection", $"AnalyzeIntonation: mismatched arrays: pitches={pitches.Length}, intensities={(intensities==null?0:intensities.Length)}");
                    // prefer to operate on the minimum length
                    int minLen = Math.Min(pitches.Length, intensities?.Length ?? pitches.Length);
                    pitches = pitches.Take(minLen).ToArray();
                    if (intensities != null) intensities = intensities.Take(minLen).ToArray();
                }

                var validPitches = pitches.Where(p => p > 0).ToArray();
                if (validPitches.Length < 2)
                {
                    analysis.FailureReason ??= "INSUFFICIENT_PITCH_DATA";
                    Rc0RuntimeLog.Write("PitchDetection", "AnalyzeIntonation: too few valid pitch samples (INSUFFICIENT_PITCH_DATA)");
                    return analysis;
                }

                // Sjekk for stigende intonasjon (spørsmål)
                int risingCount = 0;
                int windowSize = Math.Max(1, validPitches.Length / 4);

                for (int i = 0; i <= validPitches.Length - 2 * windowSize; i++)
                {
                    double startAvg = 0, endAvg = 0;
                    for (int j = 0; j < windowSize; j++)
                    {
                        startAvg += validPitches[i + j];
                        endAvg += validPitches[i + windowSize + j];
                    }
                    startAvg /= windowSize;
                    endAvg /= windowSize;

                    if (endAvg > startAvg * 1.05) // 5% økning
                        risingCount++;
                }

                analysis.RisingIntonationRatio = (double)risingCount / Math.Max(1, (validPitches.Length - 2 * windowSize + 1));

                // Sjekk pitch range
                double max = validPitches.Max();
                double min = validPitches.Min();
                if (min <= 0 || double.IsInfinity(max / min) || double.IsNaN(max / min))
                {
                    analysis.PitchRange = 0;
                    analysis.PitchRangeSemitones = 0;
                }
                else
                {
                    analysis.PitchRange = max - min;
                    analysis.PitchRangeSemitones = 12 * Math.Log2(max / min);
                }

                return analysis;
            }
            catch (Exception ex)
            {
                // Never throw — log and return neutral analysis
                try { Rc0RuntimeLog.Write("PitchDetection", $"AnalyzeIntonation exception: {ex}"); } catch { }
                analysis.FailureReason ??= "EMPTY_SESSION_ANALYSIS";
                return analysis;
            }
        }
    }
    
    public class IntonationAnalysis
    {
        public double RisingIntonationRatio { get; set; }
        public double PitchRange { get; set; }
        public double PitchRangeSemitones { get; set; }
        // If analysis could not be computed, this explains why (INSUFFICIENT_PITCH_DATA, etc.)
        public string? FailureReason { get; set; }
    }
}
