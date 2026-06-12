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
        private static readonly Regex Mojibake = new(@"Ã|Â|�|\uFFFD", RegexOptions.Compiled);
        private static readonly Regex UnsafeExerciseCopy = new(
            @"\b(avsløre stemmen|feil intonasjon|passe inn|tvinger frem|kritisk å rette|du er dehydrert|dårlig stemme|bad voice|diagnostisert med)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [Fact]
        public void UserFacingNorwegianText_DoesNotContainMojibake()
        {
            var failures = new List<string>();

            foreach (var file in PrimaryResourceFiles())
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
