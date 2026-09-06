using System;
using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// The per-user resonance baseline is measured from a calibration phase that also contains whatever happened
    /// BEFORE the user started speaking. Without a voicing gate a late start stored the room's spectrum as the user's
    /// voice baseline — and room noise is broadband, so its centroid sits far above speech, which then pinned the live
    /// resonance meter near 0 permanently. These tests drive exactly that recording shape.
    /// </summary>
    public class VoicedBaselineGateTests
    {
        private const int SampleRate = 44100;
        private const int FrameSize = 2048;
        private const double Gate = 0.0025;

        /// <summary>Broadband, deterministic "room noise" — no RNG, so the test is reproducible.</summary>
        private static void FillNoise(float[] buf, int from, int count, double amplitude)
        {
            for (int i = 0; i < count; i++)
            {
                // Deterministic pseudo-noise: sum of mutually inharmonic high partials.
                double t = (from + i) / (double)SampleRate;
                double v = Math.Sin(2 * Math.PI * 3300 * t) + Math.Sin(2 * Math.PI * 4700 * t) + Math.Sin(2 * Math.PI * 6100 * t);
                buf[from + i] = (float)(amplitude * v / 3.0);
            }
        }

        /// <summary>A dark, clearly-voiced speech-like signal (low fundamental with a couple of low harmonics).</summary>
        private static void FillVoice(float[] buf, int from, int count, double amplitude)
        {
            for (int i = 0; i < count; i++)
            {
                double t = (from + i) / (double)SampleRate;
                double v = Math.Sin(2 * Math.PI * 180 * t) + 0.6 * Math.Sin(2 * Math.PI * 360 * t);
                buf[from + i] = (float)(amplitude * v / 1.6);
            }
        }

        [Fact]
        public void LateStart_DoesNotLetRoomNoiseDominateTheBaseline()
        {
            // 30 frames of quiet room noise, then 30 frames of real (dark) voice.
            int frames = 60, half = 30;
            var samples = new float[frames * FrameSize];
            FillNoise(samples, 0, half * FrameSize, amplitude: 0.0008);              // below the gate
            FillVoice(samples, half * FrameSize, half * FrameSize, amplitude: 0.09); // clearly above it

            double gated = VoiceBrightnessMeter.MedianVoicedCentroidHz(samples, SampleRate, Gate);
            double ungated = VoiceBrightnessMeter.SpectralCentroidHz(samples, SampleRate);

            Assert.True(gated > 0, "a recording with plenty of real voice must yield a baseline");
            // The voice is dark (180 Hz fundamental); the noise is bright. The gated baseline must follow the VOICE.
            Assert.True(gated < 1000, $"gated baseline {gated:F0} Hz looks like room noise, not the dark voice");
            Assert.True(gated < ungated,
                $"gating did not pull the baseline away from the bright noise (gated {gated:F0} vs ungated {ungated:F0})");
        }

        [Fact]
        public void MostlySilence_StoresNothingRatherThanAWrongBaseline()
        {
            // Only a couple of voiced frames — not enough to trust.
            var samples = new float[40 * FrameSize];
            FillNoise(samples, 0, 38 * FrameSize, amplitude: 0.0008);
            FillVoice(samples, 38 * FrameSize, 2 * FrameSize, amplitude: 0.09);

            double baseline = VoiceBrightnessMeter.MedianVoicedCentroidHz(samples, SampleRate, Gate);

            Assert.Equal(0, baseline);   // 0 → caller stores nothing → meter keeps its honest fixed anchors
        }

        [Fact]
        public void EnoughVoice_ProducesABaselineNearTheVoicesOwnCentroid()
        {
            var samples = new float[40 * FrameSize];
            FillVoice(samples, 0, samples.Length, amplitude: 0.09);

            double baseline = VoiceBrightnessMeter.MedianVoicedCentroidHz(samples, SampleRate, Gate);
            var oneFrame = new float[FrameSize];
            Array.Copy(samples, oneFrame, FrameSize);
            double direct = VoiceBrightnessMeter.SpectralCentroidHz(oneFrame, SampleRate);

            Assert.True(baseline > 0);
            Assert.InRange(baseline, direct * 0.8, direct * 1.2);
        }

        [Fact]
        public void ShortOrEmptyRecording_IsRejected()
        {
            Assert.Equal(0, VoiceBrightnessMeter.MedianVoicedCentroidHz(Array.Empty<float>(), SampleRate, Gate));
            Assert.Equal(0, VoiceBrightnessMeter.MedianVoicedCentroidHz(new float[100], SampleRate, Gate));
        }
    }
}
