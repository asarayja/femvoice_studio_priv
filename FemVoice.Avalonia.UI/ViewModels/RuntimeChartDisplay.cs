using System;

namespace FemVoice.Avalonia.ViewModels;

/// <summary>
/// Immutable, DISPLAY-ONLY snapshot of the runtime pitch-chart's scalar state: the (fixed-per-session)
/// axis range, the target band, and the current-pitch marker — all expressed in a fixed pixel coordinate
/// space ("px from the chart bottom") that the converter-free Avalonia trace shares. NO OxyPlot, NO value
/// converters, NO clinical decision. The recent pitch trace itself is a separate ObservableCollection of
/// px heights on the view-model (kept there so it can be appended incrementally without per-frame rebuilds).
/// </summary>
public sealed class RuntimeChartDisplay
{
    /// <summary>Fixed chart surface height in px (the Canvas Height binds to this — single source of truth).</summary>
    public double ChartHeightPx { get; init; } = 200;
    public double ChartMinPitch { get; init; }
    public double ChartMaxPitch { get; init; }
    public double TargetPitchMin { get; init; }
    public double TargetPitchMax { get; init; }
    /// <summary>Target-band edges as px-from-bottom (Canvas.Bottom / Height for the band rectangle).</summary>
    public double TargetBandBottomPx { get; init; }
    public double TargetBandTopPx { get; init; }
    public double TargetBandHeightPx { get; init; }
    public double CurrentPitch { get; init; }
    /// <summary>Current-pitch marker as px-from-bottom (Canvas.Bottom for the marker line).</summary>
    public double CurrentPitchMarkerPx { get; init; }
    public bool HasVoice { get; init; }
    public string ChartStatusText { get; init; } = FemVoice.Avalonia.Localization.Localized.Get("Chart_WaitingForVoice", "Venter på stemme …");

    /// <summary>Map a pitch (Hz) to px-from-bottom within [min,max] over a chart of heightPx, clamped to [0,heightPx].</summary>
    public static double ToPx(double hz, double min, double max, double heightPx)
    {
        if (max <= min) return 0;
        double frac = (hz - min) / (max - min);
        if (frac < 0) frac = 0;
        else if (frac > 1) frac = 1;
        return frac * heightPx;
    }

    public static RuntimeChartDisplay Empty(double heightPx, double chartMin, double chartMax,
        double targetMin, double targetMax)
        => Build(heightPx, chartMin, chartMax, targetMin, targetMax, currentPitch: 0, hasVoice: false,
            statusText: FemVoice.Avalonia.Localization.Localized.Get("Chart_WaitingForVoice", "Venter på stemme …"));

    public static RuntimeChartDisplay From(double heightPx, double chartMin, double chartMax,
        double targetMin, double targetMax, double currentPitch, bool hasVoice, string statusText)
        => Build(heightPx, chartMin, chartMax, targetMin, targetMax, currentPitch, hasVoice, statusText);

    private static RuntimeChartDisplay Build(double heightPx, double chartMin, double chartMax,
        double targetMin, double targetMax, double currentPitch, bool hasVoice, string statusText)
    {
        double bandBottom = ToPx(targetMin, chartMin, chartMax, heightPx);
        double bandTop = ToPx(targetMax, chartMin, chartMax, heightPx);
        return new RuntimeChartDisplay
        {
            ChartHeightPx = heightPx,
            ChartMinPitch = Math.Round(chartMin, 0),
            ChartMaxPitch = Math.Round(chartMax, 0),
            TargetPitchMin = targetMin,
            TargetPitchMax = targetMax,
            TargetBandBottomPx = bandBottom,
            TargetBandTopPx = bandTop,
            TargetBandHeightPx = Math.Max(1, bandTop - bandBottom),
            CurrentPitch = hasVoice ? Math.Round(currentPitch, 1) : 0,
            CurrentPitchMarkerPx = hasVoice ? ToPx(currentPitch, chartMin, chartMax, heightPx) : 0,
            HasVoice = hasVoice,
            ChartStatusText = statusText,
        };
    }
}
