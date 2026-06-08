using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using FemVoiceStudio.Models;
using Xunit;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// GUIDANCE COMPLETENESS (must-fix-regression).
    ///
    /// Låser den kliniske invarianten at HVER øvelse alltid eksponerer alle fire
    /// guidance-dimensjonene — Clinical Purpose + Physical Focus + Common Mistakes +
    /// Safety — slik at ingen dimensjon kan forsvinne stille fra
    /// <c>ExerciseDetailViewModel.RebuildGuidanceItems</c>.
    ///
    /// Metode: reflection over ALLE public static fabrikkmetoder på
    /// <see cref="ExerciseTargetProfile"/> som returnerer en
    /// <see cref="ExerciseTargetProfile"/> (ResonanceExercise / PitchExercise /
    /// IntonationExercise / StrawPhonation / CreateResonanceHumming /
    /// CreateResonanceVowels / CreateCoordinatedGlideUp / CreateStabilityTraining).
    /// For hver profil asserteres at de fire nøklene er ikke-null og ikke-tomme OG at
    /// de finnes som ikke-whitespace &lt;value&gt; i Resources/Strings.resx (lastet via
    /// XDocument, samme mønster som ResourceTextPolicyTests).
    ///
    /// Reflection-tilnærmingen fanger også eventuelle NYE fabrikkmetoder automatisk:
    /// legges en øvelse til uten guidance, feiler denne testen umiddelbart.
    /// </summary>
    public class GuidanceCompletenessTests
    {
        // Navnene på de fire klinisk-sikkerhets-dimensjonene + en accessor som henter
        // nøkkelen fra en profil. Holder testen i takt med RebuildGuidanceItems.
        private static readonly (string Dimension, Func<ExerciseTargetProfile, string?> Key)[] GuidanceDimensions =
        {
            ("ClinicalPurpose", p => p.ClinicalPurposeKey),
            ("PhysicalFocus",   p => p.PhysicalFocusKey),
            ("CommonMistakes",  p => p.CommonMistakesKey),
            ("Safety",          p => p.SafetyInfoKey),
        };

        // ── Fabrikk-discovery via reflection ─────────────────────────────────────

        private static IReadOnlyList<MethodInfo> ProfileFactories()
        {
            var factories = typeof(ExerciseTargetProfile)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.ReturnType == typeof(ExerciseTargetProfile))
                // Kun fabrikker som kan kalles med default-argumenter (alle har dem i dag).
                .Where(m => m.GetParameters().All(p => p.IsOptional || p.HasDefaultValue))
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ToList();

            return factories;
        }

        private static ExerciseTargetProfile Invoke(MethodInfo factory)
        {
            var args = factory.GetParameters()
                .Select(p => p.HasDefaultValue ? p.DefaultValue : Type.Missing)
                .ToArray();
            var profile = factory.Invoke(null, args) as ExerciseTargetProfile;
            Assert.NotNull(profile);
            return profile!;
        }

        public static IEnumerable<object[]> AllFactories()
            => ProfileFactories().Select(m => new object[] { m.Name });

        private static MethodInfo FactoryByName(string name)
            => ProfileFactories().Single(m => m.Name == name);

        // ── Tester ───────────────────────────────────────────────────────────────

        [Fact]
        public void Discovery_FindsEveryKnownExerciseFactory()
        {
            // Sikrer at reflection-discoveryen faktisk treffer de forventede øvelsene —
            // ellers ville en tom liste gi en falsk grønn suite.
            var names = ProfileFactories().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);

            string[] expected =
            {
                "ResonanceExercise",
                "PitchExercise",
                "IntonationExercise",
                "StrawPhonation",
                "CreateResonanceHumming",
                "CreateResonanceVowels",
                "CreateCoordinatedGlideUp",
                "CreateStabilityTraining",
            };

            foreach (var name in expected)
                Assert.Contains(name, names);

            // Minst de åtte kjente øvelsene må være funnet.
            Assert.True(names.Count >= expected.Length,
                $"Expected at least {expected.Length} profile factories, found {names.Count}: {string.Join(", ", names)}");
        }

        [Theory]
        [MemberData(nameof(AllFactories))]
        public void EveryProfile_DeclaresAllFourGuidanceKeys_NonNullNonEmpty(string factoryName)
        {
            var profile = Invoke(FactoryByName(factoryName));

            foreach (var (dimension, key) in GuidanceDimensions)
            {
                var value = key(profile);
                Assert.False(
                    string.IsNullOrWhiteSpace(value),
                    $"{factoryName}: guidance dimension '{dimension}' key is null/empty — " +
                    "a clinical-safety dimension would silently disappear from the guidance panel.");
            }
        }

        [Theory]
        [MemberData(nameof(AllFactories))]
        public void EveryProfile_GuidanceKeys_ResolveToNonWhitespaceResxValues(string factoryName)
        {
            var profile = Invoke(FactoryByName(factoryName));
            var resourceValues = LoadNeutralResourceValues();

            foreach (var (dimension, keyAccessor) in GuidanceDimensions)
            {
                var key = keyAccessor(profile);
                Assert.False(string.IsNullOrWhiteSpace(key),
                    $"{factoryName}: guidance dimension '{dimension}' key is null/empty.");

                Assert.True(
                    resourceValues.TryGetValue(key!, out var value),
                    $"{factoryName}: guidance key '{key}' ({dimension}) is missing from Strings.resx.");

                Assert.False(
                    string.IsNullOrWhiteSpace(value),
                    $"{factoryName}: guidance key '{key}' ({dimension}) resolves to whitespace/empty text in Strings.resx.");
            }
        }

        [Fact]
        public void AllProfiles_Aggregate_HaveCompleteGuidanceCoverage()
        {
            // Aggregert kryss-sjekk: bygger en samlet feilliste over ALLE øvelser × alle
            // fire dimensjoner i én test, slik at en regresjon rapporterer hver mangel
            // samtidig (ikke bare den første). Speiler den kliniske kontrakten som
            // ExerciseDetailViewModel.AssertGuidanceDimensionsComplete håndhever i DEBUG.
            var resourceValues = LoadNeutralResourceValues();
            var failures = new List<string>();

            foreach (var factory in ProfileFactories())
            {
                var profile = Invoke(factory);
                foreach (var (dimension, keyAccessor) in GuidanceDimensions)
                {
                    var key = keyAccessor(profile);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        failures.Add($"{factory.Name}.{dimension} => <missing key>");
                        continue;
                    }

                    if (!resourceValues.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                        failures.Add($"{factory.Name}.{dimension} => key '{key}' missing/blank in Strings.resx");
                }
            }

            Assert.True(
                failures.Count == 0,
                "Incomplete guidance coverage — every exercise must expose Clinical Purpose, " +
                "Physical Focus, Common Mistakes and Safety:" + Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        // ── RESX-lasting (samme oppdagelses-mønster som ResourceTextPolicyTests) ──

        private static Dictionary<string, string> LoadNeutralResourceValues()
        {
            var resourceDirectory = FindResourceDirectory();
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
