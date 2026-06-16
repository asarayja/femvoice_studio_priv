# Avalonia Main Dashboard — Slice Plan

Date: 2026-06-16 · Branch: `avalonia-main-dashboard-slice` (off `main` after PR #1 merge).

## Goal
First real Avalonia dashboard screen showing live pitch/signal/stability/health/feedback driven by the **shared, UI-free** services (`FemVoice.Core`) from a platform-neutral `IAudioCaptureService` (synthetic on Linux). No clinical/domain behaviour changed; no Windows-only dependency in `FemVoice.Avalonia`.

## In scope (this slice)
- `MainDashboardViewModel` (Avalonia-safe; uses shared services read-only).
- Dashboard layout: Start/Stop, current pitch, signal status, stability, health, comfort-zone, feedback area, difficulty selector, synthetic-audio mode selector, pitch-trace area, navigation + professional-tools placeholders, FluentTheme skeleton, Norwegian labels.
- Synthetic audio modes (StablePitch / UnstablePitch / PitchRampUp / PitchRampDown / Silence).
- Headless verification (`--dashboard-smoke`).

## Out of scope (later slices)
Full Exercise Guide, SmartCoach detail, Progression dashboard, Reports/professional tools, Settings parity, real Linux mic capture, Android, full theme parity, full localization audit, OxyPlot.Avalonia chart parity, full FeedbackPipeline/VocalHealthSupervisor/FemVoiceScoreEngine wiring.

## Architecture
```
SyntheticAudioCaptureService (FemVoice.Audio.Abstractions)
   └─ IAudioCaptureService.FrameAvailable (float frames)
        └─ MainDashboardViewModel (FemVoice.Avalonia/ViewModels)
             ├─ PitchDetectionService (YIN)            [FemVoice.Core, read-only]
             ├─ PitchTraceStabilizer                   [FemVoice.Core, read-only]
             ├─ LiveMetricsService (smoothing/stability/health) [FemVoice.Core, read-only]
             ├─ PitchTargetZonePolicy (comfort zone)   [FemVoice.Core, read-only]
             └─ IUiDispatcher (AvaloniaUiDispatcher / InlineUiDispatcher for smoke)
        └─ MainWindow.axaml (binds to the VM)
```
The VM deliberately does **not** port the WPF `MainViewModel` (which is WPF-coupled); it re-expresses the same dashboard using shared services.

## Build/verify (Linux)
`dotnet build FemVoice.Avalonia` (green), `--smoke` + `--dashboard-smoke` (green), `FemVoice.Tests.Portable` (1570/1580, no regression), no-Windows-leak guard (Avalonia refs only Core + Abstractions). See `AVALONIA_MAIN_DASHBOARD_GATE_RESULTS.md`.
