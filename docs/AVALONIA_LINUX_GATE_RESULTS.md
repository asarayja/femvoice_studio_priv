# Avalonia / Linux Regression Gate (Agent 5)

Date: 2026-06-16 · Host: Ubuntu 26.04, linux-x64 · SDK: .NET 10.0.301 (`~/.dotnet`).
Run after adding `FemVoice.Audio.Windows` to confirm the Windows audio work did **not** break Linux/Avalonia.

## Builds — ✅ GREEN
| Project | Result |
| --- | --- |
| FemVoice.Audio.Abstractions | Build succeeded |
| FemVoice.Core | Build succeeded |
| FemVoice.Avalonia | Build succeeded (2 warnings: transitive `Tmds.DBus.Protocol` NU1903 advisory) |

## Portable tests — ✅ no regression
`FemVoice.Tests.Portable`: **1570 passed / 10 failed / 1580 total** — identical to the pre-audio-adapter baseline (the 10 are pre-existing localization-data failures, see `LINUX_PORTABLE_GATE_RESULTS.md`; plus the occasional `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` timing flake). The Audio.Windows work touched no portable code.

## Avalonia headless smoke — ✅ PASS
`dotnet run --project FemVoice.Avalonia -- --smoke` → `[smoke] OK: shared FemVoice.Core services resolve on Linux via the Avalonia head DI.` (Noop capture, `Common_Yes → "Ja"`.)

## Windows-only dependency guard — ✅ CLEAN
`FemVoice.Avalonia` references **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions` (`dotnet list reference` confirms — **NOT** `FemVoice.Audio.Windows`).

Source grep of `FemVoice.Avalonia/**` for `System.Windows`, `Microsoft.Win32`, `MessageBox`, `OxyPlot.Wpf`, `NAudio` capture (`WaveInEvent`/`WasapiCapture`), `FemVoice.Audio.Windows`, WPF `ThemeManager`: **no code references** (the only matches are explanatory comments). The Linux synthetic/noop audio path is intact.

> Note: `FemVoice.Core` references the NAudio **package** for cross-platform FFT math (`NAudio.Dsp`) used by `ResonanceProxyEngine`, so NAudio is transitively present in the Avalonia output — but **no NAudio capture API** is used on Linux. This is the same as before this gate and is not a Windows-only-capture leak.

## Post-push regression rerun (after `git push -u origin linux-portable-core`)
- Avalonia build: **succeeded**. Smoke: **OK**.
- Portable tests: **1569 passed / 11 failed** this run — i.e. the 10 pre-existing localization-data failures **plus** the documented intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` timing flake (counts oscillate 10↔11 across runs). **No new regression** (nothing changed since the adapter; the push does not alter code).
- Avalonia reference guard: still only `FemVoice.Core` + `FemVoice.Audio.Abstractions` (no `FemVoice.Audio.Windows`).

## Dependency security re-verification (NU1903 — Tmds.DBus.Protocol, 2026-06-16)
After pinning the transitive `Tmds.DBus.Protocol` to **0.21.3** (branch `deps-nu1903-tmds-dbus`; see `DEPENDENCY_SECURITY_NOTES.md`):
- `FemVoice.Avalonia` build: **succeeded, 0 warnings (NU1903 gone), 0 errors**.
- `dotnet list --vulnerable --include-transitive`: **no vulnerable packages**.
- `--smoke`: **OK**. Portable tests: **1570/1580** (no regression).
- Avalonia reference guard: still only `FemVoice.Core` + `FemVoice.Audio.Abstractions` — the Tmds pin is a direct package reference on the Avalonia head only; **no Windows-only dependency introduced**.

## Verdict
Avalonia still builds and starts (headless) on Linux; no Windows-only capture/UI dependency leaked in via the Audio.Windows addition or the Tmds.DBus.Protocol security pin. Linux portability preserved.
