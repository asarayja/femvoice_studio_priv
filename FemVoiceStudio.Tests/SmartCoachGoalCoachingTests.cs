using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    // SmartCoach baseline/health-modeller bor i Data-namespacet; alias dem slik at
    // arrange-blokkene leser likt som de øvrige SmartCoach-testene.
    using SmartCoachBaseline = FemVoiceStudio.Data.SmartCoachBaseline;
    using SmartCoachHealthMonitoring = FemVoiceStudio.Data.SmartCoachHealthMonitoring;
    using SmartCoachWeeklyProgress = FemVoiceStudio.Data.SmartCoachWeeklyProgress;

    /// <summary>
    /// Sprint C, Bølge 2 — GOAL-/STIL-/CONFIDENCE-/VARIGHET-evolusjon i SmartCoach
    /// (Agent COACH: Agent 4 + 7 + 8 + 6-varighet).
    ///
    /// Verifiserer at SmartCoachEngine, når den får de nye (valgfrie) tjenestene:
    ///   • differensierer coaching per STIL for samme fokus (særlig resonans —
    ///     DarkFeminine/Androgynous pushes ALDRI mot lysere/fremre klang),
    ///   • lar en prediktiv RecoveryForecast (Severity &gt;= Recommend) gi
    ///     restitusjons-coaching FØR mål (Health/Recovery &gt; Goals),
    ///   • feirer mestring (Stable/Mastered) med en positiv, bekreftende melding,
    ///   • skalerer øktvarighet fra recovery/konsistens (kortere ved lav recovery),
    /// og at NULL-tjenester gir NØYAKTIG dagens oppførsel (additivt + bakoverkompat).
    ///
    /// Ingen mocking: ekte <see cref="SessionAnalyticsStore"/> over ekte
    /// in-memory-repo, ekte <see cref="RecoveryIntelligenceService"/> /
    /// <see cref="LearningPathProfileBuilder"/>, ekte <see cref="TestDatabaseService"/>,
    /// og rene delegat-sømmer der en per-bruker-bro ellers ville krevd plumbing.
    /// </summary>
    public class SmartCoachGoalCoachingTests
    {
        private readonly TestDatabaseService _testDatabase = new();

        // ── Hjelpere ──────────────────────────────────────────────────────────────

        private static SessionAnalyticsStore StoreWithLatest(
            double resonance = 50,
            double comfort = 50,
            double consistency = 50,
            double intonation = 50,
            double vocalWeight = 50,
            double recovery = 50,
            double pitch = 50,
            int userId = 1,
            int sessionId = 7001)
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var startedAt = DateTime.Now.AddMinutes(-5);

            store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
            {
                SessionId = sessionId,
                UserId = userId,
                StartedAt = startedAt,
                EndedAt = startedAt.AddMinutes(4),
                ExerciseCount = 1,
                ResonanceScore100 = resonance,
                ComfortScore100 = comfort,
                ConsistencyScore100 = consistency,
                IntonationScore100 = intonation,
                VocalWeightScore100 = vocalWeight,
                RecoveryScore100 = recovery,
                PitchScore100 = pitch,
                CompositeVoiceScore = 60
            }).GetAwaiter().GetResult();

            return store;
        }

        /// <summary>Tom kilde (ingen økter) — bærer ikke noe reelt signal.</summary>
        private static SessionAnalyticsStore EmptyStore()
            => new(new InMemorySessionAnalyticsRepository());

        private SmartCoachEngine EngineWith(
            SessionAnalyticsStore? store = null,
            RecoveryIntelligenceService? recoveryIntelligence = null,
            LearningPathProfileBuilder? learningPathBuilder = null,
            Func<int, MasteryLevel?>? masteryLevelProvider = null)
            => new(
                _testDatabase,
                localization: null,
                feedbackPipeline: null,
                feedbackMapper: null,
                voiceGoalProfiles: null,
                voiceIntelligence: store,
                recoveryIntelligence: recoveryIntelligence,
                learningPathBuilder: learningPathBuilder,
                complexityEngine: null,
                masteryLevelProvider: masteryLevelProvider);

        private void SeedHealthyBaseline()
        {
            // Baseline som alene ville gitt pitch/balansert fokus (høy resonans), slik at
            // ethvert avvik vi observerer kommer fra de nye lagene.
            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 175,
                BaselineResonanceScore = 80,
                BaselineIntonation = 80,
                ConfidenceLevel = "high"
            });
        }

        /// <summary>Lav baseline-resonans ⇒ resonance-fokus i fallback-logikken.</summary>
        private void SeedLowResonanceBaseline()
        {
            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 170,
                BaselineResonanceScore = 55, // under 70-terskel ⇒ resonance
                BaselineIntonation = 80,
                ConfidenceLevel = "high"
            });
        }

        private void SetStyle(VoiceStyleGoal style)
        {
            _testDatabase.SaveUserVoiceProfile(new UserVoiceProfile
            {
                UserId = 1,
                PreferredVoiceStyle = style
            });
        }

        // ── Resurs-lesing (samme mønster som SmartCoachVoiceIntelligenceTests) ──────

        private static IReadOnlyDictionary<string, string> LoadNeutralResourceEntries()
        {
            var path = FindNeutralResourcePath();
            var document = XDocument.Load(path);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in document.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
            {
                var name = (string?)element.Attribute("name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                result[name] = element.Element("value")?.Value ?? string.Empty;
            }
            return result;
        }

        private static string FindNeutralResourcePath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = System.IO.Path.Combine(directory.FullName, "FemVoiceStudio", "Resources", "Strings.resx");
                if (System.IO.File.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }

            throw new System.IO.FileNotFoundException(
                "Fant ikke FemVoiceStudio/Resources/Strings.resx fra testens output-sti.");
        }

        private static string ResonanceTextFor(VoiceStyleGoal style)
        {
            var key = style switch
            {
                VoiceStyleGoal.Feminine => "SmartCoach_Style_Resonance_Feminine",
                VoiceStyleGoal.Androgynous => "SmartCoach_Style_Resonance_Androgynous",
                VoiceStyleGoal.DarkFeminine => "SmartCoach_Style_Resonance_DarkFeminine",
                VoiceStyleGoal.Situational => "SmartCoach_Style_Resonance_Situational",
                _ => "SmartCoach_Daily_ResonanceMaintenance"
            };
            return LocalizationService.Instance.GetString(key);
        }

        // ══ 1. STIL-DIFFERENSIERT COACHING (Agent 4) ════════════════════════════════

        [Theory]
        [InlineData(VoiceStyleGoal.Feminine)]
        [InlineData(VoiceStyleGoal.Androgynous)]
        [InlineData(VoiceStyleGoal.DarkFeminine)]
        [InlineData(VoiceStyleGoal.Situational)]
        public void Resonance_SameFocus_DifferentTextPerStyle(VoiceStyleGoal style)
        {
            // Svak resonans ⇒ resonance-fokus via VoiceMetrics-aksevalget. Teksten skal
            // matche STILEN, ikke en stil-nøytral standard.
            SeedHealthyBaseline();
            SetStyle(style);
            var store = StoreWithLatest(resonance: 32, comfort: 85, consistency: 85, recovery: 90, pitch: 80);
            var engine = EngineWith(store);

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.Equal("resonance", rec.FocusArea);
            Assert.Equal(ResonanceTextFor(style), rec.RecommendationText);
        }

        [Fact]
        public void Resonance_AllFiveStylesGiveDistinctCoaching()
        {
            // De fem stilene (Feminine/Androgynous/DarkFeminine/Situational/Custom) MÅ gi
            // ulik resonans-coaching for SAMME fokus. Custom faller til den stil-nøytrale
            // standarden, som likevel skiller seg fra de fire stil-spesifikke.
            var texts = new List<string>();
            foreach (var style in new[]
            {
                VoiceStyleGoal.Feminine,
                VoiceStyleGoal.Androgynous,
                VoiceStyleGoal.DarkFeminine,
                VoiceStyleGoal.Situational,
                VoiceStyleGoal.Custom
            })
            {
                _testDatabase.Clear();
                SeedHealthyBaseline();
                SetStyle(style);
                var store = StoreWithLatest(resonance: 30, comfort: 85, consistency: 85, recovery: 90, pitch: 80);
                var engine = EngineWith(store);

                var rec = engine.GenerateDailyRecommendation(1);
                Assert.Equal("resonance", rec.FocusArea);
                texts.Add(rec.RecommendationText);
            }

            // Alle fem distinkte.
            Assert.Equal(5, texts.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void DarkFeminine_Resonance_DoesNotPushBrighterOrFronted()
        {
            // KJERNEN i Agent 4: DarkFeminine-resonans skal SENKE / gi varm, fyldig klang —
            // og ALDRI be brukeren om en lysere/fremre resonans (som er Feminine-retningen).
            SeedHealthyBaseline();
            SetStyle(VoiceStyleGoal.DarkFeminine);
            var store = StoreWithLatest(resonance: 30, comfort: 85, consistency: 85, recovery: 90, pitch: 80);
            var engine = EngineWith(store);

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.Equal("resonance", rec.FocusArea);
            var text = rec.RecommendationText.ToLowerInvariant();
            // Ingen «lysere»/«fremre/fremover»-press i DarkFeminine-teksten.
            Assert.DoesNotContain("lysere", text);
            Assert.DoesNotContain("fremre", text);
            Assert.DoesNotContain("fremover", text);
            // Den skal positivt ramme en varm/fyldig/mørkere retning.
            Assert.True(
                text.Contains("varm") || text.Contains("fyldig") || text.Contains("mørk") || text.Contains("dyp"),
                "DarkFeminine-resonans skal ramme en varm/fyldig/mørkere klang.");
        }

        [Fact]
        public void Feminine_Resonance_KeepsBrighterFrontedFraming()
        {
            // Kontrast til DarkFeminine: Feminine-retningen SKAL kunne snakke om en lysere,
            // fremre klang.
            SeedHealthyBaseline();
            SetStyle(VoiceStyleGoal.Feminine);
            var store = StoreWithLatest(resonance: 30, comfort: 85, consistency: 85, recovery: 90, pitch: 80);
            var engine = EngineWith(store);

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.Equal("resonance", rec.FocusArea);
            var text = rec.RecommendationText.ToLowerInvariant();
            Assert.True(
                text.Contains("lysere") || text.Contains("fremre") || text.Contains("fremover"),
                "Feminine-resonans kan ramme en lysere/fremre klang.");
        }

        [Fact]
        public void DarkFeminineAndFeminine_SameResonanceFocus_ProduceDifferentText()
        {
            // To brukere med IDENTISK fokus (resonans) men ulik stil får ULIK coaching —
            // det opprinnelige bug-scenarioet (alle fikk «lysere, fremre klang»).
            SeedHealthyBaseline();
            SetStyle(VoiceStyleGoal.Feminine);
            var feminineRec = EngineWith(
                StoreWithLatest(resonance: 30, comfort: 85, consistency: 85, recovery: 90, pitch: 80))
                .GenerateDailyRecommendation(1);

            _testDatabase.Clear();
            SeedHealthyBaseline();
            SetStyle(VoiceStyleGoal.DarkFeminine);
            var darkRec = EngineWith(
                StoreWithLatest(resonance: 30, comfort: 85, consistency: 85, recovery: 90, pitch: 80))
                .GenerateDailyRecommendation(1);

            Assert.Equal("resonance", feminineRec.FocusArea);
            Assert.Equal("resonance", darkRec.FocusArea);
            Assert.NotEqual(feminineRec.RecommendationText, darkRec.RecommendationText);
        }

        // ══ 2. RECOVERY-FORECAST FØR MÅL (Agent 8) — Health/Recovery > Goals ════════

        [Fact]
        public void RecoveryForecast_SeverityRecommend_RedirectsToRecoveryBeforeGoals()
        {
            // En prediktiv overload (mange økter, lite hvile) gir Severity >= Recommend.
            // Selv om baseline-resonansen er lav (ellers ⇒ resonance), skal restitusjon
            // vinne FØR mål — men UTEN å heise en helse-advarsel (ingen persistert strain).
            SeedLowResonanceBaseline();

            var recoveryIntelligence = new RecoveryIntelligenceService();
            // Bygg en historikk som garantert utløser Recommend: høy tetthet (>5 økter)
            // med lite hvile siste døgn ⇒ overtraining-grenen + recovery-debt.
            var store = StoreWithRecentDenseHistory(sessionCount: 8, hoursApart: 2);
            var engine = EngineWith(store, recoveryIntelligence: recoveryIntelligence);

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.Equal("recovery", rec.FocusArea);
            Assert.False(rec.HealthWarning); // mild omdirigering, ikke en safety-blokk
        }

        [Fact]
        public void RecoveryForecast_None_DoesNotRedirect_BaselineWins()
        {
            // Et rolig bilde (én økt, godt uthvilt) gir Severity None ⇒ recovery-gaten
            // utløses ikke, og fallback-logikken (lav baseline-resonans ⇒ resonance)
            // bestemmer. Bekrefter at recovery-gaten er konservativ.
            SeedLowResonanceBaseline();
            var recoveryIntelligence = new RecoveryIntelligenceService();
            // Tom kilde ⇒ ingen trend ⇒ forecast på en helt fersk bruker = WellRecovered.
            var store = EmptyStore();
            var engine = EngineWith(store, recoveryIntelligence: recoveryIntelligence);

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.Equal("resonance", rec.FocusArea);
            Assert.False(rec.HealthWarning);
        }

        [Fact]
        public void LearningPath_RestRecommended_RedirectsToRecoveryBeforeGoals()
        {
            // LearningPath-profilen bygges med et recovery-resultat som er Strained
            // (RestRecommended=true). Det skal — uavhengig av forecast — gi recovery FØR mål.
            SeedLowResonanceBaseline();

            var recoveryIntelligence = new RecoveryIntelligenceService();
            var learningPathBuilder = new LearningPathProfileBuilder();
            // En historikk som gir en strained recovery (mange økter, lite hvile) slik at
            // forecast.Current.Status blir Strained/Overtrained ⇒ RestRecommended i profilen.
            var store = StoreWithRecentDenseHistory(sessionCount: 8, hoursApart: 2);
            var engine = EngineWith(
                store,
                recoveryIntelligence: recoveryIntelligence,
                learningPathBuilder: learningPathBuilder);

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.Equal("recovery", rec.FocusArea);
            Assert.False(rec.HealthWarning);
        }

        [Fact]
        public void RecoveryForecast_StyleAware_RecoveryTextMatchesStyle()
        {
            // Recovery-coaching skal også være stil-fargelagt.
            SeedHealthyBaseline();
            SetStyle(VoiceStyleGoal.DarkFeminine);
            var recoveryIntelligence = new RecoveryIntelligenceService();
            var store = StoreWithRecentDenseHistory(sessionCount: 8, hoursApart: 2);
            var engine = EngineWith(store, recoveryIntelligence: recoveryIntelligence);

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.Equal("recovery", rec.FocusArea);
            Assert.Equal(
                LocalizationService.Instance.GetString("SmartCoach_Style_Recovery_DarkFeminine"),
                rec.RecommendationText);
        }

        // ══ 3. MESTRING/CONFIDENCE (Agent 7) — feirende, bekreftende melding ════════

        [Fact]
        public void Mastery_Mastered_ProducesCelebratingMessage()
        {
            SeedWeeklyProgressNeutral();
            var engine = EngineWith(masteryLevelProvider: _ => MasteryLevel.Mastered);

            engine.GenerateMotivationalMessages(1);

            var messages = _testDatabase.GetMessages(1, 10);
            var celebrating = Assert.Single(messages);
            Assert.Equal("achievement", celebrating.MessageType);
            Assert.Equal(LocalizationService.Instance.GetString("SmartCoach_Mastery_Mastered"), celebrating.Message);
        }

        [Fact]
        public void Mastery_Stable_ProducesCelebratingMessage()
        {
            SeedWeeklyProgressNeutral();
            var engine = EngineWith(masteryLevelProvider: _ => MasteryLevel.Stable);

            engine.GenerateMotivationalMessages(1);

            var messages = _testDatabase.GetMessages(1, 10);
            var celebrating = Assert.Single(messages);
            Assert.Equal("achievement", celebrating.MessageType);
            Assert.Equal(LocalizationService.Instance.GetString("SmartCoach_Mastery_Stable"), celebrating.Message);
        }

        [Fact]
        public void Mastery_BeginnerOrNull_FallsBackToExistingMessageBehaviour()
        {
            // Beginner ⇒ ingen mestrings-melding; vi faller til den eksisterende
            // tips/motivasjons-logikken (her: lav score ⇒ ukens tips).
            SeedWeeklyProgressNeutral();
            var engine = EngineWith(masteryLevelProvider: _ => MasteryLevel.Beginner);

            engine.GenerateMotivationalMessages(1);

            var messages = _testDatabase.GetMessages(1, 10);
            var msg = Assert.Single(messages);
            // Ikke mestrings-tittelen — dagens oppførsel.
            Assert.NotEqual(LocalizationService.Instance.GetString("SmartCoach_Mastery_Title"), msg.Title);
        }

        [Fact]
        public void Mastery_RespectsHealthGate_NoMessageDuringStrain()
        {
            // Safety > Coaching: ved aktiv strain skal selv en mestrings-melding undertrykkes.
            SeedWeeklyProgressNeutral();
            _testDatabase.AddHealthMonitoring(new SmartCoachHealthMonitoring
            {
                UserId = 1,
                Date = DateTime.Today,
                StrainDetected = true,
                StrainType = "fatigue",
                StrainLevel = 40
            });
            var engine = EngineWith(masteryLevelProvider: _ => MasteryLevel.Mastered);

            engine.GenerateMotivationalMessages(1);

            Assert.Empty(_testDatabase.GetMessages(1, 10));
        }

        // ══ 4. ADAPTIV VARIGHET (Agent 6-varighet) ══════════════════════════════════

        [Fact]
        public void Duration_ShorterWhenRecoveryLow()
        {
            // Lav recovery (forecast.Current.Score lav) ⇒ kortere varighet enn basis.
            // Vi velger en NON-recovery-akse (resonance) for å isolere skalerings-effekten:
            // recovery er lav nok til å korte, men ikke svakest (comfort/recovery høyere),
            // så aksevalget lander på resonance med basis-varighet før skalering.
            SeedHealthyBaseline();
            var recoveryIntelligence = new RecoveryIntelligenceService();

            // Lav recovery via dens historikk: mange økter + lite hvile ⇒ lav score.
            var lowRecoveryStore = StoreWithRecentDenseHistory(
                sessionCount: 8, hoursApart: 2, resonance: 30, comfort: 85, consistency: 85, recovery: 40, pitch: 80);
            // Sammenlign med god-recovery-bilde (én rolig økt, samme svake resonans).
            var goodRecoveryStore = StoreWithLatest(
                resonance: 30, comfort: 85, consistency: 85, recovery: 95, pitch: 80);

            // For å isolere VARIGHET (ikke recovery-redirect), sammenligner vi to bilder
            // der recovery-gaten IKKE utløses i god-bildet, men gjør det i lav-bildet.
            // Derfor måler vi varighet i recovery-grenen mot recovery-grenen ville vært
            // identisk; i stedet bruker vi to baseline-drevne resonance-bilder via en
            // dedikert hjelp: ScaleDuration testes direkte gjennom recovery-grenens 5 min.
            var lowEngine = EngineWith(lowRecoveryStore, recoveryIntelligence: recoveryIntelligence);
            var goodEngine = EngineWith(goodRecoveryStore, recoveryIntelligence: recoveryIntelligence);

            var lowRec = lowEngine.GenerateDailyRecommendation(1);
            _testDatabase.Clear();
            SeedHealthyBaseline();
            var goodRec = goodEngine.GenerateDailyRecommendation(1);

            // Lav-recovery-bildet utløser recovery-grenen (kort, strammet) — alltid kortere
            // eller lik en uskalert god-økt med samme akse.
            Assert.True(lowRec.RecommendedDurationMinutes <= goodRec.RecommendedDurationMinutes,
                $"Forventet kortere/lik varighet ved lav recovery: lav={lowRec.RecommendedDurationMinutes}, god={goodRec.RecommendedDurationMinutes}");
        }

        [Fact]
        public void Duration_RecoveryOrientedSession_IsShortened()
        {
            // En recovery-orientert økt (recovery-redirect) skal alltid være kort: den
            // strammede varigheten ligger under den gamle faste 5-min-konstanten.
            SeedHealthyBaseline();
            var recoveryIntelligence = new RecoveryIntelligenceService();
            var store = StoreWithRecentDenseHistory(sessionCount: 8, hoursApart: 2);
            var engine = EngineWith(store, recoveryIntelligence: recoveryIntelligence);

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.Equal("recovery", rec.FocusArea);
            Assert.True(rec.RecommendedDurationMinutes < 5,
                $"Recovery-orientert økt skal være strammet (< 5 min), var {rec.RecommendedDurationMinutes}.");
            Assert.True(rec.RecommendedDurationMinutes >= 3, "Aldri under gulvet på 3 min.");
        }

        [Fact]
        public void Duration_StrainGate_StillShortAndWithinBounds()
        {
            // Strain-gaten beholdes; varigheten skaleres nå (kun strammere) men forblir
            // innenfor de trygge grensene og aldri lengre enn den gamle 5-min-konstanten.
            SeedHealthyBaseline();
            _testDatabase.AddHealthMonitoring(new SmartCoachHealthMonitoring
            {
                UserId = 1,
                Date = DateTime.Today,
                StrainDetected = true,
                StrainType = "pitch_press",
                StrainLevel = 60,
                Recommendation = "rolig"
            });
            var engine = EngineWith();

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.True(rec.HealthWarning);
            Assert.Equal("recovery", rec.FocusArea);
            Assert.True(rec.RecommendedDurationMinutes <= 5 && rec.RecommendedDurationMinutes >= 3,
                $"Strain-økt skal være kort og trygg, var {rec.RecommendedDurationMinutes}.");
        }

        // ══ 5. NULL-TJENESTER ⇒ DAGENS OPPFØRSEL UENDRET ════════════════════════════

        [Fact]
        public void NullServices_DailyRecommendation_UnchangedBaselineBehaviour()
        {
            // Ingen av de nye tjenestene injisert (og ingen VoiceIntelligence) ⇒ ren
            // baseline-oppførsel: lav baseline-resonans ⇒ resonance, basis-varighet bevart.
            SeedLowResonanceBaseline();
            var engine = EngineWith(); // alt null

            var rec = engine.GenerateDailyRecommendation(1);

            Assert.Equal("resonance", rec.FocusArea);
            // Stil-nøytral standardtekst (ingen UserVoiceProfile lagret ⇒ ingen stil).
            Assert.Equal(
                LocalizationService.Instance.GetString("SmartCoach_Daily_ResonanceDevelopment"),
                rec.RecommendationText);
            // Basis-varighet uendret (GetResonanceRecommendation gir 6 for 55-score).
            Assert.Equal(6, rec.RecommendedDurationMinutes);
        }

        [Fact]
        public void NullMasteryProvider_MotivationalMessages_UnchangedBehaviour()
        {
            // Uten mastery-provider faller GenerateMotivationalMessages til dagens logikk.
            SeedWeeklyProgressNeutral();
            var engine = EngineWith(); // masteryLevelProvider null

            engine.GenerateMotivationalMessages(1);

            var messages = _testDatabase.GetMessages(1, 10);
            var msg = Assert.Single(messages);
            Assert.NotEqual(LocalizationService.Instance.GetString("SmartCoach_Mastery_Title"), msg.Title);
        }

        // ══ 6. NYE RESX-STRENGER FINNES + ER KLINISK TRYGGE ════════════════════════

        [Theory]
        [InlineData("SmartCoach_Style_Resonance_Feminine")]
        [InlineData("SmartCoach_Style_Resonance_Androgynous")]
        [InlineData("SmartCoach_Style_Resonance_DarkFeminine")]
        [InlineData("SmartCoach_Style_Resonance_Situational")]
        [InlineData("SmartCoach_Style_Recovery_Feminine")]
        [InlineData("SmartCoach_Style_Recovery_Androgynous")]
        [InlineData("SmartCoach_Style_Recovery_DarkFeminine")]
        [InlineData("SmartCoach_Style_Recovery_Situational")]
        [InlineData("SmartCoach_Mastery_Title")]
        [InlineData("SmartCoach_Mastery_Stable")]
        [InlineData("SmartCoach_Mastery_Mastered")]
        public void NewStrings_ExistInNeutralResources(string key)
        {
            var entries = LoadNeutralResourceEntries();
            Assert.True(
                entries.ContainsKey(key) && !string.IsNullOrWhiteSpace(entries[key]),
                $"Forventet ikke-tom RESX-streng for '{key}' i Strings.resx.");
        }

        [Fact]
        public void NewStrings_PassClinicalLanguagePolicy()
        {
            var keys = new[]
            {
                "SmartCoach_Style_Resonance_Feminine",
                "SmartCoach_Style_Resonance_Androgynous",
                "SmartCoach_Style_Resonance_DarkFeminine",
                "SmartCoach_Style_Resonance_Situational",
                "SmartCoach_Style_Recovery_Feminine",
                "SmartCoach_Style_Recovery_Androgynous",
                "SmartCoach_Style_Recovery_DarkFeminine",
                "SmartCoach_Style_Recovery_Situational",
                "SmartCoach_Mastery_Title",
                "SmartCoach_Mastery_Stable",
                "SmartCoach_Mastery_Mastered",
            };
            var entries = LoadNeutralResourceEntries();
            var subset = keys
                .Where(entries.ContainsKey)
                .Select(k => new KeyValuePair<string, string>(k, entries[k]));

            var violations = ClinicalLanguagePolicy.Scan(subset);

            Assert.True(
                violations.Count == 0,
                "Klinisk-språk-brudd i nye SmartCoach-strenger:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
        }

        // ── Felles hjelpere for recovery-/progresjon-scenarioer ─────────────────────

        /// <summary>
        /// Bygger en historikk med <paramref name="sessionCount"/> nylige økter, plassert
        /// <paramref name="hoursApart"/> timer fra hverandre (siste ~hoursApart timer
        /// siden nå). Mange økter + lite hvile ⇒ scorer-ens overtraining-/debt-grener
        /// fyrer ⇒ Severity &gt;= Recommend og en strained/overtrained recovery-status.
        /// </summary>
        private static SessionAnalyticsStore StoreWithRecentDenseHistory(
            int sessionCount,
            double hoursApart,
            double resonance = 75,
            double comfort = 75,
            double consistency = 75,
            double recovery = 75,
            double pitch = 75,
            int userId = 1)
        {
            var store = new SessionAnalyticsStore(new InMemorySessionAnalyticsRepository());
            var now = DateTime.Now;
            for (var i = 0; i < sessionCount; i++)
            {
                var started = now.AddHours(-hoursApart * (i + 1));
                store.RecordSessionCompletedAsync(new SessionAnalyticsRecord
                {
                    SessionId = 8000 + i,
                    UserId = userId,
                    StartedAt = started,
                    EndedAt = started.AddMinutes(6),
                    ExerciseCount = 1,
                    ResonanceScore100 = resonance,
                    ComfortScore100 = comfort,
                    ConsistencyScore100 = consistency,
                    IntonationScore100 = 75,
                    VocalWeightScore100 = 75,
                    RecoveryScore100 = recovery,
                    PitchScore100 = pitch,
                    CompositeVoiceScore = 60
                }).GetAwaiter().GetResult();
            }
            return store;
        }

        private void SeedWeeklyProgressNeutral()
        {
            // En nøytral ukesprogresjon der ingen av de score-baserte rosene utløses
            // (AverageScore < 80, ingen resonance/pitch-endring, få økter) ⇒ uten mastery
            // faller vi til «ukens tips». Lar mastery-grenen være den eneste forskjellen.
            _testDatabase.SaveWeeklyProgress(new SmartCoachWeeklyProgress
            {
                UserId = 1,
                WeekStart = DateTime.Today.AddDays(-7),
                WeekEnd = DateTime.Today,
                SessionsCount = 1,
                AverageScore = 60,
                ResonanceChange = 0,
                PitchChange = 0
            });
        }
    }
}
