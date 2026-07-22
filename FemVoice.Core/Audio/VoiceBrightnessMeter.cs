using System;
using NAudio.Dsp;

namespace FemVoiceStudio.Audio
{
    /// <summary>
    /// A robust, MONOTONIC brightness meter for the live resonance readout (Avalonia dashboard) and for judging
    /// resonance-focused exercises.
    ///
    /// WHY THIS EXISTS: <see cref="ResonanceProxyEngine"/> emits a "closeness to a feminine target" score built from
    /// formant peak detection. On real desktop mics the peak detection frequently fails and falls back to fixed
    /// formant values, which FREEZES the formant/spacing terms of the score — so the readout gets stuck low
    /// ("always Mørk") and barely moves with the voice. That makes a resonance-focused exercise's target band
    /// unreachable no matter how the user brightens their voice.
    ///
    /// This meter sidesteps that: it computes a proper POWER-WEIGHTED SPECTRAL CENTROID (Σ f·|X|² / Σ |X|²) over the
    /// voice band, which is
    ///   • magnitude-independent (a ratio — louder ≠ brighter), and
    ///   • monotonic with brightness (a pure tone at frequency f reads a centroid ≈ f; adding high-frequency energy
    ///     always raises it),
    /// then maps the centroid (Hz) linearly onto a 0–100 "brightness percent". Deterministic and pure, so it is fully
    /// unit- and smoke-testable with synthetic dark/bright signals (see VoiceBrightnessMeterTests).
    ///
    /// NOTE ON CALIBRATION: <see cref="CentroidFloorHz"/>/<see cref="CentroidCeilHz"/> map the plausible speaking-voice
    /// centroid range onto 0–100. They are a documented best-guess and the ONLY part that benefits from real-voice
    /// tuning; the responsiveness/monotonicity above does not depend on them.
    /// </summary>
    public static class VoiceBrightnessMeter
    {
        // Centroid (Hz) → percent. At/below floor reads 0; at/above ceil reads 100; linear between. Chosen so a
        // darker/relaxed voice lands low and a brighter/forward (feminine) resonance lands high, with headroom both
        // ways. Power weighting keeps the fundamental from dragging the value to a stuck constant.
        private const double CentroidFloorHz = 500.0;
        private const double CentroidCeilHz  = 1800.0;

        // Voice band for the centroid. Excludes sub-80 Hz rumble/DC and >5 kHz hiss that would bias the measure.
        private const double MinBandHz = 80.0;
        private const double MaxBandHz = 5000.0;

        // Largest FFT we window into; frames are typically 1024–4096 samples. Power-of-two, capped for cost.
        private const int MaxFftSize = 4096;
        private const int MinFftSize = 64;

        /// <summary>0–100 brightness percent for a captured mono frame. 0 when silent/too short (caller gates on voicing).</summary>
        public static int BrightnessPercent(float[] samples, int sampleRate)
        {
            double centroid = SpectralCentroidHz(samples, sampleRate);
            if (centroid <= 0) return 0;
            double pct = (centroid - CentroidFloorHz) / (CentroidCeilHz - CentroidFloorHz) * 100.0;
            return (int)Math.Round(Math.Clamp(pct, 0.0, 100.0));
        }

        /// <summary>
        /// Power-weighted spectral centroid (Hz) over the voice band. Magnitude-independent and monotonic with
        /// brightness. Returns 0 for silence or a frame too short to transform.
        /// </summary>
        public static double SpectralCentroidHz(float[] samples, int sampleRate)
        {
            if (samples is null || sampleRate <= 0) return 0;
            int n = 1;
            while (n * 2 <= samples.Length && n * 2 <= MaxFftSize) n *= 2;   // largest power-of-two ≤ length, capped
            if (n < MinFftSize) return 0;

            var buffer = new Complex[n];
            for (int i = 0; i < n; i++)
            {
                double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1)));   // Hann window reduces leakage
                buffer[i].X = (float)(samples[i] * hann);
                buffer[i].Y = 0f;
            }
            FastFourierTransform.FFT(true, (int)Math.Log2(n), buffer);

            int half = n / 2;
            double freqResolution = (double)sampleRate / n;
            int minBin = Math.Max(1, (int)(MinBandHz / freqResolution));
            int maxBin = Math.Min(half - 1, (int)(MaxBandHz / freqResolution));

            double weighted = 0.0, energy = 0.0;
            for (int i = minBin; i <= maxBin; i++)
            {
                double mag = Math.Sqrt(buffer[i].X * buffer[i].X + buffer[i].Y * buffer[i].Y);
                double power = mag * mag;                    // power weighting: robust to noise, emphasises real tones
                weighted += (i * freqResolution) * power;
                energy += power;
            }
            return energy > 1e-12 ? weighted / energy : 0.0;
        }
    }
}
