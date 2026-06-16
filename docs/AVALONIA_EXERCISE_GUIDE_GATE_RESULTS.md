# Avalonia Exercise Guide — Gate Results

Date: 2026-06-16 · Host: Ubuntu 26.04, .NET 10.0.301 · Branch: `avalonia-exercise-guide-slice`.

## Build (Linux) — ✅ GREEN
`FemVoice.Avalonia` build: **0 warnings, 0 errors**. (Core + Audio.Abstractions unchanged, green.)

## Smokes — ✅ PASS
- `--smoke`: OK (shared services resolve via DI).
- `--dashboard-smoke`: OK (dashboard VM still drives pitch/stability/health from synthetic audio — navigation refactor did not break it).
- `--exercise-smoke`:
```
Exercises: 15
First: Grunnleggende humming
Categories: Avansert, Intonasjon, Oppvarming, Pitch-kontroll, Praksis, Pust, Resonans, Stabilitet
Detail: OK
Detail title='Grunnleggende humming', steps=5, targetPitch=140–180 Hz
nav: dashboard=True guide=True detail=True back-to-guide=True
Exercise smoke OK
```
→ Catalog loads **15** exercises; detail opens with title + instructions + target metadata; shell navigation dashboard → guide → detail → back-to-guide all verified.

## Vulnerability scan — ✅ CLEAN
`dotnet list FemVoice.Avalonia package --vulnerable --include-transitive` → **no vulnerable packages** (Tmds.DBus.Protocol 0.21.3 pin retained from main).

## Leak guard — ✅ CLEAN
`FemVoice.Avalonia` references **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`. Source/AXAML grep for `System.Windows` / `Microsoft.Win32` / `MessageBox` / `OxyPlot.Wpf` / `FemVoice.Audio.Windows` / `NAudioCaptureService` / `WaveInEvent` / `WasapiCapture` / WPF `ThemeManager` / `LocExtension` / `LocConverter`: **no references**.

## Portable tests — baseline (no regression)
`FemVoice.Tests.Portable`: **1570 passed / 10 failed** baseline (this run reported 1569/11 — the documented intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` timing flake; counts oscillate 10↔11). The 10 stable failures are pre-existing localization-data issues; **this slice changed no portable code** (only the Avalonia head), so there is no new regression.

## Windows build (not run here)
Not run on this Linux host. WPF is unaffected (this slice only touches `FemVoice.Avalonia`). The `Windows WPF Verification` workflow re-runs when the PR is opened.

## Verdict
Exercise Guide + Detail slice builds and verifies (headless) on Linux; dashboard navigation preserved; no Windows-only dependency leak; no new vulnerable packages; no behaviour change. Ready for review.
