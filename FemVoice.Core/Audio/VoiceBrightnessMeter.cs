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
    /// CALIBRATION (important): absolute centroid Hz is NOT portable across microphones — low-frequency roll-off,
    /// presence boost, proximity effect and codec/AGC each shift it by hundreds of Hz, which is comparable to the whole
    /// masculine→feminine spread. So the robust path is PER-USER calibration: measure the centroid of the user's RELAXED
    /// voice once (during mic calibration) and scale relative to it — see the <paramref name="baselineCentroidHz"/>
    /// overload. The fixed <see cref="CentroidFloorHz"/>/<see cref="CentroidCeilHz"/> anchors are only a provisional
    /// fallback for an un-calibrated user; they are power-weighted-centroid anchors (f0 + F1 dominate |X|², so real
    /// voiced-speech centroids run well BELOW music-timbre "brightness" numbers), not measured on any specific mic.
    /// The responsiveness/monotonicity above does not depend on either calibration.
    /// </summary>
    public static class VoiceBrightnessMeter
    {
        // Fixed fallback anchors (un-calibrated user). Centroid (Hz) → percent: at/below floor reads 0; at/above ceil
        // reads 100; linear between. Power weighting keeps the fundamental from dragging the value to a stuck constant.
        private const double CentroidFloorHz = 450.0;
        private const double CentroidCeilHz  = 1700.0;

        // Per-user calibrated mapping: floor = baseline − margin (relaxed voice reads a little above 0, not pinned),
        // ceil = baseline + offset (a brightened/forward resonance reaches the top). Span ≈ 1000 Hz of centroid rise.
        private const double CalibMarginHz = 100.0;
        private const double CalibCeilOffsetHz = 900.0;

        // Voice band for the centroid. Low edge at 130 Hz trims sub-fundamental rumble/DC/handling noise and reduces the
        // fundamental's double-count without materially moving the anchors; >5 kHz hiss excluded.
        private const double MinBandHz = 130.0;
        private const double MaxBandHz = 5000.0;

        // Largest FFT we window into; frames are typically 1024–4096 samples. Power-of-two, capped for cost.
        private const int MaxFftSize = 4096;
        private const int MinFftSize = 64;

        /// <summary>
        /// 0–100 brightness percent for a captured mono frame. 0 when silent/too short (caller gates on voicing). When
        /// <paramref name="baselineCentroidHz"/> is a valid calibrated relaxed-voice centroid, the scale is anchored to
        /// it (mic-robust); otherwise the fixed provisional anchors are used.
        /// </summary>
        public static int BrightnessPercent(float[] samples, int sampleRate, double? baselineCentroidHz = null)
        {
            double centroid = SpectralCentroidHz(samples, sampleRate);
            if (centroid <= 0) return 0;
            double floor, ceil;
            if (baselineCentroidHz is > 0)
            {
                floor = baselineCentroidHz.Value - CalibMarginHz;
                ceil = baselineCentroidHz.Value + CalibCeilOffsetHz;
            }
            else
            {
                floor = CentroidFloorHz;
                ceil = CentroidCeilHz;
            }
            double pct = (centroid - floor) / (ceil - floor) * 100.0;
            return (int)Math.Round(Math.Clamp(pct, 0.0, 100.0));
        }

        /// <summary>
        /// Robust relaxed-voice baseline for per-user calibration: the MEDIAN spectral centroid (Hz) across the given
        /// frames, ignoring frames that read 0 (silent/too short). Median (not mean) resists the occasional bright
        /// sibilant or dropout. Returns 0 when nothing usable is present.
        /// </summary>
        public static double MedianCentroidHz(System.Collections.Generic.IReadOnlyList<float[]> frames, int sampleRate)
        {
            if (frames is null || frames.Count == 0) return 0;
            var centroids = new System.Collections.Generic.List<double>(frames.Count);
            foreach (var frame in frames)
            {
                double c = SpectralCentroidHz(frame, sampleRate);
                if (c > 0) centroids.Add(c);
            }
            if (centroids.Count == 0) return 0;
            centroids.Sort();
            int mid = centroids.Count / 2;
            return centroids.Count % 2 == 1 ? centroids[mid] : (centroids[mid - 1] + centroids[mid]) / 2.0;
        }

        /// <summary>
        /// Median spectral centroid (Hz) of the VOICED part of a recording — the robust way to derive a user's
        /// relaxed-voice baseline from a calibration phase.
        ///
        /// A phase recording also contains whatever happened before the user actually started speaking. Averaging over
        /// everything let a late start store the ROOM's spectrum as the voice baseline, and room noise is broadband so
        /// its centroid sits far above speech — which then pinned the live resonance meter near 0 permanently. Frames
        /// below <paramref name="rmsGate"/> (pass the calibration profile's own voiced threshold) are therefore
        /// discarded, and if fewer than <paramref name="minVoicedFrames"/> remain this returns 0 so the caller stores
        /// NOTHING: a wrong baseline is worse than no baseline, since no baseline simply means fixed anchors.
        /// </summary>
        public static double MedianVoicedCentroidHz(float[] samples, int sampleRate, double rmsGate,
            int frameSize = 2048, int minVoicedFrames = 10)
        {
            if (samples is null || sampleRate <= 0 || frameSize <= 0 || samples.Length < frameSize) return 0;
            double gate = rmsGate > 0 ? rmsGate : 0.0025;

            var voiced = new System.Collections.Generic.List<float[]>(samples.Length / frameSize);
            for (int offset = 0; offset + frameSize <= samples.Length; offset += frameSize)
            {
                double sumSquares = 0;
                for (int i = 0; i < frameSize; i++)
                {
                    double s = samples[offset + i];
                    sumSquares += s * s;
                }
                if (Math.Sqrt(sumSquares / frameSize) < gate) continue;   // silence / room noise → not the user's voice

                var frame = new float[frameSize];
                Array.Copy(samples, offset, frame, 0, frameSize);
                voiced.Add(frame);
            }

            return voiced.Count < minVoicedFrames ? 0 : MedianCentroidHz(voiced, sampleRate);
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
