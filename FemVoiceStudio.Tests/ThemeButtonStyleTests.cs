using System;
using System.IO;
using Xunit;
using Assert = Xunit.Assert;

namespace FemVoiceStudio.Tests
{
    /// <summary>
    /// Sprint G Addendum — Button hover/readability hardening (static guards).
    ///
    /// Assert both themes provide a global implicit Button style + keyed button styles with
    /// readable hover/pressed/focus/disabled, that the overlay brushes exist, and that the
    /// previously-broken Settings/Manual-adjustments buttons now use themed styles rather than
    /// bare inline backgrounds on the default WPF template. Pure file/text checks (no WPF
    /// runtime); compile here, run on Windows. VISUAL confirmation is a separate manual checklist.
    /// </summary>
    public class ThemeButtonStyleTests
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

        private static string ReadView(string file)
            => File.ReadAllText(Path.Combine(RepoRoot(), "FemVoiceStudio", "Views", file));

        public static TheoryData<string> Themes => new() { "DarkTheme.xaml", "LightTheme.xaml" };

        // Both themes must provide a global implicit Button style (fixes bare <Button>s that fell
        // back to WPF's default light template) plus the keyed accent/secondary/danger styles.
        [Theory]
        [MemberData(nameof(Themes))]
        public void Themes_DefineGlobalAndKeyedButtonStyles(string theme)
        {
            var xaml = ReadTheme(theme);
            Assert.Contains("<Style TargetType=\"Button\">", xaml);            // implicit/global
            Assert.Contains("x:Key=\"PrimaryButtonStyle\"", xaml);
            Assert.Contains("x:Key=\"SecondaryButtonStyle\"", xaml);
            Assert.Contains("x:Key=\"DangerButtonStyle\"", xaml);
        }

        // The implicit Button template must cover every interactive state readably.
        [Theory]
        [MemberData(nameof(Themes))]
        public void GlobalButtonStyle_CoversAllStates(string theme)
        {
            var xaml = ReadTheme(theme);
            Assert.Contains("Property=\"IsMouseOver\"", xaml);
            Assert.Contains("Property=\"IsPressed\"", xaml);
            Assert.Contains("Property=\"IsKeyboardFocused\"", xaml);
            Assert.Contains("Property=\"IsEnabled\"", xaml);
        }

        // The hover/pressed overlay brushes the global button template references must exist.
        [Theory]
        [MemberData(nameof(Themes))]
        public void Themes_DefineButtonOverlayBrushes(string theme)
        {
            var xaml = ReadTheme(theme);
            Assert.Contains("x:Key=\"ButtonHoverOverlayBrush\"", xaml);
            Assert.Contains("x:Key=\"ButtonPressedOverlayBrush\"", xaml);
        }

        // Danger buttons must have a VISIBLE hover (regression guard: was Error->Error, a no-op).
        [Theory]
        [MemberData(nameof(Themes))]
        public void DangerButton_HasVisibleHover(string theme)
            => Assert.Contains("ErrorHoverBrush", ReadTheme(theme));

        // The named problem pages must use themed button styles, not inline-background buttons on
        // the default template (no leftover local Button.Template / Button.Style on Settings).
        [Fact]
        public void SettingsPage_UsesThemedButtonStyles_NoLocalButtonTemplate()
        {
            var xaml = ReadView("SettingsWindow.xaml");
            Assert.Contains("PrimaryButtonStyle", xaml);
            Assert.Contains("DangerButtonStyle", xaml);
            Assert.Contains("SecondaryButtonStyle", xaml);
            Assert.DoesNotContain("<Button.Template>", xaml);
            Assert.DoesNotContain("<Button.Style>", xaml);
        }

        [Fact]
        public void ManualAdjustmentsPage_ApplyButton_IsThemed()
            => Assert.Contains("PrimaryButtonStyle", ReadView("ManualOverrideWindow.xaml"));
    }
}
