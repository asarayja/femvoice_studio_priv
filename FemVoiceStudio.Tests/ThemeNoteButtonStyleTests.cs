using System;
using System.IO;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint G Addendum — Voice Analysis note-selector theming (static guards).
    ///
    /// Assert the shared NoteRadioButtonStyle exists in both themes with all readable states and
    /// theme brushes, and that the Analyzer code-behind no longer paints note buttons with
    /// per-frequency red/pink colours. Pure file/text checks (no WPF runtime); compile here, run
    /// on Windows. VISUAL contrast confirmation is a separate manual checklist.
    /// </summary>
    public class ThemeNoteButtonStyleTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FemVoiceStudio.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static string ReadTheme(string file)
            => File.ReadAllText(Path.Combine(RepoRoot(), "FemVoiceStudio", "Themes", file));

        public static TheoryData<string> Themes => new() { "DarkTheme.xaml", "LightTheme.xaml" };

        [Theory]
        [MemberData(nameof(Themes))]
        public void NoteRadioButtonStyle_ExistsAndCoversAllStates(string theme)
        {
            var xaml = ReadTheme(theme);
            Assert.Contains("x:Key=\"NoteRadioButtonStyle\"", xaml);
            // The block from the style key to its close must cover every interactive state.
            var start = xaml.IndexOf("x:Key=\"NoteRadioButtonStyle\"", StringComparison.Ordinal);
            var block = xaml.Substring(start, Math.Min(2600, xaml.Length - start));
            Assert.Contains("Property=\"IsMouseOver\"", block);
            Assert.Contains("Property=\"IsPressed\"", block);
            Assert.Contains("Property=\"IsChecked\"", block);
            Assert.Contains("<MultiTrigger>", block);             // selected + hover
            Assert.Contains("Property=\"IsKeyboardFocused\"", block);
            Assert.Contains("Property=\"IsEnabled\"", block);
        }

        [Theory]
        [MemberData(nameof(Themes))]
        public void NoteRadioButtonStyle_UsesThemeBrushes(string theme)
        {
            var start = ReadTheme(theme).IndexOf("x:Key=\"NoteRadioButtonStyle\"", StringComparison.Ordinal);
            var block = ReadTheme(theme).Substring(start, 2600);
            Assert.Contains("AccentPrimaryBrush", block);     // selected fill
            Assert.Contains("TextOnAccentBrush", block);      // selected text
            Assert.Contains("BackgroundTertiaryBrush", block); // unselected surface
            Assert.Contains("BorderFocusBrush", block);       // focus
        }

        // The note buttons must no longer be coloured per-frequency in code-behind.
        [Fact]
        public void Analyzer_NoteButtons_UseSharedStyle_NotPerFrequencyColors()
        {
            var cs = File.ReadAllText(Path.Combine(RepoRoot(), "FemVoiceStudio", "Views", "AnalyzerWindow.xaml.cs"));
            Assert.Contains("NoteRadioButtonStyle", cs);
            Assert.DoesNotContain("GetFrequencyColor(", cs);   // the per-note red/pink palette is gone
        }
    }
}
