using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Localization expansion guard — validates ONLY the newly added language files
    /// (nl-NL, pl-PL, tr-TR, uk-UA, ro-RO, cs-CZ, hu-HU, el-GR, ar) against the
    /// Norwegian/default + English sources. Pure file/text checks (no WPF runtime);
    /// compiles here, run on Windows. Existing resource files are read for comparison
    /// only — never modified by these tests.
    /// </summary>
    public class NewLanguageResourcesTests
    {
        private static readonly string[] NewCultures =
            { "nl-NL", "pl-PL", "tr-TR", "uk-UA", "ro-RO", "cs-CZ", "hu-HU", "el-GR", "ar" };

        private static readonly string[] Mojibake = { "Ã", "Â", "â€™", "â€“", "�" };

        private static readonly Regex Placeholder = new(@"\{[^}]*\}", RegexOptions.Compiled);
        private static readonly Regex Glob = new(@"\*\.\w+", RegexOptions.Compiled);

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FemVoiceStudio.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static string ResourcesDir() => Path.Combine(RepoRoot(), "FemVoiceStudio", "Resources");

        private static Dictionary<string, string> LoadResx(string fileName)
        {
            var doc = XDocument.Load(Path.Combine(ResourcesDir(), fileName));
            var map = new Dictionary<string, string>();
            foreach (var d in doc.Root!.Elements("data"))
            {
                var name = d.Attribute("name")?.Value;
                if (name is null) continue;
                map[name] = d.Element("value")?.Value ?? string.Empty;
            }
            return map;
        }

        private static Dictionary<string, (List<string> ph, List<string> globs, int pipes)> Signatures(Dictionary<string, string> src)
        {
            var sig = new Dictionary<string, (List<string>, List<string>, int)>();
            foreach (var (k, v) in src)
                sig[k] = (Placeholder.Matches(v).Select(m => m.Value).OrderBy(x => x).ToList(),
                          Glob.Matches(v).Select(m => m.Value).OrderBy(x => x).ToList(),
                          v.Count(c => c == '|'));
            return sig;
        }

        public static TheoryData<string> Cultures
        {
            get { var d = new TheoryData<string>(); foreach (var c in NewCultures) d.Add(c); return d; }
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void NewFile_Exists_And_IsValidXml(string culture)
        {
            var path = Path.Combine(ResourcesDir(), $"Strings.{culture}.resx");
            Assert.True(File.Exists(path), $"missing new resource file: Strings.{culture}.resx");
            var ex = Record.Exception(() => XDocument.Load(path));
            Assert.Null(ex);
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void NewFile_HasFullKeyCoverage(string culture)
        {
            var nb = LoadResx("Strings.resx");
            var target = LoadResx($"Strings.{culture}.resx");
            var missing = nb.Keys.Where(k => !target.ContainsKey(k)).ToList();
            var extra = target.Keys.Where(k => !nb.ContainsKey(k)).ToList();
            Assert.True(missing.Count == 0, $"{culture}: {missing.Count} missing keys (e.g. {string.Join(", ", missing.Take(5))})");
            Assert.True(extra.Count == 0, $"{culture}: {extra.Count} unexpected keys (e.g. {string.Join(", ", extra.Take(5))})");
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void NewFile_HasNoEmptyValues_And_NoMojibake(string culture)
        {
            var target = LoadResx($"Strings.{culture}.resx");
            foreach (var (k, v) in target)
            {
                Assert.False(string.IsNullOrWhiteSpace(v), $"{culture}: empty value for {k}");
                foreach (var m in Mojibake)
                    Assert.False(v.Contains(m), $"{culture}: mojibake '{m}' in {k}");
            }
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void NewFile_PreservesPlaceholdersPipesAndGlobs(string culture)
        {
            var src = Signatures(LoadResx("Strings.resx"));
            var target = LoadResx($"Strings.{culture}.resx");
            foreach (var (k, want) in src)
            {
                if (!target.TryGetValue(k, out var v)) continue; // coverage covered elsewhere
                var ph = Placeholder.Matches(v).Select(m => m.Value).OrderBy(x => x).ToList();
                var globs = Glob.Matches(v).Select(m => m.Value).OrderBy(x => x).ToList();
                var pipes = v.Count(c => c == '|');
                Assert.True(ph.SequenceEqual(want.ph), $"{culture}: placeholder mismatch in {k}");
                Assert.True(globs.SequenceEqual(want.globs), $"{culture}: glob/extension mismatch in {k}");
                Assert.True(pipes == want.pipes, $"{culture}: pipe-count mismatch in {k}");
            }
        }

        [Fact]
        public void NoRussianResourceFile_WasAdded()
        {
            var files = Directory.GetFiles(ResourcesDir(), "*.resx").Select(Path.GetFileName);
            Assert.DoesNotContain(files, f => f!.Contains("ru-RU") || f!.Contains(".ru.") || f!.EndsWith("ru.resx"));
        }
    }
}
