using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    [Collection("Localization")]
    public sealed class LocalizationAccessibilityRobustnessTests
    {
        private static readonly Regex Mojibake = new(@"Ã|Â|�|\uFFFD|â€™|â€œ|â€|â€¦|â€“|â€”|Ãƒ|Ã¢|Ã°", RegexOptions.Compiled);
        private static readonly Regex UnsafeExerciseCopy = new(
            @"\b(avsløre stemmen|feil intonasjon|passe inn|tvinger frem|kritisk å rette|du er dehydrert|dårlig stemme|bad voice|diagnostisert med)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ReleaseBetaWording = new(@"\bbeta\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [Fact]
        public void UserFacingNorwegianText_DoesNotContainMojibake()
        {
            var failures = new List<string>();

            foreach (var file in AllResourceFiles())
            {
                foreach (var (key, value) in LoadResxValues(file))
                {
                    if (Mojibake.IsMatch(value))
                        failures.Add($"{Path.GetFileName(file)}:{key} => {value}");
                }
            }

            var exerciseSource = File.ReadAllText(Path.Combine(SourceDirectory(), "Services", "VoiceFeminizationExerciseService.cs"));
            if (Mojibake.IsMatch(exerciseSource))
                failures.Add("VoiceFeminizationExerciseService.cs contains mojibake");

            Assert.True(
                failures.Count == 0,
                "Mojibake found in user-facing localization text:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void AllResourceFiles_HaveSameKeysAndNoEmptyValues()
        {
            var files = AllResourceFiles().ToArray();
            var reference = LoadResxValues(Path.Combine(ResourceDirectory(), "Strings.resx"))
                .Select(item => item.Key)
                .ToHashSet(StringComparer.Ordinal);
            var failures = new List<string>();

            foreach (var file in files)
            {
                var values = LoadResxValues(file).ToArray();
                var keys = values.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
                var missing = reference.Except(keys).OrderBy(key => key).ToArray();
                var extra = keys.Except(reference).OrderBy(key => key).ToArray();
                var empty = values
                    .Where(item => string.IsNullOrWhiteSpace(item.Value))
                    .Select(item => item.Key)
                    .OrderBy(key => key)
                    .ToArray();

                if (missing.Length > 0)
                    failures.Add($"{Path.GetFileName(file)} missing: {string.Join(", ", missing)}");
                if (extra.Length > 0)
                    failures.Add($"{Path.GetFileName(file)} extra: {string.Join(", ", extra)}");
                if (empty.Length > 0)
                    failures.Add($"{Path.GetFileName(file)} empty: {string.Join(", ", empty)}");
            }

            Assert.True(
                failures.Count == 0,
                "Resource key/value coverage mismatch:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void ReleaseResourceCopy_DoesNotUseBetaWording()
        {
            var failures = new List<string>();

            foreach (var file in AllResourceFiles())
            {
                foreach (var (key, value) in LoadResxValues(file))
                {
                    if (ReleaseBetaWording.IsMatch(value))
                        failures.Add($"{Path.GetFileName(file)}:{key} => {value}");
                }
            }

            Assert.True(
                failures.Count == 0,
                "Release-facing resources must not use beta wording:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void EnhancedExerciseCopy_UsesSupportiveNonScaryWording()
        {
            var exercises = new VoiceFeminizationExerciseService().GetAllEnhancedExercises();
            var failures = new List<string>();

            foreach (var exercise in exercises)
            {
                var text = string.Join("\n",
                    new[] { exercise.Name, exercise.Description, exercise.ScientificRationale }
                        .Concat(exercise.Steps));

                if (UnsafeExerciseCopy.IsMatch(text))
                    failures.Add($"{exercise.Id}:{exercise.Name} => {text}");
            }

            Assert.True(
                failures.Count == 0,
                "Unsafe or scary exercise wording found:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void NorwegianTimelineLabels_DoNotExposeRawDayTokens()
        {
            LocalizationService.Instance.SetLanguage("nb");

            var labels = new[] { 7, 30, 90, 180 }
                .Select(days => LocalizationService.Instance.GetFormattedString("Report_TimeWindow_LastDays", days))
                .ToArray();
            var text = string.Join("\n", labels);

            Assert.Contains("Siste 7 dager", text);
            Assert.Contains("Siste 30 dager", text);
            Assert.Contains("Siste 90 dager", text);
            Assert.Contains("Siste 180 dager", text);
            Assert.DoesNotContain("7-day", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("30-day", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("90-day", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("180-day", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EnglishTimelineLabels_DoNotExposeRawDayTokens()
        {
            LocalizationService.Instance.SetLanguage("en");

            var labels = new[] { 7, 30, 90, 180 }
                .Select(days => LocalizationService.Instance.GetFormattedString("Report_TimeWindow_LastDays", days))
                .ToArray();
            var text = string.Join("\n", labels);

            Assert.Contains("Last 7 days", text);
            Assert.Contains("Last 30 days", text);
            Assert.Contains("Last 90 days", text);
            Assert.Contains("Last 180 days", text);
            Assert.DoesNotContain("7-day", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("30-day", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("90-day", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("180-day", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NorwegianPrimaryDimensionLabels_AreLocalizedNotRawEnglish()
        {
            var neutral = LoadResxValues(Path.Combine(ResourceDirectory(), "Strings.resx"))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

            Assert.Equal("Komfort", neutral["DimensionLabel_Comfort"]);
            Assert.Equal("Restitusjon", neutral["DimensionLabel_Recovery"]);
            Assert.Equal("Resonans", neutral["DimensionLabel_Resonance"]);
            Assert.Equal("Konsistens", neutral["DimensionLabel_Consistency"]);
            Assert.Equal("Intonasjon", neutral["DimensionLabel_Intonation"]);
            Assert.Equal("Stemmevekt", neutral["DimensionLabel_VocalWeight"]);
            Assert.Equal("Pitch", neutral["DimensionLabel_Pitch"]);
        }

        [Fact]
        public void OutcomePdfRecoveryCostLabel_UsesShortReadableNorwegianText()
        {
            LocalizationService.Instance.SetLanguage("nb");

            var label = LocalizationService.Instance.GetString("ReportPdf_RecoveryCostShort");
            var exportWriterSource = File.ReadAllText(Path.Combine(SourceDirectory(), "Services", "ExportWriter.cs"));

            Assert.Equal("Rest.kostnad", label);
            Assert.DoesNotContain("Restitusjonskostn/ad", label, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ReportPdf_RecoveryCostShort", exportWriterSource);
        }

        private static IEnumerable<(string Key, string Value)> LoadResxValues(string file)
        {
            return XDocument.Load(file)
                .Root!
                .Elements("data")
                .Select(element => (
                    (string?)element.Attribute("name") ?? string.Empty,
                    element.Element("value")?.Value ?? string.Empty));
        }

        private static string ResourceDirectory() => Path.Combine(SourceDirectory(), "Resources");

        private static IEnumerable<string> PrimaryResourceFiles()
        {
            var root = ResourceDirectory();
            yield return Path.Combine(root, "Strings.resx");
            yield return Path.Combine(root, "Strings.en.resx");
            yield return Path.Combine(root, "Strings_en.resx");
        }

        private static IEnumerable<string> AllResourceFiles()
        {
            return Directory.EnumerateFiles(ResourceDirectory(), "*.resx")
                .Where(file => !Path.GetFileName(file).Equals("Strings.resx.old", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);
        }

        private static string SourceDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "FemVoiceStudio");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "FemVoiceStudio.csproj")))
                    return candidate;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find FemVoiceStudio source directory from test output path.");
        }
    }
}
