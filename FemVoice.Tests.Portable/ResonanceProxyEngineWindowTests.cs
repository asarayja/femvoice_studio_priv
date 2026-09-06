using System;
using System.Collections.Generic;
using System.Threading;
using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Regression guard for a defect that silently blinded the resonance/analyzer screens in BOTH heads: Reset() zeroed
    /// the constant Hann analysis window, and Start() calls Reset(). ProcessFrame multiplies every sample by that
    /// window, so after Start() the FFT saw pure silence.
    ///
    /// The failure was INVISIBLE to a naive test: the RMS gate reads the RAW buffer, so frames were still "accepted"
    /// and FormantsUpdated still fired — it just fired with an empty spectrum, whereupon the code substitutes the FIXED
    /// fallback formants (350/2000/2800) and the score freezes. So these tests assert on the SPECTRUM itself
    /// (SpectralCentroid) and on the formants differing from that fallback triple, using tones deliberately far from
    /// the fallback values.
    ///
    /// The engine posts its events through a captured SynchronizationContext when one exists (xunit installs one), so
    /// the tests clear it while constructing the engine to make event delivery synchronous and deterministic.
    /// </summary>
    public class ResonanceProxyEngineWindowTests
    {
        private const int SampleRate = 48000;
        private const int FftSize = 1024;

        // Deliberately NOT near the 350/2000/2800 fallback, so a fallback result is distinguishable from real analysis.
        private const double ToneLow = 700.0;
        private const double ToneHigh = 1500.0;

        private static float[] VoicedFrame(int n, double amplitude = 0.05)
        {
            var buf = new float[n];
            for (int i = 0; i < n; i++)
                buf[i] = (float)(amplitude * (Math.Sin(2 * Math.PI * ToneLow * i / SampleRate)
                                            + 0.8 * Math.Sin(2 * Math.PI * ToneHigh * i / SampleRate)));
            return buf;
        }

        /// <summary>Run a voiced signal through a freshly started engine and return every snapshot it emitted.</summary>
        private static List<FormantSnapshot> AnalyseVoicedSignal(int frames = 8, bool resetMidway = false)
        {
            var seen = new List<FormantSnapshot>();
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);   // → engine raises events synchronously
            try
            {
                var engine = new ResonanceProxyEngine(SampleRate, FftSize) { RmsThreshold = 0.001 };
                engine.LastKnownPitchIsVoiced = true;
                engine.LastKnownFallbackResonance = 1.0;
                engine.FormantsUpdated += s => seen.Add(s);
                engine.Start();                                       // Start() → Reset(): the window must survive
                for (int f = 0; f < frames; f++) engine.ProcessSamples(VoicedFrame(FftSize));
                if (resetMidway)
                {
                    seen.Clear();
                    engine.Reset();                                   // e.g. restarting a session
                    engine.Start();
                    for (int f = 0; f < frames; f++) engine.ProcessSamples(VoicedFrame(FftSize));
                }
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            return seen;
        }

        private static double MaxCentroid(List<FormantSnapshot> seen)
        {
            double best = 0;
            foreach (var s in seen) if (s.SpectralCentroid > best) best = s.SpectralCentroid;
            return best;
        }

        private static bool IsFixedFallback(FormantSnapshot s)
            => Math.Abs(s.F1 - 350) < 0.5 && Math.Abs(s.F2 - 2000) < 0.5 && Math.Abs(s.F3 - 2800) < 0.5;

        [Fact]
        public void AfterStart_TheSpectrumIsRealAndNotSilence()
        {
            var seen = AnalyseVoicedSignal();
            Assert.NotEmpty(seen);
            // A zeroed analysis window makes every FFT bin 0, so the centroid collapses to 0.
            double best = MaxCentroid(seen);
            Assert.True(best > 0,
                "Spectral centroid was 0 for a clearly voiced signal — the analysis window was zeroed, so the FFT saw silence.");
        }

        [Fact]
        public void AfterStart_FormantsComeFromTheSignal_NotTheFixedFallback()
        {
            var seen = AnalyseVoicedSignal();
            Assert.NotEmpty(seen);
            Assert.Contains(seen, s => !IsFixedFallback(s));
        }

        [Fact]
        public void ExplicitReset_MidSession_DoesNotBlindTheEngine()
        {
            var seen = AnalyseVoicedSignal(resetMidway: true);
            Assert.NotEmpty(seen);
            Assert.True(MaxCentroid(seen) > 0,
                "Spectral centroid was 0 after an explicit Reset() — the window must be regenerated, not cleared.");
        }
    }
}
