using System;
using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoice.Tests.Portable
{
    /// <summary>
    /// Pins the design decision behind the perceived-voice mirror: its resonance cue is the ABSOLUTE brightness scale,
    /// never the per-user calibrated one.
    ///
    /// The calibrated scale is defined as "how much brighter than MY relaxed voice", so a user's own habitual voice
    /// reads near zero BY DEFINITION. Feeding that into the gender estimate capped the combined score below the
    /// Feminine threshold — making that band structurally unreachable however bright the voice actually was — and meant
    /// that merely calibrating could move a voice DOWN a band. "Feminine" has to mean the same thing for everyone, so
    /// the mirror is absolute; the calibrated scale stays where it belongs, on the training meter (progress vs. self).
    /// </summary>
    public class PerceptionAbsoluteScaleTests
    {
        private const int SampleRate = 44100;

        /// <summary>A bright voiced-like signal: a 200 Hz fundamental carrying strong upper harmonics.</summary>
        private static float[] BrightVoice(int n = 4096)
        {
            var buf = new float[n];
            double[] partials = { 200, 400, 600, 800, 1000, 1400 };
            double[] gains = { 0.30, 0.45, 0.60, 0.70, 0.65, 0.45 };
            for (int i = 0; i < n; i++)
            {
                double v = 0;
                for (int p = 0; p < partials.Length; p++)
                    v += gains[p] * Math.Sin(2 * Math.PI * partials[p] * i / SampleRate);
                buf[i] = (float)(0.12 * v);
            }
            return buf;
        }

        [Fact]
        public void CalibratingAgainstYourOwnVoice_PinsTheCalibratedScaleNearZero()
        {
            var voice = BrightVoice();
            double ownCentroid = VoiceBrightnessMeter.SpectralCentroidHz(voice, SampleRate);
            Assert.True(ownCentroid > 0, "test signal produced no spectrum");

            int calibrated = VoiceBrightnessMeter.BrightnessPercent(voice, SampleRate, ownCentroid);
            int absolute = VoiceBrightnessMeter.BrightnessPercent(voice, SampleRate, baselineCentroidHz: null);

            // By construction the calibrated reading of your OWN baseline voice sits at the bottom of its scale…
            Assert.True(calibrated <= 20, $"calibrated reading of the baseline voice was {calibrated}");
            // …while the absolute reading reflects that this voice is genuinely bright.
            Assert.True(absolute > calibrated,
                $"absolute ({absolute}) should exceed the self-referential calibrated reading ({calibrated})");
        }

        [Fact]
        public void TheCalibratedScale_WouldMakeTheFeminineBandUnreachable()
        {
            // Even at the top of the pitch scale, a self-referential resonance reading cannot reach Feminine.
            var capped = VoicePerceptionEstimator.Estimate(pitchHz: 300, brightnessPercent: 10);
            Assert.Equal(100, capped.PitchScore);
            Assert.True(capped.Score < VoicePerceptionEstimator.FeminineThreshold,
                $"score {capped.Score} should be below the Feminine threshold on a self-referential resonance reading");
            Assert.NotEqual(VoicePerceptionBand.Feminine, capped.Band);

            // On an absolute scale the same voice can reach it.
            var reachable = VoicePerceptionEstimator.Estimate(pitchHz: 300, brightnessPercent: 40);
            Assert.Equal(VoicePerceptionBand.Feminine, reachable.Band);
        }

        [Fact]
        public void SameVoice_ReadsAtLeastAsFeminine_WhetherOrNotTheUserHasCalibrated()
        {
            // The user-visible guarantee: calibrating must never move you DOWN a band.
            var voice = BrightVoice();
            double ownCentroid = VoiceBrightnessMeter.SpectralCentroidHz(voice, SampleRate);

            int absolute = VoiceBrightnessMeter.BrightnessPercent(voice, SampleRate, baselineCentroidHz: null);
            int calibrated = VoiceBrightnessMeter.BrightnessPercent(voice, SampleRate, ownCentroid);

            var mirrorNow = VoicePerceptionEstimator.Estimate(200, absolute);        // what the app feeds today
            var mirrorIfCalibrated = VoicePerceptionEstimator.Estimate(200, calibrated);   // the old, wrong behaviour

            Assert.True(mirrorNow.Score >= mirrorIfCalibrated.Score,
                "the mirror must not score a voice lower just because the user calibrated their microphone");
        }
    }
}
