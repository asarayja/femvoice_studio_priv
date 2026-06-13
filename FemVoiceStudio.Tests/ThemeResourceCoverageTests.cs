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
        "HealthWarningBackgroundBrush",
        "HealthWarningTextBrush",
        "ChartBackgroundBrush",
        "ChartGridBrush",
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
