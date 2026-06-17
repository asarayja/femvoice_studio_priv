# Avalonia Analysis / Resonance Charts Scaffold — Slice Plan

Date: 2026-06-17 · Branch: `avalonia-analysis-resonance-scaffold-slice` (off `main` @ `6431fbb`, incl. PR #1–#12).

> **Status: IMPLEMENTED (Linux-verified, headless).** Display-only analysis/resonance scaffold; synthetic/
> in-memory sample data only. No clinical/domain change · no WPF change · no Android/iOS · no real mic · no
> persistence · no session-history reads/writes · no report export · no SmartCoach/progression · no
> safety-gate · no Voice-Health/recovery · no OxyPlot. See `_SLICE_REPORT.md` / `_GATE_RESULTS.md`.

## 1. Goal
Represent the WPF Analysis/Resonance surface in Avalonia as a reachable, display-only scaffold built on the
existing shell + theme + localization foundations, rendering converter-free mini-charts from synthetic
in-memory sample data. The Analysis nav item becomes implemented; other missing surfaces stay deferred.

## 2. Scope (implemented)
- **New** `ViewModels/AnalysisViewModel.cs` — a purely static page (no services, no commands, not IDisposable,
  no timers/subscriptions/capture, no DB/history reads). Generates 4 deterministic synthetic `AnalysisSeries`
  (Pitch trend, Resonance, Stability, Formant placeholder) as converter-free bar heights (px), plus 4 static
  `AnalysisSummaryMetric` placeholders. `AllActionsDeferred => true`.
- **New** `Views/AnalysisView.axaml` (+ `.axaml.cs`) — cards via nested `ItemsControl`; each mini-chart is an
  `ItemsControl` of bottom-aligned `Rectangle`s (`Height="{Binding}"` = px bar), exactly like the runtime
  trace; static summary rows; **disabled** import/export buttons (no command). Shell theme brushes; no converters; no OxyPlot.
- **Edit** `ViewModels/ShellViewModel.cs` — retained inert `_analysis` singleton + `ShowAnalysis` command; the
  "Analyse" nav item is now **implemented**; destination label + disposal guard updated.
- **Edit** `MainWindow.axaml` — `DataTemplate` for `AnalysisViewModel`.
- **Edit** `Program.cs` — `--analysis-scaffold-smoke`; updated `--shell-smoke` (4 implemented / 5 deferred).

## 3. Synthetic data
All series are deterministic (`mid + amplitude*sin(i*freq+phase)`, clamped to [4, ChartHeightPx=120]) — no
random, no real audio, no DB. Summary metrics are static "(eksempel)" placeholders, including "Økter
analysert: — (ingen lagring)" to make the no-persistence posture explicit.

## 4. Safety / what is NOT done
No OxyPlot (`Wpf` or `Avalonia`); no database/`SessionAnalyticsStore`/`IDatabaseService`/`ExerciseSessionRecorder`
reads or writes; no report export; no clinical score recalculation/FemVoice-score change; no SmartCoach/
progression; no Voice-Health/recovery; no real mic/NAudio/Wasapi/WaveIn; no exercise-definition/target-profile/
WPF change. Labels resolve read-only via `Localized.Get` (namespaced `Analysis_*` keys with Norwegian fallback).

## 5. Lifecycle
Analysis is a retained singleton (not disposed; not IDisposable), starting no work. The shell's transient-page
disposal is preserved and exercised: navigating to Analysis from a running runtime disposes the runtime (stops
synthetic capture; no orphan frames; no duplicate runtime).

## 6. Smoke (`--analysis-scaffold-smoke`)
Headless: Analysis nav item exists and is implemented; navigating switches `CurrentPage` to `AnalysisViewModel`;
the VM is inert (not IDisposable via reflection; no `IRelayCommand`); ≥3 synthetic series each with bars + a
title; summary placeholders present + `AllActionsDeferred`; and navigating to Analysis from a running runtime
disposes it (`IsRunning==false`, trace stops growing → no orphaned capture).

## 7. Gate
`dotnet build` (0 warnings) · all 12 smokes OK · `dotnet list --vulnerable` clean · `FemVoice.Tests.Portable`
baseline (1570/1580; 1569 acceptable due to the known ComfortZone flake) · leak guard clean (base +
analysis-specific incl. OxyPlot/SessionAnalyticsStore/ReportExport) · Windows CI via PR.
