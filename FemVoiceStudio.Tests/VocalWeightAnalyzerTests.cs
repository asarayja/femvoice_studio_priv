using System;
using System.Collections.Generic;
using FemVoiceStudio.Audio;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Tester for <see cref="VocalWeightAnalyzer"/> — vokalvekt-dimensjonen.
    ///
    /// Kjernekontrakt som verifiseres:
    ///   • Monotonisitet: høyere spektral centroid / F1 ⇒ lettere stemme ⇒ HØYERE score.
    ///   • Klinisk retning: lett stemme i Light-båndet (75-100), tung i Heavy-båndet (0-45).
    ///   • Klemming til 0-100 ved ekstreme inndata.
    ///   • Robusthet: degenerert inndata (0/NaN/Inf) ⇒ nøytral midt-score + forklaring.
    ///   • Kategori-grenser (Light/Medium/Heavy).
    ///   • Session-aggregering snitter robust og gir samme resultattype.
    ///   • Pitch er ikke en parameter (ingen pitch-proxy).
    /// </summary>
    public class VocalWeightAnalyzerTests
    {
        private static VocalWeightAnalyzer NewAnalyzer() => new VocalWeightAnalyzer();

        // Representative inndata for hver vekt-klasse.
        private static VocalWeightResult ScoreLight(VocalWeightAnalyzer a) =>
            a.Score(f1Hz: 700, spectralCentroidHz: 2400, hnrDb: 22, intensity: 0.15);

        private static VocalWeightResult ScoreHeavy(VocalWeightAnalyzer a) =>
            a.Score(f1Hz: 380, spectralCentroidHz: 1100, hnrDb: 8, intensity: 0.70);

        // ──────────────────────────────────────────────────────────────────
        // Monotonisitet
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_LightVoice_ScoresHigherThanHeavyVoice()
        {
            var a = NewAnalyzer();
            var light = ScoreLight(a);
            var heavy = ScoreHeavy(a);

            Assert.True(light.Score > heavy.Score,
                $"Lett stemme skal score høyere enn tung (lett={light.Score:F1}, tung={heavy.Score:F1})");
        }

        [Theory]
        [InlineData(1100, 1400)]
        [InlineData(1400, 1800)]
        [InlineData(1800, 2300)]
        public void Score_HigherCentroid_GivesHigherScore(double lowCentroid, double highCentroid)
        {
            var a = NewAnalyzer();
            // Hold alt annet likt; bare centroid endres.
            var low = a.Score(f1Hz: 500, spectralCentroidHz: lowCentroid, hnrDb: 15, intensity: 0.3);
            var high = a.Score(f1Hz: 500, spectralCentroidHz: highCentroid, hnrDb: 15, intensity: 0.3);

            Assert.True(high.Score > low.Score,
                $"Høyere centroid skal gi høyere score (low={low.Score:F1}, high={high.Score:F1})");
        }

        [Theory]
        [InlineData(380, 500)]
        [InlineData(500, 650)]
        [InlineData(650, 740)]
        public void Score_HigherF1_GivesHigherScore(double lowF1, double highF1)
        {
            var a = NewAnalyzer();
            var low = a.Score(f1Hz: lowF1, spectralCentroidHz: 1700, hnrDb: 15, intensity: 0.3);
            var high = a.Score(f1Hz: highF1, spectralCentroidHz: 1700, hnrDb: 15, intensity: 0.3);

            Assert.True(high.Score > low.Score,
                $"Høyere F1 skal gi høyere score (low={low.Score:F1}, high={high.Score:F1})");
        }

        [Theory]
        [InlineData(0.15, 0.45)]
        [InlineData(0.30, 0.70)]
        public void Score_HigherIntensity_GivesLowerScore(double lowIntensity, double highIntensity)
        {
            // Validering-funn: intensitet-aksen var byttet om. Hold alle andre akser like
            // og varier KUN intensitet — høyere trykk skal gi LAVERE vokalvekt-score
            // (tyngre/mindre feminisert), per den dokumenterte inverterte aksen.
            var a = NewAnalyzer();
            var lowPressure = a.Score(f1Hz: 500, spectralCentroidHz: 1700, hnrDb: 15, intensity: lowIntensity);
            var highPressure = a.Score(f1Hz: 500, spectralCentroidHz: 1700, hnrDb: 15, intensity: highIntensity);

            Assert.True(lowPressure.Score > highPressure.Score,
                $"Lavere trykk skal gi høyere score (lavt={lowPressure.Score:F2}, høyt={highPressure.Score:F2})");
        }

        // ──────────────────────────────────────────────────────────────────
        // Klinisk retning / score-bånd
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_LightVoice_IsCategorizedLight_InUpperBand()
        {
            var a = NewAnalyzer();
            var light = ScoreLight(a);

            Assert.Equal(WeightCategory.Light, light.Category);
            Assert.InRange(light.Score, 75.0, 100.0);
        }

        [Fact]
        public void Score_HeavyVoice_IsCategorizedHeavy_InLowerBand()
        {
            var a = NewAnalyzer();
            var heavy = ScoreHeavy(a);

            Assert.Equal(WeightCategory.Heavy, heavy.Category);
            Assert.InRange(heavy.Score, 0.0, 45.0);
        }

        [Fact]
        public void Score_MediumVoice_IsCategorizedMedium_InMiddleBand()
        {
            var a = NewAnalyzer();
            // Midt mellom ankerpunktene på alle akser.
            var mid = a.Score(f1Hz: 550, spectralCentroidHz: 1800, hnrDb: 15, intensity: 0.45);

            Assert.Equal(WeightCategory.Medium, mid.Category);
            Assert.InRange(mid.Score, 45.0, 75.0);
        }

        // ──────────────────────────────────────────────────────────────────
        // Klemming til 0-100
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_ExtremeLightInputs_ClampsToHundred()
        {
            var a = NewAnalyzer();
            // Langt over alle høy-ankere.
            var r = a.Score(f1Hz: 2000, spectralCentroidHz: 6000, hnrDb: 60, intensity: 0.05);

            Assert.InRange(r.Score, 0.0, 100.0);
            Assert.True(r.Score > 99.0, $"Ekstrem lett input skal klemmes mot 100 (fikk {r.Score:F2})");
            Assert.Equal(WeightCategory.Light, r.Category);
        }

        [Fact]
        public void Score_ExtremeHeavyInputs_ClampsToZero()
        {
            var a = NewAnalyzer();
            // Langt under alle lav-ankere; svært høyt trykk.
            var r = a.Score(f1Hz: 50, spectralCentroidHz: 200, hnrDb: 0.5, intensity: 1.0);

            Assert.InRange(r.Score, 0.0, 100.0);
            Assert.True(r.Score < 1.0, $"Ekstrem tung input skal klemmes mot 0 (fikk {r.Score:F2})");
            Assert.Equal(WeightCategory.Heavy, r.Category);
        }

        // ──────────────────────────────────────────────────────────────────
        // Robusthet: degenerert inndata
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_AllZeroInputs_ReturnsNeutralMidScore_WithExplanation()
        {
            var a = NewAnalyzer();
            var r = a.Score(f1Hz: 0, spectralCentroidHz: 0, hnrDb: 0, intensity: 0);

            Assert.Equal(50.0, r.Score, 3);
            Assert.Equal(WeightCategory.Medium, r.Category);
            Assert.Contains("Utilstrekkelig signal", r.Explanation, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Score_NaNPrimarySignals_ReturnsNeutralMidScore_WithExplanation()
        {
            var a = NewAnalyzer();
            var r = a.Score(f1Hz: double.NaN, spectralCentroidHz: double.NaN, hnrDb: 18, intensity: 0.3);

            Assert.Equal(50.0, r.Score, 3);
            Assert.Equal(WeightCategory.Medium, r.Category);
            Assert.Contains("Utilstrekkelig signal", r.Explanation, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Score_InfinityPrimarySignals_ReturnsNeutralMidScore()
        {
            var a = NewAnalyzer();
            var r = a.Score(
                f1Hz: double.PositiveInfinity,
                spectralCentroidHz: double.NegativeInfinity,
                hnrDb: 18, intensity: 0.3);

            Assert.Equal(50.0, r.Score, 3);
            Assert.Equal(WeightCategory.Medium, r.Category);
        }

        [Fact]
        public void Score_OnePrimarySignalUsable_StillProducesScore()
        {
            var a = NewAnalyzer();
            // F1 mangler (NaN), men centroid er god ⇒ skal fortsatt skåre, ikke nøytral-fallback.
            var r = a.Score(f1Hz: double.NaN, spectralCentroidHz: 2400, hnrDb: 22, intensity: 0.15);

            Assert.DoesNotContain("Utilstrekkelig signal", r.Explanation, StringComparison.OrdinalIgnoreCase);
            Assert.True(r.Score > 75.0, $"Høy centroid alene skal gi lett score (fikk {r.Score:F1})");
        }

        // ──────────────────────────────────────────────────────────────────
        // Sporbarhet og forklaring
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void Score_CarriesRawInputs_ForTraceability()
        {
            var a = NewAnalyzer();
            var r = a.Score(f1Hz: 620, spectralCentroidHz: 1950, hnrDb: 17, intensity: 0.25);

            Assert.Equal(620, r.RawInputs["F1Hz"]);
            Assert.Equal(1950, r.RawInputs["SpectralCentroidHz"]);
            Assert.Equal(17, r.RawInputs["HnrDb"]);
            Assert.Equal(0.25, r.RawInputs["Intensity"]);
        }

        [Fact]
        public void Score_ExplanationIsNonEmpty()
        {
            var a = NewAnalyzer();
            var r = ScoreLight(a);
            Assert.False(string.IsNullOrWhiteSpace(r.Explanation));
        }

        // ──────────────────────────────────────────────────────────────────
        // Session-aggregering
        // ──────────────────────────────────────────────────────────────────

        [Fact]
        public void ScoreSession_LightFrames_GivesLightResult()
        {
            var a = NewAnalyzer();
            var frames = new List<(double, double, double, double)>
            {
                (700, 2400, 22, 0.15),
                (690, 2380, 21, 0.16),
                (710, 2420, 23, 0.14),
            };

            var r = a.ScoreSession(frames);

            Assert.Equal(WeightCategory.Light, r.Category);
            Assert.InRange(r.Score, 75.0, 100.0);
        }

        [Fact]
        public void ScoreSession_RobustToOutlierFrames()
        {
            var a = NewAnalyzer();
            // Overveiende tung økt med to ekstreme lette utliggere; trimmet snitt skal holde Heavy.
            var frames = new List<(double, double, double, double)>
            {
                (380, 1100, 8, 0.70),
                (390, 1120, 9, 0.68),
                (370, 1080, 7, 0.72),
                (385, 1110, 8, 0.69),
                (395, 1130, 9, 0.67),
                (740, 5000, 40, 0.10), // utligger
                (745, 5200, 42, 0.10), // utligger
            };

            var r = a.ScoreSession(frames);

            Assert.Equal(WeightCategory.Heavy, r.Category);
        }

        [Fact]
        public void ScoreSession_EmptySequence_ReturnsNeutralMidScore_WithExplanation()
        {
            var a = NewAnalyzer();
            var r = a.ScoreSession(new List<(double, double, double, double)>());

            Assert.Equal(50.0, r.Score, 3);
            Assert.Equal(WeightCategory.Medium, r.Category);
            Assert.Contains("Utilstrekkelig signal", r.Explanation, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ScoreSession_AllDegenerateFrames_ReturnsNeutralMidScore()
        {
            var a = NewAnalyzer();
            var frames = new List<(double, double, double, double)>
            {
                (0, 0, 0, 0),
                (double.NaN, double.NaN, double.NaN, double.NaN),
            };

            var r = a.ScoreSession(frames);

            Assert.Equal(50.0, r.Score, 3);
            Assert.Equal(WeightCategory.Medium, r.Category);
            Assert.Contains("Utilstrekkelig signal", r.Explanation, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ScoreSession_NullSequence_ReturnsNeutralMidScore()
        {
            var a = NewAnalyzer();
            var r = a.ScoreSession(null!);

            Assert.Equal(50.0, r.Score, 3);
            Assert.Equal(WeightCategory.Medium, r.Category);
        }

        [Fact]
        public void ScoreSession_MeanOfFramesMatchesDirectScoreOfMean()
        {
            var a = NewAnalyzer();
            // To identiske frames ⇒ snittet er identisk med selve frame-en ⇒ samme score
            // som et direkte Score()-kall på de samme verdiene.
            var frames = new List<(double, double, double, double)>
            {
                (600, 1900, 16, 0.30),
                (600, 1900, 16, 0.30),
            };

            var session = a.ScoreSession(frames);
            var direct = a.Score(600, 1900, 16, 0.30);

            Assert.Equal(direct.Score, session.Score, 3);
            Assert.Equal(direct.Category, session.Category);
        }
    }
}
