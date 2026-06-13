using System;
using System.IO;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint G Addendum — ComboBox/DropDown dark-mode readability hardening (static guards).
    ///
    /// These parse the theme XAML and view XAML as text and assert the global ComboBoxItem
    /// styles cover every readable state in BOTH themes, that the brush keys they reference are
    /// defined, and that no view re-introduces an un-templated local ComboBoxItem style (which
    /// falls back to WPF's default light system highlight — the reported bug). Pure file/text
    /// checks (no WPF runtime); they compile here but run on Windows (net10.0-windows test host).
    /// VISUAL confirmation is a separate manual checklist — these do NOT assert pixels.
    /// </summary>
    public class ThemeComboBoxStyleTests
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

        // The ComboBoxItem template must explicitly handle every state the brief requires.
        [Theory]
        [MemberData(nameof(Themes))]
        public void ComboBoxItemStyle_CoversAllReadableStates(string theme)
        {
            var xaml = ReadTheme(theme);
            Assert.Contains("x:Key=\"StandardComboBoxItemStyle\"", xaml);
            Assert.Contains("Property=\"IsMouseOver\"", xaml);          // hover
            Assert.Contains("Property=\"IsHighlighted\"", xaml);        // keyboard navigation
            Assert.Contains("Property=\"IsSelected\"", xaml);           // selected
            Assert.Contains("<MultiTrigger>", xaml);                    // selected + hover/highlight
            Assert.Contains("Property=\"IsEnabled\"", xaml);            // disabled item
        }

        // Every brush key the hardened ComboBox styles reference must be defined in each theme.
        [Theory]
        [MemberData(nameof(Themes))]
        public void ComboBoxStyle_BrushKeys_AreDefined(string theme)
        {
            var xaml = ReadTheme(theme);
            foreach (var key in new[]
            {
                "BackgroundCardBrush", "BackgroundHoverBrush", "TextPrimaryBrush",
                "TextOnAccentBrush", "TextDisabledBrush", "AccentPrimaryBrush",
                "AccentSecondaryBrush", "BorderFocusBrush"
            })
                Assert.Contains($"x:Key=\"{key}\"", xaml);
        }

        // Hover/keyboard never pair a light surface with low-contrast text: the templates set a
        // readable Foreground in those triggers (regression guard against the reported bug).
        [Theory]
        [MemberData(nameof(Themes))]
        public void ComboBoxItem_HoverAndHighlight_SetReadableForeground(string theme)
        {
            var xaml = ReadTheme(theme);
            // Both hover and keyboard triggers use the hover surface AND set a foreground.
            Assert.Contains("Value=\"{StaticResource BackgroundHoverBrush}\"", xaml);
            Assert.Contains("Value=\"{StaticResource TextPrimaryBrush}\"", xaml);
            Assert.Contains("Value=\"{StaticResource TextOnAccentBrush}\"", xaml);
        }

        // Hardcoded-style guard: no view may re-introduce a local ComboBoxItem style (it would
        // shadow the themed style and fall back to the default light system highlight).
        [Fact]
        public void NoView_DefinesLocalComboBoxItemStyle()
        {
            var viewsDir = Path.Combine(RepoRoot(), "FemVoiceStudio", "Views");
            foreach (var file in Directory.GetFiles(viewsDir, "*.xaml"))
            {
                var xaml = File.ReadAllText(file);
                Assert.False(
                    xaml.Contains("TargetType=\"ComboBoxItem\"") || xaml.Contains("TargetType=\"{x:Type ComboBoxItem}\""),
                    $"{Path.GetFileName(file)} defines a local ComboBoxItem style; remove it so the themed style applies.");
            }
        }
    }
}
