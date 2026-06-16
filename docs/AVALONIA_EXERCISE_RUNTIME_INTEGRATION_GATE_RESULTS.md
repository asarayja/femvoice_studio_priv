# Avalonia Exercise Runtime Integration — Gate Results

Date: 2026-06-16 · Host: Ubuntu 26.04, .NET 10.0.301 · Branch: `avalonia-exercise-runtime-integration-slice`.

## Build (Linux) — ✅ GREEN
`FemVoice.Avalonia` build: **0 warnings, 0 errors**.

## Smokes — ✅ ALL PASS (5)
- `--smoke`: OK
- `--dashboard-smoke`: OK
- `--exercise-smoke`: OK (Exercises: 15)
- `--exercise-runtime-smoke`: OK
- `--exercise-runtime-integration-smoke`:
```
Exercises: 15
Mapped profiles: 15/15
Fallback profiles: 0/15
First: Grunnleggende humming
Profile: ResonanceHumming
RequiredHoldSeconds: 3 s
Resonance: 0–1  Stability: ≥ 0,45  Skills: Resonans, Stabilitet
HoldTarget: Mål: hold i 3 s (visning)
Runtime: OK
Navigation: runtime=True back-to-detail=True
Exercise runtime integration smoke OK
```
→ All 15 exercises map to a profile (0 fallback); the runtime surfaces the target profile and uses `RequiredHoldSeconds` (3 s for ResonanceHumming) as the display-only hold target; nav detail→runtime→back works.

## Vulnerability scan — ✅ CLEAN
`dotnet list FemVoice.Avalonia package --vulnerable --include-transitive` → **no vulnerable packages** (Tmds.DBus.Protocol 0.21.3 pin retained).

## Leak guard — ✅ CLEAN
`FemVoice.Avalonia` references **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`. Source/AXAML grep for `System.Windows` / `Microsoft.Win32` / `MessageBox` / `OxyPlot.Wpf` / `FemVoice.Audio.Windows` / `NAudioCaptureService` / `WaveInEvent` / `WasapiCapture` / WPF `ThemeManager` / `LocExtension` / `LocConverter`: **no references**.

## Portable tests — baseline (no regression)
`FemVoice.Tests.Portable`: **1570 passed / 10 failed / 1580 total** (the 10 pre-existing localization-data failures; occasional 1569/11 = documented ComfortZone timing flake). This slice changed no portable code.

## Windows build (not run here)
Not run on this Linux host. WPF unaffected (only `FemVoice.Avalonia` changed). The `Windows WPF Verification` workflow re-runs when the PR is opened.

## Verdict
Read-only target-profile integration builds and verifies (headless) on Linux; all 15 exercises mapped; runtime/dashboard/guide/detail nav preserved; no Windows-only leak; no new vulnerable packages; no behaviour change. Ready for review.
