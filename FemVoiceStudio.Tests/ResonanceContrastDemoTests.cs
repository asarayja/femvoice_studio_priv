using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Optional Resonance Contrast demo — feature-safety + localization guards.
    ///
    /// Verifies the demo is fully localized across every active .resx (no empty values, no
    /// mojibake, Norwegian diacritics preserved), introduces no scoring/progression/audio, and
    /// adds no Russian resource. Pure file/text checks (no WPF runtime); compile here, run on
    /// Windows. The window opening cleanly is a manual visual check.
    /// </summary>
    public class ResonanceContrastDemoTests
    {
        private static readonly string[] Keys =
        {
            "ResonanceContrast_Title", "ResonanceContrast_Subtitle", "ResonanceContrast_Description",
            "ResonanceContrast_StepLarge", "ResonanceContrast_StepSmall", "ResonanceContrast_NoticeDifference",
            "ResonanceContrast_Safety", "ResonanceContrast_StartButton", "ResonanceContrast_StopButton",
            "ResonanceContrast_OptionalNote", "ResonanceContrast_BigDogSmallDogNote",
            "ResonanceContrast_NoScoreNote", "ResonanceContrast_NotRequiredNote"
        };

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FemVoiceStudio.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static string ResourcesDir() => Path.Combine(RepoRoot(), "FemVoiceStudio", "Resources");

        private static string Value(string xaml, string key)
        {
            var m = Regex.Match(xaml, $"name=\"{key}\"[^>]*>\\s*<value>(.*?)</value>", RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        public static TheoryData<string> ResxFiles
        {
            get
            {
                var data = new TheoryData<string>();
                foreach (var f in Directory.GetFiles(ResourcesDir(), "*.resx").OrderBy(x => x))
                    data.Add(Path.GetFileName(f));
                return data;
            }
        }

        [Theory]
        [MemberData(nameof(ResxFiles))]
        public void EveryResxFile_HasAllKeys_NonEmpty_NoMojibake(string file)
        {
            var xaml = File.ReadAllText(Path.Combine(ResourcesDir(), file));
            foreach (var key in Keys)
            {
                var v = Value(xaml, key);
                Assert.False(string.IsNullOrWhiteSpace(v), $"{file}: {key} missing or empty");
                // Common double-encoding (mojibake) lead artifacts must not appear.
                Assert.DoesNotContain("Ã", v);
                Assert.DoesNotContain("Â", v);
                Assert.DoesNotContain("�", v);   // replacement character
            }
        }

        [Fact]
        public void Norwegian_PreservesDiacritics()
        {
            var nb = File.ReadAllText(Path.Combine(ResourcesDir(), "Strings.resx"));
            // The Norwegian copy genuinely contains æ/ø/å (e.g. "Prøv", "større", "påvirker").
            var joined = string.Concat(Keys.Select(k => Value(nb, k)));
            Assert.True(joined.IndexOfAny(new[] { 'æ', 'ø', 'å', 'Æ', 'Ø', 'Å' }) >= 0,
                "Norwegian ResonanceContrast copy lost its diacritics");
        }

        [Theory]
        [InlineData("Strings.en.resx")]
        [InlineData("Strings_en.resx")]
        public void English_ValuesPresent(string file)
        {
            var en = File.ReadAllText(Path.Combine(ResourcesDir(), file));
            Assert.Contains("Resonance Contrast", Value(en, "ResonanceContrast_Title"));
            Assert.False(string.IsNullOrWhiteSpace(Value(en, "ResonanceContrast_Safety")));
        }

        [Fact]
        public void NoRussianResourceFile_WasAdded()
        {
            var files = Directory.GetFiles(ResourcesDir(), "*.resx").Select(Path.GetFileName);
            Assert.DoesNotContain(files, f => f!.Contains("ru-RU") || f!.Contains("ru.resx") || f!.Contains(".ru."));
        }

        // The demo is content-only: its code-behind must not touch audio/scoring/progression.
        [Fact]
        public void DemoWindow_HasNoScoringAudioOrProgression()
        {
            var cs = File.ReadAllText(Path.Combine(RepoRoot(), "FemVoiceStudio", "Views", "ResonanceContrastDemoWindow.xaml.cs"));
            foreach (var forbidden in new[] { "Score", "Progression", "ExerciseId", "AudioCapture", "StartRecording", "PitchDetection", "Resonance" + "ProxyEngine" })
                Assert.DoesNotContain(forbidden, cs);
        }

        // The demo UI text is localized (loc keys), not hardcoded strings.
        [Fact]
        public void DemoWindow_UsesLocalizationKeys()
        {
            var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "FemVoiceStudio", "Views", "ResonanceContrastDemoWindow.xaml"));
            Assert.Contains("ResonanceContrast_Title", xaml);
            Assert.Contains("ResonanceContrast_StepLarge", xaml);
            Assert.Contains("ResonanceContrast_Safety", xaml);
            Assert.Contains("ResonanceContrast_BigDogSmallDogNote", xaml);
        }
    }
}
