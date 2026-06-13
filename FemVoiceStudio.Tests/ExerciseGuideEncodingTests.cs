using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FemVoiceStudio.Data;
using FemVoiceStudio.Models;
using FemVoiceStudio.Services;
using Xunit;

namespace FemVoiceStudio.Tests
{
    [Collection("Localization")]
    public sealed class ExerciseGuideEncodingTests
    {
        private static readonly string[] MojibakePatterns =
        {
            "Ã",
            "Â",
            "�",
            "â€™",
            "â€“",
            "talesprÃ",
            "OverfÃ",
            "Ã¸",
            "Ã¥",
            "Ã¦"
        };

        private static readonly Regex TextFileIoCall = new(
            @"File\.(?:ReadAllText|WriteAllText|WriteAllLines|ReadLines)\s*\((?<args>[\s\S]*?)\);|new\s+StreamReader\s*\((?<streamArgs>[\s\S]*?)\);",
            RegexOptions.Compiled);

        [Fact]
        public void ExerciseGuide_NorwegianText_NoMojibake_OnInitialLoad()
        {
            LocalizationService.Instance.SetLanguage("nb");
            using var catalog = TestExerciseCatalog.Create();

            var exercises = Localize(catalog.Service.GetAllExercises());
            var text = UserVisibleExerciseText(exercises);

            AssertNoMojibake(text);
            Assert.Contains("talespråk", text);
            Assert.Contains("Overføring", text);
            Assert.Contains("øvelse", text);
            Assert.Contains("større", text);
            Assert.Contains("målområdet", text);
        }

        [Fact]
        public void ExerciseGuide_NorwegianText_NoMojibake_AfterPracticeNavigation()
        {
            LocalizationService.Instance.SetLanguage("nb");
            using var catalog = TestExerciseCatalog.Create();

            var practiceExercises = Localize(FilterPractice(catalog.Service.GetAllExercises()));
            var text = UserVisibleExerciseText(practiceExercises);

            AssertNoMojibake(text);
            Assert.Contains("talespråk", text);
            Assert.Contains("Overføring", text);
            Assert.Contains("øvelse", text);
            Assert.Contains("større", text);
            Assert.Contains("må", text);
        }

        [Fact]
        public void ExerciseGuide_NorwegianText_NoMojibake_AfterReturnFromPractice()
        {
            LocalizationService.Instance.SetLanguage("nb");
            using var catalog = TestExerciseCatalog.Create();

            _ = Localize(FilterPractice(catalog.Service.GetAllExercises()));
            var returnedExercises = Localize(catalog.Service.GetAllExercises());
            var text = UserVisibleExerciseText(returnedExercises);

            AssertNoMojibake(text);
            Assert.Contains("samtalesituasjoner", text);
            Assert.Contains("variasjon", text);
            Assert.Contains("intonasjon", text);
        }

        [Fact]
        public void ExerciseGuide_NorwegianText_PreservesNorwegianLetters()
        {
            LocalizationService.Instance.SetLanguage("nb");
            using var catalog = TestExerciseCatalog.Create();

            var exercises = Localize(catalog.Service.GetAllExercises());
            var text = UserVisibleExerciseText(exercises);

            Assert.Contains('æ', text);
            Assert.Contains('ø', text);
            Assert.Contains('å', text);
            Assert.Contains('Ø', text);
            Assert.Contains('Å', text);
            Assert.Contains("Æøå", LocalizationService.Instance.GetString("Exercise_20_Content"));
            AssertNoMojibake(text);
        }

        [Fact]
        public void ExerciseGuide_NoMojibakePatternsInUserVisibleDescriptions()
        {
            using var catalog = TestExerciseCatalog.Create();

            var rawSeedText = UserVisibleExerciseText(catalog.Service.GetAllExercises());
            var localizedText = UserVisibleExerciseText(Localize(catalog.Service.GetAllExercises()));

            AssertNoMojibake(rawSeedText);
            AssertNoMojibake(localizedText);
        }

        [Fact]
        public void ResourceFiles_NoMojibake_All12Resx()
        {
            var files = ActiveResourceFiles().ToArray();

            Assert.Equal(12, files.Length);

            var failures = new List<string>();
            foreach (var file in files)
            {
                foreach (var (key, value) in LoadResxValues(file))
                {
                    if (ContainsMojibake(value))
                        failures.Add($"{Path.GetFileName(file)}:{key} => {value}");
                }
            }

            Assert.True(
                failures.Count == 0,
                "Mojibake found in active resource files:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void StaticTextFiles_ReadAsUtf8()
        {
            var failures = new List<string>();

            foreach (var file in ProductionSourceFiles())
            {
                var text = File.ReadAllText(file, System.Text.Encoding.UTF8);
                foreach (Match match in TextFileIoCall.Matches(text))
                {
                    var args = match.Groups["args"].Success
                        ? match.Groups["args"].Value
                        : match.Groups["streamArgs"].Value;

                    if (!args.Contains("Encoding.UTF8", StringComparison.Ordinal))
                        failures.Add($"{RelativeToSource(file)} uses text file IO without explicit UTF-8: {match.Value.Split('\n')[0].Trim()}");
                }
            }

            Assert.True(
                failures.Count == 0,
                "Text/static file IO must specify UTF-8:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void NoEncodingDefaultForUserFacingText()
        {
            var failures = new List<string>();
            var banned = new Regex(@"Encoding\.(?:Default|ASCII|GetEncoding\s*\()", RegexOptions.Compiled);

            foreach (var file in ProductionSourceFiles())
            {
                if (Path.GetFileName(file).Equals("IconMapping.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = File.ReadAllText(file, System.Text.Encoding.UTF8);
                if (banned.IsMatch(text))
                    failures.Add(RelativeToSource(file));
            }

            Assert.True(
                failures.Count == 0,
                "User-facing text code must not use default, ASCII, or ANSI encodings:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        private static List<Exercise> Localize(IEnumerable<Exercise> exercises)
        {
            var localizer = new ExerciseGuideTextLocalizer();
            var list = exercises.ToList();
            foreach (var exercise in list)
                localizer.Apply(exercise);
            return list;
        }

        private static IEnumerable<Exercise> FilterPractice(IEnumerable<Exercise> exercises)
        {
            return exercises.Where(exercise =>
            {
                var category = exercise.Category.ToLowerInvariant();
                return category.Contains("avansert", StringComparison.Ordinal)
                       || category.Contains("praksis", StringComparison.Ordinal);
            });
        }

        private static string UserVisibleExerciseText(IEnumerable<Exercise> exercises)
        {
            return string.Join(
                Environment.NewLine,
                exercises.Select(exercise => string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        exercise.Name,
                        exercise.Description,
                        exercise.Category,
                        exercise.FrequencyText,
                        exercise.DisplayDifficulty,
                        exercise.ScientificRationale
                    }
                    .Concat(exercise.DisplaySteps)
                    .Concat(exercise.GetStepsList()))));
        }

        private static void AssertNoMojibake(string text)
        {
            var failures = MojibakePatterns
                .Where(pattern => text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.True(
                failures.Length == 0,
                "Mojibake patterns found: " + string.Join(", ", failures));
        }

        private static bool ContainsMojibake(string value)
            => MojibakePatterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        private static IEnumerable<(string Key, string Value)> LoadResxValues(string file)
        {
            return XDocument.Load(file)
                .Root!
                .Elements("data")
                .Select(element => (
                    (string?)element.Attribute("name") ?? string.Empty,
                    element.Element("value")?.Value ?? string.Empty));
        }

        private static IEnumerable<string> ActiveResourceFiles()
        {
            return Directory.EnumerateFiles(ResourceDirectory(), "*.resx")
                .Where(file => !Path.GetFileName(file).Equals("Strings.resx.old", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> ProductionSourceFiles()
        {
            return Directory.EnumerateFiles(SourceDirectory(), "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);
        }

        private static string ResourceDirectory() => Path.Combine(SourceDirectory(), "Resources");

        private static string RelativeToSource(string file)
            => Path.GetRelativePath(SourceDirectory(), file);

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

        private sealed class TestExerciseCatalog : IDisposable
        {
            private readonly string _directory;

            private TestExerciseCatalog(string directory, ExerciseDataService service)
            {
                _directory = directory;
                Service = service;
            }

            public ExerciseDataService Service { get; }

            public static TestExerciseCatalog Create()
            {
                var directory = Path.Combine(Path.GetTempPath(), "FemVoiceStudio.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                var databasePath = Path.Combine(directory, "exercise-guide.db");

                using (new DatabaseService(databasePath))
                {
                }

                var service = new ExerciseDataService($"Data Source={databasePath}");
                service.InitializeExercises();
                return new TestExerciseCatalog(directory, service);
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(_directory, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup; test isolation uses a unique temp directory.
                }
            }
        }
    }
}
