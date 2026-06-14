using System.Text.RegularExpressions;
using Xunit;

namespace FemVoiceStudio.Tests;

public sealed class ThemeResourceCoverageTests
{
    private static readonly string[] RequiredBrushKeys =
    [
        "TextPrimaryBrush",
        "TextSecondaryBrush",
        "TextDisabledBrush",
        "TextOnAccentBrush",
        "BackgroundPrimaryBrush",
        "BackgroundSecondaryBrush",
        "BackgroundTertiaryBrush",
        "BackgroundCardBrush",
        "BackgroundHoverBrush",
        "BorderPrimaryBrush",
        "BorderFocusBrush",
        "SuccessBrush",
        "SuccessHoverBrush",
        "WarningBrush",
        "WarningHoverBrush",
        "ErrorBrush",
        "ErrorHoverBrush",
        "InfoBrush",
        "InfoHoverBrush",
        "ButtonHoverOverlayBrush",
        "ButtonPressedOverlayBrush",
        "HealthWarningBackgroundBrush",
        "HealthWarningTextBrush",
        "ChartBackgroundBrush",
        "ChartSurfaceBackgroundBrush",
        "ChartPlotAreaBackgroundBrush",
        "ChartAxisTextBrush",
        "ChartAxisLineBrush",
        "ChartGridBrush",
        "ChartGridLineBrush",
        "ChartMinorGridLineBrush",
        "ChartLegendTextBrush",
        "ChartTitleTextBrush",
        "ChartMarkerStrokeBrush",
        "ChartEmptyStateTextBrush",
        "ChartTargetAreaBrush",
        "ChartPitchBrush",
        "ChartResonanceBrush",
        "ChartIntonationBrush",
        "ChartVoiceHealthBrush",
        "DashboardStatisticsBrush",
        "DashboardStatisticsHoverBrush",
        "DashboardGuideBrush",
        "DashboardGuideHoverBrush",
        "DashboardAnalyzerBrush",
        "DashboardAnalyzerHoverBrush",
        "DashboardSettingsBrush",
        "DashboardSettingsHoverBrush",
        "AnalyzerSpectrogramLowBrush",
        "AnalyzerSpectrogramForwardBrush",
        "AnalyzerSpectrogramHighBrush",
        "AnalyzerDebugPanelBrush",
        "AnalyzerDebugTextBrush",
        "AnalyzerRangeVeryLowBrush",
        "AnalyzerRangeLowBrush",
        "AnalyzerRangeMiddleBrush",
        "AnalyzerRangeUpperBrush",
        "AnalyzerRangeVeryHighBrush",
        "AnalyzerTargetLineBrush",
        "AnalyzerMainFrequencyBrush",
        "AnalyzerForwardZoneBrush",
        "AnalyzerF1Brush",
        "AnalyzerF2Brush",
        "AnalyzerF3Brush",
        "AnalyzerSpectrogramLabelBackgroundBrush"
    ];

    [Fact]
    public void LightAndDarkThemesExposeRequiredUiBrushes()
    {
        var root = FindRepositoryRoot();
        var themeFiles = new[]
        {
            Path.Combine(root, "FemVoiceStudio", "Themes", "LightTheme.xaml"),
            Path.Combine(root, "FemVoiceStudio", "Themes", "DarkTheme.xaml")
        };

        foreach (var themeFile in themeFiles)
        {
            Assert.True(File.Exists(themeFile), $"Missing theme file: {themeFile}");
            var xaml = File.ReadAllText(themeFile);

            foreach (var key in RequiredBrushKeys)
            {
                Assert.Contains($"x:Key=\"{key}\"", xaml);
            }
        }
    }

    [Fact]
    public void ComboBoxAndListBoxThemeTemplates_CoverReadableInteractiveStates()
    {
        var root = FindRepositoryRoot();
        var themeFiles = new[]
        {
            Path.Combine(root, "FemVoiceStudio", "Themes", "LightTheme.xaml"),
            Path.Combine(root, "FemVoiceStudio", "Themes", "DarkTheme.xaml")
        };

        foreach (var themeFile in themeFiles)
        {
            var xaml = File.ReadAllText(themeFile);

            Assert.Contains("x:Key=\"StandardComboBoxStyle\"", xaml);
            Assert.Contains("x:Key=\"StandardComboBoxItemStyle\"", xaml);
            Assert.Contains("x:Key=\"StandardListBoxItemStyle\"", xaml);

            Assert.Contains("ControlTemplate TargetType=\"ToggleButton\"", xaml);
            Assert.Contains("ControlTemplate TargetType=\"ComboBoxItem\"", xaml);
            Assert.Contains("ControlTemplate TargetType=\"ListBoxItem\"", xaml);

            Assert.Contains("IsMouseOver", xaml);
            Assert.Contains("IsSelected", xaml);
            Assert.Contains("IsHighlighted", xaml);
            Assert.Contains("IsKeyboardFocusWithin", xaml);
            Assert.Contains("IsEnabled", xaml);

            Assert.Contains("BackgroundHoverBrush", xaml);
            Assert.Contains("AccentPrimaryBrush", xaml);
            Assert.Contains("AccentSecondaryBrush", xaml);
            Assert.Contains("TextPrimaryBrush", xaml);
            Assert.Contains("TextOnAccentBrush", xaml);
            Assert.Contains("TextDisabledBrush", xaml);

            Assert.DoesNotContain("SystemColors.HighlightBrushKey", xaml);
            Assert.DoesNotContain("SystemColors.HighlightTextBrushKey", xaml);
            Assert.DoesNotContain("SystemColors.InactiveSelectionHighlightBrushKey", xaml);
            Assert.DoesNotContain("SystemColors.InactiveSelectionHighlightTextBrushKey", xaml);
            Assert.DoesNotContain("Default ToggleButton hover", xaml, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SettingsAndManualOverrideButtons_DoNotOverrideThemeColorsLocally()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "FemVoiceStudio", "Views", "SettingsWindow.xaml"),
            Path.Combine(root, "FemVoiceStudio", "Views", "ManualOverrideWindow.xaml")
        };

        var localButtonColor = new Regex(
            @"<Button\b[^>]*(?:Background|Foreground|BorderBrush)\s*=",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var violations = files
            .SelectMany(path => File.ReadLines(path).Select((line, index) => new { path, line, index }))
            .Where(item => localButtonColor.IsMatch(item.line))
            .Select(item => $"{Path.GetRelativePath(root, item.path)}:{item.index + 1}: {item.line.Trim()}")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Settings/Manual Override buttons must use shared theme styles, not local colors:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ViewXamlDoesNotIntroduceUnexpectedHardcodedUiColors()
    {
        var root = FindRepositoryRoot();
        var viewsRoot = Path.Combine(root, "FemVoiceStudio", "Views");
        var hexColor = new Regex("#[0-9A-Fa-f]{3,8}", RegexOptions.Compiled);
        var namedUiColor = new Regex(
            "(Foreground|Background|BorderBrush|Fill|Stroke)=\"(Red|Green|Blue|Gray|Black|White)\"",
            RegexOptions.Compiled);

        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Static light/dark preview swatches in onboarding, not active UI theme surfaces.
            "FirstTimeSetupWindow.xaml"
        };

        var violations = Directory.EnumerateFiles(viewsRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !allowedFiles.Contains(Path.GetFileName(path)))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => new { path, line, index }))
            .Where(item => hexColor.IsMatch(item.line) || namedUiColor.IsMatch(item.line))
            .Select(item => $"{Path.GetRelativePath(root, item.path)}:{item.index + 1}: {item.line.Trim()}")
            .ToArray();

        Assert.True(violations.Length == 0, "Unexpected hardcoded UI colors:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "FemVoiceStudio"))
                && Directory.Exists(Path.Combine(directory.FullName, "FemVoiceStudio.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate FemVoice Studio repository root.");
    }
}
