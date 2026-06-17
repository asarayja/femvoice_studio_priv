# Avalonia Localization & Text Polish — Gate Results

Date: 2026-06-17 · Branch: `avalonia-localization-text-polish-slice` (off `main` @ `e05e8b5`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Text/localization only — behavior-neutral. No runtime language switching/persistence; no clinical/domain or WPF
> change; no WPF localization dependency.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (28 — all OK, all exit 0)
`--smoke` … `--visual-layout-polish-smoke` (27 prior) + **`--localization-text-polish-smoke` (new, 28th)** → **28/28 OK.**
- `--localization-text-polish-smoke`: `coachLabels=True progLabels=True settingsAudioNorsk=True privacyShort=True noEnglishLeftovers=True deferredConsistent=True dashLabelTonehøyde=True`.
- `--theme-loc-smoke` remains green.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- No WPF localization dependency (`LocExtension`/`LocConverter`); forbidden WPF/audio/database/clinical tokens (non-comment): **clean**.

## Packaging verification (readiness intact)
- `publish-linux.sh linux-x64` → OK; **14/14 published smokes exit 0** (incl. `--localization-text-polish`).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb` (unsigned, unchanged).
- `publish-macos.sh osx-x64` → OK; `package-app.sh osx-x64` → unsigned `.app`; `package-dmg.sh osx-x64` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures); **1569/1580**
acceptable when the intermittent `ComfortZoneControllerTests.ZoneUpdated_EventRaisedOnUpdate` flake fires. No new
failures (this slice changes no test-compiled code).

## Behaviour change
**None to clinical/domain behaviour. WPF untouched.** Avalonia display-label text only: scaffold labels switched to
Norwegian-consistent scaffold keys (SmartCoach tiles, Progression params/score, Settings audio/privacy, Dashboard
"Tonehøyde"). No runtime language switching, no persistence, no Core resx change.
