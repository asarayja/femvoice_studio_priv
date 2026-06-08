using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    // SmartCoach baseline/health models live in the Data namespace; alias them so the
    // arrange-blocks read the same way as the existing SmartCoachDecisionTests.
    using SmartCoachBaseline = FemVoiceStudio.Data.SmartCoachBaseline;
    using SmartCoachHealthMonitoring = FemVoiceStudio.Data.SmartCoachHealthMonitoring;

    /// <summary>
    /// Bølge 2 — SmartCoach VoiceMetrics-evolusjon (Agent COACH / Agent 9).
    ///
    /// Verifiserer at SmartCoachEngine, når den får en VoiceIntelligence-kilde
    /// (<see cref="SessionAnalyticsStore"/>), velger coaching-akse på den SVAKESTE
    /// VoiceMetrics-dimensjonen — men ALLTID etter health-gaten (Safety &gt; Coaching),
    /// og aldri slik at Pitch kaprer fokus fra en like svak høyere dimensjon.
    ///
    /// Ingen mocking: ekte <see cref="SessionAnalyticsStore"/> over en ekte
    /// in-memory-<see cref="InMemorySessionAnalyticsRepository"/>, og ekte
    /// <see cref="TestDatabaseService"/> — i tråd med prosjektets testmønster.
    /// </summary>
    public class SmartCoachVoiceIntelligenceTests
    {
        private readonly TestDatabaseService _testDatabase = new();

        // ── Hjelpere ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Bygger en ekte <see cref="SessionAnalyticsStore"/> med ETT fullført
        /// øktpunkt som bærer de oppgitte 0–100-dimensjonsscorene. Punktet legges
        /// «nylig» (innenfor motorens 30-dagers lookback) slik at det blir lest som
        /// siste økt.
        /// </summary>
        private static SessionAnalyticsStore StoreWithLatest(
            double resonance = 50,
            double comfort = 50,
            double consistency = 50,
            double intonation = 50,
            double vocalWeight = 50,
            double recovery = 50,
            double pitch = 50,
            int userId = 1,
            int sessionId = 901)
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

        private SmartCoachEngine EngineWith(SessionAnalyticsStore? store)
            => new(
                _testDatabase,
                localization: null,
                feedbackPipeline: null,
                feedbackMapper: null,
                voiceGoalProfiles: null,
                voiceIntelligence: store);

        private void SeedHealthyBaseline()
        {
            // En baseline som ALENE ville gitt pitch/balansert fokus (høy resonans),
            // slik at ethvert avvik vi observerer kommer fra VoiceMetrics-aksevalget.
            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 175,
                BaselineResonanceScore = 80,
                BaselineIntonation = 80,
                ConfidenceLevel = "high"
            });
        }

        // ── 1. Lavest dimensjon velger riktig akse ─────────────────────────────────

        [Fact]
        public void LowestDimension_Comfort_SelectsComfortFocus()
        {
            SeedHealthyBaseline();
            // Comfort klart lavest blant ekte dimensjoner.
            var store = StoreWithLatest(resonance: 75, comfort: 30, consistency: 80, recovery: 90, pitch: 70);
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("comfort", recommendation.FocusArea);
        }

        [Fact]
        public void LowestDimension_Consistency_SelectsConsistencyFocus()
        {
            SeedHealthyBaseline();
            var store = StoreWithLatest(resonance: 78, comfort: 80, consistency: 35, recovery: 88, intonation: 72, pitch: 70);
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("consistency", recommendation.FocusArea);
        }

        [Fact]
        public void LowestDimension_Recovery_SelectsRecoveryFocus()
        {
            SeedHealthyBaseline();
            // Recovery lavest — restitusjon (helse-dimensjon) vinner aksevalget. NB:
            // dette er IKKE health-gaten (ingen persistert strain) men en mild,
            // VoiceMetrics-drevet omdirigering.
            var store = StoreWithLatest(resonance: 80, comfort: 82, consistency: 84, recovery: 28, pitch: 70);
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("recovery", recommendation.FocusArea);
            // Dette er aksevalg, ikke en safety-blokk: ingen helse-advarsel skal heises.
            Assert.False(recommendation.HealthWarning);
        }

        [Fact]
        public void LowestDimension_Resonance_SelectsResonanceFocus()
        {
            SeedHealthyBaseline();
            var store = StoreWithLatest(resonance: 32, comfort: 80, consistency: 82, recovery: 90, pitch: 70);
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("resonance", recommendation.FocusArea);
        }

        // ── 2. Hierarki / tie-break ────────────────────────────────────────────────

        [Fact]
        public void TieBetweenComfortAndPitch_ComfortWins_PitchNeverDominant()
        {
            SeedHealthyBaseline();
            // Comfort og Pitch like svake (40). Hierarkiet (Comfort > ... > Pitch) skal
            // la Comfort vinne — Pitch kan ALDRI bli fokus når en høyere dimensjon er
            // like svak.
            var store = StoreWithLatest(resonance: 80, comfort: 40, consistency: 85, recovery: 90, intonation: 80, pitch: 40);
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("comfort", recommendation.FocusArea);
            Assert.NotEqual("pitch", recommendation.FocusArea);
        }

        [Fact]
        public void TieBetweenIntonationAndPitch_IntonationWins_PitchNeverDominant()
        {
            SeedHealthyBaseline();
            // Når kun Intonation og Pitch er svake og like (45), vinner Intonation —
            // Pitch er aldri eneste/viktigste fokus mens en høyere dimensjon er svak.
            var store = StoreWithLatest(resonance: 80, comfort: 82, consistency: 84, recovery: 90, intonation: 45, pitch: 45);
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("intonation", recommendation.FocusArea);
        }

        [Fact]
        public void PitchFocus_OnlyWhenPitchIsStrictlyWeakestAndAlone()
        {
            SeedHealthyBaseline();
            // Pitch er den ENESTE svake dimensjonen (alle andre over terskel). Da — og
            // kun da — er pitch et legitimt fokus.
            var store = StoreWithLatest(resonance: 80, comfort: 82, consistency: 84, recovery: 90, intonation: 78, pitch: 40);
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("pitch", recommendation.FocusArea);
        }

        // ── 3. Health-gate vinner fortsatt FØRST ───────────────────────────────────

        [Fact]
        public void HealthGate_WinsOverVoiceMetricsAxis()
        {
            SeedHealthyBaseline();
            // Aktiv strain MÅ omdirigere til recovery via health-gaten FØR aksevalget,
            // selv om en helt annen dimensjon (her: comfort) er svakest i scorene.
            _testDatabase.AddHealthMonitoring(new SmartCoachHealthMonitoring
            {
                UserId = 1,
                Date = DateTime.Today,
                StrainDetected = true,
                StrainType = "pitch_press",
                StrainLevel = 60
            });
            var store = StoreWithLatest(resonance: 80, comfort: 20, consistency: 85, recovery: 90, pitch: 70);
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            // Health-gaten heiser advarsel og setter recovery — aksevalget kjører aldri.
            Assert.True(recommendation.HealthWarning);
            Assert.Equal("recovery", recommendation.FocusArea);
        }

        // ── 4. null-provider ⇒ dagens oppførsel uendret ────────────────────────────

        [Fact]
        public void NullProvider_FallsBackToBaselineBehaviour_LowResonance()
        {
            // Uten VoiceIntelligence-kilde skal motoren oppføre seg nøyaktig som før:
            // lav baseline-resonans ⇒ resonance-fokus.
            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 170,
                BaselineResonanceScore = 55, // under 70-terskel
                BaselineIntonation = 50,
                ConfidenceLevel = "high"
            });
            var engine = EngineWith(null);

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("resonance", recommendation.FocusArea);
        }

        [Fact]
        public void NeutralOnlyTrend_DoesNotOverrideBaseline()
        {
            // Et rent nøytralt trendpunkt (alle dimensjoner = 50, dvs. intet reelt
            // signal — KJENT Bølge-1-gap) skal IKKE overstyre baseline-logikken.
            // Lav baseline-resonans ⇒ fortsatt resonance-fokus, ikke et VoiceMetrics-valg.
            _testDatabase.SetSmartCoachBaseline(new SmartCoachBaseline
            {
                UserId = 1,
                BaselinePitch = 170,
                BaselineResonanceScore = 55,
                BaselineIntonation = 50,
                ConfidenceLevel = "high"
            });
            var store = StoreWithLatest(); // alle defaults = 50
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            Assert.Equal("resonance", recommendation.FocusArea);
        }

        [Fact]
        public void AllDimensionsStrong_DoesNotOverrideBaseline()
        {
            // Når INGEN dimensjon er svak (alle godt over terskel) skal aksevalget
            // falle gjennom og baseline-/pitch-logikken bestemme — bakoverkompat.
            SeedHealthyBaseline();
            var store = StoreWithLatest(resonance: 85, comfort: 88, consistency: 90, recovery: 92, intonation: 84, vocalWeight: 80, pitch: 86);
            var engine = EngineWith(store);

            var recommendation = engine.GenerateDailyRecommendation(1);

            // Høy baseline-resonans + alt sterkt ⇒ ikke et VoiceMetrics-svakhetsvalg.
            Assert.NotEqual("comfort", recommendation.FocusArea);
            Assert.NotEqual("consistency", recommendation.FocusArea);
            Assert.NotEqual("recovery", recommendation.FocusArea);
        }

        // ── 5. Nye RESX-strenger finnes + er klinisk trygge ────────────────────────

        [Theory]
        [InlineData("SmartCoach_Focus_Comfort")]
        [InlineData("SmartCoach_Focus_Resonance")]
        [InlineData("SmartCoach_Focus_Consistency")]
        [InlineData("SmartCoach_Focus_Recovery")]
        public void NewFocusStrings_ExistInNeutralResources(string key)
        {
            var entries = LoadNeutralResourceEntries();
            Assert.True(
                entries.ContainsKey(key) && !string.IsNullOrWhiteSpace(entries[key]),
                $"Forventet ikke-tom RESX-streng for '{key}' i Strings.resx.");
        }

        [Fact]
        public void NewFocusStrings_PassClinicalLanguagePolicy()
        {
            var keys = new[]
            {
                "SmartCoach_Focus_Comfort",
                "SmartCoach_Focus_Resonance",
                "SmartCoach_Focus_Consistency",
                "SmartCoach_Focus_Recovery",
            };
            var entries = LoadNeutralResourceEntries();
            var subset = keys
                .Where(entries.ContainsKey)
                .Select(k => new KeyValuePair<string, string>(k, entries[k]));

            var violations = ClinicalLanguagePolicy.Scan(subset);

            Assert.True(
                violations.Count == 0,
                "Klinisk-språk-brudd i nye SmartCoach-fokusstrenger:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
        }

        // ── RESX-lesing (samme mønster som ResourceTextPolicyTests) ────────────────

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
    }
}
