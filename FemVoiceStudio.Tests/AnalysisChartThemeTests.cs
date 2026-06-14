using System.Text.RegularExpressions;
using FemVoiceStudio.Services;
using FemVoiceStudio.ViewModels;
using FemVoiceStudio.Views;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
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

        Assert.Contains("AnalysisChartTheme.Apply(", viewModel);
        Assert.Contains("AnalysisChartTheme.Apply(", codeBehind);
        Assert.Contains("AnalysisChartTheme.ApplyAnalysisResonanceBounds", viewModel);
        Assert.Contains("AnalysisChartTheme.ApplyAnalysisPitchBounds", viewModel);
        Assert.Contains("AnalysisChartTheme.ApplyAnalysisIntonationBounds", viewModel);
        Assert.Contains("AnalysisChartTheme.ApplyScoreChartBounds", viewModel);
        Assert.Contains("AnalysisChartTheme.ApplyAll", codeBehind);

        Assert.DoesNotContain("Background = OxyColors.White", viewModel);
        Assert.DoesNotContain("Background = OxyColors.White", codeBehind);
        Assert.DoesNotContain("PlotAreaBackground = OxyColors.White", viewModel);
        Assert.DoesNotContain("PlotAreaBackground = OxyColors.White", codeBehind);
    }

    [Fact]
    public void DybdeanalyseCharts_HaveBoundedZoomAndPanAxes()
    {
        var viewModel = new AnalysisPageViewModel(database: null, analyticsStore: null);
        var models = new[]
        {
            viewModel.ResonancePlotModel,
            viewModel.PitchPlotModel,
            viewModel.IntonationPlotModel,
            viewModel.HealthPlotModel,
            viewModel.ComfortPlotModel,
            viewModel.RecoveryPlotModel,
            viewModel.ConsistencyPlotModel,
            viewModel.VocalWeightPlotModel,
            viewModel.VoiceDevelopmentPlotModel,
            viewModel.WeeklyTrendPlotModel,
            viewModel.MonthlyTrendPlotModel,
            viewModel.VoiceDevelopmentLongPlotModel,
            viewModel.BreakthroughsPlotModel,
            viewModel.RecoveryPatternsPlotModel
        };

        foreach (var model in models)
        {
            AssertAllAxesBounded(model);
        }
    }

    [Fact]
    public void ResonanceAnalysisCharts_HaveDarkThemeAndBoundedZoomPanAxes()
    {
        var viewModel = new ResonanceChartViewModel();

        Assert.NotEqual(OxyColors.White, viewModel.F1F2ScatterModel.Background);
        Assert.NotEqual(OxyColors.White, viewModel.F1F2ScatterModel.PlotAreaBackground);
        Assert.NotEqual(OxyColors.White, viewModel.FormantTimelineModel.Background);
        Assert.NotEqual(OxyColors.White, viewModel.FormantTimelineModel.PlotAreaBackground);

        AssertAllAxesBounded(viewModel.F1F2ScatterModel);
        AssertAllAxesBounded(viewModel.FormantTimelineModel);
    }

    [Fact]
    public void ResonanceAnalysis_F1F2Points_FollowAxisMeaning()
    {
        var viewModel = new ResonanceChartViewModel();

        viewModel.AddFormantPoint(f1: 330, f2: 2200, f3: 3000, resonanceScore: 75);

        var formantSeries = Assert.IsType<ScatterSeries>(viewModel.F1F2ScatterModel.Series[0]);
        var point = Assert.Single(formantSeries.Points);
        Assert.Equal(2200, point.X);
        Assert.Equal(330, point.Y);

        var xAxis = viewModel.F1F2ScatterModel.Axes.Single(a => a.Position == AxisPosition.Bottom);
        var yAxis = viewModel.F1F2ScatterModel.Axes.Single(a => a.Position == AxisPosition.Left);
        Assert.Equal(AnalysisChartTheme.F2PlacementAbsoluteMinimumHz, xAxis.AbsoluteMinimum);
        Assert.Equal(AnalysisChartTheme.F2PlacementAbsoluteMaximumHz, xAxis.AbsoluteMaximum);
        Assert.Equal(AnalysisChartTheme.F1PlacementAbsoluteMinimumHz, yAxis.AbsoluteMinimum);
        Assert.Equal(AnalysisChartTheme.F1PlacementAbsoluteMaximumHz, yAxis.AbsoluteMaximum);
    }

    [Fact]
    public void ResonanceAnalysis_UsesSharedThemeAndThemeChangeRefresh()
    {
        var chartViewModel = ReadSource("FemVoiceStudio", "Views", "ResonanceChartViewModel.cs");
        var window = ReadSource("FemVoiceStudio", "Views", "ResonanceWindow.xaml.cs");

        Assert.Contains("AnalysisChartTheme.Apply(", chartViewModel);
        Assert.Contains("AnalysisChartTheme.ApplyF1F2PlacementBounds", chartViewModel);
        Assert.Contains("AnalysisChartTheme.ApplyFormantTimelineBounds", chartViewModel);
        Assert.DoesNotContain("Background = OxyColors.White", chartViewModel);
        Assert.DoesNotContain("PlotAreaBackground = OxyColors.White", chartViewModel);

        Assert.Contains("ThemeManager.Instance.ThemeChanged += OnThemeChanged", window);
        Assert.Contains("ThemeManager.Instance.ThemeChanged -= OnThemeChanged", window);
        Assert.Contains("F1F2PlotView.InvalidatePlot(true)", window);
        Assert.Contains("TimelinePlotView.InvalidatePlot(true)", window);
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

    private static void AssertAllAxesBounded(PlotModel model)
    {
        Assert.NotEmpty(model.Axes);

        foreach (var axis in model.Axes)
        {
            Assert.True(axis.AbsoluteMaximum > axis.AbsoluteMinimum, $"{model.Title} {axis.Title} missing absolute pan bounds.");
            Assert.True(axis.MinimumRange > 0, $"{model.Title} {axis.Title} missing minimum zoom range.");
            Assert.True(axis.MaximumRange > 0, $"{model.Title} {axis.Title} missing maximum zoom range.");
            Assert.True(axis.MaximumRange >= axis.MinimumRange, $"{model.Title} {axis.Title} has invalid zoom range.");
        }
    }

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
