using System;
using System.Linq;
using System.Reflection;
using FemVoiceStudio.Audio;
using FemVoiceStudio.Models;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Style-aware resonance scoring tests (Agent G — Goal Safety / stil-bevisst
    /// resonansscoring).
    ///
    /// ROTPROBLEM (spec forbyr «universal female target»): ResonanceProxyEngine og
    /// ResonansScoringService scoret tidligere ALLTID mot et lyst/fremre feminint
    /// klangideal og tok aldri inn brukerens <see cref="VoiceStyleGoal"/>. En
    /// DarkFeminine-bruker ble dermed presset mot feminin klang i selve feedback-
    /// RETNINGEN. Disse testene verifiserer at:
    ///   (a) Feminine/default gir EKSAKT de historiske konstantene (numerisk),
    ///   (b) DarkFeminine gir lavere centroid/F2-mål enn Feminine,
    ///   (c) GITT IDENTISKE rå formanter scorer en mørkere klang HØYERE under
    ///       DarkFeminine enn under Feminine — dvs. stilen flytter scoringsRETNINGEN,
    ///   (d) en vakt-test som feiler hvis motoren/scoringen igjen får et stil-uavhengig
    ///       hardkodet formant-optimum (målene MÅ variere med stil).
    /// </summary>
    public class ResonanceStyleTargetTests
    {
        // Historiske konstanter — MÅ være den eksakte Feminine/neutral-defaulten.
        private const double EngineF1 = 320.0, EngineF2 = 2300.0, EngineF3 = 2900.0;
        private const double EngineSpacing = 1900.0, EngineCentroid = 2500.0;
        private const double ScoringF1 = 330.0, ScoringF2 = 2200.0, ScoringCentroid = 2500.0;
        private const double ScoringF1Min = 280.0, ScoringF1Max = 450.0;
        private const double ScoringF2Min = 1800.0, ScoringF2Max = 2600.0;

        // ──────────────────────────────────────────────────────────────────────
        // (a) Feminine/default == historiske konstanter (numerisk, eksakt)
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Engine_Feminine_MatchesHistoricalConstantsExactly()
        {
            var t = ResonanceStyleTarget.ForEngine(VoiceStyleGoal.Feminine);
            Assert.Equal(EngineF1, t.F1Optimal, 6);
            Assert.Equal(EngineF2, t.F2Optimal, 6);
            Assert.Equal(EngineF3, t.F3Optimal, 6);
            Assert.Equal(EngineSpacing, t.SpacingOptimal, 6);
            Assert.Equal(EngineCentroid, t.CentroidOptimal, 6);
        }

        [Fact]
        public void Scoring_Feminine_MatchesHistoricalConstantsExactly()
        {
            var t = ResonanceStyleTarget.ForScoring(VoiceStyleGoal.Feminine);
            Assert.Equal(ScoringF1, t.F1Optimal, 6);
            Assert.Equal(ScoringF2, t.F2Optimal, 6);
            Assert.Equal(ScoringCentroid, t.CentroidOptimal, 6);
            Assert.Equal(ScoringF1Min, t.F1Min, 6);
            Assert.Equal(ScoringF1Max, t.F1Max, 6);
            Assert.Equal(ScoringF2Min, t.F2Min, 6);
            Assert.Equal(ScoringF2Max, t.F2Max, 6);
        }

        [Theory]
        [InlineData(VoiceStyleGoal.Situational)]
        [InlineData(VoiceStyleGoal.Custom)]
        public void Engine_NeutralFallbackStyles_MatchFeminineExactly(VoiceStyleGoal style)
        {
            // Situational/Custom (og default) MÅ være identiske med Feminine — ingen
            // atferdsendring for det vanlige tilfellet.
            var fem = ResonanceStyleTarget.ForEngine(VoiceStyleGoal.Feminine);
            var t = ResonanceStyleTarget.ForEngine(style);
            Assert.Equal(fem.F1Optimal, t.F1Optimal, 6);
            Assert.Equal(fem.F2Optimal, t.F2Optimal, 6);
            Assert.Equal(fem.F3Optimal, t.F3Optimal, 6);
            Assert.Equal(fem.SpacingOptimal, t.SpacingOptimal, 6);
            Assert.Equal(fem.CentroidOptimal, t.CentroidOptimal, 6);
        }

        [Fact]
        public void Engine_DefaultProperty_IsFeminineHistorical()
        {
            // En fersk motor scorer mot de historiske konstantene (default = Feminine).
            using var engine = new ResonanceProxyEngine();
            Assert.Equal(VoiceStyleGoal.Feminine, engine.VoiceStyle);
        }

        [Fact]
        public void Scoring_DefaultProperty_IsFeminineHistorical()
        {
            var svc = new ResonansScoringService(enableSmoothing: false);
            Assert.Equal(VoiceStyleGoal.Feminine, svc.VoiceStyle);
        }

        // ──────────────────────────────────────────────────────────────────────
        // (b) DarkFeminine gir lavere centroid/F2-mål (mindre fremre/lys)
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Engine_DarkFeminine_HasLowerF2AndCentroidThanFeminine()
        {
            var fem = ResonanceStyleTarget.ForEngine(VoiceStyleGoal.Feminine);
            var dark = ResonanceStyleTarget.ForEngine(VoiceStyleGoal.DarkFeminine);
            Assert.True(dark.F2Optimal < fem.F2Optimal, "DarkFeminine F2-mål skal være lavere.");
            Assert.True(dark.F3Optimal < fem.F3Optimal, "DarkFeminine F3-mål skal være lavere.");
            Assert.True(dark.CentroidOptimal < fem.CentroidOptimal, "DarkFeminine centroid-mål skal være lavere.");
            Assert.True(dark.SpacingOptimal < fem.SpacingOptimal, "DarkFeminine spacing-mål skal være lavere.");
        }

        [Fact]
        public void Scoring_DarkFeminine_HasLowerF2AndCentroidThanFeminine()
        {
            var fem = ResonanceStyleTarget.ForScoring(VoiceStyleGoal.Feminine);
            var dark = ResonanceStyleTarget.ForScoring(VoiceStyleGoal.DarkFeminine);
            Assert.True(dark.F2Optimal < fem.F2Optimal, "DarkFeminine F2-mål skal være lavere.");
            Assert.True(dark.CentroidOptimal < fem.CentroidOptimal, "DarkFeminine centroid-mål skal være lavere.");
            Assert.True(dark.F2Max < fem.F2Max, "DarkFeminine F2-bånd skal være skjøvet ned.");
        }

        [Fact]
        public void Androgynous_IsMilderThanDarkFeminine_ForBothConsumers()
        {
            // Androgynous = mild senking; DarkFeminine sterkere. Verifiser ordningen
            // Feminine > Androgynous > DarkFeminine på F2/centroid for begge konsumenter.
            var femE = ResonanceStyleTarget.ForEngine(VoiceStyleGoal.Feminine);
            var andE = ResonanceStyleTarget.ForEngine(VoiceStyleGoal.Androgynous);
            var darkE = ResonanceStyleTarget.ForEngine(VoiceStyleGoal.DarkFeminine);
            Assert.True(femE.F2Optimal > andE.F2Optimal && andE.F2Optimal > darkE.F2Optimal);
            Assert.True(femE.CentroidOptimal > andE.CentroidOptimal && andE.CentroidOptimal > darkE.CentroidOptimal);

            var femS = ResonanceStyleTarget.ForScoring(VoiceStyleGoal.Feminine);
            var andS = ResonanceStyleTarget.ForScoring(VoiceStyleGoal.Androgynous);
            var darkS = ResonanceStyleTarget.ForScoring(VoiceStyleGoal.DarkFeminine);
            Assert.True(femS.F2Optimal > andS.F2Optimal && andS.F2Optimal > darkS.F2Optimal);
            Assert.True(femS.CentroidOptimal > andS.CentroidOptimal && andS.CentroidOptimal > darkS.CentroidOptimal);
        }

        // ──────────────────────────────────────────────────────────────────────
        // (c) Identiske rå formanter: en MØRKERE klang scorer HØYERE under
        //     DarkFeminine enn under Feminine — stilen flytter retningen, ikke bare
        //     terskelen.
        // ──────────────────────────────────────────────────────────────────────

        private static FormantAnalysisResult DarkTimbre() => new FormantAnalysisResult
        {
            // Mørkere/mer tilbaketrukket klang: lavere F2/F3 enn det lyse feminine idealet,
            // men fortsatt en gyldig stemt frame.
            Timestamp = DateTime.UtcNow,
            F1 = 330, F2 = 1950, F3 = 2650,
            SmoothedF1 = 330, SmoothedF2 = 1950, SmoothedF3 = 2650,
            FrameRms = 0.1,
            Confidence = 0.9,
            IsValid = true
        };

        [Fact]
        public void Scoring_DarkTimbre_ScoresHigherUnderDarkFeminineThanFeminine()
        {
            // Glatting AV for en deterministisk én-frame-sammenligning.
            var feminine = new ResonansScoringService(enableSmoothing: false);
            var dark = new ResonansScoringService(enableSmoothing: false);
            dark.SetVoiceStyle(VoiceStyleGoal.DarkFeminine);

            double femScore = feminine.EvaluateResonance(DarkTimbre()).TotalScore;
            double darkScore = dark.EvaluateResonance(DarkTimbre()).TotalScore;

            // Stilen MÅ faktisk endre scoringsretningen, ikke bare terskelen:
            // den mørkere klangen skal score høyere når målet er DarkFeminine.
            Assert.True(darkScore > femScore,
                $"DarkFeminine-score ({darkScore:F2}) skal være høyere enn Feminine-score ({femScore:F2}) for en mørk klang.");
        }

        [Fact]
        public void Scoring_SetVoiceStyle_ChangesCentroidTargetAndScore()
        {
            // Samme service, samme frame: bytt stil og se at scoren endres (ikke konstant).
            var svc = new ResonansScoringService(enableSmoothing: false);
            double feminineScore = svc.EvaluateResonance(DarkTimbre()).TotalScore;

            svc.SetVoiceStyle(VoiceStyleGoal.DarkFeminine);
            Assert.Equal(VoiceStyleGoal.DarkFeminine, svc.VoiceStyle);
            double darkScore = svc.EvaluateResonance(DarkTimbre()).TotalScore;

            Assert.True(darkScore > feminineScore,
                "Å bytte til DarkFeminine skal heve scoren for en mørk klang.");
        }

        [Fact]
        public void Scoring_DarkFeminine_DoesNotPenalizeDarkTimbreBelowFeminine()
        {
            // Svakere variant av (c): aldri LAVERE. Holder selv om deltaer justeres.
            var feminine = new ResonansScoringService(enableSmoothing: false);
            var dark = new ResonansScoringService(enableSmoothing: false);
            dark.SetVoiceStyle(VoiceStyleGoal.DarkFeminine);

            double femScore = feminine.EvaluateResonance(DarkTimbre()).TotalScore;
            double darkScore = dark.EvaluateResonance(DarkTimbre()).TotalScore;
            Assert.True(darkScore >= femScore);
        }

        // ──────────────────────────────────────────────────────────────────────
        // (d) Vakt-test: motoren/scoringen MÅ IKKE igjen få et stil-uavhengig
        //     hardkodet formant-optimum. Hvis noen reintroduserer en universell
        //     hardkodet `const ... TargetF2Optimal`/`TargetCentroid` på en av
        //     konsumentene (i stedet for det stil-avhengige målet), skal dette feile.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Guard_EngineTargets_VaryWithStyle_NotHardcoded()
        {
            // Reflekter: F2/F3/centroid/spacing-optima MÅ variere med stil. Hvis de er
            // identiske på tvers av Feminine og DarkFeminine, er målet hardkodet igjen.
            var fem = ResonanceStyleTarget.ForEngine(VoiceStyleGoal.Feminine);
            var dark = ResonanceStyleTarget.ForEngine(VoiceStyleGoal.DarkFeminine);
            Assert.NotEqual(fem.F2Optimal, dark.F2Optimal);
            Assert.NotEqual(fem.CentroidOptimal, dark.CentroidOptimal);
        }

        [Fact]
        public void Guard_ScoringConsumerHasNoStyleIndependentFormantConstants()
        {
            // ResonansScoringService skal IKKE lenger ha private const-felt for
            // F1/F2/centroid-optima eller F1/F2-bånd — disse MÅ komme fra det stil-
            // avhengige ResonanceStyleTarget. Et gjeninnført hardkodet optimum vil
            // dukke opp her og feile testen.
            var consts = typeof(ResonansScoringService)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(double))
                .Select(f => f.Name)
                .ToList();

            string[] forbidden =
            {
                "TargetF1Optimal", "TargetF2Optimal", "TargetSpectralCentroid",
                "TargetF1Min", "TargetF1Max", "TargetF2Min", "TargetF2Max"
            };

            foreach (var name in forbidden)
                Assert.False(consts.Contains(name),
                    $"'{name}' er gjeninnført som stil-uavhengig const — formant-optima MÅ komme fra ResonanceStyleTarget.");
        }

        [Fact]
        public void Guard_EngineConsumerHasNoStyleIndependentFormantConstants()
        {
            var consts = typeof(ResonanceProxyEngine)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(double))
                .Select(f => f.Name)
                .ToList();

            string[] forbidden =
            {
                "TargetF1Optimal", "TargetF2Optimal", "TargetF3Optimal",
                "TargetSpacingOptimal", "TargetCentroidOptimal"
            };

            foreach (var name in forbidden)
                Assert.False(consts.Contains(name),
                    $"'{name}' er gjeninnført som stil-uavhengig const i ResonanceProxyEngine.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Bakoverkompatibilitet: en Feminine/default-frame gir samme score uansett
        // om stilen settes eksplisitt eller ikke (ingen skjult atferdsendring).
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Scoring_ExplicitFeminine_EqualsImplicitDefault()
        {
            var implicitDefault = new ResonansScoringService(enableSmoothing: false);
            var explicitFeminine = new ResonansScoringService(enableSmoothing: false);
            explicitFeminine.SetVoiceStyle(VoiceStyleGoal.Feminine);

            double a = implicitDefault.EvaluateResonance(DarkTimbre()).TotalScore;
            double b = explicitFeminine.EvaluateResonance(DarkTimbre()).TotalScore;
            Assert.Equal(a, b, 6);
        }
    }
}
