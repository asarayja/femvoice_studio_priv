using System;
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
    public const double PitchAbsoluteMinimumHz = 50;
    public const double PitchAbsoluteMaximumHz = 500;
    public const double FormantAbsoluteMinimumHz = 0;
    public const double FormantAbsoluteMaximumHz = 3500;
    public const double F1PlacementAbsoluteMinimumHz = 100;
    public const double F1PlacementAbsoluteMaximumHz = 1200;
    public const double F2PlacementAbsoluteMinimumHz = 500;
    public const double F2PlacementAbsoluteMaximumHz = 3500;
    public const double ScoreAbsoluteMinimum = 0;
    public const double ScoreAbsoluteMaximum = 100;

    private const double DefaultIndexedMaximum = 30;
    private const double IndexedMinimumRange = 1;
    private const double PitchMinimumRangeHz = 20;
    private const double FormantMinimumRangeHz = 100;
    private const double PlacementF1MinimumRangeHz = 50;
    private const double PlacementF2MinimumRangeHz = 150;
    private const double ScoreMinimumRange = 10;
    private const double DefaultTimelineSeconds = 30;
    private const double TimelinePaddingSeconds = 2;
    private const double TimelineMinimumRangeSeconds = 5;
    private const double TimelineMaximumRangeSeconds = 300;

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

    public static PlotModel ApplyAnalysisResonanceBounds(PlotModel model, int pointCount)
    {
        ApplyIndexedXAxisBounds(model, pointCount);
        ApplyLinearAxisBounds(
            model,
            AxisPosition.Left,
            FormantAbsoluteMinimumHz,
            FormantAbsoluteMaximumHz,
            1000,
            2500,
            FormantMinimumRangeHz,
            FormantAbsoluteMaximumHz - FormantAbsoluteMinimumHz);
        return model;
    }

    public static PlotModel ApplyAnalysisPitchBounds(
        PlotModel model,
        int pointCount,
        double? visibleMinimum = null,
        double? visibleMaximum = null)
    {
        ApplyIndexedXAxisBounds(model, pointCount);
        var minimum = visibleMinimum ?? 100;
        var maximum = visibleMaximum ?? 350;
        EnsureMinimumSpan(ref minimum, ref maximum, PitchMinimumRangeHz, PitchAbsoluteMinimumHz, PitchAbsoluteMaximumHz);

        ApplyLinearAxisBounds(
            model,
            AxisPosition.Left,
            PitchAbsoluteMinimumHz,
            PitchAbsoluteMaximumHz,
            minimum,
            maximum,
            PitchMinimumRangeHz,
            PitchAbsoluteMaximumHz - PitchAbsoluteMinimumHz);
        return model;
    }

    public static PlotModel ApplyAnalysisIntonationBounds(PlotModel model, int pointCount)
    {
        ApplyIndexedXAxisBounds(model, pointCount);
        ApplyLinearAxisBounds(model, AxisPosition.Left, 0, 200, 0, 150, 20, 200);
        return model;
    }

    public static PlotModel ApplyScoreChartBounds(PlotModel model, int pointCount)
    {
        ApplyIndexedXAxisBounds(model, pointCount);
        ApplyLinearAxisBounds(
            model,
            AxisPosition.Left,
            ScoreAbsoluteMinimum,
            ScoreAbsoluteMaximum,
            ScoreAbsoluteMinimum,
            ScoreAbsoluteMaximum,
            ScoreMinimumRange,
            ScoreAbsoluteMaximum - ScoreAbsoluteMinimum);
        return model;
    }

    public static PlotModel ApplyF1F2PlacementBounds(PlotModel model)
    {
        ApplyLinearAxisBounds(
            model,
            AxisPosition.Bottom,
            F2PlacementAbsoluteMinimumHz,
            F2PlacementAbsoluteMaximumHz,
            800,
            3000,
            PlacementF2MinimumRangeHz,
            F2PlacementAbsoluteMaximumHz - F2PlacementAbsoluteMinimumHz);

        ApplyLinearAxisBounds(
            model,
            AxisPosition.Left,
            F1PlacementAbsoluteMinimumHz,
            F1PlacementAbsoluteMaximumHz,
            200,
            700,
            PlacementF1MinimumRangeHz,
            F1PlacementAbsoluteMaximumHz - F1PlacementAbsoluteMinimumHz);
        return model;
    }

    public static PlotModel ApplyFormantTimelineBounds(PlotModel model, IEnumerable<DataPoint>? timelinePoints = null)
    {
        ApplyDateTimeAxisBounds(model, timelinePoints);
        ApplyLinearAxisBounds(
            model,
            AxisPosition.Left,
            FormantAbsoluteMinimumHz,
            FormantAbsoluteMaximumHz,
            FormantAbsoluteMinimumHz,
            FormantAbsoluteMaximumHz,
            FormantMinimumRangeHz,
            FormantAbsoluteMaximumHz - FormantAbsoluteMinimumHz);
        ApplyLinearAxisBounds(
            model,
            AxisPosition.Right,
            ScoreAbsoluteMinimum,
            ScoreAbsoluteMaximum,
            ScoreAbsoluteMinimum,
            ScoreAbsoluteMaximum,
            ScoreMinimumRange,
            ScoreAbsoluteMaximum - ScoreAbsoluteMinimum,
            "ScoreAxis");
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

    private static void ApplyIndexedXAxisBounds(PlotModel model, int pointCount)
    {
        var axis = model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
        if (axis == null)
            return;

        var maximum = Math.Max(1, pointCount > 0 ? pointCount - 1 : DefaultIndexedMaximum);
        axis.AbsoluteMinimum = 0;
        axis.AbsoluteMaximum = maximum;
        axis.Minimum = 0;
        axis.Maximum = maximum;
        axis.MinimumRange = IndexedMinimumRange;
        axis.MaximumRange = maximum;
    }

    private static void ApplyDateTimeAxisBounds(PlotModel model, IEnumerable<DataPoint>? timelinePoints)
    {
        var axis = model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
        if (axis == null)
            return;

        var now = DateTimeAxis.ToDouble(DateTime.Now);
        var points = timelinePoints?.ToArray() ?? Array.Empty<DataPoint>();
        var minimumDataX = points.Length > 0 ? points.Min(p => p.X) : now;
        var maximumDataX = points.Length > 0 ? points.Max(p => p.X) : now;
        var defaultVisibleStart = maximumDataX - SecondsToAxisUnits(DefaultTimelineSeconds);
        var absoluteMinimum = Math.Min(defaultVisibleStart, minimumDataX - SecondsToAxisUnits(TimelinePaddingSeconds));
        var absoluteMaximum = Math.Max(maximumDataX + SecondsToAxisUnits(TimelinePaddingSeconds), absoluteMinimum + SecondsToAxisUnits(TimelineMinimumRangeSeconds));
        var rangeSeconds = Math.Min(
            TimelineMaximumRangeSeconds,
            Math.Max(TimelineMinimumRangeSeconds, (absoluteMaximum - absoluteMinimum) * 86400.0));

        axis.AbsoluteMinimum = absoluteMinimum;
        axis.AbsoluteMaximum = absoluteMaximum;
        axis.Minimum = Math.Max(absoluteMinimum, maximumDataX - SecondsToAxisUnits(DefaultTimelineSeconds));
        axis.Maximum = absoluteMaximum;
        axis.MinimumRange = SecondsToAxisUnits(TimelineMinimumRangeSeconds);
        axis.MaximumRange = SecondsToAxisUnits(rangeSeconds);
    }

    private static void ApplyLinearAxisBounds(
        PlotModel model,
        AxisPosition position,
        double absoluteMinimum,
        double absoluteMaximum,
        double visibleMinimum,
        double visibleMaximum,
        double minimumRange,
        double maximumRange,
        string? key = null)
    {
        var axis = model.Axes.FirstOrDefault(a =>
            a.Position == position && (key == null || string.Equals(a.Key, key, StringComparison.Ordinal)));
        if (axis == null)
            return;

        visibleMinimum = Clamp(visibleMinimum, absoluteMinimum, absoluteMaximum);
        visibleMaximum = Clamp(visibleMaximum, absoluteMinimum, absoluteMaximum);
        EnsureMinimumSpan(ref visibleMinimum, ref visibleMaximum, minimumRange, absoluteMinimum, absoluteMaximum);

        axis.AbsoluteMinimum = absoluteMinimum;
        axis.AbsoluteMaximum = absoluteMaximum;
        axis.Minimum = visibleMinimum;
        axis.Maximum = visibleMaximum;
        axis.MinimumRange = minimumRange;
        axis.MaximumRange = maximumRange;
    }

    private static void EnsureMinimumSpan(
        ref double minimum,
        ref double maximum,
        double minimumRange,
        double absoluteMinimum,
        double absoluteMaximum)
    {
        if (maximum - minimum >= minimumRange)
            return;

        var center = (minimum + maximum) / 2.0;
        minimum = center - minimumRange / 2.0;
        maximum = center + minimumRange / 2.0;

        if (minimum < absoluteMinimum)
        {
            maximum += absoluteMinimum - minimum;
            minimum = absoluteMinimum;
        }

        if (maximum > absoluteMaximum)
        {
            minimum -= maximum - absoluteMaximum;
            maximum = absoluteMaximum;
        }
    }

    private static double Clamp(double value, double minimum, double maximum)
        => Math.Max(minimum, Math.Min(maximum, value));

    private static double SecondsToAxisUnits(double seconds) => seconds / 86400.0;

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
