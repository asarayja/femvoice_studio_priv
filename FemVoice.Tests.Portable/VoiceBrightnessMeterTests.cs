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
    }
}
