using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using FemVoiceStudio.Services.Progression;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// EXERCISE CATALOG COVERAGE (Sprint C.1 — Agent FIX, katalog/guidance must-fixes).
    ///
    /// Låser tre kliniske invarianter etter katalog/guidance-fiksene:
    ///
    /// 1. PROFILE-TYPE-NÅBARHET: HVER <see cref="ExerciseProfileType"/>-verdi mapper via den
    ///    EKTE <see cref="ExerciseProfileFactory"/> til en fabrikkprofil med alle fire
    ///    guidance-nøkler ikke-tomme OG som resolver til ikke-whitespace tekst i
    ///    Resources/Strings.resx (samme XDocument-mønster som GuidanceCompletenessTests).
    ///    Ingen enum-verdi kan lenger være "uten en case" eller peke på manglende guidance.
    ///
    /// 2. STRAW-PHONATION-FIKS (must-fix klinisk safety): øvelse 14 (Straw phonation) er
    ///    seedet med <see cref="ExerciseProfileType.StrawPhonation"/> — IKKE
    ///    StabilityTraining — og StrawPhonation gir SOVT/airflow-guidance (UsesIntensity),
    ///    ikke endurance/hold-guidance.
    ///
    /// 3. FANTOM-ID-FIKS (must-fix): de tre id-bøtte-kildene
    ///    (<see cref="ComplexityEngine.GetExerciseIdsForComplexity"/>,
    ///    <see cref="ExerciseRecommendationEngine"/> via <see cref="ExerciseRecommendationEngine.RecommendNext"/>,
    ///    <see cref="LearningPathProfileBuilder"/> via reflection på ExerciseIdsForLevel)
    ///    returnerer KUN id-er innenfor det reelle katalog-rommet (1–15), er i SYNC, og
    ///    dekker hvert nivå med minst én id. Ingen emittert id mangler en katalog-øvelse.
    ///
    /// Ekte klasser + in-memory data, ingen mocking — i tråd med prosjektets mønster.
    /// </summary>
    public class ExerciseCatalogCoverageTests
    {
        // Katalogen har nøyaktig 15 reelle, seedede øvelser (ExerciseId 1–15).
        private const int CatalogMinId = 1;
        private const int CatalogMaxId = 15;

        private static readonly (string Dimension, Func<ExerciseTargetProfile, string?> Key)[] GuidanceDimensions =
        {
            ("ClinicalPurpose", p => p.ClinicalPurposeKey),
            ("PhysicalFocus",   p => p.PhysicalFocusKey),
            ("CommonMistakes",  p => p.CommonMistakesKey),
            ("Safety",          p => p.SafetyInfoKey),
        };

        public static IEnumerable<object[]> AllProfileTypes()
            => Enum.GetValues(typeof(ExerciseProfileType))
                   .Cast<ExerciseProfileType>()
                   .Select(t => new object[] { t });

        // ── 1. Profile-type-nåbarhet ─────────────────────────────────────────────────

        [Fact]
        public void EveryProfileType_MapsToAProfile_ViaRealFactory()
        {
            // Den ekte fabrikken må ha en case for hver enum-verdi — ingen
            // ArgumentOutOfRangeException for noen definert ExerciseProfileType.
            var factory = new ExerciseProfileFactory();
            foreach (ExerciseProfileType type in Enum.GetValues(typeof(ExerciseProfileType)))
            {
                var profile = factory.CreateProfile(type);
                Assert.NotNull(profile);
            }
        }

        [Theory]
        [MemberData(nameof(AllProfileTypes))]
        public void EveryProfileType_DeclaresAllFourGuidanceKeys_NonEmpty(ExerciseProfileType type)
        {
            var profile = new ExerciseProfileFactory().CreateProfile(type);

            foreach (var (dimension, key) in GuidanceDimensions)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(key(profile)),
                    $"{type}: guidance dimension '{dimension}' key is null/empty — " +
                    "a clinical-safety dimension would silently disappear from the guidance panel.");
            }
        }

        [Theory]
        [MemberData(nameof(AllProfileTypes))]
        public void EveryProfileType_GuidanceKeys_ResolveToNonWhitespaceResxValues(ExerciseProfileType type)
        {
            var profile = new ExerciseProfileFactory().CreateProfile(type);
            var resources = LoadNeutralResourceValues();

            foreach (var (dimension, keyAccessor) in GuidanceDimensions)
            {
                var key = keyAccessor(profile);
                Assert.False(string.IsNullOrWhiteSpace(key),
                    $"{type}: guidance dimension '{dimension}' key is null/empty.");

                Assert.True(
                    resources.TryGetValue(key!, out var value),
                    $"{type}: guidance key '{key}' ({dimension}) is missing from Strings.resx.");

                Assert.False(
                    string.IsNullOrWhiteSpace(value),
                    $"{type}: guidance key '{key}' ({dimension}) resolves to whitespace/empty in Strings.resx.");
            }
        }

        // ── 2. Straw-phonation-fiks ──────────────────────────────────────────────────

        [Fact]
        public void StrawPhonationProfile_IsSovtAirflow_NotEnduranceHold()
        {
            // StrawPhonation må gi SOVT/airflow-guidance (UsesIntensity) — den kliniske
            // motsatsen til StabilityTraining sin endurance/hold-profil.
            var straw = new ExerciseProfileFactory().CreateProfile(ExerciseProfileType.StrawPhonation);
            var stability = new ExerciseProfileFactory().CreateProfile(ExerciseProfileType.StabilityTraining);

            Assert.True(straw.UsesIntensity,
                "Straw phonation is SOVT/airflow — it MUST evaluate intensity (airflow), unlike endurance training.");
            Assert.False(stability.UsesIntensity,
                "Stability/endurance training must NOT be intensity-driven; confirms the two profiles are distinct.");

            // Guidance-nøklene må være de straw-spesifikke, ikke stability-nøklene.
            Assert.Equal("GuidancePurpose_StrawPhonation", straw.ClinicalPurposeKey);
            Assert.Equal("GuidanceSafety_StrawPhonation", straw.SafetyInfoKey);
            Assert.NotEqual(stability.SafetyInfoKey, straw.SafetyInfoKey);
        }

        [Fact]
        public void Exercise14_IsSeededWithStrawPhonationProfile_NotStabilityTraining()
        {
            // Source-level lock (samme idé som GuidanceCompletenessTests som leser RESX-XML
            // direkte): øvelse 14-blokken i seeden MÅ deklarere ProfileType = StrawPhonation.
            // Fanger en regresjon der øvelse 14 igjen mis-mappes til endurance/hold-guidance.
            var seed = ReadExerciseDataServiceSource();
            var exercise14 = ExtractExerciseBlock(seed, sortOrder: 14);

            Assert.Contains("ExerciseProfileType.StrawPhonation", exercise14);
            Assert.DoesNotContain("ExerciseProfileType.StabilityTraining", exercise14);
        }

        [Fact]
        public void IntonationExercises7And8_AreSeededWithIntonationProfile()
        {
            // Klinisk re-map: spørsmåls-/utsagns-intonasjon skal ha intonasjons-guidance.
            var seed = ReadExerciseDataServiceSource();
            foreach (var sortOrder in new[] { 7, 8 })
            {
                var block = ExtractExerciseBlock(seed, sortOrder);
                Assert.Contains("ExerciseProfileType.IntonationExercise", block);
            }
        }

        // ── 3. Fantom-id-fiks: de tre id-kildene ─────────────────────────────────────

        public static IEnumerable<object[]> AllComplexityLevels()
            => Enum.GetValues(typeof(SpeechComplexityLevel))
                   .Cast<SpeechComplexityLevel>()
                   .Select(l => new object[] { l });

        [Theory]
        [MemberData(nameof(AllComplexityLevels))]
        public void RecommendationEngine_OnlyEmitsCatalogIds_ForEveryLevel(SpeechComplexityLevel level)
        {
            // Den EKTE recommendation-motoren må aldri foreslå en id utenfor 1–15 — uansett
            // nivå, recovery-tilstand eller fokus. Vi prøver flere baner gjennom motoren.
            var engine = new ExerciseRecommendationEngine();

            var inputs = new[]
            {
                // Recovery-gate (light pool).
                new ExerciseRecommendationInput
                {
                    Recovery = new RecoveryResult { Score = 5, Status = RecoveryStatus.Overtrained },
                    ComplexityLevel = level
                },
                // Foundation (ingen historikk).
                new ExerciseRecommendationInput
                {
                    Recovery = new RecoveryResult { Score = 100, Status = RecoveryStatus.WellRecovered },
                    LatestVoiceScores = null,
                    ComplexityLevel = level
                },
                // Svak dimensjon ⇒ fokus-basert pool på nivået.
                new ExerciseRecommendationInput
                {
                    Recovery = new RecoveryResult { Score = 90, Status = RecoveryStatus.WellRecovered },
                    LatestVoiceScores = new VoiceIntelligenceTrendPoint
                    {
                        SessionId = 1,
                        ResonanceScore100 = 20, ComfortScore100 = 70, ConsistencyScore100 = 70,
                        IntonationScore100 = 70, VocalWeightScore100 = 70, RecoveryScore100 = 70,
                        PitchScore100 = 70, CompositeVoiceScore = 60
                    },
                    ComplexityLevel = level
                }
            };

            foreach (var input in inputs)
            {
                var result = engine.RecommendNext(input);

                Assert.InRange(result.ExerciseId, CatalogMinId, CatalogMaxId);
                Assert.All(result.AlternativeExerciseIds,
                    id => Assert.InRange(id, CatalogMinId, CatalogMaxId));
            }
        }

        [Fact]
        public void LearningPathBuilder_OnlyRecommendsCatalogIds_ForEveryLevel()
        {
            var builder = new LearningPathProfileBuilder();
            var recovery = new RecoveryResult { Score = 80, Status = RecoveryStatus.Adequate, Explanation = "test" };

            // En svak dimensjon tvinger fram fokus-baserte anbefalinger på nivået.
            var trend = new[]
            {
                new VoiceIntelligenceTrendPoint
                {
                    SessionId = 1,
                    ResonanceScore100 = 30, ComfortScore100 = 40, ConsistencyScore100 = 35,
                    IntonationScore100 = 45, VocalWeightScore100 = 50, RecoveryScore100 = 50,
                    PitchScore100 = 55, CompositeVoiceScore = 42
                }
            };

            foreach (SpeechComplexityLevel level in Enum.GetValues(typeof(SpeechComplexityLevel)))
            {
                var complexity = new ComplexityEvaluation { CurrentLevel = level };
                var profile = builder.Build(trend, recovery, complexity);

                Assert.All(profile.RecommendedExercises,
                    r => Assert.InRange(r.ExerciseId, CatalogMinId, CatalogMaxId));
            }
        }

        [Theory]
        [MemberData(nameof(AllComplexityLevels))]
        public void ThreeIdSources_AreInSync_AndEveryLevelIsNonEmptyWithinCatalog(SpeechComplexityLevel level)
        {
            // De tre kildene skal partisjonere de samme reelle katalog-id-ene per nivå.
            var fromComplexity = ComplexityIdsForLevel(level);
            var fromRecommendation = RecommendationIdsForLevel(level);
            var fromLearningPath = LearningPathIdsForLevel(level);

            // Hver kilde non-empty og innenfor katalog-rommet.
            foreach (var (name, ids) in new[]
                     {
                         ("ComplexityEngine", fromComplexity),
                         ("ExerciseRecommendationEngine", fromRecommendation),
                         ("LearningPathProfileBuilder", fromLearningPath)
                     })
            {
                Assert.True(ids.Count > 0, $"{name}: level {level} produced an EMPTY id bucket.");
                Assert.All(ids, id => Assert.InRange(id, CatalogMinId, CatalogMaxId));
            }

            // I SYNC: identiske id-sett på tvers av de tre kildene.
            Assert.Equal(fromComplexity.OrderBy(x => x), fromRecommendation.OrderBy(x => x));
            Assert.Equal(fromComplexity.OrderBy(x => x), fromLearningPath.OrderBy(x => x));
        }

        [Fact]
        public void EveryEmittedId_AcrossAllLevels_HasACatalogExercise_AndAllLevelsAreCovered()
        {
            // Ingen emittert id mangler en katalog-øvelse (1–15), og alle 7 nivåene dekkes.
            var union = new HashSet<int>();
            foreach (SpeechComplexityLevel level in Enum.GetValues(typeof(SpeechComplexityLevel)))
            {
                var ids = ComplexityIdsForLevel(level);
                Assert.True(ids.Count > 0, $"Level {level} has no exercises.");
                foreach (var id in ids)
                    union.Add(id);
            }

            Assert.All(union, id => Assert.InRange(id, CatalogMinId, CatalogMaxId));
        }

        // ── Id-kilde-aksessorer ──────────────────────────────────────────────────────

        private static List<int> ComplexityIdsForLevel(SpeechComplexityLevel level)
        {
            // ComplexityEngine konstruerer en DatabaseService i ctor; vi reflekterer derfor
            // direkte på den PRIVATE, rene partisjons-helperen (CatalogIdsForLevel) som
            // GetExerciseIdsForComplexity bygger på — ingen DB nødvendig.
            var method = typeof(ComplexityEngine).GetMethod(
                "CatalogIdsForLevel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var ids = (System.Collections.IEnumerable)method!.Invoke(null, new object[] { level })!;
            return ids.Cast<int>().ToList();
        }

        private static List<int> RecommendationIdsForLevel(SpeechComplexityLevel level)
        {
            var method = typeof(ExerciseRecommendationEngine).GetMethod(
                "ExerciseIdsForComplexity", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var ids = (System.Collections.IEnumerable)method!.Invoke(null, new object[] { level })!;
            return ids.Cast<int>().ToList();
        }

        private static List<int> LearningPathIdsForLevel(SpeechComplexityLevel level)
        {
            var method = typeof(LearningPathProfileBuilder).GetMethod(
                "ExerciseIdsForLevel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var ids = (System.Collections.IEnumerable)method!.Invoke(null, new object[] { level })!;
            return ids.Cast<int>().ToList();
        }

        // ── RESX-lasting (samme oppdagelses-mønster som GuidanceCompletenessTests) ────

        private static Dictionary<string, string> LoadNeutralResourceValues()
        {
            var resourceDirectory = FindRepoSubdirectory(Path.Combine("FemVoiceStudio", "Resources"));
            var neutralResourcePath = Path.Combine(resourceDirectory, "Strings.resx");
            var document = XDocument.Load(neutralResourcePath);

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in document.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
            {
                var name = (string?)element.Attribute("name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                map[name] = element.Element("value")?.Value ?? string.Empty;
            }

            return map;
        }

        // ── Seed-kilde-lesing (source-level lock for seedede ProfileType-er) ──────────

        private static string ReadExerciseDataServiceSource()
        {
            var dir = FindRepoSubdirectory(Path.Combine("FemVoiceStudio", "Data"));
            return File.ReadAllText(Path.Combine(dir, "ExerciseDataService.cs"));
        }

        /// <summary>
        /// Trekker ut tekst-blokken for én seedet øvelse via dens unike SortOrder-linje,
        /// fram til neste SortOrder/blokk-slutt — slik at ProfileType-asserten gjelder
        /// nøyaktig den øvelsen og ikke en nabo.
        /// </summary>
        private static string ExtractExerciseBlock(string source, int sortOrder)
        {
            var match = Regex.Match(
                source,
                $@"SortOrder\s*=\s*{sortOrder}\s*,(?<body>.*?)(?:SortOrder\s*=\s*\d+\s*,|\}};\s*$)",
                RegexOptions.Singleline);
            Assert.True(match.Success, $"Could not locate seeded exercise block for SortOrder {sortOrder}.");
            return match.Groups["body"].Value;
        }

        private static string FindRepoSubdirectory(string relative)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException($"Could not find {relative} from test output path.");
        }
    }
}
