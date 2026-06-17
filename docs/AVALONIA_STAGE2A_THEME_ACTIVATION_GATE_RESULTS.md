# Avalonia Stage 2A — Theme Activation — Gate Results

Date: 2026-06-17 · Branch: `avalonia-stage2a-theme-activation-slice` (off `main` @ `3a8bfbf`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Stage 2A: Avalonia-only runtime activation of the saved THEME preference (startup + on Save). Language and
> reduce-motion remain persisted-only (not activated). No DB/UserSettings/WPF ThemeManager/LocalizationService;
> no audio/clinical/Core/WPF change.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (32 — all OK, all exit 0)
31 prior + **`--settings-theme-activation-smoke` (new, 32nd)** → **32/32 OK.**
- `--settings-theme-activation-smoke`: `mapOk=True darkApplied=True lightApplied=True systemApplied=True missingSafe=True corruptSafe=True noLanguageActivation=True`.
- `--settings-persistence-readiness-smoke` (updated): `notDisposable=True sectionsInert=True scanned=True noWpfHooks=True noLangActivation=True`.
- `--visual-baseline-smoke` (Stage-2A-aware): with no saved pref → `actualVariant='Dark'`; with a saved Light pref → `actualVariant='Light'`; `theme-matches-baseline-or-savedpref=True` in both cases.
- `--settings-smoke` / `--settings-visual-parity-smoke` / `--settings-preferences-persistence-smoke` / `--avalonia-localization-coverage-smoke` remain green.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Leak guard clean (no WPF/Windows/DB/clinical/engine refs; no WPF `ThemeManager`/`LocalizationService`).
- No language/culture activation anywhere (only the readiness smoke's own detection-token arrays mention culture strings). **Core `Strings.*.resx` git-clean.** Diff scope: only `FemVoice.Avalonia/` + `docs/`.

## Packaging verification
- `publish-linux.sh linux-x64` → OK; **18/18 published smokes exit 0** (incl. `--settings-theme-activation-smoke`).
- `package-deb.sh linux-x64` → built `femvoice-studio_0.1.0_amd64.deb`; `publish-macos.sh osx-x64` → OK; `package-app.sh` → unsigned `.app`; `package-dmg.sh` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures; unchanged — Core untouched). No new failures.

## Behaviour change
Avalonia-only runtime activation of the saved THEME preference (startup + live on Save), preserving the dark
baseline when no/invalid preference is saved. Language + reduce-motion remain persisted-only. No DB/UserSettings/WPF
ThemeManager/LocalizationService; no audio/clinical/domain/Core/WPF change. Behaviour-heavy Settings sections inert.
