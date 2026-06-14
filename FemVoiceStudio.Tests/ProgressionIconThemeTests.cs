using System;
using System.IO;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint G Addendum — Progresjon-side icon theming (static guards).
    ///
    /// Assert the Progression page renders its icons as themed Segoe MDL2 glyphs (level badge,
    /// today's-focus, quickest-improvement, parameter direction arrows) instead of colour emojis
    /// that ignored Foreground / matched the circle colour, and that the direction converter now
    /// emits Segoe glyphs. Pure file/text checks (no WPF runtime); compile here, run on Windows.
    /// Visual contrast is a separate manual checklist.
    /// </summary>
    public class ProgressionIconThemeTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FemVoiceStudio.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static string Read(string relative)
            => File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

        [Fact]
        public void Progression_Icons_AreThemedSegoeGlyphs_NotEmoji()
        {
            var xaml = Read("FemVoiceStudio/Views/ProgressionDashboard.xaml");

            // Icons render with the icon font so the bound Foreground actually applies.
            Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", xaml);
            // The three circle icons are explicit glyphs (level badge / focus shield / improvement).
            Assert.Contains("&#xE735;", xaml);   // level badge
            Assert.Contains("&#xEA18;", xaml);   // today's focus (shield)
            Assert.Contains("&#xE74A;", xaml);   // quickest improvement (up)

            // The colour-emoji bindings / literals are gone from the icon slots.
            Assert.DoesNotContain("Text=\"{Binding LevelEmoji}\"", xaml);
            Assert.DoesNotContain("Text=\"{Binding FocusAreaIcon}\"", xaml);
            Assert.DoesNotContain("Text=\"\U0001F4C8\"", xaml);   // the 📈 literal

            // No hardcoded black icon colour leaked in.
            Assert.DoesNotContain("Foreground=\"Black\"", xaml);
            Assert.DoesNotContain("Foreground=\"#000000\"", xaml);
        }

        [Fact]
        public void Progression_CircleIcons_UseOnAccentForeground()
        {
            var xaml = Read("FemVoiceStudio/Views/ProgressionDashboard.xaml");
            // Icons sitting on coloured circles use the on-accent brush for contrast.
            Assert.Contains("Foreground=\"{DynamicResource TextOnAccentBrush}\"", xaml);
        }

        [Fact]
        public void DirectionToArrowConverter_EmitsSegoeGlyphs_NotEmoji()
        {
            var cs = Read("FemVoiceStudio/Converters/Converters.cs");
            Assert.Contains("\\uE74A", cs);   // up glyph
            Assert.Contains("\\uE74B", cs);   // down glyph
            // The converter no longer returns an emoji arrow.
            Assert.DoesNotContain("=> \"⬆\"", cs);
            Assert.DoesNotContain("=> \"➡\"", cs);
        }

        [Theory]
        [InlineData("DarkTheme.xaml")]
        [InlineData("LightTheme.xaml")]
        public void Themes_DefineOnAccentBrush(string theme)
            => Assert.Contains("x:Key=\"TextOnAccentBrush\"", Read($"FemVoiceStudio/Themes/{theme}"));
    }
}
