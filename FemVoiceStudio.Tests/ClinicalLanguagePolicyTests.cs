using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Guards the clinical-language policy: the rule engine catches genuine
    /// shame/pressure copy, leaves legitimate clinical, educational and UI text alone,
    /// and — the load-bearing CI check — asserts that the live Strings.resx contains
    /// zero violations so the language stays calm and comfort-oriented over time.
    /// </summary>
    public class ClinicalLanguagePolicyTests
    {
        // -----------------------------------------------------------------------------
        // Rule-engine unit tests: forbidden copy is caught.
        // -----------------------------------------------------------------------------

        [Theory]
        [InlineData("press_imperative", "Press stemmen høyere!")]
        [InlineData("press_must", "Du må presse hardere for å nå tonen.")]
        [InlineData("press_too_high", "For høyt press i stemmen akkurat nå.")]
        [InlineData("push_harder", "Push harder for å treffe målet.")]
        [InlineData("hardere", "Gjør det hardere neste gang.")]
        [InlineData("tving", "Tving stemmen opp i leiet.")]
        [InlineData("tvang", "Det krever litt tvang for å komme dit.")]
        [InlineData("force", "Force the voice up to the target.")]
        [InlineData("aggressiv", "Vær mer aggressiv i ansatsen.")]
        [InlineData("daarlig_label", "Dårlig")]
        [InlineData("shame_exclaim", "Feil! Prøv på nytt.")]
        [InlineData("mislykket", "Forsøket var mislykket.")]
        [InlineData("skam", "Det er ingenting å skamme seg over her.")]
        // Deficiency-/under-standard framing (A7 false-negative round): the voice
        // measured against a norm and found lacking.
        [InlineData("under_forventning", "Under forventning")]
        [InlineData("ikke_god_nok", "Resultatet er ikke godt nok ennå.")]
        [InlineData("under_normalen", "Stemmen ligger under normalen.")]
        [InlineData("under_det_normale", "Resonansen er under det normale.")]
        // Spec-mandated gender-affirming additions (gap analysis).
        // "wrong voice" / "feil stemme" — value judgement on the voice itself.
        [InlineData("wrong_voice_en", "It sounds like the wrong voice.")]
        [InlineData("feil_stemme_no", "Du bruker feil stemme.")]
        [InlineData("feil_stemmen_no", "Dette er feil stemmen for deg.")]
        // Masculine/male voice labelling — the most gender-affirming-critical class.
        [InlineData("masculine_voice_en", "Masculine voice.")]
        [InlineData("male_voice_en", "Your male voice is showing.")]
        [InlineData("maskulin_stemme_no", "Maskulin stemme.")]
        [InlineData("mannlig_stemme_no", "En mannlig stemme.")]
        // ...detected / ...oppdaget report shape.
        [InlineData("male_voice_detected_en", "Male voice detected.")]
        [InlineData("maskulin_stemme_oppdaget_no", "Maskulin stemme oppdaget.")]
        [InlineData("mannlig_stemme_oppdaget_no", "Mannlig stemme oppdaget.")]
        // Imperative MÅL-PRESS: pushing pitch higher / høyere.
        [InlineData("go_higher_en", "Go higher to reach the target!")]
        [InlineData("aim_higher_en", "Aim higher than before.")]
        [InlineData("push_higher_en", "Push higher now.")]
        [InlineData("must_go_higher_en", "You must go higher.")]
        [InlineData("must_higher_en", "You must higher your pitch.")]
        [InlineData("gaa_hoyere_no", "Gå høyere for å nå målet.")]
        [InlineData("sikt_hoyere_no", "Sikt høyere enn forrige gang.")]
        [InlineData("press_hoyere_no", "Press stemmen høyere.")]
        [InlineData("maa_hoyere_no", "Du må høyere i tonen.")]
        [InlineData("maa_gaa_hoyere_no", "Du må gå høyere nå.")]
        // Failure / bad voice / try harder (EN).
        [InlineData("failure_en", "That attempt was a failure.")]
        [InlineData("failed_en", "You failed the exercise.")]
        [InlineData("bad_voice_en", "Such a bad voice today.")]
        [InlineData("try_harder_en", "Try harder next time.")]
        public void Scan_FlagsForbiddenCopy(string key, string value)
        {
            var violations = ClinicalLanguagePolicy.Scan(
                new[] { new KeyValuePair<string, string>(key, value) });

            Assert.NotEmpty(violations);
            Assert.All(violations, v => Assert.Equal(key, v.Key));
        }

        // -----------------------------------------------------------------------------
        // Rule-engine unit tests: legitimate copy passes (precision over breadth).
        // -----------------------------------------------------------------------------

        [Theory]
        // The explicitly mandated case: UI button instructions must NOT be flagged.
        [InlineData("button_start", "Trykk Start for å begynne")]
        [InlineData("button_measure", "Trykk måleknappen når du er klar.")]
        // Anti-pressure instructions are the GOOD message — never flag them.
        [InlineData("avoid_press_noun", "Slipp litt ned og unngå press.")]
        [InlineData("avoid_press_verb", "Bruk komfortabel lyd uten å presse stemmen.")]
        [InlineData("not_press", "Start på en komfortabel tone og glid sakte oppover. Ikke press.")]
        // Symptom observation / health awareness use "press" as a noun — legitimate.
        [InlineData("press_awareness", "Vær oppmerksom på press")]
        [InlineData("press_detected", "Press detektert - prioritér avslappet produksjon")]
        [InlineData("press_selfreport", "Jeg kjente press, sårhet eller ubehag")]
        [InlineData("press_symptom", "Lukke kjeven, press i halsen, eller tilbakefall til bakre resonans.")]
        [InlineData("press_history", "Treningsbelastningen senkes fordi historikken viser press eller tilbakegang.")]
        // Physiology: SOVT/straw technique "tvinger frem ..." is description, not coercion.
        [InlineData("sovt", "Reduser stemmeslitasje med halm-teknikk. Tvinger frem semi-okkludert vokaltrakt.")]
        // Hardware noise-gate troubleshooting — "aggressiv gate", "svak stemme".
        [InlineData("noise_gate", "Slå av aggressiv gate hvis svak stemme kuttes.")]
        // Diagnostic metric labels are neutral, not shame.
        [InlineData("metric_low", "For lav suksessrate ({0:F0}% vs {1}%)")]
        [InlineData("metric_high", "For høy belastning ({0:F0} vs maks {1})")]
        // Bare technical UI error label and formatted error messages stay allowed.
        [InlineData("ui_error", "Feil")]
        [InlineData("error_format", "Feil ved lasting av øvelser: {0}")]
        // Mic-troubleshooting "dårlig signal" is a hardware note, not a voice judgement.
        [InlineData("daarlig_signal", "Dårlig signal - sjekk mikrofon")]
        // "under" in temporal/positional senses is legitimate — only the
        // norm-comparison shapes ("under forventning/normalen") are deficiency framing.
        [InlineData("under_temporal", "Hold tonen jevn under øvelsen.")]
        [InlineData("under_position", "Slipp skuldrene ned og pust rolig under hele økten.")]
        // The 11+ existing legitimate "høyere" uses (anatomy/resonance/position/
        // education/gentle coaching) MUST keep passing — only the imperative
        // goal-pressure shape ("gå/sikt/press ... høyere", "må høyere") is forbidden.
        [InlineData("hoyere_glide", "Tren på å bevege deg jevnt fra lavere til høyere pitch. Gliding gir mer naturlig progresjon.")]
        [InlineData("hoyere_humming", "Humming forbereder stemmen på høyere pitch uten spenning.")]
        [InlineData("hoyere_overtoner", "Fremre resonans forsterker høyere overtoner. Måles ved høyere formant-frekvenser (spesielt F2).")]
        [InlineData("hoyere_oversone", "Over målsonen. Stemmen ligger høyere/lysere enn sonen. Slipp litt ned og unngå press.")]
        [InlineData("hoyere_resonans_munn", "Prøv å løfte lydresonansen litt høyere i munnen.")]
        [InlineData("hoyere_gentle_coach", "Prøv litt høyere mot {1:F0} Hz med lett, fremoverplassert lyd uten å presse.")]
        [InlineData("hoyere_f2", "Bakover resonans - flytt frem (høyere F2)")]
        [InlineData("hoyere_setninger", "Integrer høyere pitch i setninger med fokus på naturlig flyt.")]
        [InlineData("hoyere_munnresonans", "Jobb rolig med høyere munnresonans og fremre tungeposisjon.")]
        // Legitimate technical "Feil" / "Feil ved lasting" / "...feilet" — these are
        // hardware/operation errors, never a judgement on the voice. The tightly bound
        // "feil stemme" is the only forbidden shape.
        [InlineData("feil_vanlige", "Vanlige feil")]
        [InlineData("feil_feilet", "Kalibrering feilet: {0}")]
        [InlineData("feil_intonasjon", "Riktig intonasjon er nøkkelen - feil intonasjon kan avsløre stemmen.")]
        // Norwegian "å male" (to paint) prose must never be mistaken for "male voice".
        [InlineData("male_paint_no", "På fritiden liker jeg å male bilder. Noen ganger maler jeg landskap.")]
        public void Scan_AllowsLegitimateCopy(string key, string value)
        {
            var violations = ClinicalLanguagePolicy.Scan(
                new[] { new KeyValuePair<string, string>(key, value) });

            Assert.True(
                violations.Count == 0,
                $"Expected no violation for \"{value}\" but got: " +
                string.Join("; ", violations));
        }

        [Fact]
        public void Scan_IgnoresNullAndWhitespaceValues()
        {
            var violations = ClinicalLanguagePolicy.Scan(new[]
            {
                new KeyValuePair<string, string>("empty", string.Empty),
                new KeyValuePair<string, string>("whitespace", "   "),
            });

            Assert.Empty(violations);
        }

        [Fact]
        public void Scan_ReportsKeyPatternAndMatchedText()
        {
            var violations = ClinicalLanguagePolicy.Scan(
                new[] { new KeyValuePair<string, string>("k", "Tving stemmen opp.") });

            var violation = Assert.Single(violations);
            Assert.Equal("k", violation.Key);
            Assert.False(string.IsNullOrWhiteSpace(violation.Pattern));
            Assert.Equal("Tving", violation.MatchedText);
        }

        [Fact]
        public void ForbiddenPatterns_AndPreferredAlternatives_ArePopulated()
        {
            Assert.NotEmpty(ClinicalLanguagePolicy.ForbiddenPatterns);
            Assert.NotEmpty(ClinicalLanguagePolicy.EnglishForbiddenPatterns);
            Assert.NotEmpty(ClinicalLanguagePolicy.PreferredAlternatives);
            Assert.Contains("press", ClinicalLanguagePolicy.PreferredAlternatives.Keys);
            // Spec-mandated gender-affirming alternatives are wired up.
            Assert.Contains("wrong voice", ClinicalLanguagePolicy.PreferredAlternatives.Keys);
            Assert.Contains("try harder", ClinicalLanguagePolicy.PreferredAlternatives.Keys);
            // The English subset is a proper subset of the full rule set.
            Assert.True(
                ClinicalLanguagePolicy.EnglishForbiddenPatterns.Count
                    < ClinicalLanguagePolicy.ForbiddenPatterns.Count);
        }

        // -----------------------------------------------------------------------------
        // The load-bearing CI guard: every value in the canonical neutral resource
        // (Strings.resx, Norwegian) is clinically safe. We scan the neutral resource
        // only — matching the convention in ResourceTextPolicyTests, where Strings.resx
        // is treated as the authoritative source. The localized *.resx files contain
        // machine/placeholder translations (often still English fallbacks) and are
        // maintained outside this policy's scope; the Norwegian forbidden patterns would
        // produce noise against foreign-language copy.
        // -----------------------------------------------------------------------------

        [Fact]
        public void NeutralResourceValues_PassClinicalLanguagePolicy()
        {
            var resourceDirectory = FindResourceDirectory();
            var neutralResourcePath = Path.Combine(resourceDirectory, "Strings.resx");

            var document = XDocument.Load(neutralResourcePath);
            var entries = document.Root?
                .Elements("data")
                .Select(element => new KeyValuePair<string, string>(
                    (string?)element.Attribute("name") ?? string.Empty,
                    element.Element("value")?.Value ?? string.Empty))
                ?? Enumerable.Empty<KeyValuePair<string, string>>();

            var failures = ClinicalLanguagePolicy.Scan(entries);

            Assert.True(
                failures.Count == 0,
                "Clinical-language policy violations found in Strings.resx:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        // -----------------------------------------------------------------------------
        // English-resource CI guard. The gender-affirming-critical English rules
        // ("wrong/masculine/male voice", "go/aim/push/press higher", "must higher",
        // "failure"/"failed", "bad voice", "try harder") must never appear in the English
        // resource files. We scan with ScanEnglish (the English-only subset) so the
        // Norwegian-tuned rules — which legitimately fire on English UI copy such as
        // "Press start" or "without extra force" — do not produce false positives. Both
        // English resource file naming conventions are covered (Strings.en.resx and the
        // legacy Strings_en.resx), discovered via the same EnumerateFiles pattern used in
        // ResourceTextPolicyTests.
        // -----------------------------------------------------------------------------

        [Theory]
        [InlineData("Strings.en.resx")]
        [InlineData("Strings_en.resx")]
        public void EnglishResourceValues_PassEnglishClinicalLanguagePolicy(string fileName)
        {
            var resourceDirectory = FindResourceDirectory();
            var matchingFiles = Directory
                .EnumerateFiles(resourceDirectory, "*.resx")
                .Where(path => string.Equals(
                    Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // The file must exist — otherwise the guard is silently disabled.
            Assert.NotEmpty(matchingFiles);

            foreach (var file in matchingFiles)
            {
                var document = XDocument.Load(file);
                var entries = document.Root?
                    .Elements("data")
                    .Select(element => new KeyValuePair<string, string>(
                        (string?)element.Attribute("name") ?? string.Empty,
                        element.Element("value")?.Value ?? string.Empty))
                    ?? Enumerable.Empty<KeyValuePair<string, string>>();

                var failures = ClinicalLanguagePolicy.ScanEnglish(entries);

                Assert.True(
                    failures.Count == 0,
                    $"English clinical-language policy violations found in {Path.GetFileName(file)}:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, failures));
            }
        }

        // -----------------------------------------------------------------------------
        // A13-F3: motor-generated (NON-resx) runtime copy must also stay clinically clean.
        // The resource guards above protect the static *.resx values, but the user also sees
        // strings that the producers assemble at runtime from those keys plus live numbers:
        // RecoveryScorer.Score(...).Explanation and the SessionInsightBuilder output (summary,
        // improvement and risk descriptions). We drive both producers over a broad range of
        // inputs — including the clinical extremes (every load signal maxed, deep overtraining,
        // catastrophic recovery, big improvements, every risk firing) — and assert that NOT ONE
        // of the assembled strings trips ClinicalLanguagePolicy.Scan. This catches a regression
        // where a future key edit or a composed phrase (e.g. a pluralised "Lowered by: …" line)
        // reintroduces shame/pressure copy that the per-key resx scan would not see in context.
        // The producers use the default LocalizationService.Instance, so this exercises the real
        // shipping copy (neutral resource, with the verbatim English fallback as a safety net).
        // -----------------------------------------------------------------------------

        [Fact]
        public void MotorGeneratedRuntimeStrings_PassClinicalLanguagePolicy()
        {
            var recoveryScorer = new RecoveryScorer();
            var insightBuilder = new SessionInsightBuilder();

            var generated = new List<KeyValuePair<string, string>>();

            // 1) RecoveryScorer explanations across the full input spectrum.
            foreach (var input in RecoveryScoreInputs())
            {
                var result = recoveryScorer.Score(input);
                generated.Add(new("RecoveryScorer.Explanation", result.Explanation));
            }

            // 2) SessionInsightBuilder output (summary + every improvement + every risk
            //    description) across first-session, steady, improving and worst-case sessions.
            foreach (var insight in SessionInsights(insightBuilder))
            {
                generated.Add(new("SessionInsight.Summary", insight.Summary));
                foreach (var improvement in insight.Improvements)
                    generated.Add(new("SessionInsight.Improvement", improvement.Explanation));
                foreach (var risk in insight.Risks)
                    generated.Add(new("SessionInsight.Risk", risk.Description));
                generated.Add(new("SessionInsight.RecoveryNeeds", insight.RecoveryNeeds.Explanation));
            }

            // Sanity: we actually produced a meaningful body of copy to scan.
            Assert.NotEmpty(generated);
            Assert.All(generated, g => Assert.False(string.IsNullOrWhiteSpace(g.Value)));

            var violations = ClinicalLanguagePolicy.Scan(generated);

            Assert.True(
                violations.Count == 0,
                "Clinical-language policy violations in motor-generated runtime copy:" +
                Environment.NewLine +
                string.Join(Environment.NewLine,
                    violations.Select(v => v.ToString())));
        }

        // Extreme + representative RecoveryScorer inputs (mirrors the boundary cases the
        // RecoveryScorerTests cover, so every explanation branch is exercised).
        private static IEnumerable<RecoveryScoreInput> RecoveryScoreInputs()
        {
            yield return new RecoveryScoreInput(); // new user — "well rested" no-load branch
            yield return new RecoveryScoreInput { RecentSafetyLocks = 1 }; // single dominant lock
            yield return new RecoveryScoreInput { RecentSafetyLocks = 10 }; // capped, overtrained
            yield return new RecoveryScoreInput
            {
                RecentStrainEpisodes = 1,
                RecentPauseRecommendations = 1,
                RecentFatigueIndicators = 1,
                HydrationSuggestionsRecent = 1,
            }; // singular driver phrasing
            yield return new RecoveryScoreInput
            {
                RecentStrainEpisodes = 3,
                RecentPauseRecommendations = 2,
                RecentFatigueIndicators = 6,
                PriorFatigueIndicators = 1, // rising trend driver
                HydrationSuggestionsRecent = 4,
            }; // plural drivers + trend
            yield return new RecoveryScoreInput
            {
                SessionsLast7Days = 8,
                HoursSinceLastSession = 2.0, // overtraining "training density" driver
            };
            yield return new RecoveryScoreInput
            {
                HoursSinceLastSession = 48.0, // rest-reward tail with a small load
                RecentFatigueIndicators = 2,
                PriorFatigueIndicators = 2,
            };
            yield return new RecoveryScoreInput // catastrophic: every signal maxed
            {
                RecentSafetyLocks = 10,
                RecentStrainEpisodes = 10,
                RecentPauseRecommendations = 10,
                RecentFatigueIndicators = 20,
                PriorFatigueIndicators = 0,
                HydrationSuggestionsRecent = 20,
                SessionsLast7Days = 14,
                HoursSinceLastSession = 0.0,
            };
        }

        // First-session, steady, improving and worst-case insights — covers every summary
        // branch and every risk description.
        private static IEnumerable<SessionInsight> SessionInsights(SessionInsightBuilder builder)
        {
            DimensionScore D(double s) => new(s, $"score {s:0.0}");

            VoiceIntelligenceScores Scores(
                double resonance = 50, double comfort = 50, double consistency = 50,
                double intonation = 50, double vocalWeight = 50, double recovery = 50,
                double pitch = 50, double composite = 50) => new()
                {
                    Resonance = D(resonance), Comfort = D(comfort), Consistency = D(consistency),
                    Intonation = D(intonation), VocalWeight = D(vocalWeight), Recovery = D(recovery),
                    Pitch = D(pitch), CompositeVoiceScore = composite,
                };

            VoiceIntelligenceTrendPoint Prior(
                double resonance = 50, double comfort = 50, double consistency = 50,
                double intonation = 50, double vocalWeight = 50, double recovery = 50,
                double pitch = 50) => new()
                {
                    SessionId = 1,
                    StartedAt = DateTime.Now.AddDays(-1),
                    EndedAt = DateTime.Now.AddDays(-1).AddMinutes(10),
                    ResonanceScore100 = resonance, ComfortScore100 = comfort,
                    ConsistencyScore100 = consistency, IntonationScore100 = intonation,
                    VocalWeightScore100 = vocalWeight, RecoveryScore100 = recovery,
                    PitchScore100 = pitch, CompositeVoiceScore = 50,
                };

            ExerciseSessionOutcome Outcome(
                int safetyLocks = 0, int strain = 0, int comfortBreaches = 0) => new()
                {
                    SafetyLockEpisodes = safetyLocks,
                    StrainDetections = strain,
                    ComfortBreachEpisodes = comfortBreaches,
                };

            var rested = new RecoveryScorer().Score(new RecoveryScoreInput());
            var strained = new RecoveryScorer().Score(new RecoveryScoreInput
            {
                RecentStrainEpisodes = 3,
                RecentPauseRecommendations = 1,
            });
            var overtrained = new RecoveryScorer().Score(new RecoveryScoreInput
            {
                RecentSafetyLocks = 2,
                RecentStrainEpisodes = 4,
            });

            // First session (no prior trend) — first-session summary branch.
            yield return builder.Build(Scores(pitch: 30), null, Outcome(), rested);

            // Steady session (prior present, nothing improved) — steady summary branch.
            yield return builder.Build(Scores(), new[] { Prior() }, Outcome(), rested);

            // Improving session — improvement summary + improvement explanations.
            yield return builder.Build(
                Scores(resonance: 70, pitch: 40, comfort: 62),
                new[] { Prior(resonance: 50, pitch: 35, comfort: 55) },
                Outcome(), rested);

            // Recovery-needs lead branch with a strained recovery.
            yield return builder.Build(Scores(recovery: 30), new[] { Prior() }, Outcome(), strained);

            // Worst case: every risk fires and recovery is overtrained.
            yield return builder.Build(
                Scores(pitch: 15),
                new[] { Prior() },
                Outcome(safetyLocks: 2, strain: 3, comfortBreaches: 5),
                overtrained);
        }

        private static string FindResourceDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "FemVoiceStudio", "Resources");
                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find FemVoiceStudio/Resources from test output path.");
        }
    }
}
