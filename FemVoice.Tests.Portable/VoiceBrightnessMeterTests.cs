using System;
using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// Proves the brightness meter is RESPONSIVE and MONOTONIC (the property that matters — the old engine score was
    /// frozen), using synthetic sine tones where the true centroid is known. Calibration constants may be tuned to
    /// real voices, but these invariants must hold regardless.
    /// </summary>
    public class VoiceBrightnessMeterTests
    {
        private const int SampleRate = 48000;

        private static float[] Sine(double freqHz, int samples = 4096, double amplitude = 0.5)
        {
            var buf = new float[samples];
            for (int i = 0; i < samples; i++)
                buf[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * freqHz * i / SampleRate));
            return buf;
        }

        [Fact]
        public void Centroid_OfPureTone_IsNearThatTone()
        {
            // A pure tone's power-weighted centroid sits at (just around) its own frequency.
            double c = VoiceBrightnessMeter.SpectralCentroidHz(Sine(1500), SampleRate);
            Assert.InRange(c, 1300, 1700);
        }

        [Fact]
        public void Brightness_IsMonotonic_AcrossTones()
        {
            // Brighter (higher-frequency) input must never read lower. This is the invariant the old score violated.
            double[] tones = { 200, 400, 700, 1000, 1400, 1800, 2400, 3000 };
            int prev = -1;
            foreach (var f in tones)
            {
                int pct = VoiceBrightnessMeter.BrightnessPercent(Sine(f), SampleRate);
                Assert.True(pct >= prev, $"brightness dropped at {f} Hz: {pct} < {prev}");
                prev = pct;
            }
        }

        [Fact]
        public void Brightness_DarkTone_ReadsLow_BrightTone_ReadsHigh()
        {
            int dark = VoiceBrightnessMeter.BrightnessPercent(Sine(200), SampleRate);
            int bright = VoiceBrightnessMeter.BrightnessPercent(Sine(2600), SampleRate);
            Assert.True(dark <= 5, $"dark tone should read near 0, was {dark}");
            Assert.True(bright >= 95, $"bright tone should read near 100, was {bright}");
            Assert.True(bright - dark > 60, "meter must span most of its range between dark and bright");
        }

        [Fact]
        public void Brightness_IsLoudnessIndependent()
        {
            // Same tone at very different amplitudes → same brightness (it is a spectral RATIO, not a level).
            int quiet = VoiceBrightnessMeter.BrightnessPercent(Sine(1400, amplitude: 0.02), SampleRate);
            int loud = VoiceBrightnessMeter.BrightnessPercent(Sine(1400, amplitude: 0.9), SampleRate);
            Assert.True(Math.Abs(quiet - loud) <= 2, $"brightness moved with loudness: quiet={quiet} loud={loud}");
        }

        [Fact]
        public void Brightness_Silence_And_TooShort_ReadZero()
        {
            Assert.Equal(0, VoiceBrightnessMeter.BrightnessPercent(new float[4096], SampleRate));
            Assert.Equal(0, VoiceBrightnessMeter.BrightnessPercent(new float[16], SampleRate));
            Assert.Equal(0, VoiceBrightnessMeter.BrightnessPercent(Array.Empty<float>(), SampleRate));
        }

        [Fact]
        public void Calibrated_ScaleIsAnchoredToTheBaseline()
        {
            // With a per-user baseline B, a tone AT the baseline reads low (relaxed voice ≈ bottom) and a tone ~900 Hz
            // BRIGHTER than the baseline reads high — regardless of the absolute Hz, which is the whole point (mic-robust).
            double baseline = VoiceBrightnessMeter.SpectralCentroidHz(Sine(900), SampleRate);
            int atBaseline = VoiceBrightnessMeter.BrightnessPercent(Sine(900), SampleRate, baseline);
            int brighter = VoiceBrightnessMeter.BrightnessPercent(Sine(1800), SampleRate, baseline);
            Assert.True(atBaseline <= 20, $"relaxed voice (at baseline) should read low, was {atBaseline}");
            Assert.True(brighter >= 80, $"a ~900 Hz-brighter voice should read high, was {brighter}");
            Assert.True(brighter - atBaseline > 55, "calibrated scale must span most of its range across the training gap");
        }

        [Fact]
        public void Calibrated_ShiftsTheScale_SoTheSameToneReadsDifferently()
        {
            // The same input read against a LOWER baseline (darker habitual voice) reads BRIGHTER than against a higher
            // baseline — i.e. the meter measures improvement RELATIVE to the user, not an absolute Hz.
            int againstLowBaseline = VoiceBrightnessMeter.BrightnessPercent(Sine(1400), SampleRate, baselineCentroidHz: 700);
            int againstHighBaseline = VoiceBrightnessMeter.BrightnessPercent(Sine(1400), SampleRate, baselineCentroidHz: 1200);
            Assert.True(againstLowBaseline > againstHighBaseline,
                $"same tone should read brighter vs a lower baseline: low={againstLowBaseline} high={againstHighBaseline}");
        }

        [Fact]
        public void MedianCentroid_IgnoresSilentFrames_AndReturnsMiddle()
        {
            var frames = new[] { Sine(600), new float[4096], Sine(1000), Sine(1400) };
            double median = VoiceBrightnessMeter.MedianCentroidHz(frames, SampleRate);
            // Silent frame dropped → median of {~600, ~1000, ~1400} ≈ 1000.
            Assert.InRange(median, 850, 1150);
            Assert.Equal(0, VoiceBrightnessMeter.MedianCentroidHz(new[] { new float[4096] }, SampleRate));
            Assert.Equal(0, VoiceBrightnessMeter.MedianCentroidHz(Array.Empty<float[]>(), SampleRate));
        }
    }
}
