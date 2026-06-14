using System.Text.RegularExpressions;
using Xunit;

namespace FemVoiceStudio.Tests;

public sealed class AnalysisChartThemeTests
{
    [Fact]
    public void ChartThemeResources_ExistInDarkAndLightThemes()
    {
        var root = FindRepositoryRoot();
        var themeFiles = new[]
        {
            Path.Combine(root, "FemVoiceStudio", "Themes", "DarkTheme.xaml"),
            Path.Combine(root, "FemVoiceStudio", "Themes", "LightTheme.xaml")
        };

        var required = new[]
        {
            "ChartSurfaceBackgroundBrush",
            "ChartPlotAreaBackgroundBrush",
            "ChartAxisTextBrush",
            "ChartAxisLineBrush",
            "ChartGridLineBrush",
            "ChartMinorGridLineBrush",
            "ChartLegendTextBrush",
            "ChartTitleTextBrush",
            "ChartMarkerStrokeBrush",
            "ChartEmptyStateTextBrush"
        };

        foreach (var themeFile in themeFiles)
        {
            var xaml = File.ReadAllText(themeFile);
            foreach (var key in required)
            {
                Assert.Contains($"x:Key=\"{key}\"", xaml);
            }
        }
    }

    [Fact]
    public void DarkTheme_AnalysisChartSurfaces_AreNotPureWhite()
    {
        var darkTheme = ReadSource("FemVoiceStudio", "Themes", "DarkTheme.xaml");

        Assert.DoesNotContain("ChartSurfaceBackgroundColor\">#FFFFFF", darkTheme);
        Assert.DoesNotContain("ChartPlotAreaBackgroundColor\">#FFFFFF", darkTheme);
        Assert.Contains("ChartAxisTextColor", darkTheme);
        Assert.Contains("ChartGridLineBrush", darkTheme);
    }

    [Fact]
    public void AnalysisCharts_UseSharedOxyPlotThemeMapping()
    {
        var viewModel = ReadSource("FemVoiceStudio", "ViewModels", "AnalysisPageViewModel.cs");
        var codeBehind = ReadSource("FemVoiceStudio", "Views", "AnalysisWindow.xaml.cs");

        Assert.Contains("AnalysisChartTheme.Apply(model)", viewModel);
        Assert.Contains("AnalysisChartTheme.Apply(model)", codeBehind);
        Assert.Contains("AnalysisChartTheme.ApplyAll", codeBehind);

        Assert.DoesNotContain("Background = OxyColors.White", viewModel);
        Assert.DoesNotContain("Background = OxyColors.White", codeBehind);
        Assert.DoesNotContain("PlotAreaBackground = OxyColors.White", viewModel);
        Assert.DoesNotContain("PlotAreaBackground = OxyColors.White", codeBehind);
    }

    [Fact]
    public void AnalysisWindow_AllPlotViews_AreInvalidatedOnThemeChange()
    {
        var xaml = ReadSource("FemVoiceStudio", "Views", "AnalysisWindow.xaml");
        var codeBehind = ReadSource("FemVoiceStudio", "Views", "AnalysisWindow.xaml.cs");
        var plotViewNames = Regex.Matches(xaml, "x:Name=\"(?<name>[A-Za-z0-9]+PlotView)\"")
            .Select(match => match.Groups["name"].Value)
            .Distinct()
            .ToArray();

        Assert.True(plotViewNames.Length >= 14);
        Assert.Contains("ThemeManager.Instance.ThemeChanged += OnThemeChanged", codeBehind);
        Assert.Contains("ThemeManager.Instance.ThemeChanged -= OnThemeChanged", codeBehind);

        foreach (var plotViewName in plotViewNames)
        {
            Assert.Contains($"{plotViewName}.InvalidatePlot(true)", codeBehind);
        }
    }

    [Fact]
    public void AnalysisChartThemeMapping_SetsAxisTextGridAndLegendColors()
    {
        var mapper = ReadSource("FemVoiceStudio", "Services", "AnalysisChartTheme.cs");

        Assert.Contains("model.PlotAreaBackground = theme.PlotAreaColor;", mapper);
        Assert.Contains("model.TextColor = theme.AxisTextColor;", mapper);
        Assert.Contains("legend.LegendTextColor = theme.LegendTextColor;", mapper);
        Assert.Contains("axis.TextColor = theme.AxisTextColor;", mapper);
        Assert.Contains("axis.AxislineColor = theme.AxisLineColor;", mapper);
        Assert.Contains("axis.TicklineColor = theme.AxisLineColor;", mapper);
        Assert.Contains("axis.MajorGridlineColor = theme.GridLineColor;", mapper);
        Assert.Contains("axis.MinorGridlineColor = theme.MinorGridLineColor;", mapper);
    }

    private static string ReadSource(params string[] segments)
        => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FemVoiceStudio.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
