using System;
using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Regresjonstester for formant-LPC-kjeden. Bakgrunn (runtime-feil):
    /// Levinson-Durbin-rekursjonen hadde feil fortegn på autokorrelasjons-summen
    /// og oppdaterte koeffisientene in-place, og LPC-orden 12 ble kjørt direkte
    /// på 44100 Hz — resultatet var et formantløst spektrum, IsValid=false på
    /// hver eneste frame, og «--» i hele resonansanalysen. Testene bruker
    /// syntetiske vokaler (impulstog gjennom andreordens resonatorer) med kjente
    /// formantmål, slik at kjeden verifiseres ende-til-ende uten mikrofon.
    /// </summary>
    public class FormantDetectionServiceTests
    {
        private const int SampleRate = 44100;
        private const int BufferSize = 1024;   // samme som AudioCaptureService

        // ──────────────────────────────────────────────────────────────────
        // Syntetisk vokal: impulstog (f0) gjennom tre resonatorer (F1-F3)
        // ──────────────────────────────────────────────────────────────────
        private static float[] SynthesizeVowel(
            double f0, double[] formants, double[] bandwidths, double targetRms = 0.05)
        {
            int total = BufferSize * 4;   // la resonatorene nå steady state
            var source = new double[total];
            int period = (int)(SampleRate / f0);
            for (int i = 0; i < total; i += period)
                source[i] = 1.0;

            var output = source;
            for (int k = 0; k < formants.Length; k++)
            {
                double r = Math.Exp(-Math.PI * bandwidths[k] / SampleRate);
                double theta = 2 * Math.PI * formants[k] / SampleRate;
                double a1 = -2 * r * Math.Cos(theta);
                double a2 = r * r;

                var y = new double[total];
                for (int i = 0; i < total; i++)
                {
                    y[i] = output[i];
                    if (i >= 1) y[i] -= a1 * y[i - 1];
                    if (i >= 2) y[i] -= a2 * y[i - 2];
                }
                output = y;
            }

            // Siste buffer, skalert til mål-RMS
            double sumSq = 0;
            for (int i = total - BufferSize; i < total; i++)
                sumSq += output[i] * output[i];
            double scale = targetRms / Math.Sqrt(sumSq / BufferSize);

            var result = new float[BufferSize];
            for (int i = 0; i < BufferSize; i++)
                result[i] = (float)(output[total - BufferSize + i] * scale);
            return result;
        }

        private static FormantAnalysisResult Analyze(double f0, double[] formants)
        {
            var detector = new FormantDetectionService(SampleRate, 25, 10, 12);
            var samples = SynthesizeVowel(f0, formants, new[] { 80.0, 100.0, 140.0 });
            return detector.ExtractFormants(samples);
        }

        [Theory]
        [InlineData(120, 700, 1200, 2600)]   // maskulin /a/
        [InlineData(220, 800, 1400, 2800)]   // feminin /a/
        [InlineData(170, 500, 1800, 2500)]   // nøytral /e/
        [InlineData(200, 650, 1900, 2700)]   // feminin /æ/
        public void ExtractFormants_SyntheticVowel_IsValidWithFormantsNearTargets(
            double f0, double f1Target, double f2Target, double f3Target)
        {
            var result = Analyze(f0, new[] { f1Target, f2Target, f3Target });

            Assert.True(result.IsValid,
                $"Frame skulle vært gyldig (conf={result.Confidence:F2}, F1={result.F1:F0}, F2={result.F2:F0})");

            // ±20 % toleranse: enkel resonator-syntese + grov peak-binning (FFT 2048)
            Assert.InRange(result.F1, f1Target * 0.8, f1Target * 1.2);
            Assert.InRange(result.F2, f2Target * 0.8, f2Target * 1.2);
        }

        [Fact]
        public void ExtractFormants_FrontVowel_HighF2Detected()
        {
            // Feminin /i/ — lav F1, høy F2; kjernescenarioet for fremre resonans
            var result = Analyze(220, new[] { 350.0, 2300.0, 3000.0 });

            Assert.True(result.IsValid);
            Assert.True(result.F2 > 1800,
                $"Fremre vokal skal gi høy F2 (fikk {result.F2:F0} Hz)");
        }

        [Fact]
        public void ExtractFormants_Silence_IsInvalid()
        {
            var detector = new FormantDetectionService(SampleRate, 25, 10, 12);
            var silence = new float[BufferSize];   // RMS 0 < terskel

            var result = detector.ExtractFormants(silence);

            Assert.False(result.IsValid);
            Assert.Equal(0, result.Confidence);
        }

        [Fact]
        public void ExtractFormants_LpcCoefficientsAreBounded()
        {
            // Fortegns-/in-place-buggen ga koeffisienter på ~±60; friske
            // LPC-koeffisienter for tale er O(1). Vokter mot regresjon.
            var detector = new FormantDetectionService(SampleRate, 25, 10, 12);
            var samples = SynthesizeVowel(220, new[] { 800.0, 1400.0, 2800.0 }, new[] { 80.0, 100.0, 140.0 });

            detector.ExtractFormants(samples);

            foreach (var coefficient in detector.LastLpcCoefficients)
                Assert.InRange(Math.Abs(coefficient), 0, 10);
        }
    }
}
