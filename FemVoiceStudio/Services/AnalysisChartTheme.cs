using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;

namespace FemVoiceStudio.Services;

/// <summary>
/// Maps WPF theme brushes to OxyPlot colors for in-app analysis charts.
/// Report/PDF exports intentionally do not use this UI theme mapper.
/// </summary>
public static class AnalysisChartTheme
{
    public static PlotModel Apply(PlotModel model)
    {
        var theme = Current;

        model.Background = theme.SurfaceColor;
        model.PlotAreaBackground = theme.PlotAreaColor;
        model.TextColor = theme.AxisTextColor;
        model.TitleColor = theme.TitleTextColor;
        model.PlotAreaBorderColor = theme.AxisLineColor;

        foreach (var legend in model.Legends.OfType<LegendBase>())
        {
            legend.LegendTextColor = theme.LegendTextColor;
        }

        foreach (var axis in model.Axes)
        {
            Apply(axis, theme);
        }

        model.InvalidatePlot(false);
        return model;
    }

    public static void ApplyAll(IEnumerable<PlotModel?> models)
    {
        foreach (var model in models)
        {
            if (model != null)
                Apply(model);
        }
    }

    public static AnalysisChartThemeColors Current => new(
        SurfaceColor: GetOxyColor("ChartSurfaceBackgroundBrush", OxyColor.FromRgb(37, 40, 54)),
        PlotAreaColor: GetOxyColor("ChartPlotAreaBackgroundBrush", OxyColor.FromRgb(37, 40, 54)),
        AxisTextColor: GetOxyColor("ChartAxisTextBrush", OxyColor.FromRgb(192, 192, 192)),
        AxisLineColor: GetOxyColor("ChartAxisLineBrush", OxyColor.FromRgb(74, 80, 104)),
        GridLineColor: GetOxyColor("ChartGridLineBrush", OxyColor.FromRgb(61, 66, 89)),
        MinorGridLineColor: GetOxyColor("ChartMinorGridLineBrush", OxyColor.FromRgb(50, 55, 69)),
        LegendTextColor: GetOxyColor("ChartLegendTextBrush", OxyColor.FromRgb(192, 192, 192)),
        TitleTextColor: GetOxyColor("ChartTitleTextBrush", OxyColors.White),
        MarkerStrokeColor: GetOxyColor("ChartMarkerStrokeBrush", OxyColor.FromRgb(37, 40, 54)),
        EmptyStateTextColor: GetOxyColor("ChartEmptyStateTextBrush", OxyColor.FromRgb(192, 192, 192)));

    private static void Apply(Axis axis, AnalysisChartThemeColors theme)
    {
        axis.TextColor = theme.AxisTextColor;
        axis.TitleColor = theme.TitleTextColor;
        axis.AxislineColor = theme.AxisLineColor;
        axis.TicklineColor = theme.AxisLineColor;
        axis.MajorGridlineColor = theme.GridLineColor;
        axis.MinorGridlineColor = theme.MinorGridLineColor;
    }

    private static OxyColor GetOxyColor(string key, OxyColor fallback)
    {
        if (Application.Current?.TryFindResource(key) is SolidColorBrush brush)
        {
            var color = brush.Color;
            return OxyColor.FromArgb(color.A, color.R, color.G, color.B);
        }

        return fallback;
    }
}

public sealed record AnalysisChartThemeColors(
    OxyColor SurfaceColor,
    OxyColor PlotAreaColor,
    OxyColor AxisTextColor,
    OxyColor AxisLineColor,
    OxyColor GridLineColor,
    OxyColor MinorGridLineColor,
    OxyColor LegendTextColor,
    OxyColor TitleTextColor,
    OxyColor MarkerStrokeColor,
    OxyColor EmptyStateTextColor);
