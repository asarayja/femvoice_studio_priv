using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace FemVoiceStudio.Tests
{
    public class ResourceTextPolicyTests
    {
        private static readonly Regex ExplicitHzValue = new(@"\b\d+(?:[.,]\d+)?(?:\s*[-+]\s*\d+(?:[.,]\d+)?)?\s*Hz\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LocalizationMethodKey = new(@"\bGet(?:Formatted)?String\(\s*""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex LocalizationIndexerKey = new(@"(?:LocalizationService\.Instance|loc|Service|locService)\s*\[\s*""([^""]+)""\s*\]", RegexOptions.Compiled);
        private static readonly Regex UnsafeUserFacingVoiceCopy = new(
            @"\b(mannlig stemme|male voice|ikke feminin nok|not feminine enough|passing|push harder|snakk høyere|speak louder|du må høyere|you must go higher|prosjekter stemmen|project your voice)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SpeechInstructionCopy = new(@"\b(snakk|snakke|tale|speak|speech)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly HashSet<string> QualitativeExerciseAndMilestoneKeys = new()
        {
            "Exercise_103_Steps",
            "Exercise_104_Rationale",
            "Exercise_104_Steps",
            "Exercise_105_Rationale",
            "Exercise_105_Steps",
            "Exercise_109_Steps",
            "Exercise_112_Steps",
            "Progression_MilestonePitch165",
            "Progression_MilestonePitch180",
            "Progression_MilestonePitch200",
        };

        [Fact]
        public void QualitativeExerciseAndMilestoneResources_DoNotExposeExplicitHzValues()
        {
            var resourceDirectory = FindResourceDirectory();
            var failures = new List<string>();

            foreach (var file in Directory.EnumerateFiles(resourceDirectory, "*.resx"))
            {
                var document = XDocument.Load(file);
                var values = document.Root?
                    .Elements("data")
                    .Where(element => QualitativeExerciseAndMilestoneKeys.Contains((string?)element.Attribute("name") ?? string.Empty))
                    .Select(element => new
                    {
                        Key = (string?)element.Attribute("name") ?? string.Empty,
                        Value = element.Element("value")?.Value ?? string.Empty
                    }) ?? Enumerable.Empty<dynamic>();

                foreach (var item in values)
                {
                    if (ExplicitHzValue.IsMatch(item.Value))
                    {
                        failures.Add($"{Path.GetFileName(file)}:{item.Key} => {item.Value}");
                    }
                }
            }

            Assert.True(failures.Count == 0, "Explicit Hz values found in qualitative resource text:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void ReferencedLocalizationKeys_ExistInNeutralResources()
        {
            var sourceDirectory = FindSourceDirectory();
            var resourceDirectory = FindResourceDirectory();
            var neutralResourcePath = Path.Combine(resourceDirectory, "Strings.resx");
            var resourceKeys = LoadResourceKeys(neutralResourcePath);
            var referencedKeys = FindReferencedLocalizationKeys(sourceDirectory);

            var missingKeys = referencedKeys
                .Where(key => !resourceKeys.Contains(key))
                .OrderBy(key => key)
                .ToList();

            Assert.True(
                missingKeys.Count == 0,
                "Localization keys referenced from code but missing in Strings.resx:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, missingKeys));
        }

        [Fact]
        public void UserFacingResources_DoNotUseUnsafeVoicePressureCopy()
        {
            var resourceDirectory = FindResourceDirectory();
            var failures = new List<string>();

            foreach (var file in Directory.EnumerateFiles(resourceDirectory, "*.resx"))
            {
                var document = XDocument.Load(file);
                var values = document.Root?
                    .Elements("data")
                    .Select(element => new
                    {
                        Key = (string?)element.Attribute("name") ?? string.Empty,
                        Value = element.Element("value")?.Value ?? string.Empty
                    }) ?? Enumerable.Empty<dynamic>();

                foreach (var item in values)
                {
                    if (UnsafeUserFacingVoiceCopy.IsMatch(item.Value))
                    {
                        failures.Add($"{Path.GetFileName(file)}:{item.Key} => {item.Value}");
                    }
                }
            }

            Assert.True(
                failures.Count == 0,
                "Unsafe pressure/shame voice copy found in user-facing resources:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void HummingResources_DoNotUseSpeechModeInstructions()
        {
            var resourceDirectory = FindResourceDirectory();
            var failures = new List<string>();

            foreach (var file in Directory.EnumerateFiles(resourceDirectory, "*.resx"))
            {
                var document = XDocument.Load(file);
                var values = document.Root?
                    .Elements("data")
                    .Select(element => new
                    {
                        Key = (string?)element.Attribute("name") ?? string.Empty,
                        Value = element.Element("value")?.Value ?? string.Empty
                    })
                    .Where(item => item.Key.Contains("ResonanceHumming", StringComparison.OrdinalIgnoreCase))
                    ?? Enumerable.Empty<dynamic>();

                foreach (var item in values)
                {
                    if (SpeechInstructionCopy.IsMatch(item.Value))
                    {
                        failures.Add($"{Path.GetFileName(file)}:{item.Key} => {item.Value}");
                    }
                }
            }

            Assert.True(
                failures.Count == 0,
                "Humming resources should describe humming/sound, not speech-mode instructions:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
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

        private static string FindSourceDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "FemVoiceStudio");
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "FemVoiceStudio.csproj")))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find FemVoiceStudio source directory from test output path.");
        }

        private static HashSet<string> LoadResourceKeys(string path)
        {
            var document = XDocument.Load(path);
            return document.Root?
                .Elements("data")
                .Select(element => (string?)element.Attribute("name") ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);
        }

        private static HashSet<string> FindReferencedLocalizationKeys(string sourceDirectory)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var excludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bin",
                "obj"
            };

            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Split(Path.DirectorySeparatorChar).Any(part => excludedDirectories.Contains(part)))
                    continue;

                var text = File.ReadAllText(file);
                foreach (Match match in LocalizationMethodKey.Matches(text))
                    keys.Add(match.Groups[1].Value);
                foreach (Match match in LocalizationIndexerKey.Matches(text))
                    keys.Add(match.Groups[1].Value);
            }

            return keys;
        }
    }
}
