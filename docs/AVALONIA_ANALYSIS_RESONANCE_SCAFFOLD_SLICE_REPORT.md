# Avalonia Analysis / Resonance Charts Scaffold — Slice Report

Date: 2026-06-17 · Branch: `avalonia-analysis-resonance-scaffold-slice` (off `main` @ `6431fbb`).

> **Display-only analysis/resonance scaffold; synthetic/in-memory sample data only.** No clinical/domain
> behaviour changed · no WPF behaviour changed · no Android/iOS · no real mic · no persistence · no
> session-history reads/writes · no report export · no SmartCoach/progression · no safety-gate enforcement ·
> no Voice-Health/recovery · no OxyPlot.

## 1. What this slice does
Adds a reachable, display-only Analysis/Resonance page to the Avalonia shell. Analysis is now an implemented
nav destination; the other missing WPF surfaces remain deferred placeholders. The page shows converter-free
mini bar-charts (Pitch trend / Resonance / Stability / Formant placeholder) over synthetic in-memory sample
data, plus static summary placeholders, with clear "sample data / not persisted" messaging.

## 2. Files changed
- **New** `FemVoice.Avalonia/ViewModels/AnalysisViewModel.cs` — static page (no services/commands; not IDisposable);
  `AnalysisSeries` + `AnalysisSummaryMetric`; 4 deterministic synthetic series + 4 summary placeholders; `AllActionsDeferred => true`.
- **New** `FemVoice.Avalonia/Views/AnalysisView.axaml` (+ `.axaml.cs`) — converter-free cards + nested `ItemsControl`
  bar-charts (Rectangle `Height="{Binding}"`); static summary rows; disabled import/export buttons; shell theme brushes; no OxyPlot.
- **Edit** `FemVoice.Avalonia/ViewModels/ShellViewModel.cs` — inert `_analysis` singleton + `ShowAnalysis`; "Analyse"
  nav item implemented; destination label + disposal guard updated.
- **Edit** `FemVoice.Avalonia/MainWindow.axaml` — `DataTemplate` for `AnalysisViewModel`.
- **Edit** `FemVoice.Avalonia/Program.cs` — `--analysis-scaffold-smoke`; updated `--shell-smoke` (4 implemented / 5 deferred).
- **Docs** this report + `_SLICE_PLAN.md` + `_GATE_RESULTS.md` + tracker.

No files under `FemVoiceStudio/`, `FemVoice.Core/` (read-only `Localized.Get`), or `FemVoice.Audio.Windows/`.

## 3. Inertness + synthetic data (verified)
`AnalysisViewModel` holds no services, exposes no commands (no `IRelayCommand`), is not `IDisposable`, starts no
work, and reads/writes nothing. The 4 series are deterministic sine-shaped bar heights (no random, no audio, no
DB); summary metrics are static "(eksempel)" placeholders incl. "Økter analysert: — (ingen lagring)".
`--analysis-scaffold-smoke` asserts this behaviorally (reflection: no-IDisposable / no-IRelayCommand; series + summary present).

## 4. Display-only / no forbidden behaviour
No OxyPlot, no database/`SessionAnalyticsStore`/`IDatabaseService`/`ExerciseSessionRecorder`, no report export, no
clinical scoring/FemVoice-score change, no SmartCoach/progression, no Voice-Health/recovery, no real mic/NAudio/
Wasapi/WaveIn/microphone calibration. WPF analysis behaviour untouched.

## 5. Lifecycle safety
Analysis is a retained singleton (not disposed; not IDisposable). The shell's transient-page disposal is
preserved: navigating to Analysis from a running runtime disposes the runtime (stops synthetic capture; trace
stops growing → no orphaned capture; no duplicate runtime). Verified by `--analysis-scaffold-smoke`.

## 6. Verification (see `_GATE_RESULTS.md`)
Build 0 warnings · all 12 smokes OK (incl. `--analysis-scaffold-smoke`) · no vulnerable packages · leak guard
clean (base + analysis-specific) · refs only Core + Audio.Abstractions · Tmds 0.21.3 · portable 1570/1580
(1569 known flake) · Windows CI = pending PR.

## 7. Behaviour changes
**None to clinical/domain behaviour. WPF untouched.** All additions are display-only Analysis scaffold over synthetic data.
