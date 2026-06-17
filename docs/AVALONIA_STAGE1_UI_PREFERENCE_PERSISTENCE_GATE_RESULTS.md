# Avalonia Stage 1 — UI-Preference Persistence — Gate Results

Date: 2026-06-17 · Branch: `avalonia-stage1-ui-preference-persistence-slice` (off `main` @ `4c3840b`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Stage 1: harmless Avalonia-local UI-preference persistence (theme/language/reduce-motion). Persistence only —
> NO runtime activation; no DB/UserSettings/WPF ThemeManager/LocalizationService; no clinical/audio/WPF change.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (31 — all OK, all exit 0)
`--smoke` … `--settings-persistence-readiness-smoke` (30 prior; readiness smoke updated to the post-Stage-1
guardrail) + **`--settings-preferences-persistence-smoke` (new, 31st)** → **31/31 OK.**
- `--settings-preferences-persistence-smoke`: `defaults=True saved=True reload=True corruptFallback=True normalizeLang=True pathLocal=True saveFailureGraceful=True` (the last added in controlled review — fail-safe Save).
- `--settings-persistence-readiness-smoke` (updated): `notDisposable=True sectionsInert=True scanned=True noWpfHooks=True noRuntimeActivation=True`.
- `--settings-smoke` / `--settings-visual-parity-smoke` (inert sections + VM shape) remain green; `--avalonia-localization-coverage-smoke` green (7 new `Settings_LocalPrefs_*` keys registered in the backlog).

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- No WPF settings/localization dependency; no `IDatabaseService`/`ThemeManager`/`MicrophoneCalibration`/`SessionAnalyticsStore`/engine refs; forbidden tokens (non-comment): **clean**. **Core `Strings.*.resx` git-clean.**

## Packaging verification (readiness intact)
- `publish-linux.sh linux-x64` → OK; **17/17 published smokes exit 0** (incl. `--settings-preferences-persistence`).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb`; `publish-macos.sh osx-x64` → OK; `package-app.sh` → unsigned `.app`; `package-dmg.sh` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures; unchanged — Core untouched). No new failures.

## Behaviour change
Stage-1 harmless local UI-preference persistence (theme/language/reduce-motion) via an Avalonia-owned JSON file +
one interactive Settings card. **No runtime activation**, no DB/UserSettings/WPF ThemeManager/LocalizationService,
no audio/clinical/domain/WPF change. Behaviour-heavy Settings sections remain inert.
