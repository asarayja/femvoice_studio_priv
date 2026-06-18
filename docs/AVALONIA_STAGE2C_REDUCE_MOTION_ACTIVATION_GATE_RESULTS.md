# Avalonia Stage 2C — Reduce-Motion Activation — Gate Results

Date: 2026-06-18 · Branch: `avalonia-stage2c-reduce-motion-activation-slice` (off `main` @ `e2cc282`) · Host: Linux (.NET 10 user-local `~/.dotnet`, `DISPLAY=:0`).

> Stage 2C: Avalonia-only reduce-motion activation (startup + live on Save) via an Avalonia-owned motion-preference
> state. No animations exist yet → present visual effect is intentionally a no-op; the preference is active and
> ready for future Avalonia motion effects. Theme (2A) + language (2B) intact. No WPF/Core/DB change.

## Build
`dotnet build FemVoice.Avalonia/FemVoice.Avalonia.csproj` → **Build succeeded. 0 Warning(s) 0 Error(s).**

## Smokes (34 — all OK, all exit 0)
- `--settings-reduce-motion-activation-smoke`: `trueLoaded=True falseLoaded=True missingSafe=True corruptSafe=True saveAppliesLive=True themeStillWorks=True languageStillWorks=True`.
- `--settings-theme-activation-smoke`, `--settings-language-activation-smoke`, `--settings-persistence-readiness-smoke`, `--settings-preferences-persistence-smoke`, `--settings-smoke`, `--settings-visual-parity-smoke`, `--avalonia-localization-coverage-smoke` green.

## Vulnerable packages
**none**. `Tmds.DBus.Protocol` resolved `0.21.3` == requested.

## Reference / leak guard
- `FemVoice.Avalonia` references: **only** `FemVoice.Core` + `FemVoice.Audio.Abstractions`.
- Leak guard clean (no WPF/Windows/DB/clinical/engine refs; no WPF `ThemeManager`/`LocalizationService`/`LocExtension`/`LocConverter`). No global thread-culture change. **Core `Strings.*.resx` git-clean.** Diff scope: only `FemVoice.Avalonia/` + `docs/`.

## Packaging verification
- `publish-linux.sh linux-x64` → OK; **20/20 published smokes exit 0** (incl. `--settings-reduce-motion-activation-smoke`).
- `package-deb.sh linux-x64` → `.deb` built; `publish-macos.sh osx-x64` → OK; `package-app.sh` → unsigned `.app`; `package-dmg.sh` → graceful skip on Linux.

## Portable tests
`dotnet test FemVoice.Tests.Portable` → **1570/1580** (10 known localization-data baseline failures; unchanged — Core untouched). No new failures.

## Behaviour change
Avalonia-only reduce-motion activation (startup + live on Save) via the Avalonia-owned `MotionActivation` state;
truthful copy across all 20 languages. No animations exist yet → no visible motion change today (documented). No
DB/UserSettings/WPF ThemeManager/LocalizationService; no global thread-culture change; no Core/WPF/clinical/audio
change. Theme (2A) + language (2B) intact; behaviour-heavy Settings sections remain inert.
