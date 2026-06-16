using System;
using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class ResonanceProxyEngineTests
    {
        [Fact]
        public void ProcessSamples_AllowsVoicedFrame_WhenRawRmsAboveThreshold()
        {
            // Arrange
            var fftSize = 1024;
            var engine = new ResonanceProxyEngine(sampleRate: 48000, fftSize: fftSize);
            // Lower threshold for deterministic test environment
            engine.RmsThreshold = 0.001;
            engine.Start();
            // Provide a fallback resonance and voiced context to allow conservative acceptance
            engine.LastKnownPitchIsVoiced = true;
            engine.LastKnownFallbackResonance = 1.0;

            // Generate a voiced-like sine wave with amplitude that yields RMS above threshold
            float amplitude = 0.02f; // RMS ~ 0.02 / sqrt(2) ~ 0.014
            var samples = new float[fftSize];
            // Composite tones near the engine's fallback formant frequencies to
            // increase the chance of formant detection/confidence in headless tests.
            double f1 = 350.0;
            double f2 = 2000.0;
            for (int i = 0; i < fftSize; i++)
            {
                samples[i] = (float)(amplitude * (Math.Sin(2 * Math.PI * f1 * i / 48000.0) + 0.6 * Math.Sin(2 * Math.PI * f2 * i / 48000.0)));
            }

            // Act
            bool accepted = false;
            engine.FormantsUpdated += _ => accepted = true;
            engine.ProcessSamples(samples);

            // Assert — at least one frame should be accepted (FormantsUpdated event fired)
            Assert.True(engine.ResonanceCalledCount > 0, "Engine should have been called for at least one frame.");
            Assert.True(accepted || engine.ResonanceAcceptedCount > 0,
                $"Engine should accept at least one voiced frame when raw RMS is above threshold. Called={engine.ResonanceCalledCount}, Accepted={engine.ResonanceAcceptedCount}, Rejected={engine.ResonanceRejectedCount}, LastReason={engine.ResonanceLastRejectionReason}");
        }
    }
}
