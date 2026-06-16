# Avalonia Chart Port Notes

Date: 2026-06-16.

## Current state (this slice): converter-free placeholder
The dashboard pitch chart is a **placeholder**, not the OxyPlot port. It is an `ItemsControl` over `MainDashboardViewModel.PitchSamples` (recent stabilized pitch values), each rendered as a thin bottom-aligned `Rectangle` whose `Height` (px) equals the pitch value (Hz). Within the ~240 px chart area this maps the 150–240 Hz target band cleanly and updates live. No value converter and no third-party charting dependency are involved, so it builds reliably and has no Avalonia/OxyPlot version risk.

Shown: live pitch-trace (newest at right), empty-state hint, comfort-zone numbers (`ComfortZoneLow/High`).
Not yet shown: a true time axis, a Hz Y-axis with ticks, a shaded comfort-zone band overlay, zoom/pan bounds, OxyPlot styling parity with the WPF charts.

## Why OxyPlot.Avalonia was deferred (not "ready" in this slice)
- The WPF app uses `OxyPlot.Wpf`; the portable plan is `OxyPlot.Avalonia`. The PlotModel-building code (`PitchChartViewModel`, `ResonanceChartViewModel`, `AnalysisPageViewModel`) is largely portable, but:
  - `OxyPlot.Avalonia` package/version compatibility with Avalonia 11.2.1 needs validation (binding/host control changes across OxyPlot 2.x).
  - `AnalysisChartTheme` reads WPF brushes (`System.Windows.Media` + `Application.Current.TryFindResource`) — its brush source must be abstracted (inject colors) before reuse.
- A first dashboard slice does not need full chart parity; a reliable placeholder that visibly updates is sufficient and keeps the build green.

## Recommended next step (chart slice)
1. Add `OxyPlot.Avalonia` to `FemVoice.Avalonia` and validate it builds against Avalonia 11.2.1.
2. Port `PitchChartViewModel`'s `PlotModel` (pitch `LineSeries` + comfort-zone `AreaSeries`) into a shared/Avalonia-safe builder; host it in an Avalonia `PlotView`.
3. Replace `AnalysisChartTheme`'s WPF-brush reads with injected colors (`IThemeResourceProvider`), keeping the OxyColor mapping shared.
4. Preserve the WPF chart's bounds/zoom/pan limits and empty-state styling.
