# Avalonia Exercise Runtime — Gate Results

Date: 2026-06-16 · Host: Ubuntu 26.04, .NET 10.0.301 · Branch: `avalonia-exercise-runtime-slice`.

## Build (Linux) — ✅ GREEN
`FemVoice.Avalonia` build: **0 warnings, 0 errors**.

## Smokes — ✅ ALL PASS
- `--smoke`: OK
- `--dashboard-smoke`: OK
- `--exercise-smoke`: OK (Exercises: 15)
- `--exercise-runtime-smoke`:
```
Exercise: Grunnleggende humming
Target: 140-180 Hz
Pitch: 160.0 Hz
Status: Innenfor målområde
Hold: 0.7s (14%)  Elapsed: 0:00
Navigation: runtime=True back-to-detail=True
Exercise runtime smoke OK
```
→ Synthetic pitch lands in the exercise target band; display-only hold accumulates; detail → runtime → back navigation verified. (Elapsed shows 0:00 because the smoke runs < 1 s.)

## Vulnerability scan — ✅ CLEAN
`dotnet list FemVoice.Avalonia package --vulnerable --include-transitive` → **no vulnerable packages** (Tmds.DBus.Protocol 0.21.3 pin retained).

## Leak guard — ✅ CLEAN
`FemVoice.Avalonia` references **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`. Source/AXAML grep for `System.Windows` / `Microsoft.Win32` / `MessageBox` / `OxyPlot.Wpf` / `FemVoice.Audio.Windows` / `NAudioCaptureService` / `WaveInEvent` / `WasapiCapture` / WPF `ThemeManager` / `LocExtension` / `LocConverter`: **no references**.

## Portable tests — baseline (no regression)
`FemVoice.Tests.Portable`: **1570 passed / 10 failed / 1580 total** (the 10 pre-existing localization-data failures; occasional 1569/11 = documented ComfortZone timing flake). This slice changed no portable code — no new regression.

## Windows build (not run here)
Not run on this Linux host. WPF unaffected (slice touches only `FemVoice.Avalonia`). The `Windows WPF Verification` workflow re-runs when the PR is opened.

## Verdict
Exercise Runtime scaffold builds and verifies (headless) on Linux; dashboard/guide/detail navigation preserved; no Windows-only leak; no new vulnerable packages; no behaviour change. Ready for review.
